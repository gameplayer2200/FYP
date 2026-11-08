using UnityEngine;

/// <summary>
/// 换班怀表（工厂道具）：F 强制昼夜互换。
///
/// 为什么是"道具"而不是按一下就切的快捷键：工厂的核心压力是排班钟自己走，
/// 玩家只能等。怀表把这个权力交到玩家手里，但要占一格道具栏、要等冷却——
/// 于是它变成"什么时候用最划算"的决策，而不是一个随手的开关。
///
/// 与排班表锁定的分工：怀表绕过钟（随时可翻），排班表锁定直接停钟（解谜奖励）。
/// 已锁定时怀表失效——玩家的最终选择不该被一块表推翻。
///
/// F 是全项目统一的"使用道具"键：只有滚轮选中怀表格（4）时才响应
/// （见 PlayerGear.UseKeyOwner），否则一次按键会同时切昼夜又开灯。
/// </summary>
public class ShiftWatch : MonoBehaviour
{
    [SerializeField] private KeyCode useKey = KeyCode.F;

    private void Update()
    {
        if (Input.GetKeyDown(useKey) && PlayerGear.UseKeyOwner == PlayerGear.ShiftWatchSlot) Use();
    }

    /// <summary>使用怀表（拾取后生效；测试可直接调用）。</summary>
    public void Use()
    {
        if (!PlayerGear.HasShiftWatch)
        {
            HintToast.Show("没有换班怀表——工厂值班室找找");
            return;
        }

        if (FactoryShift.LockedPhase.HasValue)
        {
            HintToast.Show("排班钟已停——已锁定的班次不能再翻");
            return;
        }

        if (FactoryShift.ManualFlipCooldownLeft > 0.05f)
        {
            HintToast.Show(string.Format("怀表还在回摆——{0:F0} 秒", Mathf.Ceil(FactoryShift.ManualFlipCooldownLeft)));
            return;
        }

        if (!FactoryShift.ForceFlip())
        {
            HintToast.Show("怀表没有反应……");
            return;
        }

        HintToast.Show(FactoryShift.Current == FactoryShift.Phase.Day
            ? "怀表一响——天亮了"
            : "怀表一响——入夜了");
    }
}
