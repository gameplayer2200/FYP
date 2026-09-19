using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float walkSpeed = 2f;      // 走路最大速度 m/s
    [SerializeField] private float runSpeed = 4f;       // 冲刺最大速度 m/s
    [SerializeField] private float acceleration = 10f;  // 加速度（越大起步越快）
    [SerializeField] private float deceleration = 14f;  // 减速度（比加速大，松键少滑行）

    [Header("鼠标视角")]
    [SerializeField] private float mouseSensitivity = 3f; // 鼠标灵敏度
    [SerializeField] private float pitchMin = -40f;       // 最低俯视角（度）
    [SerializeField] private float pitchMax = 60f;        // 最高仰视角（度）

    [Header("重力")]
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private Animator animator;
    private Transform camTransform;
    private float camPitch = 12f;
    private float verticalVelocity;
    private Vector3 horizontalVel;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        camTransform = transform.Find("Camera");
        if (camTransform != null)
        {
            float x = camTransform.localEulerAngles.x;
            camPitch = x > 180f ? 0f : x; // 相机当前俯仰作为起点
        }
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

        // 输入是身体本地方向：W 前 / S 后 / A 左移 / D 右移
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);
        bool moving = input.sqrMagnitude > 0.01f;
        bool backward = moving && v < -0.3f;
        bool strafe = moving && !backward && Mathf.Abs(h) >= Mathf.Abs(v);
        bool running = moving && !backward && !strafe && Input.GetKey(KeyCode.LeftShift);

        // 加速度模型：速度向目标值渐变；冲刺只给前进
        float targetSpeed = moving ? (running ? runSpeed : walkSpeed) : 0f;
        Vector3 moveDir = transform.TransformDirection(input);
        moveDir.y = 0f;
        float rate = moving ? acceleration : deceleration;
        horizontalVel = Vector3.MoveTowards(horizontalVel, moveDir * targetSpeed, rate * Time.deltaTime);

        // 动画：Speed 按实际速度归一化（走满=0.5、跑满=1）；Backward 切倒放 Walk；侧移复用 Walk 循环
        if (animator != null)
        {
            float speedNorm = Mathf.Clamp01(horizontalVel.magnitude / runSpeed);
            animator.SetFloat("Speed", speedNorm, 0.08f, Time.deltaTime);
            animator.SetBool("Backward", backward);
        }

        // 用 CharacterController.Move 移动（含垂直分量）
        Vector3 motion = horizontalVel;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }
}
