using UnityEngine;

/// <summary>
/// 保险箱里的钥匙：只有箱子打开后才会被激活，E 拾取后登记 HasKey。
/// 拿到钥匙后，门口的门锁（FloodedKeyhole）就能把门解开。
/// </summary>
public class FloodedKeyItem : MonoBehaviour, IInteractable
{
    [SerializeField] private string pickupHint = "拿到了钥匙——去把门锁打开";

    public float InteractionRange { get { return 1.8f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (FloodedLoopState.HasKey)
        {
            HintToast.Show("钥匙已经在你手里了");
            return;
        }

        FloodedLoopState.Give(FloodedItemKind.Key);
        HintToast.Show(pickupHint, 3f);
        gameObject.SetActive(false);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }

    /// <summary>小幅度上下浮动，提示「这个东西可以拿」。</summary>
    private void Update()
    {
        transform.Rotate(0f, 30f * Time.deltaTime, 0f, Space.World);
    }
}
