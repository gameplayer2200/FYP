using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FYP.EditorTools
{
    /// <summary>
    /// 把 .glb 内嵌贴图抽成外置 PNG 并重新压缩，再把材质改成引用外置版本。
    ///
    /// 为什么需要这个工具：
    /// glTFast（com.unity.cloud.gltfast）导入 .glb 时贴图走自己的管线，**不经过 TextureImporter**，
    /// 因此没有任何压缩设置可用，只能原样存成 ARGB32。实测（2026-10-27）三个道具 GLB 里的
    /// 17 张 2048×2048 贴图全部是 ARGB32 未压缩，每张 42.7MB，合计 725MB —— 占全工程
    /// Texture2D 内存（1737MB）的四成。
    ///
    /// 做法：
    /// 1. 把内嵌 Texture2D 通过 Graphics.Blit + ReadPixels 读出来（GLB 贴图不可读，不能直接 EncodeToPNG）；
    /// 2. 写成外置 PNG（放在 GLB 同级的 CompressedTextures/ 下）；
    /// 3. 给 PNG 配 TextureImporter：maxSize 1024 + 压缩（法线图用 NormalMap 类型走 BC5）；
    /// 4. 复制 GLB 的材质到 Assets/Material/GLBCompressed/，把贴图槽换成外置 PNG；
    /// 5. 场景/预制件里所有引用原 GLB 材质的渲染器改指新材质。
    ///
    /// 效果：2048² ARGB32（42.7MB）→ 1024² BC3/BC5（约 5.3MB），单张降 8 倍。
    /// </summary>
    public static class GlbTextureCompressor
    {
        private const int TargetMaxSize = 1024;
        private const string MatOutDir = "Assets/Material/GLBCompressed";

        [MenuItem("FYP/优化/压缩 GLB 内嵌贴图")]
        public static void RunMenu()
        {
            Debug.Log(Run());
        }

        /// <summary>对全部 .glb 跑一遍，返回可读报告。</summary>
        public static string Run()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== GLB 内嵌贴图压缩 ===");

            var glbs = new List<string>();
            foreach (string p in AssetDatabase.GetAllAssetPaths())
                if (p.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
                    glbs.Add(p);
            glbs.Sort();

            if (!Directory.Exists(MatOutDir)) Directory.CreateDirectory(MatOutDir);

            long bytesBefore = 0, bytesAfter = 0;
            int texCount = 0, matCount = 0, rendererCount = 0;

            foreach (string glb in glbs)
            {
                var subs = AssetDatabase.LoadAllAssetsAtPath(glb);
                if (subs == null || subs.Length == 0) continue;

                // ── 1) 收集内嵌贴图 ──
                var embedded = new List<Texture2D>();
                var mats = new List<Material>();
                foreach (var o in subs)
                {
                    if (o is Texture2D t) embedded.Add(t);
                    else if (o is Material m) mats.Add(m);
                }
                if (embedded.Count == 0) continue;

                string texDir = Path.Combine(Path.GetDirectoryName(glb), "CompressedTextures").Replace('\\', '/');
                if (!Directory.Exists(texDir)) Directory.CreateDirectory(texDir);

                report.AppendLine("· " + glb + "（贴图 " + embedded.Count + "、材质 " + mats.Count + "）");

                // ── 2) 抽出 PNG 并配导入设置 ──
                var map = new Dictionary<Texture2D, Texture2D>();
                foreach (var tex in embedded)
                {
                    long oldBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                    bytesBefore += oldBytes;

                    string outPath = texDir + "/" + Sanitize(tex.name) + ".png";
                    if (!File.Exists(outPath)) WritePng(tex, outPath);

                    AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
                    ConfigureImporter(outPath, IsNormalMap(tex.name));

                    var newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
                    if (newTex == null)
                    {
                        report.AppendLine("    ✗ 导入失败：" + outPath);
                        continue;
                    }
                    long newBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(newTex);
                    bytesAfter += newBytes;
                    texCount++;
                    map[tex] = newTex;
                    report.AppendLine("    " + tex.name + "：" + tex.width + "x" + tex.height + " "
                        + (oldBytes / 1048576) + "MB → " + newTex.width + "x" + newTex.height + " "
                        + newTex.format + " " + (newBytes / 1024) + "KB");
                }

                // ── 3) 复制材质并换贴图 ──
                var matMap = new Dictionary<Material, Material>();
                foreach (var src in mats)
                {
                    string matPath = MatOutDir + "/" + Sanitize(Path.GetFileNameWithoutExtension(glb))
                                     + "_" + Sanitize(src.name) + ".mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (copy == null)
                    {
                        copy = new Material(src);
                        AssetDatabase.CreateAsset(copy, matPath);
                    }
                    else
                    {
                        copy.CopyPropertiesFromMaterial(src);
                        copy.shader = src.shader;
                    }

                    // 把每个贴图槽里指向内嵌贴图的，换成外置 PNG
                    var sh = copy.shader;
                    if (sh != null)
                    {
                        int n = sh.GetPropertyCount();
                        for (int i = 0; i < n; i++)
                        {
                            if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                            string pn = sh.GetPropertyName(i);
                            var cur = copy.GetTexture(pn);
                            if (cur is Texture2D ct && map.TryGetValue(ct, out var repl))
                                copy.SetTexture(pn, repl);
                        }
                    }
                    EditorUtility.SetDirty(copy);
                    matMap[src] = copy;
                    matCount++;
                }
                AssetDatabase.SaveAssets();

                // ── 4) 场景/预制件里的渲染器换材质 ──
                rendererCount += SwapInScenes(matMap, report);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine();
            report.AppendLine("合计：贴图 " + texCount + " 张、" + (bytesBefore / 1048576) + "MB → "
                              + (bytesAfter / 1048576) + "MB；材质 " + matCount + " 个；换材质渲染器 "
                              + rendererCount + " 个");
            return report.ToString();
        }

        /// <summary>把 GLB 贴图读成 PNG。GLB 贴图默认不可读，走 Blit+ReadPixels。</summary>
        private static void WritePng(Texture2D src, string path)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var readable = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                readable.Apply();
                File.WriteAllBytes(path, readable.EncodeToPNG());
                Object.DestroyImmediate(readable);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static bool IsNormalMap(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("normal") || n.Contains("_nrm") || n.EndsWith("_n");
        }

        private static void ConfigureImporter(string path, bool normalMap)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normalMap;
            ti.maxTextureSize = TargetMaxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 1;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
        }

        /// <summary>遍历所有场景与预制件，把引用旧材质的渲染器换成新材质。</summary>
        private static int SwapInScenes(Dictionary<Material, Material> matMap, System.Text.StringBuilder report)
        {
            if (matMap.Count == 0) return 0;
            int swapped = 0;

            var paths = new List<string>();
            foreach (string p in AssetDatabase.GetAllAssetPaths())
                if (p.EndsWith(".unity") || p.EndsWith(".prefab")) paths.Add(p);
            paths.Sort();

            foreach (string path in paths)
            {
                // 场景用 OpenScene 处理（可能有多个）；预制件用 LoadPrefabContents
                if (path.EndsWith(".unity"))
                {
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    if (!scene.IsValid()) continue;
                    bool dirty = false;
                    foreach (var r in Object.FindObjectsOfType<Renderer>(true))
                    {
                        if (r.gameObject.scene != scene) continue;
                        if (SwapRenderer(r, matMap)) { dirty = true; swapped++; }
                    }
                    if (dirty)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                        report.AppendLine("    保存场景 " + path);
                    }
                    if (UnityEngine.SceneManagement.SceneManager.sceneCount > 1)
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
                else
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null) continue;
                    bool dirty = false;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        if (SwapRenderer(r, matMap)) { dirty = true; swapped++; }
                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        report.AppendLine("    保存预制件 " + path);
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return swapped;
        }

        private static bool SwapRenderer(Renderer r, Dictionary<Material, Material> matMap)
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && matMap.TryGetValue(mats[i], out var repl))
                {
                    mats[i] = repl;
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
            return changed;
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
