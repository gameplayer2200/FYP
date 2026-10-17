using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float walkSpeed = 2f;      // 走路最大速度 m/s
    [SerializeField] private float runSpeed = 4f;       // 冲刺最大速度 m/s
    [SerializeField] private float crouchSpeed = 1f;    // 蹲走最大速度 m/s
    [SerializeField] private float acceleration = 10f;  // 加速度（越大起步越快）
    [SerializeField] private float deceleration = 14f;  // 减速度（比加速大，松键少滑行）

    [Header("跳跃")]
    [SerializeField] private float jumpHeight = 1.2f;   // 跳跃高度 m

    [Header("翻滚")]
    [SerializeField] private float rollSpeed = 2.5f;      // 翻滚推进速度 m/s（1.4s 约滚 3.5m）
    [SerializeField] private float rollDuration = 1.4f;   // 翻滚推进时长（对齐 UAL1_Roll 动画 1.467s）
    [SerializeField] private float rollCooldown = 0.3f;   // 翻滚结束后的冷却（防连按）
    [SerializeField] private float doubleTapWindow = 0.25f; // Shift 双击判定窗口（秒）

    [Header("蹲伏")]
    [SerializeField] private float crouchHeight = 1.2f; // 蹲下时碰撞体高度

    [Header("鼠标视角")]
    [SerializeField] private float mouseSensitivity = 3f; // 鼠标灵敏度
    [SerializeField] private float pitchMin = -40f;       // 最低俯视角（度）
    [SerializeField] private float pitchMax = 60f;        // 最高仰视角（度）

    [Header("第一人称")]
    [Tooltip("玩家相机（player 的子物体 Camera）。留空则回退到按子物体名查找。")]
    [SerializeField] private Transform camTransform;

    [Header("重力")]
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private PlayerAnimatorProxy anim;
    private float camPitch = 12f;
    private float verticalVelocity;
    private Vector3 horizontalVel;
    private Vector3 baseCamLocalPos; // 编辑器里摆放的相机本地位置（视线高度基准）
    private Transform headBone; // 视角跟随的头部骨骼

    private bool crouching;
    private float standHeight;
    private Vector3 standCenter;

    // 翻滚状态
    private bool rolling;
    private Vector3 rollDir;      // 翻滚推进方向（触发瞬间锁定）
    private float rollTimer;
    private float rollCooldownUntil;
    private float lastShiftTapTime = -10f;

    private void Awake()
    {
        // 双玩家守卫：双世界常驻、两侧场景都可摆玩家（从 Metro 直接开发/测试）。
        // 先注册者存活；后加载场景里的玩家整组禁用——避免双 E 键、双电量、注册表被顶掉。
        if (PlayerRef.IsReady && PlayerRef.Root != transform)
        {
            gameObject.SetActive(false);
            return;
        }
        controller = GetComponent<CharacterController>();
        anim = GetComponent<PlayerAnimatorProxy>();
        // 相机优先取序列化引用；留空才回退到子物体名查找（局部查找，不是全场景 Find）
        if (camTransform == null) camTransform = transform.Find("Camera");
        Camera worldCam = null;
        if (camTransform != null)
        {
            // 相机旋转强制归零：场景里存过的运行时转身（Play 中 Ctrl+S 落盘）曾被
            // yawOff 转嫁逻辑放大成"视角反 180°"，这里不再信任场景数据——
            // 视线基准=身体朝向，俯仰由鼠标从 camPitch 起步
            baseCamLocalPos = camTransform.localPosition;
            camTransform.localRotation = Quaternion.Euler(camPitch, 0f, 0f);
            worldCam = camTransform.GetComponent<Camera>();
            if (worldCam != null && worldCam.nearClipPlane > 0.3f) worldCam.nearClipPlane = 0.1f;
        }
        // 抓 Head 骨骼（视角位置跟随用）
        foreach (var s2 in GetComponentsInChildren<SkinnedMeshRenderer>())
            foreach (var b2 in s2.bones)
                if (b2 != null && b2.name == "Head") { headBone = b2; break; }
        standHeight = controller.height;
        standCenter = controller.center;
        // 发布给全项目消费方，取代各处 GameObject.Find("player")
        PlayerRef.Register(transform, worldCam, controller);
    }

    private void OnDestroy()
    {
        PlayerRef.Unregister(transform);
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Esc 释放鼠标后，点左键重新锁定
        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 鼠标视角：水平转整个身体（相机是子物体跟着转），垂直只俯仰相机
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(0f, mouseX, 0f); // Unity 中 yaw 增大 = 向右转
        camPitch = Mathf.Clamp(camPitch - mouseY, pitchMin, pitchMax);
        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(camPitch, 0f, 0f);

        // 重力：落地时给一个小的下压力，保证 isGrounded 稳定
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;
        verticalVelocity += gravity * Time.deltaTime;

        // 蹲伏（按住 Ctrl，仅地面生效；站起前检测头顶是否有遮挡）；翻滚中禁止切换蹲姿
        bool wantCrouch = Input.GetKey(KeyCode.LeftControl) && controller.isGrounded && !rolling;
        if (wantCrouch && !crouching)
            SetCrouch(true);
        else if (!wantCrouch && crouching && CanStandUp())
            SetCrouch(false);

        // 跳跃（蹲下时不可跳；翻滚中不可跳；电量≤25 时功能已被系统关闭）
        bool jumpAllowed = BatterySystem.Instance == null || BatterySystem.Instance.JumpEnabled;
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded && !crouching && !rolling && jumpAllowed)
            verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);

        // 翻滚：Shift 双击（0.25s 内两次轻点）触发，与按住冲刺不冲突
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            if (Time.time - lastShiftTapTime <= doubleTapWindow) TryStartRoll();
            lastShiftTapTime = Time.time;
        }
        if (rolling) RollTick();

        // 视角跟随 Head 骨骼：位置完全同步（含动画摆动），旋转仍由鼠标控制（避免动画晃镜头）
        // 保险丝：Animator 初始化/重定向的瞬态会把骨骼采样到建模姿势（实测飘出 6m），
        // 骨骼离玩家根超过 1.5m 一律视为无效采样，相机保持本地位——防开场视角被拽飞
        if (headBone != null && camTransform != null)
        {
            bool headSane = (headBone.position - transform.position).sqrMagnitude < 1.5f * 1.5f;
            if (headSane)
            {
                var eyePos = headBone.position + headBone.up * 0.05f;
                camTransform.position = eyePos;
            }
            else
            {
                camTransform.localPosition = baseCamLocalPos;
            }
        }

        // 输入是身体本地方向：W 前 / S 后 / A 左移 / D 右移
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        // 充电时不许移动（可中断：按 E 或走开即停，由 ChargeStation 处理）
        if (BatterySystem.Instance != null && BatterySystem.Instance.IsCharging) { v = 0f; h = 0f; }

        if (rolling)
        {
            // 翻滚期间锁输入：沿触发瞬间锁定的方向匀速推进（动画是原地滚，位移由代码给）
            horizontalVel = rollDir * rollSpeed;
            if (anim != null) anim.SetMove(1f, false, false, controller.isGrounded);
        }
        else
        {
            Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);
            bool moving = input.sqrMagnitude > 0.01f;
            bool backward = moving && v < -0.3f;
            bool strafe = moving && !backward && Mathf.Abs(h) >= Mathf.Abs(v);
            // 冲刺需电量>50（降级链：≤50 系统关闭冲刺）
            bool sprintAllowed = BatterySystem.Instance == null || BatterySystem.Instance.SprintEnabled;
            bool running = moving && !backward && !strafe && !crouching && sprintAllowed && Input.GetKey(KeyCode.LeftShift);

            // 加速度模型：速度向目标值渐变；冲刺只给前进；蹲走限速
            float targetSpeed = moving ? (crouching ? crouchSpeed : (running ? runSpeed : walkSpeed)) : 0f;
            Vector3 moveDir = transform.TransformDirection(input);
            moveDir.y = 0f;
            float rate = moving ? acceleration : deceleration;
            horizontalVel = Vector3.MoveTowards(horizontalVel, moveDir * targetSpeed, rate * Time.deltaTime);

            // 动画参数统一走 PlayerAnimatorProxy（单头驱动，避免多脚本互踩）
            if (anim != null)
            {
                float speedNorm = Mathf.Clamp01(horizontalVel.magnitude / runSpeed);
                anim.SetMove(speedNorm, backward, crouching, controller.isGrounded);
            }
        }

        // 用 CharacterController.Move 移动（含垂直分量）
        Vector3 motion = horizontalVel;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    /// <summary>外部传送（电量重启）：清零速度。穿门请用 PlayerRef.Teleport——那条路要保留动量。</summary>
    public void TeleportTo(Vector3 position)
    {
        PlayerRef.Teleport(position, transform.rotation);
        horizontalVel = Vector3.zero;
        verticalVelocity = 0f;
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

    private void SetCrouch(bool on)
    {
        crouching = on;
        controller.height = on ? crouchHeight : standHeight;
        controller.center = new Vector3(standCenter.x, on ? crouchHeight * 0.5f : standCenter.y, standCenter.z);
    }

    private bool CanStandUp()
    {
        // 从蹲姿胶囊顶部向上检测站立所需的额外空间
        Vector3 origin = transform.position + controller.center + Vector3.up * (crouchHeight * 0.5f - 0.05f);
        return !Physics.Raycast(origin, Vector3.up, standHeight - crouchHeight + 0.1f);
    }
}
