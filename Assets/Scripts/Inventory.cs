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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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
