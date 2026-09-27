using UnityEngine;
using UnityEngine.UI;

/// <summary>道具栏 UI：绑定层级里已有的 Slot1..3（InventoryCanvas/Slots 下）。
/// 拾取/消耗门把手时对应槽位点亮。</summary>
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Transform slotsParent; // 层级里的 Slots 节点

    private Image[] slots;
    private readonly Color emptyColor = new Color(0f, 0f, 0f, 0.45f);
    private readonly Color filledColor = new Color(0.22f, 0.86f, 0.95f, 0.9f); // 主题青

    private void Start()
    {
        CollectSlots();
        if (Inventory.Instance != null) Refresh(Inventory.Instance.KnobCount);
        Inventory.OnKnobChanged += Refresh;
    }

    private void OnDestroy()
    {
        Inventory.OnKnobChanged -= Refresh;
    }

    // 运行时若 inspector 没拖 parent，自动按名字找（InventoryCanvas/Slots）
    private void CollectSlots()
    {
        if (slotsParent == null)
        {
            var go = GameObject.Find("Slots");
            if (go != null) slotsParent = go.transform;
        }
        if (slotsParent == null) return;
        var list = new System.Collections.Generic.List<Image>();
        foreach (Transform child in slotsParent)
        {
            var img = child.GetComponent<Image>();
            if (img != null) list.Add(img);
        }
        slots = list.ToArray();
    }

    private void Refresh(int count)
    {
        if (slots == null || slots.Length == 0) CollectSlots();
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].color = i < count ? filledColor : emptyColor;
        }
    }
}
