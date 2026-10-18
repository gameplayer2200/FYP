using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界流送：统一裁决"哪个世界现在装载、哪个世界该卸下"。
///
/// 分工：门配对（WorldLink）只描述"谁通往谁"，PortalWindow 只负责取景与穿行；
/// 场景的装卸集中在这里，规则四条——
///   1) 需求装载：玩家进入某扇门的 preloadRadius 内 → 该门的目标世界开始**预载**；
///   2) 远离卸载：某世界关联的所有门都超出 2×preloadRadius + ReleaseMargin 米 → 卸下
///      （保活半径比取景渲染距离 2×preloadRadius 大，不会出现"还在取景却被卸掉"）；
///   3) 总量封顶：同时存活的世界不超过 MaxLoadedWorlds（含实验室）；满了先淘汰
///      "需求最弱"（离玩家最远）的可卸世界，腾不出空位就暂缓本次装载；
///   4) 常驻世界（ResidentWorlds）：不看门与距离，开局即预载、永不卸载；激活仍走常规裁决
///      （走近 / 门开 / 人在其中）。开局全激活会触发传送 bug（2026-10-18 实测），不要开绿灯。
///
/// 装载分两步（预载 → 激活）：按需世界两步都发生在玩家走近的路上；常驻世界开局只走"预载"一步。
///   预载（读盘 + 反序列化，progress 0→0.9，allowSceneActivation=false）在 preloadRadius 处开始，
///   预载一完成立即**激活**（见 ActivationRadiusFactor 里"为什么要立即激活"的两条实测理由）。
///   于是重活（读盘摊在多帧、激活/首帧渲染各卡一下）全落在玩家还在半路上时，
///   走到门前时世界已经就绪、门洞画面也建好了。
///
/// 激活串行：同一时刻只放行一个世界的激活（上一个就绪后才放行下一个），
/// 免得两个世界的实例化成本叠进同一帧。
///
/// 永不卸载：实验室（玩家根对象所在场景）、玩家当前所在世界（穿门时登记——
/// 异世界里实验室侧的门都远在天边，没有这条护栏会卸掉玩家脚下的世界）、
/// 任何"门还开着"的世界（免得开着的门洞后面突然没了世界）、
/// 以及常驻名单（ResidentWorlds）里的全部世界。
///
/// 演化背景见 docs/adr/0003-worlds-stay-resident.md：世界数量到 4~5 个后，
/// 加载职责要收敛到一个可替换的抽象，就是本类；将来换流式分块/预算调度，
/// 只改本类，"常驻"与"延迟卸载"的调用方（PortalWindow）不动。
/// </summary>
public static class WorldStreamer
{
    /// <summary>同时存活的世界上限，含实验室（5 = 实验室 + 最多 4 个异世界）。</summary>
    public static int MaxLoadedWorlds = 5;

    /// <summary>
    /// 常驻世界名单：不看门、不看距离，开局即**预载**（读盘/反序列化摊在多帧）、永不卸载
    /// （2026-10-18 定稿：全部异世界开局加载，不再"走到门前才装"）。TickInner 把名单并进 wanted——
    /// 这同时兜住远离卸载与封顶淘汰。
    /// **激活不给常驻世界开绿灯**：仍走常规裁决（玩家走近 / 门开着 / 人已在该世界）。
    /// 开局就全部激活会触发传送 bug（2026-10-18 实测）；预载已把读盘成本摊掉，激活那一下留到走近再发生。
    /// 新增异世界时把场景名加进来（实验室自身不用写）。
    /// </summary>
    public static readonly HashSet<string> ResidentWorlds = new HashSet<string>
    {
        "Metro",
        "FactoryDay",
        "FactoryNight",
        "Flooded_Grounds",
    };

    /// <summary>远离卸载余量（米）：最近的门超过 2×preloadRadius + 本值才卸。</summary>
    public const float ReleaseMargin = 2f;

    /// <summary>
    /// 延迟激活系数：预载完成（progress ≥ 0.9）后，玩家走到 preloadRadius × 本值 以内才激活。
    ///
    /// **默认 1 = 预载一完成就激活**，这是实测后的定论，原因有两条：
    ///
    /// 1) **激活成本躲不掉，只能挪**。激活（场景实例化 + 各物体 Awake/OnEnable + 首帧
    ///    渲染的着色器编译）是整帧级的重活；把激活点从 15m 挪到 8m，玩家走的路上照样卡那一下。
    ///    挪早反而更好：卡顿时玩家离门还远、视线多半不在门上，且门一定已经就绪。
    ///
    /// 2) **停在 0.9 不激活会堵住后面所有世界**。Unity 的场景激活按 AsyncOperation 创建顺序
    ///    串行放行：第一个 op 停在 0.9（allowSceneActivation=false），后面的 op 即使预载完成
    ///    也激活不了。实测：开局同时预载 Metro、FactoryDay，FactoryDay 被 Metro 堵了 83 秒，
    ///    直到玩家走近 Metro 门把 Metro 激活掉才跟着加载出来 —— 表现就是"走到门前什么都没有"。
    ///
    /// 保留本系数是为了留出调参余地（<1 = 老行为，只在玩家走近时才激活）。
    /// </summary>
    public static float ActivationRadiusFactor = 1f;

    // ── 登记表与逐帧记账 ──
    private static readonly List<PortalWindow> windows = new List<PortalWindow>();
    private static readonly Dictionary<string, float> nearestDist = new Dictionary<string, float>();   // 世界 → 关联门里离玩家最近的距离
    private static readonly Dictionary<string, float> nearestRadius = new Dictionary<string, float>(); // 世界 → 那扇门的 preloadRadius
    private static readonly HashSet<string> wanted = new HashSet<string>();    // 本帧有装载需求的世界
    private static readonly HashSet<string> pinned = new HashSet<string>();    // 门还开着，不许卸
    private static readonly HashSet<string> pending = new HashSet<string>();   // 预载进行中（progress < 0.9）
    private static readonly HashSet<string> preloaded = new HashSet<string>(); // 预载完成、等激活（progress ≥ 0.9，场景未激活）
    private static readonly HashSet<string> unloading = new HashSet<string>(); // 卸载进行中
    private static readonly HashSet<string> blocked = new HashSet<string>();   // 装载失败过（缺 Build Settings 等），不反复重试刷屏

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

    /// <summary>常驻世界已经就绪（激活完成）的数量——开局部署提示用（幕布/OSD 轮询）。</summary>
    public static int ResidentReadyCount
    {
        get
        {
            int n = 0;
            foreach (string w in ResidentWorlds) if (IsLoadedNow(w)) n++;
            return n;
        }
    }

    /// <summary>常驻世界是否全部就绪（全部激活完成）。</summary>
    public static bool AllResidentsReady { get { return ResidentReadyCount >= ResidentWorlds.Count; } }

    // ─────────────────────────────────────────────────────────────
    // 登记：PortalWindow 在 OnEnable / OnDisable 调用
    // ─────────────────────────────────────────────────────────────
    public static void Register(PortalWindow w) { if (w != null && !windows.Contains(w)) windows.Add(w); }
    public static void Unregister(PortalWindow w) { if (w != null) windows.Remove(w); }

    /// <summary>穿门落点登记：玩家已在 arrivedWorld 里，离开前不能卸（不然玩家会掉进空场）。</summary>
    public static void NotifyCrossed(string arrivedWorld)
    {
        if (!string.IsNullOrEmpty(arrivedWorld)) currentWorld = arrivedWorld;
        WorldPresence.SetCurrent(currentWorld);   // HUD 等按世界切换形态
    }

    /// <summary>瘫倒重启：玩家被传送回实验室时同步登记（returnedHome=false = 原地重启，勿动登记）。
    /// 不同步的后果：HUD 停在层域形态，且永不卸载护栏一直挂着一个不该保活的世界。</summary>
    private static void HandleCollapseBackHome(bool returnedHome)
    {
        if (!returnedHome || homeWorld == null) return;
        currentWorld = homeWorld;
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

        WorldLightIsolation.Sync(homeWorld);   // 世界装载后立即做光影隔离（实验室的明暗不随其它世界变化）
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
        foreach (var kv in nearestDist)
            if (nearestRadius[kv.Key] <= 0f || kv.Value <= nearestRadius[kv.Key]) wanted.Add(kv.Key);

        // 常驻世界：不看门、不看距离，永远记需求——开局全量装载就落在这里。
        // 记进 wanted 同时兜住后面两步：远离卸载会跳过它、封顶淘汰不选它。
        foreach (string resident in ResidentWorlds) wanted.Add(resident);

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
        foreach (string name in wanted)
        {
            // 注意必须排除 preloaded：预载完成的场景 isLoaded 仍为 false，
            // 漏了这条会在下一帧再次 BeginLoad，把同一世界装载两遍。
            if (IsLoadedNow(name) || pending.Contains(name) || preloaded.Contains(name)
                || unloading.Contains(name) || blocked.Contains(name)) continue;
            if (startedThisTick || activating.Count > 0) break;   // 一个一个来（见上）
            if (CountLoaded() < MaxLoadedWorlds) { BeginLoad(name); startedThisTick = true; continue; }
            string victim = PickWeakestVictim();
            if (victim == null) continue;   // 全都受保护/正被需要：暂缓，下一帧再试
            BeginUnload(victim, "封顶淘汰");
            BeginLoad(name);
            startedThisTick = true;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 延迟激活：预载完成 → 等玩家走近 → 激活
    //
    // 三件事：
    //   1) 轮询 progress，到 0.9 就从 pending 转入 preloaded（此时场景**未**激活）；
    //   2) 玩家进到 preloadRadius × ActivationRadiusFactor、或门开着、或人就在该世界 → 激活；
    //   3) 预载态但玩家已远离且不再需要 → 也激活它，交给下一帧的常规卸载释放内存。
    //      （AsyncOperation 一旦开始就**无法取消**，只能先激活再卸；此刻玩家在远处，卡顿不明显）
    // ─────────────────────────────────────────────────────────────
    private static void TickPreloadAndActivation()
    {
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
        foreach (string w in preloaded)
        {
            bool mustShow = pinned.Contains(w) || w == currentWorld;
            float d = 0f, r = 0f;
            bool known = nearestDist.TryGetValue(w, out d) && nearestRadius.TryGetValue(w, out r);

            if (mustShow) { if (toActivate == null) toActivate = new List<string>(); toActivate.Add(w); continue; }
            if (!known) continue;

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
