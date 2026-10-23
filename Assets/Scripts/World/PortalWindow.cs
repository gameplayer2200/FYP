using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 传送门：把本门的门洞变成对面世界的实时窗口。
///
/// ── 取景数学（改动前先读自检） ────────────────────────────────────────
/// 门是一扇窗。透过窗看到什么，只取决于「眼睛的位置」和「窗框」，与眼睛朝向无关。所以：
///   1) 虚拟相机的位置 = 玩家眼位经「门→门」等距变换映射过去；
///   2) 虚拟相机的朝向任取（取成垂直于目标门洞平面，便于构造简单视锥）；
///   3) 投影矩阵由目标门洞四角反算（偏心视锥），门洞里露出的就是那个世界该露出的那一片。
/// 自检：Tools/传送门/数值自检，把目标门洞四角投回 NDC，应当精确落在 (±1, ±1)。
///
/// ── 投影屏 ───────────────────────────────────────────────────────────
/// 不新建挡板：直接征用门自身 Cube.001 上的背板材质槽（backSlot），换成
/// Custom/PortalPanelProj——该着色器按世界坐标投影采样，绕开"FBX 未开 Read/Write"
/// 与"黑板 UV 是图集子区域"这两个坑。
///
/// ── 职责边界 ─────────────────────────────────────────────────────────
/// 传送门实现仅此一处（docs/adr/0001）；世界场景的装载/卸载由 WorldStreamer
/// 按需求统一裁决（docs/adr/0003 复查后的收敛点），本类只管取景、穿行与开合同步。
/// 配置全部来自门配对资产 WorldLink，代码里不再有场景名/门名的魔法字符串。
/// </summary>
[DisallowMultipleComponent]
public class PortalWindow : MonoBehaviour
{
    [Header("门配对")]
    [Tooltip("本门通往哪个世界、对面是哪扇门。autoPair 开启时不需要指定。")]
    [SerializeField] private WorldLink link;

    [Header("自动配对（方案 B：挪门后零操作）")]
    [Tooltip("勾选后门配对信息（目标世界、目标门、朝向差）完全由门名运行时推导，" +
             "不再需要 WorldLink 资产。挪门、改朝向后无需任何手动操作。")]
    [SerializeField] private bool autoPair = true;

    [Header("取景参数（Link 为空时的兜底值）")]
    [SerializeField] private int rtHeight = 720;
    [SerializeField] private float farClip = 200f;
    [SerializeField] private float nearClip = 0.05f;
    [SerializeField] private float preloadRadius = 15f;
    [SerializeField] private bool mirrorU = false;
    [SerializeField] private string openingPath = "Cube.001/Plane";

    [Header("取景分辨率自适应")]
    [Tooltip("勾选后取景 RT 的高度会跟着「门洞此刻在屏幕上占多高」自动升降档：\n" +
             "玩家离得远时门洞只有几十像素，基准值就够；贴到门口时门洞铺满屏幕，\n" +
             "固定 720 会被放大采样，画面糊成一团。关掉则永远用 rtHeight（旧行为）。")]
    [SerializeField] private bool dynamicResolution = true;
    [Tooltip("自适应上限（像素高度）。门洞在屏幕上再大也不会超过它，用来兜住性能。")]
    [SerializeField] private int maxRtHeight = 1440;

    [Header("取景相机后撤钳制")]
    [Tooltip("取景相机离对门门洞中心的最大距离（米）。玩家离门越远，虚拟相机在对门后方撤得越深；" +
             "撤过头会钻进墙里/走出世界边界，门洞里只剩一片纯色（实测：远看整片绿色），" +
             "画面还会随玩家走动大幅摇摆。钳住此值后：近处视差保留，远处相机位置定住、" +
             "门洞画面稳定不动，也不再撞墙。")]
    [SerializeField] private float maxRecede = 3f;

    [Header("本门")]
    [Tooltip("本门根节点（门框）。留空则取自身。")]
    [SerializeField] private Transform srcFrame;
    [Tooltip("本门 Cube.001 上作为投影屏的材质槽位索引。\n" +
             "本工程的门模型 Cube.001 有两个子网格（槽位↔部位是实测结论，不是推测）：\n" +
             "槽0 = 堵住门洞的那块内板（正面看得见的一大片，约 1.22×2.19，跟门洞 1.23×2.11 同尺寸）；\n" +
             "槽1 = 门框环（只有四周一圈 8~14 cm 的边，外廓约 1.39×2.39）。\n" +
             "**必须填 0**：投影屏要贴在门洞里那块板上。填 1 等于把「门框环」变成投影屏——" +
             "环上只会显示投影边缘被 saturate 拉伸出来的像素，看起来「左右门框消失」，" +
             "而门洞中间还是门自己的暗板，对面世界根本露不出来。")]
    [SerializeField] private int backSlot = 0;

    [Header("穿行")]
    [Tooltip("传送后多久内不再判定穿行（秒），防止一次穿越触发两次")]
    [SerializeField] private float crossCooldown = 0.6f;
    [Tooltip("离门平面多近才判定穿行（米），防止远处穿过门所在平面误触发")]
    [SerializeField] private float crossPlaneTolerance = 1.5f;
    [Tooltip("提前触发量（米）：玩家离门根平面还有这么远、且朝门里走时就传送。\n" +
             "投影屏面板贴着门根平面往前约 5cm，贴到 5cm 内会被相机近裁剪切掉，看起来像穿模；\n" +
             "提前量要盖住它再加一帧的移动步长。落点会做反向补偿，出场位置不受影响。")]
    [SerializeField] private float crossPlaneLead = 0.25f;

    [Header("调试")]
    [Tooltip("显示屏幕底部的调试信息（定稿后设 false）")]
    [SerializeField] private bool showDebugOsd = true;

    // ── 运行时 ──
    private Transform srcPlane;          // 本门门洞平面
    private Transform destFrame;         // 目标门根
    private Transform destPlane;         // 目标门门洞平面
    private Camera portalCam;
    private RenderTexture rt;
    private int currentRtHeight;         // 当前 RT 高度（自适应时用来判断是否需要改尺寸）
    private float rtAspect = 0.58f;      // 门洞宽高比（CreateRt 时实测，改尺寸要沿用）
    private Material quadMat;            // 投影屏材质（Custom/PortalPanelProj）
    private Material originalBackMat;    // 背板原本的材质：关门时换回它
    private Material darkMat;            // 兜底暗板（原材质缺失时才用）
    private Renderer backMr;             // 背板（Cube.001）：投影屏与暗板共用它的 backSlot
    private AnywhereDoor door;           // 本门开关组件（驱动背板材质切换）
    private AnywhereDoor pairedDoor;     // 对面那扇门的开关组件（开合同步）
    private bool lastDoorOpen;           // 上一帧本门开合态：只在"本门变化"时才向对门下发
    private bool backPanelDark = true;   // 背板当前是不是暗板（防重复 SetRenderer）
    private bool ready;
    /// <summary>
    /// 初始化已判定失败（目标世界里找不到配对门 / 找不到门洞平面）。
    /// 一旦置位就彻底放弃：否则 ready 永远为 false，Update 会每帧重试并刷屏报错。
    /// 典型场景：对面那扇门被删掉或挪到了别的场景，但本门的 WorldLink 还指着旧位置。
    /// </summary>
    private bool failed;

    /// <summary>就绪后的头几帧渲染要做耗时诊断（首次渲染含着色器编译/网格上传，是最贵的几帧）。</summary>
    private int renderDiagLeft;

    /// <summary>
    /// 常驻世界（preloadRadius ≤0，开局即加载）的取景渲染距离兜底值（米）。
    /// 这类世界的 preloadRadius 是 0，乘 2 也是 0，会让门洞永远不渲染。
    /// </summary>
    private const float ResidentRenderRadius = 30f;
    private float prevDot;               // 上一帧玩家在门平面法线上的带符号距离（穿行判定基准）
    private float cooldown;              // 穿行冷却
    private readonly Vector3[] srcCorners = new Vector3[4];
    private readonly Vector3[] destCorners = new Vector3[4];

    // ── 方案 B：运行时推导的配对信息 ──
    private bool derivedMapYaw180;       // 运行时根据两扇门朝向差计算

    private void Awake()
    {
        if (srcFrame == null) srcFrame = transform;
        if (!autoPair && link == null)
        {
            Debug.LogError("[PortalWindow] 未指定门配对（WorldLink）且 autoPair 关闭，本门不会工作。", this);
            return;
        }
        srcPlane = FindOpening(srcFrame);
        if (srcPlane == null)
            Debug.LogError("[PortalWindow] 找不到门洞平面 " + ResolvedOpeningPath + "（挂在 " + srcFrame.name + " 下）", this);
    }

    private void OnEnable()
    {
        WorldStreamer.Register(this);     // 世界流送器：登记本门，参与装载/卸载裁决
    }

    private void OnDisable()
    {
        WorldStreamer.Unregister(this);
    }

    // 先按配置路径找，找不到就退化为「子树里名字含 plane 的节点」
    private Transform FindOpening(Transform doorRoot)
    {
        var t = doorRoot.Find(ResolvedOpeningPath);
        if (t != null) return t;
        foreach (var c in doorRoot.GetComponentsInChildren<Transform>(true))
            if (c != doorRoot && c.name.ToLower().Contains("plane")) return c;
        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // 供世界流送器（WorldStreamer）查询
    // ─────────────────────────────────────────────────────────────
    /// <summary>本门通往的世界（场景名）。</summary>
    public string TargetWorldName { get { return ResolvedWorldSceneName; } }

    /// <summary>本门的预载半径（米）：世界流送器据此决定何时装载目标世界。</summary>
    public float PreloadRadius { get { return ResolvedPreloadRadius; } }

    /// <summary>本门根节点：世界流送器用它量"玩家离本门多远"。</summary>
    public Transform SrcFrame { get { return srcFrame; } }

    /// <summary>本门门板是否开着（世界流送器据此保住"开着的门洞背后"的世界）。</summary>
    public bool IsDoorOpen { get { return door != null && door.IsOpen; } }

    // ── 方案 B：统一解析（autoPair 时推导，否则用 Link） ──
    private string ResolvedWorldSceneName { get { return autoPair ? DerivePartnerScene() : (link != null ? link.WorldSceneName : null); } }
    private string ResolvedWorldDoorName { get { return autoPair ? DerivePartnerDoor() : (link != null ? link.WorldDoorName : null); } }
    private bool ResolvedMapYaw180 { get { return autoPair ? derivedMapYaw180 : (link != null ? link.MapYaw180 : false); } }
    private string ResolvedOpeningPath { get { return link != null && !string.IsNullOrEmpty(link.OpeningPath) ? link.OpeningPath : openingPath; } }
    private int ResolvedRtHeight { get { return link != null ? link.RtHeight : rtHeight; } }
    private float ResolvedFarClip { get { return link != null ? link.FarClip : farClip; } }
    private float ResolvedNearClip { get { return link != null ? link.NearClip : nearClip; } }
    private float ResolvedPreloadRadius { get { return link != null ? link.PreloadRadius : preloadRadius; } }
    private bool ResolvedMirrorU { get { return link != null ? link.MirrorU : mirrorU; } }

    // ─────────────────────────────────────────────────────────────
    // 等待目标世界：装载/卸载由 WorldStreamer 按需求裁决（见其类注释），
    // 本窗只负责"世界就绪后把取景建起来"。
    // ─────────────────────────────────────────────────────────────
    private void Update()
    {
        if (srcFrame == null) return;
        if (!autoPair && link == null) return;
        if (!PlayerRef.IsReady) return;

        WorldStreamer.Tick();   // 每帧一次装载/卸载裁决（幂等：多扇门重复调用无副作用）

        if (ready || failed) return;

        // 玩家还远就不建立取景。半径由门配对配置；≤0 表示开局立即建立
        float radius = ResolvedPreloadRadius;
        if (radius > 0f && (PlayerRef.Root.position - srcFrame.position).sqrMagnitude > radius * radius) return;

        // 目标世界可能已由其它门装好——优先复用，绝不重复加载
        if (!WorldStreamer.IsWorldReady(ResolvedWorldSceneName)) return;
        OnWorldLoaded(null);
    }

    private void OnWorldLoaded(AsyncOperation op)
    {
        if (ready) return;   // 幂等：只初始化一次，避免重复创建相机/取景屏
        var scene = SceneManager.GetSceneByName(ResolvedWorldSceneName);
        if (!scene.isLoaded) { Debug.LogError("[PortalWindow] 目标场景未加载：" + ResolvedWorldSceneName, this); return; }

        // 自配对护栏：门名（或用错配置的 WorldLink）把"目标世界"指到了本门自己所在的世界，
        // 于是配对门被找成它自己 —— 穿行时落点会落在**门背面 0.02m**，也就是挂门的那堵墙里，
        // 表现为"穿模 / 被墙挤出去 / 掉出世界"，而且这扇门永远回不到对面世界。
        // 典型的起错名：世界 X 场景里放了 `anywhere-door-X-A`（约定里 `-A` = 不在 X 里、通往 X 的门）。
        // 这里直接判为配置错误停用，不再"静默地把自己当对面"。
        if (scene == gameObject.scene)
        {
            Debug.LogError("[PortalWindow] 门名推导出的目标世界就是本门自己所在的世界（" + ResolvedWorldSceneName
                           + "）——配对成了自己。世界侧的门应叫 `anywhere-door-X`（不带 -A），"
                           + "`-A` 只用于实验室等「外面」的门。本门停用。", this);
            failed = true;
            return;
        }

        // 找不到配对门 / 门洞平面属于**配置错误**（对面那扇门被删了或挪走了），
        // 不是暂时的：置 failed 彻底放弃，否则 ready 永远为 false，Update 会每帧重试刷屏。
        // 修好配对后重进 Play 即可（或点菜单 FYP/门配对/扫描并自动重配）。
        destFrame = FindDoor(scene, ResolvedWorldDoorName);
        if (destFrame == null)
        {
            Debug.LogError("[PortalWindow] 目标世界里找不到门：" + ResolvedWorldDoorName
                           + "（" + ResolvedWorldSceneName + "）。配对已失效，本门停用。"
                           + "请用 FYP/门配对/扫描并自动重配 修复。", this);
            failed = true;
            return;
        }

        // 方案 B：运行时根据两扇门朝向差判定 mapYaw180
        DeriveMapYaw180(destFrame);

        destPlane = destFrame.Find(ResolvedOpeningPath);
        if (destPlane == null)
        {
            Debug.LogError("[PortalWindow] 目标门找不到门洞平面：" + ResolvedOpeningPath, this);
            failed = true;
            return;
        }

        // 停掉目标世界自带的相机，避免抢画面。
        // 但绝不能停玩家自己的相机——如果目标就是当前活动场景（从异世界回头取景实验室），
        // 停相机等于把玩家搞瞎。
        if (scene != SceneManager.GetActiveScene())
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Camera>(true))
                    c.gameObject.SetActive(false);
        }

        ReadCorners(srcPlane, srcCorners);
        ReadCorners(destPlane, destCorners);
        LoadDiag.Begin();
        CreateRt();
        BuildCamera();
        ApplyPortalToBackPanel();
        LoadDiag.End("建立取景 " + srcFrame.name + "（RT/相机/投影屏）");

        // 背板材质联动：开门=投影露出，关门=暗板挡住。
        // 门板本身（Cube）不做任何处理——它由 AnywhereDoor 正常开合，一直可见。
        door = srcFrame.GetComponentInChildren<AnywhereDoor>(true);
        // 配对门：两个世界是同一扇门，开合必须一致。目标场景刚加载完，
        // 对面那扇门的组件此刻一定在（就在同一个 destFrame 下）。
        pairedDoor = destFrame.GetComponentInChildren<AnywhereDoor>(true);
        lastDoorOpen = door != null && door.IsOpen;
        SyncPairedDoor(true);        // 先对齐一次历史状态差
        SyncBackPanel(true);
        ResetCrossBaseline();        // 清穿行基准：装载期间玩家可能已换过世界，陈旧基准会误判

        ready = true;
        renderDiagLeft = 3;      // 头 3 帧渲染打耗时（首次渲染含着色器编译/网格上传）
        Debug.Log("[PortalWindow] 取景已建立：源=" + srcFrame.name + " 目标=" + destFrame.name
                  + " RT=" + rt.width + "x" + rt.height, this);
    }

    /// <summary>
    /// 在已加载的目标场景里按名字找门。
    /// 这里保留按名字找是**刻意的**：门配对天然跨场景，而 Unity 无法序列化指向未加载
    /// 场景中对象的引用。名字来自 WorldLink 资产（配置数据），不是代码里的魔法字符串。
    /// 见 docs/adr/0002-cross-scene-refs-are-data.md。
    /// </summary>
    private static Transform FindDoor(Scene scene, string doorName)
    {
        string key = doorName.ToLower();
        foreach (var root in scene.GetRootGameObjects())
        {
            // 精确名优先，其次包含匹配（anywhere-door / anywhere-door-open 等变体通吃）
            var f = root.transform.Find(doorName);
            if (f != null) return f;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains(key)) return t;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // 穿行：玩家真的从门洞走过去 → 瞬移到对面门
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 穿行判定：记录玩家在门平面法线上的带符号投影（米），跨过**提前触发面**即传送。
    /// 触发面在门根平面往接近侧偏 crossPlaneLead 米——面板贴着根平面，玩家贴太近时
    /// 投影屏会被近裁剪切掉（看起来像穿模），所以要提前触发；且只认「朝门里走」的方向，
    /// 退着走、在门口来回晃不算。
    ///
    /// 落点 = 玩家在本门的 local 坐标镜像到对门（z 取反），再沿对门法线补偿回根平面附近；
    /// 朝向同步镜像（前向 z 取反）。位置镜像与朝向翻转**必须成对**：
    /// 只做位置会把玩家放到对面却面朝来路，继续走就穿回去了。
    /// </summary>
    private void TryCross()
    {
        if (!PlayerRef.IsReady || destFrame == null || srcFrame == null) return;

        float dotNow = Vector3.Dot(PlayerRef.Root.position - srcFrame.position, srcFrame.forward);

        // 提前触发面：从哪边来，就在那边离门根平面 crossPlaneLead 米处判翻转。
        // 向内判定：位移方向必须与「上一帧所在侧」相反（= 朝门里走）；
        // 没有这条，退着走出门洞/在门口来回晃也会被当成穿门。
        float trigger = Mathf.Sign(prevDot) * crossPlaneLead;
        float delta = dotNow - prevDot;
        bool inward = prevDot != 0f
                      && Mathf.Abs(delta) > 0.0005f
                      && Mathf.Sign(delta) != Mathf.Sign(prevDot);
        bool crossed = prevDot != 0f                                          // 首帧哨兵：还没建立基准不判定
                       && Mathf.Sign(dotNow - trigger) != Mathf.Sign(prevDot - trigger) // 跨过触发面
                       && Mathf.Abs(dotNow - trigger) < crossPlaneTolerance    // 且确实在门洞附近（防远处误传送）
                       && inward;
        prevDot = dotNow;
        if (!crossed) return;

        // 门关着不许穿（物理上门板碰撞体也该挡住，这里是判定层兜底）
        if (door != null && !door.IsOpen) return;
        // 必须从门洞里穿过去，不能贴着门框旁边蹭过去
        if (!IsInsideOpening(PlayerRef.Root.position)) return;

        Vector3 local = srcFrame.InverseTransformPoint(PlayerRef.Root.position);
        Vector3 target = destFrame.TransformPoint(MapLocalPoint(local));

        // 落点补偿：提前触发会让镜像点落在对面门根平面「后面」crossPlaneLead 米处（门洞内侧），
        // 把它沿对门法线拉回到根平面往前 exitAdvance 米（朝对面世界那一侧）。
        // 不补的话提前量越大、人越往对面门里/墙里钻。
        const float exitAdvance = 0.02f;
        float destRaw = Vector3.Dot(target - destFrame.position, destFrame.forward);
        target += destFrame.forward * (-exitAdvance - destRaw);

        // 朝向：与位置共用同一套映射，两者必须成对——
        // 只搬位置不搬朝向，玩家会面朝来路继续走回去
        Vector3 fwdLocal = Quaternion.Inverse(srcFrame.rotation) * PlayerRef.Root.forward;
        Vector3 fwdNew = destFrame.rotation * MapLocalDirection(fwdLocal);
        Quaternion facing = Quaternion.LookRotation(fwdNew, Vector3.up);

        cooldown = crossCooldown;
        PlayerRef.Teleport(target, facing);
        WorldStreamer.NotifyCrossed(ResolvedWorldSceneName);   // 登记"玩家已在对面世界"：脚下的世界不许被流送器卸掉

        // 重置两侧的穿行基准：对面那扇门若沿用上一帧的陈旧值，会把玩家立刻弹回来
        var other = destFrame.GetComponentInChildren<PortalWindow>(true);
        if (other != null && other != this) other.ResetCrossBaseline();
        ResetCrossBaseline();

        Debug.Log("[PortalWindow] 穿门 → " + destFrame.name + " @ " + target.ToString("F1"), this);
    }

    /// <summary>
    /// 玩家是否落在门洞范围内。用门洞四角现算，不写死尺寸——换一扇尺寸不同的门不用改代码。
    /// </summary>
    private bool IsInsideOpening(Vector3 worldPos)
    {
        Vector3 u = srcCorners[1] - srcCorners[0];
        Vector3 v = srcCorners[3] - srcCorners[0];
        if (u.sqrMagnitude < 0.0001f || v.sqrMagnitude < 0.0001f) return true; // 四角没算出来就不拦
        Vector3 d = worldPos - srcCorners[0];
        float a = Vector3.Dot(d, u) / Vector3.Dot(u, u);
        float b = Vector3.Dot(d, v) / Vector3.Dot(v, v);
        const float margin = 0.2f;
        return a > -margin && a < 1f + margin && b > -margin && b < 1f + margin;
    }

    /// <summary>
    /// 清空穿行基准（传送后两侧都要调，否则会来回弹）。
    ///
    /// 为什么是"清空"而不是"按此刻位置重算"：对面那扇门的 prevDot 里存的是传送前
    /// 玩家在**另一个世界**坐标下的带符号距离（隔着上百米，正负都有），落地后首帧
    /// dotNow 符号一翻、又正好落在容差内，就会被当成"又穿了一次"弹回来。
    /// 置 0 = 哨兵（TryCross 首帧只建立基准不判定），下一帧用落地后的真实位置重建。
    /// </summary>
    public void ResetCrossBaseline()
    {
        cooldown = crossCooldown;
        prevDot = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    // 门→门映射：把本门 local 系里的点/方向搬到对门 local 系。
    //
    // **这必须是一个刚体变换（旋转 + 平移），不能是镜像。**
    // 镜像会翻转手性，后果有两个：虚拟相机落到对面门的"背面"，于是门洞里看到的
    // 不是那扇门的正面；而且"走进去"的方向被反转，穿门后等于原路往回走。
    // （早先这里写的是镜像 (x, y, -z)，正是"门洞内的正面不是对面门的正面"的原因。）
    //
    // mapYaw180 决定两扇门之间差多少旋转：
    //   false = 两扇门朝向一致 → 纯平移，local 坐标原样搬过去
    //   true  = 两扇门朝向相差 180° → 绕 Y 转 180°，(x,y,z) → (-x,y,-z)
    //
    // 取景相机与穿门瞬移**共用**这一个映射。两处各写各的，就会出现
    // "门里看见的"和"走过去得到的"对不上（实测曾差 1.755m）。
    // ─────────────────────────────────────────────────────────────
    private Vector3 MapLocalPoint(Vector3 srcLocal)
    {
        return ResolvedMapYaw180
            ? new Vector3(-srcLocal.x, srcLocal.y, -srcLocal.z)
            : srcLocal;
    }

    private Vector3 MapLocalDirection(Vector3 srcLocalDir)
    {
        return ResolvedMapYaw180
            ? new Vector3(-srcLocalDir.x, srcLocalDir.y, -srcLocalDir.z)
            : srcLocalDir;
    }

    // ─────────────────────────────────────────────────────────────
    // 每帧：摆虚拟相机 + 偏心视锥 + 手动渲染
    // ─────────────────────────────────────────────────────────────
    private void LateUpdate()
    {
        if (!ready) return;
        ApplySampleAxes();   // 材质属性可能被编辑器刷新/着色器重载清零——每帧补一次（见方法注释）
        SyncBackPanel(false);
        SyncPairedDoor(false);

        if (cooldown > 0f) cooldown -= Time.deltaTime;
        else TryCross();

        var playerCam = PlayerRef.Camera;
        if (playerCam == null) return;
        // 取景渲染距离 = preloadRadius × 2。
        // 但 preloadRadius ≤0 表示"开局即加载的常驻世界"，乘出来是 0 会让门洞**永远不渲染**，
        // 所以这种情况用一个够大的固定值兜底（常驻世界的取景本来就该一直可见）。
        float renderRadius = ResolvedPreloadRadius > 0f
            ? ResolvedPreloadRadius * 2f
            : ResidentRenderRadius;
        if ((playerCam.transform.position - srcFrame.position).sqrMagnitude > renderRadius * renderRadius)
            return; // 离得远就不渲染取景，省性能

        UpdatePortalCamera(playerCam);
        UpdateRtResolution(playerCam);
        RenderPortal();
    }

    /// <summary>
    /// 按门的开关状态切换背板材质：开门 = 投影屏，关门 = 背板原本的材质。
    ///
    /// 关门时**换回原材质**（门自己的颜色），不要换暗板。因为 backSlot 选中的是门洞里的
    /// 内板（实测 1.22×2.19，与门洞 1.225×2.106 基本同尺寸、正对门洞），关门时它就是门芯；
    /// 换成暗板会把它变成一块死黑，看起来像门板缺了一块。换回原材质，整扇门同色一体。
    /// （另一个槽是门框圈，只有 8~14cm 宽的边，投影贴上去就是"门框消失"那个 bug。）
    ///
    /// **门板（Cube）本身一律不动，渲染器和碰撞体都保持启用。**
    /// 早先这里会把门板渲染器连同碰撞体一起关掉，理由写在注释里是"门板转轴在面板中心、
    /// 开关动画原地翻转、始终盖着门洞"。但那个前提现在已经不成立：门板 pivot 就在合页边
    /// （Cube localPos.x = -1.0，正好是门洞左边缘），开到 95° 时门板整个让开门洞，
    /// 既不挡视线也不挡穿行。关掉它只会让玩家看到"门一开就凭空消失"。
    /// </summary>
    private void SyncBackPanel(bool force)
    {
        if (door == null) return;
        // 判据必须是门板的**实际**角度，不是 IsOpen 意图值。
        // IsOpen 在 SetOpen(false) 那一帧就翻成 false，而门板还要走完
        // spellWindup(1.2s) + 旋转(0.8s) + spellOutro(0.6s) 才真的合上；
        // 用 IsOpen 判定 → 门还在慢慢转回去，门洞先换回背板材质，观感就是「没关就黑了」。
        bool wantDark = door.OpenFraction <= ClosedFraction;
        if (!force && wantDark == backPanelDark) return;
        // 原材质没捕获到（例如 Cube.001 结构不同）才退回暗板
        ApplyBackSlotMaterial(wantDark ? (originalBackMat != null ? originalBackMat : darkMat) : quadMat);
        backPanelDark = wantDark;
    }

    /// <summary>
    /// 门板开合进度低于此值才算「关上了」，背板才换回门自己的材质。
    /// 取 0.02 = 门板还差 2% 的角度（95° 门约 2°）就完全合上——肉眼已看不出缝。
    /// 不能取大值：那会变成「门还剩一条缝就先黑」，正是要修的毛病。
    /// </summary>
    private const float ClosedFraction = 0.02f;

    /// <summary>
    /// 配对门开合同步：两个世界是同一扇门，开合必须一致。
    ///
    /// 只在**本门状态发生变化**时向对门下发，而不是每帧把对门"扳"成本门的样子。
    /// 两个世界的 PortalWindow 都在跑这段代码，逐帧扳会互相打架：在 Metro 那边按 E
    /// 开门，实验室这扇门的窗口下一帧就把它扳回去，门等于打不开。
    /// 下发前再比对一次状态：对门已经一致就不调用（SetOpen 会重启动画，多调会抖）。
    ///
    /// 自动关计时只由被交互的那扇门排（scheduleAutoClose=false，见 AnywhereDoor.SetOpen 注释），
    /// 否则两边各排各的计时器，到期后互相触发成级联。
    /// 镜像下发不播玩家施法演出（playSpell=false）：对门那头没有人在推门，
    /// 玩家在另一边看的门跟着动就够了，绝不能让他莫名其妙抬手。
    /// force=true（目标世界刚加载完）时做一次对齐，把历史遗留的状态差抹平。
    /// </summary>
    private void SyncPairedDoor(bool force)
    {
        if (door == null) return;
        bool changed = force || door.IsOpen != lastDoorOpen;
        lastDoorOpen = door.IsOpen;
        if (!changed) return;
        if (pairedDoor == null || pairedDoor.IsOpen == door.IsOpen) return;
        pairedDoor.SetOpen(door.IsOpen, false, false);
    }

    /// <summary>
    /// 按**槽位索引**换背板材质。
    /// 早先的实现按材质名找槽（找名为 PortalScreen 的槽），导致换过去就换不回来——
    /// 第一次换成暗板后槽里已经不是 PortalScreen，下一次再也搜不到它，投影永远露不出来。
    /// </summary>
    private void ApplyBackSlotMaterial(Material m)
    {
        if (backMr == null || m == null) return;
        var mats = backMr.sharedMaterials;
        if (backSlot < 0 || backSlot >= mats.Length) return;
        if (mats[backSlot] == m) return;
        mats[backSlot] = m;
        backMr.sharedMaterials = mats;
    }

    // ─────────────────────────────────────────────────────────────
    // 取景 RT：尺寸按门洞宽高比，保证画面不变形
    // ─────────────────────────────────────────────────────────────
    private void CreateRt()
    {
        float aspect = Mathf.Max(0.1f,
            (srcCorners[1] - srcCorners[0]).magnitude / Mathf.Max(0.05f, (srcCorners[3] - srcCorners[0]).magnitude));
        rtAspect = aspect;
        int h = ResolvedRtHeight;
        rt = new RenderTexture(Mathf.RoundToInt(h * aspect), h, 24, RenderTextureFormat.Default);
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        currentRtHeight = h;
    }

    /// <summary>
    /// 让取景 RT 的高度跟着「门洞在玩家屏幕上占多高」走。
    ///
    /// 为什么要这样：RT 是**贴图**，门洞在屏幕上占多少像素，它就得有多少像素，
    /// 否则就是放大采样 → 糊。玩家离得远时门洞在屏幕上只有几十像素高，720 绰绰有余；
    /// 贴到门口时门洞铺满整个屏幕（1080p 下 1000+ 像素），720 的图被拉大两倍多，
    /// 糊得连门里是什么都看不清。
    ///
    /// 不能逐帧连续改：改尺寸要重建 RT（`RenderTexture.Release()` + `Create()`），
    /// 每帧重建是纯浪费。所以只在实际需要量明显超出时升（≥1.15×）、明显富余时才降
    /// （<1/1.6），正常走动几乎不会触发重建；且升档一次到位，贴脸瞬间就该清晰。
    /// </summary>
    private void UpdateRtResolution(Camera playerCam)
    {
        if (!dynamicResolution || rt == null || playerCam == null) return;

        // 自愈：门洞角点被清成零向量时（编辑器刷新 / 场景重载后实测出现过 srcCorners 全 0），
        // NDC 投影恒等、screenFraction 恒为 0，自适应会**静默失效**。发现退化就从门洞平面重读。
        if (srcCorners[0] == srcCorners[2] && srcPlane != null)
            ReadCorners(srcPlane, srcCorners);

        // 门洞在屏幕上的高度占比：把门洞上下边中点投到 NDC，取 |Δy| / 2
        float ndcTop = ProjectNdcY(playerCam, srcCorners[3], srcCorners[2]);
        float ndcBottom = ProjectNdcY(playerCam, srcCorners[0], srcCorners[1]);
        if (float.IsNaN(ndcTop) || float.IsNaN(ndcBottom)) return;
        float screenFraction = Mathf.Abs(ndcTop - ndcBottom) * 0.5f;   // NDC 高度 2 = 铺满屏幕
        if (screenFraction <= 0.0001f) return;

        // 门洞在屏幕上实际占多少像素 → 这就是需要的 RT 高度
        int wanted = Mathf.CeilToInt(screenFraction * playerCam.pixelHeight);
        int minH = ResolvedRtHeight;
        int maxH = Mathf.Max(minH, maxRtHeight);
        wanted = Mathf.Clamp(wanted, minH, maxH);

        // 滞回：升档要「明显不够」才升（1.15×），降档要「明显富余」才降（1.6×），
        // 免得站在某个距离上反复重建 RT。
        if (wanted <= currentRtHeight && wanted * 1.6f >= currentRtHeight) return;
        if (wanted > currentRtHeight && wanted < currentRtHeight * 1.15f) return;
        if (wanted == currentRtHeight) return;

        currentRtHeight = wanted;
        rt.Release();
        rt.height = wanted;
        rt.width = Mathf.Max(1, Mathf.RoundToInt(wanted * rtAspect));
        rt.Create();
    }

    /// <summary>把世界坐标点投到玩家相机的 NDC y；在相机背后返回 NaN。</summary>
    private static float ProjectNdcY(Camera cam, Vector3 a, Vector3 b)
    {
        Vector3 p = (a + b) * 0.5f;
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0f) return float.NaN;
        return (v.y - 0.5f) * 2f;
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
    // 背板变投影屏：征用 Cube.001 的 backSlot 槽，按世界坐标投影采样对面世界。
    // 门体保持完整（门框/背板/把手全可见），只替换该槽的材质。
    // ─────────────────────────────────────────────────────────────
    private void ApplyPortalToBackPanel()
    {
        var back = srcFrame.Find("Cube.001");
        if (back == null) { Debug.LogError("[PortalWindow] 找不到 Cube.001，投影屏未建立", this); return; }
        var mr = back.GetComponent<MeshRenderer>();
        if (mr == null) { Debug.LogError("[PortalWindow] Cube.001 无网格渲染器", this); return; }

        var mats = mr.sharedMaterials;
        if (backSlot < 0 || backSlot >= mats.Length)
        {
            Debug.LogError("[PortalWindow] backSlot=" + backSlot + " 越界（Cube.001 只有 "
                           + mats.Length + " 个材质槽）", this);
            return;
        }

        var shader = Shader.Find("Custom/PortalPanelProj");
        if (shader == null) { Debug.LogError("[PortalWindow] 找不到 Custom/PortalPanelProj 着色器", this); return; }

        quadMat = new Material(shader);
        quadMat.name = "PortalScreen";
        quadMat.mainTexture = rt;

        // 采样轴/镜像开关：见 ApplySampleAxes（构建时下发一次，之后每帧自愈重发）。
        ApplySampleAxes();

        // 关门暗板：不透明深色（原黑板槽是透明化材质，直接换回会透视 =「门消失」观感）
        darkMat = new Material(Shader.Find("Standard"));
        darkMat.color = new Color(0.06f, 0.06f, 0.08f);

        backMr = mr;
        originalBackMat = mats[backSlot];   // 先存下原材质，关门时换回它（见 SyncBackPanel）
        mats[backSlot] = quadMat;
        mr.sharedMaterials = mats;

        // 门体内置的 Plane（旧屏位）必须藏掉，别盖住新投影
        var planeR = srcPlane != null ? srcPlane.GetComponent<Renderer>() : null;
        if (planeR != null) planeR.enabled = false;
    }

    /// <summary>
    /// 采样轴与镜像开关：屏幕着色器按「世界坐标投影到门洞平面」算采样坐标，轴由这里下发。
    ///
    /// 这些属性一度只写成着色器的裸 uniform、没进 Properties 块，后果实测：编辑器刷新 /
    /// 着色器重载之后材质上的值被清零（控制台同时刷 "doesn't have a color property '_Origin'"
    /// 一类报错），门内板 UV 全部塌到 (0,0)——整块面板只采样 RT 的一个像素，看起来就是
    /// **门洞里一片纯色**（离得远面板画得小，连那点细节也看不出来）。
    ///
    /// 双保险：① 着色器 Properties 里正式声明这四个属性；② 这里每帧重算重下发（值本来
    /// 就是静态的，开销可忽略）——即使材质被重建/重置，下一帧也自动恢复，不需要外部补救。
    ///
    /// 采样轴必须与「取景相机的右方向」一致，否则画面左右镜像（实测：门局部 +X 的物体
    /// 会显示在窗口左半边）。取景相机的 up 取的是目标门洞 V 轴方向（UpdatePortalCamera 里
    /// 是倒架的），相机右方向 = cross(up, nrm) 恰好与 c0→c1 相反，所以 U 轴要从 c1 指向 c0。
    /// V 不受影响：c0 与 c1 在同一条水平边上，原点到两者算出的 v 相同。
    /// 轴向传**单位向量**、w 传轴长：着色器算的是 dot(d, 轴)/w，只有轴是单位向量时
    /// 这个商才是 0~1 的采样比例。传整条边向量（长度 1.2/2.1 米）算出来是"米"，
    /// 画面会被压缩、多出来的部分被 saturate 夹成死条——实测：面板中点采到 0.613
    /// （应为 0.500），竖轴 1.0m 以下全部夹在 v=1（下半幅是一条拉伸的边行）。
    /// </summary>
    private void ApplySampleAxes()
    {
        if (quadMat == null) return;
        Vector3 uAxis = srcCorners[0] - srcCorners[1]; float uLen = Mathf.Max(0.01f, uAxis.magnitude);
        Vector3 vAxis = srcCorners[3] - srcCorners[0]; float vLen = Mathf.Max(0.01f, vAxis.magnitude);
        Vector3 uDir = uAxis / uLen;
        Vector3 vDir = vAxis / vLen;
        quadMat.SetVector("_Origin", srcCorners[1]);   // u=0 的那个角（本约定下 = 门局部 +X 侧）
        quadMat.SetVector("_UDir", new Vector4(uDir.x, uDir.y, uDir.z, uLen));
        quadMat.SetVector("_VDir", new Vector4(vDir.x, vDir.y, vDir.z, vLen));
        quadMat.SetFloat("_MirrorU", ResolvedMirrorU ? 1f : 0f);
    }

    private void BuildCamera()
    {
        var go = new GameObject("PortalWindow_Cam");
        go.transform.SetParent(transform, false);
        portalCam = go.AddComponent<Camera>();
        portalCam.targetTexture = rt;
        portalCam.nearClipPlane = ResolvedNearClip;
        portalCam.farClipPlane = ResolvedFarClip;
        portalCam.enabled = false;      // 手动 Render，便于藏门
        portalCam.allowHDR = false;
        portalCam.allowMSAA = false;
        portalCam.clearFlags = CameraClearFlags.SolidColor;
        portalCam.backgroundColor = Color.black;
    }

    private void UpdatePortalCamera(Camera playerCam)
    {
        // 1) 位置：玩家眼位经「门→门」映射过去。
        //    这里必须与 TryCross 用**同一个** MapLocalPoint——两处各写各的，
        //    就会出现"门里看见的"和"走过去得到的"对不上（实测差 1.75m）。
        Vector3 localEye = srcFrame.InverseTransformPoint(playerCam.transform.position);
        Vector3 camPos = destFrame.TransformPoint(MapLocalPoint(localEye));

        // 1b) 后撤钳制：玩家站得越远，虚拟相机在对门后方撤得越深（≈玩家到门的距离）。
        //      门后的净空是有限的——撤过头就钻进墙里/走出世界边界，门洞里只剩一片纯色
        //      （实测：远处整片绿色），而且画面随玩家走动大幅摇摆。
        //      钳到 maxRecede 后：近处保留完整视差；远处相机位置不再变，门洞画面稳定不动。
        Vector3 planeCenter = (destCorners[0] + destCorners[2]) * 0.5f;
        Vector3 rel = camPos - planeCenter;
        float relDist = rel.magnitude;
        if (relDist > maxRecede) camPos = planeCenter + rel * (maxRecede / relDist);
        portalCam.transform.position = camPos;

        // 2) 朝向：垂直于目标门洞平面，从外侧看进去（朝向与玩家无关——窗景只取决于眼位）
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
        float d2 = Mathf.Max(ResolvedNearClip * 1.01f, Mathf.Abs(planeZ));
        float s = ResolvedNearClip / d2;
        portalCam.projectionMatrix = Matrix4x4.Frustum(minX * s, maxX * s, minY * s, maxY * s, ResolvedNearClip, ResolvedFarClip);
        // 千万不要在设完 projectionMatrix 之后再碰 aspect / fieldOfView / orthographicSize——
        // 任何一个都会让 Unity 按 FOV 重算投影矩阵，把上面的偏心视锥整个覆盖掉
        //（宽高比已经包含在 Frustum 参数里）。
    }

    private void RenderPortal()
    {
        if (portalCam == null) return;

        bool diag = renderDiagLeft > 0;
        if (diag) { renderDiagLeft--; LoadDiag.Begin(); }

        // 藏掉目标门自身全部渲染器：任意门预制件的门体是一块实心门板
        //（Cube / Cube.001 / Cube.002 / Cube.003 / Cylinder…），并不是带洞的门框。
        // 相机就贴在门后 2~3 米，若不藏门板，取景里只会看到门板本身（一片纯色），
        // 看不到门后面的真实世界。注意：只藏渲染器，不动物体本身，渲染完立刻恢复。
        //
        // 投影屏不必单独处理：它就在目标门的 Cube.001 上，已经被这段一起藏掉了。
        // 两扇门的取景相机各自藏对方的门，结构上不可能互拍成递归。
        var doorHidden = new System.Collections.Generic.List<Renderer>();
        if (destFrame != null)
        {
            foreach (var r in destFrame.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                doorHidden.Add(r);
            }
        }

        portalCam.Render();

        for (int i = 0; i < doorHidden.Count; i++)
            if (doorHidden[i] != null) doorHidden[i].enabled = true;

        if (diag) LoadDiag.End("渲染取景 " + (srcFrame != null ? srcFrame.name : name));
    }

    private void OnDestroy()
    {
        ReleaseView();
        if (darkMat != null) { Destroy(darkMat); darkMat = null; }
    }

    // ─────────────────────────────────────────────────────────────
    // 方案 B：门名推导配对
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 从本门名推导目标场景名。
    /// 规则：anywhere-door-X-A → X（"-" 替换为 "_"）；anywhere-door-X → SpecimenLab。
    /// </summary>
    private string DerivePartnerScene()
    {
        string name = gameObject.name;
        const string prefix = "anywhere-door-";
        if (!name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return null;
        string key = name.Substring(prefix.Length);
        bool hasA = key.EndsWith("-A", System.StringComparison.OrdinalIgnoreCase);
        if (hasA)
        {
            key = key.Substring(0, key.Length - 2);
            return key.Replace("-", "_");
        }
        return "SpecimenLab";
    }

    /// <summary>
    /// 从本门名推导配对门名。
    /// 规则：anywhere-door-X-A ↔ anywhere-door-X。
    /// </summary>
    private string DerivePartnerDoor()
    {
        string name = gameObject.name;
        const string prefix = "anywhere-door-";
        if (!name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return null;
        string key = name.Substring(prefix.Length);
        bool hasA = key.EndsWith("-A", System.StringComparison.OrdinalIgnoreCase);
        if (hasA)
        {
            key = key.Substring(0, key.Length - 2);
            return prefix + key;
        }
        return prefix + key + "-A";
    }

    /// <summary>
    /// 门是同一个开口的两面：从 A 门穿到 B 门，玩家必须绕门轴转半圈，
    /// 才会「背对着对门、面朝对门所在的世界」走出去。
    /// 因此映射恒为 180°（(x,y,z) → (-x,y,-z)），与两扇门在世界里的朝向差无关。
    ///
    /// 旧实现按 |两门 eulerAngles.y 之差| > 90 推导：两门朝向相反时（实验室↔Metro、
    /// 实验室↔工厂昼）恰好算对，而两门同朝向时（工厂昼↔工厂夜、工厂夜↔水淹区）
    /// 会错算成 false —— 表现就是「从夜工厂/水淹区出来时方向是反的，要自己转身 180°」。
    /// </summary>
    private void DeriveMapYaw180(Transform partnerFrame)
    {
        if (!autoPair || partnerFrame == null || srcFrame == null) return;
        derivedMapYaw180 = true;
    }

    /// <summary>
    /// 目标世界被 WorldStreamer 卸下后的收尾：本门留在原世界，但取景的目标全没了——
    /// 必须把缓存引用与取景资源全部放掉，并把背板换回门自己的材质。
    /// 不做这一步，LateUpdate 会继续使用已销毁的目标门（MissingReference），
    /// 面板还会"冻结"在最后一帧画面上。
    /// </summary>
    public void OnTargetWorldUnloaded()
    {
        if (!ready) return;
        ReleaseView();
        ApplyBackSlotMaterial(originalBackMat != null ? originalBackMat : darkMat);
        backPanelDark = true;        // 视作非投影状态：门再开合也不会去碰已销毁的投影材质
        destFrame = null;
        destPlane = null;
        pairedDoor = null;
        ready = false;
    }

    /// <summary>放掉取景相机 / RT / 投影屏材质（OnDestroy 与"世界被卸下"共用）。</summary>
    private void ReleaseView()
    {
        if (portalCam != null) { Destroy(portalCam.gameObject); portalCam = null; }
        if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        if (quadMat != null) { Destroy(quadMat); quadMat = null; }
        currentRtHeight = 0;
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
        // 附带：采样轴自检（比例 / 镜像）。
        // 比例：轴向必须传单位向量、w 传轴长，着色器算的 dot(d,轴)/w 才是 0~1 采样比例。
        // 镜像：不镜像时 U 轴（c1→c0）必须与相机右方向**同向**：RT 的 u=0 在相机左侧、
        //       u=1 在相机右侧，采样轴同向右，面板上 u→1 的那头（c0）才会把相机右侧的
        //       内容显示在门框对应的那一侧；反向即左右翻（早先红标实测：门局部 +X 的
        //       物体出现在窗口右半边 = 正确不镜像）。
        if (quadMat != null && portalCam != null)
        {
            var ud = quadMat.GetVector("_UDir");
            var vd = quadMat.GetVector("_VDir");
            Vector3 uDir = new Vector3(ud.x, ud.y, ud.z);
            Vector3 vDir = new Vector3(vd.x, vd.y, vd.z);
            float align = Vector3.Dot(uDir, portalCam.transform.right);
            if (ResolvedMirrorU) align = -align;
            float uLen = (srcCorners[0] - srcCorners[1]).magnitude;
            float vLen = (srcCorners[3] - srcCorners[0]).magnitude;
            bool scaleOk = Mathf.Abs(uDir.magnitude - 1f) < 0.001f && Mathf.Abs(vDir.magnitude - 1f) < 0.001f
                           && Mathf.Abs(ud.w - uLen) < 0.01f && Mathf.Abs(vd.w - vLen) < 0.01f;
            bool mirrorOk = align > 0f;
            sb.AppendLine("  采样轴：|u|=" + uDir.magnitude.ToString("F4") + " |v|=" + vDir.magnitude.ToString("F4")
                          + " w=(" + ud.w.ToString("F3") + ", " + vd.w.ToString("F3") + ")"
                          + " 实长=(" + uLen.ToString("F3") + ", " + vLen.ToString("F3") + ") → "
                          + (scaleOk ? "OK" : "FAIL"));
            sb.AppendLine("  镜像：dot(U轴, 相机右)=" + align.ToString("F4") + "（应 > 0，同向=不镜像）→ " + (mirrorOk ? "OK" : "FAIL"));
            if (!scaleOk || !mirrorOk) ok = false;
        }
        sb.AppendLine("  结果：" + (ok ? "PASS（偏心视锥正确）" : "FAIL"));
        report = sb.ToString();
        return ok;
    }

    private void OnGUI()
    {
        if (!ready || !showDebugOsd) return;
        GUI.color = Color.white;
        GUI.Label(new Rect(20f, Screen.height - 96f, 700f, 20f),
            "PortalWindow  " + ResolvedWorldSceneName + "  映射Y180=" + ResolvedMapYaw180
            + "  镜像U=" + ResolvedMirrorU + "  RT=" + rt.width + "x" + rt.height);
    }
}
