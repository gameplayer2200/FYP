using UnityEngine;

/// <summary>
/// 电量系统：AI 调查员没有血量，受创掉电；低电量自动关闭部分功能保命；
/// 只有在充电桩充回满电（100）才恢复全部功能。
/// 降级链：≤50 锁冲刺，≤25 锁跳跃，蹲伏不锁。0 = Death01 瘫倒，强制休眠后回任意门旁重启（电量30）。
/// F 键 = 模拟受创（测试用，怪物 AI 接入后移除）。
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
    [SerializeField] private float restartDelay = 3f;
    [SerializeField] private Vector3 restartOffset = new Vector3(1.5f, 0.2f, 0f);

    public float Battery { get; private set; }
    public bool SprintEnabled { get { return !IsDead && Battery > sprintLockThreshold; } }
    public bool JumpEnabled { get { return !IsDead && Battery > jumpLockThreshold; } }
    public bool IsDead { get; private set; }
    public bool IsCharging { get; private set; }

    private PlayerAnimatorProxy anim;
    private Transform labDoor;
    private bool lastSprint, lastJump;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        anim = GetComponentInParent<PlayerAnimatorProxy>(); if (anim == null) anim = GetComponent<PlayerAnimatorProxy>();
    }

    private void Start()
    {
        Battery = maxBattery;
        var door = GameObject.Find("anywhere-door");
        if (door != null) labDoor = door.transform;
        Notify();
    }

    /// <summary>受创接口：怪物攻击/危险区调用。</summary>
    public void TakeHit()
    {
        if (IsDead) return;
        Battery = Mathf.Max(0f, Battery - hitDrain);
        if (anim != null) anim.PlayHit();
        Debug.Log("[Battery] 受创 -" + hitDrain + " 剩余 " + (int)Battery + " 冲刺=" + SprintEnabled + " 跳跃=" + JumpEnabled);
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
        if (labDoor != null)
        {
            var pm = GetComponentInParent<PlayerMovement>();
            if (pm != null) pm.TeleportTo(labDoor.position + restartOffset);
            else transform.position = labDoor.position + restartOffset;
        }
        Battery = restartBattery;
        IsDead = false;
        Notify();
        Debug.Log("[Battery] 重启完成，电量 " + (int)Battery);
    }

    /// <summary>充电桩调用：true=持续充电（满电自动停止并恢复全部功能）。</summary>
    public void SetCharging(bool on)
    {
        if (IsDead) on = false;
        if (IsCharging == on) return;
        IsCharging = on;
        if (anim != null && on) anim.SetCharging(true);
    }

    private void Update()
    {
        // F 键测试受创
        if (!IsDead && Input.GetKeyDown(KeyCode.F)) TakeHit();

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
        GUI.color = Battery > sprintLockThreshold ? new Color(0.22f, 0.86f, 0.95f) : new Color(0.95f, 0.4f, 0.2f);
        GUI.Box(new Rect(20f, Screen.height - 46f, 170f, 22f), "");
        GUI.Box(new Rect(22f, Screen.height - 44f, 166f * (Battery / maxBattery), 18f), "");
        GUI.color = Color.white;
        GUI.Label(new Rect(24f, Screen.height - 44f, 320f, 20f),
            "BATTERY " + (int)Battery + (SprintEnabled ? "" : "  [冲刺已关闭]") + (JumpEnabled ? "" : " [跳跃已关闭]"));
    }
}
