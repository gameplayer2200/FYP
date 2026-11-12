using UnityEngine;

/// <summary>
/// 异常物拾取物：层域中要回收的目标物（记忆核心软盘等）。E 拾取入随身收容，
/// 带回实验室收容台（SpecimenDeposit）入账才算收益。
/// 模型建议：Art/Props/RandomObjects/floppydisk。
/// </summary>
public class SpecimenPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string pickupHint = "回收异常物——带回实验室收容台才能入账";
    [Tooltip("缓慢自转（已按需求默认关闭：所有拾取物都不做旋转提示）")]
    [SerializeField] private bool spin = false;

    [Header("收容档案（图鉴条目，留空显示\"未登记样本\"）")]
    [SerializeField] private string specimenName = "";
    [Tooltip("行为记录——一句话环境叙事")]
    [SerializeField] private string flavor = "";

    private void Update()
    {
        if (spin) transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
    }

    // 拾取范围刻意收紧到"贴近才提示"：2.5m 时离得老远就弹 [E]，实机偏早（2026-10-18）。
    public float InteractionRange { get { return 1.5f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (Inventory.Instance == null) return;
        var arch = new Inventory.SpecimenArch { name = specimenName, flavor = flavor };
        if (!Inventory.Instance.AddSpecimen(arch))
        {
            HintToast.Show("随身收容已满（" + Inventory.Instance.SpecimenCapacity + " 件）");
            return;
        }
        // 文案红线：捡起只算「随身收容」，「收容台入账」必须等玩家真的把东西放回收容台
        //（旧文案在这里就写"收容台入账"，玩家会以为捡起瞬间就入了账——实测反馈的误导点）。
        HintToast.Show(string.IsNullOrEmpty(specimenName)
            ? pickupHint
            : "回收「" + specimenName + "」——已随身收容（" + Inventory.Instance.SpecimenCount + "/" + Inventory.Instance.SpecimenCapacity
              + "），带回实验室收容台才能入账");
        Destroy(gameObject);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
