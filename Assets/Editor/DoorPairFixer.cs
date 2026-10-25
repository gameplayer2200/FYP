using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 门配对一次性修复（2026-10-18）。
///
/// ── 修的什么病 ──────────────────────────────────────────────────
/// 1) **世界侧的门被改名成了 `-A`**，而命名约定里 `anywhere-door-X-A` 的含义是
///    "不在世界 X 里的、通往世界 X 的那扇门"（通常是实验室侧）。于是世界 X 场景里的
///    `anywhere-door-X-A` 推导出"目标世界 = 自己所在的世界" → 自己跟自己配对。
///    症状：走到这扇门穿过去，落点是**门背面 0.02m**（= 门所在的那堵墙里）——
///    玩家穿模、被物理挤出来，看起来像"掉下去了"；而且这扇门永远回不到实验室。
///    修法：世界侧的门改回 `anywhere-door-X`（推导目标 = SpecimenLab，配对 `anywhere-door-X-A`）。
///
/// 2) **链条上的门**（造门时就规划了：实验室 → FactoryDay → FactoryNight → Flooded_Grounds）
///    没法用命名约定表达——约定里"不带 -A"恒等于"回实验室"。这几扇门必须挂显式
///    WorldLink 资产（Assets/WorldLinks/ 里已备好），并关掉 autoPair。
///    症状：推导出"目标世界 = SpecimenLab"，而实验室里根本没有对应的配对门 →
///    PortalWindow 报"配对已失效，本门停用"，这条链整个走不通。
///
/// ── 用法 ────────────────────────────────────────────────────────
/// 菜单：FYP/门配对/修复世界侧门名与链条（一次性）
/// 幂等：已经修好的门会被跳过，可以反复点。
/// 会保存涉及的门场景（Metro / FactoryDay / FactoryNight / Flooded_Grounds）。
///
/// ── 实现注意（踩过的坑）─────────────────────────────────────────
/// 改完必须**先把场景存盘再关**：`CloseScene(scene, true)` 是不保存关场景，
/// 先关再"回头保存"等于把旧文件重存一遍，改动全丢（本工具第一版就栽在这）。
/// 所以本工具把打开的场景留在手里，全部改完 → 保存 → 才关闭自己打开的那些。
/// </summary>
public static class DoorPairFixer
{
    // 世界侧的门：场景 → (现名, 应改名)
    private static readonly string[,] Renames =
    {
        { "Metro",      "anywhere-door-Metro-A",      "anywhere-door-Metro" },
        { "FactoryDay", "anywhere-door-FactoryDay-A", "anywhere-door-FactoryDay" },
    };

    // 链条门：场景 / 门名 / 要挂的 WorldLink 资产
    private static readonly string[,] LinkAssignments =
    {
        { "FactoryDay",       "anywhere-door-FactoryNight",       "Assets/WorldLinks/FactoryDayToFactoryNight.asset" },
        { "FactoryNight",     "anywhere-door-FactoryNight-A",     "Assets/WorldLinks/FactoryNightToFactoryDay.asset" },
        { "FactoryNight",     "anywhere-door-Flooded-Grounds",    "Assets/WorldLinks/FactoryNightToFlooded_Grounds.asset" },
        { "Flooded_Grounds",  "anywhere-door-Flooded-Grounds-A",  "Assets/WorldLinks/Flooded_GroundsToFactoryNight.asset" },
    };

    /// <summary>
    /// ⚠️ 已停用（2026-10-25）：本工具是 2026-10-18 的一次性补丁，硬编码改名表 +
    /// 给链条门挂显式 WorldLink。现在门配对已改成"门名即目标世界"的拓扑推导
    /// （见 PortalWindow.TryDerivePairing / DoorLinkAutoRepair 一键配对），
    /// 显式 WorldLink 已退场，跑这个工具反而会把门改回旧命名并重新挂链。
    /// 代码保留仅作历史参考，不再出现在菜单里。
    /// </summary>
    [System.Obsolete("已被 FYP/门配对/一键配对并配齐 取代，不再使用")]
    public static void Fix()
    {
        var report = new StringBuilder();
        var opened = new Dictionary<string, Scene>();   // 本工具打开的场景：改完统一保存
        var originallyOpen = new HashSet<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            originallyOpen.Add(SceneManager.GetSceneAt(i).name);

        // ── 1) 世界侧门改名 ──
        for (int i = 0; i < Renames.GetLength(0); i++)
        {
            string sceneName = Renames[i, 0], from = Renames[i, 1], to = Renames[i, 2];
            var sc = OpenScene(sceneName, opened);
            if (!sc.IsValid()) { report.AppendLine("  [跳过] 场景未找到：" + sceneName); continue; }

            var door = FindDoor(sc, from);
            if (door == null)
            {
                report.AppendLine("  [跳过] " + sceneName + "/" + from + " 不存在（可能已改名）");
                continue;
            }
            if (FindDoor(sc, to) != null)
            {
                report.AppendLine("  [跳过] " + sceneName + " 里已存在 " + to + "，请手工确认");
                continue;
            }

            door.gameObject.name = to;
            EditorSceneManager.MarkSceneDirty(sc);
            report.AppendLine("  [改名] " + sceneName + "：" + from + " → " + to);
        }

        // ── 2) 链条门挂 WorldLink ──
        for (int i = 0; i < LinkAssignments.GetLength(0); i++)
        {
            string sceneName = LinkAssignments[i, 0], doorName = LinkAssignments[i, 1], linkPath = LinkAssignments[i, 2];
            var link = AssetDatabase.LoadAssetAtPath<WorldLink>(linkPath);
            if (link == null) { report.AppendLine("  [跳过] 找不到 WorldLink 资产：" + linkPath); continue; }

            var sc = OpenScene(sceneName, opened);
            if (!sc.IsValid()) { report.AppendLine("  [跳过] 场景未找到：" + sceneName); continue; }

            var door = FindDoor(sc, doorName);
            if (door == null) { report.AppendLine("  [跳过] " + sceneName + "/" + doorName + " 不存在"); continue; }

            var pw = door.GetComponent<PortalWindow>();
            if (pw == null) { report.AppendLine("  [跳过] " + sceneName + "/" + doorName + " 上没挂 PortalWindow"); continue; }

            var so = new SerializedObject(pw);
            var propAuto = so.FindProperty("autoPair");
            var propLink = so.FindProperty("link");
            if (propAuto != null && !propAuto.boolValue
                && propLink != null && propLink.objectReferenceValue == link)
            {
                report.AppendLine("  [跳过] " + sceneName + "/" + doorName + " 已挂 " + link.name);
                continue;
            }

            if (propAuto != null) propAuto.boolValue = false;    // 显式配对：不再走命名推导
            if (propLink != null) propLink.objectReferenceValue = link;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(sc);
            report.AppendLine("  [挂链] " + sceneName + "/" + doorName + " → " + link.name
                              + "（目标 " + link.WorldSceneName + "/" + link.WorldDoorName + "）");
        }

        // ── 3) 保存（还在手上时存），再关掉本工具打开的场景 ──
        foreach (var kv in opened)
        {
            if (!kv.Value.IsValid()) continue;
            bool ok = EditorSceneManager.SaveScene(kv.Value);
            report.AppendLine((ok ? "  [已保存] " : "  [保存失败] ") + kv.Key
                              + (kv.Value.isDirty ? "（仍有未保存改动）" : ""));
        }
        foreach (var kv in opened)
            if (!originallyOpen.Contains(kv.Key) && kv.Value.IsValid())
                EditorSceneManager.CloseScene(kv.Value, true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[门配对修复] 完成：\n" + report);
    }

    /// <summary>
    /// 只读量测：每扇门的**门板**开门后停在门根的哪一侧。
    ///
    /// 干什么用：判断"开门方向"是否符合直觉——玩家永远沿门的 +forward 方向穿行，
    /// 门板应该摆向玩家要去的世界那一侧（+forward，推门），而不是摆回玩家来的那一侧。
    /// 同一对门（世界侧主门 `door-X` ↔ 接应门 `door-X-A`）相差 mapYaw180 时，
    /// 同一个 openAngle 在各自本地系里会摆向相反的世界方向——这正是"从这边看向左开、
    /// 从对面看却向右开"的由来。
    ///
    /// 不改任何东西，只开场景读数据，读完把本工具打开的场景关掉。
    /// </summary>
    [MenuItem("FYP/门配对/量开门方向（只读）", false, 20)]
    public static void MeasureSwing()
    {
        var opened = new Dictionary<string, Scene>();
        var originallyOpen = new HashSet<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            originallyOpen.Add(SceneManager.GetSceneAt(i).name);

        var report = new StringBuilder();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(s.path);
            var sc = OpenScene(sceneName, opened);
            if (!sc.IsValid()) continue;

            foreach (var root in sc.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("anywhere-door")) continue;
                    var aw = t.GetComponentInChildren<AnywhereDoor>(true);
                    if (aw == null) continue;                       // 门根 = 带 AnywhereDoor 子节点的那层

                    Transform panel = aw.transform;                  // 门板
                    Transform par = panel.parent;
                    var mr = panel.GetComponent<Renderer>();
                    if (mr == null) { report.AppendLine("  " + sc.name + "/" + t.name + "：门板没有 Renderer"); continue; }

                    Quaternion rotOpen = par.rotation * (panel.localRotation * Quaternion.Euler(0f, 0f, aw.OpenAngle));
                    Vector3 cLocal = mr.localBounds.center;
                    Vector3 cOpen = panel.position + rotOpen * Vector3.Scale(cLocal, panel.lossyScale);
                    Vector3 cClosed = panel.TransformPoint(cLocal);
                    Vector3 lo = t.InverseTransformPoint(cOpen);      // 开态板心→门根本地系
                    Vector3 lc = t.InverseTransformPoint(cClosed);

                    Vector3 axR = rotOpen * Vector3.right, axU = rotOpen * Vector3.up, axF = rotOpen * Vector3.forward;
                    report.AppendLine("· " + sc.name + "/" + t.name + "  openAngle=" + aw.OpenAngle
                                      + "  门根 pos=" + t.position.ToString("F2") + " fwd=" + t.forward.ToString("F2"));
                    report.AppendLine("    板心本地 关=" + lc.ToString("F2") + " → 开=" + lo.ToString("F2")
                                      + "   （门根本地系：x=门右、y=上、z=门法线/穿行方向）");
                    report.AppendLine("    开启态门板本地轴→世界：x=" + axR.ToString("F2") + " y=" + axU.ToString("F2") + " z=" + axF.ToString("F2")
                                      + "   竖轴=" + (Mathf.Abs(axR.y) > 0.7f ? "本地x" : (Mathf.Abs(axU.y) > 0.7f ? "本地y" : (Mathf.Abs(axF.y) > 0.7f ? "本地z" : "?"))));
                }
            }
        }

        foreach (var kv in opened)
            if (!originallyOpen.Contains(kv.Key) && kv.Value.IsValid())
                EditorSceneManager.CloseScene(kv.Value, true);

        Debug.Log("[开门方向量测]\n" + report);
    }

    // ───────────── 小工具 ─────────────
    private static Scene OpenScene(string sceneName, Dictionary<string, Scene> opened)
    {
        Scene cached;
        if (opened.TryGetValue(sceneName, out cached) && cached.IsValid()) return cached;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name == sceneName && s.isLoaded) { opened[sceneName] = s; return s; }
        }
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            if (System.IO.Path.GetFileNameWithoutExtension(s.path) != sceneName) continue;
            var sc = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Additive);
            opened[sceneName] = sc;
            return sc;
        }
        return default(Scene);
    }

    private static Transform FindDoor(Scene sc, string name)
    {
        foreach (var root in sc.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }
}
