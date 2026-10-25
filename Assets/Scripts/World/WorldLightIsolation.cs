using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界光影隔离：让每个世界的光只照自己，实验室的明暗与阴影不随其它世界装卸而变化。
///
/// 背景：方向光是场景全局的（不受位置限制），每个世界场景各带一盏"太阳"；加性装载后
/// 它们会同时照亮实验室，还会抢占每个物体有限的"逐像素灯"名额（前向渲染按重要性排序，
/// 新增的灯会把实验室自己的灯挤下去），表现为玩家走近某扇门时实验室整体明暗突变
/// （实测：装入三个世界后 97% 的像素发生变化、83% 变暗）。
///
/// 做法（Built-in 管线最稳的隔离手段是"图层 + 灯的 CullingMask"）：
///   1) 世界场景装载后，把它的全部渲染体改挂到该世界的专属图层（每个世界一个；
///      前四个内置世界用命名图层，其余从空闲池自动分配——新增世界不用改代码），
///      并把它的所有灯收敛为"只照本世界图层"；
///   2) 实验室的灯做一次反向收敛：CullingMask 去掉全部世界图层（玩家身上的灯除外，
///      手电要在任何世界都能照物）。
/// 于是实验室只被实验室的灯照亮，世界只被自己的灯照亮；从实验室看取景窗（预览）
/// 与真正走进去之后的观感一致（预览=进入后），灯槽也不再互相抢占。
///
/// 由 WorldStreamer.Tick 每帧调用 Sync（幂等）：同一场景只处理一次，卸载后重装会重新处理；
/// 实验室那侧只在"世界图层位集合"增长时补一次收敛。
/// </summary>
public static class WorldLightIsolation
{
    // 内置世界 → 命名图层（在 ProjectSettings/TagManager 里登记，便于排查）
    private static readonly string[,] BuiltIn =
    {
        { "Metro", "World_Metro" },
        { "FactoryDay", "World_FactoryDay" },
        { "FactoryNight", "World_FactoryNight" },
        { "Flooded_Grounds", "World_Flooded" },
    };

    private const int FirstDynamicLayer = 11;   // 0~6 是项目既有图层，7~10 留给内置世界
    private const int LastLayer = 31;

    private static readonly Dictionary<string, int> layerOf = new Dictionary<string, int>();
    private static readonly HashSet<string> applied = new HashSet<string>();     // 已收敛过的场景名
    private static readonly HashSet<string> loadedNow = new HashSet<string>();   // 本轮在册场景（Sync 内复用）
    private static int worldBits;               // 全部世界图层的位并集（实验室灯的排除掩码）
    private static int homeBitsApplied = -1;    // 上次给实验室做收敛时的 worldBits（-1=还没做）
    private static bool preRegistered;
    private static bool warnedExhausted;

    // ── 分帧收敛 ──
    // 为什么要分帧：ApplyWorld 原先一次性跑完。OfficePack 有 6448 个 Renderer，
    // 实测同步写 layer 要 17.2ms —— 整整一帧的卡顿，而它正好落在"按 E 激活世界"那一帧。
    // 现在拆成每帧 2ms 的预算，世界激活后十帧左右收敛完；这期间玩家正在看门阈校准屏
    // （ThresholdLoadScreen），过渡态不可见。
    private const double FrameBudgetMs = 2.0;

    /// <summary>
    /// 一个世界的收敛作业：**自己走 Transform 树**，不用 GetComponentsInChildren。
    ///
    /// 为什么不用 GetComponentsInChildren：它是一次不可分割的原生调用。OfficePack 的
    /// Floor1 一个根下就有 2487 个 Renderer，这一次调用实测约 5~6ms —— 分帧分不掉它，
    /// 预算再小也会有一帧被它顶穿（实测首帧仍是 7.8ms）。
    /// 改成手工 DFS：每次只取当前节点的 4 种组件（恒定小开销），检查一次预算就返回，
    /// 于是不管场景多大，单帧成本都由预算而非场景规模决定。
    /// </summary>
    private sealed class IsolationJob
    {
        public string sceneName;
        public int layer;

        // DFS 栈：待访问的 Transform。
        public readonly List<Transform> stack = new List<Transform>();
        // 当前节点收集到的组件与游标（一个节点的组件很少，可以整批收完）。
        // 用固定 4 槽 + 计数，**不复用 new 数组**：场景有几千个节点时，
        // 每个节点分配一个小数组会把托管堆推起来，GC 会在收敛中途砸出一帧 15ms 的尖峰
        // （实测 Laboratory 第 100 帧左右出现过 8.6 / 15.4ms）。
        public readonly Component[] current = new Component[4];
        public int count;
        public int ci;

        public int objs, lit, prb;
        public double ms;               // 累计耗时（跨帧），用于日志
    }

    private static readonly List<IsolationJob> jobs = new List<IsolationJob>();

    /// <summary>世界图层位并集（调试/验收用）。</summary>
    public static int WorldLayerBits { get { return worldBits; } }

    public static void Sync(string homeWorld)
    {
        if (string.IsNullOrEmpty(homeWorld)) return;
        PreRegister();

        // 1) 当前装载的每个非起始场景：分配图层 → 排一次光影收敛（该步骤内部按帧预算推进）
        loadedNow.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            loadedNow.Add(s.name);
            if (s.name == homeWorld) continue;
            int layer = EnsureLayer(s.name);
            if (layer < 0) continue;
            if (applied.Contains(s.name)) continue;
            if (HasJob(s.name)) continue;
            applied.Add(s.name);
            BeginApplyWorld(s, layer);
        }
        // 已卸载的场景清掉处理记录：重装时重新收敛（顺便丢掉没跑完的作业）
        applied.RemoveWhere(n => !loadedNow.Contains(n));
        jobs.RemoveAll(j => !loadedNow.Contains(j.sceneName));

        // 1b) 推进收敛作业：每帧预算 FrameBudgetMs，超预算就留到下一帧
        TickJobs();

        // 2) 实验室：灯收敛一次（世界图层位增长时再补一次）
        if (homeBitsApplied != worldBits)
        {
            ApplyHome(homeWorld);
            homeBitsApplied = worldBits;
        }
    }

    private static bool HasJob(string sceneName)
    {
        for (int i = 0; i < jobs.Count; i++) if (jobs[i].sceneName == sceneName) return true;
        return false;
    }

    /// <summary>建作业：把根对象压进 DFS 栈（很便宜），其余都在推进时按预算做。</summary>
    private static void BeginApplyWorld(Scene scene, int layer)
    {
        var job = new IsolationJob { sceneName = scene.name, layer = layer };
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i] != null) job.stack.Add(roots[i].transform);
        jobs.Add(job);
        LoadDiag.Begin();
    }

    /// <summary>按帧预算推进所有作业。</summary>
    private static void TickJobs()
    {
        if (jobs.Count == 0) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int ji = 0; ji < jobs.Count; ji++)
        {
            var job = jobs[ji];
            Progress(job, sw, jobs.Count > 1 ? FrameBudgetMs / jobs.Count : FrameBudgetMs);
            if (job.stack.Count == 0 && job.ci >= job.count)
            {
                jobs.RemoveAt(ji--);
                LoadDiag.End("光影隔离「" + job.sceneName + "」（物体 " + job.objs + "、灯 " + job.lit + "）");
                Debug.Log("[WorldLightIsolation] 世界「" + job.sceneName + "」光影隔离：图层 " + job.layer
                    + "（物体 " + job.objs + "、灯 " + job.lit + "、反射探针 " + job.prb
                    + "；分帧 " + job.ms.ToString("F1") + " ms）");
            }
        }
    }

    /// <summary>在预算内尽量推进一个作业（手工 DFS，单节点开销恒定）。</summary>
    private static void Progress(IsolationJob job, System.Diagnostics.Stopwatch sw, double budgetMs)
    {
        int bit = 1 << job.layer;
        int guard = 0;

        // job.ms 累计的是"本作业真正花掉的时间"，所以要记增量而不是 sw 的绝对值。
        // （早期写成 job.ms += sw.Elapsed 是把不断增长的绝对值反复累加，
        //   100ms 的作业会记成 6000+ms，日志看上去像分帧完全没生效。）
        double lastMs = sw.Elapsed.TotalMilliseconds;
        while (true)
        {
            // 当前节点还没处理完 → 处理它的组件
            if (job.ci < job.count)
            {
                var c = job.current[job.ci++];
                if (c is Renderer || c is Terrain)
                {
                    if (c != null && c.gameObject.layer != job.layer) { c.gameObject.layer = job.layer; job.objs++; }
                }
                else if (c is Light)
                {
                    var l = c as Light;
                    if (l != null && l.cullingMask != bit) { l.cullingMask = bit; job.lit++; }
                }
                else if (c is ReflectionProbe)
                {
                    var p = c as ReflectionProbe;
                    if (p != null && (p.cullingMask & bit) == 0) { p.cullingMask |= bit; job.prb++; }
                }
                if ((++guard & 15) == 0 && sw.Elapsed.TotalMilliseconds >= budgetMs)
                {
                    job.ms += sw.Elapsed.TotalMilliseconds - lastMs;
                    return;
                }
                continue;
            }

            // 当前节点的组件处理完 → 弹出下一个节点
            if (job.stack.Count == 0)
            {
                job.ms += sw.Elapsed.TotalMilliseconds - lastMs;
                return;
            }
            var t = job.stack[job.stack.Count - 1];
            job.stack.RemoveAt(job.stack.Count - 1);
            if (t == null) continue;

            // 只在这个节点上取它自己的 4 种组件（不用 GetComponentsInChildren，零分配写入固定槽）
            FillNodeComponents(t, job.current, out job.count);
            job.ci = 0;

            // 子节点全部入栈（DFS 顺序无所谓，只求每帧量恒定）
            int n = t.childCount;
            for (int i = 0; i < n; i++)
            {
                var ch = t.GetChild(i);
                if (ch != null) job.stack.Add(ch);
            }

            job.ms += sw.Elapsed.TotalMilliseconds - lastMs;
            lastMs = sw.Elapsed.TotalMilliseconds;   // 继续跑：下一段增量从这里算起，避免重复累加
            if (sw.Elapsed.TotalMilliseconds >= budgetMs) return;
        }
    }

    /// <summary>取一个 Transform 自身挂着的 4 种关注组件，写进固定槽（零分配）。</summary>
    private static void FillNodeComponents(Transform t, Component[] buffer, out int count)
    {
        var go = t.gameObject;
        count = 0;
        // 逐个 GetComponent：比 GetComponents&lt;T&gt;() 省掉全类型扫描与数组分配
        var r = go.GetComponent<Renderer>();
        if (r != null) buffer[count++] = r;
        var ter = go.GetComponent<Terrain>();
        if (ter != null) buffer[count++] = ter;
        var li = go.GetComponent<Light>();
        if (li != null) buffer[count++] = li;
        var pr = go.GetComponent<ReflectionProbe>();
        if (pr != null) buffer[count++] = pr;
    }

    // ── 实验室侧：灯排除全部世界图层（玩家的灯 = 手电不排除）──
    private static void ApplyHome(string homeWorld)
    {
        var scene = SceneManager.GetSceneByName(homeWorld);
        if (!scene.isLoaded) return;
        LoadDiag.Begin();
        var playerRoot = PlayerRef.IsReady ? PlayerRef.Root : null;
        int changed = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (playerRoot != null && l.transform.IsChildOf(playerRoot)) continue;
                int m = l.cullingMask & ~worldBits;
                if (m != l.cullingMask) { l.cullingMask = m; changed++; }
            }
        LoadDiag.End("实验室灯收敛（改 " + changed + " 盏）");
        Debug.Log("[WorldLightIsolation] 实验室灯收敛：排除世界层 0x" + worldBits.ToString("X")
            + "（改 " + changed + " 盏；玩家的灯不动）");
    }

    // ── 图层分配 ──
    private static void PreRegister()
    {
        if (preRegistered) return;
        preRegistered = true;
        for (int i = 0; i < BuiltIn.GetLength(0); i++)
        {
            string scene = BuiltIn[i, 0], layerName = BuiltIn[i, 1];
            int l = LayerMask.NameToLayer(layerName);
            if (l >= 0) { layerOf[scene] = l; worldBits |= 1 << l; }
            else Debug.LogWarning("[WorldLightIsolation] TagManager 里找不到图层「" + layerName + "」——" + scene + " 将改用动态图层");
        }
    }

    private static int EnsureLayer(string sceneName)
    {
        int l;
        if (layerOf.TryGetValue(sceneName, out l)) return l;

        // 先用「门配对工具」在 TagManager 里登记的命名图层 World_{场景名}。
        // 否则两边各用一套编号：场景里的物体被挂到动态图层 N，而 TagManager 里 N 早已是
        // 另一个世界的名字，排查时完全对不上。
        l = LayerMask.NameToLayer("World_" + sceneName);
        if (l >= 0 && !LayerTaken(l))
        {
            layerOf[sceneName] = l;
            worldBits |= 1 << l;
            Debug.Log("[WorldLightIsolation] 世界「" + sceneName + "」使用登记图层 " + l + "（World_" + sceneName + "）");
            return l;
        }

        for (l = FirstDynamicLayer; l <= LastLayer; l++)
        {
            if (LayerTaken(l)) continue;
            // 已被别人命名占用的图层不能抢（TagManager 里的名字比动态编号更可信）
            if (!string.IsNullOrEmpty(LayerMask.LayerToName(l))) continue;
            layerOf[sceneName] = l;
            worldBits |= 1 << l;
            Debug.Log("[WorldLightIsolation] 世界「" + sceneName + "」分配动态图层 " + l);
            return l;
        }
        if (!warnedExhausted)
        {
            warnedExhausted = true;
            Debug.LogError("[WorldLightIsolation] 空闲图层用尽——世界「" + sceneName + "」不做光影隔离");
        }
        return -1;
    }

    private static bool LayerTaken(int layer)
    {
        foreach (var kv in layerOf)
            if (kv.Value == layer) return true;
        return false;
    }
}
