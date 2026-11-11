using UnityEngine;

/// <summary>
/// 超级跳解锁闸（World）：中后期能力门。
///
/// 超级跳是「地铁 + 工厂」之后才解锁的探索能力（请求 4）。解锁条件 =
/// Metro 全站复明（MetroBlackout.PowerRestored）且工厂谜题解完（FactoryShift.PuzzleSolved）。
///
/// 为什么不放进 Core：MetroBlackout 在 World 程序集，Core 不能反向引用它。
/// 放在 World、挂到工厂场景常驻物体上，由它把「两条世界线都通关」翻译成
/// Core 的 PlayerGear.AcquireSuperJump()。
///
/// 顺序无关：两世界先做哪个都行——先 Metro 后工厂时，工厂加载靠静态快照
/// PowerRestored 判定；先工厂后 Metro 时靠 OnPowerRestored 事件；解谜瞬间靠
/// OnPuzzleSolved 事件。
/// </summary>
public class AbilityUnlockGate : MonoBehaviour
{
    private void OnEnable()
    {
        MetroBlackout.OnPowerRestored += HandlePowerRestored;
        FactoryShift.OnPuzzleSolved += HandlePuzzleSolved;

        // 晚加载/晚挂载：两条件可能早已成立，先查一次静态快照，别漏发。
        if (MetroBlackout.PowerRestored && FactoryShift.PuzzleSolved)
            PlayerGear.AcquireSuperJump();
    }

    private void OnDisable()
    {
        MetroBlackout.OnPowerRestored -= HandlePowerRestored;
        FactoryShift.OnPuzzleSolved -= HandlePuzzleSolved;
    }

    private void HandlePowerRestored()
    {
        if (FactoryShift.PuzzleSolved) PlayerGear.AcquireSuperJump();
    }

    private void HandlePuzzleSolved()
    {
        if (MetroBlackout.PowerRestored) PlayerGear.AcquireSuperJump();
    }
}
