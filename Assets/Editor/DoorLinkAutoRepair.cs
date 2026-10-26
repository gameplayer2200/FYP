using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 门配对一键工具：**摆好门 → 起对名字 → 点一下，剩下的全自动**。
///
/// ── 命名约定（唯一的"配置"）────────────────────────────────────
/// 一对门只有两个名字，差一个 `-A` 后缀：
///   · **主门** `anywhere-door-X`   —— 放在出发侧，走过去到 X 世界；
///   · **副门** `anywhere-door-X-A` —— 放在 X 世界，走回来。
/// 其中 X 就是**目标世界的场景名**。例如：
///   · 实验室 `anywhere-door-Metro`     ↔ Metro   `anywhere-door-Metro-A`
///   · 实验室 `anywhere-door-FactoryDay` ↔ 工厂昼 `anywhere-door-FactoryDay-A`
///   · 工厂昼 `anywhere-door-FactoryNight` ↔ 工厂夜 `anywhere-door-FactoryNight-A`
/// 链式 / 环状 / 星形拓扑都只是名字，没有别的规则。
///
/// ── 它替你做什么 ────────────────────────────────────────────────
/// 1) 扫描所有游戏场景（Build Settings 里的 + `Assets/Scenes/` 下的），把门全找出来；
/// 2) 按后缀把 `X` 与 `X-A` 配成一对，校验"副门是否真在 X 世界里"；
/// 3) 把配对结果**写进门的序列化字段**（`pairKey` / `pairPartnerScene` / `pairPartnerDoor`），
///    运行时直接读，不再靠名字猜——副门光看名字推不出主门在哪个世界；
/// 4) 把新世界补进 Build Settings（不加进去 WorldStreamer 根本装载不了它）；
/// 5) 把每扇门的 PortalWindow 参数归一化（autoPair / backSlot / openingPath /
///    showDebugOsd / 取景参数 / 穿行参数 / srcFrame）——新建的门不用再手动调；
/// 6) 给新世界在 TagManager 里登记 `World_{场景名}` 图层（光影隔离用）；
/// 7) 保存所有涉及的门场景。
///
/// ── 它不做什么 ──────────────────────────────────────────────────
/// **不自动造门、不改门名、不挪门位置**。门是要卡在墙上的实体，自动摆进去十有八九
/// 穿墙或悬空，造出比"缺门"更坏的传送门；名字与位置由人决定，工具只报还缺哪一扇、
/// 该叫什么、该放在哪个世界。
///
/// 注意：本工具会**保存所有涉及的门场景**。运行前确认没有未保存的其它改动。
/// </summary>
public static class DoorLinkAutoRepair
{
    const string GameSceneDir = "Assets/Scenes";

    /// <summary>实验室侧的门预载半径。其余世界统一 15（与既有配置一致）。</summary>
    const float PreloadRadius = 15f;

    /// <summary>
    /// 提前激活距离：玩家走到离门这么近就把对面世界激活，而不是等按下 E。
    /// 激活要实例化整个场景（Metro ≈100ms、OfficePack ≈178ms），落在按键那一帧就是"按一下卡一下"。
    /// 6m 是折中值：够玩家走十几帧把卡顿摊开，又不会让挤在一起的门在实验室开局全部点亮。
    /// </summary>
    const float ActivateAheadDistance = 6f;

    private class DoorInfo
    {
        public string name;
        public string sceneName;
        public Transform t;
        public PortalWindow pw;
        public string key;              // 去掉前缀与 -A 后缀后的配对标识（= 目标世界名）
        public bool isSub;              // 名字带 -A = 副门
        public string partnerScene;     // 配对成功的另一扇门所在世界
        public string partnerDoor;      // 配对成功的另一扇门名
        public bool paired;
        public string problem;          // 阻断性问题（有值 = 这扇门用不了）
        public string warning;          // 非阻断提醒
    }

    [MenuItem("FYP/门配对/一键配对并配齐", false, 1)]
    public static void Repair()
    {
        Run(true);
    }

    [MenuItem("FYP/门配对/检查配对（只读，不改任何东西）", false, 2)]
    public static void Check()
    {
        Run(false);
    }

    // ─────────────────────────────────────────────────────────────
    // 主流程
    // ─────────────────────────────────────────────────────────────
    private static void Run(bool apply)
    {
        var report = new StringBuilder();
        report.AppendLine(apply ? "【一键配对并配齐】" : "【检查配对（只读）】");

        var openedScenes = new List<Scene>();
        string activePath = EditorSceneManager.GetActiveScene().path;
        try
        {
            // ── 1) 收集场景（Build Settings ∪ Assets/Scenes/）──
            var scenePaths = CollectScenePaths();
            report.AppendLine("候选场景 " + scenePaths.Count + " 个。");

            // ── 2) 补 Build Settings ──
            var missingBuild = new List<string>();
            foreach (var p in scenePaths)
                if (!InBuildSettings(p)) missingBuild.Add(p);

            if (missingBuild.Count > 0)
            {
                if (apply)
                {
                    AddToBuildSettings(missingBuild);
                    report.AppendLine("已补进 Build Settings：" + string.Join("、", NamesOf(missingBuild)));
                }
                else
                {
                    report.AppendLine("[待办] 未进 Build Settings（WorldStreamer 装载不了）："
                                      + string.Join("、", NamesOf(missingBuild)));
                }
            }

            // ── 3) 逐场景开门收集 ──
            var doors = new List<DoorInfo>();
            foreach (var path in scenePaths)
            {
                Scene sc;
                try { sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                catch (Exception e)
                {
                    report.AppendLine("[跳过] 打不开 " + path + "：" + e.Message);
                    continue;
                }
                if (!sc.IsValid()) { report.AppendLine("[跳过] 场景无效 " + path); continue; }
                if (!openedScenes.Contains(sc)) openedScenes.Add(sc);

                int found = 0;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!t.name.StartsWith(PortalWindow.DoorNamePrefix, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var d = new DoorInfo { name = t.name, sceneName = sc.name, t = t };
                        d.pw = t.GetComponent<PortalWindow>();
                        if (d.pw == null)
                        {
                            // 只有门板/门框没有 PortalWindow：多半是误放了 prefab 的零件，不算门
                            if (t.GetComponentInChildren<AnywhereDoor>(true) == null) continue;
                            d.problem = "缺 PortalWindow 组件";
                        }
                        doors.Add(d);
                        found++;
                    }
                report.AppendLine("  " + sc.name + "：" + found + " 扇");
            }

            // 让推导认得本次扫描到的全部世界（含还没进 Build Settings 的新世界）。
            // 只读模式下工具不会写 Build Settings，若不在这里登记，新世界会被误判成
            // "进不来"，报告就与实际执行结果不一致。
            PortalWindow.InvalidateSceneCache();
            PortalWindow.RegisterKnownScenes(NamesOf(scenePaths));

            // ── 4) 按后缀分组配对 ──
            // key = 门名去掉前缀与可选的 -A。同一 key 下，不带 -A 的是主门、带 -A 的是副门。
            var groups = new Dictionary<string, List<DoorInfo>>();
            foreach (var d in doors)
            {
                if (d.problem != null) continue;
                string key = d.name.Substring(PortalWindow.DoorNamePrefix.Length);
                d.isSub = key.EndsWith("-A", StringComparison.OrdinalIgnoreCase);
                if (d.isSub) key = key.Substring(0, key.Length - 2);
                d.key = key;
                if (!groups.ContainsKey(key)) groups.Add(key, new List<DoorInfo>());
                groups[key].Add(d);
            }

            // 逐个 key 判定
            var keys = new List<string>(groups.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                var list = groups[key];
                DoorInfo main = null, sub = null;
                foreach (var d in list)
                {
                    if (d.isSub) { if (sub == null) sub = d; else d.problem = "同一对里有多个副门（重名）"; }
                    else { if (main == null) main = d; else d.problem = "同一对里有多个主门（重名）"; }
                }

                // 目标世界必须是真实场景，否则运行时装载不了
                bool keyIsScene = PortalWindow.ResolveSceneNamePublic(key) != null;

                if (main == null && sub != null)
                {
                    sub.problem = "只有副门，缺主门。在出发侧任一世界里放一扇名为「"
                                  + PortalWindow.DoorNamePrefix + key + "」的门即可配对";
                    continue;
                }
                if (main != null && sub == null)
                {
                    main.problem = "只有主门，缺副门。在「" + key + "」世界里放一扇名为「"
                                   + PortalWindow.DoorNamePrefix + key + "-A」的门即可配对";
                    continue;
                }
                if (main == null || sub == null) continue;

                if (!keyIsScene)
                {
                    main.problem = sub.problem = "配对标识「" + key + "」不是已登记的场景名，门名拼错或世界没进 Build Settings";
                    continue;
                }
                if (string.Equals(main.sceneName, key, StringComparison.Ordinal))
                {
                    main.problem = "主门放在了目标世界「" + key + "」自己里。主门应在**出发侧**（别的世界），副门才在 " + key;
                    // 副门这时也是配不上的，必须一并说明，否则它在报告里会静默消失
                    sub.problem = "配对失败：本对的另一扇「" + main.sceneName + "/" + main.name
                                  + "」是主门却放在了 " + key + " 自己里。把两扇门的名字对调即可"
                                  + "（" + main.sceneName + " 里这扇改成「" + PortalWindow.DoorNamePrefix + key + "-A"
                                  + "」，" + sub.sceneName + " 里那扇改成「" + PortalWindow.DoorNamePrefix + key + "」）";
                    continue;
                }
                if (!string.Equals(sub.sceneName, key, StringComparison.Ordinal))
                {
                    // 不算致命：门可能被人挪过。但要提醒，因为副门放错世界时，"走回去"就落到别处了。
                    sub.warning = "副门不在「" + key + "」世界里（当前在「" + sub.sceneName + "」）。"
                                  + "按约定副门应放在它所属的 " + key;
                }

                // ── 配对成功 ──
                main.paired = true; main.partnerScene = sub.sceneName; main.partnerDoor = sub.name;
                sub.paired = true; sub.partnerScene = main.sceneName; sub.partnerDoor = main.name;
            }

            // ── 5) 报告拓扑 ──
            report.AppendLine();
            report.AppendLine("── 配对结果 ──");
            int okCount = 0, badCount = 0;
            foreach (var key in keys)
            {
                var list = groups[key];
                // 同一对的门排在一起：主门在前
                foreach (var d in list)
                {
                    string head = d.sceneName + "/" + d.name;
                    if (d.problem != null)
                    {
                        badCount++;
                        report.AppendLine("  ✗ " + head + "  —— " + d.problem);
                        continue;
                    }
                    if (!d.paired) continue;
                    okCount++;
                    report.AppendLine("  ✓ " + head.PadRight(46) + " ↔  " + d.partnerScene + "/" + d.partnerDoor);
                    if (d.warning != null) report.AppendLine("      ⚠ " + d.warning);
                }
            }
            report.AppendLine("  合计：" + okCount + " 扇已配对，" + badCount + " 扇有问题。");

            // ── 6) 应用 ──
            if (apply)
            {
                int touched = 0, pairedWritten = 0;
                foreach (var d in doors)
                {
                    if (d.pw == null) continue;

                    // 配对结果写进序列化字段。这一步是**关键**：
                    // 副门 `X-A` 光看名字只能推回 `X`，推不出主门在哪个世界——
                    // 那需要全工程的门分布，运行时没有。工具在这里把答案写死。
                    //
                    // 注意：这里**不能**用 problem 做提前跳过。一扇门可能上一轮还配对成功、
                    // 这轮因为对面被挪走/改名而变成单向——它身上还挂着指向旧世界的显式配对，
                    // 不清掉的话运行时照样会去连那个已经不存在的门（OfficePack 就踩过这个坑）。
                    string wantScene = d.paired ? d.partnerScene : null;
                    string wantDoor = d.paired ? d.partnerDoor : null;
                    if (!d.pw.ExplicitPairingIs(d.paired ? d.name : null, wantScene, wantDoor))
                    {
                        d.pw.SetExplicitPairing(d.paired ? d.name : null, wantScene, wantDoor);
                        // 必须 SetDirty(pw)：只 MarkSceneDirty 时，组件上的字段改动不会被序列化，
                        // 存完盘再打开就变回空字符串（实测踩过）。
                        EditorUtility.SetDirty(d.pw);
                        pairedWritten++;
                    }

                    // 参数归一化只对配得上的门做——配不上的门多半位置/方向也不对，
                    // 刷参数只会掩盖真正要人去处理的那件事。
                    if (d.problem == null && NormalizePortalWindow(d)) touched++;
                }
                report.AppendLine();
                report.AppendLine("已写入显式配对 " + pairedWritten + " 扇；已归一化 " + touched + " 扇门的 PortalWindow 参数。");

                int layers = EnsureWorldLayers(scenePaths, report);
                if (layers > 0) report.AppendLine("已登记 " + layers + " 个世界图层。");

                // 保存（还在手上时存），再关掉本工具打开的场景
                foreach (var sc in openedScenes)
                {
                    if (!sc.IsValid() || !sc.isDirty) continue;
                    bool ok = EditorSceneManager.SaveScene(sc);
                    report.AppendLine((ok ? "  [已保存] " : "  [保存失败] ") + sc.name);
                }

                AssetDatabase.SaveAssets();
            }
        }
        finally
        {
            // 关掉本工具打开、而用户原本没开的场景；恢复用户原本的活动场景
            foreach (var sc in openedScenes)
            {
                if (sc.IsValid() && sc.isLoaded) EditorSceneManager.CloseScene(sc, true);
            }
            if (!string.IsNullOrEmpty(activePath) && File.Exists(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }

        Debug.Log("[门配对] " + report);
    }

    // ─────────────────────────────────────────────────────────────
    // PortalWindow 参数归一化：新建的门不该再手动调这些
    // ─────────────────────────────────────────────────────────────
    private static bool NormalizePortalWindow(DoorInfo d)
    {
        var pw = d.pw;
        var so = new SerializedObject(pw);
        bool changed = false;

        changed |= SetBool(so, "autoPair", true);
        changed |= SetObject(so, "link", null);
        changed |= SetObject(so, "srcFrame", d.t);
        changed |= SetString(so, "openingPath", "Cube.001/Plane");
        changed |= SetInt(so, "backSlot", 0);
        changed |= SetBool(so, "showDebugOsd", false);
        changed |= SetInt(so, "rtHeight", 720);
        changed |= SetFloat(so, "farClip", 200f);
        changed |= SetFloat(so, "nearClip", 0.05f);
        changed |= SetFloat(so, "preloadRadius", PreloadRadius);
        changed |= SetFloat(so, "activateAheadDistance", ActivateAheadDistance);
        changed |= SetBool(so, "mirrorU", false);
        changed |= SetBool(so, "dynamicResolution", true);
        changed |= SetInt(so, "maxRtHeight", 1440);
        changed |= SetFloat(so, "maxRecede", 3f);
        changed |= SetFloat(so, "crossCooldown", 0.6f);
        changed |= SetFloat(so, "crossPlaneTolerance", 1.5f);
        changed |= SetFloat(so, "crossPlaneLead", 0.25f);

        if (!changed) return false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pw);
        if (d.t != null && d.t.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(d.t.gameObject.scene);
        return true;
    }

    // 只在值真的不同时才写，避免把干净场景无谓标脏
    private static bool SetBool(SerializedObject so, string n, bool v)
    {
        var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.Boolean) return false;
        if (p.boolValue == v) return false; p.boolValue = v; return true;
    }
    private static bool SetInt(SerializedObject so, string n, int v)
    {
        var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.Integer) return false;
        if (p.intValue == v) return false; p.intValue = v; return true;
    }
    private static bool SetFloat(SerializedObject so, string n, float v)
    {
        var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.Float) return false;
        if (Mathf.Approximately(p.floatValue, v)) return false; p.floatValue = v; return true;
    }
    private static bool SetString(SerializedObject so, string n, string v)
    {
        var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.String) return false;
        if (p.stringValue == v) return false; p.stringValue = v; return true;
    }
    private static bool SetObject(SerializedObject so, string n, UnityEngine.Object v)
    {
        var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) return false;
        if (p.objectReferenceValue == v) return false; p.objectReferenceValue = v; return true;
    }

    // ─────────────────────────────────────────────────────────────
    // 世界图层：光影隔离按 `World_{场景名}` 找，缺了就动态分配，
    // 但登记过的名字优先（否则会和 TagManager 里已有编号打架）
    // ─────────────────────────────────────────────────────────────
    private static int EnsureWorldLayers(List<string> scenePaths, StringBuilder report)
    {
        int added = 0;
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");

        foreach (var path in scenePaths)
        {
            string sceneName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(sceneName)) continue;
            // 起始世界不参与光影隔离（它的灯一直都是"照所有可见世界之外"的那一份），
            // 给它登记世界图层只会白占一个槽位。
            if (string.Equals(sceneName, PortalWindow.HomeSceneName, StringComparison.Ordinal)) continue;
            string layerName = "World_" + sceneName;
            if (LayerMask.NameToLayer(layerName) >= 0) continue;   // 已登记

            bool placed = false;
            for (int i = 8; i < layers.arraySize; i++)            // 0~7 是 Unity 内置，不碰
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(el.stringValue)) continue;
                el.stringValue = layerName;
                placed = true;
                added++;
                report.AppendLine("  [图层] " + i + " → " + layerName);
                break;
            }
            if (!placed) report.AppendLine("  [图层] 32 个图层已满，无法登记 " + layerName);
        }
        if (added > 0) tagManager.ApplyModifiedPropertiesWithoutUndo();
        return added;
    }

    // ─────────────────────────────────────────────────────────────
    // 场景收集
    // ─────────────────────────────────────────────────────────────
    private static List<string> CollectScenePaths()
    {
        var result = new List<string>();
        // 已登记但**没勾选**的场景要记下来：它们是"用户明确不参与游戏的世界"，
        // 下面扫目录补新世界时必须跳过，否则工具会把 Shrine 这类临时下线的
        // 大世界重新当成"候选世界"报进配对报告，甚至重新勾回 Build Settings。
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (string.IsNullOrEmpty(s.path)) continue;
            if (!s.enabled) { disabled.Add(s.path); continue; }
            if (!File.Exists(s.path)) continue;
            if (!result.Contains(s.path)) result.Add(s.path);
        }
        if (Directory.Exists(GameSceneDir))
        {
            foreach (var p in Directory.GetFiles(GameSceneDir, "*.unity", SearchOption.AllDirectories))
            {
                string norm = p.Replace('\\', '/');
                if (disabled.Contains(norm)) continue;   // 用户主动下线的世界，不参与配对
                if (!result.Contains(norm)) result.Add(norm);
            }
        }
        return result;
    }

    private static bool InBuildSettings(string path)
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void AddToBuildSettings(List<string> paths)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var p in paths)
        {
            bool exists = false;
            foreach (var s in list) if (string.Equals(s.path, p, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
            if (!exists) list.Add(new EditorBuildSettingsScene(p, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }

    private static List<string> NamesOf(List<string> paths)
    {
        var names = new List<string>();
        foreach (var p in paths) names.Add(Path.GetFileNameWithoutExtension(p));
        return names;
    }
}
