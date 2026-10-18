using UnityEngine;

/// <summary>
/// 装载耗时诊断（默认关闭，关闭时零开销）：给「世界装载链路」上几处可能卡帧的重活计时，
/// 每处打一行 [LoadDiag] 日志。配合 Assets/Editor/LoadHitchProbe.cs 的逐帧耗时记录，
/// 一次卡顿就能拆清楚到底是**激活**、**光影隔离**、**建取景**还是**首帧渲染**。
///
/// 打开方式：Play 中设 LoadDiag.Enabled = true（经 MCP execute_code 或探针设置）。
/// 为什么用 System.Diagnostics.Stopwatch 而不是 Time.realtimeSinceStartup：
/// 后者只有帧级精度，同一帧里连着测几段会全是 0ms，而这几段重活恰恰都挤在同一帧。
/// </summary>
public static class LoadDiag
{
    /// <summary>总开关：默认关（装载是性能敏感的路径，诊断只在排查时开）。</summary>
    public static bool Enabled;

    private static readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

    public static void Begin()
    {
        if (Enabled) sw.Restart();
    }

    public static void End(string label)
    {
        if (!Enabled) return;
        sw.Stop();
        Debug.Log("[LoadDiag] " + label + "  " + sw.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
    }
}
