using UnityEngine;

/// <summary>
/// 配电箱：Metro 复明谜题的中枢。三根线里一根带电（默认每局随机）；
/// 持万用表时滚轮选中它、按 F 线 = 检测带电与否并接入电闸；E 电闸 = 合闸——
/// 接带电线全站复明，接错线火花四溅、闸弹回，可重试。
/// </summary>
public class PowerCabinet : MonoBehaviour
{
    [SerializeField] private PowerWire[] wires;
    [SerializeField] private BreakerSwitch breaker;
    [SerializeField] private MetroBlackout blackout;
    [Tooltip("每局随机带电线；关闭则用 fixedLiveWireIndex")]
    [SerializeField] private bool randomizeLiveWire = true;
    [SerializeField] private int fixedLiveWireIndex;

    private int liveIndex;
    private int selectedIndex = -1;

    public bool HasSelection { get { return selectedIndex >= 0; } }

    private void Start()
    {
        int count = wires == null ? 0 : wires.Length;
        liveIndex = randomizeLiveWire
            ? Random.Range(0, count)
            : Mathf.Clamp(fixedLiveWireIndex, 0, count - 1);
    }

    public bool IsLive(int wireIndex)
    {
        return wireIndex == liveIndex;
    }

    /// <summary>PowerWire 调用：把该线接入电闸（测线与接线一次完成）。</summary>
    public string Select(int wireIndex)
    {
        selectedIndex = wireIndex;
        return "已把 " + wires[wireIndex].DisplayName + " 接入电闸";
    }

    /// <summary>BreakerSwitch 调用：合闸验证。</summary>
    public void TryCloseBreaker()
    {
        if (!HasSelection)
        {
            HintToast.Show("电闸推不上去——滚轮选到万用表，按 F 把一根线接进来");
            return;
        }
        if (selectedIndex == liveIndex)
        {
            if (breaker != null) breaker.SetClosed(true);
            if (blackout != null) blackout.RestoreAll();
            HintToast.Show("电闸合上，电流涌过全站");
        }
        else
        {
            if (breaker != null) breaker.Spark();
            HintToast.Show("火花四溅——接的是没电的线，电闸弹了回去");
        }
    }
}
