using System;
using UnityEngine;

/// <summary>
/// Metro「渐渐熄灭」规则中枢：玩家在 Metro 期间，灯组按顺序每 segmentInterval 秒熄一组；
/// 停电开始后玩家缓慢掉电（baseDrain/秒），所在区段已熄灭时再乘 darknessMultiplier；
/// 配电室电闸合对线后 RestoreAll()：全站复明、停止环境耗电、广播 OnPowerRestored
/// （尾节车厢锁、充电桩供电都订阅它）。数值全部 Inspector 可调。
/// </summary>
public class MetroBlackout : MonoBehaviour
{
    /// <summary>全站复明时触发一次。晚加载的对象请同时查 PowerRestored 静态快照。</summary>
    public static event Action OnPowerRestored;

    /// <summary>静态快照：电已恢复则为真（供后订阅/后加载的对象对齐状态）。</summary>
    public static bool PowerRestored { get; private set; }

    /// <summary>玩家当前是否身处 Metro（每帧刷新；异常事件用它做触发门槛）。</summary>
    public bool PlayerInZone { get; private set; }

    /// <summary>已熄灭的灯组数（>0 = 停电进行中）。</summary>
    public int KilledGroups { get { return killedCount; } }

    [Header("灯组（按熄灭顺序排列）")]
    [SerializeField] private LightGroup[] groups;

    [Header("世界范围（判定玩家是否身处 Metro，盒形）")]
    [SerializeField] private Vector3 zoneCenter = new Vector3(-34f, 11f, 120f);
    [SerializeField] private Vector3 zoneSize = new Vector3(72f, 42f, 204f);

    [Header("节奏与耗电")]
    [Tooltip("每段的熄灭间隔（秒）。测试时可临时调小观察效果。")]
    [SerializeField] private float segmentInterval = 90f;
    [Tooltip("停电开始后每秒掉电量")]
    [SerializeField] private float baseDrain = 1f;
    [Tooltip("玩家所在区段已熄灭时的掉电倍率（提案：黑暗中 1.5 倍）")]
    [SerializeField] private float darknessMultiplier = 1.5f;
    [Tooltip("玩家离最近灯组不超过该距离视为身处该区段")]
    [SerializeField] private float segmentRadius = 18f;

    private int killedCount;
    private float timer;
    private bool restored;

    private void OnDestroy()
    {
        OnPowerRestored = null;
        PowerRestored = false;
    }

    private void Update()
    {
        if (restored) return;

        var player = PlayerRef.Root;
        bool inZone = player != null && InZone(player.position);
        PlayerInZone = inZone;

        if (!inZone)
        {
            // 玩家回实验室：环境耗电清零，熄灭进度保留（世界状态不回滚）
            SetDrain(0f);
            return;
        }

        if (groups == null || groups.Length == 0) return;

        timer += Time.deltaTime;
        if (timer >= segmentInterval && killedCount < groups.Length)
        {
            timer = 0f;
            if (groups[killedCount] != null) groups[killedCount].SetOn(false);
            killedCount++;
        }

        if (killedCount > 0)
        {
            bool dark = IsPlayerInDarkSegment(player);
            SetDrain(dark ? baseDrain * darknessMultiplier : baseDrain);
        }
        else SetDrain(0f);
    }

    /// <summary>盒形世界判定：Metro 的 Z 区间与实验室天然分离（双世界常驻、零重叠）。</summary>
    private bool InZone(Vector3 p)
    {
        Vector3 d = p - zoneCenter;
        return Mathf.Abs(d.x) <= zoneSize.x * 0.5f
            && Mathf.Abs(d.y) <= zoneSize.y * 0.5f
            && Mathf.Abs(d.z) <= zoneSize.z * 0.5f;
    }

    private bool IsPlayerInDarkSegment(Transform player)
    {
        if (player == null || groups == null || groups.Length == 0) return false;
        LightGroup nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null) continue;
            float d = Vector3.Distance(player.position, groups[i].transform.position);
            if (d < best) { best = d; nearest = groups[i]; }
        }
        return nearest != null && best <= segmentRadius && !nearest.IsOn;
    }

    private void SetDrain(float rate)
    {
        if (BatterySystem.Instance != null) BatterySystem.Instance.SetAmbientDrain(rate);
    }

    /// <summary>配电室电闸合对线后调用：全站复明、停止环境耗电、广播事件。</summary>
    public void RestoreAll()
    {
        if (restored) return;
        restored = true;
        timer = 0f;
        killedCount = 0;
        if (groups != null)
        {
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] != null) groups[i].SetOn(true, false);
        }
        SetDrain(0f);
        PowerRestored = true;
        if (OnPowerRestored != null) OnPowerRestored();
        Debug.Log("[MetroBlackout] 电闸合上——全站复明");
    }
}
