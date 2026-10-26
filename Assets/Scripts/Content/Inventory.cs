using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局道具栏：管理门把手与异常物的持有数量。挂在常驻场景对象上。
/// 异物档案：拾取物自带档案（名/行为记录），随身携带→收容入账后进入收容数据库（图鉴）。
/// </summary>
public class Inventory : MonoBehaviour
{
    /// <summary>一件异常物的收容档案（图鉴条目）。</summary>
    public struct SpecimenArch
    {
        public string name;
        public string flavor;
    }

    public static Inventory Instance { get; private set; }
    public static event System.Action<int> OnKnobChanged;
    /// <summary>(携带数, 已收容数)。异常物只有送进实验室收容台才从携带转已收容。</summary>
    public static event System.Action<int, int> OnSpecimenChanged;
    /// <summary>收容台入账时广播（本次入账件数）——电费账单等结算演出订阅。</summary>
    public static event System.Action<int> OnDeposit;

    [SerializeField] private int knobCapacity = 3;
    [Tooltip("随身可携带的异常物上限")]
    [SerializeField] private int specimenCapacity = 3;

    public int KnobCount { get; private set; }
    public int KnobCapacity { get { return knobCapacity; } }
    public int SpecimenCount { get; private set; }
    public int StoredSpecimenCount { get; private set; }
    public int SpecimenCapacity { get { return specimenCapacity; } }

    // 随身档案队列（与 SpecimenCount 同步；瘫倒散失从队尾丢）
    private readonly List<SpecimenArch> pending = new List<SpecimenArch>();
    // 收容数据库（已入账，图鉴读）
    private readonly List<SpecimenArch> archive = new List<SpecimenArch>();

    /// <summary>收容数据库（已入账档案，图鉴用）。</summary>
    public IReadOnlyList<SpecimenArch> Archive { get { return archive; } }

    private void Awake()
    {
        // 销毁重复的**组件**而不是整个 GameObject——本脚本与 InventoryUI 同挂 WorldManager，
        // 早先写的 Destroy(gameObject) 会连带把 WorldManager 和上面的 UI 一起删掉。
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable() { CollapsePenalty.OnCollapseRestart += HalveCarrying; }
    private void OnDisable() { CollapsePenalty.OnCollapseRestart -= HalveCarrying; }

    /// <summary>瘫倒 = 撤离失败：随身异常物散失一半（向下取整）；已收容的不动。原不原地重启都照散，参数用不上。</summary>
    private void HalveCarrying(bool returnedHome)
    {
        if (SpecimenCount <= 0) return;
        int lost = SpecimenCount / 2;
        if (lost <= 0) return;
        SpecimenCount -= lost;
        for (int i = 0; i < lost && pending.Count > 0; i++) pending.RemoveAt(pending.Count - 1);
        NotifySpecimen();
        HintToast.Show("瘫倒重启——随身异常物散失 " + lost + " 件");
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

    /// <summary>异常物拾取物调用：随身收容一件异常物（档案可缺省，缺省显示"未登记样本"）。</summary>
    public bool AddSpecimen(SpecimenArch arch)
    {
        if (SpecimenCount >= specimenCapacity) return false;
        SpecimenCount++;
        pending.Add(arch);
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
        archive.AddRange(pending);
        pending.Clear();
        NotifySpecimen();
        if (OnDeposit != null) OnDeposit(n);
        return n;
    }

    // ── 收容台两段式暂存（放入箱内 → 结算入账）──────────────────────────
    // 玩家把随身异常物「放进箱子」时，它们既不算随身、也不算已收容——挂在箱内等结算。
    // 这样瘫倒散失碰不到已放进箱子的货（箱子在实验室里，安全），
    // 而图鉴与电费账单要等第二次按 E 演完开合动画才更新。
    private readonly List<SpecimenArch> staged = new List<SpecimenArch>();

    /// <summary>箱内暂存件数（放入后、结算前）。</summary>
    public int StagedCount { get { return staged.Count; } }

    /// <summary>箱内暂存档案（结算前的展示用）。</summary>
    public IReadOnlyList<SpecimenArch> Staged { get { return staged; } }

    /// <summary>收容台第一段：随身异常物全部挪进箱内暂存（尚未入账）。返回件数。</summary>
    public int StageAllSpecimens()
    {
        if (SpecimenCount <= 0) return 0;
        int n = SpecimenCount;
        staged.AddRange(pending);
        pending.Clear();
        SpecimenCount = 0;
        NotifySpecimen();
        return n;
    }

    /// <summary>收容台第二段：箱内暂存转入已收容（真正结算）。返回件数。</summary>
    public int CommitStaged()
    {
        if (staged.Count <= 0) return 0;
        int n = staged.Count;
        StoredSpecimenCount += n;
        archive.AddRange(staged);
        staged.Clear();
        NotifySpecimen();
        if (OnDeposit != null) OnDeposit(n);
        return n;
    }

    private void NotifySpecimen()
    {
        if (OnSpecimenChanged != null) OnSpecimenChanged(SpecimenCount, StoredSpecimenCount);
    }
}
