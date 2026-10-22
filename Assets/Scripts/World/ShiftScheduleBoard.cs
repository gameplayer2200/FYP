using UnityEngine;

/// <summary>
/// 值班室排班表（工厂谜题中枢，昼/夜两场景各挂一份镜像）：E 读表——显示当班的机器
/// 启动顺序（每局随机，必然跨昼夜）。三台机器按序启动后，排班表变成「锁定器」：
/// E 锁定当前班次（首次），之后再 E 在昼/夜之间改锁（冷却 60 秒——奖励：轮换控制权交给玩家）。
/// </summary>
public class ShiftScheduleBoard : MonoBehaviour, IInteractable
{
    [Tooltip("机器显示名（下标 = 机器编号：0/1=昼侧主厅，2=夜侧深区）——与 MachinePowerSwitch.displayName 一致")]
    [SerializeField] private string[] machineNames = { "一号风机", "二号风机", "深区送料机" };

    [Tooltip("读表提示时长（秒）——顺序信息长，给足阅读时间")]
    [SerializeField] private float hintDuration = 7f;
    [SerializeField] private float interactionRange = 2.5f;

    public float InteractionRange { get { return interactionRange; } }
    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (!FactoryShift.PuzzleSolved)
        {
            var order = FactoryShift.PeekOrder();
            string line = "今日排班：" + NameOf(order[0]) + " → " + NameOf(order[1]) + " → " + NameOf(order[2]);
            if (FactoryShift.StartedMachines > 0)
                line += "（已启动 " + FactoryShift.StartedMachines + "/" + FactoryShift.MachineCount
                    + "，下一台 " + NameOf(order[FactoryShift.StartedMachines]) + "）";
            HintToast.Show(line, hintDuration);
            return;
        }

        // 谜题完成：排班表 = 锁定器。首次锁定当前班次；之后每次 E 在昼/夜间切换。
        var target = FactoryShift.LockedPhase.HasValue
            ? (FactoryShift.LockedPhase.Value == FactoryShift.Phase.Day ? FactoryShift.Phase.Night : FactoryShift.Phase.Day)
            : FactoryShift.Current;

        if (FactoryShift.SetLock(target))
        {
            bool first = FactoryShift.SwitchCooldownLeft >= FactoryShift.ManualSwitchCooldown - 1f;
            HintToast.Show(first
                ? "排班钟停了——锁定" + PhaseName(target) + "（这一班不会再换）"
                : "已改锁：" + PhaseName(target) + "（冷却 60 秒）", 4f);
        }
        else if (FactoryShift.SwitchCooldownLeft > 0f)
        {
            HintToast.Show("改锁冷却中——还剩 " + Mathf.CeilToInt(FactoryShift.SwitchCooldownLeft) + " 秒");
        }
    }

    private static string PhaseName(FactoryShift.Phase p)
    {
        return p == FactoryShift.Phase.Day ? "白班" : "夜班";
    }

    private string NameOf(int machineId)
    {
        if (machineNames == null || machineId < 0 || machineId >= machineNames.Length) return "机器 " + machineId;
        return machineNames[machineId];
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
