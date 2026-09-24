using UnityEngine;

public class AnywhereDoor : MonoBehaviour
{
    [Header("开门设置")]
    [SerializeField] private float openAngle = 90f;   // 开门角度（度，绕本地 Z 轴）
    [SerializeField] private float duration = 0.8f;   // 开/关耗时（秒）
    [SerializeField] private float autoCloseDelay = 10f; // 开门后自动关闭延时（秒），0 = 不自动关
    [SerializeField] private bool respondToKey = true;   // 是否响应 E 键开关（远端门设 false，由连接器控制）

    public bool IsOpen { get; private set; }

    private Quaternion closedLocalRot;
    private Quaternion openLocalRot;
    private Coroutine animating;
    private Coroutine autoCloseRoutine;

    private void Awake()
    {
        // 初始姿态视为"关"，开门 = 绕本地 Z 轴转 openAngle
        closedLocalRot = transform.localRotation;
        openLocalRot = closedLocalRot * Quaternion.Euler(0f, 0f, openAngle);
        IsOpen = false;
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

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
        animating = StartCoroutine(RotateRoutine(open ? openLocalRot : closedLocalRot));
        // 开门后计时，到点自动关闭；提前手动关门则不会触发
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
        animating = null;
    }

    // 临时测试触发：按 E 开/关门；respondToKey=false 的门（远端世界门）不响应
    private void Update()
    {
        if (respondToKey && Input.GetKeyDown(KeyCode.E)) Toggle();
    }
}
