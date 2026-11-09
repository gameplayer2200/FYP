using UnityEngine;

/// <summary>
/// 可拾取 / 可丢下的物理道具。
///
/// 【为什么不显示在手上】
/// 「拿在手上」属于第一人称表现层：要把道具挂到 hand_r，再用手臂 IK 把手抬进画面。
/// 这两件事在实测里咬成了反馈环——手动了道具跟着动、道具的抓握点又反过来推动手，
/// 表现为拾起时人物抽搐、多件道具叠在一处；手臂 IK 还依赖 Animator 的 IK Pass，
/// 层上一关就静默失效。这条路的复杂度远超道具本身，故不做手持表现。
///
/// 【现在的手感】
/// E 拾取 → 授予对应能力，物件收起（隐藏模型与碰撞、刚体转 kinematic）。
/// 长按鼠标右键 0.55 秒 → 把物件重新丢回世界：落在身前、带一点前抛速度，
/// 之后完全按刚体走——会落地、会滚、会被玩家撞开，也能再捡起来。
/// 拾取与丢下都在同一个脚本里闭环，不依赖玩家身上的额外组件。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HeldItem : MonoBehaviour, IInteractable
{
    public enum ItemEffect { None, Flashlight, EmfDetector, OrderTerminal, Multimeter }
    private enum State { World, Carried }

    [Header("身份")]
    [SerializeField] private string displayName = "手电筒";
    [SerializeField] private string pickupHint = "拾起手电筒——按 F 开关";
    [SerializeField] private ItemEffect effect = ItemEffect.Flashlight;

    [Header("交互")]
    [Tooltip("可交互距离（米）")]
    [SerializeField] private float interactionRange = 1.5f;

    [Header("丢下")]
    [Tooltip("长按多久算「丢下」（秒）。右键在 FPS 里常做瞄准，单击容易误丢，故用长按")]
    [SerializeField] private float dropHoldTime = 0.55f;
    [Tooltip("丢出后落在身前多远（米）")]
    [SerializeField] private float dropForward = 1.0f;
    [Tooltip("丢出后离地多高（米）")]
    [SerializeField] private float dropUp = 0.25f;
    [Tooltip("丢出时的前抛速度（米/秒）。0 = 原地放下")]
    [SerializeField] private float dropSpeed = 1.8f;
    [Tooltip("丢下后多久内不能再捡，避免刚脱手就被立刻吸回去")]
    [SerializeField] private float pickupCooldown = 0.4f;

    /// <summary>当前被携带的那一件。同一时刻只允许携带一件。</summary>
    public static HeldItem Carried { get; private set; }

    private Rigidbody body;
    private Collider[] cols;
    private Renderer[] meshes;
    private State state = State.World;
    private bool granted;
    private float dropHeldSince = -1f;
    private float nextPickupAt;

    public float InteractionRange { get { return interactionRange; } }
    public Transform GetInteractionPoint() { return transform; }
    public string DisplayName { get { return displayName; } }
    public bool IsHeld { get { return state == State.Carried; } }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        cols = GetComponentsInChildren<Collider>(true);
        meshes = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        if (state == State.World) InteractionRegistry.Register(this);
    }

    private void OnDisable()
    {
        InteractionRegistry.Unregister(this);
        if (Carried == this) Carried = null;
    }

    private void Update()
    {
        if (state != State.Carried) { dropHeldSince = -1f; return; }
        if (Cursor.lockState != CursorLockMode.Locked) { dropHeldSince = -1f; return; }

        if (Input.GetMouseButton(1))
        {
            if (dropHeldSince < 0f) dropHeldSince = Time.time;
            else if (Time.time - dropHeldSince >= dropHoldTime) { dropHeldSince = -1f; Drop(); }
        }
        else
        {
            dropHeldSince = -1f;
        }
    }

    public void Interact(GameObject player)
    {
        if (state == State.Carried) { Drop(); return; }
        if (Time.time < nextPickupAt) return;
        PickUp(player);
    }

    private void PickUp(GameObject player)
    {
        if (Carried != null && Carried != this) Carried.Drop();

        state = State.Carried;
        Carried = this;
        InteractionRegistry.Unregister(this);

        if (body != null) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.isKinematic = true; }
        SetVisible(false);
        SetColliders(false);

        // 能力只授予一次：丢下再捡不会重复播特效，也不会叠组件
        if (!granted) { granted = true; GrantEffect(player); }

        HintToast.Show(pickupHint + "　（长按右键丢下）");
    }

    /// <summary>把道具丢回世界：落到身前，之后完全交给物理。</summary>
    public void Drop()
    {
        if (state != State.Carried) return;

        Transform anchor = PlayerRef.Root != null ? PlayerRef.Root : transform;
        Vector3 forward = anchor.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 spawn = anchor.position + forward * dropForward + Vector3.up * dropUp;
        // 丢掉时才把物件放到身前；携带期间它原地不动（不可见，不参与物理）
        transform.position = spawn;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        state = State.World;
        Carried = null;
        nextPickupAt = Time.time + pickupCooldown;

        SetVisible(true);
        SetColliders(true);
        if (body != null)
        {
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.velocity = forward * dropSpeed + Vector3.up * 0.6f;
            body.angularVelocity = Random.insideUnitSphere * 1.2f;
        }

        InteractionRegistry.Register(this);
        HintToast.Show("放下" + displayName);
    }

    private void SetVisible(bool on)
    {
        if (meshes == null) return;
        for (int i = 0; i < meshes.Length; i++) if (meshes[i] != null) meshes[i].enabled = on;
    }

    private void SetColliders(bool on)
    {
        if (cols == null) return;
        for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = on;
    }

    private void GrantEffect(GameObject player)
    {
        if (player == null) return;
        switch (effect)
        {
            case ItemEffect.Flashlight:
                PlayerGear.AcquireFlashlight();
                if (player.GetComponentInChildren<Flashlight>(true) == null) player.AddComponent<Flashlight>();
                break;
            case ItemEffect.EmfDetector:
                PlayerGear.AcquireEmfDetector();
                if (player.GetComponentInChildren<EmfDetector>(true) == null) player.AddComponent<EmfDetector>();
                break;
            case ItemEffect.OrderTerminal:
                PlayerGear.AcquireOrderTerminal();
                if (player.GetComponent<OrderTerminal>() == null) player.AddComponent<OrderTerminal>();
                break;
            case ItemEffect.Multimeter:
                PlayerGear.AcquireMultimeter();
                break;
            default:
                break;
        }
    }
}
