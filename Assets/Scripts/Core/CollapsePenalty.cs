using System;

/// <summary>
/// 瘫倒惩罚钩子（Core：BatterySystem 在重启完成时触发，Inventory 等背包侧订阅执行）。
/// Player 程序集不能反向依赖 Content，经此解耦。呼应提案：提前离场/瘫倒 = 损失部分收益。
/// </summary>
public static class CollapsePenalty
{
    /// <summary>瘫倒重启完成时触发一次。</summary>
    public static event Action OnCollapseRestart;

    public static void Notify()
    {
        if (OnCollapseRestart != null) OnCollapseRestart();
    }
}
