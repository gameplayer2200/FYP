using UnityEngine;

/// <summary>充电桩：E 键交互由 InteractionRunner 分发；充断电通过 BatterySystem 公开接口。</summary>
public class ChargeStation : MonoBehaviour, IInteractable
{
    [SerializeField] private float useRange = 2.5f;
    private float cancelLockUntil; // 兜底取消后短时间内禁止 Interact 重新开启（防止同一次按键先取消又重启）
    public float InteractionRange { get { return useRange; } }

    public Transform GetInteractionPoint() { return transform; }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }

    public void Interact(GameObject player)
    {
        if (BatterySystem.Instance == null) return;
        if (Time.time < cancelLockUntil) return; // 本按键周期已处理过（兜底取消）
        if (BatterySystem.Instance.IsCharging)
        {
            BatterySystem.Instance.SetCharging(false);
            Debug.Log("[ChargeStation] 停止充电");
        }
        else if (BatterySystem.Instance.Battery < BatterySystem.Instance.MaxBattery)
        {
            BatterySystem.Instance.SetCharging(true);
            Debug.Log("[ChargeStation] 开始充电");
        }
    }

    private void Update()
    {
        var battery = BatterySystem.Instance;
        if (battery == null || !battery.IsCharging) return; // 只有充电中才需要做距离判定

        // 玩家位置直接取自 BatterySystem（挂在 player 根对象上）。
        // 原实现每帧 GameObject.Find("player") 全场景遍历；且 player 的 Tag 是 Untagged，
        // FindGameObjectWithTag("Player") 一直返回 null，属于无效查找。
        Vector3 playerPos = battery.transform.position;
        float sqrDist = (playerPos - transform.position).sqrMagnitude;

        // 充电中按 E 全局兜底取消（不依赖 InteractionRunner 选中本桩——充电时玩家不动，最近的交互物可能是门）
        float cancelRange = useRange + 1f;
        if (Input.GetKeyDown(KeyCode.E) && sqrDist <= cancelRange * cancelRange)
        {
            battery.SetCharging(false);
            cancelLockUntil = Time.time + 0.25f; // 锁一小段，吃掉同一次按键的 Interact 分发
            Debug.Log("[ChargeStation] 按 E 取消充电");
            return;
        }

        // 走出范围自动断电（距离保护，非交互逻辑）
        float keepRange = useRange + 0.5f;
        if (sqrDist > keepRange * keepRange)
        {
            battery.SetCharging(false);
            Debug.Log("[ChargeStation] 离开充电桩，充电中断");
        }
    }
}
