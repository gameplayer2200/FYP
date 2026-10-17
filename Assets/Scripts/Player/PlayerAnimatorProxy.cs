using UnityEngine;

/// <summary>
/// 玩家动画唯一操作者：所有系统只发事件（PlaySpell/PlayHit/…），
/// 由本类统一翻译成 Animator 参数——避免多脚本互踩参数。
/// 挂 player（与 PlayerMovement 同级）。
/// </summary>
public class PlayerAnimatorProxy : MonoBehaviour
{
    private Animator animator;
    private readonly int speed = Animator.StringToHash("Speed");
    private readonly int backward = Animator.StringToHash("Backward");
    private readonly int crouch = Animator.StringToHash("Crouch");
    private readonly int grounded = Animator.StringToHash("Grounded");
    private readonly int hit = Animator.StringToHash("Hit");
    private readonly int dead = Animator.StringToHash("Dead");
    private readonly int charging = Animator.StringToHash("Charging");
    private readonly int spellEnter = Animator.StringToHash("SpellEnter");
    private readonly int spellExit = Animator.StringToHash("SpellExit");
    private readonly int pickUp = Animator.StringToHash("PickUp");
    private readonly int interact = Animator.StringToHash("Interact");
    private readonly int roll = Animator.StringToHash("Roll");
    private readonly int getUp = Animator.StringToHash("GetUp");
    private readonly int getUpDone = Animator.StringToHash("GetUpDone");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    // ---- 移动参数（PlayerMovement 每帧调用） ----
    public void SetMove(float speedNorm, bool isBackward, bool isCrouch, bool isGrounded)
    {
        if (animator == null) return;
        animator.SetFloat(speed, speedNorm, 0.05f, Time.deltaTime);
        animator.SetBool(backward, isBackward);
        animator.SetBool(crouch, isCrouch);
        animator.SetBool(grounded, isGrounded);
    }

    // ---- 单次演出（事件式） ----
    public void PlayHit()   { if (animator != null) animator.SetTrigger(hit); }
    public void PlayDead()  { if (animator != null) animator.SetTrigger(dead); }
    public void PlaySpellEnter() { if (animator != null) animator.SetTrigger(spellEnter); }
    public void PlaySpellExit()  { if (animator != null) animator.SetTrigger(spellExit); }
    public void SetCharging(bool on) { if (animator != null) animator.SetBool(charging, on); }

    /// <summary>拾取动作（弯腰搬起桌面物品），播完自动回 Idle。</summary>
    public void PlayPickUp() { if (animator != null) animator.SetTrigger(pickUp); }

    /// <summary>通用交互动作（按 E 与场景物件互动），播完自动回 Idle。</summary>
    public void PlayInteract() { if (animator != null) animator.SetTrigger(interact); }

    /// <summary>前滚翻（Shift 双击触发），播完自动回 Idle。</summary>
    public void PlayRoll() { if (animator != null) animator.SetTrigger(roll); }

    /// <summary>
    /// 从倒地状态爬起来（UAL2_LayToIdle，1.53s）。重生时调用。
    /// Dead 状态没有任何自动出口，必须靠这个 trigger 把它拉出来，否则会一直保持倒地姿势。
    ///
    /// 出口不用 hasExitTime：实测该转移会在进入的同一帧就判定完成（状态时间未复位），
    /// 导致起身动画被整个跳过。改为由代码在动画播完后置 GetUpDone。
    /// </summary>
    public void PlayGetUp()
    {
        if (animator == null) return;
        animator.SetBool(getUpDone, false);   // 复位出口条件
        animator.SetTrigger(getUp);
    }

    /// <summary>起身动画播完（BatterySystem 按 getUpDuration 计时后调用），放行 GetUp → Idle。</summary>
    public void SetGetUpDone(bool done)
    {
        if (animator != null) animator.SetBool(getUpDone, done);
    }
}
