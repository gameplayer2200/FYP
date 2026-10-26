using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FYP.EditorTools
{
    /// <summary>
    /// 把场景里"以 .glb 为源预制件"的 PrefabInstance 解包成普通 GameObject。
    ///
    /// 为什么需要：
    /// 上一步 `GlbMeshExtractor` 把网格抽成了独立 .asset、`GlbTextureCompressor`
    /// 把材质换成了压缩版外置材质，但场景里那个对象仍然是 **GLB 的 PrefabInstance**
    /// （`m_SourcePrefab` 指向 .glb）。只要实例还挂着，
    /// 整个 GLB 资产（含内嵌 2048² ARGB32 贴图，每张 42.7MB）就会被加载。
    ///
    /// 实测（2026-10-27）：Metro 场景里 8 个 multimeter 渲染器、SpecimenLab 里 10 个 case
    /// 渲染器都还挂着 GLB PrefabInstance，导致那 3 张 42.7MB 贴图（共 128MB）一直驻留。
    ///
    /// 做法：`PrefabUtility.UnpackPrefabInstance(root, CompletelyUnpack)`，
    /// 把实例变成普通物体。网格/材质引用此时已经指向独立资产，解包不会丢外观。
    /// </summary>
    public static class GlbPrefabUnpacker
    {
        [MenuItem("FYP/优化/解包 GLB 预制件实例")]
        public static void RunMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== 解包 GLB 预制件实例 ===");

            var scenes = new List<string>();
            foreach (string p in AssetDatabase.GetAllAssetPaths())
                if (p.EndsWith(".unity")) scenes.Add(p);
            scenes.Sort();

            var current = EditorSceneManager.GetActiveScene();
            string currentPath = current.IsValid() ? current.path : "";

            int totalUnpacked = 0;
            foreach (string path in scenes)
            {
                bool alreadyOpen = false;
                var scene = default(UnityEngine.SceneManagement.Scene);
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (s.path == path) { scene = s; alreadyOpen = true; break; }
                }
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (!scene.IsValid()) continue;

                var roots = new List<GameObject>();
                foreach (var go in scene.GetRootGameObjects()) roots.Add(go);

                int n = 0;
                // 逐层收集所有 GLB 源预制件实例的根
                var toUnpack = new List<GameObject>();
                foreach (var root in roots) Collect(root.transform, toUnpack);

                foreach (var go in toUnpack)
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                    var instRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                    if (instRoot == null) continue;
                    string src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instRoot);
                    if (string.IsNullOrEmpty(src) || !src.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (toUnpack.Contains(instRoot) && instRoot != go) continue;
                    PrefabUtility.UnpackPrefabInstance(instRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    report.AppendLine("    " + path + " :: " + instRoot.name + " ← " + src);
                    n++;
                }

                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.AppendLine("  场景 " + path + "：解包 " + n + " 个");
                    totalUnpacked += n;
                }
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.SaveAssets();
            report.AppendLine("合计解包 " + totalUnpacked + " 个");

            if (!string.IsNullOrEmpty(currentPath))
            {
                var reopened = EditorSceneManager.GetSceneByPath(currentPath);
                if (reopened.IsValid()) EditorSceneManager.SetActiveScene(reopened);
            }
            return report.ToString();
        }

        private static void Collect(Transform t, List<GameObject> outList)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if (root != null && !outList.Contains(root)) outList.Add(root);
                return;
            }
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), outList);
        }
    }
}
