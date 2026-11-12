using System.Collections;
using UnityEngine;

/// <summary>
/// 水淹区的保险箱：机械系统被破坏，必须**先有工具、再有燃料**才能打开（缺哪样提示哪样）。
/// 打开后把箱子里的钥匙（keyInside）显示出来，玩家再 E 拾取。
///
/// 设计约束：
/// - 只读 FloodedLoopState，不碰 Inventory / 共用脚本；
/// - 打开是**一次性**的，开过之后按 E 只给「已经开了」的提示；
/// - 箱门（lid）可选：给了就绕它自己的本地 Y 轴转开，没给就只做「箱子抖一下 + 钥匙出现」。
/// </summary>
public class FloodedSafe : MonoBehaviour, IInteractable
{
    [Header("箱子部件")]
    [Tooltip("箱门（可选）。留空则开门时只做震动+钥匙出现")]
    [SerializeField] private Transform lid;

    [Tooltip("箱门打开的角度（绕本地 Y 轴）")]
    [SerializeField] private float lidOpenAngle = 100f;

    [Tooltip("箱门打开用时（秒）")]
    [SerializeField] private float lidOpenTime = 0.9f;

    [Tooltip("箱子里的钥匙对象（默认隐藏，打开后显示）")]
    [SerializeField] private GameObject keyInside;

    [Header("文案")]
    [Tooltip("打开时的提示")]
    [SerializeField] private string openHint = "机械咬合了一声，箱门弹开——里面只有一把钥匙";

    [Tooltip("已开过再按 E 的提示")]
    [SerializeField] private string alreadyHint = "箱子已经空了";

    [Header("其他")]
    [Tooltip("未满足条件时的震动幅度（0 = 不动）")]
    [SerializeField] private float denyShake = 0.02f;

    private bool opening;
    private Vector3 baseLocalPos;

    public float InteractionRange { get { return 2.2f; } }

    public Transform GetInteractionPoint() { return transform; }

    private void Awake()
    {
        baseLocalPos = transform.localPosition;
        if (keyInside != null && !FloodedLoopState.SafeOpened) keyInside.SetActive(false);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }

    public void Interact(GameObject player)
    {
        if (opening) return;

        if (FloodedLoopState.SafeOpened)
        {
            HintToast.Show(alreadyHint);
            return;
        }

        string missing = FloodedLoopState.MissingForSafe();
        if (!string.IsNullOrEmpty(missing))
        {
            HintToast.Show(missing, 3f);
            if (denyShake > 0f) StartCoroutine(Shake());
            return;
        }

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator Shake()
    {
        opening = true;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            transform.localPosition = baseLocalPos + Vector3.right * Mathf.Sin(t * 60f) * denyShake;
            yield return null;
        }
        transform.localPosition = baseLocalPos;
        opening = false;
    }

    private IEnumerator OpenRoutine()
    {
        opening = true;

        // 先「挣扎」一下，再开——比瞬间打开更有机械感
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            transform.localPosition = baseLocalPos + Vector3.right * Mathf.Sin(t * 50f) * denyShake * 1.5f;
            yield return null;
        }
        transform.localPosition = baseLocalPos;

        if (lid != null)
        {
            Quaternion from = lid.localRotation;
            Quaternion to = from * Quaternion.Euler(0f, lidOpenAngle, 0f);
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / Mathf.Max(0.05f, lidOpenTime);
                lid.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            lid.localRotation = to;
        }

        FloodedLoopState.MarkSafeOpened();
        if (keyInside != null) keyInside.SetActive(true);
        HintToast.Show(openHint, 3.5f);

        opening = false;
    }
}
