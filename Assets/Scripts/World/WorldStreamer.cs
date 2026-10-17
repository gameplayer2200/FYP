using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界流送：统一裁决"哪个世界现在装载、哪个世界该卸下"。
///
/// 分工：门配对（WorldLink）只描述"谁通往谁"，PortalWindow 只负责取景与穿行；
/// 场景的装卸集中在这里，规则三条——
///   1) 需求装载：玩家进入某扇门的 preloadRadius 内 → 该门的目标世界开始装载；
///   2) 远离卸载：某世界关联的所有门都超出 2×preloadRadius + ReleaseMargin 米 → 卸下
///      （保活半径比取景渲染距离 2×preloadRadius 大，不会出现"还在取景却被卸掉"）；
///   3) 总量封顶：同时存活的世界不超过 MaxLoadedWorlds（含实验室）；满了先淘汰
///      "需求最弱"（离玩家最远）的可卸世界，腾不出空位就暂缓本次装载。
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

    // ── 登记表与逐帧记账 ──
    private static readonly List<PortalWindow> windows = new List<PortalWindow>();
    private static readonly Dictionary<string, float> nearestDist = new Dictionary<string, float>();   // 世界 → 关联门里离玩家最近的距离
    private static readonly Dictionary<string, float> nearestRadius = new Dictionary<string, float>(); // 世界 → 那扇门的 preloadRadius
    private static readonly HashSet<string> wanted = new HashSet<string>();    // 本帧有装载需求的世界
    private static readonly HashSet<string> pinned = new HashSet<string>();    // 门还开着，不许卸
    private static readonly HashSet<string> pending = new HashSet<string>();   // 装载进行中
    private static readonly HashSet<string> unloading = new HashSet<string>(); // 卸载进行中
    private static readonly HashSet<string> blocked = new HashSet<string>();   // 装载失败过（缺 Build Settings 等），不反复重试刷屏

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
            if (pending.Contains(name) || unloading.Contains(name)) continue;
            if (!IsLoadedNow(name)) continue;
            if (kv.Value <= nearestRadius[name] * 2f + ReleaseMargin) continue;   // 还在保活范围内
            BeginUnload(name, "远离");
        }

        // ── 3) 需求装载：按需装；上限满了先淘汰"需求最弱"的世界 ──
        foreach (string name in wanted)
        {
            if (IsLoadedNow(name) || pending.Contains(name) || unloading.Contains(name) || blocked.Contains(name)) continue;
            if (CountLoaded() < MaxLoadedWorlds) { BeginLoad(name); continue; }
            string victim = PickWeakestVictim();
            if (victim == null) continue;   // 全都受保护/正被需要：暂缓，下一帧再试
            BeginUnload(victim, "封顶淘汰");
            BeginLoad(name);
        }
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
        Debug.Log("[WorldStreamer] 装载世界 " + world + "（玩家进入其门的预载半径）");
        op.completed += _ => pending.Remove(world);
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
            if (kv.Key != homeWorld && (IsLoadedNow(kv.Key) || pending.Contains(kv.Key))) n++;
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
        return sb.ToString();
    }
}
