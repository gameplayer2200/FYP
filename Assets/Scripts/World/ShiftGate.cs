using System.Collections;
using UnityEngine;

/// <summary>
/// 深区门（工厂规则件，昼/夜两场景镜像各一扇）：昼侧门在谜题完成前锁死（白班深区不开放）；
/// 夜侧门在夜班进行中开放；谜题完成后双侧常开（工厂已稳定）。
/// 开放条件满足时自动开闸（换班那一刻亲眼看着门开——「夜班门全开」的演出）；
/// E 只在锁死时给提示。开门后让出交互位（PowerLockedDoor 模式：关阻挡碰撞体、协程跑完动画）。
/// </summary>
public class ShiftGate : MonoBehaviour, IInteractable
{
    [SerializeField] private FactoryShiftController controller;
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private Vector3 openEuler = new Vector3(0f, 95f, 0f);
    [SerializeField] private float openDuration = 1.4f;
    [Tooltip("开门时要关闭的阻挡碰撞体")]
    [SerializeField] private Collider blockingCollider;
    [Tooltip("开闸滚动声（可空）")]
    [SerializeField] private AudioSource roll;
    [SerializeField] private string dayLockedHint = "门纹丝不动——白班时段，深区不开放";
    [SerializeField] private string nightDormantHint = "门锁死了——等夜班开始（挂钟看得见换班）";

    private bool opened;

    public float InteractionRange { get { return 2.5f; } }
    public Transform GetInteractionPoint() { return transform; }

    /// <summary>此门当前是否该开：谜题完成 → 双侧全开；否则仅「夜侧门 + 夜班进行中」。</summary>
    public bool ShouldOpen
    {
        get
        {
            if (FactoryShift.PuzzleSolved) return true;
            return controller != null
                && controller.World == FactoryShift.Phase.Night
                && FactoryShift.Current == FactoryShift.Phase.Night;
        }
    }

    public void Interact(GameObject player)
    {
        if (opened) return;
        bool nightSide = controller != null && controller.World == FactoryShift.Phase.Night;
        HintToast.Show(nightSide ? nightDormantHint : dayLockedHint);
    }

    private void Update()
    {
        if (!opened && ShouldOpen) Open();
    }

    private void Open()
    {
        opened = true;
        if (blockingCollider != null) blockingCollider.enabled = false;
        if (roll != null) roll.Play();
        StartCoroutine(OpenRoutine());
        enabled = false; // 开启后让出交互位（协程继续跑完动画）
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

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
