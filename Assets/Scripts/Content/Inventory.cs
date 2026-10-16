using UnityEngine;

/// <summary>全局道具栏：管理门把手与异常物的持有数量。挂在常驻场景对象上。</summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }
    public static event System.Action<int> OnKnobChanged;
    /// <summary>(携带数, 已收容数)。异常物只有送进实验室收容台才从携带转已收容。</summary>
    public static event System.Action<int, int> OnSpecimenChanged;

    [SerializeField] private int knobCapacity = 3;
    [Tooltip("随身可携带的异常物上限")]
    [SerializeField] private int specimenCapacity = 3;

    public int KnobCount { get; private set; }
    public int KnobCapacity { get { return knobCapacity; } }
    public int SpecimenCount { get; private set; }
    public int StoredSpecimenCount { get; private set; }
    public int SpecimenCapacity { get { return specimenCapacity; } }

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

    /// <summary>异常物拾取物调用：随身收容一件异常物。</summary>
    public bool AddSpecimen()
    {
        if (SpecimenCount >= specimenCapacity) return false;
        SpecimenCount++;
        NotifySpecimen();
        return true;
    }

    /// <summary>收容台调用：把随身携带的异常物全部转入已收容（带回实验室才算收益）。</summary>
    public int StoreAllSpecimens()
    {
        if (SpecimenCount <= 0) return 0;
        int n = SpecimenCount;
        StoredSpecimenCount += n;
        SpecimenCount = 0;
        NotifySpecimen();
        return n;
    }

    private void NotifySpecimen()
    {
        if (OnSpecimenChanged != null) OnSpecimenChanged(SpecimenCount, StoredSpecimenCount);
    }
}
