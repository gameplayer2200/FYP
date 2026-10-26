using System.Text;
using UnityEngine;

/// <summary>
/// 字体图集预热：把 HUD 会用到的全部字符在**加载屏期间**一次性光栅化进动态字体图集。
///
/// 为什么需要这个：HUD 用动态字体（NotoSansMonoCJKsc，16MB OTF）。动态字体的字形是
/// 第一次被请求时才光栅化的，图集塞满还会翻倍重建。实测成本（Unity 2022.3）：
///   首次 489 字 → 图集 1024×1024，14~19ms
///   累计 2000 字 → 图集 1024×2048，66ms
///   累计 4000 字 → 图集 2048×2048，**142ms**
/// 而值班播报正好是"刚进层域就弹出的一批新字"——于是玩家感受到的是
/// 「传送到 Metro 时卡一下」/「右上角播报出来时卡一下」。二者是同一件事。
///
/// 做法：开局部署阶段（加载屏还盖着、玩家已被 PlayerInputGate 冻住）就把
/// Assets/Resources/CrtCharset.txt 里的字符按 HUD 实际用到的每个字号全部请求一遍。
/// 图集在这一刻定型，之后任何文案上屏都是命中缓存（实测二次请求 ≈0.1ms）。
///
/// 字符表由 Editor 菜单 FYP/UI/生成字体预热字符表 扫描脚本生成，改了文案后重跑一次即可。
/// 资源缺失时静默跳过——预热是优化，不是功能依赖。
/// </summary>
public static class CrtFontPrewarm
{
    private const string CharsetPath = "CrtCharset";

    /// <summary>HUD 实际用到的像素字号（CrtTheme.Style 传入的 px 值经 S() 换算）。</summary>
    /// <remarks>
    /// 必须覆盖所有实际字号：同一个字在 13px 与 26px 下是**两个独立字形**，
    /// 只预热 13px 的话标题（26px）上屏仍会触发一次光栅化。
    /// 新增字号时记得同步这里（CrtTheme.Style 的调用点用 grep 就能列全）。
    /// </remarks>
    private static readonly int[] Sizes = { 11, 12, 13, 14, 15, 16, 26, 30 };

    private static bool done;

    /// <summary>预热是否已完成（诊断/测试用）。</summary>
    public static bool Done { get { return done; } }

    /// <summary>最近一次预热的字符数与耗时，供加载屏显示与日志。</summary>
    public static string LastReport { get; private set; }

    /// <summary>
    /// 执行预热。可重复调用（已做过直接返回）。
    /// 返回实际预热的字符数（0 表示字符表缺失、已跳过）。
    /// </summary>
    public static int Run()
    {
        if (done) return 0;
        done = true;

        TextAsset asset = Resources.Load<TextAsset>(CharsetPath);
        if (asset == null || string.IsNullOrEmpty(asset.text))
        {
            LastReport = "字符表缺失（" + CharsetPath + "），跳过预热";
            Debug.LogWarning("[字体预热] " + LastReport
                             + "。跑一次菜单 FYP/UI/生成字体预热字符表 即可生成。");
            return 0;
        }

        Font font = CrtTheme.Mono;
        if (font == null || !font.dynamic)
        {
            // 静态字体不需要预热（字形随字体资产一起打包）
            LastReport = "字体非动态，无需预热";
            return 0;
        }

        string chars = asset.text;
        float t0 = Time.realtimeSinceStartup;

        for (int i = 0; i < Sizes.Length; i++)
            font.RequestCharactersInTexture(chars, Sizes[i], FontStyle.Normal);

        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        Texture atlas = font.material != null ? font.material.mainTexture : null;
        string size = atlas != null ? atlas.width + "×" + atlas.height : "?";

        LastReport = chars.Length + " 字 × " + Sizes.Length + " 字号，" + ms.ToString("F0") + "ms，图集 " + size;
        Debug.Log("[字体预热] 完成：" + LastReport);
        return chars.Length;
    }

    /// <summary>重置（域重载/重新开局后允许再预热一次）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        done = false;
        LastReport = null;
    }
}
