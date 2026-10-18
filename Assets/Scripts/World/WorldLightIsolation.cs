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

    /// <summary>世界图层位并集（调试/验收用）。</summary>
    public static int WorldLayerBits { get { return worldBits; } }

    public static void Sync(string homeWorld)
    {
        if (string.IsNullOrEmpty(homeWorld)) return;
        PreRegister();

        // 1) 当前装载的每个非起始场景：分配图层 → 做一次光影收敛
        loadedNow.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            loadedNow.Add(s.name);
            if (s.name == homeWorld) continue;
            int layer = EnsureLayer(s.name);
            if (layer < 0 || applied.Contains(s.name)) continue;
            applied.Add(s.name);
            ApplyWorld(s, layer);
        }
        // 已卸载的场景清掉处理记录：重装时重新收敛
        applied.RemoveWhere(n => !loadedNow.Contains(n));

        // 2) 实验室：灯收敛一次（世界图层位增长时再补一次）
        if (homeBitsApplied != worldBits)
        {
            ApplyHome(homeWorld);
            homeBitsApplied = worldBits;
        }
    }

    // ── 世界侧：整场改挂专属图层 + 灯只照本世界 ──
    private static void ApplyWorld(Scene scene, int layer)
    {
        LoadDiag.Begin();
        int bit = 1 << layer;
        int objs = 0, lights = 0, probes = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.layer != layer) { r.gameObject.layer = layer; objs++; }
            foreach (var t in root.GetComponentsInChildren<Terrain>(true))
                if (t.gameObject.layer != layer) { t.gameObject.layer = layer; objs++; }
            foreach (var l in root.GetComponentsInChildren<Light>(true))
                if (l.cullingMask != bit) { l.cullingMask = bit; lights++; }
            foreach (var p in root.GetComponentsInChildren<ReflectionProbe>(true))
                if ((p.cullingMask & bit) == 0) { p.cullingMask |= bit; probes++; }
        }
        LoadDiag.End("光影隔离「" + scene.name + "」（物体 " + objs + "、灯 " + lights + "）");
        Debug.Log("[WorldLightIsolation] 世界「" + scene.name + "」光影隔离：图层 " + layer
            + "（物体 " + objs + "、灯 " + lights + "、反射探针 " + probes + "）");
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
        for (l = FirstDynamicLayer; l <= LastLayer; l++)
        {
            if (LayerTaken(l)) continue;
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
