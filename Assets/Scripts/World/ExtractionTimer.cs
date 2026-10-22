using UnityEngine;

/// <summary>
/// 撤离窗口计时器（World）：玩家进站武装 ExtractionClock，倒计时归零锁死 Metro 侧任意门
/// （关门 + E 失效——穿行要求门开，自然拦住归途）；玩家回到实验室（含瘫倒重启传送）
/// 即复位并解锁。窗口时长 Inspector 可调。
/// </summary>
public class ExtractionTimer : MonoBehaviour
{
    [SerializeField] private MetroBlackout blackout;
    [SerializeField] private AnywhereDoor exitDoor;
    [Tooltip("进站后的撤离窗口（秒）")]
    [SerializeField] private float windowSeconds = 600f;
    [SerializeField] private string lockHint = "撤离窗口关闭——归途门锁死了";
    [SerializeField] private string unlockHint = "门又开了。下一次进站，窗口重新计时";

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
        if (blackout == null || exitDoor == null) return;
        if (blackout.PlayerInZone)
        {
            if (!ExtractionClock.Armed) ExtractionClock.Arm(windowSeconds);
            ExtractionClock.Tick(Time.deltaTime);
        }
        else if (ExtractionClock.Armed && WorldPresence.InHome)
        {
            // 回实验室（含瘫倒重启）：窗口复位、门解锁。
            // 复位必须带 InHome 门槛（2026-10-22）：ExtractionClock 是全局唯一的一口钟，
            // 工厂等其它层域也会武装它——「玩家不在 Metro」不再意味着「这口钟归我管」，
            // 只有回到实验室才复位（Metro 单层域时代的对外行为不变：离开 Metro 只能是回实验室）。
            ExtractionClock.Reset();
            Unlock();
        }
    }

    private void HandleLocked()
    {
        if (locked) return;
        if (!blackout.PlayerInZone) return; // 只锁本层域武装的窗口——别家的钟归零不关 Metro 的门
        locked = true;
        exitDoor.SetOpen(false, false);   // 关门并同步给对门
        exitDoor.SetRespondToKey(false);  // E 失效——门打不开了
        HintToast.Show(lockHint);
    }

    private void Unlock()
    {
        if (!locked) return;
        locked = false;
        exitDoor.SetRespondToKey(true);
        HintToast.Show(unlockHint);
    }
}
