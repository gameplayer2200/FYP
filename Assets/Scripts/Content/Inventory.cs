using UnityEngine;

/// <summary>全局道具栏：管理门把手的持有数量。挂在常驻场景对象上。</summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }
    public static event System.Action<int> OnKnobChanged;

    [SerializeField] private int knobCapacity = 3;
    public int KnobCount { get; private set; }
    public int KnobCapacity { get { return knobCapacity; } }

    private void Awake()
    {
        // 销毁重复的**组件**而不是整个 GameObject——本脚本与 InventoryUI 同挂 WorldManager，
        // 早先写的 Destroy(gameObject) 会连带把 WorldManager 和上面的 UI 一起删掉。
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool AddKnob()
    {
        if (KnobCount >= knobCapacity) return false;
        KnobCount++;
        if (OnKnobChanged != null) OnKnobChanged(KnobCount);
        return true;
    }

    public bool TryTakeKnob()
    {
        if (KnobCount <= 0) return false;
        KnobCount--;
        if (OnKnobChanged != null) OnKnobChanged(KnobCount);
        return true;
    }
}
