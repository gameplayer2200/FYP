using System;
using UnityEngine;

/// <summary>
/// 玩家随身装备登记表（静态，与 PlayerRef / InteractionRegistry 同构）：
/// 记录探索道具（万用表等）的持有状态。
/// 放在 Core：World（配电箱测线）与 Content（拾取物）都要读写，
/// 两者都已依赖 Core，且不引入对背包程序集（Content）的反向依赖。
/// </summary>
public static class PlayerGear
{
    /// <summary>是否持有万用表（电力检测仪）。</summary>
    public static bool HasMultimeter { get; private set; }

    /// <summary>是否持有手电筒（探索道具）。</summary>
    public static bool HasFlashlight { get; private set; }

    /// <summary>手电筒是否开着（世界规则据它判定"照明中"，如黑暗加成失效）。</summary>
    public static bool FlashlightOn { get; private set; }

    /// <summary>是否持有探测器（探索道具，非接触式把异常扫显形）。</summary>
    public static bool HasEmfDetector { get; private set; }

    /// <summary>获得万用表时触发一次（UI / 演出订阅）。重复获得不触发。</summary>
    public static event Action OnMultimeterAcquired;

    /// <summary>手电筒开关切换时触发。</summary>
    public static event Action OnFlashlightChanged;

    /// <summary>获得探测器时触发一次（UI / 演出订阅）。重复获得不触发。</summary>
    public static event Action OnEmfDetectorAcquired;

    public static void AcquireMultimeter()
    {
        if (HasMultimeter) return;
        HasMultimeter = true;
        if (OnMultimeterAcquired != null) OnMultimeterAcquired();
    }

    public static void AcquireFlashlight()
    {
        if (HasFlashlight) return;
        HasFlashlight = true;
    }

    public static void AcquireEmfDetector()
    {
        if (HasEmfDetector) return;
        HasEmfDetector = true;
        if (OnEmfDetectorAcquired != null) OnEmfDetectorAcquired();
    }

    public static void SetFlashlight(bool on)
    {
        if (!HasFlashlight || FlashlightOn == on) return;
        FlashlightOn = on;
        if (OnFlashlightChanged != null) OnFlashlightChanged();
    }

    /// <summary>
    /// 是否持有任务提示板（AI 数据扫描仪）。
    ///
    /// 它和手电筒/万用表/探测器一样只是"道具栏里的一格"，不等于"一直在看"：
    /// 持有只代表能选中它，真正打开右上角订单信息要靠 OrderTerminalOn（按 F 开、持续耗电）。
    /// </summary>
    public static bool HasOrderTerminal { get; private set; }

    /// <summary>
    /// 任务提示板是否正在使用（右上角订单 HUD 的唯一开关）。
    ///
    /// 为什么不做成"拿到就永久显示"：订单板最初是常驻屏幕的，等于玩家一进实验室
    /// 就有块面板挂在右上角——那是 UI 而不是道具。改成手持终端后，玩家要主动掏出来看、
    /// 而且要付电费，这块信息才重新变成"代价"。
    /// </summary>
    public static bool OrderTerminalOn { get; private set; }

    /// <summary>获得任务提示板时触发一次（道具栏图标据此点亮）。重复获得不触发。</summary>
    public static event Action OnOrderTerminalAcquired;

    /// <summary>任务提示板开关切换时触发。</summary>
    public static event Action OnOrderTerminalChanged;

    public static void AcquireOrderTerminal()
    {
        if (HasOrderTerminal) return;
        HasOrderTerminal = true;
        if (OnOrderTerminalAcquired != null) OnOrderTerminalAcquired();
    }

    public static void SetOrderTerminal(bool on)
    {
        if (!HasOrderTerminal || OrderTerminalOn == on) return;
        OrderTerminalOn = on;
        if (OnOrderTerminalChanged != null) OnOrderTerminalChanged();
    }

    /// <summary>调试用：读档/重开时清空持有状态（正常游玩不调用）。</summary>
    public static void ResetOrderTerminal()
    {
        HasOrderTerminal = false;
        OrderTerminalOn = false;
    }

    /// <summary>道具格总数（手电筒 / 万用表 / 探测器 / 任务提示板 / 备用空位）。</summary>
    public const int SlotCount = 5;

    /// <summary>任务提示板所在的格位。</summary>
    public const int OrderTerminalSlot = 3;

    /// <summary>当前选中的道具格（0=手电筒，1=万用表，2=探测器，3=任务提示板，4=空）。鼠标滚轮切换。</summary>
    public static int SelectedIndex { get; private set; }

    /// <summary>选中格变化时触发（UI 高亮用；UI 也可以直接轮询 SelectedIndex）。</summary>
    public static event Action OnSelectionChanged;

    /// <summary>滚轮切换选中格：dir&gt;0 下一个 / dir&lt;0 上一个；SlotCount 格循环。</summary>
    public static void CycleSelection(int dir)
    {
        if (dir == 0) return;
        int n = Mathf.Max(1, SlotCount);
        int next = ((SelectedIndex + (dir > 0 ? 1 : -1)) % n + n) % n;
        if (next == SelectedIndex) return;
        SelectedIndex = next;
        // 换格**不**关闭已开的道具：手电筒与任务提示板都是"开关式"设备，
        // 开一次就一直开着（各自再按一次 F 才关），换格只是换 F 键归谁。
        // 若在这里强制关机，玩家换个格子看背包就会把灯关掉，是很反直觉的惩罚。
        if (OnSelectionChanged != null) OnSelectionChanged();
    }

    /// <summary>
    /// F 键当前归哪一格用 = 就是滚轮选中的那一格。
    ///
    /// 全项目只有 F 一个"使用道具"键，而一次按键只准触发一件道具，
    /// 所以这条规则必须只有一处真值：三件道具各自先问这里"F 是不是我的"，
    /// 免得出现一次 F 同时开灯又扫描。
    /// 手里没有那一件时不做回落——由各自组件给提示（"没有手电筒……"）。
    /// </summary>
    public static int UseKeyOwner { get { return SelectedIndex; } }
}
