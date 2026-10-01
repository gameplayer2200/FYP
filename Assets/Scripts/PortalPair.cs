using UnityEngine;

/// <summary>
/// 传送门配对（最终版数学）：
/// 两个世界各待在原坐标（Additive 同载，相距 72m+ 零重叠）。
/// demoEff = demo 门朝向 × 180°：demo 世界的"内容"在 demo 门 raw -Z 侧，
/// 引入 demoEff 后两扇门在各自 eff 系里语义统一（-Z=内容侧，+Z=门外侧）。
/// 门洞屏：Unlit RT，Unity Quad 法线实测朝 local -Z。
/// 穿行规则：各自门框 raw -Z→+Z 穿越触发瞬移，落点=对方门框内容侧，朝向+180°。
/// </summary>
public class PortalPair : MonoBehaviour
{
    [Header("连接")]
    [SerializeField] private string worldSceneName = "DemoScene"; // 异世界场景名（须在 Build Settings）
    [SerializeField] private Transform labFrame;                  // 实验室门框
    [SerializeField] private string worldDoorName = "anywhere-door"; // 异世界门框名（包含匹配，支持 anywhere-door-open 等变体）

    [Header("门洞屏")]
    [SerializeField] private Vector2 portalSize = new Vector2(0.97f, 1.72f); // 门洞宽×高
    [SerializeField] private float portalCenterHeight = 0.86f;  // 门洞中心离门底高度
    [SerializeField] private int rtWidth = 384;
    [SerializeField] private int rtHeight = 680;
    [SerializeField] private float renderRange = 15f;           // 玩家离门多近才开启渲染

    private Transform demoFrame;
    private Transform labQuad, demoQuad;
    private Transform playerT;
    private Camera playerCam;
    private Camera labViewCam;   // 站实验室门框，渲染实验室 → 异世界侧门洞屏
    private Camera demoViewCam;  // 站异世界门框，渲染异世界 → 实验室侧门洞屏
    private bool ready;
    private float cooldown;
    private float prevZLab, prevZDemo;

#if UNITY_EDITOR
    private void Awake()
    {
        bool found = false;
        foreach (var s in UnityEditor.EditorBuildSettings.scenes)
        {
            if (s.path.Replace('\\', '/').EndsWith(worldSceneName + ".unity") && s.enabled) { found = true; break; }
        }
        if (!found)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(worldSceneName + " t:Scene");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var list = new System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
                list.Add(new UnityEditor.EditorBuildSettingsScene(path, true));
                UnityEditor.EditorBuildSettings.scenes = list.ToArray();
                Debug.Log("[PortalPair] 已自动注册场景到 Build Settings: " + path);
            }
        }
    }
#endif

    private void Start()
    {
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(worldSceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError("[PortalPair] LoadSceneAsync 返回 null：" + worldSceneName);
            return;
        }
        op.completed += OnWorldLoaded;
    }

    private void OnWorldLoaded(AsyncOperation op)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(worldSceneName);
        string key = worldDoorName.ToLower();
        foreach (var rootObj in scene.GetRootGameObjects())
        {
            // 精确名优先，其次包含匹配（anywhere-door / anywhere-door-open 等变体通吃）
            Transform f = rootObj.transform.Find(worldDoorName);
            if (f == null)
            {
                foreach (var t in rootObj.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.ToLower().Contains(key)) { f = t; break; }
                }
            }
            if (f != null) { demoFrame = f; break; }
        }
        if (demoFrame == null || labFrame == null)
        {
            Debug.LogError("[PortalPair] 找不到两边门框 lab=" + (labFrame != null) + " demo=" + (demoFrame != null));
            return;
        }

        // 异世界门（anywhere-door-open）：姿态以场景手工摆放为准（常开），程序不做任何旋转/开关操控
        // 穿门由本类的门框触发检测负责，玩家 E 不作用于它

        // 禁用异世界自带相机
        foreach (var rootObj in scene.GetRootGameObjects())
            foreach (var cam in rootObj.GetComponentsInChildren<Camera>(true))
                cam.gameObject.SetActive(false);

        var player = GameObject.Find("player");
        if (player == null) { Debug.LogError("[PortalPair] 找不到 player"); return; }
        playerT = player.transform;
        playerCam = player.GetComponentInChildren<Camera>();

        // 屏与取景：两块 Plane 都是"门洞屏"（显示对方世界），相机各自站在对方 Plane 的位置、
        // 沿其正面反方向穿过门洞拍摄——画面透视与门洞完全对应
        float quadAspect = Mathf.Max(portalSize.x, 1.3f) / Mathf.Max(portalSize.y, 2.1f);
        int rtW = Mathf.RoundToInt(rtHeight * quadAspect);
        var rtDemo = MakeRT(rtW);
        var rtLab = MakeRT(rtW);

        // 门洞屏：门模型自带的 Plane 一律停用（嵌在门体深处，画面像贴在盒底），
        // 统一用贴门洞平面的自建屏（大于门洞、边缘藏进门框后，向内容侧微缩防 z-fight）
        Transform modelPlane = labFrame.Find("Cube.001/Plane");
        if (modelPlane != null) modelPlane.gameObject.SetActive(false);
        Transform modelPlane2 = FindPlaneInTree(demoFrame);
        if (modelPlane2 != null) modelPlane2.gameObject.SetActive(false);
        labQuad = MakeQuad(labFrame, true, rtDemo, "PortalQuad_Lab");
        demoQuad = MakeQuad(demoFrame, false, rtLab, "PortalQuad_Demo");
        // 门框 Cube.001 带黑色背板子网格，会挡在贴门洞的新屏前面（用户最早诊断过的坑）——
        // 把两侧门 Cube.001 链上的深色材质槽换成全透明（粉色件不受影响）
        MakeFrameBlackInvisible(labFrame.root);
        MakeFrameBlackInvisible(demoFrame.root);

        // 相机取景：站在对方门洞屏的位置，面朝各自世界的内容方向（demo 内容在门框 -Z，lab 内容也在门框 -Z）
        demoViewCam = MakeCam(demoFrame, rtDemo, "PortalCam_DemoView");
        labViewCam = MakeCam(labFrame, rtLab, "PortalCam_LabView");
        // 藏门：每台取景相机渲染时隐藏自己所在世界的门整体（门框/门板/门洞屏），防套娃
        demoViewCam.gameObject.AddComponent<PortalViewMask>().SetHidden(demoFrame.root.gameObject);
        labViewCam.gameObject.AddComponent<PortalViewMask>().SetHidden(labFrame.root.gameObject);
        // 项目没有天空盒材质（实验室天空为 null）：给两台传送门相机配程序化蓝天，
        // 否则门洞上半截是纯白背景——"开门一片白"的主因之一
        if (RenderSettings.skybox == null && Shader.Find("Skybox/Procedural") != null)
        {
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            demoViewCam.GetComponent<PortalViewMask>().SetSkybox(sky);
            labViewCam.GetComponent<PortalViewMask>().SetSkybox(sky);
        }
        // 初始位姿锚在门洞平面（Update 的视点同步每帧覆盖，仅作首帧兜底）
        demoViewCam.transform.position = demoFrame.position + demoFrame.up * portalCenterHeight;
        demoViewCam.transform.rotation = demoFrame.rotation * Quaternion.Euler(0f, 180f, 0f);
        labViewCam.transform.position = labFrame.position + labFrame.up * portalCenterHeight;
        labViewCam.transform.rotation = labFrame.rotation * Quaternion.Euler(0f, 180f, 0f);

        prevZLab = LocalZ(labFrame, false);
        prevZDemo = LocalZ(demoFrame, true);
        ready = true;
        Debug.Log("[PortalPair] 传送门已建立 labFrame=" + labFrame.position.ToString("F1") +
                  " demoFrame=" + demoFrame.position.ToString("F1"));
    }

    // 玩家在门框 raw local 系里的 z（内容侧判定用）
    private float LocalZ(Transform frame, bool isDemo)
    {
        return frame.InverseTransformPoint(playerT.position).z;
    }

    // 把门 Cube.001（含子物体）渲染器上的深色材质槽换成全透明（_Color 最亮分量<0.15 判定）
    private void MakeFrameBlackInvisible(Transform doorRoot)
    {
        Transform framePart = FindCube001(doorRoot);
        if (framePart == null) return;
        var invisible = new Material(Shader.Find("Unlit/Transparent"));
        invisible.color = new Color(1f, 1f, 1f, 0f);
        foreach (var r in framePart.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m != null && m.HasProperty("_Color") && m.color.maxColorComponent < 0.15f)
                {
                    mats[i] = invisible;
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
        }
        Debug.Log("[PortalPair] 门框黑色背板已透明：" + framePart.name);
    }

    private Transform FindCube001(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("Cube.001")) return t;
        return null;
    }

    private Transform FindPlaneInTree(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name.ToLower().Contains("plane")) return t;
        return null;
    }

    private RenderTexture MakeRT(int width)
    {
        var rt = new RenderTexture(width, rtHeight, 24);
        rt.Create();
        return rt;
    }

    private Transform MakeQuad(Transform frame, bool normalTowardMinusZ, RenderTexture rt, string name)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        float recess = normalTowardMinusZ ? 0.04f : -0.04f;
        quad.transform.position = frame.position + frame.up * portalCenterHeight + frame.forward * recess;
        // Quad 法线实测朝 local -Z：normalTowardMinusZ=true → 不翻转（法线朝 frame -Z）
        quad.transform.rotation = frame.rotation * (normalTowardMinusZ ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
        quad.transform.localScale = new Vector3(Mathf.Max(portalSize.x, 1.3f), Mathf.Max(portalSize.y, 2.1f), 1f);
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var r = quad.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = rt;
        r.material = mat;
        return quad.transform;
    }

    private Camera MakeCam(Transform frame, RenderTexture rt, string name)
    {
        var go = new GameObject(name);
        go.transform.position = frame.position + frame.up * portalCenterHeight;
        go.transform.rotation = frame.rotation;
        var cam = go.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 120f;
        cam.aspect = (float)rtWidth / rtHeight;
        cam.enabled = false;
        return cam;
    }

    private static readonly Matrix4x4 FlipY180 = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

    // 虚拟相机摆到玩家眼位的镜像位姿；视场角按"相机到门洞屏的垂直距离"动态算——
    // 让取景锥体在门洞平面上的截面恰好等于门洞屏（窗户光学：近看视野大、远看视野窄），
    // 否则玩家 60° 广角被压进小门洞，门里世界看起来像缩小模型
    private void SyncPortalCam(Camera cam, Matrix4x4 portalTransform, Transform screen)
    {
        cam.transform.SetPositionAndRotation(
            portalTransform.MultiplyPoint3x4(playerCam.transform.position),
            portalTransform.rotation * playerCam.transform.rotation);
        float perpDist = Mathf.Abs(Vector3.Dot(cam.transform.position - screen.position, screen.forward));
        float quadH = Mathf.Max(portalSize.y, 2.1f);
        cam.fieldOfView = 2f * Mathf.Atan(quadH * 0.5f / Mathf.Max(0.3f, perpDist)) * Mathf.Rad2Deg;
        cam.aspect = Mathf.Max(portalSize.x, 1.3f) / quadH;
    }

    // demo 门的"有效朝向"：内容侧定义到 raw +Z（即 raw 转半圈）
    private Quaternion DemoEff()
    {
        return demoFrame.rotation * Quaternion.Euler(0f, 180f, 0f);
    }

    private void Update()
    {
        if (!ready || playerCam == null) return;
        if (cooldown > 0f) cooldown -= Time.deltaTime;

        Quaternion demoEff = DemoEff();

        // 视点同步（视差）：虚拟相机 = 玩家眼位经"门→门"传送变换的镜像，人动画面动。
        // 不用斜裁剪投影（实测斜视角下会把可视区裁成细条），套娃由 PortalViewMask 解决
        float dLab = Vector3.Distance(playerT.position, labFrame.position);
        float dDemo = Vector3.Distance(playerT.position, demoFrame.position);
        if (dLab < renderRange)
        {
            demoViewCam.enabled = true;
            SyncPortalCam(demoViewCam, demoFrame.localToWorldMatrix * FlipY180 * labFrame.worldToLocalMatrix, demoQuad);
        }
        else demoViewCam.enabled = false;
        if (dDemo < renderRange)
        {
            labViewCam.enabled = true;
            SyncPortalCam(labViewCam, labFrame.localToWorldMatrix * FlipY180 * demoFrame.worldToLocalMatrix, labQuad);
        }
        else labViewCam.enabled = false;



        if (cooldown <= 0f)
        {
            TryCross(labFrame, true, ref prevZLab, demoFrame, demoEff);
            TryCross(demoFrame, false, ref prevZDemo, labFrame, labFrame.rotation);
        }
    }

    // 穿行触发：raw local z 从 -（内容侧）穿到 +（门外侧）
    // lab：-→+ 进异世界；demo：-→+ 回实验室。落点=对方门框内容侧，朝向+180°
    private void TryCross(Transform frame, bool isLab, ref float prevZ, Transform otherFrame, Quaternion otherEff)
    {
        Vector3 local = frame.InverseTransformPoint(playerT.position);
        bool inOpening = Mathf.Abs(local.x) < 1.1f && local.y > -0.3f && local.y < 2.4f;
        bool crossed = inOpening && prevZ < 0f && local.z >= 0f && local.z < 0.8f && prevZ > -0.8f;
        prevZ = local.z;
        if (!crossed) return;

        cooldown = 0.6f;
        // 落点：本门 local 原样映射到对方 eff 系（z 已是"内容侧为负"语义）
        Vector3 target = otherFrame.position + otherEff * local;
        var cc = playerT.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerT.position = target;
        if (cc != null) cc.enabled = true;
        playerT.rotation = Quaternion.Euler(0f, playerT.eulerAngles.y + 180f, 0f);
        prevZLab = labFrame.InverseTransformPoint(playerT.position).z;
        prevZDemo = demoFrame.InverseTransformPoint(playerT.position).z;
        Debug.Log("[PortalPair] 穿门瞬移 -> " + otherFrame.name + " @ " + target.ToString("F1"));
    }
}
