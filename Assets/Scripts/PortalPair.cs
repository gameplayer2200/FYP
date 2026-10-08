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
    [SerializeField] private Color portalColor = new Color(0.25f, 0.95f, 0.75f); // 门洞挡板颜色（每对门一色，标识传送目的地）
    [SerializeField] private Vector2 portalSize = new Vector2(0.97f, 1.72f); // 门洞宽×高
    [SerializeField] private float portalCenterHeight = 0.86f;  // 门洞中心离门底高度

    private Transform demoFrame;
    private Transform labQuad, demoQuad;
    private AnywhereDoor labDoor, demoDoor;   // 开关同步用：两侧门的开关组件
    private bool lastLabOpen, lastDemoOpen;
    private Transform playerT;
    private Camera playerCam;
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

        // 屏与取景：纯色挡板方案（放弃实时渲染）——每对门一个识别色，开门=色板可见，
        // 关门=门板遮挡。RT 尺寸参数保留但不再使用
        // 门洞屏：门模型自带的 Plane 一律停用（嵌在门体深处，画面像贴在盒底），
        // 统一用贴门洞平面的自建挡板（纯色发光）
        Transform modelPlane = labFrame.Find("Cube.001/Plane");
        if (modelPlane != null) modelPlane.gameObject.SetActive(false);
        Transform modelPlane2 = FindPlaneInTree(demoFrame);
        if (modelPlane2 != null) modelPlane2.gameObject.SetActive(false);
        labQuad = MakeQuad(labFrame, true, "PortalQuad_Lab");
        demoQuad = MakeQuad(demoFrame, false, "PortalQuad_Demo");
        // 门框 Cube.001 带黑色背板子网格，会挡在贴门洞的新屏前面（用户最早诊断过的坑）——
        // 把两侧门 Cube.001 链上的深色材质槽换成全透明（粉色件不受影响）。
        // 注意只扫门自身子树：门若嵌在世界根下，扫 .root 会误伤整个世界的同名部件
        MakeFrameBlackInvisible(labFrame);
        MakeFrameBlackInvisible(demoFrame);

        // 开关同步初始化：两侧门配成一对。anywhere-door 预制体本身不带开关脚本
        // （实验室侧是场景实例上手动加的），远端门若没有就运行时补挂到与实验室门板同名的叶子上
        labDoor = labFrame.GetComponentInChildren<AnywhereDoor>(true);
        demoDoor = demoFrame.GetComponentInChildren<AnywhereDoor>(true);
        if (demoDoor == null && labDoor != null)
        {
            foreach (var t in demoFrame.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == labDoor.gameObject.name)
                {
                    demoDoor = t.gameObject.AddComponent<AnywhereDoor>();
                    demoDoor.ConfigureOpenAngle(labDoor.OpenAngle); // 摆角跟实验室侧一致（默认90°，实验室调过-180°）
                    Debug.Log("[PortalPair] 远端门缺开关脚本，已补挂到叶子 " + t.name);
                    break;
                }
            }
        }
        if (labDoor != null && demoDoor != null)
        {
            demoDoor.SetRespondToKey(true); // 远端也要能 E：穿过去后门自动关了人得能从那边开回来
            if (demoDoor.IsOpen != labDoor.IsOpen) demoDoor.SetOpen(labDoor.IsOpen, false);
            lastLabOpen = labDoor.IsOpen;
            lastDemoOpen = labDoor.IsOpen;
            Debug.Log("[PortalPair] 门开关已配对同步（初始=" + (labDoor.IsOpen ? "开" : "关") + "）");
        }
        else Debug.LogWarning("[PortalPair] 开关同步未启用：labDoor=" + (labDoor != null) + " demoDoor=" + (demoDoor != null));

        // 纯色挡板方案：无需取景相机。相机创建/视点同步/藏门遮罩整体移除
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

    // 把门 Cube.001（含子物体）上的"背板"材质槽换成全透明——背板颜色不定（有黑有白），
    // 判定取"中性色"（RGB 三通道接近且极亮或极暗），品红门框（饱和色）不受影响
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
                if (m == null) continue;
                if (!m.HasProperty("_Color"))
                {
                    // URP 系材质（主色属性叫 _BaseColor）在内置管线下读不到颜色——
                    // Cube.001 上的这类槽就是背板，直接透明
                    mats[i] = invisible;
                    changed = true;
                    continue;
                }
                Color c = m.color;
                bool neutral = Mathf.Abs(c.r - c.g) < 0.12f && Mathf.Abs(c.g - c.b) < 0.12f
                               && (c.maxColorComponent < 0.18f || c.maxColorComponent > 0.82f);
                if (neutral)
                {
                    mats[i] = invisible;
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
        }
        Debug.Log("[PortalPair] 门框背板已透明（中性色规则）：" + framePart.name);
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

    private Transform MakeQuad(Transform frame, bool normalTowardMinusZ, string name)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        float s = Mathf.Max(0.1f, frame.lossyScale.y); // 门洞尺寸随门的实际缩放
        float recess = normalTowardMinusZ ? 0.04f : -0.04f;
        quad.transform.position = frame.position + frame.up * (portalCenterHeight * s) + frame.forward * recess;
        // Quad 法线实测朝 local -Z：normalTowardMinusZ=true → 不翻转（法线朝 frame -Z）
        quad.transform.rotation = frame.rotation * (normalTowardMinusZ ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
        quad.transform.localScale = new Vector3(portalSize.x * s, portalSize.y * s, 1f);
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var r = quad.GetComponent<MeshRenderer>();
        // 纯色发光挡板：Unlit 不受灯光影响颜色恒定，Emission 让暗处也发光（传送门的"能量膜"观感）
        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = portalColor;
        r.material = mat;
        return quad.transform;
    }



    // 门洞屏宽高比（按门的实际缩放）
    private float QuadAspect(Transform frame)
    {
        float s = Mathf.Max(0.1f, frame.lossyScale.y);
        return (portalSize.x * s) / Mathf.Max(0.3f, portalSize.y * s);
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

        // 开关同步：任一侧状态翻转（E 交互或自动关）立即镜像到另一侧。
        // 镜像用 SetOpen(x, false) 不排自动关——计时器只挂在被交互的那扇门上
        if (labDoor != null && demoDoor != null)
        {
            bool lo = labDoor.IsOpen;
            bool dmo = demoDoor.IsOpen;
            if (lo != lastLabOpen)
            {
                lastLabOpen = lo; lastDemoOpen = lo;
                demoDoor.SetOpen(lo, false);
            }
            else if (dmo != lastDemoOpen)
            {
                lastDemoOpen = dmo; lastLabOpen = dmo;
                labDoor.SetOpen(dmo, false);
            }
        }

        Quaternion demoEff = DemoEff();

        // 挡板随门开关显隐：关门=看到门板，开门=看到发光色板（传送激活的视觉语言）
        bool portalOn = labDoor == null || labDoor.IsOpen;
        if (labQuad != null && labQuad.gameObject.activeSelf != portalOn)
            labQuad.gameObject.SetActive(portalOn);
        if (demoQuad != null && demoQuad.gameObject.activeSelf != portalOn)
            demoQuad.gameObject.SetActive(portalOn);

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

        // 门关着不许穿（物理上门板碰撞体也该挡住，这里是判定层兜底——
        // 贴门框边缘挤过门洞平面也不会被传送）。两侧门状态已同步，查本侧即可
        AnywhereDoor gate = isLab ? labDoor : demoDoor;
        if (gate != null && !gate.IsOpen) return;

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
