using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 世界槽位审计与归槽（2026-10-24 定式，规则见 AGENTS.md §4「世界槽位」）。
///
/// 背景：全部世界常驻同一坐标空间、由 WorldStreamer 按需装卸，世界间的几何穿插
/// 只能靠「各占一片远方坐标」避免。本工具把这套约定自动化：
///   · 审计：列出每个世界的实体包围盒、所在槽位、与最近世界的净距、穿模判定；
///   · 归槽：把压在别人身上（或未入槽）的世界整体平移到下一个空槽。
///
/// 槽位规则：5000m 网格，内容包围盒中心对齐槽心；新世界沿 −X 递进；
/// 两世界实体 AABB 净距 ≥2000m；坐标绝对值 ≤50000（单精度安全上限）。
///
/// ⚠️ 实测坑：根节点整体平移后，未弄脏的子渲染器在编辑器进程内保留旧
/// Renderer.bounds 缓存（跨 execute_code 调用仍在）——本工具一律用
/// 网格本地包围盒 × localToWorldMatrix 现算，不信 Renderer.bounds。
/// </summary>
public static class WorldSlotAudit
{
    private const float SlotSize = 5000f;      // 槽位网格边长（m）
    private const float MinClearance = 2000f;  // 两世界实体 AABB 最小净距（m）
    private const float MaxCoord = 50000f;     // 单精度浮点安全上限（m）
    private const float SlotSnap = 500f;       // 中心距槽心 ≤ 此值视为已入槽

    private class WorldBox
    {
        public string Path;
        public string Name;
        public Bounds Solid = new Bounds(Vector3.zero, Vector3.zero); // 实体盒（不含地形）
        public Bounds TerrainFootprint = new Bounds(Vector3.zero, Vector3.zero); // 地形 XZ 足迹（仅提示）
        public bool HasSolid;
        public bool HasTerrain;
        public Vector2Int Slot;
        public bool InSlot;
        public float MinDist = float.MaxValue;
        public string Nearest = "—";
        public bool Overlaps;
    }

    [MenuItem("FYP/世界槽位/审计（只读）")]
    public static void AuditMenu()
    {
        var report = Audit();
        Debug.Log(report);
        EditorUtility.DisplayDialog("世界槽位审计", report, "知道了");
    }

    [MenuItem("FYP/世界槽位/审计 + 未入槽世界自动归槽")]
    public static void AuditAndPlaceMenu()
    {
        var report = new StringBuilder();
        var boxes = MeasureAll();
        foreach (var b in boxes)
        {
            if (b.InSlot) continue;
            if (!b.HasSolid) { report.AppendLine("跳过（无实体内容）：" + b.Name); continue; }
            report.AppendLine(PlaceAtNextFreeSlot(b.Path, boxes));
            boxes = MeasureAll(); // 每次迁移后重测，避免用过期数据给下一个世界找槽
        }
        report.AppendLine(Audit());
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("世界槽位审计 + 归槽", report.ToString(), "知道了");
    }

    /// <summary>审计入口：返回文本报告（只读，不改任何场景）。</summary>
    public static string Audit()
    {
        var boxes = MeasureAll();
        Pairwise(boxes);
        var sb = new StringBuilder();
        sb.AppendLine("== 世界槽位审计 ==");
        foreach (var b in boxes)
        {
            string verdict = b.Overlaps ? "❌ 穿模" : (b.MinDist < MinClearance ? "⚠️ 净距不足" : "✅");
            string slot = b.InSlot ? "槽(" + b.Slot.x + "," + b.Slot.y + ")" : "未入槽";
            sb.AppendLine(string.Format("{0,-18} {1} {2} 最近={3} 净距={4}m",
                b.Name, verdict, slot, b.Nearest, b.MinDist == float.MaxValue ? "∞" : b.MinDist.ToString("F0")));
            sb.AppendLine("    实体 " + Fmt(b.Solid) + (b.HasTerrain ? "  地形足迹 " + Fmt(b.TerrainFootprint) : ""));
            if (Mathf.Max(Mathf.Abs(b.Solid.min.x), Mathf.Abs(b.Solid.max.x)) > MaxCoord ||
                Mathf.Max(Mathf.Abs(b.Solid.min.z), Mathf.Abs(b.Solid.max.z)) > MaxCoord)
                sb.AppendLine("    ⚠️ 坐标超出 ±" + MaxCoord + "（单精度抖动风险）");
        }
        sb.AppendLine("规则：槽=" + SlotSize + "m 网格中心对齐；净距≥" + MinClearance + "m；|坐标|≤" + MaxCoord);
        return sb.ToString();
    }

    /// <summary>把指定世界平移到下一个空 −X 槽（其余世界不动），返回操作日志。</summary>
    private static string PlaceAtNextFreeSlot(string scenePath, List<WorldBox> boxes)
    {
        var me = boxes.Find(b => b.Path == scenePath);
        if (me == null) return "未找到场景：" + scenePath;
        // 沿 −X 从 −15000 起逐槽找空位（现状占用：实验室≈0、工厂昼 −4324、夜 +10475 等）
        for (int i = -3; i >= -9; i--)
        {
            var slotCenter = new Vector3(i * SlotSize, me.Solid.center.y, me.Solid.center.z);
            var shifted = Shift(me.Solid, slotCenter - me.Solid.center);
            bool ok = true;
            foreach (var other in boxes)
            {
                if (other == me || !other.HasSolid) continue;
                if (Clearance(shifted, other.Solid) < MinClearance) { ok = false; break; }
            }
            if (!ok) continue;
            var delta = slotCenter - me.Solid.center;
            var sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!sc.isLoaded) return "打开失败：" + scenePath;
            foreach (var go in sc.GetRootGameObjects())
                if (go != null) go.transform.position += delta;
            EditorSceneManager.SaveScene(sc);
            EditorSceneManager.CloseScene(sc, true);
            return "已迁移 " + me.Name + " → 槽心 " + slotCenter.x.ToString("F0") + "（Δ=" + delta.x.ToString("F0") + "）";
        }
        return me.Name + "：−X 方向 7 个槽均无空位，请人工规划坐标";
    }

    // ---------- 测量：网格本地包围盒 × 变换矩阵现算，不用 Renderer.bounds ----------

    private static List<WorldBox> MeasureAll()
    {
        var result = new List<WorldBox>();
        var seen = new HashSet<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || !File.Exists(s.path) || !seen.Add(s.path)) continue;
            var sc = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Additive);
            if (!sc.isLoaded) continue;
            var box = new WorldBox { Path = s.path, Name = sc.name };
            foreach (var root in sc.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    bool isTerrain = r.GetComponent<Terrain>() != null;
                    Bounds local;
                    if (mf != null && mf.sharedMesh != null) local = mf.sharedMesh.bounds;
                    else if (r is BillboardRenderer || r is LineRenderer) continue;
                    else continue; // 粒子等运行时形态不参与静态审计
                    var m = r.transform.localToWorldMatrix;
                    var world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
                    // 有旋转/缩放时用 8 个角点包围
                    var e = local.extents;
                    var corners = new[]
                    {
                        new Vector3(-e.x,-e.y,-e.z), new Vector3(e.x,-e.y,-e.z),
                        new Vector3(-e.x,e.y,-e.z), new Vector3(e.x,e.y,-e.z),
                        new Vector3(-e.x,-e.y,e.z), new Vector3(e.x,-e.y,e.z),
                        new Vector3(-e.x,e.y,e.z), new Vector3(e.x,e.y,e.z),
                    };
                    foreach (var c in corners) world.Encapsulate(m.MultiplyPoint3x4(local.center + c));
                    if (isTerrain)
                    {
                        world.min = new Vector3(world.min.x, 0f, world.min.z); // 地形渲染盒含整张高程范围，压成 XZ 足迹
                        if (box.HasTerrain) box.TerrainFootprint.Encapsulate(world);
                        else { box.TerrainFootprint = world; box.HasTerrain = true; }
                    }
                    else
                    {
                        if (box.HasSolid) box.Solid.Encapsulate(world);
                        else { box.Solid = world; box.HasSolid = true; }
                    }
                }
            }
            if (box.HasSolid)
            {
                var c = box.Solid.center;
                box.Slot = new Vector2Int(Mathf.RoundToInt(c.x / SlotSize), Mathf.RoundToInt(c.z / SlotSize));
                box.InSlot = Mathf.Abs(c.x - box.Slot.x * SlotSize) < SlotSnap &&
                             Mathf.Abs(c.z - box.Slot.y * SlotSize) < SlotSnap;
            }
            EditorSceneManager.CloseScene(sc, true);
            result.Add(box);
        }
        return result;
    }

    private static void Pairwise(List<WorldBox> boxes)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i]; var b = boxes[j];
                if (!a.HasSolid || !b.HasSolid) continue;
                float d = Clearance(a.Solid, b.Solid);
                if (d < a.MinDist) { a.MinDist = d; a.Nearest = b.Name; }
                if (d < b.MinDist) { b.MinDist = d; b.Nearest = a.Name; }
                if (d <= 0f) { a.Overlaps = true; b.Overlaps = true; }
            }
        }
    }

    /// <summary>两 AABB 的三维净距：相交返回 0（用 Overlaps 区分相切与相交）。</summary>
    private static float Clearance(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
        if (dx == 0f && dy == 0f && dz == 0f) return -1f; // 相交
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Bounds Shift(Bounds b, Vector3 delta)
    {
        return new Bounds(b.center + delta, b.size);
    }

    private static string Fmt(Bounds b)
    {
        return "[" + b.min.x.ToString("F0") + "," + b.min.y.ToString("F0") + "," + b.min.z.ToString("F0") + " ~ " +
               b.max.x.ToString("F0") + "," + b.max.y.ToString("F0") + "," + b.max.z.ToString("F0") + "]";
    }
}
