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
    public float InteractionRange { get { return 3f; } }

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

    public void Interact(GameObject player)
    {
        if (!respondToKey) return;
        if (anim == null && player != null) anim = player.GetComponent<PlayerAnimatorProxy>();
        Toggle();
    }

    public void Toggle() { SetOpen(!IsOpen); }

    // 运行时改开门角度（Awake 缓存的 openLocalRot 需要重算）
    public void ConfigureOpenAngle(float angle)
    {
        openAngle = angle;
        openLocalRot = closedLocalRot * Quaternion.Euler(0f, 0f, openAngle);
    }

    public void SetOpen(bool open)
    {
        if (animating != null) { StopCoroutine(animating); animating = null; }
        if (autoCloseRoutine != null) { StopCoroutine(autoCloseRoutine); autoCloseRoutine = null; }
        IsOpen = open;
        animating = StartCoroutine(OpenSequence(open));
    }

    // 完整时序：挥法 → 门旋转 → 收势；开门后计时自动关
    private System.Collections.IEnumerator OpenSequence(bool open)
    {
        if (spellOpen && anim != null) anim.PlaySpellEnter();
        yield return new WaitForSeconds(spellWindup);
        yield return StartCoroutine(RotateRoutine(open ? openLocalRot : closedLocalRot));
        if (spellOpen && anim != null) anim.PlaySpellExit();
        yield return new WaitForSeconds(spellOutro);
        animating = null;
        if (open && autoCloseDelay > 0f)
            autoCloseRoutine = StartCoroutine(AutoCloseRoutine());
    }

    private System.Collections.IEnumerator AutoCloseRoutine()
    {
        yield return new WaitForSeconds(autoCloseDelay);
        autoCloseRoutine = null;
        SetOpen(false);
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
