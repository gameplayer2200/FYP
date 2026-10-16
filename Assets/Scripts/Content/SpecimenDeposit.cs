using UnityEngine;

/// <summary>
/// 实验室收容台：靠近 E，把随身携带的异常物全部存入收容（携带 → 已收容）。
/// 对应提案"带回实验室才算收益"的结算口径。
/// </summary>
public class SpecimenDeposit : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactionRange = 2.5f;

    public float InteractionRange { get { return interactionRange; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        var inv = Inventory.Instance;
        if (inv == null) return;
        int n = inv.StoreAllSpecimens();
        if (n <= 0) HintToast.Show("没有待收容的异常物");
        else HintToast.Show("已收容 " + n + " 件异常物（累计 " + inv.StoredSpecimenCount + "）");
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
