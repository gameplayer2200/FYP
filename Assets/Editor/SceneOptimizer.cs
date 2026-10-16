using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景性能优化工具（编辑器专用）——对应审查报告 P1-1 / P1-2。
///
/// 为什么需要它：本工程 SpecimenLab 场景里
///   · m_OcclusionCullingData = {fileID: 0} → 遮挡剔除从未烘焙；
///   · 全部场景对象 m_StaticEditorFlags = 0 → 静态合批 / GI / 遮挡剔除都没参与；
///   · 480 个 MeshCollider（478 个非凸）。
/// 这三项都必须在编辑器里改，纯改脚本/YAML 不安全，所以做成菜单工具。
///
/// 菜单入口：Tools / 场景优化 / ...
/// 所有"标记"操作都走 Undo，可用 Ctrl+Z 撤销。
/// </summary>
public static class SceneOptimizer
{
    private const string MenuRoot = "Tools/场景优化/";

    /// <summary>
    /// 这些脚本组件所在的物体及其子树一律排除——它们运行时会移动/旋转/显隐，
    /// 一旦标记 Static 会被烘进静态合批，动画与交互全部失效。
    /// </summary>
    private static readonly string[] MovableTypeNames =
    {
        "AnywhereDoor", "PortalWindow", "ChargeStation", "Inventory", "InventoryUI",
        "InteractionRunner", "BatterySystem", "PlayerMovement", "PlayerAnimatorProxy"
    };

    /// <summary>
    /// 按名字排除：门（叶板会在运行时旋转，且异世界侧的 AnywhereDoor 是运行时补挂的，
    /// 组件检查抓不到）与玩家。
    /// </summary>
    private static readonly string[] ExcludedNameFragments = { "door", "player" };

    // ─────────────────────────────────────────────────────────────
    // 1. 标记静态几何
    // ─────────────────────────────────────────────────────────────
    [MenuItem(MenuRoot + "1. 扫描并标记静态几何", false, 10)]
    public static void MarkStaticGeometry()
    {
        var candidates = CollectStaticCandidates();
        if (candidates.Count == 0)
        {
            Debug.Log("[SceneOptimizer] 未找到可标记为静态的几何体（可能已全部标记）。");
            return;
        }

        var flags = StaticEditorFlags.ContributeGI
                  | StaticEditorFlags.BatchingStatic
                  | StaticEditorFlags.OccluderStatic
                  | StaticEditorFlags.OccludeeStatic;

        bool ok = EditorUtility.DisplayDialog(
            "标记静态几何",
            "将把 " + candidates.Count + " 个物体标记为 Static\n" +
            "（Contribute GI / Batching / Occluder / Occludee）\n\n" +
            "已自动排除：玩家、任意门、传送门、充电桩、灯、相机、\n" +
            "带 Animator / Rigidbody / LODGroup 的物体。\n\n" +
            "标记后请执行「2. 烘焙遮挡剔除」。可用 Ctrl+Z 撤销。",
            "标记", "取消");
        if (!ok) return;

        var scene = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("标记静态几何");
        int group = Undo.GetCurrentGroup();

        foreach (var go in candidates)
        {
            Undo.RecordObject(go, "标记静态几何");
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[SceneOptimizer] 已标记 " + candidates.Count + " 个物体为 Static。下一步：Tools/场景优化/2. 烘焙遮挡剔除");
    }

    // ─────────────────────────────────────────────────────────────
    // 2. 烘焙遮挡剔除
    // ─────────────────────────────────────────────────────────────
    [MenuItem(MenuRoot + "2. 烘焙遮挡剔除", false, 11)]
    public static void BakeOcclusionCulling()
    {
        if (!EditorUtility.DisplayDialog(
            "烘焙遮挡剔除",
            "将对当前场景烘焙 Occlusion Culling。\n\n" +
            "前提：静态几何已标记 Occluder Static / Occludee Static\n" +
            "（先执行「1. 扫描并标记静态几何」）。\n\n" +
            "烘焙期间编辑器会短暂卡住，大场景可能需要数十秒。",
            "开始烘焙", "取消")) return;

        StaticOcclusionCulling.Compute();
        Debug.Log("[SceneOptimizer] 遮挡剔除烘焙完成。");
    }

    // ─────────────────────────────────────────────────────────────
    // 3. 扫描非凸 MeshCollider
    // ─────────────────────────────────────────────────────────────
    [MenuItem(MenuRoot + "3. 扫描非凸 MeshCollider（只报告）", false, 12)]
    public static void ScanNonConvexMeshColliders()
    {
        var cols = Object.FindObjectsOfType<MeshCollider>(true);
        int total = cols.Length;
        int nonConvex = 0;

        // 按顶层父物体归类，便于判断该优化哪一片区域
        var byRoot = new Dictionary<string, int>();
        var examples = new List<string>();

        foreach (var c in cols)
        {
            if (c == null || c.convex) continue;
            nonConvex++;

            Transform root = c.transform;
            while (root.parent != null) root = root.parent;
            string key = root.name;
            int n;
            byRoot.TryGetValue(key, out n);
            byRoot[key] = n + 1;

            if (examples.Count < 15) examples.Add(c.name);
        }

        var sb = new StringBuilder();
        sb.AppendLine("[SceneOptimizer] MeshCollider 统计（当前场景）");
        sb.AppendLine("  总数：" + total + "，其中非凸：" + nonConvex);
        sb.AppendLine("  非凸 MeshCollider 无法用 GPU 加速，物理求交与内存开销远高于基本体。");
        sb.AppendLine("  建议：墙面/地面/大型结构保留；家具、装饰、小物件改用 Box / Capsule / SphereCollider。");
        sb.AppendLine("  按顶层父物体归类：");
        foreach (var kv in byRoot)
            sb.AppendLine("    " + kv.Key + " → " + kv.Value + " 个");
        sb.AppendLine("  示例：" + string.Join("、", examples));

        Debug.Log(sb.ToString());
    }

    // ─────────────────────────────────────────────────────────────
    // 4. 选中物体：MeshCollider → BoxCollider（小物件用）
    // ─────────────────────────────────────────────────────────────
    [MenuItem(MenuRoot + "4. 选中物体：MeshCollider 换成 BoxCollider", false, 13)]
    public static void ConvertSelectedToBoxCollider()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("[SceneOptimizer] 请先在 Hierarchy 里选中要转换的物体。");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "替换为 BoxCollider",
            "将把选中的 " + selected.Length + " 个物体上的 MeshCollider\n" +
            "替换为按渲染包围盒生成的 BoxCollider。\n\n" +
            "⚠ 只适合规则的小物件（箱子、桌子、柜子）。\n" +
            "墙面、楼梯、异形结构请勿使用——包围盒会明显偏离实际形状，\n" +
            "可能导致穿模或卡住。",
            "替换", "取消")) return;

        int converted = 0;
        Undo.SetCurrentGroupName("MeshCollider → BoxCollider");
        int group = Undo.GetCurrentGroup();

        foreach (var go in selected)
        {
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) continue;

            Bounds b;
            var mr = go.GetComponent<MeshRenderer>();
            var mf = go.GetComponent<MeshFilter>();
            if (mr != null) b = mr.localBounds;
            else if (mf != null && mf.sharedMesh != null) b = mf.sharedMesh.bounds;
            else continue;

            Undo.DestroyObjectImmediate(mc);
            var box = Undo.AddComponent<BoxCollider>(go);
            box.center = b.center;
            box.size = b.size;
            converted++;
        }

        Undo.CollapseUndoOperations(group);
        if (converted > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[SceneOptimizer] 已转换 " + converted + " 个 MeshCollider → BoxCollider。");
    }

    // ─────────────────────────────────────────────────────────────
    // 内部：收集静态候选
    // ─────────────────────────────────────────────────────────────
    private static List<GameObject> CollectStaticCandidates()
    {
        var result = new List<GameObject>();
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (IsStaticCandidate(go)) result.Add(go);
            }
        }
        return result;
    }

    private static bool IsStaticCandidate(GameObject go)
    {
        // 必须是有材质的 MeshRenderer 才值得静态化（静态合批 / GI / 遮挡都靠它）
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null || mr.sharedMaterial == null) return false;

        // 自身及任意祖先命中排除规则 → 跳过
        for (var t = go.transform; t != null; t = t.parent)
        {
            string lower = t.name.ToLowerInvariant();
            for (int i = 0; i < ExcludedNameFragments.Length; i++)
                if (lower.Contains(ExcludedNameFragments[i])) return false;

            if (t.GetComponent<Animator>() != null) return false;
            if (t.GetComponent<Rigidbody>() != null) return false;
            if (t.GetComponent<Light>() != null) return false;
            if (t.GetComponent<Camera>() != null) return false;
            if (t.GetComponent<ParticleSystem>() != null) return false;
            if (t.GetComponent<LODGroup>() != null) return false;

            var mbs = t.GetComponents<MonoBehaviour>();
            for (int i = 0; i < mbs.Length; i++)
            {
                if (mbs[i] == null) continue;
                string typeName = mbs[i].GetType().Name;
                for (int j = 0; j < MovableTypeNames.Length; j++)
                    if (typeName == MovableTypeNames[j]) return false;
            }
        }

        // 已经是静态的无需重复标记
        var flags = GameObjectUtility.GetStaticEditorFlags(go);
        if ((flags & StaticEditorFlags.BatchingStatic) != 0
            && (flags & StaticEditorFlags.OccluderStatic) != 0
            && (flags & StaticEditorFlags.ContributeGI) != 0) return false;

        return true;
    }
}
