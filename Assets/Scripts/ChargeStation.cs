using UnityEngine;

/// <summary>充电桩：E 键交互由 InteractionRunner 分发；充断电通过 BatterySystem 公开接口。</summary>
public class ChargeStation : MonoBehaviour, IInteractable
{
    [SerializeField] private float useRange = 2.5f;
    private float cancelLockUntil; // 兜底取消后短时间内禁止 Interact 重新开启（防止同一次按键先取消又重启）
    public float InteractionRange { get { return useRange; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (BatterySystem.Instance == null) return;
        if (Time.time < cancelLockUntil) return; // 本按键周期已处理过（兜底取消）
        if (BatterySystem.Instance.IsCharging)
        {
            BatterySystem.Instance.SetCharging(false);
            Debug.Log("[ChargeStation] 停止充电");
        }
        else if (BatterySystem.Instance.Battery < 100f)
        {
            BatterySystem.Instance.SetCharging(true);
            Debug.Log("[ChargeStation] 开始充电");
        }
    }

    private void Update()
    {
        // 充电中按 E 全局兜底取消（不依赖 InteractionRunner 选中本桩——充电时玩家不动，最近的交互物可能是门）
        if (BatterySystem.Instance != null && BatterySystem.Instance.IsCharging && Input.GetKeyDown(KeyCode.E))
        {
            // 仅当玩家仍在桩附近才允许这里取消（避免隔空断电）
            var pp = GameObject.Find("player");
            if (pp != null && Vector3.Distance(pp.transform.position, transform.position) <= useRange + 1f)
            {
                BatterySystem.Instance.SetCharging(false);
                cancelLockUntil = Time.time + 0.25f; // 锁一小段，吃掉同一次按键的 Interact 分发
                Debug.Log("[ChargeStation] 按 E 取消充电");
                return;
            }
        }
        // 走出范围自动断电（距离保护，非交互逻辑）
        if (BatterySystem.Instance != null && BatterySystem.Instance.IsCharging)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("player");
            if (p == null || Vector3.Distance(p.transform.position, transform.position) > useRange + 0.5f)
            {
                BatterySystem.Instance.SetCharging(false);
                Debug.Log("[ChargeStation] 离开充电桩，充电中断");
            }
        }
    }
}
