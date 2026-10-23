using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 交互中枢：挂 player。遍历可交互物登记表，选最近的为当前目标；
/// 按 E 只通知它一个——E 键单点分发，任何新交互物实现接口并自注册即可接入，无需改本类。
/// 不做物理扫描：密集几何场景里 OverlapSphere 缓冲区会被静态碰撞体填满，交互物反而被挤出结果。
///
/// 准心判定：只有准心落在交互物上才提示、才可交互（requireAim）。
/// 用"屏幕中心射线是否打进交互物包围盒"为主判定，角度容差兜底——
/// 不能只用物理射线命中：拾取物（探测器/手电筒）没有 Collider，射线会直接漏过它们。
/// </summary>
public class InteractionRunner : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float scanInterval = 0.2f; // 扫描节流（不必每帧）

    [Header("准心交互")]
    [Tooltip("只有准心落在交互物上才显示提示、才响应 E。关掉则退回旧的纯距离判定。")]
    [SerializeField] private bool requireAim = true;
    [Tooltip("全局交互距离上限（米）。取它与各交互物自身范围的较小值——\n场景里已序列化的 interactionRange 不必逐个改。")]
    [SerializeField] private float maxInteractionRange = 3.2f;
    [Tooltip("准心容差角（度）：包围盒内未命中时的兜底，吸收门的旋转 AABB 误差")]
    [SerializeField] private float aimTolerance = 7f;
    [Tooltip("已选中目标的额外容差倍数：提示一旦出现就黏住，避免准心微颤导致提示闪烁")]
    [SerializeField] private float stickyFactor = 2.2f;
    [Tooltip("连续多少次扫描都瞄不准才释放目标。必须 ≥2：单次抖动不该让提示消失")]
    [SerializeField] private int releaseAfterMisses = 3;
    [Tooltip("距离滞回（米）：已选中的目标允许多出这么远才释放。\n玩家在有效距离边界来回微动时，没有它会疯狂闪")]
    [SerializeField] private float rangeHysteresis = 0.6f;

    [Tooltip("打印每次按键选中的目标，排查交互失灵时开。")]
    [SerializeField] private bool verbose;

    private readonly List<IInteractable> nearby = new List<IInteractable>();
    private float nextScan;
    private IInteractable current;
    private string currentLabel = "交互";
    private int missStreak;   // 连续未命中的扫描次数（抑制提示闪烁）

    /// <summary>当前选中的交互物（供提示 UI 读取；可能为 null）。</summary>
    public IInteractable Current { get { return current; } }

    private void OnGUI()
    {
        if (current == null) return;

        // CRT 终端提示片：等宽字形 + 磷光绿 + 取景角（亮场景里靠暗底条保证可读）
        var style = CrtTheme.Style(15, CrtTheme.Ink, false, false, TextAnchor.MiddleCenter);
        var content = new GUIContent("[E] " + currentLabel);
        float w = style.CalcSize(content).x + CrtTheme.Px(30f);
        float h = CrtTheme.Px(26f);
        var box = new Rect(Mathf.Round((Screen.width - w) * 0.5f), Mathf.Round(Screen.height * 0.62f), w, h);
        CrtTheme.Fill(box, new Color(0.01f, 0.03f, 0.02f, 0.74f));
        CrtTheme.Border(box, new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.32f));
        CrtTheme.Corners(box, new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.70f), 7f, 2f);
        GUI.Label(box, content, style);
    }

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + scanInterval;
            ScanNearest();
        }
        if (Input.GetKeyDown(interactKey))
        {
            if (verbose)
                Debug.Log("[Interact] 选中=" + (current != null ? ((Component)current).gameObject.name : "无目标")
                          + " 范围内=" + nearby.Count);
            if (current != null) current.Interact(gameObject);
        }
    }

    private void ScanNearest()
    {
        nearby.Clear();
        IInteractable best = null;
        float bestDist = float.MaxValue;
        var pos = transform.position;

        // 准心基准：玩家相机。未就绪（世界 Additive 加载的空窗帧）时跳过准心判定，
        // 只按距离走，避免交互整体失灵。
        Transform cam = PlayerRef.Camera != null ? PlayerRef.Camera.transform : null;

        var items = InteractionRegistry.Items;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            // 已销毁的残留项顺手清出登记表
            if (!IsAlive(it))
            {
                InteractionRegistry.RemoveAt(i);
                continue;
            }

            var point = it.GetInteractionPoint();
            if (point == null) continue;

            nearby.Add(it);

            float d = Vector3.Distance(pos, point.position);
            // 有效距离 = 自身范围与全局上限的较小值（上限一处可调，不必改场景里的序列化值）。
            // 已选中的目标再放宽 rangeHysteresis：玩家站在边界上微动时，没有它就会反复闪。
            bool isCurrent0 = ReferenceEquals(it, current);
            float allow = Mathf.Min(it.InteractionRange, maxInteractionRange) + (isCurrent0 ? rangeHysteresis : 0f);
            if (d > allow) continue;

            // 准心必须落在它身上。已选中的目标放宽容差（滞回），否则准心微颤就让提示闪烁。
            if (requireAim && cam != null)
            {
                bool isCurrent = ReferenceEquals(it, current);
                float tol = isCurrent ? aimTolerance * stickyFactor : aimTolerance;
                if (!IsAimedAt(it, point, cam, tol)) continue;
            }

            if (d < bestDist) { bestDist = d; best = it; }
        }

        // 滞回：一次没瞄准不算丢失，必须连续 miss 满 releaseAfterMisses 次才放手。
        // 用"计数"而不是"时长"——时长容易被扫描间隔吞掉（保持期小于扫描间隔时永远不生效）。
        if (best != null)
        {
            missStreak = 0;
        }
        else if (current != null && IsAlive(current))
        {
            missStreak++;
            if (missStreak < Mathf.Max(2, releaseAfterMisses)) best = current;
            else { current = null; missStreak = 0; }
        }
        else
        {
            missStreak = 0;
        }

        current = best;
        currentLabel = "交互";
        if (best != null)
        {
            // 挂了 InteractionLabel 的交互物用自定义提示文案
            var c = best as Component;
            if (!ReferenceEquals(c, null))
            {
                var label = c.GetComponent<InteractionLabel>();
                if (label != null) currentLabel = label.Text;
            }
        }
    }

    /// <summary>
    /// 准心是否落在该交互物上：屏幕中心射线打进它的包围盒即算命中；
    /// 未命中时用"包围球张角 + 容差角"兜底（吸收旋转带来的 AABB 膨胀误差）。
    /// 没有包围盒可量（纯 C# 实现 / 无网格）时退回纯角度容差。
    /// tolerance 由调用方给：已选中的目标用放大后的容差做滞回，避免提示闪烁。
    /// </summary>
    private bool IsAimedAt(IInteractable it, Transform point, Transform cam, float tolerance)
    {
        Vector3 camPos = cam.position;
        Vector3 to = point.position - camPos;
        float dist = to.magnitude;
        if (dist < 0.01f) return true;          // 贴在脸上，必中

        Vector3 fwd = cam.forward;
        float angle = Vector3.Angle(fwd, to);

        var comp = it as Component;
        Bounds b;
        if (!ReferenceEquals(comp, null) && TryGetBounds(comp, out b))
        {
            // 主判定：准心射线穿过包围盒
            if (b.IntersectRay(new Ray(camPos, fwd))) return true;
            // 兜底：包围球在屏幕上的张角
            float angularRadius = Mathf.Atan2(b.extents.magnitude, dist) * Mathf.Rad2Deg;
            return angle <= angularRadius + tolerance;
        }

        return angle <= tolerance;
    }

    /// <summary>
    /// 交互物的世界包围盒：自身及子物体的碰撞体 + 渲染器。
    /// 刻意不向父链查找——门的父链是门框/房间，取上去会得到一个巨大的盒子，
    /// 准心随便指哪都算命中。
    /// </summary>
    private static bool TryGetBounds(Component comp, out Bounds bounds)
    {
        bounds = default(Bounds);
        bool any = false;

        var cols = comp.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null || !col.enabled) continue;
            if (!any) { bounds = col.bounds; any = true; }
            else bounds.Encapsulate(col.bounds);
        }

        if (!any)
        {
            var rs = comp.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null || !r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    /// <summary>
    /// 判活。注意不能用 `it as Component == null`：`as` 不走 Unity 重载的 ==，
    /// 已销毁的 MonoBehaviour 转换结果并非 null。必须用 ReferenceEquals 区分
    /// "不是 Unity 对象"和"是 Unity 对象但已销毁"两种情况。
    /// </summary>
    private static bool IsAlive(IInteractable it)
    {
        if (ReferenceEquals(it, null)) return false;
        var c = it as Component;
        if (ReferenceEquals(c, null)) return true; // 纯 C# 实现
        return c != null;                          // Unity 对象：重载的 == 能识别已销毁
    }
}
