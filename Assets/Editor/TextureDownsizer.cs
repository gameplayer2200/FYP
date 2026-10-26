using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FYP.EditorTools
{
    /// <summary>
    /// 把「法线图 / 金属度-粗糙度图」这类"细节辅助贴图"的最大尺寸从 2048 降到 1024。
    ///
    /// 为什么只动这一类：
    /// 基色图（BaseColor / Albedo）决定观感，保持 2048；
    /// 法线图和金属度图提供的是高频细节，1024 与 2048 在游戏视角下几乎无法分辨，
    /// 而它们占的内存一样大（2048² DXT5 = 10.7MB/张）。
    ///
    /// 实测（2026-10-27）：Factory/Metro 世界包里 56 张 2048² DXT5 贴图共占 597MB，
    /// 其中 25 张法线图 + 31 张金属度图 —— 全部降到 1024 可省约 448MB。
    ///
    /// 判定规则（只按名字，不按材质槽）：
    ///   命中 `_NM` / `_Normal` / `Normal` 结尾 → 法线图
    ///   命中 `_Metallic` / `Metallic` / `_Mask` 结尾 → 金属度/遮罩图
    /// 其余（含 Albedo / BaseColor / _D / _Diffuse）不动。
    /// </summary>
    public static class TextureDownsizer
    {
        private const int TargetSize = 1024;

        [MenuItem("FYP/优化/降采样辅助贴图（法线/金属度 → 1024）")]
        public static void RunMenu()
        {
            Debug.Log(Run(false));
        }

        [MenuItem("FYP/优化/降采样辅助贴图（试运行，不写入）")]
        public static void RunDryMenu()
        {
            Debug.Log(Run(true));
        }

        public static string Run(bool dryRun)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine(dryRun ? "=== 试运行（不写入）===" : "=== 降采样辅助贴图 ===");

            int scanned = 0, changed = 0;
            long savedBytes = 0;
            var lines = new List<string>();

            foreach (string p in AssetDatabase.GetAllAssetPaths())
            {
                string lower = p.ToLowerInvariant();
                if (!lower.EndsWith(".png") && !lower.EndsWith(".tga") && !lower.EndsWith(".jpg")
                    && !lower.EndsWith(".tif") && !lower.EndsWith(".tiff") && !lower.EndsWith(".psd")) continue;
                // 只处理第三方世界包的贴图（自制材质贴图量小，没必要冒险）
                if (!p.StartsWith("Assets/Art/Worlds/", System.StringComparison.OrdinalIgnoreCase)) continue;

                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                scanned++;
                if (imp.maxTextureSize <= TargetSize) continue;
                if (!IsAuxiliaryMap(System.IO.Path.GetFileNameWithoutExtension(p), imp.textureType)) continue;

                int before = imp.maxTextureSize;
                // 减半策略：4096 → 2048、2048 → 1024。
                // 不一步降到 1024，是因为 4096 的多是建筑/地形法线图，直降会让近景墙面细节塌掉。
                int target = Mathf.Max(TargetSize, before / 2);
                if (target >= before) continue;

                // DXT5 每像素 1 字节（含 mip 约 1.33 倍），尺寸减半 → 面积 1/4
                long w = target, h = target;
                long est = (long)(w * h * 1.33);
                savedBytes += est * 3; // 面积是原来的 1/4，省 3/4

                lines.Add("  " + System.IO.Path.GetFileName(p) + " : " + before + " → " + target
                          + "  (" + imp.textureType + ")");
                if (!dryRun)
                {
                    imp.maxTextureSize = target;
                    imp.SaveAndReimport();
                    changed++;
                }
            }

            foreach (var l in lines) report.AppendLine(l);
            report.AppendLine("扫描 " + scanned + " 张，命中 " + lines.Count + " 张"
                              + (dryRun ? "（试运行未写入）" : "，已改 " + changed + " 张"));
            report.AppendLine("预计节省约 " + (savedBytes / 1048576) + "MB");

            if (!dryRun) AssetDatabase.Refresh();
            return report.ToString();
        }

        /// <summary>法线图 / 金属度-粗糙度图 / AO 图：细节辅助贴图，降到 1024 观感损失可忽略。</summary>
        private static bool IsAuxiliaryMap(string name, TextureImporterType type)
        {
            if (type == TextureImporterType.NormalMap) return true;
            string n = name.ToLowerInvariant();
            if (n.EndsWith("_nm") || n.EndsWith("nm") || n.Contains("_normal") || n.EndsWith("normal")) return true;
            if (n.Contains("metallic") || n.Contains("roughness") || n.EndsWith("_mask")) return true;
            if (n.EndsWith("_ao") || n.EndsWith("ao")) return true;
            return false;
        }
    }
}
