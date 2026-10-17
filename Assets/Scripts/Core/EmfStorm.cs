using System;
using UnityEngine;

/// <summary>
/// 电磁风暴状态（Core：World 的异常写，Content/Player 的 HUD 读——分层合法）。
/// 风暴中仪器不可信：所有 HUD 文本经 Scramble 乱码化、位置抖动。
/// </summary>
public static class EmfStorm
{
    private static readonly char[] Noise = { '▓', '▒', '█', '/', '\\', '|', '_', '·', '#', '*' };

    public static bool IsStorming { get; private set; }

    /// <summary>风暴开始/结束时触发一次（HUD/B 演出订阅）。</summary>
    public static event Action OnChanged;

    public static void Set(bool on)
    {
        if (IsStorming == on) return;
        IsStorming = on;
        if (OnChanged != null) OnChanged();
    }

    /// <summary>风暴中把文本随机替换成乱码块（每次调用结果不同 = 滋滋跳动）。</summary>
    public static string Scramble(string text)
    {
        if (!IsStorming || string.IsNullOrEmpty(text)) return text;
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] != ' ' && chars[i] != '\n' && UnityEngine.Random.value < 0.3f)
                chars[i] = Noise[UnityEngine.Random.Range(0, Noise.Length)];
        }
        return new string(chars);
    }

    /// <summary>风暴中 HUD 抖动偏移；平时为零。</summary>
    public static Vector2 Jitter()
    {
        return IsStorming
            ? new Vector2(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-2f, 2f))
            : Vector2.zero;
    }
}
