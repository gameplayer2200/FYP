using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public partial class PlayerMovement : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float walkSpeed = 2f;      // 走路最大速度 m/s
    [SerializeField] private float runSpeed = 4f;       // 冲刺最大速度 m/s
    [SerializeField] private float crouchSpeed = 1f;    // 蹲走最大速度 m/s
    [SerializeField] private float acceleration = 10f;  // 加速度（越大起步越快）
    [SerializeField] private float deceleration = 14f;  // 减速度（比加速大，松键少滑行）

    [Header("跳跃")]
    [SerializeField] private float jumpHeight = 1.2f;   // 跳跃高度 m

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

    // 脚步噪音：按累计行走距离触发，不是每帧广播（敌人经 NoiseBus 听）
    private float noiseDistanceAccum;

    // 动画用的落地态（见 ComputeAnimGrounded）。
    // controller.isGrounded 只在 Move() 真正触地的那一帧为真：传送（PlayerRef.Teleport 会
    // 禁用再启用 CharacterController）、台阶边缘、斜坡、蹲起改碰撞体高度都会让它单帧闪 false。
    // 而动画控制器里 JumpStart 没有回 Idle 的出口（只有 JumpStart → JumpLoop → JumpLand → Idle），
    // 所以一次闪断就要把整套「起跳→滞空→落地」播完——表现就是人站在地上却变成空中落下的动画。
    private bool animGrounded = true;
    private float groundedGraceUntil;
    private Vector3 lastPosForTeleport;
    private const float GroundedGrace = 0.12f;   // 落地宽限：闪断不超过这个时长就不算离地
    private const float FallVelocity = 2.5f;     // 垂直速度超过这个下行值才算真的在坠落（米/秒）

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
        lastPosForTeleport = transform.position;
        groundedGraceUntil = Time.time + GroundedGrace;
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

        // 开局部署期间（PlayerInputGate.Locked）：允许转视角，但冻结一切"会发生的事"。
        // 早退而不是禁用组件——禁用会打断 Awake/OnEnable 建立的缓存，放开时容易漏项。
        // 只跳过移动/跳跃/蹲伏，鼠标视角已经在上面处理完了，所以等待时仍能环视。
        if (PlayerInputGate.Locked)
        {
            // 重力照常：冻结时可能正从空中落下，不能让它悬在半空
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;
            verticalVelocity += gravity * Time.deltaTime;
            horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, deceleration * Time.deltaTime);
            if (anim != null) anim.SetMove(0f, false, false, animGrounded);
            controller.Move(new Vector3(horizontalVel.x, verticalVelocity, horizontalVel.z) * Time.deltaTime);
            return;
        }

        // 重力：落地时给一个小的下压力，保证 isGrounded 稳定
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;
        verticalVelocity += gravity * Time.deltaTime;

        // 动画落地态：带滞回，避免单帧闪断把起跳动画整套播完（详见字段注释）
        UpdateAnimGrounded();

        // 重生起身中：锁移动 / 跳跃 / 翻滚 / 蹲伏。
        // 起身动画（UAL2_LayToIdle）要 1.5s，不锁的话角色会边爬起边滑走。
        // 倒地（IsDead）同样要锁：电量归零后 restartDelay（默认 3s）里人还躺着，
        // 只判 IsRecovering 会让这 3 秒能爬着走——看着像尸体在滑行。
        bool dead = BatterySystem.Instance != null && BatterySystem.Instance.IsDead;
        bool recovering = BatterySystem.Instance != null && BatterySystem.Instance.IsRecovering;
        bool bodyLocked = dead || recovering;

        // 蹲伏（按住 Ctrl，仅地面生效；站起前检测头顶是否有遮挡）；翻滚/起身中禁止切换蹲姿
        bool wantCrouch = Input.GetKey(KeyCode.LeftControl) && controller.isGrounded && !rolling && !bodyLocked;
        if (wantCrouch && !crouching)
            SetCrouch(true);
        else if (!wantCrouch && crouching && CanStandUp())
            SetCrouch(false);

        // ── 跳跃（蓄力版）：点按 = 普通跳；按住攒力、松开 = 超级跳(更高更远+耗电) ──
        // 起步条件：在地面、非蹲、非翻滚、非起身、电量允许。
        bool canJumpState = controller.isGrounded && !crouching && !rolling && !bodyLocked;
        bool jumpAllowed = BatterySystem.Instance == null || BatterySystem.Instance.JumpEnabled;

        HandleJump(canJumpState, jumpAllowed);

        // 翻滚：Shift 双击（0.25s 内两次轻点）触发，与按住冲刺不冲突；倒地/起身中不可翻滚
        HandleRoll(bodyLocked);

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
        // 倒地/起身时同样锁移动：否则角色会边躺着/边爬起边滑走
        if (BatterySystem.Instance != null && (BatterySystem.Instance.IsCharging || bodyLocked)) { v = 0f; h = 0f; }

        if (rolling)
        {
            // 翻滚期间锁输入：沿触发瞬间锁定的方向匀速推进（动画是原地滚，位移由代码给）
            horizontalVel = rollDir * rollSpeed;
            if (anim != null) anim.SetMove(1f, false, false, animGrounded);
        }
        else if (bodyLocked)
        {
            // 倒地/起身：横向速度立刻归零，人不会再往前滑。
            // 用减速渐变会残留半米滑行（实测 6 m/s 起手滑出 0.42m），躺着滑行很出戏。
            horizontalVel = Vector3.zero;
            if (anim != null) anim.SetMove(0f, false, false, animGrounded);
        }
        else if (TrySuperJumpBoost(h, v))
        {
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
                anim.SetMove(speedNorm, backward, crouching, animGrounded);
            }
        }

        // 用 CharacterController.Move 移动（含垂直分量）
        Vector3 motion = horizontalVel;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        // ── 脚步噪音上报（敌人经 NoiseBus 听觉感知）─────────────────────
        // 响度约定：蹲行 0.2（多数敌人听不见）/ 走路 0.55 / 奔跑 1.0。
        // 按累计行走距离触发，避免每帧广播；出门落地那一下顺手也算一记。
        float speedNow = horizontalVel.magnitude;
        if (controller.isGrounded && speedNow > 0.25f)
        {
            float loudness = crouching
                ? CrouchNoise
                : (speedNow > (walkSpeed + runSpeed) * 0.5f ? RunNoise : WalkNoise);

            noiseDistanceAccum += speedNow * Time.deltaTime;
            float stride = crouching ? 1.1f : 0.8f; // 蹲着步子短，触发距离换算成更稀疏
            if (noiseDistanceAccum >= stride)
            {
                noiseDistanceAccum = 0f;
                NoiseBus.Emit(transform.position, loudness);
            }
        }
        else
        {
            noiseDistanceAccum = 0f;
        }
    }

    private const float CrouchNoise = 0.2f;
    private const float WalkNoise = 0.55f;
    private const float RunNoise = 1f;

    /// <summary>
    /// 维护「动画用的落地态」。直接拿 controller.isGrounded 喂 Animator 会出问题：
    /// 它只在 Move() 触地那一帧为真，传送（CharacterController 被禁用再启用）、
    /// 台阶边缘、斜坡、蹲起改高度都可能让它单帧闪 false。而动画控制器里 JumpStart
    /// 没有回 Idle 的出口，只有 JumpStart → JumpLoop → JumpLand → Idle，
    /// 所以一次闪断就会把整套「起跳 → 滞空 → 落地」播完 —— 站在地上却做出空中落下的动作。
    ///
    /// 三重判据（任一成立即视为在地面）：
    ///   ① isGrounded 为真；
    ///   ② 距上次触地不超过 GroundedGrace（吸收单帧闪断）；
    ///   ③ 垂直速度还没真正掉下去（|v| &lt; FallVelocity，人没在往下坠就不是在飞）。
    /// 只有三条都不成立才判离地。
    /// </summary>
    private void UpdateAnimGrounded()
    {
        // 瞬移检测：一帧内位移超过 3m 只可能是穿门，不当成离地
        Vector3 delta = transform.position - lastPosForTeleport;
        if (delta.sqrMagnitude > 9f)
        {
            MarkGrounded();
            lastPosForTeleport = transform.position;
            return;
        }
        lastPosForTeleport = transform.position;

        // 真起跳（明显向上）立刻判离地，不享受宽限——否则跳跃动画会晚 0.12s 才起
        if (verticalVelocity > 0.5f)
        {
            animGrounded = false;
            return;
        }

        if (controller.isGrounded)
        {
            MarkGrounded();
            return;
        }

        if (Time.time < groundedGraceUntil) { animGrounded = true; return; }

        // 还没真正往下坠（小台阶、斜坡、碰撞体高度变化）→ 仍然算在地面
        if (verticalVelocity > -FallVelocity) { MarkGrounded(); return; }

        animGrounded = false;
    }

    private void MarkGrounded()
    {
        animGrounded = true;
        groundedGraceUntil = Time.time + GroundedGrace;
    }

    /// <summary>当前移动输入方向（世界空间，水平），无输入则朝玩家正前方。供超级跳冲刺用。</summary>
    private Vector3 PlayerInputDir()
    {
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);
        if (input.sqrMagnitude > 0.01f) return transform.TransformDirection(input);
        return transform.forward;
    }

    /// <summary>外部传送（电量重启）：清零速度。穿门请用 PlayerRef.Teleport——那条路要保留动量。</summary>
    public void TeleportTo(Vector3 position)
    {
        PlayerRef.Teleport(position, transform.rotation);
        horizontalVel = Vector3.zero;
        verticalVelocity = 0f;
        // 传送后必然有一段 isGrounded=false，直接判为落地并刷新宽限，避免播起跳/落地动画
        animGrounded = true;
        groundedGraceUntil = Time.time + GroundedGrace;
        lastPosForTeleport = transform.position;
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
