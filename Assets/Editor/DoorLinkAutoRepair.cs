using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 门配对一键重配工具。
///
/// ── 解决什么问题 ────────────────────────────────────────────────
/// WorldLink 里硬编码了「目标场景名 + 目标门名」。把一扇门挪到别的场景后，
/// 所有指向它的 link 就全错了，必须手工一条条改。
///
/// 本工具把配对**回归到命名约定**：任意门名 `X` 与 `X-Lab` 互为配对。
/// 场景不靠配置，而是扫描 Build Settings 里所有场景现算——门挪到哪都自动跟上。
///
/// ── 用法 ────────────────────────────────────────────────────────
/// 摆完门后点菜单：FYP/门配对/扫描并自动重配
/// 它会：扫全部场景 → 按名字配对 → 复用或新建 WorldLink → 写回每扇门的 PortalWindow → 保存场景
///
/// ── 判定规则 ────────────────────────────────────────────────────
///   · 配对：`anywhere-door-FactoryNight-Lab` ↔ `anywhere-door-FactoryNight`
///   · mapYaw180：两扇门世界朝向相差 > 90° 时置 true（门→门映射需绕 Y 转 180°）
///   · preloadRadius：复用已有资产的调参；新建时实验室侧 4.5、其余 15
///
/// 注意：本工具会**保存所有涉及的门场景**。运行前确认没有未保存的其它改动。
/// </summary>
public static class DoorLinkAutoRepair
{
    const string DoorPrefix = "anywhere-door";
    const string LabSuffix = "-Lab";
    const string WorldLinkDir = "Assets/WorldLinks";
    const string HomeScene = "SpecimenLab";

    private class DoorInfo
    {
        public string name;
        public string sceneName;
        public Vector3 position;
        public float yaw;
        public bool hasWindow;
        public string partnerName;
        public string partnerScene;
    }

    [MenuItem("FYP/门配对/扫描并自动重配")]
    public static void Repair()
    {
        string originalPath = EditorSceneManager.GetActiveScene().path;
        var report = new StringBuilder();
        var doors = new List<DoorInfo>();

        try
        {
            // ── 第 1 遍：扫描所有场景，收集门 ──
            var scenePaths = CollectScenePaths();
            report.AppendLine("扫描场景 " + scenePaths.Count + " 个：");

            for (int i = 0; i < scenePaths.Count; i++)
            {
                var sc = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
                int found = 0;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!t.name.StartsWith(DoorPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                        var pw = t.GetComponent("PortalWindow");
                        doors.Add(new DoorInfo
                        {
                            name = t.name,
                            sceneName = sc.name,
                            position = t.position,
                            yaw = t.eulerAngles.y,
                            hasWindow = pw != null,
                        });
                        found++;
                    }
                report.AppendLine("  " + sc.name + "：" + found + " 扇");
            }

            // ── 配对：按命名约定 ──
            var byName = new Dictionary<string, DoorInfo>();
            foreach (var d in doors) if (!byName.ContainsKey(d.name)) byName[d.name] = d;

            int paired = 0, missing = 0;
            foreach (var d in doors)
            {
                d.partnerName = PartnerOf(d.name);
                DoorInfo p;
                if (byName.TryGetValue(d.partnerName, out p))
                {
                    d.partnerScene = p.sceneName;
                    paired++;
                }
                else
                {
                    d.partnerScene = null;
                    missing++;
                    report.AppendLine("  [警告] " + d.name + " 找不到配对 " + d.partnerName);
                }
            }

            // ── 第 2 遍：配置资产并写回 ──
            var assignedByScene = new Dictionary<string, List<KeyValuePair<string, string>>>(); // 场景 → (门名, 资产路径)

            foreach (var d in doors)
            {
                if (d.partnerScene == null) continue;
                if (!d.hasWindow) { report.AppendLine("  [跳过] " + d.name + " 没有 PortalWindow"); continue; }

                var partner = byName[d.partnerName];
                bool yaw180 = YawDiffers180(d.yaw, partner.yaw);
                var link = GetOrCreateLink(d, yaw180, report);
                if (link == null) continue;

                string linkPath = AssetDatabase.GetAssetPath(link);
                if (!assignedByScene.ContainsKey(d.sceneName))
                    assignedByScene[d.sceneName] = new List<KeyValuePair<string, string>>();
                assignedByScene[d.sceneName].Add(new KeyValuePair<string, string>(d.name, linkPath));

                report.AppendLine("  " + d.sceneName + "/" + d.name
                                  + "  →  " + d.partnerScene + "/" + d.partnerName
                                  + "   [" + link.name + "]  yaw180=" + (yaw180 ? 1 : 0));
            }

            // ── 第 3 遍：写回场景并保存 ──
            foreach (var kv in assignedByScene)
            {
                string path = ScenePathOf(kv.Key);
                if (path == null) continue;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int wrote = 0;
                foreach (var pair in kv.Value)
                {
                    var link = AssetDatabase.LoadAssetAtPath<WorldLink>(pair.Value);
                    foreach (var root in sc.GetRootGameObjects())
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name != pair.Key) continue;
                            var pw = t.GetComponent("PortalWindow");
                            if (pw == null) continue;
                            var so = new SerializedObject(pw);
                            var prop = so.FindProperty("link");
                            if (prop == null) continue;
                            prop.objectReferenceValue = link;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            wrote++;
                        }
                }
                if (wrote > 0)
                {
                    EditorSceneManager.MarkSceneDirty(sc);
                    EditorSceneManager.SaveScene(sc);
                    report.AppendLine("  已保存 " + sc.name + "（写入 " + wrote + " 个 link）");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine();
            report.AppendLine("完成：配对 " + paired + " 扇，未找到配对 " + missing + " 扇。");
            Debug.Log("[门配对] " + report);
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalPath))
                EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
        }
    }

    // ───────────── 规则 ─────────────

    /// <summary>按命名约定推导配对门名：`X-Lab` → `X`，`X` → `X-Lab`。</summary>
    public static string PartnerOf(string doorName)
    {
        if (doorName.EndsWith(LabSuffix, StringComparison.OrdinalIgnoreCase))
            return doorName.Substring(0, doorName.Length - LabSuffix.Length);
        return doorName + LabSuffix;
    }

    /// <summary>两扇门朝向相差是否接近 180°（用最短夹角，避免 359/1 这种绕圈误判）。</summary>
    static bool YawDiffers180(float a, float b)
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(a, b));
        return diff > 90f;
    }

    // ───────────── 资产 ─────────────

    static WorldLink GetOrCreateLink(DoorInfo d, bool yaw180, StringBuilder report)
    {
        // 先找"内容已经对得上"的现有资产，避免无谓改名/新建
        var existing = FindMatching(d.partnerScene, d.partnerName);
        if (existing != null)
        {
            Configure(existing, d.partnerScene, d.partnerName, yaw180, keepRadius: true);
            return existing;
        }

        string assetName = d.sceneName + "To" + d.partnerScene;
        string path = WorldLinkDir + "/" + assetName + ".asset";
        var wl = AssetDatabase.LoadAssetAtPath<WorldLink>(path);
        if (wl == null)
        {
            if (!AssetDatabase.IsValidFolder(WorldLinkDir))
            {
                AssetDatabase.CreateFolder("Assets", "WorldLinks");
            }
            wl = ScriptableObject.CreateInstance<WorldLink>();
            AssetDatabase.CreateAsset(wl, path);
            report.AppendLine("  [新建] " + assetName);
        }
        Configure(wl, d.partnerScene, d.partnerName, yaw180, keepRadius: false);
        return wl;
    }

    /// <summary>在所有 WorldLink 资产里找一条已经指向 (scene, door) 的。</summary>
    static WorldLink FindMatching(string scene, string door)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:WorldLink"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var wl = AssetDatabase.LoadAssetAtPath<WorldLink>(path);
            if (wl == null) continue;
            var so = new SerializedObject(wl);
            var s = so.FindProperty("worldSceneName");
            var dd = so.FindProperty("worldDoorName");
            if (s != null && dd != null && s.stringValue == scene && dd.stringValue == door)
                return wl;
        }
        return null;
    }

    static void Configure(WorldLink wl, string scene, string door, bool yaw180, bool keepRadius)
    {
        var so = new SerializedObject(wl);
        so.FindProperty("worldSceneName").stringValue = scene;
        so.FindProperty("worldDoorName").stringValue = door;
        so.FindProperty("mapYaw180").boolValue = yaw180;
        if (!keepRadius)
        {
            var r = so.FindProperty("preloadRadius");
            if (r != null) r.floatValue = scene == HomeScene ? 4.5f : 15f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wl);
    }

    // ───────────── 场景枚举 ─────────────

    static List<string> CollectScenePaths()
    {
        var list = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && !string.IsNullOrEmpty(s.path)) list.Add(s.path);
        return list;
    }

    static string ScenePathOf(string sceneName)
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == sceneName) return s.path;
        return null;
    }
}
