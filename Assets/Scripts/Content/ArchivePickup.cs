using UnityEngine;

/// <summary>
/// 残档拾取物：散落在层域里的前人记录（纸片/终端卡/留言条）。
/// E 读取 → 弹出 CRT 阅读窗 → 读过的自动进图鉴，且**不会消失**（可以反复看）。
///
/// 【为什么不 Destroy】
/// 异常物（SpecimenPickup）捡走就没了，因为它是要带回实验室的货物；
/// 残档是"环境叙事信息"，它的价值在于随时回来复核——
/// 玩家经常在别处看到线索想再确认一遍上面的数字，东西不见了会很恼人。
/// 所以读完后物体保留，仅换一个提示文案（"已归档 · 按 E 重读"）。
///
/// 【放在哪个程序集】
/// 与 SpecimenPickup 并列，走 FYP.Content；它要调用 ArchiveLog（Core）与阅读窗（Content）。
/// </summary>
public class ArchivePickup : MonoBehaviour, IInteractable
{
    [Header("残档身份")]
    [Tooltip("对应 ArchiveLog 里的 id（写错会读不到，交互时给提示）")]
    [SerializeField] private string entryId = "metro_b1";

    [Header("交互")]
    [SerializeField] private float interactionRange = 1.5f;
    [Tooltip("未读时的提示")]
    [SerializeField] private string unreadHint = "阅读前人残档";
    [Tooltip("已读后的提示")]
    [SerializeField] private string readHint = "重读残档";

    public float InteractionRange { get { return interactionRange; } }
    public Transform GetInteractionPoint() { return transform; }

    private void OnEnable()
    {
        InteractionRegistry.Register(this);
        RefreshLabel();
    }

    private void OnDisable() { InteractionRegistry.Unregister(this); }

    /// <summary>提示文案跟着"读没读过"走：已读的显示"重读"，让玩家知道自己看过。</summary>
    private void RefreshLabel()
    {
        var label = GetComponent<InteractionLabel>();
        if (label == null) return;
        label.Text = ArchiveLog.IsRead(entryId) ? readHint : unreadHint;
    }

    public void Interact(GameObject player)
    {
        var entry = ArchiveLog.Get(entryId);
        if (entry == null)
        {
            HintToast.Show("残档内容缺失（id: " + entryId + "）");
            Debug.LogWarning("[ArchivePickup] 找不到残档 id=" + entryId + " 于 " + name);
            return;
        }

        bool isNew = ArchiveLog.MarkRead(entryId);
        ArchiveReader.Open(entry);
        RefreshLabel();

        if (isNew)
            HintToast.Show("残档《" + entry.title + "》已归档", 2.5f);
    }

    /// <summary>给编辑器工具/调试用：改 id 时同步提示。</summary>
    public string EntryId { get { return entryId; } }
}
