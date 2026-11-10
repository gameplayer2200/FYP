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
/// 2026-11 恢复三台：2026-11-08 删掉夜侧深区送料机后 MachineCount 降到 2，谜题退化成
/// "昼侧两台同层机器"——换班从此只打断、不推进（layout.md §4.3(a) 的主机制失效）。
/// 现在深区送料机回到夜侧深区，跨层接力重新成为解谜的必经一步。
///
/// 双场景常驻：两侧 FactoryShiftController 在玩家身处对应世界时驱动 Tick（玩家离开工厂，
/// 轮换暂停——像 Metro 渐熄一样，世界状态不因玩家缺席推进）。
/// </summary>
public static class FactoryShift
{
    public enum Phase { Day, Night }

    /// <summary>机器总数（谜题目标：昼侧 2 + 夜侧深区 1 = 3，必然跨昼夜往返）。</summary>
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

    /// <summary>预警窗口默认值（秒）：layout.md §4.2 三段式的"规划期"——玩家要在这段时间里决定</summary>
    /// <remarks>是现在冲完，还是搬到另一边接着做。不是提示，是决策时间。</remarks>
    public const float DefaultWarningSeconds = 20f;

    /// <summary>
    /// 换班前多久开始预警（秒）。到时触发一次 <see cref="OnPhaseWarning"/>，给玩家反应时间。
    /// 强制时空切换必须有预警——否则玩家会觉得是"卡了一下"而不是"世界变了"。
    /// 由场景里的 FactoryShiftController 经 Configure 同步（两侧同值）。
    /// </summary>
    public static float WarningSeconds = DefaultWarningSeconds;

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
        WarningSeconds = DefaultWarningSeconds;
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

    /// <summary>控制器调用：同步班时长与预警窗口（Inspector 可调；两侧场景保持一致）。</summary>
    /// <param name="seconds">每班时长（秒）。</param>
    /// <param name="warningSeconds">换班前预警窗口（秒）；传 &lt;= 0 表示不改。</param>
    public static void Configure(float seconds, float warningSeconds = -1f)
    {
        // 预警窗口先同步：它不依赖班时长，也要能被单独调。
        if (warningSeconds > 0f && !Mathf.Approximately(warningSeconds, WarningSeconds))
        {
            // 预警窗口不能吃掉整班：夹到班时长的一半以内，否则"刚接班就开始预警"，
            // 三段式里的稳定期会被压没。
            float cap = Mathf.Max(1f, seconds * 0.5f);
            WarningSeconds = Mathf.Min(warningSeconds, cap);
        }

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
        LastFlipWasManual = false; // 排班钟到点自动换：玩家没动手
        if (OnPhaseFlipped != null) OnPhaseFlipped();
    }

    /// <summary>
    /// 本次换班是玩家手动拨的（怀表 / 值班钟）还是排班钟到点自动换的。
    /// 订阅 OnPhaseFlipped 的一侧靠它区分——首次「强制」换班要发怀表，
    /// 而首次换班必然是被排班钟自动推过去的（那会儿玩家还没表）。
    /// </summary>
    public static bool LastFlipWasManual { get; private set; }

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
        LastFlipWasManual = true; // 玩家自己拨的（怀表 / 值班钟）
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
