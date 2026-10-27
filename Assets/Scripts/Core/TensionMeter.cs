using System;
using UnityEngine;

/// <summary>
/// 恐怖张力值（Core 静态：World 的张力导演写、HUD/叠层读——与 EmfStorm 同构的分层解耦）。
///
/// 设计来源：参考工程 §2.2「恐怖张力系统」——每个房间自带 <c>MinTensionInCell</c> 与
/// <c>IncreaseTensionSpeed</c>，玩家停留越久张力越高，张力再驱动环境音与随机异常事件。
///
/// 本项目的简化模型：**在层域里时间就是压力**。
///   · 进层域从 <see cref="EntryValue"/> 起步，随停留时间线性上升；
///   · 回实验室立刻衰减，归零后保持静止（家是安全的）；
///   · 手电筒开着、光照充足时上升变慢（<see cref="LightRelief"/>），
///     黑暗里独自走动时上升加速——这让"省电关灯"变成一个真实的恐怖代价，
///     和已有的黑暗 ×1.5 掉电加成形成同一条压力轴。
///
/// 消费方：<c>TensionDirector</c>（推进/触发事件）、<c>CrtOverlay</c>（画面畸变）、
/// <c>OrderBoard</c> 可读 <see cref="Level"/> 显示当班压力。
/// </summary>
public static class TensionMeter
{
    /// <summary>0~1 的当前张力。</summary>
    public static float Value { get; private set; }

    /// <summary>分档（供 HUD 与事件门槛使用）。</summary>
    public static TensionLevel Level { get; private set; }

    /// <summary>数值变化时广播（每帧最多一次，值没变就不发）。</summary>
    public static event Action<float> OnChanged;

    /// <summary>跨档时广播（只在真的换档时发，避免每帧刷）。</summary>
    public static event Action<TensionLevel> OnLevelChanged;

    /// <summary>进层域的起始张力（已经有点不对劲了，但不是满值）。</summary>
    public const float EntryValue = 0.12f;

    /// <summary>分档门槛。</summary>
    public const float UneasyAt = 0.35f;
    public const float TenseAt = 0.65f;
    public const float CriticalAt = 0.88f;

    /// <summary>光照充足时的上升速率缩放（开灯/亮处不那么容易崩）。</summary>
    public const float LightRelief = 0.55f;

    private static float lastBroadcast = -1f;

    /// <summary>回实验室时调用：张力衰减到 0。</summary>
    public static void Reset()
    {
        Set(0f, true);
    }

    /// <summary>进层域时调用：若当前低于起步值则抬到起步值。</summary>
    public static void BeginExpedition()
    {
        if (Value < EntryValue) Set(EntryValue, true);
    }

    /// <summary>
    /// 每帧推进。<paramref name="risePerSecond"/> 是基础上升速率（由 TensionDirector 给），
    /// <paramref name="relief"/> 是 0~1 的缓解系数（1 = 全速上升，0 = 完全不涨）。
    /// </summary>
    public static void Tick(float delta, float risePerSecond, float relief)
    {
        if (delta <= 0f) return;
        float rate = risePerSecond * Mathf.Clamp01(relief);
        Set(Value + rate * delta, false);
    }

    /// <summary>衰减（回实验室时用）。</summary>
    public static void Decay(float delta, float fallPerSecond)
    {
        if (delta <= 0f) return;
        Set(Value - fallPerSecond * delta, false);
    }

    /// <summary>事件冲击：一次加一截（突然的惊吓比时间流逝更有效）。</summary>
    public static void Add(float amount)
    {
        if (amount <= 0f) return;
        Set(Value + amount, true);
    }

    /// <summary>直接设定（调试/存档用）。</summary>
    public static void Set(float value, bool forceBroadcast)
    {
        float v = Mathf.Clamp01(value);
        bool changed = !Mathf.Approximately(v, Value);
        Value = v;

        TensionLevel lv = LevelOf(v);
        bool levelChanged = lv != Level;
        Level = lv;

        if (!changed && !forceBroadcast && !levelChanged) return;
        if (OnChanged != null) OnChanged(Value);
        if (levelChanged && OnLevelChanged != null) OnLevelChanged(Level);
        lastBroadcast = Time.unscaledTime;
    }

    /// <summary>最近一次广播的时刻（诊断用）。</summary>
    public static float LastBroadcastTime { get { return lastBroadcast; } }

    public static TensionLevel LevelOf(float v)
    {
        if (v >= CriticalAt) return TensionLevel.Critical;
        if (v >= TenseAt) return TensionLevel.Tense;
        if (v >= UneasyAt) return TensionLevel.Uneasy;
        return TensionLevel.Calm;
    }

    /// <summary>供 HUD 的中文档位名。</summary>
    public static string LevelName(TensionLevel lv)
    {
        switch (lv)
        {
            case TensionLevel.Uneasy: return "不安";
            case TensionLevel.Tense: return "紧逼";
            case TensionLevel.Critical: return "濒临失控";
            default: return "平静";
        }
    }

    /// <summary>域重载后清场（静态值会跨 Play 残留，和 EmfStorm 同样的处理）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Value = 0f;
        Level = TensionLevel.Calm;
        OnChanged = null;
        OnLevelChanged = null;
        lastBroadcast = -1f;
    }
}

public enum TensionLevel
{
    Calm = 0,
    Uneasy = 1,
    Tense = 2,
    Critical = 3
}
