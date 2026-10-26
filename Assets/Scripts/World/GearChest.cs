using UnityEngine;

/// <summary>
/// 探索道具·工具箱：两段式交互 —— 先按 E 开盖，箱内的道具才变得可以拾取。
///
/// 语义（与收容台 SpecimenDeposit 同构，但用途相反：那边往里存异常物，这边往外取装备）：
///   · 关盖态：箱内道具**看得见**但拿不到（拾取脚本被 disable，已从交互中枢注销）；
///   · 按 E  → 卡扣解锁 + 掀盖动画，箱内道具变为可拾取（拾取脚本 enable，重新注册）；
///   · 开盖态再按 E → 合盖，道具重新变为不可拾取。
///
/// 为什么道具常驻可见：它们是"出发前在装备箱里领"的东西，摆在箱里一眼能看清有哪几件，
/// 比藏起来再按 E 才凭空出现更符合直觉；而"打开才能拿"保留了开箱这个动作的仪式感。
///
/// 箱内道具用现成的拾取物脚本（FlashlightPickup / EmfDetectorPickup / OrderTerminalPickup），
/// 本组件只负责切换它们的 enabled —— 拾取物自己的 OnEnable/OnDisable 会注册/注销交互中枢，
/// 所以不需要额外的"是否可交互"状态位。取走的道具会自己 Destroy，这里每次都重新扫，不缓存失效引用。
/// </summary>
public class GearChest : MonoBehaviour, IInteractable, IInteractionPriority
{
    [Tooltip("交互范围（米）。工具箱是两段式操作，要求玩家真的站到箱前。")]
    [SerializeField] private float interactionRange = 1.6f;

    [Header("开关动画（角度 = 本地欧拉增量）")]
    [Tooltip("盖子掀开角度（绕本地 X）。负值 = 朝后方掀开；0 = 不做动画")]
    [SerializeField] private float lidOpenAngle = -105f;
    [Tooltip("卡扣解锁外翻角度（绕本地 X，两扣同向）")]
    [SerializeField] private float claspOpenAngle = 60f;
    [SerializeField] private float lidMoveTime = 0.4f;
    [SerializeField] private float claspMoveTime = 0.18f;
    [SerializeField] private float openHoldTime = 0.2f;

    [Header("可动部件（可留空：Awake 按名字认领）")]
    [Tooltip("盖子子物体的路径（相对箱体根，可用 A/B/C 形式跨层级）。\n留空或找不到时退回 caseup* 名字前缀匹配。")]
    [SerializeField] private string lidChildPath = "caseup_low";
    [Tooltip("卡扣名字前缀（两扣同向，可留空表示没有卡扣）")]
    [SerializeField] private string claspNamePrefix = "caseop";
    [SerializeField] private Transform lid;
    [SerializeField] private Transform[] clasps;

    [Header("箱内道具（常驻可见；只有开盖后才能拾取）")]
    [Tooltip("开盖时启用其拾取脚本的子物体。留空则自动收集直接子物体里挂了拾取物脚本的。\n注意：只切换拾取脚本 enabled，不隐藏物体——道具要一直看得见。")]
    [SerializeField] private Transform[] contents;

    [Header("交互锚点（可留空：Awake 在箱体下部自动生成）")]
    [SerializeField] private Transform interactionAnchor;
    [Tooltip("自动锚点相对箱体根的下沉量（本地单位）")]
    [SerializeField] private float anchorDrop = 0.4f;

    public float InteractionRange { get { return interactionRange; } }

    /// <summary>
    /// 交互优先级，按「开启 &gt; 拾取物品 &gt; 关闭」排序：
    /// <list type="bullet">
    /// <item>关盖态 → 1：此时箱内道具不可拾取，箱子的「打开工具箱」是唯一该提示的动作，最高优先。</item>
    /// <item>开盖且还有道具 → -1：让位给箱内道具（0），否则箱体 Collider 包住道具时会抢走提示。</item>
    /// <item>开盖但道具取空 → 0：这时只剩「合上工具箱」，正常参与距离比较即可。</item>
    /// </list>
    /// </summary>
    public int InteractionPriority
    {
        get
        {
            if (!lidOpen) return 1;
            return RemainingCount() > 0 ? -1 : 0;
        }
    }

    public Transform GetInteractionPoint()
    {
        // 与收容台同理：锚点下沉到箱体下部，垂直偏置才不会吃掉水平触及半径。
        if (interactionAnchor != null) return interactionAnchor;
        return transform;
    }

    private bool busy;              // 动画进行中（防重入）
    private bool lidOpen;           // 是否处于「开盖可取物」态
    private InteractionLabel label;
    private Quaternion lidClosedRot, lidOpenRot;
    private Quaternion[] claspClosedRot, claspOpenRot;

    private void Awake()
    {
        // 强制清空后重新认领，避免旧域/序列化残留干扰（与 SpecimenDeposit 一致）
        lid = null;
        clasps = null;
        label = GetComponent<InteractionLabel>();
        AcquireParts();
        CollectContents();
        EnsureAnchor();
        ApplyLid(0f);           // 回到关盖姿态（场景里可能残留上一次的开盖姿态）
        SetContentsInteractable(false);   // 常驻可见，但关盖时拿不到
        UpdateLabel();
    }

    private void EnsureAnchor()
    {
        if (interactionAnchor != null) return;
        var found = transform.Find("interaction_anchor");
        if (found != null) { interactionAnchor = found; return; }
        var go = new GameObject("interaction_anchor");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -anchorDrop, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        interactionAnchor = go.transform;
    }

    /// <summary>收集箱内道具：优先用手动接线的，否则自动找直接子物体里的拾取物。</summary>
    private void CollectContents()
    {
        if (contents != null && contents.Length > 0) return;
        var list = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c == null || c == interactionAnchor) continue;
            bool isGear = c.GetComponent<FlashlightPickup>() != null
                       || c.GetComponent<EmfDetectorPickup>() != null
                       || c.GetComponent<OrderTerminalPickup>() != null
                       || c.GetComponent<MultimeterPickup>() != null;
            if (isGear) list.Add(c);
        }
        contents = list.ToArray();
    }

    /// <summary>箱内还剩几件没被取走（取走的会自己 Destroy）。</summary>
    private int RemainingCount()
    {
        if (contents == null) return 0;
        int n = 0;
        for (int i = 0; i < contents.Length; i++)
            if (contents[i] != null) n++;
        return n;
    }

    /// <summary>
    /// 切换箱内道具「能否被拾取」：只开关拾取脚本的 enabled，**不动 GameObject 的 active**
    /// （道具要一直看得见）。拾取物脚本的 OnEnable/OnDisable 会自行注册/注销交互中枢。
    /// </summary>
    private void SetContentsInteractable(bool interactable)
    {
        if (contents == null) return;
        for (int i = 0; i < contents.Length; i++)
        {
            var c = contents[i];
            if (c == null) continue;                 // 已被取走
            SetPickupsEnabled(c.gameObject, interactable);
        }
    }

    /// <summary>把物体上所有拾取物脚本的 enabled 设成给定值（已取走的物体上脚本已不存在，跳过）。</summary>
    private static void SetPickupsEnabled(GameObject go, bool enabled)
    {
        bool any = false;
        var fl = go.GetComponent<FlashlightPickup>();
        if (fl != null) { fl.enabled = enabled; any = true; }
        var em = go.GetComponent<EmfDetectorPickup>();
        if (em != null) { em.enabled = enabled; any = true; }
        var ot = go.GetComponent<OrderTerminalPickup>();
        if (ot != null) { ot.enabled = enabled; any = true; }
        var mm = go.GetComponent<MultimeterPickup>();
        if (mm != null) { mm.enabled = enabled; any = true; }
        // 道具可能有子物体挂拾取脚本（预制件结构不定），一并处理
        if (!any)
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n == "FlashlightPickup" || n == "EmfDetectorPickup"
                    || n == "OrderTerminalPickup" || n == "MultimeterPickup")
                    mb.enabled = enabled;
            }
    }

    /// <summary>按当前开合状态刷新交互提示。</summary>
    private void UpdateLabel()
    {
        if (label == null) return;
        if (lidOpen)
            label.Text = RemainingCount() > 0 ? "收起工具箱" : "合上工具箱";
        else
            label.Text = RemainingCount() > 0 ? "打开工具箱" : "工具箱（已空）";
    }

    public void Interact(GameObject player)
    {
        if (busy) { HintToast.Show("工具箱正在开/合盖"); return; }

        if (!lidOpen)
        {
            int n = RemainingCount();
            HintToast.Show(n > 0 ? "工具箱打开（" + n + " 件装备待取）" : "工具箱已打开（装备都取走了）");
            StartCoroutine(OpenRoutine());
        }
        else
        {
            HintToast.Show("工具箱合上");
            StartCoroutine(CloseRoutine());
        }
    }

    private System.Collections.IEnumerator OpenRoutine()
    {
        busy = true;
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 0f, 1f, claspMoveTime);
        yield return RotateRoutine(ApplyLid, 0f, 1f, lidMoveTime);
        yield return new WaitForSeconds(openHoldTime);
        lidOpen = true;
        SetContentsInteractable(true);   // 开盖后道具才可拾取（但一直看得见）
        UpdateLabel();
        busy = false;
    }

    private System.Collections.IEnumerator CloseRoutine()
    {
        busy = true;
        SetContentsInteractable(false);
        yield return RotateRoutine(ApplyLid, 1f, 0f, lidMoveTime);
        if (HasClasps)
            yield return RotateRoutine(ApplyClasps, 1f, 0f, claspMoveTime);
        lidOpen = false;
        UpdateLabel();
        busy = false;
    }

    private System.Collections.IEnumerator RotateRoutine(System.Action<float> apply, float from, float to, float duration)
    {
        if (duration <= 0f) { apply(to); yield break; }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float eased = k * k * (3f - 2f * k);
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

    private void AcquireParts()
    {
        // 1) 按路径精确取（支持跨层级）
        if (lid == null && !string.IsNullOrEmpty(lidChildPath))
            lid = transform.Find(lidChildPath);

        // 2) 兜底：caseup* 名字前缀（不同模型命名不同）
        if (lid == null)
        {
            foreach (var c in GetComponentsInChildren<Transform>(true))
                if (c != transform && c.name.StartsWith("caseup") && !c.name.ToLower().Contains("deco"))
                    { lid = c; break; }
        }

        // 卡扣：先看直接子物体，再兜底全层级
        if (!HasClasps && !string.IsNullOrEmpty(claspNamePrefix))
        {
            var found = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c.name.StartsWith(claspNamePrefix)) found.Add(c);
            }
            if (found.Count == 0)
                foreach (var c in GetComponentsInChildren<Transform>(true))
                    if (c != transform && c.name.StartsWith(claspNamePrefix)) found.Add(c);
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
        // 禁用会掐断协程，busy 会永卡 true——这里自愈
        StopAllCoroutines();
        busy = false;
    }
}
