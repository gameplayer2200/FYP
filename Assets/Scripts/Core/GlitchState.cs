using System;

/// <summary>
/// 「故障」共享状态（Core 静态，与 <see cref="EmfStorm"/> / <see cref="TensionMeter"/> 同一模式）：
/// 玩法侧写入、UI 侧读取，避免 World 与 Content 两个程序集互相引用（Content 不引用 World，见 ADR-0004）。
///
/// 设计意图：这一层不是"特效"，是**信息层面的不可靠**——
/// 玩家最依赖的三样东西（道具栏顺序、世界名字、听到的话）偶尔会撒一次谎，
/// 而游戏本身不解释、不纠正。恐怖感来自"我不确定刚才看到的是不是真的"，不是来自鬼跳出来。
///
/// 生命周期：由 <c>GlitchDirector</c> 在高张力时开关；<see cref="Reset"/> 在回实验室/域重载时清场。
/// </summary>
public static class GlitchState
{
    /// <summary>故障类型。每种在 UI 上的表现形式不同，见各写入方的注释。</summary>
    public enum Kind
    {
        None = 0,
        /// <summary>道具栏顺序错乱：第 1 格显示的是第 3 格的东西。</summary>
        InventorySort = 1,
        /// <summary>世界名字被替换成一个"不该存在"的名字。</summary>
        WorldName = 2,
        /// <summary>幻听：不是系统播报，而是像另一个玩家在说话。</summary>
        PhantomChat = 3,
        /// <summary>玩家复制体：正前方站着一个和自己一样的身影。</summary>
        PlayerCopy = 4
    }

    /// <summary>当前正在发作的故障（None = 正常）。</summary>
    public static Kind Active { get; private set; }

    /// <summary>道具栏错位量（1~4）。仅 <see cref="Kind.InventorySort"/> 期间非 0。</summary>
    public static int SlotOffset { get; private set; }

    /// <summary>世界名替换文本。仅 <see cref="Kind.WorldName"/> 期间非空。</summary>
    public static string WorldAlias { get; private set; }

    /// <summary>故障开始/结束时触发（参数 = 是否发作中），供 UI 做瞬时抖动。</summary>
    public static event Action<bool> OnChanged;

    public static bool IsActive { get { return Active != Kind.None; } }

    /// <summary>开始一次故障。<paramref name="slotOffset"/> 与 <paramref name="worldAlias"/> 按类型取用。</summary>
    public static void Begin(Kind kind, int slotOffset = 0, string worldAlias = null)
    {
        if (kind == Kind.None) return;
        Active = kind;
        SlotOffset = kind == Kind.InventorySort ? NormalizeOffset(slotOffset) : 0;
        WorldAlias = kind == Kind.WorldName ? worldAlias : null;
        var h = OnChanged;
        if (h != null) h(true);
    }

    /// <summary>结束故障：UI 必须回到真实状态，不留任何"错位"残影。</summary>
    public static void Clear()
    {
        if (Active == Kind.None && SlotOffset == 0 && string.IsNullOrEmpty(WorldAlias)) return;
        Active = Kind.None;
        SlotOffset = 0;
        WorldAlias = null;
        var h = OnChanged;
        if (h != null) h(false);
    }

    /// <summary>域重载 / 回实验室：彻底复位（含事件订阅方已销毁的情况）。</summary>
    public static void Reset()
    {
        Active = Kind.None;
        SlotOffset = 0;
        WorldAlias = null;
        OnChanged = null;
    }

    /// <summary>把错位量收进 1..(SlotCount-1)：0 或整圈等于没变，要避免。</summary>
    private static int NormalizeOffset(int raw)
    {
        int n = PlayerGear.SlotCount;
        if (n <= 1) return 0;
        int v = raw % n;
        if (v < 0) v += n;
        if (v == 0) v = 1;
        return v;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = Kind.None;
        SlotOffset = 0;
        WorldAlias = null;
        OnChanged = null;
    }
}
