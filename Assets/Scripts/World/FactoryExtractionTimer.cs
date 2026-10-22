using UnityEngine;

/// <summary>
/// 工厂撤离窗口计时器（World，ExtractionTimer 的工厂版）：玩家身处任一工厂世界
/// （昼/夜双 zone 任一）时武装 ExtractionClock，倒计时归零锁死昼场景的实验室归途门
/// （关门 + E 失效——穿行要求门开，自然拦住归途）；玩家回到实验室（含瘫倒重启传送）
/// 即复位并解锁。只挂一份在昼场景（双场景常驻，夜侧不需要第二份）。
///
/// 与 Metro 版的差异：复位条件是「玩家回实验室」而不是「不在工厂 zone」——
/// 否则玩家从工厂穿去水淹区（链上下游）会把窗口错误复位；
/// 锁门也只在本层域武装期内生效（HandleLocked 时玩家必须在厂内）。
/// </summary>
public class FactoryExtractionTimer : MonoBehaviour
{
    [Tooltip("归途门（昼场景的实验室门）；留空则只计时锁不了门")]
    [SerializeField] private AnywhereDoor exitDoor;
    [Tooltip("进厂后的撤离窗口（秒）")]
    [SerializeField] private float windowSeconds = 600f;
    [SerializeField] private string lockHint = "撤离窗口关闭——归途门锁死了";
    [SerializeField] private string unlockHint = "门又开了。下一次进厂，窗口重新计时";

    // 昼/夜控制器跨场景（双世界常驻）——场景间引用不可序列化（ADR-0002），运行时按类型查找。
    // 每帧重试直到就绪：自愈域重载与加载次序（FindObjectsOfType 在 refs 补齐后即停）。
    private FactoryShiftController dayZone;
    private FactoryShiftController nightZone;

    private bool locked;

    private void OnEnable()
    {
        ExtractionClock.OnLocked += HandleLocked;
    }

    private void OnDisable()
    {
        ExtractionClock.OnLocked -= HandleLocked;
    }

    private void Update()
    {
        if (dayZone == null || nightZone == null) RefreshZones();
        bool inFactory = (dayZone != null && dayZone.PlayerInZone)
                      || (nightZone != null && nightZone.PlayerInZone);
        if (inFactory)
        {
            if (!ExtractionClock.Armed) ExtractionClock.Arm(windowSeconds);
            ExtractionClock.Tick(Time.deltaTime);
        }
        else if (ExtractionClock.Armed && WorldPresence.InHome)
        {
            // 回实验室（含瘫倒重启）：窗口复位、门解锁。去水淹区不算回——窗口继续走。
            ExtractionClock.Reset();
            Unlock();
        }
    }

    private void HandleLocked()
    {
        if (locked) return;
        // 只锁本层域的归途门：此刻玩家必须还在厂内（Metro 的钟归零不关工厂的事）
        if (dayZone == null || nightZone == null) RefreshZones();
        bool inFactory = (dayZone != null && dayZone.PlayerInZone)
                      || (nightZone != null && nightZone.PlayerInZone);
        if (!inFactory || exitDoor == null) return;

        locked = true;
        exitDoor.SetOpen(false, false);   // 关门并同步给对门
        exitDoor.SetRespondToKey(false);  // E 失效——门打不开了
        HintToast.Show(lockHint);
    }

    private void RefreshZones()
    {
        foreach (var c in FindObjectsOfType<FactoryShiftController>())
        {
            if (c.World == FactoryShift.Phase.Day) dayZone = c;
            else nightZone = c;
        }
    }

    private void Unlock()
    {
        if (!locked) return;
        locked = false;
        if (exitDoor != null) exitDoor.SetRespondToKey(true);
        HintToast.Show(unlockHint);
    }
}
