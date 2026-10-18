using UnityEngine;

/// <summary>
/// 实验室收容台：靠近 E，把随身携带的异常物全部存入收容（携带 → 已收容）。
/// 对应提案"带回实验室才算收益"的结算口径。
///
/// 开关动画：E 入账时演出「卡扣解锁外翻 → 开盖 → 停留 → 合盖 → 卡扣复位」。
/// 可动部件从 case 模型子物体按名字认领（caseup_low = 盖子，caseop_low* = 卡扣），
/// 也可在 Inspector 手动接线；找不到就当普通交互物，静默跳过、不报错。
/// 结算是即时的（Interact 一进来就入账），动画只是反馈，不参与逻辑。
/// </summary>
public class SpecimenDeposit : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactionRange = 2.5f;

    [Header("开关动画（角度 = 本地欧拉增量）")]
    [Tooltip("盖子掀开角度（绕本地 X）。负值 = 朝后方掀开；0 = 不做动画")]
    [SerializeField] private float lidOpenAngle = -105f;
    [Tooltip("卡扣解锁外翻角度（绕本地 X，两扣同向）")]
    [SerializeField] private float claspOpenAngle = 60f;
    [SerializeField] private float lidMoveTime = 0.4f;    // 掀开/合上耗时（秒）
    [SerializeField] private float claspMoveTime = 0.18f; // 卡扣解锁/复位耗时（秒）
    [SerializeField] private float holdOpenTime = 1.4f;   // 全开停留（给入账反馈留时间）

    [Header("可动部件（可留空：Awake 按名字认领）")]
    [SerializeField] private Transform lid;
    [SerializeField] private Transform[] clasps;

    public float InteractionRange { get { return interactionRange; } }
    public Transform GetInteractionPoint() { return transform; }

    private bool busy;
    private Quaternion lidClosedRot, lidOpenRot;
    private Quaternion[] claspClosedRot, claspOpenRot;

    private void Awake()
    {
        AcquireParts();
    }

    public void Interact(GameObject player)
    {
        var inv = Inventory.Instance;
        if (inv == null) return;
        int n = inv.StoreAllSpecimens();
        if (n <= 0) { HintToast.Show("没有待收容的异常物"); return; }
        HintToast.Show("已收容 " + n + " 件异常物（累计 " + inv.StoredSpecimenCount + "）");
        PlaySwitchAnimation();
    }

    // ─────────────────────────────────────────────────────────────
    // 开关动画：卡扣先解锁、合盖后卡扣再复位——顺序不能反。
    // 卡扣扣在盖子前沿上，先掀盖会被扯住（低模上表现为穿模）。
    // ─────────────────────────────────────────────────────────────
    private void PlaySwitchAnimation()
    {
        if (busy || lid == null) return;
        busy = true;
        StartCoroutine(SwitchRoutine());
    }

    private System.Collections.IEnumerator SwitchRoutine()
    {
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 0f, 1f, claspMoveTime); // 解锁外翻
        yield return RotateRoutine(ApplyLid, 0f, 1f, lidMoveTime);         // 掀盖
        yield return new WaitForSeconds(holdOpenTime);
        yield return RotateRoutine(ApplyLid, 1f, 0f, lidMoveTime);         // 合盖
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 1f, 0f, claspMoveTime); // 复位
        busy = false;
    }

    /// <summary>把 apply 从 from 匀到 to（0=闭合 / 1=打开），smoothstep 缓动。</summary>
    private System.Collections.IEnumerator RotateRoutine(System.Action<float> apply, float from, float to, float duration)
    {
        if (duration <= 0f) { apply(to); yield break; }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float eased = k * k * (3f - 2f * k); // smoothstep：有加减速，不生硬
            apply(Mathf.Lerp(from, to, eased));
            yield return null;
        }
        apply(to);
    }

    private void ApplyLid(float t)
    {
        if (lid == null) return;
        lid.localRotation = Quaternion.Slerp(lidClosedRot, lidOpenRot, t);
    }

    private void ApplyClasps(float t)
    {
        if (clasps == null) return;
        for (int i = 0; i < clasps.Length; i++)
            if (clasps[i] != null && claspClosedRot != null && i < claspClosedRot.Length)
                clasps[i].localRotation = Quaternion.Slerp(claspClosedRot[i], claspOpenRot[i], t);
    }

    private bool HasClasps { get { return clasps != null && clasps.Length > 0; } }

    /// <summary>认领可动部件并缓存开合目标旋转。部件缺任何一件，对应动作静默跳过。</summary>
    private void AcquireParts()
    {
        if (lid == null || !HasClasps)
        {
            foreach (var c in GetComponentsInChildren<Transform>(true))
            {
                if (lid == null && c.name.StartsWith("caseup")) lid = c;
            }
            if (!HasClasps)
            {
                var found = new System.Collections.Generic.List<Transform>();
                foreach (var c in GetComponentsInChildren<Transform>(true))
                    if (c != transform && c.name.StartsWith("caseop")) found.Add(c);
                clasps = found.ToArray();
            }
        }
        if (lid != null)
        {
            lidClosedRot = lid.localRotation;
            lidOpenRot = lidClosedRot * Quaternion.Euler(lidOpenAngle, 0f, 0f);
        }
        if (HasClasps)
        {
            claspClosedRot = new Quaternion[clasps.Length];
            claspOpenRot = new Quaternion[clasps.Length];
            for (int i = 0; i < clasps.Length; i++)
            {
                if (clasps[i] == null) continue;
                claspClosedRot[i] = clasps[i].localRotation;
                claspOpenRot[i] = claspClosedRot[i] * Quaternion.Euler(claspOpenAngle, 0f, 0f);
            }
        }
    }

    private void OnEnable() { InteractionRegistry.Register(this); }

    private void OnDisable()
    {
        InteractionRegistry.Unregister(this);
        // 禁用会掐断协程，busy 会永卡 true——这里自愈，下次 E 还能演
        StopAllCoroutines();
        busy = false;
    }
}
