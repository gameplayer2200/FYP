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
        int rtW = Mathf.RoundToInt(rtHeight * Mathf.Max(1f, playerCam.aspect));
        var rtDemo = MakeRT(rtW);
        var rtLab = MakeRT(rtW);

        // 实验室屏：Cube.001/Plane（找不到则自建 quad）
        var planeT = labFrame.Find("Cube.001/Plane");
        if (planeT != null)
        {
            labQuad = planeT;
            var planeCol = planeT.GetComponent<Collider>();
            if (planeCol != null) Destroy(planeCol);
            var planeMr = planeT.GetComponent<MeshRenderer>();
            if (planeMr == null) planeMr = planeT.gameObject.AddComponent<MeshRenderer>();
            var planeMat = new Material(Shader.Find("Unlit/Texture"));
            planeMat.mainTexture = rtDemo;
            planeMr.material = planeMat;
            Debug.Log("[PortalPair] 实验室门洞屏 = Cube.001/Plane");
        }
        else
        {
            labQuad = MakeQuad(labFrame, true, rtDemo, "PortalQuad_Lab");
        }

        // 异世界屏：anywhere-door-open 下的 Plane（找不到则自建 quad）
        Transform demoPlane = null;
        foreach (var t in demoFrame.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLower().Contains("plane")) { demoPlane = t; break; }
        }
        if (demoPlane != null)
        {
            demoQuad = demoPlane;
            var dCol = demoPlane.GetComponent<Collider>();
            if (dCol != null) Destroy(dCol);
            var dMr = demoPlane.GetComponent<MeshRenderer>();
            if (dMr == null) dMr = demoPlane.gameObject.AddComponent<MeshRenderer>();
            var dMat = new Material(Shader.Find("Unlit/Texture"));
            dMat.mainTexture = rtLab;
            dMr.material = dMat;
            Debug.Log("[PortalPair] 异世界门洞屏 = " + demoPlane.name);
        }
        else
        {
            demoQuad = MakeQuad(demoFrame, false, rtLab, "PortalQuad_Demo");
        }

        // 相机取景：站在对方门洞屏的位置，面朝各自世界的内容方向（demo 内容在门框 -Z，lab 内容也在门框 -Z）
        demoViewCam = MakeCam(demoFrame, rtDemo, "PortalCam_DemoView");
        labViewCam = MakeCam(labFrame, rtLab, "PortalCam_LabView");
        // 藏门：每台取景相机渲染时隐藏自己所在世界的门整体（门框/门板/门洞屏），防套娃
        demoViewCam.gameObject.AddComponent<PortalViewMask>().SetHidden(demoFrame.root.gameObject);
        labViewCam.gameObject.AddComponent<PortalViewMask>().SetHidden(labFrame.root.gameObject);
        if (demoPlane != null)
        {
            demoViewCam.transform.position = demoPlane.position;
            demoViewCam.transform.rotation = demoFrame.rotation * Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            demoViewCam.transform.position = demoFrame.position + demoFrame.up * portalCenterHeight;
            demoViewCam.transform.rotation = demoFrame.rotation * Quaternion.Euler(0f, 180f, 0f);
        }
        if (planeT != null)
        {
            labViewCam.transform.position = planeT.position;
            labViewCam.transform.rotation = labFrame.rotation * Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            labViewCam.transform.position = labFrame.position + labFrame.up * portalCenterHeight;
            labViewCam.transform.rotation = labFrame.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

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
        quad.transform.localScale = new Vector3(portalSize.x, portalSize.y, 1f);
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

    // 虚拟相机摆到玩家眼位的镜像位姿，并对齐玩家视场
    private void SyncPortalCam(Camera cam, Matrix4x4 portalTransform)
    {
        cam.transform.SetPositionAndRotation(
            portalTransform.MultiplyPoint3x4(playerCam.transform.position),
            portalTransform.rotation * playerCam.transform.rotation);
        cam.fieldOfView = playerCam.fieldOfView;
        cam.aspect = playerCam.aspect;
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
            SyncPortalCam(demoViewCam, demoFrame.localToWorldMatrix * FlipY180 * labFrame.worldToLocalMatrix);
        }
        else demoViewCam.enabled = false;
        if (dDemo < renderRange)
        {
            labViewCam.enabled = true;
            SyncPortalCam(labViewCam, labFrame.localToWorldMatrix * FlipY180 * demoFrame.worldToLocalMatrix);
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
