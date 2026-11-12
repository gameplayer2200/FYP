using UnityEngine;

/// <summary>
/// 水淹区闭环里的任务道具拾取物（工具 / 燃料）。
/// 与 SpecimenPickup 的区别：不是异常物、不进背包、不会因瘫倒而丢失——只登记到 FloodedLoopState。
/// 用法：把模型挂在带 Collider 的空物体上，加本脚本，选 kind。
/// </summary>
public class FloodedPickup : MonoBehaviour, IInteractable
{
    [Tooltip("这件东西是什么：Tool=修理工具，Fuel=燃料")]
    [SerializeField] private FloodedItemKind kind = FloodedItemKind.Tool;

    [Tooltip("拾取时弹出的提示")]
    [SerializeField] private string pickupHint = "拿到了工具";

    [Tooltip("拾取后是否隐藏本物体（留一个空壳在原地也行，默认直接隐藏）")]
    [SerializeField] private bool hideOnPickup = true;

    public float InteractionRange { get { return 2.0f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (FloodedLoopState.Has(kind))
        {
            HintToast.Show("已经拿过了");
            return;
        }

        FloodedLoopState.Give(kind);
        HintToast.Show(string.IsNullOrEmpty(pickupHint) ? "拿到了东西" : pickupHint);

        if (hideOnPickup) gameObject.SetActive(false);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
