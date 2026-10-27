using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界流送：统一裁决"哪个世界现在装载、哪个世界该卸下、什么时候可以激活"。
///
/// 分工：门配对（WorldLink / autoPair）只描述"谁通往谁"，PortalWindow 只负责取景与穿行；
/// 场景的装卸集中在这里，规则四条——
///   1) 邻近预载：从玩家**当前世界**沿门图向外 PreloadHops 跳（默认 1 跳）的世界，
///      以及玩家进入某扇门 preloadRadius 内的世界，开始**预载**；
///   2) 远离卸载：已激活、没有需求、不受保护、且超出 2×preloadRadius + ReleaseMargin 的世界卸下；
///   3) 总量封顶：同时存活的世界不超过 MaxLoadedWorlds（含实验室）；满了先淘汰"需求最弱"；
///   4) 永不卸载：实验室、玩家当前世界、任何门还开着的世界。
///
/// **预载 ≠ 激活**（世界数量增长后这是最重要的一条）：
///   预载 = LoadSceneAsync(allowSceneActivation=false)，读盘 + 反序列化摊在多帧，progress 停在 0.9；
///   激活 = allowSceneActivation=true，实例化整个场景（GameObject/Awake/OnEnable/物理体/首帧着色器），
///   是**整帧级、无法拆帧**的重活。场景越大（Shrine 46k 物体 / 9.3k MeshCollider），激活越贵。
///
/// 所以激活**不再按距离发生**（ActivateOnApproach=false）：实验室出生点离 FactoryDay 门只有 1.6m，
/// 三扇门又都在 15m 内，按距离激活等于"开局把三个世界全部激活"，卡顿正好落在进游戏的第一秒。
/// 现在只有三种情况会激活：
///   · 玩家把门打开（pinned）；
///   · 玩家已经在该世界里（currentWorld，穿门登记）；
///   · 门主动请求（forced，按 E 开门时由 PortalWindow 提交）。
/// 开门时 AnywhereDoor 会等对面世界 + 取景就绪再转门板，前摇（spellWindup）正好用来遮激活；
/// 超过前摇的等待由 ThresholdLoadScreen 以「门阈校准」接管。
///
/// 预载窗口跟着当前世界滚动，所以世界数量增长时：内存只跟"当前世界的邻居"走，
/// 新增世界不用维护任何名单——门一摆好，图就自动长出来。
///
/// 激活串行：同一时刻只放行一个世界的激活（上一个就绪后才放行下一个），免得成本叠帧。
/// 演出/延迟卸载的调用方（PortalWindow）不感知这些策略。
/// </summary>
public static class WorldStreamer
{
    /// <summary>同时存活的世界上限，含实验室（5 = 实验室 + 最多 4 个异世界）。</summary>
    public static int MaxLoadedWorlds = 5;

    /// <summary>
    /// 预载窗口跳数：从玩家当前世界沿门图向外预载几跳。1 = 当前世界 + 直接相邻的世界。
    /// 0 = 只按门的 preloadRadius 预载（旧行为）。
    ///
    /// 为什么用"窗口"而不是"全部世界常驻"：预载的场景数据会一直占内存，世界越多线性越涨；
    /// 而玩家一次只可能在当前世界和它的邻居之间活动，1 跳足够覆盖下一扇门。
    /// 窗口跟着 currentWorld 滚动，新增世界不需要维护名单。
    /// </summary>
    public static int PreloadHops = 1;

    /// <summary>
    /// 是否按"走近门"激活世界。默认 false（2026-10-25 定稿）。
    ///
    /// 关掉的原因：实验室出生点离 FactoryDay 门只有 1.6m，Metro/OfficePack 门也都在 15m 内，
    /// 按距离激活 = 开局把三个世界全部激活，卡顿全砸在进游戏第一秒。
    /// 打开它只用于对比测试/特殊关卡；正常玩法请保持 false，让激活发生在"按 E 开门"的前摇里。
    /// </summary>
    public static bool ActivateOnApproach = false;

    /// <summary>
    /// 队列里最多留几个"已预载、待激活"的世界（默认 1）。
    ///
    /// 为什么要有上限：Unity 的 allowSceneActivation **只能按 op 创建顺序串行激活**。
    /// 若让三个邻居都停在"预载完成、待激活"，玩家按 E 的那一刻，挡在目标前面的世界
    /// 必须先逐个付掉——实测一次开门连撞三帧 250~330ms（让路两次 + 目标一次），
    /// 1.2s 的开门前摇根本盖不住，体感就是"一按 E 就卡一大下"。
    ///
    /// 压到 1 以后，任何一次开门最多只剩一个世界要激活，它的整帧成本正好藏进前摇里。
    /// 超出上限的那些世界会在**平静帧**里被悄悄预热激活（见 PrewarmCalmFrames），
    /// 玩家站着不动/走路时就把队列消化掉，而不是攒到开门那一刻。
    ///
    /// 设 0 = 预载完立刻全部激活（等价于 ActivateOnApproach，不推荐）；
    /// 设很大的值 = 回到"全都不激活"的旧行为（就是这次要修的卡顿）。
    /// </summary>
    public static int PrewarmAhead = 1;

    /// <summary>
    /// 预热激活前要求连续多少帧"平静"（帧时间 &lt; 50ms）。默认 30 帧（60fps 下约 0.5s）。
    ///
    /// 给一个缓冲期是因为"刚激活完上一个世界"的那几帧本来就长，
    /// 紧接着再放一个世界会让两次卡顿贴在一起。等它喘匀了再动，体感上就是"偶尔一下"，
    /// 而不是"连续抽搐"。
    /// </summary>
    public static int PrewarmCalmFrames = 30;

    /// <summary>
    /// 预载窗口的距离护栏（倍数 preloadRadius）。
    /// 门图邻居默认只按"跳数"选，但门可能隔着上千米：玩家站在 Metro 里，
    /// 通往 Shrine 的门在 1125m 外——把 96.5MB 的 Shrine 拉进需求毫无意义，
    /// 反而会在玩家刚穿门落地时触发它的激活（整场景实例化 = 一次长卡顿，
    /// 实测表现为"传送到 Metro 后有一段时间不能操作"）。
    /// 邻居世界只有在自己那扇门也落在保活范围内时才计入需求。
    /// </summary>
    public static float NeighborhoodRadiusFactor = 2f;

    /// <summary>
    /// 强制常驻名单（默认空，正常不要填）。
    ///
    /// 预载窗口（PreloadHops）会按门图自动覆盖"当前世界 + 邻居"，不需要手工维护世界列表。
    /// 只有一种情况才往这里填：某个世界没有门、却必须一直预载（例如纯展示用的背景世界）。
    /// 填进来的世界不看门图、永远预载且不卸载；但**仍然不会自动激活**
    /// （激活只认：门开着 / 人在其中 / 门主动请求）。
    /// </summary>
    public static readonly HashSet<string> ResidentWorlds = new HashSet<string>();

    /// <summary>远离卸载余量（米）：最近的门超过 2×preloadRadius + 本值才卸。</summary>
    public const float ReleaseMargin = 2f;

    // ── 卸载后的资源回收（内存恒定的关键）─────────────────────────────
    //
    // 卸载场景只是销毁 GameObject，**场景里引用到的纹理/网格/材质不会自动释放**：
    // 它们仍被"上一次加载"的引用链占着，直到有人调用 Resources.UnloadUnusedAssets。
    // 实测（2026-10-27）：卸掉 FactoryDay 之后 alloc 993→1015MB、tex 1149MB 纹丝不动，
    // 等于"世界卸了但内存一点没还"。而本工程此前**从未调用过** UnloadUnusedAssets
    // （全工程 grep 0 命中），所以 MaxLoadedWorlds 这个上限在内存意义上是不成立的——
    // 玩家跑遍全部世界后，所有世界的纹理都会累积在内存里。
    //
    // 触发时机：卸载完成后再等几帧。刚卸载时同一帧里可能还有别的组件持有引用
    // （例如正在收尾的协程、PortalWindow 的取景缓存），立刻扫会把它们判成"还在用"，
    // 白扫一次。等 2 帧让这些引用自然落地，回收率最高。
    //
    // 为什么不用 Resources.UnloadAsset 逐个点杀：纹理属于 GLB/场景导入产物，
    // 走引用计数，点杀会误伤仍被其它世界共用的贴图（同一张材质可能两个世界都在用）。
    // UnloadUnusedAssets 按引用可达性判定，是唯一安全的口径。
    private const int UnloadSweepDelayFrames = 2;
    private static int unloadSweepAtFrame = -1;

    /// <summary>卸载完成后排一次资源回收（见 UnloadSweepDelayFrames 注释）。</summary>
    private static void ScheduleUnloadSweep()
    {
        unloadSweepAtFrame = Time.frameCount + UnloadSweepDelayFrames;
    }

    /// <summary>
    /// 到点就扫一次未引用资源。异步扫（不阻塞主线程），完成时把结果写进日志，
    /// 方便对照"卸了世界之后内存到底还了多少"。
    /// </summary>
    private static void TickUnloadSweep()
    {
        if (unloadSweepAtFrame < 0 || Time.frameCount < unloadSweepAtFrame) return;
        unloadSweepAtFrame = -1;
        long before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        var op = Resources.UnloadUnusedAssets();
        if (op == null) return;
        op.completed += _ =>
        {
            long after = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Debug.Log("[WorldStreamer] 资源回收完成：总内存 " + (before / 1048576) + "MB → "
                      + (after / 1048576) + "MB（释放 " + ((before - after) / 1048576) + "MB）");
        };
    }

    /// <summary>
    /// 延迟激活系数（**仅在 ActivateOnApproach=true 时生效**）。
    /// 预载完成（progress ≥ 0.9）后，玩家走到 preloadRadius × 本值 以内才激活。
    ///
    /// 正常玩法不用它：激活已经改成"按 E 开门时发生"（见 ActivateOnApproach）。
    /// 保留本系数只用于对比测试/特殊关卡，并记录两条历史实测：
    ///
    /// 1) **激活成本躲不掉，只能挪**。激活（场景实例化 + 各物体 Awake/OnEnable + 首帧
    ///    渲染的着色器编译）是整帧级的重活；把激活点从 15m 挪到 8m，玩家走的路上照样卡那一下。
    ///
    /// 2) **停在 0.9 不激活会堵住后面所有世界**。Unity 的场景激活按 AsyncOperation 创建顺序
    ///    串行放行：第一个 op 停在 0.9（allowSceneActivation=false），后面的 op 即使预载完成
    ///    也激活不了。实测：开局同时预载 Metro、FactoryDay，FactoryDay 被 Metro 堵了 83 秒。
    ///    （预载串行闸 + 按需激活已经绕开这个坑：同一时刻只有一个 pending，且激活由开门触发。）
    /// </summary>
    public static float ActivationRadiusFactor = 1f;

    // ── 登记表与逐帧记账 ──
    private static readonly List<PortalWindow> windows = new List<PortalWindow>();
    private static readonly Dictionary<string, float> nearestDist = new Dictionary<string, float>();   // 世界 → 关联门里离玩家最近的距离
    private static readonly Dictionary<string, float> nearestRadius = new Dictionary<string, float>(); // 世界 → 那扇门的 preloadRadius
    private static readonly HashSet<string> wanted = new HashSet<string>();    // 本帧有装载需求的世界

    /// <summary>
    /// wanted 里"玩家真的走近了它的门"的那部分（按 preloadRadius 判定）。
    /// 与 wanted 的区别：wanted 还包含门图邻居、常驻世界、门请求——这些只是
    /// "迟早可能要"，不构成"现在就该付激活成本"的理由。见 TickPreloadAndActivation
    /// 的预热消化段。
    /// </summary>
    private static readonly HashSet<string> nearWanted = new HashSet<string>();
    private static readonly HashSet<string> pinned = new HashSet<string>();    // 门还开着，不许卸
    private static readonly HashSet<string> pending = new HashSet<string>();   // 预载进行中（progress < 0.9）
    private static readonly HashSet<string> preloaded = new HashSet<string>(); // 预载完成、等激活（progress ≥ 0.9，场景未激活）
    private static readonly HashSet<string> unloading = new HashSet<string>(); // 卸载进行中
    private static readonly HashSet<string> blocked = new HashSet<string>();   // 装载失败过（缺 Build Settings 等），不反复重试刷屏

    /// <summary>门图预载窗口：当前世界 + PreloadHops 跳内的世界。跟着 currentWorld 滚动。</summary>
    private static readonly HashSet<string> neighborhood = new HashSet<string>();
    private static readonly HashSet<string> neighborhoodNext = new HashSet<string>();   // 逐跳展开的临时缓冲
    private static bool graphDirty = true;            // 门登记表或当前世界变了 → 重新展开窗口
    private static string neighborhoodOrigin;         // 上次展开窗口时以哪个世界为起点

    /// <summary>
    /// 门主动请求激活的世界（按 E 开门时由 PortalWindow 提交）。
    /// 只进"需求 + 激活白名单"，真正的装载/激活仍由 Tick 串行执行；场景就绪后自动摘除。
    /// </summary>
    private static readonly HashSet<string> forced = new HashSet<string>();

    /// <summary>预载中/待激活世界的 AsyncOperation —— 激活时要拿它置 allowSceneActivation。</summary>
    private static readonly Dictionary<string, AsyncOperation> preloadOps = new Dictionary<string, AsyncOperation>();

    /// <summary>
    /// 预载**完成**、等激活的 AsyncOperation（激活时才置 allowSceneActivation）。
    ///
    /// 必须与 preloadOps 分开：完成后若还留在 preloadOps 里，"转待激活"那一步每帧都会重跑，
    /// 表现为同一世界每帧重复入列 preloaded + 每帧刷一条「预载完成，待激活」日志
    /// （实测每帧一条，等于给每个待激活世界挂了个逐帧 Debug.Log）。
    /// </summary>
    private static readonly Dictionary<string, AsyncOperation> preloadedOps = new Dictionary<string, AsyncOperation>();

    /// <summary>
    /// 预载串行闸：true 时同一时刻只允许**一个**世界处于 pending（progress &lt; 0.9）。
    ///
    /// 为什么需要：LoadSceneAsync 是异步的，只限制"同一帧发起一个"没有用——
    /// 隔一帧再发起第二个，两个世界照样在后台同时读盘 + 反序列化，主线程被两份
    /// 反序列化工作交替打断，表现为一整段持续掉帧，而不是一个可定位的尖峰。
    /// 实测日志：玩家走到实验室门区时 FactoryDay 与 Metro 同一帧进入预载，
    /// 紧接着 FactoryNight 与 Flooded_Grounds 也进来了——四个世界并发。
    ///
    /// 串行后：总耗时相近（都是读盘+反序列化），但任何时刻只有一份工作压在主线程上，
    /// 帧时间曲线从"一段持续低谷"变成"几个短尖峰 + 中间正常帧"。
    /// </summary>
    public static bool SerializePreload = true;

    /// <summary>已放行激活、等场景真正就绪的世界（同帧最多放行一个，见 TickPreloadAndActivation）。</summary>
    private static readonly HashSet<string> activating = new HashSet<string>();
    private static int activateGateFrames;   // activating 持续帧数：迟迟不就绪时的兜底放行

    /// <summary>
    /// op 创建顺序（即调用 LoadSceneAsync 的先后）。**只放还没激活过的**：激活一放行就从这里摘掉。
    ///
    /// 用途：Unity 的场景激活按 op 创建顺序**串行**放行——老 op 停在 0.9 不激活，后面的 op
    /// 就算预载完成也激活不了（实测堵了 83 秒）。装载前查一下"最老的待激活 op"，别把它跳过去。
    /// </summary>
    private static readonly List<string> outstanding = new List<string>();

    // ── 喘息闸：两次激活之间至少隔几帧，且等帧时间回落 ──
    private static int framesSinceActivate = 999;   // 距上次放行激活过了几帧
    private static int calmFrames;                   // 连续"帧时间正常"的帧数

    private static string homeWorld;     // 玩家根对象所在场景 = 实验室（起始世界），永不卸载
    private static string currentWorld;  // 玩家物理所在世界（穿门时登记），离开前不卸载
    private static bool ticking;

    public static string HomeWorld { get { return homeWorld; } }
    public static string CurrentWorld { get { return currentWorld; } }

    /// <summary>当前存活的世界数（含实验室，含装载中的）。</summary>
    public static int LoadedWorldCount { get { return CountLoaded(); } }

    /// <summary>预载窗口里已经激活完成的数量（开局部署提示 / OSD 轮询用）。</summary>
    public static int ResidentReadyCount
    {
        get
        {
            int n = 0;
            foreach (string w in neighborhood) if (IsLoadedNow(w)) n++;
            return n;
        }
    }

    /// <summary>预载窗口是否全部就绪（窗口为空也算就绪）。</summary>
    public static bool AllResidentsReady
    {
        get { return neighborhood.Count == 0 || ResidentReadyCount >= neighborhood.Count; }
    }

    /// <summary>当前预载窗口里的世界（只读查询，调试/OSD 用）。</summary>
    public static IEnumerable<string> PreloadWindow { get { return neighborhood; } }

    /// <summary>
    /// 预载窗口是否已经算出来了（开局第一帧 Tick 之前 homeWorld/neighborhood 都是空的）。
    /// 开局部署器靠它区分"还没展开窗口"和"窗口是空的、已经全部就绪"——
    /// 少了这个判据，第一帧就会误判成"0/0 完成"，玩家还没等加载就被放开。
    /// </summary>
    public static bool PreloadingReady { get { return homeWorld != null; } }

    /// <summary>
    /// 装载状态的一句话快照（诊断用：帧尖峰看门狗靠它标出"卡这一帧时在装什么"）。
    /// 全空时返回空串，不给正常帧增加字符串分配。
    /// </summary>
    public static string DebugActiveSet()
    {
        if (pending.Count == 0 && preloaded.Count == 0 && activating.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        if (activating.Count > 0) { sb.Append("激活:"); JoinSet(sb, activating); }
        if (preloaded.Count > 0) { if (sb.Length > 0) sb.Append("  "); sb.Append("待激活:"); JoinSet(sb, preloaded); }
        if (pending.Count > 0) { if (sb.Length > 0) sb.Append("  "); sb.Append("预载中:"); JoinSet(sb, pending); }
        return sb.ToString();
    }

    private static void JoinSet(System.Text.StringBuilder sb, HashSet<string> set)
    {
        bool first = true;
        foreach (string w in set) { if (!first) sb.Append(","); sb.Append(w); first = false; }
    }

    // ─────────────────────────────────────────────────────────────
    // 登记：PortalWindow 在 OnEnable / OnDisable 调用
    // ─────────────────────────────────────────────────────────────
    public static void Register(PortalWindow w)
    {
        if (w != null && !windows.Contains(w)) { windows.Add(w); graphDirty = true; }
    }

    public static void Unregister(PortalWindow w)
    {
        if (w != null && windows.Remove(w)) graphDirty = true;
    }

    /// <summary>
    /// 门请求"现在就要看到对面世界"（按 E 开门时调用）。
    /// 只把世界记进本帧需求 + 激活白名单；真正的装载/激活仍由 Tick 串行执行。
    /// 已激活的世界调用无副作用。
    /// </summary>
    public static void RequestActivation(string world)
    {
        if (!string.IsNullOrEmpty(world)) forced.Add(world);
    }

    /// <summary>穿门落点登记：玩家已在 arrivedWorld 里，离开前不能卸（不然玩家会掉进空场）。</summary>
    public static void NotifyCrossed(string arrivedWorld)
    {
        if (!string.IsNullOrEmpty(arrivedWorld)) currentWorld = arrivedWorld;
        graphDirty = true;                        // 预载窗口跟着当前世界滚动
        WorldPresence.SetCurrent(currentWorld);   // HUD 等按世界切换形态
    }

    /// <summary>瘫倒重启：玩家被传送回实验室时同步登记（returnedHome=false = 原地重启，勿动登记）。
    /// 不同步的后果：HUD 停在层域形态，且永不卸载护栏一直挂着一个不该保活的世界。</summary>
    private static void HandleCollapseBackHome(bool returnedHome)
    {
        if (!returnedHome || homeWorld == null) return;
        currentWorld = homeWorld;
        graphDirty = true;
        WorldPresence.SetCurrent(currentWorld);
    }

    /// <summary>目标世界是否可用（已装载且不在卸载中）——PortalWindow 据此决定"能不能建立取景"。</summary>
    public static bool IsWorldReady(string world)
    {
        if (string.IsNullOrEmpty(world)) return false;
        if (unloading.Contains(world)) return false;
        return IsLoadedNow(world);
    }

    // ─────────────────────────────────────────────────────────────
    // 每帧裁决：由任意一扇门的 Update 调用，做一次就够（见 ticking 重入闸）
    // ─────────────────────────────────────────────────────────────
    public static void Tick()
    {
        if (ticking || !PlayerRef.IsReady) return;
        ticking = true;
        try { TickInner(); }
        finally { ticking = false; }
    }

    private static void TickInner()
    {
        if (homeWorld == null)
        {
            var root = PlayerRef.Root;
            homeWorld = root != null ? root.gameObject.scene.name : SceneManager.GetActiveScene().name;
            if (currentWorld == null) currentWorld = homeWorld;   // 开局：玩家在起始世界
            // 瘫倒重启会把玩家传送回实验室（BatterySystem.restartAnchor）——登记见 HandleCollapseBackHome
            CollapsePenalty.OnCollapseRestart -= HandleCollapseBackHome;
            CollapsePenalty.OnCollapseRestart += HandleCollapseBackHome;
        }
        WorldPresence.SetHome(homeWorld);        // HUD 等按世界切换形态（含开局初始化）
        WorldPresence.SetCurrent(currentWorld);

        TickUnloadSweep();                        // 卸载后的资源回收（把世界真正还回内存）

        WorldLightIsolation.Sync(homeWorld);   // 世界装载后立即做光影隔离（实验室的明暗不随其它世界变化）
        EnsureNeighborhood();                  // 预载窗口跟着当前世界滚动（门图自动展开）
        Vector3 player = PlayerRef.Root.position;

        // ── 1) 逐门记账：每个世界取"关联门里离玩家最近的那扇" ──
        //（本项目每世界恰好一扇门；多门世界按最近的门算，语义也成立）
        nearestDist.Clear();
        nearestRadius.Clear();
        wanted.Clear();
        pinned.Clear();
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            var w = windows[i];
            if (w == null) { windows.RemoveAt(i); continue; }   // 场景卸载清出来的残留
            string name = w.TargetWorldName;
            Transform frame = w.SrcFrame;
            if (string.IsNullOrEmpty(name) || frame == null) continue;

            float dist = Vector3.Distance(player, frame.position);
            float known;
            if (!nearestDist.TryGetValue(name, out known) || dist < known)
            {
                nearestDist[name] = dist;
                nearestRadius[name] = w.PreloadRadius;
            }
            if (w.IsDoorOpen) pinned.Add(name);
        }

        // 需求判定：进过某扇门的 preloadRadius 就要装载（半径 ≤0 表示开局即要）
        nearWanted.Clear();
        foreach (var kv in nearestDist)
            if (nearestRadius[kv.Key] <= 0f || kv.Value <= nearestRadius[kv.Key])
            {
                wanted.Add(kv.Key);
                nearWanted.Add(kv.Key);   // 这是"真的走近了"，允许预热激活
            }

        // 预载窗口：当前世界 + 门图 1 跳内的世界。不看距离、不需要名单——
        // 新世界只要摆好门，走一次 Tick 就自动进窗口。
        // 记进 wanted 同时兜住后面两步：远离卸载会跳过它、封顶淘汰不选它。
        foreach (string n in neighborhood) wanted.Add(n);

        // 门主动请求过的世界（按 E 开门）一定在需求里：哪怕它不在窗口内（有人把门
        // 摆在了非相邻世界），开门这一刻也要立刻开始装载，否则门要等一个完整读盘。
        foreach (string f in forced) wanted.Add(f);

        // 特殊常驻名单（默认空）：只给"没有门、却必须一直预载"的背景世界用。
        foreach (string resident in ResidentWorlds) wanted.Add(resident);

        // 预载窗口：当前世界 + PreloadHops 跳内的邻居，同样记进 wanted。
        // 这是"世界变多之后还能无缝"的关键——玩家还在实验室时，邻居已经在读了，
        // 走到门前只剩激活；而窗口只覆盖邻居，不会把所有世界一次性拖进内存。
        // 窗口里的世界不参与"需求最弱"淘汰（进了 wanted 就不会被选为 victim）。
        foreach (string near in neighborhood)
        {
            // 距离护栏：邻居世界只有自己那扇门也落在保活范围内才算需求。
            // 否则 Metro 会把 1125m 外的 Shrine（96.5MB）拖进来并激活它。
            if (near != currentWorld
                && nearestDist.TryGetValue(near, out float nd)
                && nearestRadius.TryGetValue(near, out float nr)
                && nd > nr * NeighborhoodRadiusFactor + ReleaseMargin)
                continue;
            wanted.Add(near);
        }

        // 门主动请求的世界（按 E 开门时登记）：可能不在预载窗口里（比如隔着两跳），
        // 这里补进需求，装载循环才会真的去读它。激活由 TickPreloadAndActivation 的 forced 档负责。
        foreach (string ask in forced) wanted.Add(ask);

        // ── 2) 远离卸载：没有需求、超出保活半径、又不受保护的世界，卸掉 ──
        foreach (var kv in nearestDist)
        {
            string name = kv.Key;
            if (name == homeWorld || name == currentWorld) continue;   // 永不卸载（见类注释）
            if (pinned.Contains(name)) continue;                        // 门还开着
            // 本帧有装载需求的一律不卸。这条同时兜住 preloadRadius≤0 的"常驻世界"：
            // 它的保活半径算出来是 0+余量，玩家一走开就会被误卸，加上这条才是真常驻。
            if (wanted.Contains(name)) continue;
            if (pending.Contains(name) || unloading.Contains(name)) continue;
            if (!IsLoadedNow(name)) continue;
            if (kv.Value <= nearestRadius[name] * 2f + ReleaseMargin) continue;   // 还在保活范围内
            BeginUnload(name, "远离");
        }

        // ── 2.5) 预载推进与激活裁决（延迟激活，见 ActivationRadiusFactor） ──
        TickPreloadAndActivation();

        // ── 3) 需求装载：按需装；上限满了先淘汰"需求最弱"的世界 ──
        // 一帧一件事：同一帧最多发起一个新世界的装载，且世界里还有激活在途就先不发起新装载——
        // 装载与激活的重活错开，开局"N 个世界连发"的复合卡顿就不会出现（一个一个来）。
        bool startedThisTick = false;
        foreach (string name in LoadOrder())
        {
            // 注意必须排除 preloaded：预载完成的场景 isLoaded 仍为 false，
            // 漏了这条会在下一帧再次 BeginLoad，把同一世界装载两遍。
            if (IsLoadedNow(name) || pending.Contains(name) || preloaded.Contains(name)
                || unloading.Contains(name) || blocked.Contains(name)) continue;
            if (startedThisTick || activating.Count > 0) break;   // 一个一个来（见上）
            // 预载串行闸：已有世界在预载中就先不发新的（见 SerializePreload 注释）。
            // 这一条才是真正的串行——上面 startedThisTick 只管"同一帧"，异步装载照样并发。
            if (SerializePreload && pending.Count > 0) break;
            if (CountLoaded() < MaxLoadedWorlds) { BeginLoad(name); startedThisTick = true; continue; }
            string victim = PickWeakestVictim();
            if (victim == null) continue;   // 全都受保护/正被需要：暂缓，下一帧再试
            BeginUnload(victim, "封顶淘汰");
            BeginLoad(name);
            startedThisTick = true;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 装载排队顺序
    //
    // wanted 是 HashSet，遍历顺序不确定；串行装载后"先装谁"变成了实质问题——
    // 先装远处世界会让站在近处门前的玩家干等。按「离玩家的最近门距离」升序排：
    // 越近越先装，玩家真的走向某扇门时，那个世界已经排在最前面。
    //
    // 常驻世界（ResidentWorlds）不参与距离排序的优先权：它们没有"必须马上要"的语义，
    // 统一排在按需世界之后（距离视为 +∞），让玩家的实际去向优先。
    // ─────────────────────────────────────────────────────────────
    private static readonly List<string> loadOrderBuf = new List<string>();

    private static List<string> LoadOrder()
    {
        loadOrderBuf.Clear();
        foreach (string name in wanted)
        {
            // 常驻世界只在"没有按需世界"时才轮到；这里先用距离排序，
            // 但常驻且当前无距离记录的排到最后（nearestDist 里没有它 → 用 float.MaxValue）。
            loadOrderBuf.Add(name);
        }
        if (loadOrderBuf.Count <= 1) return loadOrderBuf;

        // 排序键：有距离的按距离升序；没距离的（纯常驻、无关联门）排最后。
        loadOrderBuf.Sort((a, b) =>
        {
            float da = nearestDist.TryGetValue(a, out float va) ? va : float.MaxValue;
            float db = nearestDist.TryGetValue(b, out float vb) ? vb : float.MaxValue;
            return da.CompareTo(db);
        });
        return loadOrderBuf;
    }

    // ─────────────────────────────────────────────────────────────
    // 预载窗口：从玩家当前世界沿门图向外展开 PreloadHops 跳
    //
    // 门图直接取自登记表 windows —— 每扇门贡献一条 `本门所在场景 → 目标世界` 的边。
    // PortalWindow.TargetWorldName 已经是名字推导的结果（含 -A 兜底与模糊匹配），
    // 所以这里不需要再理解任何命名约定，只当普通有向图用。
    //
    // 只在 graphDirty（门登记表变了 / 玩家换世界了）时重算：
    // 每帧重算是 O(门数 × 跳数)，世界变多之后没必要。
    // ─────────────────────────────────────────────────────────────
    private static void EnsureNeighborhood()
    {
        if (!graphDirty && neighborhoodOrigin == currentWorld) return;

        graphDirty = false;
        neighborhoodOrigin = currentWorld;
        neighborhood.Clear();

        if (string.IsNullOrEmpty(currentWorld)) return;

        neighborhood.Add(currentWorld);          // 第 0 跳：玩家脚下的世界
        if (PreloadHops <= 0) return;

        var frontier = neighborhoodNext;         // 上一跳新展开出来的世界
        frontier.Clear();
        frontier.Add(currentWorld);

        for (int hop = 0; hop < PreloadHops; hop++)
        {
            List<string> added = null;
            foreach (var w in windows)
            {
                if (w == null) continue;
                Transform frame = w.SrcFrame;
                string from = frame != null ? frame.gameObject.scene.name : null;
                string to = w.TargetWorldName;
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;
                if (!frontier.Contains(from)) continue;      // 只从上一跳的世界往外走
                if (!neighborhood.Add(to)) continue;         // 已经在窗口里
                if (added == null) added = new List<string>();
                added.Add(to);
            }
            if (added == null) break;                        // 没有新世界 → 图到头了

            frontier.Clear();
            for (int i = 0; i < added.Count; i++) frontier.Add(added[i]);
        }

        frontier.Clear();
    }

    //
    // 三件事：
    //   1) 轮询 progress，到 0.9 就从 pending 转入 preloaded（此时场景**未**激活）；
    //   2) 玩家进到 preloadRadius × ActivationRadiusFactor、或门开着、或人就在该世界 → 激活；
    //   3) 预载态但玩家已远离且不再需要 → 也激活它，交给下一帧的常规卸载释放内存。
    //      （AsyncOperation 一旦开始就**无法取消**，只能先激活再卸；此刻玩家在远处，卡顿不明显）
    // ─────────────────────────────────────────────────────────────
    private static void TickPreloadAndActivation()
    {
        // 0a) 请求兑现：已经就绪的世界从 forced 里摘掉。
        //     forced 的语义是"这扇门要我现在就看到对面"，世界一旦可看，请求就结束了；
        //     留着它会让该世界永远进 wanted、永远 on 必激活档（卸不掉、也挡着别的世界激活）。
        //     这里按"已就绪"判而不是跟着 preloaded 的清理走：激活还有别的路径能把它移出
        //     preloaded（Unity 自己 settled/卸载），只挂在一条路径上会漏。
        if (forced.Count > 0)
        {
            List<string> satisfied = null;
            foreach (string w in forced)
                if (IsLoadedNow(w))
                {
                    if (satisfied == null) satisfied = new List<string>();
                    satisfied.Add(w);
                }
            if (satisfied != null)
                for (int i = 0; i < satisfied.Count; i++) forced.Remove(satisfied[i]);
        }

        // 0) 已激活且真正就绪的，从"待激活"集里摘掉。
        //    激活（allowSceneActivation=true）到 scene.isLoaded 为真要一帧左右，
        //    这期间必须继续留在 preloaded 里当"保护标记"，否则装载循环会重复 BeginLoad。
        if (preloaded.Count > 0)
        {
            List<string> settled = null;
            foreach (string w in preloaded)
                if (IsLoadedNow(w))
                {
                    if (settled == null) settled = new List<string>();
                    settled.Add(w);
                }
            if (settled != null)
                for (int i = 0; i < settled.Count; i++)
                {
                    preloaded.Remove(settled[i]);
                    activating.Remove(settled[i]);   // 就绪 → 放行下一个世界的激活
                    outstanding.Remove(settled[i]);
                }
        }

        // 串行闸：上一个世界还没真正就绪就不再放行新激活（正常情况下只挡 1~2 帧）。
        // 兜底：极久不就绪（异常）时放行，免得一个卡住的世界把后续装载全堵死。
        if (activating.Count > 0)
        {
            if (++activateGateFrames > 600)
            {
                Debug.LogWarning("[WorldStreamer] 已有世界激活超时未就绪，放行后续装载：" + string.Join("、", activating));
                activating.Clear();
                activateGateFrames = 0;
            }
        }
        else activateGateFrames = 0;

        // 1) 预载进度 → 待激活
        if (preloadOps.Count > 0)
        {
            List<string> done = null;
            foreach (var kv in preloadOps)
            {
                var op = kv.Value;
                if (op == null || op.progress >= 0.9f)
                {
                    if (done == null) done = new List<string>();
                    done.Add(kv.Key);
                }
            }
            if (done != null)
            {
                for (int i = 0; i < done.Count; i++)
                {
                    string w = done[i];
                    pending.Remove(w);
                    AsyncOperation op;
                    bool hasOp = preloadOps.TryGetValue(w, out op) && op != null;
                    preloadOps.Remove(w);            // 关键：摘出"预载中"，否则本段每帧重跑（见 preloadedOps 注释）
                    if (hasOp)
                    {
                        preloadedOps[w] = op;        // 激活时还它置 allowSceneActivation
                        preloaded.Add(w);
                        Debug.Log("[WorldStreamer] 预载完成，待激活：" + w);
                    }
                    else { blocked.Add(w); outstanding.Remove(w); }
                }
            }
        }

        if (preloaded.Count == 0) return;

        // 2) 激活裁决
        float factor = Mathf.Clamp(ActivationRadiusFactor, 0.05f, 1f);
        List<string> toActivate = null;
        List<string> toAbandon = null;

        // 预热候选池：已预载、仍被需要、但不是"必须现在显示"的世界。
        // 它们若一直停在"待激活"，就会在玩家按 E 时按 Unity 的 op 创建顺序
        // 挡在目标前面被迫逐个付掉（见 PrewarmAhead 注释）。
        List<string> prewarmPool = null;

        foreach (string w in preloaded)
        {
            // 三档必激活：门开着（背后世界要保住）、玩家就在里面、门主动求过。
            bool mustShow = pinned.Contains(w) || w == currentWorld || forced.Contains(w);
            float d = 0f, r = 0f;
            bool known = nearestDist.TryGetValue(w, out d) && nearestRadius.TryGetValue(w, out r);

            if (mustShow) { if (toActivate == null) toActivate = new List<string>(); toActivate.Add(w); continue; }
            if (!known) continue;

            // ActivateOnApproach=false 时**不按距离激活**：玩家走到门前才激活，
            // 那一下的整帧重活正好落在开门挥手/穿行的那一秒，体感就是"卡顿"。
            // 关掉它以后激活只由门请求（RequestActivation，见 AnywhereDoor 的门阈校准），
            // 成本被挪进开门前摇，而玩家看不见前摇里的帧。
            // 「已远离且不需要」那条仍要保留：否则预载数据永远不释放。
            if (!ActivateOnApproach)
            {
                if (!wanted.Contains(w) && known && d > r * 2f + ReleaseMargin)
                {
                    if (toAbandon == null) toAbandon = new List<string>();
                    toAbandon.Add(w);
                }
                else if (wanted.Contains(w))
                {
                    // 只有"玩家真的走近了它的门"或"显式常驻"才进预热队列。
                    //
                    // 纯门图邻居只预载、不预热激活：预载是异步的、摊在很多帧里几乎无感；
                    // 激活是整场景实例化，是一帧里的重活。玩家刚穿门落地那一刻去激活一个
                    // 几十 MB 的邻居（只是门图相邻，人根本没往那扇门走），就是"传送到
                    // Metro 后有一段时间不能操作"的直接原因。
                    if (nearWanted.Contains(w) || ResidentWorlds.Contains(w))
                    {
                        if (prewarmPool == null) prewarmPool = new List<string>();
                        prewarmPool.Add(w);
                    }
                }
                continue;
            }

            if (r <= 0f || d <= r * factor)   // 够近 → 激活
            {
                if (toActivate == null) toActivate = new List<string>();
                toActivate.Add(w);
            }
            else if (!wanted.Contains(w) && d > r * 2f + ReleaseMargin)   // 已远离且不需要 → 激活后交卸载
            {
                if (toAbandon == null) toAbandon = new List<string>();
                toAbandon.Add(w);
            }
        }

        // 2.5) 预热：把超出 PrewarmAhead 的待激活世界在平静帧里提前消化掉。
        //
        // 只在"帧时间已经平稳"时动手（calmFrames >= PrewarmCalmFrames），
        // 这样预热产生的整帧成本不会和"刚激活完上一个世界"的余波叠在一起。
        // 挑最近的那个先激活：玩家下一步最可能走向它，先付掉最划算。
        if (prewarmPool != null && prewarmPool.Count > Mathf.Max(0, PrewarmAhead)
            && calmFrames >= Mathf.Max(0, PrewarmCalmFrames))
        {
            prewarmPool.Sort((a, b) =>
            {
                float da = nearestDist.ContainsKey(a) ? nearestDist[a] : float.MaxValue;
                float db = nearestDist.ContainsKey(b) ? nearestDist[b] : float.MaxValue;
                return da.CompareTo(db);
            });
            int budget = prewarmPool.Count - Mathf.Max(0, PrewarmAhead);
            for (int i = 0; i < budget; i++)
            {
                if (toActivate == null) toActivate = new List<string>();
                toActivate.Add(prewarmPool[i]);
            }
            if (budget > 0)
                Debug.Log("[WorldStreamer] 预热消化待激活队列：" + budget + " 个"
                          + "（队列 " + prewarmPool.Count + " → " + Mathf.Max(0, PrewarmAhead) + "，"
                          + "按 E 开门时就只剩一个世界要激活）");
        }

        // 一次只放行一个世界：激活 = 整帧级的重活（场景实例化 + 各物体 Awake/OnEnable），
        // 同帧放行两个世界会把两次卡顿叠成一帧超长停顿（实测 Metro + FactoryDay 同帧 ≈ 900ms+）。
        // 串行放行后是两帧各 ~450ms，帧间还有余量恢复，体感明显更轻。
        if (activating.Count > 0) return;   // 上一个还没就绪：这帧不放行

        // 喘息闸：上次激活后至少隔 1 帧，并等帧时间回落到正常（连续 2 帧 < 50ms）再放行下一个。
        // 实测：Unity 会把"刚请求激活"的 op 在同一次加载更新里一起实例化，两个世界挤在一起时
        // 两段卡顿并成一帧 1.5s；错开后是两帧各 ~0.7s，中间还有正常帧喘气。
        // 帧时间本来就差（比如只有 20fps）时用 120 帧上限强制放行，绝不死等。
        framesSinceActivate++;
        if (Time.unscaledDeltaTime < 0.05f) calmFrames++; else calmFrames = 0;
        if (framesSinceActivate < 2) return;
        if (calmFrames < 2 && framesSinceActivate < 120) return;

        string target = null;
        if (toActivate != null && toActivate.Count > 0) target = toActivate[0];
        else if (toAbandon != null && toAbandon.Count > 0) target = toAbandon[0];
        if (target == null) return;

        // 让路：Unity 按 op 创建顺序串行激活，更早创建、已经预载完成的 op 若不激活，
        // 排在它后面的目标永远激活不了（实测被堵 83 秒，门洞一直空着）。先放它上路。
        string oldest = OldestReadyToActivate();
        if (oldest != null && oldest != target)
        {
            Debug.Log("[WorldStreamer] 让路：先激活更早创建的世界 " + oldest
                      + "（Unity 激活按创建顺序串行，挡路的先走）");
            Activate(oldest);
            return;
        }

        if (toActivate == null || toActivate.Count == 0)
            Debug.Log("[WorldStreamer] 预载后已远离，激活以便释放：" + target);
        Activate(target);
    }

    /// <summary>最早创建、且已预载完成（能激活）的 op —— 它就是堵在队列最前面的那个。</summary>
    private static string OldestReadyToActivate()
    {
        for (int i = 0; i < outstanding.Count; i++)
            if (preloadedOps.ContainsKey(outstanding[i])) return outstanding[i];
        return null;
    }

    private static void Activate(string world)
    {
        AsyncOperation op;
        if (preloadedOps.TryGetValue(world, out op) && op != null)
        {
            preloadedOps.Remove(world);
            activating.Add(world);
            outstanding.Remove(world);   // 已放行：不再挡后面 op 的激活
            framesSinceActivate = 0;     // 重置喘息闸
            calmFrames = 0;
            op.allowSceneActivation = true;
            Debug.Log("[WorldStreamer] 激活世界 " + world);
        }
        else { preloadedOps.Remove(world); outstanding.Remove(world); }
        // **故意不从 preloaded 移除**：激活要一帧左右才真正 isLoaded，
        // 这期间若把它漏出保护集，装载循环会判定"还没加载"并再 BeginLoad 一次 → 重复加载。
        // 由 TickPreloadAndActivation 第 0 步在 IsLoadedNow 为真时清掉。
    }

    // ─────────────────────────────────────────────────────────────
    // 装载 / 卸载
    // ─────────────────────────────────────────────────────────────
    private static void BeginLoad(string world)
    {
        pending.Add(world);
        outstanding.Add(world);      // 记创建顺序：Unity 按它串行激活，放行前要查谁挡路
        LoadDiag.Begin();
        var op = SceneManager.LoadSceneAsync(world, LoadSceneMode.Additive);
        LoadDiag.End("发起装载 " + world + "（LoadSceneAsync 同步段）");
        if (op == null)
        {
            pending.Remove(world);
            outstanding.Remove(world);
            blocked.Add(world);
            Debug.LogError("[WorldStreamer] 世界装载失败：" + world + "（场景是否已加入 Build Settings？）");
            return;
        }

        // 先只预载、不激活：progress 停在 0.9，等预载一完成再置 allowSceneActivation = true
        //（见 TickPreloadAndActivation / ActivationRadiusFactor）——
        // 这样"读盘 + 反序列化"被摊在玩家走近的路上，激活那一下的卡顿与读盘分开。
        //
        // 注意：allowSceneActivation = false 时 op.completed **永远不触发**，
        // 所以不能靠它清 pending，必须每帧轮询 progress。
        op.allowSceneActivation = false;
        preloadOps[world] = op;
        Debug.Log("[WorldStreamer] 开始预载世界 " + world + "（玩家进入其门的预载半径）");
    }

    private static void BeginUnload(string world, string why)
    {
        unloading.Add(world);
        LoadDiag.Begin();
        var op = SceneManager.UnloadSceneAsync(world);
        LoadDiag.End("发起卸载 " + world + "（UnloadSceneAsync 同步段）");
        if (op == null)
        {
            unloading.Remove(world);
            Debug.LogError("[WorldStreamer] 世界卸载失败：" + world);
            return;
        }
        Debug.Log("[WorldStreamer] 卸下世界 " + world + "（" + why + "）");
        op.completed += _ =>
        {
            unloading.Remove(world);
            // 防御性清理：正常情况下预载记录在激活时已清掉
            preloadOps.Remove(world);
            preloadedOps.Remove(world);
            preloaded.Remove(world);
            activating.Remove(world);
            outstanding.Remove(world);
            pending.Remove(world);
            TeardownWindows(world);
            ScheduleUnloadSweep();   // 场景销毁 ≠ 资源释放，见 ScheduleUnloadSweep 注释
        };
    }

    /// <summary>
    /// 世界卸下后的收尾：让还留在其它世界里的门把取景资源全部放掉
    /// （见 PortalWindow.OnTargetWorldUnloaded——不清的话会引用到已销毁的目标门）。
    /// 世界自己内部的门已随场景销毁，登记表在卸载过程中就注销掉了，这里自然只剩幸存者。
    /// </summary>
    private static void TeardownWindows(string world)
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            var w = windows[i];
            if (w == null) { windows.RemoveAt(i); continue; }
            if (w.TargetWorldName == world) w.OnTargetWorldUnloaded();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 记账辅助
    // ─────────────────────────────────────────────────────────────
    private static int CountLoaded()
    {
        int n = IsLoadedNow(homeWorld) ? 1 : 0;
        foreach (var kv in nearestDist)
            if (kv.Key != homeWorld &&
                (IsLoadedNow(kv.Key) || pending.Contains(kv.Key) || preloaded.Contains(kv.Key))) n++;
        return n;
    }

    /// <summary>
    /// 封顶淘汰对象：没有需求、不受保护、离玩家相对最远（需求最弱）的世界。
    /// 一个都没有就返回 null = 暂缓装载（比如三扇相邻门同时都要装载，只能等玩家走开一扇）。
    /// </summary>
    private static string PickWeakestVictim()
    {
        string best = null;
        float bestWeakness = float.NegativeInfinity;
        foreach (var kv in nearestDist)
        {
            string name = kv.Key;
            if (name == homeWorld || name == currentWorld) continue;
            if (wanted.Contains(name) || pinned.Contains(name)) continue;
            if (pending.Contains(name) || unloading.Contains(name)) continue;
            // 预载态（场景未激活）不能直接卸——AsyncOperation 无法取消。
            // 它由 TickPreloadAndActivation 的"远离则激活"分支转成已加载，再由常规路径释放。
            if (preloaded.Contains(name)) continue;
            if (!IsLoadedNow(name)) continue;
            float reference = Mathf.Max(0.01f, nearestRadius[name] * 2f);
            float weakness = kv.Value / reference;   // 越大越弱：离玩家相对越远
            if (weakness > bestWeakness) { bestWeakness = weakness; best = name; }
        }
        return best;
    }

    private static bool IsLoadedNow(string world)
    {
        if (string.IsNullOrEmpty(world)) return false;
        return SceneManager.GetSceneByName(world).isLoaded;
    }

    /// <summary>调试/验收用的一行状态。</summary>
    public static string StatusText()
    {
        var sb = new StringBuilder();
        sb.Append("起始=").Append(homeWorld).Append(" 当前=").Append(currentWorld)
          .Append(" 上限=").Append(MaxLoadedWorlds).Append(" 存活=");
        int n = 0;
        if (IsLoadedNow(homeWorld)) { sb.Append(homeWorld); n++; }
        foreach (var kv in nearestDist)
        {
            if (kv.Key == homeWorld || !IsLoadedNow(kv.Key)) continue;
            if (n > 0) sb.Append(",");
            sb.Append(kv.Key);
            n++;
        }
        sb.Append(" (共").Append(n).Append("个)");
        if (pending.Count > 0 || preloaded.Count > 0)
            sb.Append("  预载中=").Append(pending.Count).Append(" 待激活=").Append(preloaded.Count);
        return sb.ToString();
    }
}
