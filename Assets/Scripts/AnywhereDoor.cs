using UnityEngine;

public class AnywhereDoor : MonoBehaviour
{
    [Header("开门设置")]
    [SerializeField] private float openAngle = 90f;   // 开门角度（度，绕本地 Z 轴）
    [SerializeField] private float duration = 0.8f;   // 开/关耗时（秒）

    public bool IsOpen { get; private set; }

    private Quaternion closedLocalRot;
    private Quaternion openLocalRot;
    private Coroutine animating;

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

    public void SetOpen(bool open)
    {
        if (animating != null) { StopCoroutine(animating); animating = null; }
        IsOpen = open;
        animating = StartCoroutine(RotateRoutine(open ? openLocalRot : closedLocalRot));
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

    // 临时测试触发：按 E 开/关门；之后接入近距离交互检测后可移除
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) Toggle();
    }
}
