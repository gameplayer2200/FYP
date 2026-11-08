using System;
using UnityEngine;

/// <summary>
/// 工厂排班钟（Core：World 的控制器写，Content 的订单板读——分层合法，同 ExtractionClock 模式）。
/// 值班/沉睡模型（2026-10-22 拍板）：昼 ⇄ 夜每班 ShiftSeconds 轮换——在班的世界醒着
/// （机器可启动、夜侧深区门开放、异常显形），歇班的世界死寂（机器停摆、门锁死、交互拒绝）。
/// 不换班的代价 = 困在歇班的世界里等下一班（软轮换：不强制、不掉电）。
///
/// 谜题 = 值班室排班表按序启动 3 台机器（昼侧 2 + 夜侧深区 1，顺序每局随机，必然跨昼夜往返）；
/// 完成后轮换冻结，玩家在排班表锁定想要的昼夜——被锁的世界永久在班且缺陷消除
/// （锁昼 = 昼侧深区门也开 / 锁夜 = 异常平息），之后每 ManualSwitchCooldown 秒可手动改锁
/// （奖励：轮换控制权交给玩家）。
///
/// 双场景常驻：两侧 FactoryShiftController 在玩家身处对应世界时驱动 Tick（玩家离开工厂，
/// 轮换暂停——像 Metro 渐熄一样，世界状态不因玩家缺席推进）。
/// </summary>
public static class FactoryShift
{
    public enum Phase { Day, Night }

    /// <summary>机器总数（谜题目标：昼侧 2；夜侧深区送料机已随泵房移除，2026-11-08）。</summary>
    public const int MachineCount = 2;

    /// <summary>手动改锁冷却（秒）。</summary>
    public const float ManualSwitchCooldown = 60f;

    /// <summary>当前班次（锁定后恒等于 LockedPhase）。</summary>
    public static Phase Current { get; private set; }

    /// <summary>本班剩余秒数（锁定后不再推进）。</summary>
    public static float TimeLeft { get; private set; }

    /// <summary>每班时长（秒）；由场景里的 FactoryShiftController 写入（两侧同值）。</summary>
    public static float ShiftSeconds { get; private set; }

    /// <summary>谜题是否已完成（3 台机器按序启动）。</summary>
    public static bool PuzzleSolved { get; private set; }

    /// <summary>已锁定的班次；谜题完成后玩家在排班表做出选择才非空。</summary>
    public static Phase? LockedPhase { get; private set; }

    /// <summary>下一次允许手动改锁的时间（Time.unscaledTime 域；首次锁定不受限）。</summary>
    public static float NextSwitchAt { get; private set; }

    /// <summary>已按序启动的机器数。</summary>
    public static int StartedMachines { get; private set; }

    /// <summary>每局随机的启动顺序：Order[i] = 第 (i+1) 台该启动的机器编号（0/1=昼侧，2=夜侧深区）。</summary>
    public static int[] Order { get; private set; }

    /// <summary>换班时触发一次（仅在未锁定时）。</summary>
    public static event Action OnPhaseFlipped;

    /// <summary>
    /// 换班前多久开始预警（秒）。到时触发一次 <see cref="OnPhaseWarning"/>，给玩家反应时间。
    /// 强制时空切换必须有预警——否则玩家会觉得是"卡了一下"而不是"世界变了"。
    /// </summary>
    public static float WarningSeconds = 5f;

    /// <summary>本班已预警过的班次标记（防同一班重复预警；换班时清空）。</summary>
    private static bool warnedThisShift;

    /// <summary>预警触发一次（每班一次，仅在未锁定时）。</summary>
    public static event Action OnPhaseWarning;

    /// <summary>距离换班还有几秒（供挂钟/HUD 显示；已锁定时恒为 0）。</summary>
    public static float SecondsToFlip
    {
        get { return LockedPhase.HasValue || PuzzleSolved ? 0f : Mathf.Max(0f, TimeLeft); }
    }

    /// <summary>锁定变化（首次锁定 / 手动改锁）时触发。</summary>
    public static event Action OnLockChanged;

    /// <summary>错序复位时触发（已启动的机器听它停机）。</summary>
    public static event Action OnProgressReset;

    private static int lastTickFrame = -1;

    static FactoryShift()
    {
        ResetAll();
    }

    /// <summary>整局复位（最后一个 FactoryShiftController 销毁时调用，防跨 Play 会话残留）。</summary>
    public static void ResetAll()
    {
        Current = Phase.Day;
        ShiftSeconds = 240f;
        TimeLeft = ShiftSeconds;
        PuzzleSolved = false;
        LockedPhase = null;
        NextSwitchAt = 0f;
        NextManualFlipAt = 0f;
        StartedMachines = 0;
        Order = null;
        lastTickFrame = -1;
        warnedThisShift = false;
        OnPhaseFlipped = null;
        OnPhaseWarning = null;
        OnLockChanged = null;
        OnProgressReset = null;
    }

    /// <summary>某世界此刻是否醒着（在班，或被锁定）。</summary>
    public static bool IsAwake(Phase world)
    {
        return LockedPhase.HasValue ? LockedPhase.Value == world : Current == world;
    }

    /// <summary>夜侧异常是否显形：夜班进行中且尚未锁定（锁夜 = 异常平息）。</summary>
    public static bool AnomaliesActive
    {
        get { return !PuzzleSolved && Current == Phase.Night; }
    }

    /// <summary>控制器调用：同步班时长（Inspector 可调；两侧场景保持一致）。</summary>
    public static void Configure(float seconds)
    {
        if (seconds <= 1f) return;
        if (Mathf.Approximately(seconds, ShiftSeconds)) return;

        // 改班时长必须同时收住本轮剩余时间。
        //
        // 曾经踩过：ResetAll 给的是占位值 240，控制器第一帧 Configure(90) 只改了
        // ShiftSeconds，TimeLeft 仍是 240——于是第一班真跑了 240 秒，之后才 90 秒。
        // 玩家看到的是"第一次换班特别久"，而代码里搜不到 240（它只在 ResetAll 里）。
        // 收窄剩余时间不会把已过的时间还回去，也不会让玩家凭空多等一会儿。
        ShiftSeconds = seconds;
        if (TimeLeft > seconds) TimeLeft = seconds;
    }

    /// <summary>控制器驱动（玩家身处工厂时才推进；帧号守卫防双常驻控制器同帧双扣）。</summary>
    public static void Tick(float delta)
    {
        if (PuzzleSolved || LockedPhase.HasValue) return; // 已冻结
        if (lastTickFrame == Time.frameCount) return;
        lastTickFrame = Time.frameCount;

        TimeLeft -= delta;

        // 预警：进入 WarningSeconds 窗口时触发一次（每班一次）
        if (!warnedThisShift && TimeLeft > 0f && TimeLeft <= WarningSeconds)
        {
            warnedThisShift = true;
            if (OnPhaseWarning != null) OnPhaseWarning();
        }

        if (TimeLeft > 0f) return;

        Current = Current == Phase.Day ? Phase.Night : Phase.Day;
        TimeLeft = ShiftSeconds;
        warnedThisShift = false;
        if (OnPhaseFlipped != null) OnPhaseFlipped();
    }

    /// <summary>手动换班冷却（秒）。怀表是道具不是开关，按一次要等一会儿才能再按。</summary>
    public const float ManualFlipCooldown = 15f;

    public static float NextManualFlipAt { get; private set; }

    public static float ManualFlipCooldownLeft
    {
        get { return Mathf.Max(0f, NextManualFlipAt - Time.unscaledTime); }
    }

    public static bool ForceFlip()
    {
        if (LockedPhase.HasValue) return false;
        if (Time.unscaledTime < NextManualFlipAt) return false;
        Current = Current == Phase.Day ? Phase.Night : Phase.Day;
        TimeLeft = ShiftSeconds;
        warnedThisShift = false;
        NextManualFlipAt = Time.unscaledTime + ManualFlipCooldown;
        if (OnPhaseFlipped != null) OnPhaseFlipped();
        return true;
    }

    /// <summary>读表用：确保顺序已掷出（谜题未开始也能看排班表）。</summary>
    public static int[] PeekOrder()
    {
        EnsureOrder();
        return Order;
    }

    /// <summary>机器开关调用：按序启动。对序 true（满 3 台置 PuzzleSolved，轮换就此冻结）；错序 false（调用方演出火花并复位）。</summary>
    public static bool TryStart(int machineId)
    {
        EnsureOrder();
        if (Order[StartedMachines] != machineId) return false;

        StartedMachines++;
        if (StartedMachines >= MachineCount) PuzzleSolved = true;
        return true;
    }

    /// <summary>错序复位：全部机器重新排队（已启动的机器听 OnProgressReset 停机——进度回滚，排班表是唯一线索）。</summary>
    public static void ResetProgress()
    {
        if (StartedMachines == 0) return;
        StartedMachines = 0;
        if (OnProgressReset != null) OnProgressReset();
    }

    /// <summary>排班表调用：锁定 / 改锁 target。未解谜或冷却中返回 false（调用方提示原因）。</summary>
    public static bool SetLock(Phase target)
    {
        if (!PuzzleSolved) return false;
        if (LockedPhase == target) return false;
        if (Time.unscaledTime < NextSwitchAt) return false;

        LockedPhase = target;
        Current = target; // 排班钟停在这一班
        TimeLeft = ShiftSeconds;
        NextSwitchAt = Time.unscaledTime + ManualSwitchCooldown;
        if (OnLockChanged != null) OnLockChanged();
        return true;
    }

    /// <summary>改锁冷却剩余（秒，供提示）。</summary>
    public static float SwitchCooldownLeft
    {
        get { return Mathf.Max(0f, NextSwitchAt - Time.unscaledTime); }
    }

    private static void EnsureOrder()
    {
        if (Order != null && Order.Length == MachineCount) return;
        Order = new int[MachineCount];
        for (int i = 0; i < MachineCount; i++) Order[i] = i;
        for (int i = Order.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int t = Order[i]; Order[i] = Order[j]; Order[j] = t;
        }
    }
}
