using UnityEngine;

/// <summary>
/// 任意门：开关门 + 施法演出（SpellEnter → 开门 → SpellExit）。
/// 交互由 InteractionRunner 统一分发（Interact 被调用时才开关）；
/// 动画通过 PlayerAnimatorProxy 播放（不再直接摸玩家的 Animator）。
/// </summary>
public class AnywhereDoor : MonoBehaviour, IInteractable
{
    [Header("开门设置")]
    [SerializeField] private float openAngle = 90f;   // 开门角度（度，绕本地 Z 轴）
    [SerializeField] private float duration = 0.5f;   // 开/关耗时（秒）
    [SerializeField] private float autoCloseDelay = 10f; // 开门后自动关闭延时（秒），0 = 不自动关
    [SerializeField] private bool respondToKey = true;   // 是否参与交互（远端展示门设 false）
    [SerializeField] private bool spellOpen = true;      // 开门时播放施法演出
    [SerializeField] private bool startOpen = false;     // 初始即为开态（demo 常开门用，无动画直接摆到开）

    [Header("施法时序")]
    [SerializeField] private float spellWindup = 0.5f;  // 挥法到门动的延迟
    [SerializeField] private float spellOutro = 0.1f;   // 收势缓冲

    public bool IsOpen { get; private set; }
    public float OpenAngle { get { return openAngle; } } // 配对门同步摆角用
    public float InteractionRange { get { return 3f; } }

    /// <summary>
    /// 门板**实际**开合进度：0 = 完全合上（门板盖住门洞），1 = 完全打开。
    ///
    /// 与 IsOpen 的区别很关键：IsOpen 是**意图**——SetOpen 一被调用就翻，
    /// 而门板还要走完 spellWindup(1.2s) + 旋转(0.8s) + spellOutro(0.6s) 才到位。
    /// 传送门的背板材质切换必须看这个，否则「门还在慢慢转回去，门洞先黑了」。
    ///
    /// 用门板当前朝向与关闭朝向的夹角反推，所以对任意 openAngle（含 Metro 世界门的 -180）都成立。
    /// </summary>
    public float OpenFraction
    {
        get
        {
            float total = Mathf.Abs(openAngle);
            if (total < 0.01f) return IsOpen ? 1f : 0f;   // 开合角为 0：没有动画可言，直接跟意图
            float cur = Quaternion.Angle(closedLocalRot, transform.localRotation);
            return Mathf.Clamp01(cur / total);
        }
    }

    private Quaternion closedLocalRot;
    private Quaternion openLocalRot;
    private Coroutine animating;
    private Coroutine autoCloseRoutine;
    private PlayerAnimatorProxy anim;

    private void Awake()
    {
        closedLocalRot = transform.localRotation;
        openLocalRot = closedLocalRot * Quaternion.Euler(0f, 0f, openAngle);
        IsOpen = false;
        if (startOpen) { transform.localRotation = openLocalRot; IsOpen = true; }
        // 交互可发现性保障：InteractionRunner 靠物理扫描找交互物，
        // 本物体或父链上没有 Collider 时自动补一个触发体（不挡人不挡门）
        if (GetComponentInParent<Collider>(true) == null && GetComponent<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(1.4f, 2.2f, 0.6f);
        }
    }

    public Transform GetInteractionPoint() { return transform; }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }

    public void Interact(GameObject player)
    {
        if (!respondToKey) return;
        if (anim == null && player != null) anim = player.GetComponent<PlayerAnimatorProxy>();
        Toggle();
    }

    public void Toggle() { SetOpen(!IsOpen); }

    // 配对门同步用：远端门也要能 E（穿到异世界后从那边开门回来）
    public void SetRespondToKey(bool on) { respondToKey = on; }

    // 运行时改开门角度（Awake 缓存的 openLocalRot 需要重算）
    public void ConfigureOpenAngle(float angle)
    {
        openAngle = angle;
        openLocalRot = closedLocalRot * Quaternion.Euler(0f, 0f, openAngle);
    }

    public void SetOpen(bool open)
    {
        SetOpen(open, true);
    }

    /// <summary>带自动关计时的门：保留旧签名，演出默认跟随本次动作的发起方。</summary>
    public void SetOpen(bool open, bool scheduleAutoClose)
    {
        SetOpen(open, scheduleAutoClose, true);
    }

    /// <summary>
    /// 门体开关的唯一入口。
    ///
    /// playSpell 决定**玩家的施法演出**要不要播——它与门体动画是两件事，必须能独立：
    ///   · 玩家按 E 开门 / 按 E 关门 → true（人做的动作，抬手有反馈）
    ///   · 自动关（到点 / 撤离锁门 / 配对门镜像） → false（没有人在推门，玩家不该跟着抬手）
    ///
    /// scheduleAutoClose=false 供配对门镜像调用：自动关计时只由被交互的那扇门排，
    /// 否则两边各排各的计时器、到期后互相触发成级联。
    /// </summary>
    public void SetOpen(bool open, bool scheduleAutoClose, bool playSpell)
    {
        if (animating != null) { StopCoroutine(animating); animating = null; }
        if (autoCloseRoutine != null) { StopCoroutine(autoCloseRoutine); autoCloseRoutine = null; }
        IsOpen = open;
        animating = StartCoroutine(OpenSequence(open, scheduleAutoClose, playSpell));
    }

    // 完整时序：挥法 → 门旋转 → 收势；开门后计时自动关
    // playSpell=false 时整段只动门、不碰玩家动画（门自动关闭就属于这种）
    private System.Collections.IEnumerator OpenSequence(bool open, bool scheduleAutoClose, bool playSpell)
    {
        bool spell = spellOpen && playSpell && anim != null;
        if (spell) anim.PlaySpellEnter();
        yield return new WaitForSeconds(spellWindup);
        yield return StartCoroutine(RotateRoutine(open ? openLocalRot : closedLocalRot));
        if (spell) anim.PlaySpellExit();
        yield return new WaitForSeconds(spellOutro);
        animating = null;
        if (open && scheduleAutoClose && autoCloseDelay > 0f)
            autoCloseRoutine = StartCoroutine(AutoCloseRoutine());
    }

    private System.Collections.IEnumerator AutoCloseRoutine()
    {
        yield return new WaitForSeconds(autoCloseDelay);
        autoCloseRoutine = null;
        // 自动关闭 = 门自己的事，玩家不抬手（这是与"人按 E 关门"的唯一差别）
        SetOpen(false, true, false);
    }

    private System.Collections.IEnumerator RotateRoutine(Quaternion target)
    {
        Quaternion start = transform.localRotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = t * t * (3f - 2f * t); // smoothstep：门有加减速，不生硬
            transform.localRotation = Quaternion.Slerp(start, target, eased);
            yield return null;
        }
        transform.localRotation = target;
    }
}
