using System;
using UnityEngine;

/// <summary>
/// 玩家随身装备登记表（静态，与 PlayerRef / InteractionRegistry 同构）：
/// 记录探索道具（万用表等）的持有状态。
/// 放在 Core：World（配电箱测线）与 Content（拾取物）都要读写，
/// 两者都已依赖 Core，且不引入对背包程序集（Content）的反向依赖。
/// </summary>
public static class PlayerGear
{
    /// <summary>是否持有万用表（电力检测仪）。</summary>
    public static bool HasMultimeter { get; private set; }

    /// <summary>是否持有手电筒（探索道具）。</summary>
    public static bool HasFlashlight { get; private set; }

    /// <summary>手电筒是否开着（世界规则据它判定"照明中"，如黑暗加成失效）。</summary>
    public static bool FlashlightOn { get; private set; }

    /// <summary>获得万用表时触发一次（UI / 演出订阅）。重复获得不触发。</summary>
    public static event Action OnMultimeterAcquired;

    /// <summary>手电筒开关切换时触发。</summary>
    public static event Action OnFlashlightChanged;

    public static void AcquireMultimeter()
    {
        if (HasMultimeter) return;
        HasMultimeter = true;
        if (OnMultimeterAcquired != null) OnMultimeterAcquired();
    }

    public static void AcquireFlashlight()
    {
        if (HasFlashlight) return;
        HasFlashlight = true;
    }

    public static void SetFlashlight(bool on)
    {
        if (!HasFlashlight || FlashlightOn == on) return;
        FlashlightOn = on;
        if (OnFlashlightChanged != null) OnFlashlightChanged();
    }

    /// <summary>当前选中的道具格（0=手电筒，1=万用表，2=异常物）。鼠标滚轮切换。</summary>
    public static int SelectedIndex { get; private set; }

    /// <summary>选中格变化时触发（UI 高亮用；UI 也可以直接轮询 SelectedIndex）。</summary>
    public static event Action OnSelectionChanged;

    /// <summary>滚轮切换选中格：dir&gt;0 下一个 / dir&lt;0 上一个；三格循环。</summary>
    public static void CycleSelection(int dir)
    {
        if (dir == 0) return;
        int next = ((SelectedIndex + (dir > 0 ? 1 : -1)) % 3 + 3) % 3;
        if (next == SelectedIndex) return;
        SelectedIndex = next;
        if (OnSelectionChanged != null) OnSelectionChanged();
    }
}
