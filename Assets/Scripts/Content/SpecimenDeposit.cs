using UnityEngine;

/// <summary>
/// 实验室收容台（Prop_Chest）：**常态开盖**的两段式交互。
///
/// 语义（2026-10-27 定稿，替代早先「先开盖再收容」）：
///   · 常态 = 开盖。玩家带着异常物走过来，箱子已经敞着，不需要先「打开」；
///   · 第一段：按 E → 随身异常物全部**放进箱内**（可见地摆在箱里），此时尚未入账；
///   · 第二段：按 E → 演一遍**合盖 → 开盖**，箱内清空，随即把暂存转成数据值
///     （已收容计数 + 图鉴档案 + 电费账单）。
///
/// 为什么要有「放进箱子」这一步而不是直接入账：玩家能看见自己带回的东西躺在箱子里，
/// 合盖那一下是「封存」的动作语义，开盖后箱子空了、数据涨了，反馈才闭环。
/// 暂存期间这批货既不算随身也不算已收容——瘫倒散失碰不到它们（箱子在实验室里）。
///
/// 可动部件从模型子物体按路径认领（Prop_Chest = Chest_Armature/Root/Chest_Bottom/Chest_Top），
/// 优先用模型自带的开合动画片段；找不到就当普通交互物，静默跳过、不报错。
/// 动画进行中忽略交互（busy 防重入）。
/// </summary>
public class SpecimenDeposit : MonoBehaviour, IInteractable
{
    [Tooltip("交互范围（米）。收容台是两段式操作（先开盖再收容），要求玩家真的站到箱前；\n调大会在箱子还离得远时就亮提示，反而让人以为站在哪儿都能收容。")]
    [SerializeField] private float interactionRange = 1.6f;

    [Header("骨骼动画（模型自带开合动画时填这里，优先于下面的角度插值）")]
    [Tooltip("开盖动画片段。填了就按片段采样驱动骨骼，忽略 lidOpenAngle。\nProp_Chest 用 FBX 内嵌的 Chest_Armature|Chest_Open。")]
    [SerializeField] private AnimationClip lidOpenClip;
    [Tooltip("合盖动画片段。填了就按片段采样驱动骨骼，忽略 lidOpenAngle。\nProp_Chest 用 FBX 内嵌的 Chest_Armature|Chest_Close。")]
    [SerializeField] private AnimationClip lidCloseClip;

    [Header("开关动画（角度 = 本地欧拉增量；仅在没有动画片段时生效）")]
    [Tooltip("盖子掀开角度（绕本地 X）。负值 = 朝后方掀开；0 = 不做动画")]
    [SerializeField] private float lidOpenAngle = -105f;
    [Tooltip("卡扣解锁外翻角度（绕本地 X，两扣同向）")]
    [SerializeField] private float claspOpenAngle = 60f;
    [SerializeField] private float lidMoveTime = 0.4f;    // 掀开/合上耗时（秒）
    [SerializeField] private float claspMoveTime = 0.18f; // 卡扣解锁/复位耗时（秒）
    [SerializeField] private float openHoldTime = 0.2f;   // 开盖到位后稍停（给反馈留时间）
    [Tooltip("结算演出：合盖「封存」后到再开盖之间的停顿（秒）。留一点时间让玩家意识到东西被封进去了。")]
    [SerializeField] private float settleHoldTime = 0.35f;

    [Header("可动部件（可留空：Awake 按名字认领）")]
    [Tooltip("盖子子物体的路径（相对箱体根，可用 A/B/C 形式跨层级）。\n留空或找不到时退回 caseup* 名字前缀匹配。\n换成 Prop_Chest 这类骨骼箱体时填 Chest_Armature/Root/Chest_Bottom/Chest_Top。")]
    [SerializeField] private string lidChildPath = "caseup_low";
    [Tooltip("卡扣名字前缀（两扣同向）。留空 = 该模型没有卡扣，跳过解锁动作。")]
    [SerializeField] private string claspNamePrefix = "caseop";
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
        // 1.07 米的垂直偏置会把触及半径压掉约 0.24 米，玩家明明站在箱子前却选不中。
        // 把锚点下沉到箱体下部，垂直偏置降到约 0.5 米，水平触及半径才接近设定值。
        if (interactionAnchor != null) return interactionAnchor;
        return transform;
    }

    private bool busy;              // 动画进行中（防重入 / 防状态错乱）
    private bool lidOpen = true;    // 常态开盖：箱子一直敞着，玩家走过来就能放东西
    private InteractionLabel label; // 交互提示(可运行时改,让两段式有明确文案)
    private Quaternion lidClosedRot, lidOpenRot;
    private Quaternion[] claspClosedRot, claspOpenRot;
    private bool HasLidClip { get { return lidOpenClip != null && lidCloseClip != null; } }

    // 箱内可视化的暂存物（每件异常物一个小方块，摆在箱腔里）
    private readonly System.Collections.Generic.List<GameObject> stagedVisuals = new System.Collections.Generic.List<GameObject>();
    [Tooltip("箱内暂存物的摆放点（本地坐标，相对箱体根）。按顺序取用，不够用时循环复用并略微错位。\ny 要落在箱腔内部（箱口约 0.42，太矮会穿底、太高会浮在箱口上方）。")]
    [SerializeField] private Vector3[] stagedSlots = new Vector3[]
    {
        new Vector3(-0.32f, 0.26f, -0.10f),
        new Vector3(0.00f, 0.26f, -0.10f),
        new Vector3(0.32f, 0.26f, -0.10f),
        new Vector3(-0.16f, 0.26f, 0.14f),
        new Vector3(0.16f, 0.26f, 0.14f),
    };
    [Tooltip("箱内暂存小方块的边长（米）。")]
    [SerializeField] private float stagedItemSize = 0.16f;

    private void Awake()
    {
        lid = null;  // 强制清空,避免旧域/序列化残留干扰认领
        clasps = null;
        label = GetComponent<InteractionLabel>();
        AcquireParts();
        // 常态是「开盖」，所以用开盖片段末帧把姿态校到打开位。
        // 场景里存的 rest 姿态不可信（Prop_Chest 的 rest 恰好是开着的，但换模型就未必）。
        if (HasLidClip && lidOpenClip.length > 0f) lidOpenClip.SampleAnimation(gameObject, lidOpenClip.length);
        lidOpen = true;
        EnsureAnchor();
        UpdateLabel();
    }

    private void OnEnable()
    {
        InteractionRegistry.Register(this);
        Inventory.OnSpecimenChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        InteractionRegistry.Unregister(this);
        Inventory.OnSpecimenChanged -= OnInventoryChanged;
        // 禁用会掐断协程，busy 会永卡 true——这里自愈，下次 E 还能演
        StopAllCoroutines();
        busy = false;
    }

    /// <summary>随身/箱内件数变了就刷新提示文案（「放入异常物」↔「结算异常物」）。</summary>
    private void OnInventoryChanged(int carrying, int stored) { UpdateLabel(); }

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

    /// <summary>
    /// 按当前状态刷新交互提示。
    /// 箱内空且身上有货 → 「放入异常物」；箱内有货 → 「结算异常物」；
    /// 箱内空且身上也没货 → 「收容台（空）」。
    /// </summary>
    private void UpdateLabel()
    {
        if (label == null) return;
        var inv = Inventory.Instance;
        int staged = inv != null ? inv.StagedCount : 0;
        int carrying = inv != null ? inv.SpecimenCount : 0;
        if (staged > 0) label.Text = "结算异常物";
        else if (carrying > 0) label.Text = "放入异常物";
        else label.Text = "收容台（空）";
    }

    public void Interact(GameObject player)
    {
        if (busy) { HintToast.Show("收容台正在处理"); return; }   // 动画中:给反馈而非静默忽略

        var inv = Inventory.Instance;
        if (inv == null) return;

        if (inv.StagedCount > 0)
        {
            // 第二段：箱里有货 → 合盖（封存）→ 开盖（揭晓）→ 结算成数据值。
            StartCoroutine(SettleRoutine());
            return;
        }

        // 第一段：箱内空 → 把随身异常物放进箱子。
        if (inv.SpecimenCount <= 0)
        {
            HintToast.Show("箱内是空的——先去层域回收异常物");
            return;
        }
        int n = inv.StageAllSpecimens();
        SpawnStagedVisuals(n);
        HintToast.Show("放入 " + n + " 件异常物——再按 E 结算");
    }

    // ─────────────────────────────────────────────────────────────
    // 结算演出：合盖（封存）→ 停顿 → 开盖（揭晓）→ 清空箱内 → 转数据值。
    // 卡扣扣在盖子前沿上,先掀盖会被扯住——开合顺序不能反。
    // ─────────────────────────────────────────────────────────────
    private System.Collections.IEnumerator SettleRoutine()
    {
        busy = true;

        // 合盖（封存）
        yield return CloseLidStep();
        UpdateLabel();

        yield return new WaitForSeconds(settleHoldTime);   // 「封存」停顿

        // 开盖（揭晓）
        yield return OpenLidStep();

        // 揭晓：箱内清空 + 结算入账
        ClearStagedVisuals();
        int n = Inventory.Instance != null ? Inventory.Instance.CommitStaged() : 0;
        if (n > 0)
            HintToast.Show("已收容 " + n + " 件异常物（累计 " + Inventory.Instance.StoredSpecimenCount + "）");
        UpdateLabel();
        busy = false;
    }

    // ── 箱内可视化：每件暂存物摆一个小方块，结算后清掉 ──────────────
    private void SpawnStagedVisuals(int count)
    {
        ClearStagedVisuals();
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "staged_specimen_" + (i + 1);
            // 交互物不该被准心选中，也不该挡玩家
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = SlotOf(i);
            go.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
            go.transform.localScale = Vector3.one * (stagedItemSize / Mathf.Max(0.0001f, transform.lossyScale.x));
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // 用自发光材质，暗环境里也能看见箱内躺着东西
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.45f, 1f, 0.78f, 1f);
                mr.material = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            stagedVisuals.Add(go);
        }
    }

    /// <summary>取第 i 件暂存物的箱内摆位；超出槽位数后按行错开，避免完全重叠。</summary>
    private Vector3 SlotOf(int i)
    {
        if (stagedSlots == null || stagedSlots.Length == 0) return new Vector3(0f, 0.5f, 0f);
        int k = i % stagedSlots.Length;
        int row = i / stagedSlots.Length;
        Vector3 p = stagedSlots[k];
        p.y += row * 0.10f;
        return p;
    }

    private void ClearStagedVisuals()
    {
        for (int i = 0; i < stagedVisuals.Count; i++)
            if (stagedVisuals[i] != null) Destroy(stagedVisuals[i]);
        stagedVisuals.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    // 底层动画：开盖 = 卡扣先解锁外翻 → 掀盖。
    //          合盖 = 先合盖 → 卡扣复位。
    // ─────────────────────────────────────────────────────────────
    /// <summary>开盖一步（卡扣解锁 → 掀盖）。供 SettleRoutine 复用。</summary>
    private System.Collections.IEnumerator OpenLidStep()
    {
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 0f, 1f, claspMoveTime); // 解锁外翻
        if (HasLidClip)
            yield return PlayLidClip(lidOpenClip);                          // 采样模型自带开盖动画
        else
            yield return RotateRoutine(ApplyLid, 0f, 1f, lidMoveTime);      // 掀盖（角度插值兜底）
        yield return new WaitForSeconds(openHoldTime);
        lidOpen = true;
    }

    /// <summary>合盖一步（合盖 → 卡扣复位）。供 SettleRoutine 复用。</summary>
    private System.Collections.IEnumerator CloseLidStep()
    {
        if (HasLidClip) yield return PlayLidClip(lidCloseClip);
        else            yield return RotateRoutine(ApplyLid, 1f, 0f, lidMoveTime);
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 1f, 0f, claspMoveTime); // 复位
        lidOpen = false;
    }

    /// <summary>
    /// 按时间轴采样模型自带的骨骼动画片段。
    ///
    /// 为什么不用 Animator/Animation 组件：这两者会接管整个层级的状态机，
    /// 而这个箱体上还挂着 MeshCollider 和交互逻辑，加 Animator 后每帧
    /// 都要维护参数与过渡，且和其它交互物的触发时序更难对齐。片段采样
    /// 是纯函数式的：给时刻、写骨骼、不留下任何组件状态，动画结束即静止。
    /// </summary>
    private System.Collections.IEnumerator PlayLidClip(AnimationClip clip)
    {
        if (clip == null || lid == null) yield break;
        float dur = clip.length;
        if (dur <= 0f) { clip.SampleAnimation(gameObject, 0f); yield break; }

        float elapsed = 0f;
        while (elapsed < dur)
        {
            // 用 unscaled 时间：收容台动画不该被暂停/子弹时间影响。
            elapsed += Time.deltaTime;
            clip.SampleAnimation(gameObject, Mathf.Clamp(elapsed, 0f, dur));
            yield return null;
        }
        clip.SampleAnimation(gameObject, dur);   // 精确落到末帧，避免浮点累积误差
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
        // 路径可跨层级（Prop_Chest 的盖子是 Chest_Armature/Root/Chest_Bottom/Chest_Top），
        // 所以优先用 lidChildPath；留空或找不到再退回 caseup* 前缀匹配。
        if (lid == null && !string.IsNullOrEmpty(lidChildPath))
            lid = transform.Find(lidChildPath);

        if (lid == null)    // 兜底: 找不到精确名时遍历一次 (不同模型名/变体)
        {
            foreach (var c in GetComponentsInChildren<Transform>(true))
                if (c != transform && c.name.StartsWith("caseup") && !c.name.ToLower().Contains("deco"))
                    { lid = c; break; }
        }
        if (!HasClasps && !string.IsNullOrEmpty(claspNamePrefix))
        {
            var found = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c.name.StartsWith(claspNamePrefix)) found.Add(c);
            }
            if (found.Count == 0)   // 兜底: 遍历,用于非标准层级
            {
                foreach (var c in GetComponentsInChildren<Transform>(true))
                    if (c != transform && c.name.StartsWith(claspNamePrefix)) found.Add(c);
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
}