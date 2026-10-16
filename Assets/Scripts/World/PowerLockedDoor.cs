using System.Collections;
using UnityEngine;

/// <summary>
/// 供电联动的锁闭门（尾节车厢端门）：停电期间锁死，E 只给提示；
/// 全站复明后自动开闸——门板旋转 + 关闭阻挡碰撞体。
/// </summary>
public class PowerLockedDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private Vector3 openEuler = new Vector3(0f, 100f, 0f);
    [SerializeField] private float openDuration = 1.2f;
    [Tooltip("开门时要关闭的阻挡碰撞体")]
    [SerializeField] private Collider blockingCollider;
    [SerializeField] private string lockedHint = "车厢门锁死——似乎和车站的供电联动";

    private bool opened;

    public float InteractionRange { get { return 2.5f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (!opened) HintToast.Show(lockedHint);
    }

    private void OnEnable()
    {
        InteractionRegistry.Register(this);
        MetroBlackout.OnPowerRestored += HandlePowerRestored;
        if (MetroBlackout.PowerRestored) HandlePowerRestored();
    }

    private void OnDisable()
    {
        InteractionRegistry.Unregister(this);
        MetroBlackout.OnPowerRestored -= HandlePowerRestored;
    }

    private void HandlePowerRestored()
    {
        if (opened) return;
        opened = true;
        if (blockingCollider != null) blockingCollider.enabled = false;
        StartCoroutine(OpenRoutine()); // 禁用组件不会停协程，只有销毁才会
        HintToast.Show("车厢门开了");
        enabled = false; // 开启后让出交互位
    }

    private IEnumerator OpenRoutine()
    {
        Transform t = doorLeaf != null ? doorLeaf : transform;
        Quaternion start = t.localRotation;
        Quaternion end = Quaternion.Euler(openEuler);
        float e = 0f;
        while (e < openDuration)
        {
            e += Time.deltaTime;
            t.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, e / openDuration));
            yield return null;
        }
        t.localRotation = end;
    }
}
