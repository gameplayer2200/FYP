using UnityEngine;

/// <summary>
/// 换班钟（值班室落地钟）：E 直接强制换班。
/// 不走"先捡怀表、滚轮切格、再按 F"那三步——站在钟前按 E 才是最直觉的动作。
/// 第一次按 E 顺手把怀表交给玩家，之后离开值班室也能用滚轮+F 远程换班。
/// 不销毁本体：钟是场景地标，取走的是表芯，钟留着。交互范围 1.5m。
/// </summary>
public class ShiftWatchPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string firstHint = "钟一响，天就换——怀表收好，滚轮切到它、F 也能换";
    [SerializeField] private string flipHint = "拨动钟摆——换班";

    private bool gaveWatch;

    public float InteractionRange { get { return 1.5f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (FactoryShift.LockedPhase.HasValue)
        {
            HintToast.Show("排班钟已停——这个班次是你自己定下的。");
            return;
        }
        if (FactoryShift.ManualFlipCooldownLeft > 0.01f)
        {
            HintToast.Show("钟摆还在回摆——" + Mathf.CeilToInt(FactoryShift.ManualFlipCooldownLeft) + " 秒");
            return;
        }
        if (!FactoryShift.ForceFlip())
        {
            HintToast.Show("钟摆纹丝不动……");
            return;
        }
        if (!gaveWatch)
        {
            gaveWatch = true;
            PlayerGear.AcquireShiftWatch();
            if (player != null && player.GetComponentInChildren<ShiftWatch>(true) == null)
                player.AddComponent<ShiftWatch>();
            HintToast.Show(firstHint);
        }
        else
        {
            HintToast.Show(flipHint);
        }
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
