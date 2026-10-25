using UnityEngine;

/// <summary>
/// 任务提示板（AI 数据扫描仪）：F 开关右上角订单 HUD，开着持续耗电。
///
/// 为什么是"道具 + 电费"而不是"常驻 UI"：订单信息（值班订单、收容进度、撤离倒计时）
/// 是玩家判断下一步该往哪走的唯一依据，白给等于把探索压力拿掉。做成手持终端后，
/// 想看得先把 F 按下去、并且每秒付电费——和手电筒"照亮但费电"是同一套代价逻辑。
/// 组件由 OrderTerminalPickup 在拾取时挂到玩家根上，无需场景预摆。
///
/// F 是全项目统一的"使用道具"键，一次只触发一件：
/// 只有滚轮选中任务提示板格（3）时 F 才归它（见 PlayerGear.UseKeyOwner）。
/// </summary>
[DisallowMultipleComponent]
public class OrderTerminal : MonoBehaviour
{
    [SerializeField] private KeyCode useKey = KeyCode.F;
    [Tooltip("开机时每秒耗电")]
    [SerializeField] private float drainPerSecond = 1.2f;

    private void Update()
    {
        if (!Input.GetKeyDown(useKey)) return;
        if (PlayerGear.UseKeyOwner != PlayerGear.OrderTerminalSlot) return;
        Toggle();
    }

    /// <summary>开关任务提示板（拾取后生效；测试可直接调用）。</summary>
    public void Toggle()
    {
        if (!PlayerGear.HasOrderTerminal)
        {
            HintToast.Show("没有任务提示板——实验室出发前记得带上");
            return;
        }

        // 开机才要电：电量已经见底时不给开，否则玩家会以为按键坏了
        if (!PlayerGear.OrderTerminalOn)
        {
            var battery = BatterySystem.Instance;
            if (battery == null || battery.IsDead)
            {
                HintToast.Show("电量耗尽——任务提示板开不了机");
                return;
            }
        }

        PlayerGear.SetOrderTerminal(!PlayerGear.OrderTerminalOn);
        ApplyState();
    }

    private void ApplyState()
    {
        if (BatterySystem.Instance != null)
            BatterySystem.Instance.SetTerminalDrain(PlayerGear.OrderTerminalOn ? drainPerSecond : 0f);

        HintToast.Show(PlayerGear.OrderTerminalOn
            ? "任务提示板：开（右上角显示订单，耗电中）"
            : "任务提示板：关");
    }

    private void OnDisable()
    {
        // 玩家被禁用（双玩家守卫）或组件下线时，停掉耗电通道并收起 HUD
        if (BatterySystem.Instance != null) BatterySystem.Instance.SetTerminalDrain(0f);
        if (PlayerGear.OrderTerminalOn) PlayerGear.SetOrderTerminal(false);
    }
}
