using UnityEngine;

/// <summary>
/// 实验室收容台：两段式交互 —— 先按 E 触发「打开动画」，再按 E 才真正收容。
///
/// 语义：
///   · 关盖态：按 E → 开盖动画（卡扣解锁外翻 → 掀盖），进入「待收容」态（**空手也能开**）；
///   · 待收容态：按 E → 把随身异常物全部存入（携带 → 已收容）+ 合盖动画（合盖 → 卡扣复位）回关盖态；
///     空手时按 E 只合盖。
///
/// 开盖是「看一眼箱子」的动作，不该被携带量卡住——早先空手按 E 直接 return 不开动画，
/// 玩家会以为箱子坏了。收容结算仍然要求有货，这一步才和「带回实验室才算收益」的口径一致。
///
/// 可动部件从 case 模型子物体按名字认领（caseup_low = 盖子，caseop_low* = 卡扣），
/// 也可在 Inspector 手动接线；找不到就当普通交互物，静默跳过、不报错。
/// 动画由本组件协程驱动；动画进行中忽略交互（busy 防重入）。
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
    [SerializeField] private float openHoldTime = 0.2f;   // 开盖到位后稍停（给反馈留时间）

    [Header("可动部件（可留空：Awake 按名字认领）")]
    [SerializeField] private Transform lid;
    [SerializeField] private Transform[] clasps;

    [Header("交互锚点（可留空：Awake 在箱体下部自动生成）")]
    [Tooltip("留空则自动生成 interaction_anchor 子物体。锚点要贴近玩家脚底高度，\n否则垂直偏置会吃掉水平触及半径（锚点 1.15 米高时 2.5 米范围只剩 2.26 米）。")]
    [SerializeField] private Transform interactionAnchor;
    [Tooltip("自动锚点相对箱体根的下沉量（本地单位；箱体根缩放约 1.4，故 0.4 ≈ 0.56 米）")]
    [SerializeField] private float anchorDrop = 0.4f;

    public float InteractionRange { get { return interactionRange; } }

    public Transform GetInteractionPoint()
    {
        // InteractionRunner 用「玩家脚底 → 锚点」的直线距离判范围。锚点在箱体顶部时，
        // 1.07 米的垂直偏置会把 2.5 米的触及半径压到 2.26 米，玩家明明站在箱子前却选不中。
        // 把锚点下沉到箱体下部，垂直偏置降到约 0.5 米，触及半径恢复到约 2.45 米。
        if (interactionAnchor != null) return interactionAnchor;
        return transform;
    }

    private bool busy;              // 动画进行中（防重入 / 防状态错乱）
    private bool lidOpen;           // 是否处于「开盖待收容」态
    private InteractionLabel label; // 交互提示(可运行时改,让两段式有明确文案)
    private Quaternion lidClosedRot, lidOpenRot;
    private Quaternion[] claspClosedRot, claspOpenRot;

    private void Awake()
    {
        lid = null;  // 强制清空,避免旧域/序列化残留干扰认领
        clasps = null;
        label = GetComponent<InteractionLabel>();
        AcquireParts();
        EnsureAnchor();
        UpdateLabel();
    }

    /// <summary>
    /// 确保交互锚点存在。锚点做成子物体而不是运行时 Vector3，是为了让
    /// InteractionRunner 的 GetInteractionPoint().position 直接可用，
    /// 且能在 Scene 视图里看到、需要时可手动挪。
    /// </summary>
    private void EnsureAnchor()
    {
        if (interactionAnchor != null) return;
        var found = transform.Find("interaction_anchor");
        if (found != null) { interactionAnchor = found; return; }
        var go = new GameObject("interaction_anchor");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -anchorDrop, 0f);   // 负值 = 往下沉
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        interactionAnchor = go.transform;
    }

    /// <summary>按当前开合状态刷新交互提示（关盖=打开收容台 / 开盖=存放异常物）。</summary>
    private void UpdateLabel()
    {
        if (label != null) label.Text = lidOpen ? "存放异常物" : "打开收容台";
    }

    public void Interact(GameObject player)
    {
        if (busy) { HintToast.Show("收容台正在开/合盖"); return; }   // 动画中:给反馈而非静默忽略

        var inv = Inventory.Instance;
        if (inv == null) return;
        int n = inv.SpecimenCount;

        if (!lidOpen)
        {
            // 关盖态：开盖动画本身不受「有没有货」影响——箱子随时能打开看一眼。
            // （早先这里写成 n <= 0 就 return，导致空手按 E 完全没有动画，像坏了一样。）
            if (n > 0) HintToast.Show("收容台打开");
            else HintToast.Show("收容台已打开（没有待收容的异常物）");
            StartCoroutine(OpenRoutine());   // 动画结束置 lidOpen = true
        }
        else
        {
            // 待收容态：有货就收容再合盖；空手只是把盖子合上。
            if (n > 0)
            {
                int stored = inv.StoreAllSpecimens();
                HintToast.Show("已收容 " + stored + " 件异常物（累计 " + inv.StoredSpecimenCount + "）");
            }
            StartCoroutine(CloseRoutine());   // 动画结束置 lidOpen = false
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 动画：开盖 = 卡扣先解锁外翻 → 掀盖 → 置 lidOpen。
    //      合盖 = 先合盖 → 卡扣复位 → 清 lidOpen。
    // 卡扣扣在盖子前沿上,先掀盖会被扯住——顺序不能反。
    // ─────────────────────────────────────────────────────────────
    private System.Collections.IEnumerator OpenRoutine()
    {
        busy = true;
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 0f, 1f, claspMoveTime); // 解锁外翻
        yield return RotateRoutine(ApplyLid, 0f, 1f, lidMoveTime);          // 掀盖
        yield return new WaitForSeconds(openHoldTime);
        lidOpen = true;
        UpdateLabel();   // 文案「打开收容台」→「存放异常物」
        busy = false;
    }

    private System.Collections.IEnumerator CloseRoutine()
    {
        busy = true;
        yield return RotateRoutine(ApplyLid, 1f, 0f, lidMoveTime);         // 合盖
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 1f, 0f, claspMoveTime); // 复位
        lidOpen = false;
        UpdateLabel();   // 文案「存放异常物」→「打开收容台」
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
        // 精确匹配子物体: 并发会话/域重载下 GetComponentsInChildren 的遍历顺序不稳定,
        // transform.Find 按路径名查更快且不依赖顺序。
        if (lid == null) lid = transform.Find("caseup_low");
        if (lid == null)    // 兜底: 找不到精确名时遍历一次 (不同模型名/变体)
        {
            foreach (var c in GetComponentsInChildren<Transform>(true))
                if (c != transform && c.name.StartsWith("caseup") && !c.name.ToLower().Contains("deco"))
                    { lid = c; break; }
        }
        if (!HasClasps)
        {
            var found = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c.name.StartsWith("caseop")) found.Add(c);
            }
            if (found.Count == 0)   // 兜底: 遍历,用于非标准层级
            {
                foreach (var c in GetComponentsInChildren<Transform>(true))
                    if (c != transform && c.name.StartsWith("caseop")) found.Add(c);
            }
            clasps = found.ToArray();
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