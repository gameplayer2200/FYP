using UnityEngine;

/// <summary>
/// 电量系统：AI 调查员没有血量，受创掉电；低电量自动关闭部分功能保命；
/// 只有在充电桩充回满电（100）才恢复全部功能。
/// 降级链：≤50 锁冲刺，≤25 锁跳跃，蹲伏不锁。0 = Death01 瘫倒，强制休眠后回任意门旁重启（电量30）。
/// G 键 = 模拟受创（测试用，怪物 AI 接入后移除）。
/// </summary>
public class BatterySystem : MonoBehaviour
{
    public static BatterySystem Instance { get; private set; }
    public static event System.Action<float, bool, bool> OnBatteryChanged; // (电量, 冲刺可用, 跳跃可用)

    [Header("电量")]
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float hitDrain = 20f;             // 每次受创掉电（数值待定，Inspector 可调）
    [SerializeField] private float restartBattery = 30f;      // 瘫倒重启时的电量
    [SerializeField] private float chargeRate = 5f;           // 充电速度/秒

    [Header("降级阈值")]
    [SerializeField] private float sprintLockThreshold = 50f; // ≤50 锁冲刺
    [SerializeField] private float jumpLockThreshold = 25f;   // ≤25 锁跳跃

    [Header("瘫倒重启")]
    [Tooltip("重启落点的参照物（实验室的任意门）。留空则原地重启。")]
    [SerializeField] private Transform restartAnchor;
    [SerializeField] private float restartDelay = 3f;
    [SerializeField] private Vector3 restartOffset = new Vector3(1.5f, 0.2f, 0f);
    [Tooltip("重生后起身动画时长（秒）。UAL2_LayToIdle ≈ 1.53s；此期间锁移动。")]
    [SerializeField] private float getUpDuration = 1.5f;

    // 环境耗电速率（电/秒）。由世界规则（如 Metro 停电）经 SetAmbientDrain 设定；
    // 实验室没有世界规则会调它，天然保持 0。
    private float ambientDrain;

    // 手电筒耗电通道（电/秒）。Flashlight 组件设定；与环境耗电独立、叠加扣减。
    private float flashlightDrain;

    [Header("调试")]
    [Tooltip("G 键模拟受创。怪物 AI 接入后关掉。")]
    [SerializeField] private bool allowDebugHit = true;

    public float Battery { get; private set; }
    public float MaxBattery { get { return maxBattery; } } // 供 UI / 充电桩判定满电，避免各处硬编码 100
    public bool SprintEnabled { get { return !IsDead && Battery > sprintLockThreshold; } }
    public bool JumpEnabled { get { return !IsDead && Battery > jumpLockThreshold; } }
    public bool IsDead { get; private set; }
    public bool IsCharging { get; private set; }

    /// <summary>
    /// 重生起身中（死亡动画 → 传送 → 爬起来）。期间 PlayerMovement 锁移动输入，
    /// 否则角色会边爬起边滑走。
    /// </summary>
    public bool IsRecovering { get; private set; }

    private PlayerAnimatorProxy anim;
    private bool lastSprint, lastJump;
    private float recoverUntil;   // 起身结束时刻（Time.time 基准）

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        anim = GetComponentInParent<PlayerAnimatorProxy>(); if (anim == null) anim = GetComponent<PlayerAnimatorProxy>();
    }

    private void Start()
    {
        Battery = maxBattery;
        BillingLedger.SetCapacity(maxBattery);
        Notify();
    }

    /// <summary>受创接口：怪物攻击/危险区调用。</summary>
    public void TakeHit()
    {
        if (IsDead) return;
        float before = Battery;
        Battery = Mathf.Max(0f, Battery - hitDrain);
        BillingLedger.AddHit(before - Battery);
        if (anim != null) anim.PlayHit();
        Debug.Log("[Battery] 受创 -" + hitDrain + " 剩余 " + (int)Battery + " 冲刺=" + SprintEnabled + " 跳跃=" + JumpEnabled);
        if (Battery <= 0f) Die();
        else Notify();
    }

    /// <summary>
    /// 一次性按量耗电（超级跳等主动技能用）：
    /// 直接扣固定数值，不播受创动画；扣到 0 同样触发瘫倒。
    /// </summary>
    public void Drain(float amount)
    {
        if (IsDead) return;
        float before = Battery;
        Battery = Mathf.Max(0f, Battery - Mathf.Max(0f, amount));
        BillingLedger.AddDevice(before - Battery);
        if (Battery <= 0f) Die();
        else Notify();
    }

    private void Die()
    {
        IsDead = true;
        if (anim != null) anim.PlayDead();
        Debug.Log("[Battery] 电量归零——强制休眠");
        Invoke(nameof(Restart), restartDelay);
    }

    private void Restart()
    {
        if (restartAnchor != null)
        {
            Vector3 target = restartAnchor.position + restartOffset;
            var pm = GetComponentInParent<PlayerMovement>();
            if (pm != null) pm.TeleportTo(target);
            else PlayerRef.Teleport(target, transform.rotation);
        }
        Battery = restartBattery;
        IsDead = false;

        // 起身：Dead 状态没有任何自动出口（控制器里它是死胡同），必须显式发 GetUp trigger
        // 才能把角色从倒地姿势拉起来。播的是 UAL2_LayToIdle——死亡动画 UAL1_Death01 的逆过程
        //（根骨骼 Y：Death 0.947→0.125，LayToIdle 0.079→0.946，首尾正好接上）。
        if (anim != null) anim.PlayGetUp();
        IsRecovering = true;
        // 用 Update 计时而不是 Invoke：实测 Invoke 的延时在这里不可靠（会立刻触发）
        recoverUntil = Time.time + getUpDuration;

        Notify();
        // 瘫倒 = 撤离失败：背包侧惩罚（携带异常物减半）经 Core 钩子解耦触发；
        // 参数 = 是否真的传送回了实验室（restartAnchor 有效）——WorldStreamer 据此同步「当前世界」登记
        CollapsePenalty.Notify(restartAnchor != null);
        Debug.Log("[Battery] 重启完成，电量 " + (int)Battery + "（起身中，时长 " + getUpDuration + "s，至 t=" + recoverUntil.ToString("F2") + "）");
    }

    /// <summary>起身动画结束：放行动画出口并恢复移动。</summary>
    private void EndRecover()
    {
        IsRecovering = false;
        if (anim != null) anim.SetGetUpDone(true);   // 条件转移 GetUp → Idle
        Debug.Log("[Battery] 起身结束，恢复移动（t=" + Time.time.ToString("F2") + " until=" + recoverUntil.ToString("F2") + "）");
    }

    /// <summary>充电桩调用：true=持续充电（满电自动停止并恢复全部功能）。</summary>
    public void SetCharging(bool on)
    {
        if (IsDead) on = false;
        if (IsCharging == on) return;
        IsCharging = on;
        if (anim != null) anim.SetCharging(on); // 开关都要同步动画：取消充电也要放下手
    }

    /// <summary>世界规则调用：设置环境耗电速率（电/秒）。0 = 无环境消耗（实验室常态）。</summary>
    public void SetAmbientDrain(float ratePerSecond)
    {
        ambientDrain = Mathf.Max(0f, ratePerSecond);
    }

    /// <summary>手电筒组件调用：开灯耗电通道（电/秒），与环境耗电叠加。</summary>
    public void SetFlashlightDrain(float ratePerSecond)
    {
        flashlightDrain = Mathf.Max(0f, ratePerSecond);
    }

    private void Update()
    {
        // 起身计时到点 → 放行动画出口 + 恢复移动
        if (IsRecovering && Time.time >= recoverUntil) EndRecover();

        // G 键测试受创（怪物 AI 接入后把 allowDebugHit 关掉）
        if (allowDebugHit && !IsDead && Input.GetKeyDown(KeyCode.G)) TakeHit();

        // 持续耗电：停电/黑暗等世界规则 + 手电筒。充电与瘫倒期间不耗。
        float drain = ambientDrain + flashlightDrain;
        if (!IsDead && !IsCharging && drain > 0f)
        {
            BillingLedger.AddAmbient(ambientDrain * Time.deltaTime);
            BillingLedger.AddLighting(flashlightDrain * Time.deltaTime);
            Battery = Mathf.Max(0f, Battery - drain * Time.deltaTime);
            if (Battery <= 0f) Die();
            else Notify();
        }

        if (IsCharging && !IsDead)
        {
            Battery = Mathf.Min(maxBattery, Battery + chargeRate * Time.deltaTime);
            Notify();
            if (Battery >= maxBattery)
            {
                if (anim != null) anim.SetCharging(false);
                IsCharging = false;
                Debug.Log("[Battery] 电量已满，全部功能恢复");
            }
        }
    }

    private void Notify()
    {
        bool s = SprintEnabled, j = JumpEnabled;
        if (OnBatteryChanged != null) OnBatteryChanged(Battery, s, j);
        if (s != lastSprint && !s) Debug.Log("[Battery] 供电不足：已关闭 冲刺");
        if (j != lastJump && !j) Debug.Log("[Battery] 供电不足：已关闭 跳跃");
        lastSprint = s; lastJump = j;
    }

    private void OnGUI()
    {
        if (IsDead) return;

        // CRT 终端电量条：方块格 + 三位读数（VT323 像素字）。不加任何文字标签。
        // 蓄力跳（按住空格）时，第二行显示蓄力百分比与蓄力格。
        var pm = GetComponentInParent<PlayerMovement>();
        float charge = pm != null ? pm.ChargeRatio : -1f;
        bool chargingJump = charge >= 0f;
        bool degraded = !SprintEnabled || !JumpEnabled;

        float ratio = maxBattery > 0f ? Mathf.Clamp01(Battery / maxBattery) : 0f;
        Color c = ratio > 0.5f ? CrtTheme.Ink : (ratio > 0.25f ? CrtTheme.Warn : CrtTheme.Danger);

        float W = CrtTheme.Px(252f);
        float H = CrtTheme.Px(40f);
        float rowH = CrtTheme.Px(17f);
        float extra = (chargingJump || degraded) ? rowH + CrtTheme.Px(2f) : 0f;

        var r = new Rect(CrtTheme.Px(20f), Screen.height - CrtTheme.Px(20f) - (H + extra), W, H + extra);
        CrtTheme.Panel(r);

        // 电量格 + 三位读数
        CrtTheme.Gauge(new Rect(r.x + CrtTheme.Px(10f), r.y + CrtTheme.Px(13f), CrtTheme.Px(130f), CrtTheme.Px(12f)),
            ratio, 10, c);
        GUI.Label(new Rect(r.x + CrtTheme.Px(146f), r.y + CrtTheme.Px(9f), CrtTheme.Px(96f), CrtTheme.Px(17f)),
            ((int)Battery).ToString("000"), CrtTheme.Style(19, c, false, true, TextAnchor.MiddleCenter));

        // 第二行：蓄力跳优先显示蓄力进度（蓄力中 / 降级提示二选一，避免挤成两行糊在一起）
        if (chargingJump)
        {
            int pct = Mathf.RoundToInt(charge * 100f);
            var cJump = pct >= 100 ? CrtTheme.Ink : CrtTheme.Warn;
            CrtTheme.Gauge(new Rect(r.x + CrtTheme.Px(10f), r.y + CrtTheme.Px(34f), CrtTheme.Px(130f), CrtTheme.Px(8f)),
                charge, 10, cJump);
            GUI.Label(new Rect(r.x + CrtTheme.Px(146f), r.y + CrtTheme.Px(30f), CrtTheme.Px(96f), CrtTheme.Px(15f)),
                pct.ToString("000") + "%", CrtTheme.Style(13, cJump, false, true, TextAnchor.MiddleCenter));
        }
        else if (degraded)
        {
            string flags = (SprintEnabled ? "" : "冲刺已关闭 ") + (JumpEnabled ? "" : "跳跃已关闭");
            GUI.Label(new Rect(r.x + CrtTheme.Px(10f), r.y + CrtTheme.Px(29f), W - CrtTheme.Px(20f), CrtTheme.Px(14f)),
                flags, CrtTheme.Style(10, CrtTheme.Warn, false));
        }
    }
}
