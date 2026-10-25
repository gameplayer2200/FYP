using UnityEngine;

/// <summary>
/// 电量系统：AI 调查员没有血量，受创掉电；低电量自动关闭部分功能保命；
/// 只有在充电桩充回满电（100）才恢复全部功能。
/// 降级链：≤50 锁冲刺，≤25 锁跳跃，蹲伏不锁。0 = Death01 瘫倒，强制休眠后回任意门旁重启（电量30）。
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

    // 任务提示板耗电通道（电/秒）。OrderTerminal 组件设定；同样独立叠加。
    private float terminalDrain;

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

    /// <summary>任务提示板组件调用：开机耗电通道（电/秒），与其它通道叠加。</summary>
    public void SetTerminalDrain(float ratePerSecond)
    {
        terminalDrain = Mathf.Max(0f, ratePerSecond);
    }

    private void Update()
    {
        // 起身计时到点 → 放行动画出口 + 恢复移动
        if (IsRecovering && Time.time >= recoverUntil) EndRecover();

        // 持续耗电：停电/黑暗等世界规则 + 手电筒 + 任务提示板。充电与瘫倒期间不耗。
        float drain = ambientDrain + flashlightDrain + terminalDrain;
        if (!IsDead && !IsCharging && drain > 0f)
        {
            BillingLedger.AddAmbient(ambientDrain * Time.deltaTime);
            BillingLedger.AddLighting(flashlightDrain * Time.deltaTime);
            BillingLedger.AddDevice(terminalDrain * Time.deltaTime);
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

        // 左上角电池图标：外壳 + 按电量从下往上填充 + 右侧三位读数。
        // 颜色随电量走绿→黄→红（与参考图一致），≤10% 时缓慢呼吸，提示"要没电了"。
        // 蓄力跳（按住空格）时，读数下方显示蓄力百分比；降级时显示"冲刺/跳跃已关闭"。
        var pm = GetComponentInParent<PlayerMovement>();
        float charge = pm != null ? pm.ChargeRatio : -1f;
        bool chargingJump = charge >= 0f;
        bool degraded = !SprintEnabled || !JumpEnabled;

        float ratio = maxBattery > 0f ? Mathf.Clamp01(Battery / maxBattery) : 0f;
        Color c = ratio > 0.5f ? CrtTheme.Ink : (ratio > 0.25f ? CrtTheme.Warn : CrtTheme.Danger);

        // 极低电量：外壳与填充一起缓慢呼吸（0.85~1.0），不用闪烁——闪烁太吵，且和 HUD 乱码效果撞车
        float alpha = 1f;
        if (ratio <= 0.1f)
            alpha = 0.85f + 0.15f * Mathf.PingPong(Time.unscaledTime * 0.9f, 1f);

        float shellW = CrtTheme.Px(34f);
        float shellH = CrtTheme.Px(58f);
        float nubW = CrtTheme.Px(14f);
        float nubH = CrtTheme.Px(5f);
        float line = Mathf.Max(1f, Mathf.Round(CrtTheme.Px(2.4f)));
        int rad = Mathf.Max(2, Mathf.RoundToInt(CrtTheme.Px(5f)));

        float x = CrtTheme.Px(24f);
        float y = CrtTheme.Px(28f);
        var nub = new Rect(x + (shellW - nubW) * 0.5f, y, nubW, nubH);
        var shell = new Rect(x, y + nubH + CrtTheme.Px(2f), shellW, shellH);

        var prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        // 外壳：描边环 + 顶部凸点。
        // 用 RoundBorderExact（按实际像素 1:1 生成）而不是九宫格版：
        // 电池只有 34×58 这种小尺寸，九宫格拉伸会把环形贴图中段里的左右两条边一起拉歪。
        CrtTheme.RoundBorderExact(shell, c, rad, line);
        CrtTheme.RoundFill(nub, c, Mathf.Max(1, rad - 2));

        // 填充：内缩 line+2，从底部长上来
        float pad = line + CrtTheme.Px(2f);
        var inner = new Rect(shell.x + pad, shell.y + pad, shell.width - pad * 2f, shell.height - pad * 2f);
        if (ratio > 0.001f)
        {
            float fh = inner.height * ratio;
            var fill = new Rect(inner.x, inner.yMax - fh, inner.width, fh);
            // 填充底边给一点内圆角，顶边保持平（像液面）
            CrtTheme.RoundFill(fill, new Color(c.r, c.g, c.b, 0.92f), Mathf.Max(1, rad - 2));
        }

        GUI.color = prevColor;

        // 右侧读数：大号数字 + 小号百分号
        float tx = shell.xMax + CrtTheme.Px(10f);
        GUI.Label(new Rect(tx, shell.y + CrtTheme.Px(2f), CrtTheme.Px(78f), CrtTheme.Px(30f)),
            Mathf.CeilToInt(Battery).ToString("000"),
            CrtTheme.Style(26, c, false, true, TextAnchor.UpperLeft));
        GUI.Label(new Rect(tx + CrtTheme.Px(44f), shell.y + CrtTheme.Px(13f), CrtTheme.Px(26f), CrtTheme.Px(18f)),
            "%", CrtTheme.Style(13, CrtTheme.InkDim, false, true, TextAnchor.UpperLeft));

        // 读数下方：蓄力进度 / 降级提示（二选一，避免挤成两行）
        float ry = shell.y + CrtTheme.Px(34f);
        if (chargingJump)
        {
            int pct = Mathf.RoundToInt(charge * 100f);
            var cJump = pct >= 100 ? CrtTheme.Ink : CrtTheme.Warn;
            CrtTheme.Gauge(new Rect(tx, ry, CrtTheme.Px(78f), CrtTheme.Px(6f)), charge, 8, cJump);
            GUI.Label(new Rect(tx, ry + CrtTheme.Px(8f), CrtTheme.Px(78f), CrtTheme.Px(14f)),
                pct.ToString("000") + "%", CrtTheme.Style(11, cJump, false, true, TextAnchor.UpperLeft));
        }

        // 功能图标（电池正下方，横排）：可用=亮，不可用=灰。
        // 为什么放图标而不是文字：冲刺/跳跃是否可用要一眼扫到，读文字会打断看路。
        // 为什么横排在电池下方而不是右侧：右上角是订单板的地盘，屏幕一窄就会被压住。
        DrawAbilityIcons(shell.x, shell.yMax + CrtTheme.Px(8f));
    }

    // 冲刺：向右的双人字箭头（快进符号，一眼读成"能跑"）。
    private static readonly string[] GlyphSprint =
    {
        "#....#...",
        ".#....#..",
        "..#....#.",
        "...#....#",
        "..#....#.",
        ".#....#..",
        "#....#...",
    };

    // 跳跃：向上箭头 + 地面横线（箭头离地 = 能起跳）。
    private static readonly string[] GlyphJump =
    {
        "....#....",
        "...###...",
        "..#####..",
        ".#######.",
        "....#....",
        "....#....",
        "#########",
    };

    /// <summary>在指定位置画一排功能图标：亮=可用，暗灰=被低电量锁掉。</summary>
    private void DrawAbilityIcons(float x, float y)
    {
        // 图标要够大才认得出来：15px 时 7 行像素被取整吃成 2~3 行，箭头会糊成两个点。
        // 22px + 单元格取整后每格仍有 2~3px，双人字箭头与跳跃箭头都能看清。
        float size = CrtTheme.Px(22f);
        float gap = CrtTheme.Px(10f);

        var onInk = new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.95f);
        var offInk = new Color(CrtTheme.InkDim.r, CrtTheme.InkDim.g, CrtTheme.InkDim.b, 0.26f);

        CrtTheme.PixelGlyph(new Rect(x, y, size, size), GlyphSprint, SprintEnabled ? onInk : offInk);
        CrtTheme.PixelGlyph(new Rect(x + size + gap, y, size, size), GlyphJump, JumpEnabled ? onInk : offInk);
    }
}
