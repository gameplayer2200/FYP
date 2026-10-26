using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FYP.EditorTools
{
    /// <summary>
    /// 把 .glb 里的网格抽成独立 .asset，并让场景/预制件改引用独立网格。
    ///
    /// 为什么需要这个工具：
    /// glTFast 导入 .glb 时把网格、材质、贴图全部塞进同一个资产文件。
    /// **只要场景里还有任何一个 MeshFilter 引用 GLB 的网格，整个 GLB 就会被加载**，
    /// 连带它内嵌的 2048² ARGB32 贴图（每张 42.7MB）一起进内存。
    ///
    /// 实测（2026-10-27）：`multimeter.glb` 的材质已全部换成压缩版外置材质，
    /// 但 Metro 场景里 8 个渲染器的网格仍指向 GLB，那 3 张 42.7MB 贴图就一直在。
    ///
    /// 做法：把每个 Mesh 复制成独立 .asset（`<glb 目录>/ExtractedMeshes/<name>.asset`），
    /// 再把所有场景/预制件里指向 GLB 网格的 MeshFilter/MeshCollider 换过去。
    /// 换完之后 GLB 不再被任何场景引用，它的内嵌贴图就不会被加载。
    ///
    /// 幂等：独立网格已存在就复用，不重复创建。
    /// </summary>
    public static class GlbMeshExtractor
    {
        private const string MeshSubDir = "ExtractedMeshes";

        [MenuItem("FYP/优化/抽取 GLB 网格为独立资产")]
        public static void RunMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== GLB 网格抽取 ===");

            var glbs = new List<string>();
            foreach (string p in AssetDatabase.GetAllAssetPaths())
                if (p.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) glbs.Add(p);
            glbs.Sort();

            // glb 网格 → 独立网格
            var map = new Dictionary<Mesh, Mesh>();
            int created = 0, reused = 0;

            foreach (string glb in glbs)
            {
                var meshes = new List<Mesh>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(glb))
                    if (o is Mesh m) meshes.Add(m);
                if (meshes.Count == 0) continue;

                string dir = Path.Combine(Path.GetDirectoryName(glb), MeshSubDir).Replace('\\', '/');
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                report.AppendLine("· " + glb + "（网格 " + meshes.Count + "）");

                foreach (var src in meshes)
                {
                    string outPath = dir + "/" + Sanitize(src.name) + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(outPath);
                    if (existing != null)
                    {
                        map[src] = existing;
                        reused++;
                        continue;
                    }
                    var copy = Object.Instantiate(src);
                    copy.name = src.name;
                    AssetDatabase.CreateAsset(copy, outPath);
                    map[src] = copy;
                    created++;
                    report.AppendLine("    " + src.name + " verts=" + src.vertexCount);
                }
            }
            AssetDatabase.SaveAssets();
            report.AppendLine("网格：新建 " + created + "、复用 " + reused);

            if (map.Count == 0) return report.ToString();

            // 换引用
            int swapped = SwapInAssets(map, report);
            report.AppendLine("换引用：MeshFilter/MeshCollider 共 " + swapped + " 处");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report.ToString();
        }

        private static int SwapInAssets(Dictionary<Mesh, Mesh> map, System.Text.StringBuilder report)
        {
            int swapped = 0;
            var paths = new List<string>();
            foreach (string p in AssetDatabase.GetAllAssetPaths())
                if (p.EndsWith(".unity") || p.EndsWith(".prefab")) paths.Add(p);
            paths.Sort();

            foreach (string path in paths)
            {
                if (path.EndsWith(".unity"))
                {
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    if (!scene.IsValid()) continue;
                    int n = 0;
                    foreach (var mf in Object.FindObjectsOfType<MeshFilter>(true))
                    {
                        if (mf.gameObject.scene != scene) continue;
                        if (mf.sharedMesh != null && map.TryGetValue(mf.sharedMesh, out var repl))
                        { mf.sharedMesh = repl; n++; }
                    }
                    foreach (var mc in Object.FindObjectsOfType<MeshCollider>(true))
                    {
                        if (mc.gameObject.scene != scene) continue;
                        if (mc.sharedMesh != null && map.TryGetValue(mc.sharedMesh, out var repl))
                        { mc.sharedMesh = repl; n++; }
                    }
                    if (n > 0)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                        report.AppendLine("    场景 " + path + "：" + n + " 处");
                        swapped += n;
                    }
                    if (UnityEngine.SceneManagement.SceneManager.sceneCount > 1)
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
                else
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null) continue;
                    int n = 0;
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null && map.TryGetValue(mf.sharedMesh, out var repl))
                        { mf.sharedMesh = repl; n++; }
                    foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                        if (mc.sharedMesh != null && map.TryGetValue(mc.sharedMesh, out var repl))
                        { mc.sharedMesh = repl; n++; }
                    if (n > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        report.AppendLine("    预制件 " + path + "：" + n + " 处");
                        swapped += n;
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return swapped;
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }
    }
}
