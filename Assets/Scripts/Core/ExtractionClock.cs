using System;
using UnityEngine;

/// <summary>
/// 撤离窗口（Core：World 的计时器写，Content 的订单板读——分层合法）。
/// 进站武装、离站复位；归零广播 OnLocked（ExtractionTimer 锁死归途门）。
/// 呼应提案核心循环：「在归途门锁死前撤回实验室」。
/// </summary>
public static class ExtractionClock
{
    /// <summary>窗口进行中（玩家身处层域）。</summary>
    public static bool Armed { get; private set; }

    /// <summary>剩余秒数；≤0 = 已锁死（保持 Armed 直到离站复位）。</summary>
    public static float Remaining { get; private set; }

    /// <summary>窗口归零时触发一次（锁门方订阅）。</summary>
    public static event Action OnLocked;

    /// <summary>进站时武装窗口。已武装则不重复计时。</summary>
    public static void Arm(float seconds)
    {
        if (Armed) return;
        Armed = true;
        Remaining = seconds;
    }

    /// <summary>每帧推进（由 ExtractionTimer 驱动）。</summary>
    public static void Tick(float delta)
    {
        if (!Armed || Remaining <= 0f) return;
        Remaining -= delta;
        if (Remaining <= 0f)
        {
            Remaining = 0f;
            if (OnLocked != null) OnLocked();
        }
    }

    /// <summary>离站复位（回实验室 / 瘫倒重启后），下一次进站重新计时。</summary>
    public static void Reset()
    {
        Armed = false;
        Remaining = 0f;
    }
}
