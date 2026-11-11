using UnityEngine;

public partial class PlayerMovement
{
    [Header("蓄力超级跳")]
    [Tooltip("按住空格超过此秒数即判定为蓄力（而非普通跳）。松开时发射。")]
    [SerializeField] private float superJumpHoldTime = 0.25f;
    [Tooltip("蓄力满时长（秒）。蓄力成长线性,满蓄力跳得最高最远。")]
    [SerializeField] private float superJumpMaxCharge = 2f;
    [Tooltip("满蓄力时的跳跃初速加速度(相对普通跳高度比例)。")]
    [SerializeField] private float superJumpHeightScale = 2.2f;
    [Tooltip("满蓄力时的水平冲刺初速(m/s)。")]
    [SerializeField] private float superJumpForwardSpeed = 9f;
    [Tooltip("蓄力跳扣除的电量（固定值，不随蓄力比例变化）。")]
    [SerializeField] private float superJumpDrain = 1f;
    [Tooltip("超级跳的水平冲刺持续时长(秒)。期间无视按键,保持冲刺速度。")]
    [SerializeField] private float superJumpBoostTime = 0.6f;

    // 蓄力超级跳
    private float superJumpPressTime = -10f;   // 本次按住空格的起始时刻（Time.time）
    private float superJumpCharge;             // 当前蓄力值 0~1
    private bool superJumpBoost;               // 本段按下的水平冲刺是否已注入
    private float superJumpBoostUntil;         // 冲刺结束时刻（Time.time）,期间保持冲刺速度
    private Vector3 superJumpBoostDir;         // 冲刺方向（发射瞬间锁定，空中无输入时兜底）
    private float superJumpBoostSpeed;         // 冲刺速度下限（发射瞬间锁定，含起跳前的动量）

    /// <summary>
    /// 蓄力跳的充能进度 0~1；返回 -1 表示「此刻不是在蓄力」（没按住 / 还没过蓄力阈值）。
    /// 供 HUD 显示蓄力百分比，避免普通点按跳跃时闪一下 0%。
    /// </summary>
    public float ChargeRatio
    {
        get
        {
            if (!PlayerGear.HasSuperJump) return -1f;   // 未解锁：HUD 不显示蓄力条
            if (superJumpPressTime < 0f) return -1f;
            if (!Input.GetKey(KeyCode.Space)) return -1f;
            float held = Time.time - superJumpPressTime;
            if (held < superJumpHoldTime) return -1f;   // 未达蓄力阈值 = 普通跳
            return Mathf.Clamp01(held / superJumpMaxCharge);
        }
    }

    /// <summary>蓄力跳一次固定扣多少电（HUD 提示用）。</summary>
    public float SuperJumpDrain { get { return superJumpDrain; } }

    private void HandleJump(bool canJumpState, bool jumpAllowed)
    {
        // 1) 按下空格：记下起始时刻，进入蓄力。
        if (Input.GetKeyDown(KeyCode.Space) && canJumpState && jumpAllowed)
        {
            superJumpPressTime = Time.time;
            superJumpCharge = 0f;
        }

        // 2) 按住空格且尚未松开：蓄力按按住时长成长。
        if (Input.GetKey(KeyCode.Space) && superJumpPressTime > -1f)
        {
            float held = Time.time - superJumpPressTime;
            superJumpCharge = Mathf.Clamp01(held / superJumpMaxCharge);
        }

        // 3) 松开空格：普通跳 or 超级跳。
        if (Input.GetKeyUp(KeyCode.Space) && superJumpPressTime > -1f)
        {
            float held = Time.time - superJumpPressTime;
            // 超级跳是中后期能力（PlayerGear.HasSuperJump，见 AbilityUnlockGate）。
            // 未解锁时按住再久也只是普通跳：不耗电、不冲刺。
            bool isSuper = PlayerGear.HasSuperJump && held >= superJumpHoldTime;
            if (canJumpState && jumpAllowed)
            {
                if (isSuper)
                {
                    float charge = Mathf.Clamp01(held / superJumpMaxCharge);   // 0~1
                    // 跳得更高：初速 = 普通跳初速 × 蓄力缩放
                    float normalV = Mathf.Sqrt(2f * -gravity * jumpHeight);
                    verticalVelocity = normalV * Mathf.Lerp(1f, superJumpHeightScale, charge);
                    // 水平冲刺：方向按当前意图，速度取「蓄力加成」与「起跳前已有动量」的较大者。
                    // 旧实现直接 horizontalVel = dir * Lerp(0, 9, charge)：轻蓄力只有 ~1.1 m/s，
                    // 正在冲刺（4 m/s）时一按空格反而被刹停——奔跑与跳跃没法一起用。
                    if (!superJumpBoost)
                    {
                        Vector3 dir = PlayerInputDir();
                        dir.y = 0f;
                        if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
                        dir.Normalize();

                        float boostSpeed = Mathf.Lerp(0f, superJumpForwardSpeed, charge);
                        float carried = horizontalVel.magnitude;   // 起跳前的水平动量
                        superJumpBoostSpeed = Mathf.Max(boostSpeed, carried);
                        superJumpBoostDir = dir;
                        horizontalVel = dir * superJumpBoostSpeed;
                        superJumpBoost = true;   // 本跳只冲一次
                        superJumpBoostUntil = Time.time + superJumpBoostTime;   // 冲刺持续这段
                    }
                    // 耗电：蓄力跳固定扣 superJumpDrain（不随蓄力比例变化）
                    if (BatterySystem.Instance != null)
                        BatterySystem.Instance.Drain(superJumpDrain);
                    Debug.Log("[SuperJump] 蓄力 " + (int)(charge * 100f) + "% 起跳 速度=" + horizontalVel.magnitude.ToString("F1") + " 高度="
                              + verticalVelocity.ToString("F1") + " 耗电=" + superJumpDrain);
                }
                else
                {
                    // 普通跳：不耗电。水平动量原样保留——奔跑中起跳会继续以奔跑速度前冲。
                    verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
                    superJumpBoost = false;
                    superJumpBoostUntil = 0f;   // 清掉上一跳可能残留的冲刺窗口
                }
            }
            else
            {
                superJumpBoost = false;
            }
            // 每次松开都要清掉按下标记与蓄力(哪怕是无效跳,避免空蓄力残留)
            superJumpPressTime = -1f;
            superJumpCharge = 0f;
            // 真起跳了就立刻判离地，别等下一帧的速度判定（否则起跳动画晚一帧）
            if (verticalVelocity > 0.5f) animGrounded = false;
        }

        // 若在空中/落地时重置水平冲刺标记:下次落地后再按空格才会重新冲
        if (!controller.isGrounded) superJumpBoost = false;

    }

    private bool TrySuperJumpBoost(float h, float v)
    {
        if (Time.time >= superJumpBoostUntil) return false;
            // 超级跳冲刺持续期：保住发射时的速度下限，但允许继续按方向键转向 / 按住 Shift 加速。
            // 旧实现这里完全无视输入、只沿锁定方向滑行 0.6s——起跳后想跑却动不了，
            // 与「奔跑和跳跃同时用」直接冲突。
            Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);
            bool moving = input.sqrMagnitude > 0.01f;
            bool sprintAllowed = BatterySystem.Instance == null || BatterySystem.Instance.SprintEnabled;
            bool sprinting = moving && sprintAllowed && Input.GetKey(KeyCode.LeftShift);

            // 目标速度：有输入按方向走（冲刺 4 / 走 2），无输入则沿锁定方向
            Vector3 dir = moving ? transform.TransformDirection(input) : superJumpBoostDir;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            dir.Normalize();

            float wantSpeed = moving ? (sprinting ? runSpeed : walkSpeed) : 0f;
            // 速度下限 = 发射时的动量；窗口内不允许掉到它以下（冲刺的"惯性"感）
            float targetSpeed = Mathf.Max(wantSpeed, superJumpBoostSpeed);
            float rate = moving ? acceleration : deceleration;
            horizontalVel = Vector3.MoveTowards(horizontalVel, dir * targetSpeed, rate * Time.deltaTime);

            if (anim != null)
            {
                float speedNorm = Mathf.Clamp01(horizontalVel.magnitude / runSpeed);
                anim.SetMove(speedNorm, false, false, animGrounded);
            }
        return true;
    }
}
