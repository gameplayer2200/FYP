using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成字体预热字符表：扫描 Assets/Scripts 下所有 .cs 里的中文字符，
/// 写进 Assets/Resources/CrtCharset.txt，供 CrtFontPrewarm 在加载屏期间预热字体图集。
///
/// 为什么需要：HUD 用的是**动态字体**（NotoSansMonoCJKsc，16MB OTF）。动态字体的字形是
/// 第一次被请求时才光栅化进图集的；图集塞满后会**翻倍重建**——实测成本：
///   首次 489 字 → 1024×1024，14~19ms
///   累计 2000 字 → 1024×2048，66ms
///   累计 4000 字 → 2048×2048，**142ms**
/// 值班播报正好是"进层域那一刻弹出来的一批新字"，所以表现为"传送/进世界时卡一下"。
/// 把全部会用到的字符在加载屏期间一次性请求掉，图集就在那时定型，之后永不重建。
///
/// 为什么只扫 .cs：玩家真正看到的文字都写在脚本里（UI 文案、播报、订单、账单）。
/// 场景/预制件里的中文是物体名，不上屏；把它们算进来会让图集白涨一倍。
/// </summary>
public static class CrtCharsetGenerator
{
    private const string OutputPath = "Assets/Resources/CrtCharset.txt";
    private const string ScanRoot = "Assets/Scripts";

    [MenuItem("FYP/UI/生成字体预热字符表")]
    public static void Generate()
    {
        var set = new HashSet<char>();

        // 1) 脚本里的中文（真正上屏的文案）
        string[] files = System.IO.Directory.GetFiles(ScanRoot, "*.cs", System.IO.SearchOption.AllDirectories);
        int scanned = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string text;
            try { text = System.IO.File.ReadAllText(files[i], Encoding.UTF8); }
            catch { continue; }
            scanned++;
            for (int c = 0; c < text.Length; c++) AddIfCjk(set, text[c]);
        }

        // 2) 补上乱码字符（EmfStorm 风暴会把文字替换成这些块）
        const string noise = "▓▒█/\\|_·#*";
        for (int i = 0; i < noise.Length; i++) set.Add(noise[i]);

        // 3) 补上 HUD 一定会出现的符号
        const string punct = "·—…「」『』（）【】《》、。，：；！？　%+-×/";
        for (int i = 0; i < punct.Length; i++) set.Add(punct[i]);

        // 4) 数字与常用拉丁（等宽字体里也是独立字形）
        for (char c = '0'; c <= '9'; c++) set.Add(c);
        for (char c = 'A'; c <= 'Z'; c++) set.Add(c);
        for (char c = 'a'; c <= 'z'; c++) set.Add(c);

        var list = new List<char>(set);
        list.Sort();
        string content = new string(list.ToArray());

        // 确保目录存在
        string dir = System.IO.Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        System.IO.File.WriteAllText(OutputPath, content, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

        Debug.Log("[字体预热] 扫描 " + scanned + " 个 .cs，生成 " + list.Count
                  + " 个字符 → " + OutputPath
                  + "（含中文 " + CountCjk(list) + " 个）");
    }

    private static void AddIfCjk(HashSet<char> set, char ch)
    {
        int c = ch;
        if ((c >= 0x4E00 && c <= 0x9FFF) ||      // CJK 统一表意
            (c >= 0x3400 && c <= 0x4DBF) ||      // 扩展 A
            (c >= 0x3000 && c <= 0x303F) ||      // 中文标点
            (c >= 0xFF00 && c <= 0xFFEF))        // 全角字符
            set.Add(ch);
    }

    private static int CountCjk(List<char> list)
    {
        int n = 0;
        for (int i = 0; i < list.Count; i++)
        {
            int c = list[i];
            if (c >= 0x4E00 && c <= 0x9FFF) n++;
        }
        return n;
    }
}
