using UnityEngine;

/// <summary>
/// 任意门：开关门 + 施法演出（SpellEnter → 开门 → SpellExit）。
/// 交互由 InteractionRunner 统一分发（Interact 被调用时才开关）；
/// 动画通过 PlayerAnimatorProxy 播放（不再直接摸玩家的 Animator）。
/// </summary>
public class AnywhereDoor : MonoBehaviour, IInteractable
{
    // 铰链轴：门板绕哪条本地轴摆。任意门套件（现役八扇）枢轴在本地 Z；
    // 第三方套件门的网格枢轴各异，按实测「哪条本地轴指向世界上方」配置
    private enum SwingAxis { LocalZ, LocalX, LocalY }

    [Header("开门设置")]
    [SerializeField] private float openAngle = 90f;   // 开门角度（度，绕本地 Z 轴）
    [SerializeField] private SwingAxis swingAxis = SwingAxis.LocalZ; // 摆动轴（默认 Z = 任意门套件枢轴约定）
    [SerializeField] private float duration = 0.5f;   // 开/关耗时（秒）
    [SerializeField] private float autoCloseDelay = 10f; // 开门后自动关闭延时（秒），0 = 不自动关
    [SerializeField] private bool respondToKey = true;   // 是否参与交互（远端展示门设 false）
    [SerializeField] private bool locked = false;        // 锁死：按 E 不开门、只弹 lockedHint（装饰门/封闭区用）
    [SerializeField] private string lockedHint = "这扇门无法打开"; // 锁死时按 E 的提示文案
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

    // ── 门阈校准 ──────────────────────────────────────────────
    // 开门时如果对面世界还只是个"预载完成但没激活"的空壳，门板一开玩家就会看见
    // 一片空场，紧接着激活的整帧重活砸下来 —— 这就是"走到门前卡一下"的来源。
    // 所以按 E 时先向 WorldStreamer 请求激活，把门板旋转**压后**到对面就绪，
    // 等待成本落进挥法前摇（玩家看不见的空档），而不是落在开门那一帧。
    /// <summary>门阈校准最长等多久（秒）。超过就照常开门——宁可门洞里空一瞬，也不能卡着不开。</summary>
    private const float ThresholdWaitMax = 2.5f;
    /// <summary>本次动作要不要等对面世界就绪（自动关/配对镜像都不等，它们不该被装载拖慢）。</summary>
    private bool waitThreshold;
    /// <summary>本门所属的传送门（取景屏）。运行时自动找，找不到就退化为普通门。</summary>
    private PortalWindow portal;

    private void Start()
    {
        if (portal == null) portal = GetComponentInParent<PortalWindow>();
        if (portal == null) portal = GetComponentInChildren<PortalWindow>(true);
    }

    private void Awake()
    {
        closedLocalRot = transform.localRotation;
        openLocalRot = closedLocalRot * SwingDelta(openAngle);
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
        if (locked) { HintToast.Show(lockedHint); return; }
        if (!respondToKey) return;
        if (anim == null && player != null) anim = player.GetComponent<PlayerAnimatorProxy>();
        // 开门是玩家主动动作：这时去请求对面世界激活，并允许门板等它就绪（见门阈校准）。
        // 关门不需要——对面已经在眼前了，等它只会拖慢。
        waitThreshold = !IsOpen;
        if (waitThreshold) RequestPartnerActivation();
        Toggle();
    }

    /// <summary>请世界流送器马上激活本门通往的世界（幂等；取景未建立或没有取景屏时静默跳过）。</summary>
    private void RequestPartnerActivation()
    {
        if (portal == null) portal = GetComponentInParent<PortalWindow>();
        if (portal == null) return;
        string target = portal.TargetWorldName;
        if (!string.IsNullOrEmpty(target)) WorldStreamer.RequestActivation(target);
    }

    /// <summary>对面世界名（给门阈校准屏显示用；没有取景屏时返回 null）。</summary>
    private string PortalTargetWorldName()
    {
        if (portal == null) portal = GetComponentInParent<PortalWindow>();
        return portal != null ? portal.TargetWorldName : null;
    }

    /// <summary>对面世界是否已可看（没有取景屏 / 无目标世界时视为已就绪，退化成普通门）。</summary>
    private bool PartnerReady()
    {
        if (portal == null) return true;
        string target = portal.TargetWorldName;
        if (string.IsNullOrEmpty(target)) return true;
        return WorldStreamer.IsWorldReady(target);
    }

    public void Toggle() { SetOpen(!IsOpen); }

    // 配对门同步用：远端门也要能 E（穿到异世界后从那边开门回来）
    public void SetRespondToKey(bool on) { respondToKey = on; }

    // 运行时改开门角度（Awake 缓存的 openLocalRot 需要重算）
    public void ConfigureOpenAngle(float angle)
    {
        openAngle = angle;
        openLocalRot = closedLocalRot * SwingDelta(openAngle);
    }

    // 摆动增量旋转：按配置的铰链轴生成（默认 Z 兼容任意门套件八扇现役门）
    private Quaternion SwingDelta(float angle)
    {
        switch (swingAxis)
        {
            case SwingAxis.LocalX: return Quaternion.Euler(angle, 0f, 0f);
            case SwingAxis.LocalY: return Quaternion.Euler(0f, angle, 0f);
            default: return Quaternion.Euler(0f, 0f, angle);
        }
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

        // 门阈校准：开门时把"挥法到门开始转"这段当成缓冲，边挥边等对面世界激活完成。
        // 等待上限 ThresholdWaitMax，超时就照常开门 —— 门绝不能因为装载慢而不开。
        // 遮蔽屏只在真的需要等时才挂出来（ThresholdLoadScreen 内部还有 0.18s 犹豫期，
        // 短等待一闪而过的面板比不显示更糟）。
        float waited = 0f;
        bool screening = false;
        while (open && waitThreshold && waited < spellWindup)
        {
            yield return null;
            waited += Time.deltaTime;
            if (PartnerReady()) break;
            if (!screening) { screening = true; ThresholdLoadScreen.Show(PortalTargetWorldName()); }
        }
        // 前摇还没走完就继续补足（挥法的时间不能被装载等待吃掉，否则演出会突然变快）
        if (waited < spellWindup) yield return new WaitForSeconds(spellWindup - waited);

        // 前摇结束后再给一次机会：多数情况下这时已经就绪；没就绪则继续等（仍受上限约束）。
        while (open && waitThreshold && !PartnerReady() && waited < ThresholdWaitMax)
        {
            yield return null;
            waited += Time.deltaTime;
            if (!screening) { screening = true; ThresholdLoadScreen.Show(PortalTargetWorldName()); }
        }
        if (screening) ThresholdLoadScreen.Hide();
        waitThreshold = false;

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
