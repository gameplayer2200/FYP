using UnityEngine;

/// <summary>
/// 异常物拾取物：层域中要回收的目标物（记忆核心软盘等）。E 拾取入随身收容，
/// 带回实验室收容台（SpecimenDeposit）入账才算收益。
/// 模型建议：items/RandomObjects/floppydisk。
/// </summary>
public class SpecimenPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private string pickupHint = "回收异常物——带回实验室收容台才能入账";
    [Tooltip("缓慢自转，提高暗处的可发现性")]
    [SerializeField] private bool spin = true;

    private void Update()
    {
        if (spin) transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
    }

    public float InteractionRange { get { return interactionRange; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (Inventory.Instance == null) return;
        if (!Inventory.Instance.AddSpecimen())
        {
            HintToast.Show("随身收容已满（" + Inventory.Instance.SpecimenCapacity + " 件）");
            return;
        }
        HintToast.Show(pickupHint);
        Destroy(gameObject);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
