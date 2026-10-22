using System.Collections;
using UnityEngine;

/// <summary>
/// 「巡检灯」（工厂夜班异常）：深区走廊里一道规律扫动的手电光束——像有人在巡检，
/// 走近却永远找不到人。光束扫过玩家时 HUD 乱码一瞬（EmfStorm 短脉冲）+ 刺点声 + 提示。
/// 无伤害：夜班不掉电，耗的是 nerves。只在夜班显形期活动（锁夜 = 平息）。
/// </summary>
public class PatrolBeam : MonoBehaviour
{
    [SerializeField] private FactoryShiftController controller; // 夜场景
    [Tooltip("Spotlight（灯锥，renderMode 已由 FactorySceneDev 设 ForcePixel——灯多场景必须）")]
    [SerializeField] private Light spot;
    [Tooltip("摇摆枢轴（Spotlight 挂其下；留空用本 transform）")]
    [SerializeField] private Transform pivot;
    [SerializeField] private float centerYaw = 0f;
    [Tooltip("来回扫动总角度")]
    [SerializeField] private float sweepAngle = 65f;
    [Tooltip("一个来回的周期（秒）")]
    [SerializeField] private float sweepPeriod = 7f;
    [SerializeField] private float pitch = -6f;
    [Header("命中判定（光锥扫过玩家）")]
    [SerializeField] private float detectRange = 20f;
    [Tooltip("命中半角（度）——比 spotlight 的 spotAngle 稍宽，容错")]
    [SerializeField] private float detectHalfAngle = 16f;
    [Tooltip("同一束光两次命中的间隔（秒）")]
    [SerializeField] private float retriggerCooldown = 8f;
    [Tooltip("命中时的 HUD 乱码秒数")]
    [SerializeField] private float scrambleTime = 1.2f;
    [SerializeField] private AudioSource sting;
    [SerializeField] private string sweepHint = "有光扫过你——像在找人";

    private float phase;
    private float nextHitAt;
    private Coroutine pulse;

    private void Update()
    {
        bool active = controller != null && controller.PlayerInZone && FactoryShift.AnomaliesActive;
        if (spot != null && spot.enabled != active) spot.enabled = active;
        if (!active) return;

        var t = pivot != null ? pivot : transform;
        phase += Time.deltaTime;
        float yaw = centerYaw + Mathf.Sin(phase / Mathf.Max(0.05f, sweepPeriod) * Mathf.PI * 2f) * sweepAngle * 0.5f;
        t.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        var player = PlayerRef.Root;
        if (player == null) return;
        Vector3 toPlayer = player.position + Vector3.up * 1.2f - t.position;
        if (toPlayer.magnitude > detectRange) return;
        if (Vector3.Angle(t.forward, toPlayer) > detectHalfAngle) return;
        if (Time.time < nextHitAt) return;

        nextHitAt = Time.time + retriggerCooldown;
        if (sting != null) sting.Play();
        HintToast.Show(sweepHint);
        if (pulse != null) StopCoroutine(pulse);
        pulse = StartCoroutine(ScrambleRoutine());
    }

    private IEnumerator ScrambleRoutine()
    {
        // 玩家此刻在工厂，Metro 的风暴不可能同时进行（互斥于玩家位置）——短脉冲安全
        EmfStorm.Set(true);
        yield return new WaitForSeconds(scrambleTime);
        EmfStorm.Set(false);
        pulse = null;
    }

    private void OnDisable()
    {
        if (pulse != null)
        {
            StopCoroutine(pulse);
            EmfStorm.Set(false);
            pulse = null;
        }
    }
}
