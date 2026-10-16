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

    /// <summary>获得万用表时触发一次（UI / 演出订阅）。重复获得不触发。</summary>
    public static event Action OnMultimeterAcquired;

    public static void AcquireMultimeter()
    {
        if (HasMultimeter) return;
        HasMultimeter = true;
        if (OnMultimeterAcquired != null) OnMultimeterAcquired();
    }
}
