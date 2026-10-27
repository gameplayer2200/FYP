using System.Collections.Generic;
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

    // ── 一键配对写入的显式配对（比门名推导更硬，供 FYP/门配对 工具填） ──
    // 为什么要有它：同名两扇互配（主门 X / 副门 X-A）里，副门只有名字推不出"主门在哪个世界"——
    // 它需要知道全工程的门分布，运行时不具备。工具扫描全部场景后把答案写在这里，
    // 运行时直接用；pairKey 用来校验"这扇门没被改名"，名变了就作废、退回门名推导。
    [SerializeField] private string pairKey;
    [SerializeField] private string pairPartnerScene;
    [SerializeField] private string pairPartnerDoor;

    [Header("取景参数（Link 为空时的兜底值）")]
    [SerializeField] private int rtHeight = 720;
    [SerializeField] private float farClip = 200f;
    [SerializeField] private float nearClip = 0.05f;
    [SerializeField] private float preloadRadius = 15f;

    /// <summary>
    /// 提前激活距离（米）：玩家走到离门这么近时，就把门背后的世界激活，而不是等按下 E。
    ///
    /// 为什么要有它：激活（`allowSceneActivation=true`）那一帧要实例化整个场景，
    /// 实测 Metro ≈100ms、OfficePack ≈178ms、Laboratory 更久。若等到按 E 才激活，
    /// 这笔开销必然砸在按键那一帧，体感就是"按一下卡一下"。
    /// 设 0 = 关掉提前激活，退回"按键时才激活"的旧行为。
    /// </summary>
    [SerializeField] private float activateAheadDistance = 6f;
    [SerializeField] private bool mirrorU = false;
    [SerializeField] private string openingPath = "Cube.001/Plane";

    [Header("取景分辨率自适应")]
    [Tooltip("勾选后取景 RT 的高度会跟着「门洞此刻在屏幕上占多高」自动升降档：\n" +
             "玩家离得远时门洞只有几十像素，基准值就够；贴到门口时门洞铺满屏幕，\n" +
             "固定 720 会被放大采样，画面糊成一团。关掉则永远用 rtHeight（旧行为）。")]
    [SerializeField] private bool dynamicResolution = true;
    [Tooltip("自适应上限（像素高度）。门洞在屏幕上再大也不会超过它，用来兜住性能。")]
    [SerializeField] private int maxRtHeight = 1440;

    [Header("照片模式（远距离静态帧）")]
    [Tooltip("门洞在屏幕上的像素高度低于此值时，改用「照片模式」：只渲染一帧，之后复用这张静态帧，" +
             "玩家视角变化明显了才重拍。\n\n" +
             "为什么值得做：取景每帧都是一次**完整的目标世界渲染**（Metro 门洞里是整片隧道，几万个三角形）。" +
             "玩家站在实验室另一头时，门洞在屏幕上只有一两百像素高，却仍然每帧付掉这份钱——门一多就是" +
             "每帧多渲好几个世界。Project Doors 的 PortalView 正是用 RenderPhoto 的照片模式处理" +
             "这个区间的：远处用静态帧 + 视差，近处才切真渲染。\n" +
             "设 0 关闭照片模式（永远真渲染，旧行为）。")]
    [SerializeField] private float photoPixelHeight = 260f;
    [Tooltip("退出照片模式的滞回系数：像素高度超过 photoPixelHeight × 此值才切回真渲染，" +
             "免得站在临界距离上反复横跳。")]
    [SerializeField] private float photoExitFactor = 1.35f;
    [Tooltip("玩家相机位移超过这个距离（米）就重拍一帧")]
    [SerializeField] private float photoRefreshDistance = 0.35f;
    [Tooltip("玩家相机转动超过这个角度（度）就重拍一帧")]
    [SerializeField] private float photoRefreshAngle = 2.5f;
    [Tooltip("照片最长存活时间（秒）。即使玩家完全不动也会定期重拍——" +
             "门里的世界是活的（灯在灭、异常在动），一张永不更新的画面会变成「门里按了暂停」。")]
    [SerializeField] private float photoMaxAge = 0.5f;

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
    /// <summary>目标门的渲染器缓存（建立取景时抓一次）。取景时逐个临时隐藏，渲染完恢复。</summary>
    private Renderer[] destRenderers;
    /// <summary>各渲染器在"隐藏前"的启用状态，恢复时按它还原（不是一律 true）。</summary>
    private bool[] destRendererWasEnabled;
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

    /// <summary>
    /// 贴身豁免半径（米）的平方：玩家离门这么近时跳过"门洞是否在屏幕上"的判定。
    /// 站进门洞里时四角全在屏幕外，四角判定会误判为不可见，画面反而会冻住。
    /// 门洞宽 1.225m，2.5m 覆盖"半个身子进门 + 贴门侧看"的整个区间。
    /// </summary>
    private const float CloseBypassSqr = 2.5f * 2.5f;
    private float prevDot;               // 上一帧玩家在门平面法线上的带符号距离（穿行判定基准）
    private float cooldown;              // 穿行冷却

    // ── 照片模式（远距离静态帧）──
    /// <summary>当前是否处于照片模式（本帧没跑真渲染，门洞里贴的是上一张照片）。</summary>
    private bool photoMode;
    /// <summary>上次真渲染时玩家相机的位置 / 朝向（判断"视角变了多少"的基准）。</summary>
    private Vector3 photoEyePos;
    private Quaternion photoEyeRot;
    /// <summary>上次真渲染的时刻（unscaledTime），用于 photoMaxAge 兜底重拍。</summary>
    private float photoTakenAt = -999f;
    /// <summary>相机在照片模式下的最近一次 True 渲染次数，仅用于 SelfTest / 诊断。</summary>
    private int photoRenders;
    private int liveRenders;
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

    /// <summary>本帧是否处于照片模式（门洞里贴的是上一帧的静态画面）。诊断用。</summary>
    public bool InPhotoMode { get { return photoMode; } }

    /// <summary>真渲染过多少帧（照片模式省掉的帧不计入）。诊断用。</summary>
    public int PhotoRenders { get { return photoRenders; } }

    /// <summary>走的真渲染分支的总帧数（含照片模式下偶尔的重拍）。诊断用。</summary>
    public int LiveRenders { get { return liveRenders; } }

    // ── 方案 B：统一解析（autoPair 时推导，否则用 Link） ──
    // pairKey 校验"这扇门没被改名"：一键配对写下的 explicit 配对只对当时的门名有效，
    // 改名后必须作废退回门名推导，否则会指着一个已经不存在的门。
    private bool HasExplicitPair
    {
        get
        {
            return !string.IsNullOrEmpty(pairKey)
                && string.Equals(pairKey, gameObject.name, System.StringComparison.Ordinal)
                && !string.IsNullOrEmpty(pairPartnerScene);
        }
    }

    private string ResolvedWorldSceneName
    {
        get
        {
            if (!autoPair) return link != null ? link.WorldSceneName : null;
            if (HasExplicitPair) return pairPartnerScene;
            return DerivePartnerScene();
        }
    }

    private string ResolvedWorldDoorName
    {
        get
        {
            if (!autoPair) return link != null ? link.WorldDoorName : null;
            if (HasExplicitPair && !string.IsNullOrEmpty(pairPartnerDoor)) return pairPartnerDoor;
            return DerivePartnerDoor();
        }
    }

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

        ActivateAheadIfApproaching();

        if (ready || failed) return;

        // 玩家还远就不建立取景。半径由门配对配置；≤0 表示开局立即建立
        float radius = ResolvedPreloadRadius;
        if (radius > 0f && (PlayerRef.Root.position - srcFrame.position).sqrMagnitude > radius * radius) return;

        // 目标世界可能已由其它门装好——优先复用，绝不重复加载
        if (!WorldStreamer.IsWorldReady(ResolvedWorldSceneName)) return;
        OnWorldLoaded(null);
    }

    /// <summary>
    /// 玩家正在走近门（还没按键）时，提前把这个门背后的世界激活。
    ///
    /// 为什么需要它：`WorldStreamer.ActivateOnApproach=false` 之后，激活的唯一入口是
    /// `AnywhereDoor.Interact` 里的 `RequestActivation`——于是"激活整个场景"这件整帧级的重活
    /// 必然落在玩家按下 E 的那一帧（实测 Metro 100ms、OfficePack 178ms），体感就是"按一下卡一下"。
    ///
    /// 这里把时机往前挪到"走到门前 6m"。玩家走完这 6m 的十几帧里，
    /// 卡顿被分摊进"靠近门"的过程，按键那一帧就只剩开门动画。
    /// 6m 是个折中：太近（1~2m）等于没提前；太远会让挤在一起的门在实验室里
    /// 一次点亮好几个世界——那正是 ActivateOnApproach 被关掉的原因
    /// （实验室出生点离 FactoryDay 门 1.62m、离 Metro 门 8.39m，10m 会在开局全点亮）。
    /// </summary>
    private void ActivateAheadIfApproaching()
    {
        if (activateAheadDistance <= 0f) return;          // 显式关掉

        string world = ResolvedWorldSceneName;
        if (string.IsNullOrEmpty(world)) return;
        if (WorldStreamer.IsWorldReady(world)) return;    // 已经好了，没什么可提前的

        Vector3 d = PlayerRef.Root.position - srcFrame.position;
        if (d.sqrMagnitude > activateAheadDistance * activateAheadDistance) return;

        WorldStreamer.RequestActivation(world);
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
        // 修好配对后重进 Play 即可（或点菜单 FYP/门配对/一键配对并配齐）。
        // 找门时按候选名列表逐个试（见 DerivePartnerDoorCandidates）：对面名字差一点也能连上。
        var partnerCandidates = DerivePartnerDoorCandidates(gameObject.name, gameObject.scene.name);
        destFrame = FindDoor(scene, partnerCandidates);
        if (destFrame == null)
        {
            Debug.LogError("[PortalWindow] 目标世界里找不到门：" + ResolvedWorldDoorName
                           + "（" + ResolvedWorldSceneName + "）。已尝试的名字："
                           + string.Join(" / ", partnerCandidates.ToArray())
                           + "。配对已失效，本门停用。"
                           + "请用 FYP/门配对/一键配对并配齐 修复。", this);
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
        CacheDestRenderers();      // 必须在 CreateRt 之前？无关；但必须在 ready 之前（渲染时要用）
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
        StartPrewarm();          // 把"第一次渲染取景"的开销提前到装载时刻（见方法注释）
        Debug.Log("[PortalWindow] 取景已建立：源=" + srcFrame.name + " 目标=" + destFrame.name
                  + " RT=" + rt.width + "x" + rt.height, this);
    }

    /// <summary>
    /// 在已加载的目标场景里按名字找门。
    /// 这里保留按名字找是**刻意的**：门配对天然跨场景，而 Unity 无法序列化指向未加载
    /// 场景中对象的引用。名字来自命名约定或 WorldLink 资产（配置数据），不是代码里的魔法字符串。
    /// 见 docs/adr/0002-cross-scene-refs-are-data.md。
    ///
    /// 三种匹配，从严到松：
    /// 1) 精确名；
    /// 2) 连字符/下划线互换后再精确匹配（场景名 `Flooded_Grounds` ↔ 门名 `Flooded-Grounds`）；
    /// 3) 包含匹配（`anywhere-door-X` 能命中 `anywhere-door-X (1)`、`anywhere-door-X-open` 等变体）。
    /// 注意第 3 条必须放在最后：它太宽松，先跑会把 `anywhere-door-Metro` 错配到
    /// `anywhere-door-Metro-A` 上（同名前缀的门在同一场景里并存时就会发生）。
    /// </summary>
    private static Transform FindDoor(Scene scene, string doorName)
    {
        if (string.IsNullOrEmpty(doorName)) return null;
        return FindDoor(scene, new List<string> { doorName });
    }

    /// <summary>
    /// 按候选名列表找门，**按列表顺序**逐个试（列表由
    /// <see cref="DerivePartnerDoorCandidates"/> 按可能性从高到低生成）。
    ///
    /// 为什么需要列表：对面那扇门的名字常常"差一点"——多了个 `-A`、少了个连字符、
    /// 或者世界改过名而门名还是旧的。旧实现只认一个标准名，差一点就判「配对已失效、
    /// 本门停用」，于是必须跑一遍编辑器工具才能用。现在运行时自己会试几种写法，
    /// 只要对面确实摆着一扇门就能连上；工具仍然会把名字改成标准写法。
    ///
    /// 每个候选名内部三级匹配，从严到松：
    /// 1) 精确名；
    /// 2) 连字符/下划线互换后再精确匹配（场景名 `Flooded_Grounds` ↔ 门名 `Flooded-Grounds`）；
    /// 3) 包含匹配（`anywhere-door-X` 能命中 `anywhere-door-X (1)`、`anywhere-door-X-open` 等变体）。
    /// 注意第 3 条**只在所有候选名的前两级都失败后**才跑：它太宽松，先跑会把
    /// `anywhere-door-Metro` 错配到 `anywhere-door-Metro-A` 上（同名前缀的门在同一场景里并存时就会发生）。
    /// </summary>
    private static Transform FindDoor(Scene scene, List<string> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        var roots = scene.GetRootGameObjects();

        // 前两级：精确名 / 连字符↔下划线互换，按候选优先级逐个试
        foreach (var name in candidates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            foreach (var root in roots)
            {
                var f = root.transform.Find(name);
                if (f != null) return f;
            }
            string swapped = name.Replace('-', '_');
            if (swapped == name) swapped = name.Replace('_', '-');
            if (swapped != name)
            {
                foreach (var root in roots)
                {
                    var f = root.transform.Find(swapped);
                    if (f != null) return f;
                }
            }
        }

        // 第 3 级：包含匹配兜底（见上）
        foreach (var name in candidates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            string key = name.ToLower();
            string keySwapped = name.Replace('-', '_').ToLower();
            foreach (var root in roots)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLower();
                    if (n.Contains(key) || n.Contains(keySwapped)) return t;
                }
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
        TickPrewarm();       // 装载后隔一帧做一次"取景预热"，把首次渲染开销提前消化
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

        // 第二道闸：门洞根本不在画面里（背后 / 视野外）就不渲染。
        // 距离判定只能挡"远处"，挡不住"背对着门"——玩家背对门往隧道里走时，
        // 门在身后十几米内、距离闸拦不住，但屏幕上根本没有它，这一帧的
        // 虚拟相机渲染 + RT 拷贝就是纯浪费（Metro 门洞里是整片隧道，几万个三角形）。
        // 文档里 Project Doors 用的是 CheckVisabilityByCast 射线 + 门框四角 FindMinZ 视锥判定，
        // 这里用等价但更便宜的版本：把门洞四角投到 NDC，全在屏幕外就跳过。
        //
        // ⚠️ 近身豁免：玩家站到门洞里时，四个门框角全都跑到屏幕外，四角判定会判"不可见"，
        // 于是最容易看清门内的那一瞬间反而停渲染（画面冻住/发黑）。
        // 所以 2.5m 以内直接跳过这道闸——贴身时不管朝向一律渲染。
        if ((playerCam.transform.position - srcFrame.position).sqrMagnitude > CloseBypassSqr
            && !IsOpeningOnScreen(playerCam)) return;

        // 第三道闸：门关着时门洞里贴的是门自己的材质，投影屏根本没有露出来，
        // 渲染这一帧纯粹是白烧（关着的门占全场时间的大头——玩家在实验室里来回走时，
        // 三扇门有 90% 的时间是关的）。
        // 判据用 backPanelDark（由 SyncBackPanel 在本帧开头刷新的实际门板角度），
        // 不用 door.IsOpen（那是意图值，门板还在转的过程中就已经是 true 了）。
        // 开门那一帧 backPanelDark 会先翻成 false，接着就恢复渲染，不会漏帧。
        // 门组件缺失时（结构异常）不启用这道闸，宁可多渲染也不要一片死黑。
        if (door != null && backPanelDark) return;

        // 第四道闸：照片模式 —— 门洞在屏幕上只占一小块时，不必每帧全量渲染。
        //
        // 前三道闸挡的是"不该渲染的门"（太远 / 不在画面里 / 关着）。
        // 这一道挡的是**该渲染但不必每帧渲染**的门：玩家站在实验室另一头，
        // 门洞在屏幕上只有一两百像素高，里面的世界却仍然每帧整场渲染一遍
        // （Metro 门洞是整片隧道，几万个三角形）。门一多，就是每帧多渲好几个世界。
        //
        // 做法与 Project Doors 的 PortalView.RenderPhoto 等价：
        //   · 门洞像素高度低于 photoPixelHeight → 进照片模式；
        //   · 照片模式下只在"视角变化明显"（位移 > photoRefreshDistance 或转角 > photoRefreshAngle）
        //     或"照片太旧"（> photoMaxAge）时才真渲染一帧，其余帧直接复用上一帧；
        //   · 退出用滞回（photoExitFactor），避免站在临界距离上反复横跳。
        //
        // 为什么不是"永远复用"：门里的世界是活的（Metro 的灯在逐组熄灭、异常在动）。
        // 一张永不更新的画面会变成"门里按了暂停"，比卡顿更出戏。photoMaxAge 兜住这一点。
        if (ShouldUsePhoto(playerCam)) return;

        UpdatePortalCamera(playerCam);
        UpdateRtResolution(playerCam);
        RenderPortal();
        photoTakenAt = Time.unscaledTime;
        photoEyePos = playerCam.transform.position;
        photoEyeRot = playerCam.transform.rotation;
        photoMode = false;
        liveRenders++;
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
        rt = PortalRtPool.Get(Mathf.RoundToInt(h * aspect), h);
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

        // 换档：池化后不能在原对象上改尺寸（那张 RT 可能正被别的门借用），
        // 而是「还一张 + 借一张」。同尺寸的 RT 会在池里直接命中，等于零分配。
        int newW = Mathf.Max(1, Mathf.RoundToInt(wanted * rtAspect));
        currentRtHeight = wanted;
        var oldRt = rt;
        rt = PortalRtPool.Get(newW, wanted);
        if (quadMat != null) quadMat.mainTexture = rt;   // 投影屏要立刻改指向，否则采样的还是旧图
        if (portalCam != null) portalCam.targetTexture = rt;
        PortalRtPool.Release(oldRt);                     // 新的已挂上，旧的才能还
    }

    /// <summary>
    /// 本帧是否走「照片模式」（不真渲染，直接复用上一张照片）。
    ///
    /// 判据只有三条，但进出方向不对称：
    ///   · 进入：门洞像素高度 &lt; photoPixelHeight（严格）
    ///   · 退出：门洞像素高度 ≥ photoPixelHeight × photoExitFactor（滞回，防抖动）
    ///   · 照片模式下：视角位移/转角超过阈值，或照片超过 photoMaxAge → 允许真渲染一帧（拍照）
    ///
    /// 没拍过照片（photoTakenAt 是哨兵值）时一律放行真渲染，否则门洞里会一直是黑的。
    /// photoPixelHeight ≤ 0 表示关闭照片模式，退回永远真渲染的旧行为。
    /// </summary>
    private bool ShouldUsePhoto(Camera playerCam)
    {
        if (photoPixelHeight <= 0f) return false;
        if (rt == null || portalCam == null) return false;
        // 还没拍过任何一帧 → 必须真渲染，否则门洞是黑的
        if (photoRenders <= 0 || Time.unscaledTime - photoTakenAt > 1e9f) return false;

        float px = OpeningPixelHeight(playerCam);
        if (px <= 0f) return false;   // 数据不可信（角点退化等）→ 走真渲染

        float enterAt = photoPixelHeight;
        float exitAt = photoPixelHeight * Mathf.Max(1.01f, photoExitFactor);
        if (photoMode) { if (px >= exitAt) { photoMode = false; return false; } }
        else { if (px >= enterAt) return false; photoMode = true; }

        // 已在照片模式：只有当视角几乎没动、照片还新，才复用旧帧
        if (Time.unscaledTime - photoTakenAt >= photoMaxAge) return false;

        Vector3 dp = playerCam.transform.position - photoEyePos;
        if (dp.sqrMagnitude >= photoRefreshDistance * photoRefreshDistance) return false;

        float ang = Quaternion.Angle(playerCam.transform.rotation, photoEyeRot);
        if (ang >= photoRefreshAngle) return false;

        return true;   // 复用上一帧：这一帧省掉整场目标世界渲染
    }

    /// <summary>
    /// 门洞此刻在玩家相机屏幕上的像素高度。数据退化时返回 0（调用方应当退回真渲染）。
    ///
    /// 与 <see cref="UpdateRtResolution"/> 用的是同一套投影（门洞上下边中点投到 NDC），
    /// 故意不合并成一个方法：那边要的是"RT 该多大"，这边要的是"该不该渲染"，
    /// 两者的滞回与失效策略不同，硬合并会让任何一方的调整都影响另一方。
    /// </summary>
    private float OpeningPixelHeight(Camera playerCam)
    {
        if (playerCam == null) return 0f;
        if (srcCorners[0] == srcCorners[2] && srcPlane != null)
            ReadCorners(srcPlane, srcCorners);

        float ndcTop = ProjectNdcY(playerCam, srcCorners[3], srcCorners[2]);
        float ndcBottom = ProjectNdcY(playerCam, srcCorners[0], srcCorners[1]);
        if (float.IsNaN(ndcTop) || float.IsNaN(ndcBottom)) return 0f;

        float fraction = Mathf.Abs(ndcTop - ndcBottom) * 0.5f;   // NDC 高度 2 = 铺满屏幕
        if (fraction <= 0.0001f) return 0f;
        return fraction * playerCam.pixelHeight;
    }

    /// <summary>
    /// 门洞是否落在玩家相机画面上（哪怕只露一角）。
    ///
    /// 判据：门洞四角逐个投到视口空间，任意一角在相机前方且视口坐标落在
    /// [-m, 1+m] 内就算可见。留 15% 余量是因为四角都在屏幕外、但门洞中间
    /// 横穿画面时（贴着门侧身看）会漏判——余量把这种情况兜回来。
    ///
    /// 它不是精确的遮挡剔除（门后面隔一堵墙时仍会判可见），但那种情况有距离闸兜着；
    /// 这里要挡的是"完全背对门"这种最浪费、最常见的情况，用最便宜的算法就够。
    /// </summary>
    private bool IsOpeningOnScreen(Camera playerCam)
    {
        // 自愈（与 UpdateRtResolution 同款）：门洞角点被清成零向量时（编辑器刷新/场景重载后
        // 实测出现过全 0），中心点会退化到世界原点，快速否定判据直接判"背后"→ 门洞永远不渲染。
        if (srcCorners[0] == srcCorners[2] && srcPlane != null)
            ReadCorners(srcPlane, srcCorners);

        const float margin = 0.15f;
        // 快速否定：门洞中心在相机背后 → 整扇门都在背后（相机朝前看，背后不可能有可见物）
        Vector3 center = (srcCorners[0] + srcCorners[2]) * 0.5f;
        if (Vector3.Dot(center - playerCam.transform.position, playerCam.transform.forward) <= 0f)
            return false;

        for (int i = 0; i < 4; i++)
        {
            Vector3 v = playerCam.WorldToViewportPoint(srcCorners[i]);
            if (v.z <= 0f) continue;                 // 这个角在背后，看其余的
            if (v.x >= -margin && v.x <= 1f + margin
             && v.y >= -margin && v.y <= 1f + margin) return true;
        }
        return false;
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
        // relDist 为 0 时归一化会得到 NaN，进而让整个投影矩阵非法
        //（控制台报 Assertion failed: IsMatrixValid(matrix)）。
        if (relDist > 1e-4f && relDist > maxRecede) camPos = planeCenter + rel * (maxRecede / relDist);
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

        // 非法矩阵防护：目标世界刚激活的那一帧、或门洞四角退化（重合/共线）时，
        // 上面算出的数可能是 NaN / Infinity / 零面积。直接喂给 Frustum 会让 Unity 每帧
        // 报 Assertion failed: IsMatrixValid(matrix)，而且门洞画面彻底花掉。
        // 这种情况跳过这一帧的投影更新，下一帧数据正常了自然会重算。
        if (!IsFinite(minX) || !IsFinite(maxX) || !IsFinite(minY) || !IsFinite(maxY) || !IsFinite(planeZ)) return;
        if (maxX - minX < 1e-4f || maxY - minY < 1e-4f) return;

        float d2 = Mathf.Max(ResolvedNearClip * 1.01f, Mathf.Abs(planeZ));
        float s = ResolvedNearClip / d2;
        var proj = Matrix4x4.Frustum(minX * s, maxX * s, minY * s, maxY * s, ResolvedNearClip, ResolvedFarClip);
        // Frustum 本身不会抛错，但 NaN 会悄悄进去；这里再校验一次整体
        if (!IsFinite(proj.m00) || !IsFinite(proj.m11) || !IsFinite(proj.m22) || !IsFinite(proj.m23)) return;
        portalCam.projectionMatrix = proj;
        // 千万不要在设完 projectionMatrix 之后再碰 aspect / fieldOfView / orthographicSize——
        // 任何一个都会让 Unity 按 FOV 重算投影矩阵，把上面的偏心视锥整个覆盖掉
        //（宽高比已经包含在 Frustum 参数里）。
    }

    private static bool IsFinite(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    // ─────────────────────────────────────────────────────────────
    // 取景预热：把"第一次看这个世界的开销"从玩家眼前挪到装载时刻
    // ─────────────────────────────────────────────────────────────
    //
    // 实测（游戏内看门狗，不经过任何外部工具）：
    //   玩家从远处走到 Metro 门前的那一帧，单帧 485ms —— 期间装载状态机是空的
    //   （装载=待激活:FactoryDay，与 Metro 无关），GC 只涨 1.2MB。
    //   同一会话里把玩家挪远再挪回、连做 3 轮，每轮都是 0ms（场景数始终 3，没被卸载）。
    //
    // 结论：那 485ms 不是"装载世界"，而是**第一次用门户相机把这个世界的材质/着色器
    // 真正渲染出来**的一次性成本（运行时着色器编译 + 纹理/网格首上传）。
    // 三个世界在开局就都装好了，所以"第 2、3 轮不卡"完全符合这个解释。
    //
    // 修法（Project Doors 的 PortalView 就是这么干的）：
    //   世界一装载完成、取景一建立，就立刻替玩家"看一眼"——用门户相机渲染一次到 RT，
    //   把这笔一次性成本花在装载帧里（那一帧本来就在卡，玩家被加载屏/开门动画挡着），
    //   之后玩家走到门前时只是常规渲染，不再有 400~500ms 的停顿。
    //
    // 分两帧做：世界刚激活的那一帧已经在付实例化成本，再叠一次全场景渲染容易把帧拉到 1 秒以上，
    // 反而让加载屏更久。隔一帧再做，总耗时接近不变但峰值减半。
    private void StartPrewarm()
    {
        if (prewarmPending) return;
        prewarmPending = true;
        prewarmFrame = Time.frameCount + 1;   // 下一帧再渲染
    }

    private bool prewarmPending;
    private int prewarmFrame = -1;

    /// <summary>预热：用门户相机渲染一帧（结果不显示，只为触发着色器编译/资源上传）。</summary>
    private void TickPrewarm()
    {
        if (!prewarmPending) return;
        if (Time.frameCount < prewarmFrame) return;

        prewarmPending = false;
        if (!ready || portalCam == null) return;

        LoadDiag.Begin();
        try
        {
            // 与正式渲染走完全相同的路径：同样的相机参数、同样的隐藏门体，
            // 这样编译出来的着色器变体与真正取景时一致。
            HideDestinationDoor(true);
            portalCam.Render();
        }
        catch (System.Exception e)
        {
            // 预热失败不该影响玩法：吞掉异常，正式渲染时自然会再试一次
            Debug.LogWarning("[PortalWindow] 取景预热失败（不影响正常取景）：" + e.Message);
        }
        finally
        {
            // ⚠️ 必须放 finally：HideDestinationDoor(true) 之后任何异常都不能让确认隐藏的门体
            // 永远留在隐藏状态——那会让对面那扇门在玩家眼里凭空消失，比卡顿更严重。
            HideDestinationDoor(false);
        }
        LoadDiag.End("取景预热 " + (srcFrame != null ? srcFrame.name : name));
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
        //
        // 渲染器列表在建立取景时缓存一次（destRenderers）：早先每帧 new List + 
        // GetComponentsInChildren 遍历整棵子树，门多的时候是纯浪费——门体的渲染器
        // 在一次装载生命周期内不会增减（门板开合只改 Transform，不改结构）。
        HideDestinationDoor(true);
        portalCam.Render();
        HideDestinationDoor(false);
        photoRenders++;

        if (diag) LoadDiag.End("渲染取景 " + (srcFrame != null ? srcFrame.name : name));
    }

    /// <summary>
    /// 批量开关目标门的渲染器。用缓存数组 + 索引循环，零 GC。
    /// 缓存为 null（目标世界刚卸下 / 尚未建立取景）时什么都不做。
    ///
    /// 恢复时**逐个还原隐藏前的状态**，而不是一律置 true：
    /// 门体上有个别渲染器本来就该是关的（例如某些世界把它们关掉了），
    /// 一律置 true 会让它们凭空出现。
    /// </summary>
    private void HideDestinationDoor(bool hide)
    {
        if (destRenderers == null) return;
        if (hide)
        {
            for (int i = 0; i < destRenderers.Length; i++)
            {
                var r = destRenderers[i];
                destRendererWasEnabled[i] = r != null && r.enabled;
                if (destRendererWasEnabled[i]) r.enabled = false;
            }
        }
        else
        {
            for (int i = 0; i < destRenderers.Length; i++)
            {
                var r = destRenderers[i];
                if (r != null && destRendererWasEnabled[i]) r.enabled = true;
            }
        }
    }

    /// <summary>
    /// 取景并缓存目标门的所有渲染器。
    /// 只做一次：装载后门体结构固定，逐帧重建列表等于每帧制造垃圾。
    /// </summary>
    private void CacheDestRenderers()
    {
        destRenderers = null;
        destRendererWasEnabled = null;
        if (destFrame == null) return;
        var found = destFrame.GetComponentsInChildren<Renderer>(true);
        if (found == null || found.Length == 0) return;
        destRenderers = found;
        destRendererWasEnabled = new bool[found.Length];
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
    ///
    /// ── 唯一的约定：同名两扇互配 ──────────────────────────────────
    /// 出发那一侧放**主门** `anywhere-door-X`，要去的那个世界里放**副门** `anywhere-door-X-A`，
    /// 两扇只差一个 `-A`，配成一对。X 习惯取目标世界的名字
    /// （实验室里那扇 `anywhere-door-Metro` 通的就是 Metro，Metro 那侧叫 `anywhere-door-Metro-A`），
    /// 但判据只看后缀——所以链式、环状、星形，任何拓扑都只是名字，不需要额外配置。
    ///
    /// 完整判据集中在 <see cref="TryDerivePairing"/>。
    /// </summary>
    private string DerivePartnerScene()
    {
        string scene, partner;
        return TryDerivePairing(gameObject.name, gameObject.scene.name, out scene, out partner) ? scene : null;
    }

    /// <summary>
    /// 一键配对工具写入显式配对（副门靠门名推不出主门在哪个世界，需要全工程扫描才知道）。
    /// 传空值清空，退回门名推导。sourceKey 就是写入时的门名，改名后配对自动作废。
    /// </summary>
    public void SetExplicitPairing(string sourceKey, string partnerScene, string partnerDoor)
    {
        pairKey = string.IsNullOrEmpty(partnerScene) ? null : sourceKey;
        pairPartnerScene = string.IsNullOrEmpty(partnerScene) ? null : partnerScene;
        pairPartnerDoor = string.IsNullOrEmpty(partnerScene) ? null : partnerDoor;
        failed = false;      // 配对换了，允许重试建立取景
        ready = false;
        ReleaseView();
    }

    /// <summary>当前是否用了显式配对（一键工具写下的），供诊断工具查。</summary>
    public bool UsingExplicitPairing { get { return HasExplicitPair; } }

    /// <summary>显式配对的目标世界（没有则为空）。</summary>
    public string ExplicitPartnerScene { get { return HasExplicitPair ? pairPartnerScene : null; } }

    /// <summary>
    /// 显式配对是否已经就是这条。一键工具靠它做幂等——不然每次点一下都会把
    /// 已经正确的门标脏、重存一遍场景（LFS 场景文件动辄上百 MB，白写一次很贵）。
    /// </summary>
    public bool ExplicitPairingIs(string sourceKey, string partnerScene, string partnerDoor)
    {
        // 传空 = 要求"没有显式配对"。这里看原始字段而不是 HasExplicitPair：
        // 门一旦改名，pairKey 就失效（HasExplicitPair 变假），但三个字段还赖在场景里，
        // 趁这次清理一并抹掉，免得日后门又改回原名时旧答案诈尸。
        if (string.IsNullOrEmpty(partnerScene))
            return string.IsNullOrEmpty(pairKey)
                && string.IsNullOrEmpty(pairPartnerScene)
                && string.IsNullOrEmpty(pairPartnerDoor);

        return string.Equals(pairKey, sourceKey, System.StringComparison.Ordinal)
            && string.Equals(pairPartnerScene, partnerScene, System.StringComparison.Ordinal)
            && string.Equals(pairPartnerDoor, partnerDoor, System.StringComparison.Ordinal);
    }


    /// <summary>门名前缀（所有传送门对象的命名约定）。</summary>
    public const string DoorNamePrefix = "anywhere-door-";

    /// <summary>起始世界（实验室）场景名：命名推导的兜底目标。</summary>
    public const string HomeSceneName = "SpecimenLab";

    /// <summary>
    /// 门名推导的唯一判据（编辑器工具与运行时共用，避免两边规则漂移）。
    ///
    /// ── 规则：同名两扇互配 ────────────────────────────────────────
    /// 出发侧放**主门** `anywhere-door-X`，要去那个世界里放**副门** `anywhere-door-X-A`，
    /// 两扇只差一个 `-A`，配成一对。X 习惯取目标世界的名字，于是：
    ///   实验室/`anywhere-door-Metro`  →  Metro/`anywhere-door-Metro-A`
    ///   工厂昼/`anywhere-door-FactoryNight` → 工厂夜/`anywhere-door-FactoryNight-A`
    ///
    /// **主门**：门名里的 X 就是目标世界（X 必须是 Build Settings 里的真实场景），对面叫 X-A。
    /// **副门**：对面叫 X（剥掉 -A），但 X 只是"这一对的名字"，推不出主门在哪个世界——
    ///        星形拓扑下主门都在实验室，所以兜底回实验室；链式/环状由一键配对
    ///        写入的 <see cref="pairPartnerScene"/> 覆盖（见 <see cref="ResolvedWorldSceneName"/>）。
    /// </summary>
    /// <param name="doorName">门对象名，形如 `anywhere-door-Metro` / `anywhere-door-Metro-A`。</param>
    /// <param name="ownSceneName">本门所在场景名（用于排除"指向自己"）。</param>
    /// <param name="scene">推导出的目标世界场景名。</param>
    /// <param name="partnerDoor">推导出的目标世界里那扇门的对象名。</param>
    /// <returns>名字符合约定返回 true；不以门名前缀开头返回 false。</returns>
    public static bool TryDerivePairing(string doorName, string ownSceneName,
                                        out string scene, out string partnerDoor)
    {
        scene = null;
        partnerDoor = null;
        if (string.IsNullOrEmpty(doorName)) return false;
        if (!doorName.StartsWith(DoorNamePrefix, System.StringComparison.OrdinalIgnoreCase)) return false;
        string key = doorName.Substring(DoorNamePrefix.Length);
        if (string.IsNullOrEmpty(key)) return false;

        if (key.EndsWith("-A", System.StringComparison.OrdinalIgnoreCase))
        {
            // 副门：同一对的另一扇叫 X（剥掉 -A）
            string stripped = key.Substring(0, key.Length - 2);
            partnerDoor = DoorNamePrefix + stripped;
            string w = ResolveSceneName(stripped);
            scene = (w != null && !string.Equals(w, ownSceneName, System.StringComparison.Ordinal))
                    ? w : HomeSceneName;
            return true;
        }

        // 主门：门名里的世界就是要去的地方，对面那扇叫 X-A
        partnerDoor = DoorNamePrefix + key + "-A";
        string target = ResolveSceneName(key);
        scene = (target != null && !string.Equals(target, ownSceneName, System.StringComparison.Ordinal))
                ? target : HomeSceneName;
        return true;
    }

    /// <summary>
    /// 目标世界里"可能是对面那扇门"的所有候选名（按可能性从高到低）。
    ///
    /// 运行时找门时按这个列表逐个试：标准名找不到就试 `-A` / `A` 变体，
    /// 再试连字符↔下划线互换。这样"名字差一点"的门在**运行时**也能连上，
    /// 不必等到跑一遍编辑器工具才能用（工具仍然会把它改成标准名）。
    /// </summary>
    public static List<string> DerivePartnerDoorCandidates(string doorName, string ownSceneName)
    {
        var names = new List<string>();
        string scene, partner;
        if (!TryDerivePairing(doorName, ownSceneName, out scene, out partner)) return names;
        if (!string.IsNullOrEmpty(partner))
        {
            AddName(names, partner);
            AddName(names, partner + "-A");                 // 对面多了个 -A 后缀
            AddName(names, partner + "A");                  // 少了个连字符
            AddName(names, partner.Replace('-', '_'));      // 连字符 ↔ 下划线
            AddName(names, partner.Replace('_', '-'));
            if (partner.EndsWith("-A", System.StringComparison.OrdinalIgnoreCase))
                AddName(names, partner.Substring(0, partner.Length - 2));   // 对面少了 -A
        }
        return names;
    }

    /// <summary>把门名（或世界名片段）解析成真实场景名；失败返回 null。供编辑器工具复用，避免两边判定漂移。</summary>
    public static string ResolveSceneNamePublic(string key) { return ResolveSceneName(key); }

    /// <summary>两个门名是否指向"同一扇门"（宽松比较，见 <see cref="SameDoorName"/>）。</summary>
    public static bool SameDoorNamePublic(string a, string b) { return SameDoorName(a, b); }

    /// <summary>门名归一化（去前缀、小写、`-`→`_`、剥 `-A` 与世界名尾巴）。</summary>
    public static string NormalizeDoorKeyPublic(string doorName) { return NormalizeDoorKey(doorName); }

    /// <summary>
    /// 两个门名是否指向"同一扇门"。判据刻意宽松——去前缀、大小写不敏感、
    /// 连字符↔下划线互换、去掉 `-A` / `_game` 这类尾巴后相同，或一方是另一方的前缀。
    ///
    /// 为什么不能直接比字符串：互指检查要能容忍真实工程里到处存在的"差一点"——
    /// 工厂昼那扇通往工厂夜的门叫 `anywhere-door-FactoryNight-A`，而工厂夜那扇
    /// 回工厂昼的门按约定该找 `anywhere-door-FactoryNight`；两者是同一扇门的两端，
    /// 名字却差一个后缀。严格比较会让这类门永远拿不到"互指"分，被判成单向门。
    /// </summary>
    public static bool SameDoorName(string a, string b)
    {
        string ka = NormalizeDoorKey(a);
        string kb = NormalizeDoorKey(b);
        if (ka.Length == 0 || kb.Length == 0) return false;
        if (string.Equals(ka, kb, System.StringComparison.Ordinal)) return true;
        return ka.StartsWith(kb, System.StringComparison.Ordinal)
            || kb.StartsWith(ka, System.StringComparison.Ordinal);
    }

    /// <summary>门名归一化：去前缀、转小写、`-`→`_`、剥掉 `-A`/`_a` 与世界名尾巴。</summary>
    private static string NormalizeDoorKey(string doorName)
    {
        if (string.IsNullOrEmpty(doorName)) return string.Empty;
        string s = doorName;
        if (s.StartsWith(DoorNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            s = s.Substring(DoorNamePrefix.Length);
        s = s.Replace('-', '_').ToLowerInvariant();
        if (s.EndsWith("_a")) s = s.Substring(0, s.Length - 2);
        string[] tails = { "_game", "_scene", "_level" };
        foreach (var t in tails)
            if (s.EndsWith(t, System.StringComparison.Ordinal))
            { s = s.Substring(0, s.Length - t.Length); break; }
        return s;
    }

    /// <summary>
    /// 单次、无打分的推导（只走固定顺序）。
    /// 仅用于互指检测——必须是纯函数且不递归，否则对称性检查会自己调用自己无限展开。
    /// </summary>
    private static bool ResolvePairingOnce(string doorName, string ownSceneName,
                                           out string scene, out string partnerDoor)
    {
        return TryDerivePairing(doorName, ownSceneName, out scene, out partnerDoor);
    }

    private static void AddName(List<string> names, string n)
    {
        if (string.IsNullOrEmpty(n)) return;
        if (!names.Contains(n)) names.Add(n);
    }

    /// <summary>
    /// 工程里可装载的场景名集合（Build Settings 的运行时视图）。
    ///
    /// 为什么要自己建表：门名推导必须能区分"名字里的这段是真实世界"还是"只是门名的一部分"
    /// （`anywhere-door-Metro-A` 的 `Metro-A` 不是世界，`Metro` 才是）。
    /// 而 `Application.CanStreamedLevelBeLoaded` 在**编辑器非 Play 模式下不可靠**
    /// （实测对已在 Build Settings 里的场景也返回 false），会让所有门都退回"回实验室"，
    /// 于是编辑器工具与运行时得出两套不同结论。改用 `SceneUtility.GetScenePathByBuildIndex`
    /// 逐个取路径，编辑器与运行时都成立。
    /// </summary>
    private static HashSet<string> buildSceneNames;

    private static bool IsBuildScene(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        EnsureSceneCache();
        return buildSceneNames.Contains(name);
    }

    /// <summary>建场景名表（惰性，只建一次）。</summary>
    private static void EnsureSceneCache()
    {
        if (buildSceneNames != null) return;
        buildSceneNames = new HashSet<string>(System.StringComparer.Ordinal);
        int n = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < n; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path)) continue;
#if UNITY_EDITOR
            // 编辑器里 sceneCountInBuildSettings 把"未勾选"的条目也算进去，与真机不一致
            // （打包后只算勾选的）。不过滤的话，一扇指向未启用世界的门在编辑器里能解析、
            // 打包后却变成断头门——玩家推门进去只有黑屏。以真机行为为准。
            var settings = UnityEditor.EditorBuildSettings.scenes;
            if (i < settings.Length && !settings[i].enabled) continue;
#endif
            string s = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(s)) buildSceneNames.Add(s);
        }
    }

    /// <summary>
    /// 丢弃场景名缓存。Build Settings 变动后必须调用，否则推导还按旧表走
    /// （编辑器工具补完新世界要立刻重算，不能等到下次域重载）。
    /// </summary>
    public static void InvalidateSceneCache()
    {
        buildSceneNames = null;
    }

    /// <summary>
    /// 额外登记一批"已知世界"场景名，供推导时解析。
    /// 用途：编辑器工具在**只读检查**时不会真把场景写进 Build Settings，
    /// 但报告必须能说出"这扇门该连哪个新世界"，否则新拖进来的世界永远显示
    /// "退回实验室"。工具调用方用完要 <see cref="InvalidateSceneCache"/> 复位。
    /// </summary>
    public static void RegisterKnownScenes(System.Collections.Generic.IEnumerable<string> sceneNames)
    {
        if (sceneNames == null) return;
        if (buildSceneNames == null) buildSceneNames = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var s in sceneNames)
            if (!string.IsNullOrEmpty(s)) buildSceneNames.Add(s);
    }

    /// <summary>
    /// 把门名里的 key 解析成真实场景名。三级，从严到松：
    /// 1) 字面（`Metro` → `Metro`）；
    /// 2) 连字符换成下划线（`Flooded-Grounds` → `Flooded_Grounds`）；
    /// 3) **模糊匹配**：世界改过名而门名没跟着改时（`Shrine_game` → 场景已改叫 `Shrine`，
    ///    `OfficePack_game` → `OfficePack`），按"去掉 `_game`/`_scene`/`Scene_` 这类尾巴后
    ///    相同"或"一方是另一方的前缀"来认。
    ///
    /// 只认 Build Settings 里可加载的场景，免得把打错的名字当成合法世界去装载。
    /// 第 3 条刻意放在最后且要求**唯一命中**：宁可判失败让人去改名字，也不能在
    /// `FactoryDay` / `FactoryNight` 这种长得很像的世界之间猜错。
    /// </summary>
    private static string ResolveSceneName(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (IsBuildScene(key)) return key;
        string underscored = key.Replace('-', '_');
        if (underscored != key && IsBuildScene(underscored)) return underscored;

        // 3) 模糊匹配（世界改名后的旧门名）
        EnsureSceneCache();
        string norm = NormalizeSceneKey(underscored);
        string only = null;
        int hits = 0;
        foreach (var s in buildSceneNames)
        {
            if (string.Equals(NormalizeSceneKey(s), norm, System.StringComparison.OrdinalIgnoreCase))
            { only = s; hits++; }
        }
        if (hits == 1) return only;

        // 前缀匹配：`Shrine_game` → `Shrine`（归一化后互为前缀，且唯一）
        only = null;
        hits = 0;
        foreach (var s in buildSceneNames)
        {
            string ns = NormalizeSceneKey(s);
            if (ns.Length == 0) continue;
            if (norm.StartsWith(ns, System.StringComparison.OrdinalIgnoreCase)
                || ns.StartsWith(norm, System.StringComparison.OrdinalIgnoreCase))
            { only = s; hits++; }
        }
        return hits == 1 ? only : null;
    }

    /// <summary>
    /// 场景名归一化：去掉 `_game` / `_scene` / `Scene_` 这类"这只是一个场景"的尾巴，
    /// 再转小写。用于让"改过名的世界"和"没跟着改的门名"能对上。
    /// </summary>
    private static string NormalizeSceneKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        string s = name.Trim();
        string[] tails = { "_game", "-game", "_scene", "-scene", "_level", "-level" };
        foreach (var t in tails)
            if (s.EndsWith(t, System.StringComparison.OrdinalIgnoreCase))
            { s = s.Substring(0, s.Length - t.Length); break; }
        if (s.StartsWith("Scene_", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(6);
        if (s.StartsWith("scene_", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(6);
        return s.ToLowerInvariant();
    }

    /// <summary>
    /// 从本门名推导配对门名（与 <see cref="DerivePartnerScene"/> 同源，保证不会错位）。
    /// </summary>
    private string DerivePartnerDoor()
    {
        string scene, partner;
        return TryDerivePairing(gameObject.name, gameObject.scene.name, out scene, out partner) ? partner : null;
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
        // 顺序有讲究：先把相机与 RT 解绑，再还 RT。
        // Destroy 是延迟到帧末的，这行执行时 portalCam 仍然指向 rt；
        // 若先还池（rt.Release + 可能被别的门借走重开），那张 RT 会在本帧剩下的时间里
        // 既是 A 门的 targetTexture 又要被 B 门拿来渲染——部分图形 API 上直接花屏。
        if (portalCam != null)
        {
            portalCam.targetTexture = null;
            Destroy(portalCam.gameObject);
            portalCam = null;
        }
        if (rt != null) { PortalRtPool.Release(rt); rt = null; }   // 池化：不销毁，交还备用
        if (quadMat != null) { Destroy(quadMat); quadMat = null; }
        currentRtHeight = 0;
        destRenderers = null;
        destRendererWasEnabled = null;
        // 照片模式状态一并清掉：新世界重装后必须重新拍第一张
        photoMode = false;
        photoTakenAt = -999f;
        photoRenders = 0;
        liveRenders = 0;
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
