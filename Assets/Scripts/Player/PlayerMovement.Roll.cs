using UnityEngine;

public partial class PlayerMovement
{
    [Header("翻滚")]
    [SerializeField] private float rollSpeed = 2.5f;      // 翻滚推进速度 m/s（1.4s 约滚 3.5m）
    [SerializeField] private float rollDuration = 1.4f;   // 翻滚推进时长（对齐 UAL1_Roll 动画 1.467s）
    [SerializeField] private float rollCooldown = 0.3f;   // 翻滚结束后的冷却（防连按）
    [SerializeField] private float doubleTapWindow = 0.25f; // Shift 双击判定窗口（秒）

    // 翻滚状态
    private bool rolling;
    private Vector3 rollDir;      // 翻滚推进方向（触发瞬间锁定）
    private float rollTimer;
    private float rollCooldownUntil;
    private float lastShiftTapTime = -10f;

    private void HandleRoll(bool bodyLocked)
    {
        // 翻滚：Shift 双击（0.25s 内两次轻点）触发，与按住冲刺不冲突；倒地/起身中不可翻滚
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            if (!bodyLocked && Time.time - lastShiftTapTime <= doubleTapWindow) TryStartRoll();
            lastShiftTapTime = Time.time;
        }
        if (rolling) RollTick();
    }

    /// <summary>
    /// 尝试起滚。Shift 双击触发（单击=冲刺，长按=持续冲刺，二者不冲突）。
    /// 条件：在地面、非充电、不在冷却中。
    /// </summary>
    private void TryStartRoll()
    {
        if (rolling) return;
        if (Time.time < rollCooldownUntil) return;
        if (!controller.isGrounded) return;                       // 空中不可滚
        if (BatterySystem.Instance != null && BatterySystem.Instance.IsCharging) return;
        if (BatterySystem.Instance != null && (BatterySystem.Instance.IsDead || BatterySystem.Instance.IsRecovering)) return;

        // 方向：有移动输入则朝输入方向，否则朝身体正前方
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);
        Vector3 dir = input.sqrMagnitude > 0.01f ? transform.TransformDirection(input) : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;    // 兜底：朝向退化时用正前方
        rollDir = dir.normalized;

        // 蹲姿起滚先站起（否则碰撞体停在半高，滚完卡住）
        if (crouching && CanStandUp()) SetCrouch(false);

        rolling = true;
        rollTimer = 0f;
        if (anim != null) anim.PlayRoll();
    }

    /// <summary>翻滚计时；到点结束并进入冷却。</summary>
    private void RollTick()
    {
        rollTimer += Time.deltaTime;
        if (rollTimer >= rollDuration)
        {
            rolling = false;
            rollCooldownUntil = Time.time + rollCooldown;
        }
    }

}
