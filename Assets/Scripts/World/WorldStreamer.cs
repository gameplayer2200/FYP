using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界流送：统一裁决"哪个世界现在装载、哪个世界该卸下"。
///
/// 分工：门配对（WorldLink）只描述"谁通往谁"，PortalWindow 只负责取景与穿行；
/// 场景的装卸集中在这里，规则三条——
///   1) 需求装载：玩家进入某扇门的 preloadRadius 内 → 该门的目标世界开始**预载**；
///   2) 远离卸载：某世界关联的所有门都超出 2×preloadRadius + ReleaseMargin 米 → 卸下
///      （保活半径比取景渲染距离 2×preloadRadius 大，不会出现"还在取景却被卸掉"）；
///   3) 总量封顶：同时存活的世界不超过 MaxLoadedWorlds（含实验室）；满了先淘汰
///      "需求最弱"（离玩家最远）的可卸世界，腾不出空位就暂缓本次装载。
///
/// 装载分两步（延迟激活，见 ActivationRadiusFactor）：
///   预载（progress 0→0.9，allowSceneActivation=false）在 preloadRadius 处开始，
///   激活（场景真正就绪）等玩家走到 preloadRadius × 系数 以内。
///   这样大世界的读盘成本摊在玩家走近的路上，不会"刚进半径就整帧卡一下"。
///
/// 永不卸载：实验室（玩家根对象所在场景）、玩家当前所在世界（穿门时登记——
/// 异世界里实验室侧的门都远在天边，没有这条护栏会卸掉玩家脚下的世界）、
/// 以及任何"门还开着"的世界（免得开着的门洞后面突然没了世界）。
///
/// 演化背景见 docs/adr/0003-worlds-stay-resident.md：世界数量到 4~5 个后，
/// 加载职责要收敛到一个可替换的抽象，就是本类；将来换流式分块/预算调度，
/// 只改本类，"常驻"与"延迟卸载"的调用方（PortalWindow）不动。
/// </summary>
public static class WorldStreamer
{
    /// <summary>同时存活的世界上限，含实验室（3 = 实验室 + 最多 2 个异世界）。</summary>
    public static int MaxLoadedWorlds = 3;

    /// <summary>远离卸载余量（米）：最近的门超过 2×preloadRadius + 本值才卸。</summary>
    public const float ReleaseMargin = 2f;

    /// <summary>
    /// 延迟激活：预载（读盘 + 反序列化）在 preloadRadius 处就开始，但场景**先不激活**；
    /// 等玩家走到 preloadRadius × 本值 以内才真正激活。
    ///
    /// 目的：把大世界（本项目 Flooded_Grounds 约 11MB / 27 万行，是其余场景的 5 倍）
    /// 的加载成本摊到玩家走近的路上，避免"刚进半径就整帧卡一下"。
    /// 代价：门洞画面要等激活后才出现（未激活时 IsWorldReady 为 false，PortalWindow 不建取景）。
    ///
    /// 1 = 等价于不延迟激活；0.55 = 走到一半距离才激活。
    /// </summary>
    public static float ActivationRadiusFactor = 0.55f;

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

    private static string homeWorld;     // 玩家根对象所在场景 = 实验室（起始世界），永不卸载
    private static string currentWorld;  // 玩家物理所在世界（穿门时登记），离开前不卸载
    private static bool ticking;

    public static string HomeWorld { get { return homeWorld; } }
    public static string CurrentWorld { get { return currentWorld; } }

    /// <summary>当前存活的世界数（含实验室，含装载中的）。</summary>
    public static int LoadedWorldCount { get { return CountLoaded(); } }

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
        foreach (string name in wanted)
        {
            // 注意必须排除 preloaded：预载完成的场景 isLoaded 仍为 false，
            // 漏了这条会在下一帧再次 BeginLoad，把同一世界装载两遍。
            if (IsLoadedNow(name) || pending.Contains(name) || preloaded.Contains(name)
                || unloading.Contains(name) || blocked.Contains(name)) continue;
            if (CountLoaded() < MaxLoadedWorlds) { BeginLoad(name); continue; }
            string victim = PickWeakestVictim();
            if (victim == null) continue;   // 全都受保护/正被需要：暂缓，下一帧再试
            BeginUnload(victim, "封顶淘汰");
            BeginLoad(name);
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
            if (settled != null) for (int i = 0; i < settled.Count; i++) preloaded.Remove(settled[i]);
        }

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
                    if (preloadOps[w] != null) { preloaded.Add(w); Debug.Log("[WorldStreamer] 预载完成，待激活：" + w); }
                    else { preloadOps.Remove(w); blocked.Add(w); }
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

        if (toActivate != null) for (int i = 0; i < toActivate.Count; i++) Activate(toActivate[i]);
        if (toAbandon != null)
            for (int i = 0; i < toAbandon.Count; i++)
            {
                Debug.Log("[WorldStreamer] 预载后已远离，激活以便释放：" + toAbandon[i]);
                Activate(toAbandon[i]);
            }
    }

    private static void Activate(string world)
    {
        AsyncOperation op;
        if (preloadOps.TryGetValue(world, out op) && op != null)
        {
            op.allowSceneActivation = true;
            Debug.Log("[WorldStreamer] 激活世界 " + world);
        }
        preloadOps.Remove(world);
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
        var op = SceneManager.LoadSceneAsync(world, LoadSceneMode.Additive);
        if (op == null)
        {
            pending.Remove(world);
            blocked.Add(world);
            Debug.LogError("[WorldStreamer] 世界装载失败：" + world + "（场景是否已加入 Build Settings？）");
            return;
        }

        // 关键：先只预载、不激活。progress 会停在 0.9，等玩家走近再置 allowSceneActivation = true
        //（见 TickPreloadAndActivation）——把"读盘 + 反序列化"和"激活"错开，避免刚进半径就卡一帧。
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
        var op = SceneManager.UnloadSceneAsync(world);
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
            preloaded.Remove(world);
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
