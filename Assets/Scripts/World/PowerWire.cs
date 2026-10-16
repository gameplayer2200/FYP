using UnityEngine;

/// <summary>
/// 配电箱里的一根线：持万用表时 E = 检测带电与否并把该线接入电闸；
/// 没有万用表只提示检不了。
/// </summary>
public class PowerWire : MonoBehaviour, IInteractable
{
    [SerializeField] private PowerCabinet cabinet;
    [SerializeField] private int wireIndex;
    [SerializeField] private string displayName = "红线";
    [SerializeField] private float interactionRange = 2.5f;

    public string DisplayName { get { return displayName; } }

    public float InteractionRange { get { return interactionRange; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (cabinet == null) return;
        if (!PlayerGear.HasMultimeter)
        {
            HintToast.Show("这根线看不出有没有电——需要万用表");
            return;
        }
        bool live = cabinet.IsLive(wireIndex);
        HintToast.Show(displayName + "：" + (live ? "带电！" : "没有电") + "（" + cabinet.Select(wireIndex) + "）");
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
