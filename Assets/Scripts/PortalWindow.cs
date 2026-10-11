using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 传送门实时取景渲染器（正确版）。
///
/// ── 和之前失败版本的区别 ──────────────────────────────────────────────
/// 旧做法：虚拟相机用「玩家全屏视锥 + 屏幕宽高比」渲染进 RT，再整张贴到门洞。
///   门洞只占屏幕一小块，等于把一整幅广角画面压进一扇窄门 → 比例和视差都错。
/// 本做法：**偏心视锥**——虚拟相机的视锥精确穿过目标门洞的四个角，
///   门洞里露出的就是那个世界「该露出的那一片」，人走动时透视自动正确。
///
/// ── 关键认识 ─────────────────────────────────────────────────────────
/// 门是一扇窗。透过窗看到什么，只取决于「眼睛的位置」和「窗框」，
/// 与眼睛朝向无关。所以：
///   1) 虚拟相机的位置 = 玩家眼位经「门→门」等距变换映射过去；
///   2) 虚拟相机的朝向可以任取（取成垂直于目标门洞平面，便于构造简单视锥）；
///   3) 投影矩阵由目标门洞四角反算。
///
/// ── 自检 ─────────────────────────────────────────────────────────────
/// 菜单 Tools/传送门/数值自检，会把目标门洞四角投影回 NDC，
/// 应当精确落在 (±1, ±1)。这一步不依赖肉眼。
/// </summary>
[DisallowMultipleComponent]
// 版本标记：v1（改这一行可强制 Unity 重编译 / 域重载，用于刷新运行时 Build Settings 缓存）
public class PortalWindow : MonoBehaviour
{
    [Header("连接")]
    [SerializeField] private string worldSceneName = "Metro";                 // 目标场景（须在 Build Settings）
    [SerializeField] private string worldDoorName = "anywhere-door-Metro";    // 目标场景里的门对象名（包含匹配）
    [SerializeField] private string openingPath = "Cube.001/Plane";           // 门洞平面（相对门根）
    [Tooltip("门洞内的实体门板节点名（会被隐藏，好让取景屏露出来）。多个用逗号分隔。留空则不隐藏任何东西。")]
    [SerializeField] private string panelName = "Cube,Cube.001";              // 门板本体 + 外框（相对门根）

    [Header("取景")]
    [SerializeField] private int rtHeight = 720;            // RT 高度（宽度按门洞宽高比推）
    [SerializeField] private float farClip = 200f;
    [SerializeField] private float nearClip = 0.05f;
    [SerializeField] private float preloadRadius = 15f;     // 玩家离本门多近才渲染取景

    [Header("约定（看画面调，改完立刻生效）")]
    [Tooltip("门→门映射是否额外绕 Y 轴 180°。默认对齐现有 PortalPair 的传送数学")]
    [SerializeField] private bool mapYaw180 = true;
    [Tooltip("画面左右镜像时勾上")]
    [SerializeField] private bool mirrorU = false;
    [Tooltip("显示左上角的调试信息（最后设成 false）")]
    [SerializeField] private bool showDebugOsd = true;

    [Header("材质")]
    [SerializeField] private Material quadMaterialOverride;  // 留空则运行时用 Unlit/Texture 生成

    // ── 运行时 ──
    private Transform srcFrame;          // 本门根（实验室侧）
    private Transform srcPlane;          // 本门门洞平面
    private Transform destFrame;         // 目标门根
    private Transform destPlane;         // 目标门门洞平面
    private Camera portalCam;
    private RenderTexture rt;
    private Transform quad;              // 贴在门洞上的取景屏
    private MeshRenderer quadMr;
    private Material quadMat;
    private Camera playerCam;
    private bool ready;
    private bool loading;
    private float waitStart = -1f;       // 进入预加载半径的时刻（用于给 PortalPair 让路）
    private readonly Vector3[] srcCorners = new Vector3[4];
    private readonly Vector3[] destCorners = new Vector3[4];
    private int[] uOrder = new int[4];   // 角点 → u 归属
    private int[] vOrder = new int[4];
    private Transform[] legacyQuads;     // PortalPair 生成的纯色挡板（需压制）
    private int quadScanFrame = -1;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        srcFrame = transform;
        srcPlane = FindOpening(srcFrame);
        if (srcPlane == null)
            Debug.LogError("[PortalWindow] 找不到门洞平面 " + openingPath + "（挂在 " + srcFrame.name + " 下）");
    }

    // 所有取景器实例登记表：渲染任意一台取景相机时，需要把「所有」取景屏都藏掉，
    // 否则两个方向的相机互相拍到对方的屏 → 无限递归 → 画面锁死成一片纯色。
    private static readonly System.Collections.Generic.List<PortalWindow> all =
        new System.Collections.Generic.List<PortalWindow>();
    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }
    /// <summary>取景屏的 Renderer（供其它取景器在渲染时临时隐藏）。</summary>
    public MeshRenderer QuadRenderer { get { return quadMr; } }

    // 先按配置路径找，找不到就退化为「子树里名字含 plane 的节点」
    private Transform FindOpening(Transform door)
    {
        var t = door.Find(openingPath);
        if (t != null) return t;
        foreach (var c in door.GetComponentsInChildren<Transform>(true))
            if (c != door && c.name.ToLower().Contains("plane")) return c;
        return null;
    }

    private void Start()
    {
        playerCam = FindPlayerCamera();
    }

    /// <summary>
    /// 找"渲染世界"的主相机。
    /// 本项目玩家相机没有打 MainCamera 标签（Camera.main 恒为 null），
    /// 且 player 下有两个相机：depth=0 的 world 相机 + depth=1 的 FPCamera（手持物叠加，只渲染第 6 层）。
    /// 所以：排除有 targetTexture 的、排除只清深度的叠加相机，再取 depth 最小的那个。
    /// </summary>
    private static Camera FindPlayerCamera()
    {
        if (Camera.main != null) return Camera.main;

        Camera best = null;
        var all = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null || c.targetTexture != null) continue;
            if (c.clearFlags == CameraClearFlags.Depth || c.clearFlags == CameraClearFlags.Nothing) continue; // 叠加相机
            if (best == null || c.depth < best.depth) best = c;
        }
        return best;
    }

    private void Update()
    {
        if (ready)
        {
            if (playerCam == null) playerCam = FindPlayerCamera();
            return;
        }
        if (playerCam == null) playerCam = FindPlayerCamera();
        if (playerCam == null || srcFrame == null) return;
        if ((playerCam.transform.position - srcFrame.position).sqrMagnitude > preloadRadius * preloadRadius) return;

        // 目标场景可能已由 PortalPair 加载好了 —— 优先直接复用，绝不重复加载
        if (SceneManager.GetSceneByName(worldSceneName).isLoaded) { OnWorldLoaded(null); return; }
        if (loading) return;

        // 给 PortalPair 一点"先加载"的机会：等 2 秒仍未加载，才由自己兜底加载。
        // 两边同时 LoadSceneAsync 会载入两份同样的场景，叠加渲染直接把帧率拖死。
        if (waitStart < 0f) waitStart = Time.time;
        if (Time.time - waitStart >= 2f) BeginLoad();
    }

    private void LateUpdate()
    {
        if (playerCam == null) playerCam = FindPlayerCamera();
        if (playerCam == null) return;

        // 压制 PortalPair 的纯色板必须无条件执行：玩家穿到对面世界后离本门很远，
        // 一旦跟着"距离太远就不渲染"一起跳过，对面门洞里就会露出那块白板。
        SuppressLegacyQuads();

        if (!ready) return;
        if ((playerCam.transform.position - srcFrame.position).sqrMagnitude > preloadRadius * preloadRadius * 4f)
            return; // 离得远就不渲染取景，省性能

        UpdatePortalCamera();
        RenderPortal();
    }

    /// <summary>
    /// PortalPair 会自己造一块纯色挡板（PortalQuad_*，是场景根物体），开门时它会激活并盖住取景。
    /// 这里定期把它们压下去。每隔 60 帧才扫一次根物体，开销可忽略。
    /// </summary>
    private void SuppressLegacyQuads()
    {
        if (legacyQuads == null || legacyQuads.Length == 0 || Time.frameCount - quadScanFrame > 60)
        {
            quadScanFrame = Time.frameCount;
            var list = new System.Collections.Generic.List<Transform>();
            var scene = srcFrame.gameObject.scene;
            if (scene.IsValid())
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name.StartsWith("PortalQuad_")) list.Add(root.transform);
            legacyQuads = list.ToArray();
        }
        for (int i = 0; i < legacyQuads.Length; i++)
        {
            if (legacyQuads[i] == null) continue;
            if (legacyQuads[i].gameObject.activeSelf) legacyQuads[i].gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 加载目标世界并搭建取景
    // ─────────────────────────────────────────────────────────────
    private void BeginLoad()
    {
        if (loading || ready) return;
        loading = true;
        if (SceneManager.GetSceneByName(worldSceneName).isLoaded) { loading = false; OnWorldLoaded(null); return; }
        var op = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
        if (op == null) { Debug.LogError("[PortalWindow] LoadSceneAsync 返回 null：" + worldSceneName); loading = false; return; }
        op.completed += OnWorldLoaded;
    }

    private void OnWorldLoaded(AsyncOperation op)
    {
        if (ready) return;   // 幂等：只初始化一次，避免重复创建相机/取景屏
        var scene = SceneManager.GetSceneByName(worldSceneName);
        if (!scene.isLoaded) { Debug.LogError("[PortalWindow] 目标场景未加载：" + worldSceneName); return; }

        string key = worldDoorName.ToLower();
        foreach (var root in scene.GetRootGameObjects())
        {
            Transform f = root.transform.Find(worldDoorName);
            if (f == null)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.ToLower().Contains(key)) { f = t; break; }
            if (f != null) { destFrame = f; break; }
        }
        if (destFrame == null) { Debug.LogError("[PortalWindow] 目标场景里找不到门：" + worldDoorName); return; }

        destPlane = destFrame.Find(openingPath);
        if (destPlane == null) { Debug.LogError("[PortalWindow] 目标门找不到门洞平面：" + openingPath); return; }

        // 停掉目标世界自带的相机，避免抢画面。
        // 但绝不能停玩家自己的相机——如果目标就是当前活动场景（例如从异世界回头取景实验室），
        // 停相机等于把玩家搞瞎。
        if (scene != SceneManager.GetActiveScene())
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Camera>(true))
                    c.gameObject.SetActive(false);
        }

        ReadCorners(srcPlane, srcCorners);
        ReadCorners(destPlane, destCorners);
        BuildQuad();
        BuildCamera();
        ComputeUvOrder();
        ApplyUvs();

        // 关掉旧的纯色挡板（PortalPair 生成的 PortalQuad_*），免得盖住取景
        foreach (var t in srcFrame.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("PortalQuad_")) t.gameObject.SetActive(false);

        // 隐藏「门洞内的实体门板」：panelName 支持逗号分隔多个节点名。
        // 这个任意门预制件用 Cube（门板本体，1.22×2.18×0.36）+ Cube.001（外框，1.41×2.39×0.37）
        // 把门洞堵死，不隐藏它们的话取景屏永远看不到。门框立柱（Cube.002/003/Cylinder…）保留，门才不散架。
        if (!string.IsNullOrEmpty(panelName))
        {
            foreach (var nm in panelName.Split(','))
            {
                var nodeName = nm.Trim();
                if (nodeName.Length == 0) continue;
                var panel = srcFrame.Find(nodeName);
                if (panel == null) { Debug.LogWarning("[PortalWindow] 门板节点不存在：" + nodeName); continue; }
                foreach (var r in panel.GetComponentsInChildren<Renderer>(true))
                    // 排除门把手：door knob 是 Cube（前门板）的子物体，
                    // GetComponentsInChildren 会把它一起捞出来，必须显式跳过，否则把手也被藏掉。
                    if (r != null && r.name != "door knob") r.enabled = false;
            }
        }

        ready = true;
        Debug.Log("[PortalWindow] 取景已建立：源=" + srcFrame.name + " 目标=" + destFrame.name
                  + " RT=" + rt.width + "x" + rt.height);
    }

    // 门洞四角（世界坐标）：c0=-r-u  c1=+r-u  c2=+r+u  c3=-r+u
    private static void ReadCorners(Transform plane, Vector3[] outC)
    {
        var mf = plane.GetComponent<MeshFilter>();
        Vector3 hx, hy;
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            hx = plane.TransformVector(new Vector3(b.extents.x, 0f, 0f));
            hy = plane.TransformVector(new Vector3(0f, 0f, b.extents.z));
        }
        else
        {
            hx = plane.right * 0.5f;
            hy = plane.up * 1f;
        }
        Vector3 c = plane.position;
        outC[0] = c - hx - hy;
        outC[1] = c + hx - hy;
        outC[2] = c + hx + hy;
        outC[3] = c - hx + hy;
    }

    // ─────────────────────────────────────────────────────────────
    // 取景屏：自己建 quad，UV 由 ComputeUvOrder 决定，不依赖预制件 UV
    // ─────────────────────────────────────────────────────────────
    private void BuildQuad()
    {
        var go = new GameObject("PortalWindow_Quad");
        // 注意：不能挂在门洞 Plane 下——PortalPair 会主动禁用那个 Plane（modelPlane.SetActive(false)），
        // 连带把取景屏一起禁掉（曾导致门洞一片空白）。挂到门根上最安全。
        go.transform.SetParent(srcFrame, false);
        quad = go.transform;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // 顶点直接用世界四角换算到门根本地
        var mesh = new Mesh { name = "PortalWindowMesh" };
        var verts = new Vector3[4];
        for (int i = 0; i < 4; i++) verts[i] = srcFrame.InverseTransformPoint(srcCorners[i]);
        mesh.vertices = verts;
        mesh.uv = new Vector2[4];
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        quadMr = go.AddComponent<MeshRenderer>();

        // RT 尺寸按门洞宽高比，保证贴上去不变形
        float aspect = Mathf.Max(0.1f, (srcCorners[1] - srcCorners[0]).magnitude / Mathf.Max(0.05f, (srcCorners[3] - srcCorners[0]).magnitude));
        rt = new RenderTexture(Mathf.RoundToInt(rtHeight * aspect), rtHeight, 24, RenderTextureFormat.Default);
        rt.Create();

        quadMat = quadMaterialOverride != null
            ? new Material(quadMaterialOverride)
            : new Material(Shader.Find("Unlit/Texture"));
        quadMat.mainTexture = rt;
        quadMr.sharedMaterial = quadMat;
    }

    private void BuildCamera()
    {
        var go = new GameObject("PortalWindow_Cam");
        go.transform.SetParent(transform, false);
        portalCam = go.AddComponent<Camera>();
        portalCam.targetTexture = rt;
        portalCam.nearClipPlane = nearClip;
        portalCam.farClipPlane = farClip;
        portalCam.enabled = false;      // 手动 Render，便于藏屏 / 控递归
        portalCam.allowHDR = false;
        portalCam.allowMSAA = false;
        portalCam.clearFlags = CameraClearFlags.SolidColor;
        portalCam.backgroundColor = Color.black;
    }

    // 目标门洞四角在「垂直于门洞平面」的相机空间里，定出 u/v 归属
    private void ComputeUvOrder()
    {
        Vector3 n = Vector3.Cross(destCorners[1] - destCorners[0], destCorners[3] - destCorners[0]).normalized;
        Vector3 u = (destCorners[3] - destCorners[0]).normalized;
        Vector3 r = (destCorners[1] - destCorners[0]).normalized;

        var cx = new float[4];
        var cy = new float[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = destCorners[i] - destCorners[0];
            cx[i] = Vector3.Dot(d, r);
            cy[i] = Vector3.Dot(d, u);
        }
        // 按 x 排序：前两个 u=0，后两个 u=1
        var idx = new[] { 0, 1, 2, 3 };
        System.Array.Sort(idx, (a, b) => cx[a].CompareTo(cx[b]));
        uOrder = new[] { idx[0], idx[1], idx[2], idx[3] };
        vOrder = new[] { idx[0], idx[1], idx[2], idx[3] };
        System.Array.Sort(idx, (a, b) => cy[a].CompareTo(cy[b]));
        vOrder = new[] { idx[0], idx[1], idx[2], idx[3] };
        _ = n;
    }

    private void ApplyUvs()
    {
        var mf = quad.GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        var uv = new Vector2[4];
        for (int rank = 0; rank < 4; rank++)
        {
            int i = uOrder[rank];
            uv[i].x = rank < 2 ? 0f : 1f;
        }
        for (int rank = 0; rank < 4; rank++)
        {
            int i = vOrder[rank];
            uv[i].y = rank < 2 ? 0f : 1f;
        }
        if (mirrorU) for (int i = 0; i < 4; i++) uv[i].x = 1f - uv[i].x;
        mesh.uv = uv;
        mesh.UploadMeshData(false);
    }

    // ─────────────────────────────────────────────────────────────
    // 每帧：摆虚拟相机 + 偏心视锥 + 手动渲染
    // ─────────────────────────────────────────────────────────────
    private void UpdatePortalCamera()
    {
        // 1) 位置：玩家眼位经「源门 → 目标门」的等距变换映射过去。
        //    关键：取景相机必须落在目标门「法线负侧」（门洞的背面），才能透过门洞
        //    看到目标世界；若落在正侧就会看到目标门自己的门板（一片纯色）。
        //    玩家在源门法线正侧 d 米 → 相机应在目标门法线负侧 d 米。
        Quaternion srcRot = srcFrame.rotation;
        Quaternion destRot = destFrame.rotation * (mapYaw180 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
        Vector3 localEye = Quaternion.Inverse(srcRot) * (playerCam.transform.position - srcFrame.position);
        // 【实测结论】相机的「正侧」位置才是能透过门洞看到目标世界的那一侧：
        // 相机在目标门正侧、朝门看 → 看到门洞后面的 Metro 隧道（正确）；
        // 翻到负侧反而只看到门背面之外的黑虚空。所以这里不做翻转。
        portalCam.transform.position = destFrame.position + destRot * localEye;

        // 2) 朝向：垂直于目标门洞平面，从外侧看进去（朝向与玩家无关——窗景只取决于眼位）
        Vector3 planeCenter = (destCorners[0] + destCorners[2]) * 0.5f;
        Vector3 nrm = Vector3.Cross(destCorners[1] - destCorners[0], destCorners[3] - destCorners[0]).normalized;
        // 把法线翻成"从相机一侧指向门洞"，相机就沿它看进去
        if (Vector3.Dot(planeCenter - portalCam.transform.position, nrm) < 0f) nrm = -nrm;
        Vector3 up = (destCorners[3] - destCorners[0]).normalized;
        portalCam.transform.rotation = Quaternion.LookRotation(nrm, up);

        // 3) 投影：偏心视锥，精确穿过目标门洞四角
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, planeZ = 0f;
        Vector3 p0 = portalCam.transform.position;
        Vector3 cr = portalCam.transform.right, cu = portalCam.transform.up, cf = portalCam.transform.forward;
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = destCorners[i] - p0;
            float x = Vector3.Dot(d, cr), y = Vector3.Dot(d, cu), z = Vector3.Dot(d, cf);
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            planeZ = z;
        }
        float d2 = Mathf.Max(nearClip * 1.01f, Mathf.Abs(planeZ));
        float s = nearClip / d2;
        portalCam.projectionMatrix = Matrix4x4.Frustum(minX * s, maxX * s, minY * s, maxY * s, nearClip, farClip);
        // 千万不要在设完 projectionMatrix 之后再碰 aspect / fieldOfView / orthographicSize——
        // 任何一个都会让 Unity 按 FOV 重算投影矩阵，把上面的偏心视锥整个覆盖掉（宽高比已经包含在 Frustum 参数里）。
    }

    private void RenderPortal()
    {
        if (quadMr == null) return;

        // 藏掉「所有」取景屏（含对面那扇门的）：只藏自己那块不够——
        // 对面相机此刻正对着本门的屏，本相机也正对着对面门的屏，两边互拍会无限递归。
        var hidden = new System.Collections.Generic.List<Renderer>();
        for (int i = 0; i < all.Count; i++)
        {
            var w = all[i];
            if (w == null) continue;
            var mr = w.QuadRenderer;
            if (mr != null && mr.enabled) { mr.enabled = false; hidden.Add(mr); }
        }

        // 藏掉「目标门的实体门板」：这个任意门预制件的门体是一块实心门板
        // （Cube / Cube.001 / Cube.002 / Cube.003 / Cylinder…），并不是带洞的门框。
        // 相机就贴在门后 2~3 米，若不藏门板，取景里只会看到门板本身（一片纯色），
        // 看不到门后面的真实世界。注意：只藏渲染器，不动物体本身，退出渲染立刻恢复。
        var doorHidden = new System.Collections.Generic.List<Renderer>();
        if (destFrame != null)
        {
            foreach (var r in destFrame.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                if (r.name.StartsWith("PortalWindow")) continue;   // 别误伤取景屏
                r.enabled = false;
                doorHidden.Add(r);
            }
        }

        portalCam.Render();

        for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].enabled = true;
        for (int i = 0; i < doorHidden.Count; i++) if (doorHidden[i] != null) doorHidden[i].enabled = true;
    }

    private void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (quadMat != null) Destroy(quadMat);
    }

    // ─────────────────────────────────────────────────────────────
    // 数值自检：目标门洞四角投影回 NDC，应精确为 (±1, ±1)
    // ─────────────────────────────────────────────────────────────
    public bool SelfTest(out string report)
    {
        if (!ready) { report = "尚未就绪（目标场景未加载）"; return false; }
        var vp = portalCam.projectionMatrix * portalCam.worldToCameraMatrix;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[PortalWindow] 门洞四角 NDC 自检（应精确为 ±1）：");
        bool ok = true;
        for (int i = 0; i < 4; i++)
        {
            Vector4 clip = vp * new Vector4(destCorners[i].x, destCorners[i].y, destCorners[i].z, 1f);
            Vector3 ndc = new Vector3(clip.x / clip.w, clip.y / clip.w, clip.z / clip.w);
            sb.AppendLine("  c" + i + " → (" + ndc.x.ToString("F4") + ", " + ndc.y.ToString("F4") + ")");
            if (Mathf.Abs(Mathf.Abs(ndc.x) - 1f) > 0.01f || Mathf.Abs(Mathf.Abs(ndc.y) - 1f) > 0.01f) ok = false;
        }
        sb.AppendLine("  结果：" + (ok ? "PASS（偏心视锥正确）" : "FAIL"));
        report = sb.ToString();
        return ok;
    }

    private void OnGUI()
    {
        // 临时调试信息（确认传送门工作后可以删掉整个 OnGUI）。
        // 把 showDebugOsd 设为 false 即可关闭。
        if (!ready || !showDebugOsd) return;
        GUI.color = Color.white;
        GUI.Label(new Rect(20f, Screen.height - 96f, 700f, 20f),
            "PortalWindow  映射Y180=" + mapYaw180 + "  镜像U=" + mirrorU + "  RT=" + rt.width + "x" + rt.height);
    }
}

