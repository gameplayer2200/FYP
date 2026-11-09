using System;
using UnityEngine;

/// <summary>
/// 现实锚定值（Core 静态：World 的驱动器写、Content 的订单板/HUD 读——与 EmfStorm / TensionMeter 同构）。
///
/// 【它是什么】
/// 层域不是稳定存在的地方，玩家每进入一次，本地现实就裂开一点。
/// 锚定值满值 100 = 刚进场、这层域还算"记得自己本来长什么样"；
/// 归零 = 本地现实彻底崩解，门区撕毁，玩家被强制弹回实验室，本轮回收作废。
///
/// 【为什么是混合型，而不是纯随时间流逝】
/// 纯计时会让玩家只想"快点走"，把探索变成赶路，任何一个犹豫的瞬间都在罚你。
/// 混合型取两者之长：
///   · **缓慢自然衰减**——待在层域里本身就有代价，但不至于逼你一路狂奔；
///   · **失误扣分才是主刀**——惊动敌人、触发异常、丢下装备都要真金白银地还，
///     于是"怎么走"比"走多快"更重要，这正是潜入类玩法想要的张力。
///
/// 【数值关系】
///   · 进层域从 <see cref="EntryValue"/>(100) 起；
///   · 基础衰减 <see cref="BaseFallPerSecond"/>(0.32/秒)，光待着约 312 秒见底；
///   · 张力加速：满张力时 ×<see cref="TensionFallScale"/>(1.8)，约 173 秒见底；
///   · 手电筒开着时衰减略缓（<see cref="LightRelief"/>）——照亮环境也是在确认这里真实存在；
///   · 失误扣分见 <see cref="Penalty"/> 的各档常量，单次 3~12 点；
///   · 收容一件异常物回升 <see cref="SpecimenRecovery"/>(18)——带回的东西才是真正的收益。
///
/// 消费方：<c>AnchorDirector</c>（推进/崩解触发）、<c>OrderBoard</c>（HUD 显示）、
/// 各失误源（怪物、异常事件、道具丢弃）调用 <see cref="Penalty"/>。
/// </summary>
public static class RealityAnchor
{
    /// <summary>满值（进场值）。</summary>
    public const float MaxValue = 100f;

    /// <summary>进层域的起始锚定值。</summary>
    public const float EntryValue = 100f;

    /// <summary>自然衰减（点/秒）。光待着约 312 秒见底——慢到不逼人赶路，但也不是无限。</summary>
    public const float BaseFallPerSecond = 0.32f;

    /// <summary>张力到顶时的衰减倍率（乘在基础值上）。</summary>
    public const float TensionFallScale = 1.8f;

    /// <summary>手电筒开着时的衰减缓解系数（0.72 = 只按七成速度掉）。</summary>
    public const float LightRelief = 0.72f;

    /// <summary>收容一件异常物回升的点数。</summary>
    public const float SpecimenRecovery = 18f;

    // ── 失误扣分档位（混合型的主刀）────────────────────────────────
    // 数字之间刻意拉开差距：被看见 > 被听见 > 自己失误，让玩家能记住"哪种错更贵"。

    /// <summary>被怪物看见（进入追猎）。最贵——它意味着你在这里留下了痕迹。</summary>
    public const float PenaltySpotted = 12f;

    /// <summary>触发异常事件（电磁风暴/幽灵进站等）。</summary>
    public const float PenaltyAnomaly = 8f;

    /// <summary>谜题操作失败（错序启动/接错线）。</summary>
    public const float PenaltyPuzzleFail = 6f;

    /// <summary>在层域里丢下装备（道具没带走）。</summary>
    public const float PenaltyDroppedGear = 4f;

    /// <summary>制造了明显噪音（跑动/跳跃被听见）。</summary>
    public const float PenaltyLoudNoise = 3f;

    /// <summary>警戒线（HUD 变黄）。</summary>
    public const float WarnAt = 45f;

    /// <summary>危急线（HUD 变红闪烁）。</summary>
    public const float CriticalAt = 18f;

    /// <summary>当前锚定值 0~100。</summary>
    public static float Value { get; private set; } = MaxValue;

    /// <summary>本轮是否已在层域里（进场后才计时）。</summary>
    public static bool Active { get; private set; }

    /// <summary>是否已崩解（归零后置位，直到回实验室或重新进场才清）。</summary>
    public static bool Collapsed { get; private set; }

    /// <summary>本轮累计因失误扣掉的总点数（结算界面用）。</summary>
    public static float TotalPenalty { get; private set; }

    /// <summary>数值变化时广播（每帧最多一次）。</summary>
    public static event Action<float> OnChanged;

    /// <summary>跨过警戒线 / 危急线时广播（只在真的跨线时发）。</summary>
    public static event Action<AnchorLevel> OnLevelChanged;

    /// <summary>归零时广播一次（强制撤离方订阅）。</summary>
    public static event Action OnCollapsed;

    /// <summary>失误扣分时广播（原因文本，HUD 可弹一行提示）。</summary>
    public static event Action<float, string> OnPenalty;

    private static AnchorLevel level = AnchorLevel.Stable;

    /// <summary>当前分档。</summary>
    public static AnchorLevel Level { get { return level; } }

    /// <summary>归一化值 0~1（HUD 条用）。</summary>
    public static float Normalized { get { return Mathf.Clamp01(Value / MaxValue); } }

    /// <summary>进层域：重置到满值并开始计时。</summary>
    public static void BeginExpedition()
    {
        Active = true;
        Collapsed = false;
        TotalPenalty = 0f;
        SetValue(EntryValue, true);
    }

    /// <summary>回实验室：停止计时（锚定值不再下降，但也不重置——重置发生在下次进场）。</summary>
    public static void EndExpedition()
    {
        Active = false;
    }

    /// <summary>完全复位（重启/调试）。</summary>
    public static void Reset()
    {
        Active = false;
        Collapsed = false;
        TotalPenalty = 0f;
        SetValue(MaxValue, true);
    }

    /// <summary>
    /// 每帧推进自然衰减。<paramref name="delta"/> 帧间隔；
    /// <paramref name="tension01"/> 当前张力 0~1（拿来加速衰减）；
    /// <paramref name="lit"/> 玩家是否照明中（手电筒开着）。
    /// </summary>
    public static void Tick(float delta, float tension01, bool lit)
    {
        if (delta <= 0f || !Active || Collapsed) return;

        float scale = Mathf.Lerp(1f, TensionFallScale, Mathf.Clamp01(tension01));
        float rate = BaseFallPerSecond * scale;
        if (lit) rate *= LightRelief;

        SetValue(Value - rate * delta, false);
        CheckCollapse();
    }

    /// <summary>
    /// 失误扣分（混合型主刀）。<paramref name="reason"/> 会广播给 HUD 显示。
    /// 只在层域里生效——在实验室里折腾不该罚。
    /// </summary>
    public static void Penalty(float amount, string reason)
    {
        if (!Active || Collapsed) return;
        if (amount <= 0f) return;

        TotalPenalty += amount;
        SetValue(Value - amount, true);

        // Core 只广播，不直接弹提示：HintToast 在 FYP.Interaction，
        // 反向引用会破坏 ADR-0004 的单向依赖（与 BillingLedger / EmfStorm 同一处理）。
        if (OnPenalty != null) OnPenalty(amount, reason);

        CheckCollapse();
    }

    /// <summary>收容一件异常物：锚定值回升。</summary>
    public static void RecoverFromSpecimen(float amount = SpecimenRecovery)
    {
        if (!Active || Collapsed) return;
        SetValue(Value + Mathf.Max(0f, amount), true);
    }

    /// <summary>外部事件直接扣（陷阱/环境异常调用，不带文案）。</summary>
    public static void Damaged(float amount)
    {
        Penalty(amount, null);
    }

    private static void CheckCollapse()
    {
        if (Value > 0f || Collapsed) return;
        Collapsed = true;
        SetValue(0f, true);
        if (OnCollapsed != null) OnCollapsed();
    }

    private static void SetValue(float v, bool force)
    {
        float nv = Mathf.Clamp(v, 0f, MaxValue);
        bool changed = !Mathf.Approximately(nv, Value);
        Value = nv;

        AnchorLevel nl = LevelOf(nv);
        bool levelChanged = nl != level;
        level = nl;

        if (!changed && !force && !levelChanged) return;
        if (OnChanged != null) OnChanged(Value);
        if (levelChanged && OnLevelChanged != null) OnLevelChanged(level);
    }

    public static AnchorLevel LevelOf(float v)
    {
        if (v <= CriticalAt) return AnchorLevel.Critical;
        if (v <= WarnAt) return AnchorLevel.Warn;
        return AnchorLevel.Stable;
    }

    /// <summary>供 HUD 的中文名。</summary>
    public static string LevelName(AnchorLevel lv)
    {
        switch (lv)
        {
            case AnchorLevel.Warn: return "松动";
            case AnchorLevel.Critical: return "即将崩解";
            default: return "锚定";
        }
    }

    /// <summary>域重载后清场（静态值跨 Play 残留）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Value = MaxValue;
        Active = false;
        Collapsed = false;
        TotalPenalty = 0f;
        level = AnchorLevel.Stable;
        OnChanged = null;
        OnLevelChanged = null;
        OnCollapsed = null;
        OnPenalty = null;
    }
}

public enum AnchorLevel
{
    Stable = 0,
    Warn = 1,
    Critical = 2
}
