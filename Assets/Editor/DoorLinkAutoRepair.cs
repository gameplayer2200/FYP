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
/// ── 它替你做什么 ────────────────────────────────────────────────
/// 1) 扫描所有游戏场景（Build Settings 里的 + `Assets/Scenes/` 下的），把门全找出来；
/// 2) 按命名约定推导「目标世界 + 对面那扇门」，并交叉验证两边是否互相指认；
/// 3) 把新世界补进 Build Settings（不加进去 WorldStreamer 根本装载不了它）；
/// 4) 把每扇门的 PortalWindow 参数归一化（autoPair / backSlot / openingPath /
///    showDebugOsd / 取景参数 / 穿行参数 / srcFrame）——新建的门不用再手动调；
/// 5) 给新世界在 TagManager 里登记 `World_{场景名}` 图层（光影隔离用）；
/// 6) 保存所有涉及的门场景。
///
/// ── 命名约定（唯一的"配置"）────────────────────────────────────
/// **门名里的世界 = 这扇门通向的世界**：
///   · 工厂昼里的 `anywhere-door-FactoryNight`  →  通往工厂夜；
///   · 工厂夜里的 `anywhere-door-FactoryDay`    →  通往工厂昼；
///   · Metro 里的 `anywhere-door-Shrine_game`   →  通往 Shrine_game。
/// 对面那扇门 = 目标世界里名为 `anywhere-door-{本门所在场景名}` 的门。两边天然互指。
///
/// 旧写法仍然有效（星形拓扑，实验室侧）：`anywhere-door-X-A` = 通往 X 的外侧门，
/// 世界 X 里那扇 `anywhere-door-X` = 回实验室。判据在
/// <see cref="PortalWindow.TryDerivePairing"/>，工具与运行时共用，不会漂移。
///
/// ── 为什么不再需要 WorldLink ───────────────────────────────────
/// 旧方案里"世界之间连接"无法用命名表达，只能手写 WorldLink 资产并关掉 autoPair。
/// 新约定下任意拓扑（链式 / 环状 / 星形）都只是名字，因此本工具把 autoPair 全部打开、
/// 清掉 link 引用。`Assets/WorldLinks/` 里的资产保留作历史与参数参考，运行时不再读取。
///
/// 注意：本工具会**保存所有涉及的门场景**。运行前确认没有未保存的其它改动。
/// </summary>
public static class DoorLinkAutoRepair
{
    const string WorldLinkDir = "Assets/WorldLinks";
    const string OpeningPath = "Cube.001/Plane";
    const string GameSceneDir = "Assets/Scenes";

    /// <summary>实验室侧的门预载半径。其余世界统一 15（与既有配置一致）。</summary>
    const float PreloadRadius = 15f;

    private class DoorInfo
    {
        public string name;
        public string sceneName;
        public Transform t;
        public PortalWindow pw;
        public string targetScene;      // 推导出的目标世界
        public string partnerDoor;      // 推导出的对面门名
        public string renameTo;         // 迁移阶段算出的新名字（apply 时落到场景）
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

        string activePath = EditorSceneManager.GetActiveScene().path;
        var originallyOpen = new HashSet<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            originallyOpen.Add(SceneManager.GetSceneAt(i).path);

        var openedScenes = new List<Scene>();
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

            // ── 4) 推导 + 交叉验证 ──
            // 让推导认得本次扫描到的全部世界（含还没进 Build Settings 的新世界）。
            // 只读模式下工具不会写 Build Settings，若不在这里登记，新世界的门会被
            // 误判成"退回实验室"，报告就与实际执行结果不一致。
            PortalWindow.InvalidateSceneCache();
            PortalWindow.RegisterKnownScenes(NamesOf(scenePaths));

            // 先做命名迁移：旧门挂着显式 WorldLink（autoPair=false），link 里记着作者的真实意图，
            // 把它翻译成新约定的门名，之后就能完全靠名字跑，不再需要 link。
            // 迁移结果在这里就落到门上（只读模式只落内存），后面的索引与推导都按新名字走。
            int renamed = MigrateNamesFromLinks(doors, report);
            ApplyPendingRenames(doors, apply);
            if (renamed > 0)
                report.AppendLine(apply
                    ? "  已改名 " + renamed + " 扇（旧 WorldLink 意图 → 新命名约定）"
                    : "  若执行，将改名 " + renamed + " 扇（旧 WorldLink 意图 → 新命名约定）");

            // 再纠一次名：手工摆门常把名字写成 `anywhere-door-Shrine_gameA`（少个连字符），
            // 这种名字推导不出来会静默退回"回实验室"，表现就是"门放了却没配对"。
            int fixedNames = FixMisnamedDoors(doors, apply, report);
            ApplyPendingRenames(doors, apply);
            if (fixedNames > 0)
                report.AppendLine(apply
                    ? "  已纠正 " + fixedNames + " 扇门的名字"
                    : "  若执行，将纠正 " + fixedNames + " 扇门的名字");

            // 再纠自指门：`世界 X 里的 anywhere-door-X` 在新约定下指回自己，是无意义的
            // （复制预制件忘了改名的典型产物）。若恰好只有一个世界想连到 X，那它就是回程门。
            int fixedSelf = FixSelfReferenceDoors(doors, apply, report);
            ApplyPendingRenames(doors, apply);
            if (fixedSelf > 0)
                report.AppendLine(apply
                    ? "  已修正 " + fixedSelf + " 扇自指门"
                    : "  若执行，将修正 " + fixedSelf + " 扇自指门");

            var byKey = new Dictionary<string, DoorInfo>();   // "场景/门名" → 门
            var byScene = new Dictionary<string, List<DoorInfo>>();
            foreach (var d in doors)
            {
                string k = d.sceneName + "/" + d.name;
                if (byKey.ContainsKey(k))
                {
                    d.problem = "同一场景里有重名门";
                }
                else byKey[k] = d;

                if (!byScene.ContainsKey(d.sceneName)) byScene[d.sceneName] = new List<DoorInfo>();
                byScene[d.sceneName].Add(d);
            }

            foreach (var d in doors)
            {
                string scene, partner;
                if (!PortalWindow.TryDerivePairing(d.name, d.sceneName, out scene, out partner))
                {
                    d.problem = d.problem ?? "门名不以 " + PortalWindow.DoorNamePrefix + " 开头";
                    continue;
                }
                d.targetScene = scene;
                d.partnerDoor = partner;
                if (d.problem != null) continue;

                // 自配对：目标就是自己所在的世界（穿过去会掉在门背面/墙里）
                if (string.Equals(scene, d.sceneName, StringComparison.Ordinal))
                {
                    d.problem = "推导出的目标世界就是本门所在世界（自配对）";
                    continue;
                }

                // 目标世界里有没有那扇门。判定必须和运行时一致：运行时用的是
                // PortalWindow.DerivePartnerDoorCandidates 的候选名列表 + FindDoor 的三级匹配，
                // 所以这里也用同一套候选名 + 同一套宽松比较，否则会出现"工具说找不到、
                // 运行时其实连得上"的假报警。
                var candidates = PortalWindow.DerivePartnerDoorCandidates(d.name, d.sceneName);
                DoorInfo p = FindByCandidates(scene, candidates, byScene);
                if (p == null)
                {
                    d.problem = "目标世界「" + scene + "」里找不到配对门「" + partner + "」"
                                + "（已试 " + string.Join(" / ", candidates.ToArray()) + "）";
                    continue;
                }

                // 报告里写"实际会连上的那扇"，而不是理想名——玩家看到的是行为，不是约定。
                d.targetScene = p.sceneName;
                d.partnerDoor = p.name;

                // 交叉验证：对面那扇门是否也真的指回我。
                // 判据必须是"从对面推导 + 在对面目标世界里找到实际那扇门 + 那扇门就是我"，
                // 只比字符串会误判：`anywhere-door-Metro` 这类靠兜底回实验室的门，
                // 推导出的名字与对面门的实际名字本来就不一样，但拓扑是完全对称的。
                if (!ResolvesBackTo(p, d, byScene))
                {
                    string bs, bd;
                    PortalWindow.TryDerivePairing(p.name, p.sceneName, out bs, out bd);
                    d.warning = (d.warning == null ? "" : d.warning + "；")
                                + "对面「" + p.sceneName + "/" + p.name + "」指向 "
                                + bs + "/" + bd + "，不是本门（单向）";
                }
            }

            // ── 5) 报告拓扑 ──
            report.AppendLine();
            report.AppendLine("── 配对结果 ──");
            int okCount = 0, badCount = 0;
            foreach (var d in doors)
            {
                string head = d.sceneName + "/" + d.name;
                if (d.problem != null)
                {
                    badCount++;
                    report.AppendLine("  ✗ " + head + "  —— " + d.problem);
                    continue;
                }
                okCount++;
                report.AppendLine("  ✓ " + head + "  →  " + d.targetScene + "/" + d.partnerDoor);
                if (d.warning != null) report.AppendLine("      ⚠ " + d.warning);
            }
            report.AppendLine("  合计：" + okCount + " 扇可用，" + badCount + " 扇有问题。");

            // ── 6) 应用 ──
            if (apply)
            {
                int touched = 0;
                foreach (var d in doors)
                {
                    if (d.problem != null || d.pw == null) continue;
                    if (NormalizePortalWindow(d)) touched++;
                }
                report.AppendLine();
                report.AppendLine("已归一化 " + touched + " 扇门的 PortalWindow 参数。");

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

            // 未使用的 WorldLink 提示
            int linkCount = AssetDatabase.FindAssets("t:WorldLink").Length;
            if (linkCount > 0)
                report.AppendLine("注：" + WorldLinkDir + " 下有 " + linkCount
                                  + " 条 WorldLink 资产，命名推导已覆盖全部配对，运行时不再读取（保留作参数参考）。");
        }
        finally
        {
            // 关掉本工具打开、且原本没开着的场景
            foreach (var sc in openedScenes)
            {
                if (!sc.IsValid()) continue;
                if (originallyOpen.Contains(sc.path)) continue;
                EditorSceneManager.CloseScene(sc, true);
            }
            // 把用户原本的活动场景放回前台
            if (!string.IsNullOrEmpty(activePath))
            {
                var cur = SceneManager.GetActiveScene();
                if (cur.path != activePath)
                {
                    var s = SceneManager.GetSceneByPath(activePath);
                    if (s.IsValid()) EditorSceneManager.SetActiveScene(s);
                }
            }
        }

        if (apply) AssetDatabase.Refresh();
        Debug.Log("[门配对] " + report);
    }

    // ─────────────────────────────────────────────────────────────
    // 名字纠错：把"差一点就对"的门名改成标准写法
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 手工摆门最容易出的错是名字少个连字符（`anywhere-door-Shrine_gameA` 而不是
    /// `anywhere-door-Shrine_game`），结果运行时推导不出来、退回"回实验室"，
    /// 表现就是"门放了但没配对"。这里做一次宽容匹配：把尾部像 `-A` / `A` 的残留剥掉
    /// 再认一次世界名，认得出就给出标准门名。
    ///
    /// 只在**推导失败**或**退回兜底**时才纠错，已经能正常解析的门绝不改名。
    /// </summary>
    private static int FixMisnamedDoors(List<DoorInfo> doors, bool apply, StringBuilder report)
    {
        int count = 0;
        foreach (var d in doors)
        {
            if (d.pw == null || d.problem != null || d.renameTo != null) continue;
            if (!d.name.StartsWith(PortalWindow.DoorNamePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            string key = d.name.Substring(PortalWindow.DoorNamePrefix.Length);
            if (string.IsNullOrEmpty(key)) continue;

            // 已经能正确解析的门不动
            string scene, partner;
            if (PortalWindow.TryDerivePairing(d.name, d.sceneName, out scene, out partner)
                && scene != PortalWindow.HomeSceneName) continue;

            string guess = GuessScene(key, d.sceneName);
            if (guess == null) continue;

            string want = PortalWindow.DoorNamePrefix + guess;
            d.renameTo = want;
            report.AppendLine("  [改名] " + d.sceneName + "/" + d.name + "  →  " + want
                              + "（名字少连字符/后缀残留）");
            count++;
        }
        return count;
    }

    /// <summary>把门名里的 key 宽容地认成一个世界名（剥掉 `-A` / `A` / `_A` 残留后重试）。</summary>
    private static string GuessScene(string key, string ownScene)
    {
        var tries = new List<string> { key, key.Replace('-', '_') };
        foreach (var t in new List<string>(tries))
        {
            if (t.EndsWith("-A", StringComparison.OrdinalIgnoreCase)) tries.Add(t.Substring(0, t.Length - 2));
            if (t.EndsWith("_A", StringComparison.OrdinalIgnoreCase)) tries.Add(t.Substring(0, t.Length - 2));
            if (t.EndsWith("A", StringComparison.OrdinalIgnoreCase)) tries.Add(t.Substring(0, t.Length - 1));
        }
        foreach (var t in tries)
        {
            if (string.IsNullOrEmpty(t) || t == ownScene) continue;
            // 用公开接口间接验证：拼成门名后交给 TryDerivePairing，目标世界不是兜底就算认出来了
            string scene, partner;
            string probe = PortalWindow.DoorNamePrefix + t;
            if (PortalWindow.TryDerivePairing(probe, ownScene, out scene, out partner)
                && scene != PortalWindow.HomeSceneName
                && !string.Equals(scene, ownScene, StringComparison.Ordinal))
                return scene;
        }
        return null;
    }

    /// <summary>
    /// 自指门纠错：`世界 X 里的 anywhere-door-X` 在新约定下等于"指回自己"，
    /// 推导会退回"回实验室"再去找 `anywhere-door-X-A`——找不到就是死门。
    /// 这种门几乎都是复制预制件后忘了改名的产物（用户想要的是"从 X 回某个地方"）。
    ///
    /// 判据（必须同时成立才改，宁可不动也不要猜错拓扑）：
    ///   ① 名字解析出来就是自己所在的世界（自指）；
    ///   ② 按兜底推导出的配对门在目标世界里**确实不存在**（否则它其实能正常工作，
    ///      例如 `Metro/anywhere-door-Metro` 就靠兜底正确指回实验室，绝不能动）；
    ///   ③ 恰好只有一个别的世界 W 摆了一扇 `anywhere-door-X`（即 W 想连到 X），
    ///      那么 X 里这扇门唯一合理的名字就是 `anywhere-door-W`（回程门）。
    /// 多于一扇就交给人工判断。
    /// </summary>
    private static int FixSelfReferenceDoors(List<DoorInfo> doors, bool apply, StringBuilder report)
    {
        var exists = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in doors)
            if (d.problem == null) exists.Add(d.sceneName + "/" + d.name);

        // 先把"谁想连到谁"统计出来：目标世界 → 想连它的世界集合
        var wants = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var d in doors)
        {
            if (d.problem != null || d.renameTo != null) continue;
            if (!d.name.StartsWith(PortalWindow.DoorNamePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            string scene, partner;
            if (!PortalWindow.TryDerivePairing(d.name, d.sceneName, out scene, out partner)) continue;
            if (string.Equals(scene, PortalWindow.HomeSceneName, StringComparison.Ordinal)) continue;  // 兜底不算
            if (!wants.ContainsKey(scene)) wants[scene] = new List<string>();
            if (!wants[scene].Contains(d.sceneName)) wants[scene].Add(d.sceneName);
        }

        int count = 0;
        foreach (var d in doors)
        {
            if (d.pw == null || d.problem != null || d.renameTo != null) continue;
            if (!d.name.StartsWith(PortalWindow.DoorNamePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            string key = d.name.Substring(PortalWindow.DoorNamePrefix.Length);

            // ① 自指判定
            string underscored = key.Replace('-', '_');
            if (!string.Equals(key, d.sceneName, StringComparison.Ordinal)
                && !string.Equals(underscored, d.sceneName, StringComparison.Ordinal)) continue;

            // ② 兜底推导出的配对门不存在，才说明它真是死门
            string scene, partner;
            if (!PortalWindow.TryDerivePairing(d.name, d.sceneName, out scene, out partner)) continue;
            if (exists.Contains(scene + "/" + partner)) continue;

            // ③ 唯一候选回程世界
            List<string> from;
            if (!wants.TryGetValue(d.sceneName, out from) || from.Count != 1) continue;
            string back = from[0];
            if (string.Equals(back, d.sceneName, StringComparison.Ordinal)) continue;

            string want = PortalWindow.DoorNamePrefix + back;
            d.renameTo = want;
            report.AppendLine("  [改名] " + d.sceneName + "/" + d.name + "  →  " + want
                              + "（原名指回自己；" + back + " 想连到这里，改为回程门）");
            count++;
        }
        return count;
    }

    // ─────────────────────────────────────────────────────────────
    // 旧门迁移：把 WorldLink 里记录的意图翻译成新约定的门名
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 旧门（`autoPair=false` + 显式 WorldLink）里，link 记着作者真实想连的世界与门名。
    /// 新约定用名字表达同一件事，所以把 link 的意图"反写"成门名，之后这些门就能
    /// 完全靠命名推导跑起来，不再依赖资产引用。
    ///
    /// 迁移规则：本门所在场景 S、link 指向 `T/D`（目标世界 T、目标门名 D），则：
    ///   · 本门改名为 `anywhere-door-{T}`（名字 = 它通向的世界）；
    ///   · 对面那扇 `T/D` 应改名为 `anywhere-door-{S}`，这样两边互相指认。
    /// 只改**名字不合新约定**的门，已经正确的门绝不动，避免把用户手工起的名字冲掉。
    ///
    /// 这里只把目标名字记进 `renameTo`，真正的改名在
    /// <see cref="ApplyPendingRenames"/> 统一做——因为只读模式不能动场景，
    /// 但推导又必须按"改名之后"的名字跑，否则报告与实际执行结果不一致。
    /// </summary>
    private static int MigrateNamesFromLinks(List<DoorInfo> doors, StringBuilder report)
    {
        int count = 0;
        var said = new HashSet<string>();
        foreach (var d in doors)
        {
            if (d.pw == null || d.problem != null) continue;
            var so = new SerializedObject(d.pw);
            bool auto = so.FindProperty("autoPair").boolValue;
            var linkProp = so.FindProperty("link");
            var link = linkProp.objectReferenceValue as WorldLink;
            if (auto || link == null) continue;                     // 命名模式的门无需迁移

            string targetScene = link.WorldSceneName;
            string targetDoor = link.WorldDoorName;
            if (string.IsNullOrEmpty(targetScene) || string.IsNullOrEmpty(targetDoor)) continue;

            string wantSelf = PortalWindow.DoorNamePrefix + targetScene;    // 名字 = 它通向的世界
            string wantPartner = PortalWindow.DoorNamePrefix + d.sceneName; // 对面 = 我所在的世界

            if (!string.Equals(d.name, wantSelf, StringComparison.Ordinal))
            {
                d.renameTo = wantSelf;
                if (said.Add(d.sceneName + "/" + d.name))
                    report.AppendLine("  [迁移] " + d.sceneName + "/" + d.name + "  →  " + wantSelf
                                      + "（WorldLink " + link.name + "）");
                count++;
            }

            // 对面那扇门如果在本次扫描里，也顺手改名（它可能同样挂着 autoPair=false）
            foreach (var p in doors)
            {
                if (p == d || p.problem != null) continue;
                if (!string.Equals(p.sceneName, targetScene, StringComparison.Ordinal)) continue;
                if (!string.Equals(p.name, targetDoor, StringComparison.Ordinal)) continue;
                if (string.Equals(p.name, wantPartner, StringComparison.Ordinal)) continue;
                p.renameTo = wantPartner;
                if (said.Add(p.sceneName + "/" + p.name))
                    report.AppendLine("  [迁移] " + p.sceneName + "/" + p.name + "  →  " + wantPartner);
                count++;
                break;
            }
        }
        return count;
    }

    /// <summary>
    /// 把迁移算出的名字落到门上：`apply` 时真改场景，只读时只在内存里改，
    /// 这样后续推导（byKey 索引、配对查找、交叉验证）看到的都是**改名后**的名字，
    /// 只读报告与真正执行的结果才不会各说一套。
    /// </summary>
    private static void ApplyPendingRenames(List<DoorInfo> doors, bool apply)
    {
        foreach (var d in doors)
        {
            if (d.renameTo == null) continue;
            if (apply) RenameDoor(d, d.renameTo);
            else d.name = d.renameTo;
            d.renameTo = null;
        }
    }

    /// <summary>
    /// 改名并同步场景里的引用。改 GameObject 名字不会断任何序列化引用
    /// （引用按 instanceID/GUID 走，不按名字），所以这里只需改名字 + 标脏。
    /// </summary>
    private static void RenameDoor(DoorInfo d, string newName)
    {
        d.t.gameObject.name = newName;
        d.name = newName;
        EditorUtility.SetDirty(d.t.gameObject);
        EditorSceneManager.MarkSceneDirty(d.t.gameObject.scene);
    }

    // ─────────────────────────────────────────────────────────────
    // 参数归一化
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 把一扇门的 PortalWindow 参数刷成项目标准值。
    /// 目的：从基座 prefab 新建的门带着调试默认（showDebugOsd=1）、
    /// 或者从别的门复制来的门带着旧 link 引用 —— 这些都会让"看起来配好了其实没配好"。
    /// 返回是否真的改了东西。
    /// </summary>
    private static bool NormalizePortalWindow(DoorInfo d)
    {
        var so = new SerializedObject(d.pw);
        bool changed = false;

        changed |= SetBool(so, "autoPair", true);
        changed |= SetObject(so, "link", null);            // 命名推导已覆盖，清掉旧引用防误导
        changed |= SetObject(so, "srcFrame", d.t);         // 门根 = 自身（留空时 Awake 也会兜底）
        changed |= SetString(so, "openingPath", OpeningPath);
        changed |= SetInt(so, "backSlot", 0);              // 槽 0 = 门洞内板；填 1 会让门框消失
        changed |= SetBool(so, "showDebugOsd", false);     // 基座 prefab 里是 1，定稿必须关
        changed |= SetInt(so, "rtHeight", 720);
        changed |= SetFloat(so, "farClip", 200f);
        changed |= SetFloat(so, "nearClip", 0.05f);
        changed |= SetFloat(so, "preloadRadius", PreloadRadius);
        changed |= SetBool(so, "mirrorU", false);
        changed |= SetBool(so, "dynamicResolution", true);
        changed |= SetInt(so, "maxRtHeight", 1440);
        changed |= SetFloat(so, "maxRecede", 3f);
        changed |= SetFloat(so, "crossCooldown", 0.6f);
        changed |= SetFloat(so, "crossPlaneTolerance", 1.5f);
        changed |= SetFloat(so, "crossPlaneLead", 0.25f);

        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(d.pw);
            EditorSceneManager.MarkSceneDirty(d.pw.gameObject.scene);
        }
        return changed;
    }

    // 只在值确实不同时才写，避免把场景无谓标脏
    private static bool SetBool(SerializedObject so, string n, bool v)
    { var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.Boolean || p.boolValue == v) return false; p.boolValue = v; return true; }
    private static bool SetInt(SerializedObject so, string n, int v)
    { var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.Integer || p.intValue == v) return false; p.intValue = v; return true; }
    private static bool SetFloat(SerializedObject so, string n, float v)
    { var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.Float || Mathf.Approximately(p.floatValue, v)) return false; p.floatValue = v; return true; }
    private static bool SetString(SerializedObject so, string n, string v)
    { var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.String || p.stringValue == v) return false; p.stringValue = v; return true; }
    private static bool SetObject(SerializedObject so, string n, UnityEngine.Object v)
    { var p = so.FindProperty(n); if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == v) return false; p.objectReferenceValue = v; return true; }

    // ─────────────────────────────────────────────────────────────
    // 世界图层
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 给每个非起始世界在 TagManager 里登记 `World_{场景名}` 图层（幂等）。
    /// WorldLightIsolation 优先用同名图层，没有才动态分配——登记后排查日志里能看到世界名。
    /// </summary>
    private static int EnsureWorldLayers(List<string> scenePaths, StringBuilder report)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return 0;
        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        if (layers == null || !layers.isArray) return 0;

        int added = 0;
        foreach (var path in scenePaths)
        {
            string scene = Path.GetFileNameWithoutExtension(path);
            if (scene == PortalWindow.HomeSceneName) continue;
            string layerName = "World_" + scene;
            if (LayerMask.NameToLayer(layerName) >= 0) continue;

            // 只从 12 往上找空位：0~6 是项目既有图层，7~11 已给四个内置世界与 World_Other
            int slot = -1;
            for (int i = 12; i < layers.arraySize; i++)
            {
                var e = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(e.stringValue)) { slot = i; break; }
            }
            if (slot < 0) { report.AppendLine("[警告] 图层槽用尽，未登记 " + layerName); continue; }

            layers.GetArrayElementAtIndex(slot).stringValue = layerName;
            added++;
            report.AppendLine("  [图层] " + slot + " → " + layerName);
        }
        if (added > 0) so.ApplyModifiedPropertiesWithoutUndo();
        return added;
    }

    // ─────────────────────────────────────────────────────────────
    // 场景枚举 / Build Settings
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 候选场景 = Build Settings 里启用的 ∪ `Assets/Scenes/` 下的 .unity。
    /// 后者让"刚拖进来还没登记"的新世界也能被自动发现，这是"加了世界没配对"的常见入口。
    /// </summary>
    private static List<string> CollectScenePaths()
    {
        var list = new List<string>();
        var seen = new HashSet<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || string.IsNullOrEmpty(s.path)) continue;
            if (seen.Add(s.path)) list.Add(s.path);
        }
        if (Directory.Exists(GameSceneDir))
        {
            foreach (var f in Directory.GetFiles(GameSceneDir, "*.unity", SearchOption.TopDirectoryOnly))
            {
                string p = f.Replace('\\', '/');
                if (seen.Add(p)) list.Add(p);
            }
        }
        return list;
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
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].path, p, StringComparison.OrdinalIgnoreCase)) { list[i] = new EditorBuildSettingsScene(p, true); exists = true; break; }
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

    /// <summary>宽松查找：门名连字符/下划线互换后再找一次（场景名带下划线、门名习惯用连字符）。</summary>
    private static DoorInfo FindLoose(string sceneName, string doorName, Dictionary<string, List<DoorInfo>> byScene)
    {
        List<DoorInfo> list;
        if (!byScene.TryGetValue(sceneName, out list)) return null;
        string a = doorName.Replace('-', '_');
        string b = doorName.Replace('_', '-');
        foreach (var d in list)
            if (string.Equals(d.name, a, StringComparison.OrdinalIgnoreCase)
                || string.Equals(d.name, b, StringComparison.OrdinalIgnoreCase)) return d;
        return null;
    }

    /// <summary>
    /// 从 <paramref name="other"/> 出发，按运行时真实行为推导一遍，看它最终会不会连回
    /// <paramref name="me"/>。判据三步：①对面推导出的目标世界就是我这边的场景；
    /// ②在我这个场景里按候选名列表能找到一扇门；③找到的那扇就是我。
    /// 只比字符串会误判——`anywhere-door-Metro` 这类靠"回起始世界"兜底的门，
    /// 推导出的名字与对面门的实际名字本来就不同，但两端拓扑是对称的。
    /// </summary>
    private static bool ResolvesBackTo(DoorInfo other, DoorInfo me,
                                       Dictionary<string, List<DoorInfo>> byScene)
    {
        string scene, partner;
        if (!PortalWindow.TryDerivePairing(other.name, other.sceneName, out scene, out partner))
            return false;
        if (!string.Equals(scene, me.sceneName, StringComparison.Ordinal)) return false;

        var back = FindByCandidates(me.sceneName,
                                    PortalWindow.DerivePartnerDoorCandidates(other.name, other.sceneName),
                                    byScene);
        return back != null && ReferenceEquals(back, me);
    }

    /// <summary>
    /// 按候选名列表在目标世界里找门，语义与运行时 <c>PortalWindow.FindDoor(Scene, List&lt;string&gt;)</c> 一致：
    /// 先按候选优先级逐个做精确匹配（含连字符↔下划线互换），全部失败才退到宽松比较。
    /// 之所以要复刻这套顺序：报告必须反映"运行时到底会连上哪一扇"，否则工具与游戏各说各话。
    /// </summary>
    private static DoorInfo FindByCandidates(string sceneName, List<string> candidates,
                                             Dictionary<string, List<DoorInfo>> byScene)
    {
        List<DoorInfo> list;
        if (!byScene.TryGetValue(sceneName, out list) || list == null || list.Count == 0) return null;

        // 第 1、2 级：按候选优先级逐个精确匹配（名字 / 连字符↔下划线互换）
        foreach (var want in candidates)
        {
            foreach (var d in list)
                if (string.Equals(d.name, want, StringComparison.Ordinal)) return d;
            string alt = want.Replace('-', '_');
            if (alt != want)
                foreach (var d in list)
                    if (string.Equals(d.name, alt, StringComparison.Ordinal)) return d;
            string alt2 = want.Replace('_', '-');
            if (alt2 != want)
                foreach (var d in list)
                    if (string.Equals(d.name, alt2, StringComparison.Ordinal)) return d;
        }

        // 第 3 级：宽松比较（剥后缀/世界名尾巴后同名），只在唯一命中时采信
        DoorInfo only = null;
        int hits = 0;
        foreach (var d in list)
            if (PortalWindow.SameDoorName(d.name, candidates.Count > 0 ? candidates[0] : ""))
            { only = d; hits++; }
        return hits == 1 ? only : null;
    }
}
