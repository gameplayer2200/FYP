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

    [Header("蹲伏")]
    [SerializeField] private float crouchHeight = 1.2f; // 蹲下时碰撞体高度

    [Header("鼠标视角")]
    [SerializeField] private float mouseSensitivity = 3f; // 鼠标灵敏度
    [SerializeField] private float pitchMin = -40f;       // 最低俯视角（度）
    [SerializeField] private float pitchMax = 60f;        // 最高仰视角（度）

    [Header("第一人称")]
    [SerializeField] private float eyeHeightStand = 1.62f; // 站立视线高度
    [SerializeField] private float eyeHeightCrouch = 1.0f; // 蹲下视线高度
    [SerializeField] private float eyeForward = 0.12f;     // 眼睛向前偏移（防贴脸穿模）

    [Header("重力")]
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private Animator animator;
    private Transform camTransform;
    private float camPitch = 12f;
    private float verticalVelocity;
    private Vector3 horizontalVel;

    private bool crouching;
    private float standHeight;
    private Vector3 standCenter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        camTransform = transform.Find("Camera");
        if (camTransform != null)
        {
            // 第一人称：相机固定在眼高，水平视角归零（第三人称遗留的俯角清除）
            camTransform.localPosition = new Vector3(0f, eyeHeightStand, eyeForward);
            camTransform.localRotation = Quaternion.identity;
            camPitch = 0f;
            var cam = camTransform.GetComponent<Camera>();
            if (cam != null) cam.nearClipPlane = 0.1f;
        }
        // 角色模型设为仅投影：第一人称看不见自己，但世界里保留影子
        foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        standHeight = controller.height;
        standCenter = controller.center;
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

        // 蹲伏（按住 Ctrl，仅地面生效；站起前检测头顶是否有遮挡）
        bool wantCrouch = Input.GetKey(KeyCode.LeftControl) && controller.isGrounded;
        if (wantCrouch && !crouching)
            SetCrouch(true);
        else if (!wantCrouch && crouching && CanStandUp())
            SetCrouch(false);

        // 跳跃（蹲下时不可跳）
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded && !crouching)
            verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);

        // 第一人称视线高度随蹲伏平滑升降
        if (camTransform != null)
        {
            Vector3 lp = camTransform.localPosition;
            lp.y = Mathf.Lerp(lp.y, crouching ? eyeHeightCrouch : eyeHeightStand, 10f * Time.deltaTime);
            lp.z = eyeForward;
            camTransform.localPosition = lp;
        }

        // 输入是身体本地方向：W 前 / S 后 / A 左移 / D 右移
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);
        bool moving = input.sqrMagnitude > 0.01f;
        bool backward = moving && v < -0.3f;
        bool strafe = moving && !backward && Mathf.Abs(h) >= Mathf.Abs(v);
        bool running = moving && !backward && !strafe && !crouching && Input.GetKey(KeyCode.LeftShift);

        // 加速度模型：速度向目标值渐变；冲刺只给前进；蹲走限速
        float targetSpeed = moving ? (crouching ? crouchSpeed : (running ? runSpeed : walkSpeed)) : 0f;
        Vector3 moveDir = transform.TransformDirection(input);
        moveDir.y = 0f;
        float rate = moving ? acceleration : deceleration;
        horizontalVel = Vector3.MoveTowards(horizontalVel, moveDir * targetSpeed, rate * Time.deltaTime);

        // 动画参数：Speed 按实际速度归一化（走满=0.5、跑满=1）；Backward 倒放 Walk；
        // Crouch 切蹲姿；Grounded 驱动跳跃三段（Start→Loop→Land）
        if (animator != null)
        {
            float speedNorm = Mathf.Clamp01(horizontalVel.magnitude / runSpeed);
            animator.SetFloat("Speed", speedNorm, 0.05f, Time.deltaTime);
            animator.SetBool("Backward", backward);
            animator.SetBool("Crouch", crouching);
            animator.SetBool("Grounded", controller.isGrounded);
        }

        // 用 CharacterController.Move 移动（含垂直分量）
        Vector3 motion = horizontalVel;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
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
