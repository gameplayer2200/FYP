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

    /// <summary>机器总数（谜题目标：昼侧 2 + 夜侧深区 1）。</summary>
    public const int MachineCount = 3;

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
        StartedMachines = 0;
        Order = null;
        lastTickFrame = -1;
        OnPhaseFlipped = null;
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
        if (seconds > 1f) ShiftSeconds = seconds;
    }

    /// <summary>控制器驱动（玩家身处工厂时才推进；帧号守卫防双常驻控制器同帧双扣）。</summary>
    public static void Tick(float delta)
    {
        if (PuzzleSolved || LockedPhase.HasValue) return; // 已冻结
        if (lastTickFrame == Time.frameCount) return;
        lastTickFrame = Time.frameCount;

        TimeLeft -= delta;
        if (TimeLeft > 0f) return;

        Current = Current == Phase.Day ? Phase.Night : Phase.Day;
        TimeLeft = ShiftSeconds;
        if (OnPhaseFlipped != null) OnPhaseFlipped();
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
        Order = new[] { 0, 1, 2 };
        for (int i = Order.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int t = Order[i]; Order[i] = Order[j]; Order[j] = t;
        }
    }
}
