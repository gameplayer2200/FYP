using UnityEngine;

/// <summary>
/// 断电门控：挂在 Metro 的充电桩上。停电期间禁用充电桩本体，本组件顶替交互位提示断电；
/// 全站复明（MetroBlackout.OnPowerRestored）后恢复充电桩、自己退场。
/// </summary>
[RequireComponent(typeof(ChargeStation))]
public class RequiresPower : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private string powerlessHint = "充电桩没有电——车站恢复供电后可用";

    private ChargeStation station;
    private bool powered;

    public float InteractionRange { get { return interactionRange; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (!powered) HintToast.Show(powerlessHint);
    }

    private void Awake()
    {
        station = GetComponent<ChargeStation>();
    }

    private void OnEnable()
    {
        InteractionRegistry.Register(this);
        MetroBlackout.OnPowerRestored += HandlePowerRestored;
        if (MetroBlackout.PowerRestored) HandlePowerRestored();
        else if (station != null) station.enabled = false; // 停电期间充电桩本体下线
    }

    private void OnDisable()
    {
        InteractionRegistry.Unregister(this);
        MetroBlackout.OnPowerRestored -= HandlePowerRestored;
    }

    private void HandlePowerRestored()
    {
        if (powered) return;
        powered = true;
        if (station != null) station.enabled = true;
        enabled = false; // 让出交互位给充电桩
    }
}
