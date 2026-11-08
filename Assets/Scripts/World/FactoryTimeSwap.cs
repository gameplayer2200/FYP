using System.Collections;
using UnityEngine;

/// <summary>
/// 工厂时空强制切换（World）：换班那一刻把玩家搬到另一层，相对位置保持不变。
///
/// 为什么这版能做：工厂昼夜原本是两个独立场景（x 相差 14797），强制切换等于一次跨场景
/// 传送，要等 WorldStreamer 异步激活（整帧级重活，见 ADR-0005）。现在两层合并进同一个
/// Factory 场景、上下叠放，传送退化成**改一次坐标**——零加载成本、无卡顿，这是这个玩法
/// 成立的前提。
///
/// 与「泰坦陨落 2 · 因果」的差别（设计上的自觉）：那边的切换是玩家主动按的，是战术工具；
/// 这边是**时钟强制**的，玩家没有选择权。所以这里不做「闪一下换世界」，而是：
///   ① 换班前 WarningSeconds 秒挂钟报警（OnPhaseWarning）；
///   ② 到达落点先做净空检测，被几何挤住就沿朝向轻推，再不行才放弃切换并记录；
///   ③ 充电中/空中/正在穿门时最多延后 MaxDefer 秒，不会把玩家切进墙里；
///   ④ 切换后短暂冷却，防抖。
///
/// 解锁后的世界（FactoryShift.LockedPhase 有值）不再强制切换——那是解谜奖励：
/// 把轮换的控制权交还玩家。
/// </summary>
[DisallowMultipleComponent]
public class FactoryTimeSwap : MonoBehaviour
{
    [Header("层锚点（留空 = 运行时按名字自动找 Day / Night 根节点）")]
    [SerializeField] private Transform dayRoot;
    [SerializeField] private Transform nightRoot;

    [Header("行为")]
    [Tooltip("切过去之后，最多尝试几次找落脚点（沿着朝向/侧向轻推）")]
    [SerializeField] private int settleTries = 6;
    [Tooltip("每次轻推的距离（米）")]
    [SerializeField] private float settleStep = 0.35f;
    [Tooltip("玩家处于不安全状态时最多延后多久再切（秒）")]
    [SerializeField] private float maxDefer = 1.5f;
    [Tooltip("切换后的冷却（秒），防抖")]
    [SerializeField] private float cooldown = 1f;

    private float deferUntil = -1f;
    private float nextSwapAt;
    private FactoryShift.Phase pendingTarget;
    private bool hasPending;

    public static FactoryTimeSwap Instance { get; private set; }

    /// <summary>最近一次切换是否成功（Play 测试读它）。</summary>
    public bool LastSwapOk { get; private set; }

    /// <summary>最近一次切换的落点（Play 测试读它）。</summary>
    public Vector3 LastLanding { get; private set; }

    /// <summary>今日已切换次数（排查"一直切"用）。</summary>
    public int SwapCount { get; private set; }

    private void Awake()
    {
        Instance = this;
        ResolveRoots();
        // 进场就按当前班次设好遮罩，否则开局能看见另一层
        ApplyViewMask(FactoryShift.Current);
    }

    private void OnEnable()
    {
        FactoryShift.OnPhaseFlipped += HandleFlip;
        FactoryShift.OnPhaseWarning += HandleWarning;
    }

    private void OnDisable()
    {
        FactoryShift.OnPhaseFlipped -= HandleFlip;
        FactoryShift.OnPhaseWarning -= HandleWarning;
        if (Instance == this) Instance = null;
    }

    private void ResolveRoots()
    {
        if (dayRoot == null || nightRoot == null)
        {
            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.transform == transform.root) continue;
                if (root.name == "Day") dayRoot = root.transform;
                else if (root.name == "Night") nightRoot = root.transform;
            }
        }
        if (dayRoot == null || nightRoot == null)
        {
            Debug.LogWarning("[FactoryTimeSwap] 找不到 Day/Night 根节点——强制切换不会生效。", this);
        }
    }

    private void HandleFlip()
    {
        // 已解锁 = 玩家拿回控制权，不再强制搬人
        if (FactoryShift.LockedPhase.HasValue) return;
        if (Time.time < nextSwapAt) return;

        pendingTarget = FactoryShift.Current;   // 换班后 Current 已经是目标班次
        hasPending = true;
        deferUntil = Time.time + maxDefer;
        TrySwapNow();
    }

    /// <summary>
    /// 换班前预警（OnPhaseWarning）：把目标层着色器变体的一次性编译成本挪到预警窗口里。
    ///
    /// 卡顿的根因（FrameHitchWatchdog 实测）：第一次切换到某层时，该层材质的 shader 变体
    /// 要现场编译，约 39ms——正好砸在换班那一帧。提前在预警的 5 秒里离屏渲一帧目标层，
    /// 编译就发生在玩家还在等挂钟响的时候；真到切换时只剩改一次坐标 + 换遮罩，<2ms。
    /// </summary>
    private void HandleWarning()
    {
        Transform to = FactoryShift.Current == FactoryShift.Phase.Night ? dayRoot : nightRoot;
        if (to == null) { ResolveRoots(); to = FactoryShift.Current == FactoryShift.Phase.Night ? dayRoot : nightRoot; }
        if (to == null) return;
        StartCoroutine(WarmLayer(to));
    }

    private System.Collections.IEnumerator WarmLayer(Transform layerRoot)
    {
        var cam = PlayerRef.Camera != null ? PlayerRef.Camera : Camera.main;
        if (cam == null) yield break;

        int targetBit = layerRoot.name == "Night" ? (1 << 9) : (1 << 8);
        int oldMask = cam.cullingMask;
        if ((oldMask & targetBit) != 0) yield break;

        var rt = RenderTexture.GetTemporary(256, 256, 16);
        var oldTarget = cam.targetTexture;
        cam.cullingMask = oldMask | targetBit;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = oldTarget;
        cam.cullingMask = oldMask;
        RenderTexture.ReleaseTemporary(rt);
    }

    private void Update()
    {
        if (!hasPending) return;
        if (Time.time > deferUntil)
        {
            // 等太久也得切——否则玩家一直处在"该切却没切"的错位状态
            ForceSwap();
            return;
        }
        TrySwapNow();
    }

    private bool SafeToSwap()
    {
        var player = PlayerRef.Root;
        if (player == null) return false;

        // 空中（离地超过一小步）就再等等，避免落点埋进地板
        var cc = PlayerRef.Controller;
        if (cc != null && !cc.isGrounded && cc.velocity.y < -1f) return false;

        // 充电中：玩家站桩，切走会静默断充
        var battery = BatterySystem.Instance;
        if (battery != null && battery.IsCharging) return false;

        // 正站在门洞里（穿门进行中）不切，否则和 PortalWindow 抢 transform
        var door = FindDoorNear(player.position, 1.2f);
        if (door) return false;

        return true;
    }

    private void TrySwapNow()
    {
        if (!SafeToSwap()) return;
        if (dayRoot == null || nightRoot == null) { ResolveRoots(); if (dayRoot == null || nightRoot == null) return; }
        ForceSwap();
    }

    private void ForceSwap()
    {
        hasPending = false;
        var player = PlayerRef.Root;
        if (player == null) { LastSwapOk = false; return; }

        bool goingNight = pendingTarget == FactoryShift.Phase.Night;
        Transform to = goingNight ? nightRoot : dayRoot;

        // 世界 XZ 原样带过去，只让 Y 重新落地。
        //
        // 为什么不用「相对层根的偏移」：两层几何在**世界 XZ 上重合**，但两个层根
        // 节点并不重合（实测 Day 根 (0, -12100.5, 13.0)，Night 根 (-0.54, -11998, -0.08)，
        // 差 x 0.54、z 13.08）。按层根算相对位移会额外平移这 13 米，玩家被甩到墙外。
        // 实测成对物体的世界坐标差：ShiftGate 0.02/-0.01、ShiftClock 0.46/-0.08、
        // 排班表 0.46/-0.08 —— 都在半米内，说明该沿用的是**世界 XZ**。
        //
        // Y 一律不用（两层地板差 100 米，且地板与层根的关系两层还不一样），一律交给
        // Settle() 在目标层用射线重新找地面。
        Vector3 desired = new Vector3(player.position.x, to.position.y, player.position.z);

        // 净空检测 + 找地面 + 轻推
        Vector3 landed;
        bool ok = Settle(desired, to, player, out landed);
        LastSwapOk = ok;
        LastLanding = landed;

        if (!ok)
        {
            // 实在找不到落脚点：宁可不切，也不要埋进墙里
            Debug.LogWarning("[FactoryTimeSwap] 落点无净空，放弃本次切换（desired=" + desired.ToString("F2") + "）");
            nextSwapAt = Time.time + cooldown;
            return;
        }

        PlayerRef.Teleport(landed, player.rotation);
        SwapCount++;
        nextSwapAt = Time.time + cooldown;

        // 换班那一下的"世界错位感"：一次短促重震 + 收尾余震（参照泰坦陨落2的时空切换）。
        // 必须放在 Teleport 之后：CameraShake 驱动在 LateUpdate 叠加偏移。
        CameraShake.Impulse(swapShakeAmplitude, swapShakeDuration, swapShakeFrequency);
        StartCoroutine(AfterShake(swapShakeDuration));

        // 相机遮罩跟着切：只渲染玩家所在那一层，另一层整片剔掉。
        // 这是防穿帮的关键——两层只差 100 米，主相机 farClip 已经设到 115，
        // 不剔层的话站在 Day 抬头能看见 Night 的整片厂房悬在头顶。
        ApplyViewMask(pendingTarget);

        // 同步世界登记：HUD/播报/撤离钟都靠它判断"玩家在哪个世界"
        WorldStreamer.NotifyCrossed(gameObject.scene.name);
    }

    /// <summary>
    /// 让主相机只渲染玩家所在的那一层。
    ///
    /// 保留位：默认层(0)、FirstPersonBody(6)、UI(5)、实验室(19) 等非工厂层一律保留，
    /// 只把另一层的位摘掉——这样玩家手上的东西、HUD、透门看到的实验室都不受影响。
    /// 第一人称手臂相机（mask=64 只有 FirstPersonBody）不参与，它本来就只看手。
    /// </summary>
    private void ApplyViewMask(FactoryShift.Phase current)
    {
        int dayBit = 1 << 8, nightBit = 1 << 9;

        var cam = PlayerRef.Camera != null ? PlayerRef.Camera : Camera.main;
        if (cam == null) return;

        int mask = cam.cullingMask;
        mask |= dayBit;                 // 先把两层都打开
        mask |= nightBit;
        mask &= ~(current == FactoryShift.Phase.Night ? dayBit : nightBit);  // 再关掉对面那层
        cam.cullingMask = mask;
    }

    /// <summary>
    /// 在期望落点附近找一个真正站得住的位置。
    ///
    /// **Y 完全由射线决定，不用 desired.y**：两层地板高度不同，玩家在源层的相对高度
    /// 搬到目标层没有意义。做法是从目标层根节点的上方往下打一条射线找地面。
    /// 找不到就沿玩家朝向的前/后/左/右轻推若干次。
    /// </summary>
    private bool Settle(Vector3 desired, Transform layerRoot, Transform player, out Vector3 landed)
    {
        landed = desired;
        var cc = PlayerRef.Controller;
        float radius = cc != null ? cc.radius * 0.9f : 0.3f;
        float height = cc != null ? cc.height : 1.8f;

        Vector3 fwd = player.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        // 找地面的方式：从**层根略下方**起扫，取所有命中里最低的那个。
        //
        // 两个坑，都是这个项目已经踩过的（Docs/ENEMY-AI.md §9 坑 3）：
        //  ① 从高处往下打单条射线，第一个命中的往往是**顶棚/天花板**——实测落点比地板
        //     高 10 米（landed y=-11991.9，而 Night 地板在 -12001.9），玩家会悬空下坠。
        //  ② 扫得太深会穿过本层地板命中**另一层**地板（两层只差 100 米）。
        // 所以：起点压在层根略下（顶棚通常在层根上方），扫深限制在 20 米内，
        // 命中用 RaycastNonAlloc 取最低的那个当真正的地板。
        Vector3 layerOrigin = layerRoot.position;
        float probeTopY = layerOrigin.y + 0.5f;
        const float ScanDepth = 20f;
        var hitsBuf = new RaycastHit[16];

        for (int i = 0; i <= settleTries; i++)
        {
            Vector3 dir = Vector3.zero;
            if (i > 0)
            {
                int leg = (i - 1) / 2 + 1;
                int axis = (i - 1) % 2;
                dir = (axis == 0 ? fwd : right) * settleStep * leg;
                if ((i % 4) >= 2) dir = -dir;
            }

            Vector3 col = desired + dir;             // 只看 XZ
            Vector3 probeTop = new Vector3(col.x, probeTopY, col.z);

            // 取所有命中里最低的面 = 真正的地板（顶棚/横梁都在它上面）
            int n = Physics.RaycastNonAlloc(probeTop, Vector3.down, hitsBuf, ScanDepth, ~0, QueryTriggerInteraction.Ignore);
            if (n <= 0) continue;

            float floorY = float.MaxValue;
            for (int h = 0; h < n; h++)
                if (hitsBuf[h].point.y < floorY) floorY = hitsBuf[h].point.y;

            Vector3 p = new Vector3(col.x, floorY + 0.06f, col.z);

            Vector3 bottom = p + Vector3.up * (radius + 0.02f);
            Vector3 top = p + Vector3.up * (height - radius + 0.02f);
            if (Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
                continue;

            landed = p;
            return true;
        }

        // 小幅轻推找不到位置时，向四周放大搜索一圈。
        //
        // 为什么需要这一步：实测二号风机那一带（世界 xz −9.1, 55.3）周围 2 米被
        // 柜子、碎屑、墙围死，settleTries×settleStep = 6×0.35 ≈ 2.1 米的轻推走不出去，
        // 结果每次换班都放弃切换——玩家留在 Day 层，而时钟/HUD 已经翻到 Night，
        // 状态自相矛盾。宁可把他挪到最近的空位，也不要卡在这种错位里。
        const float ringStep = 1f;
        const int ringRings = 5;      // 最远 5 米
        const int ringDirs = 8;
        for (int ring = 1; ring <= ringRings; ring++)
        {
            float r = ringStep * ring;
            for (int d = 0; d < ringDirs; d++)
            {
                float ang = d * Mathf.PI * 2f / ringDirs;
                Vector3 col = new Vector3(
                    desired.x + Mathf.Cos(ang) * r,
                    0f,
                    desired.z + Mathf.Sin(ang) * r);

                Vector3 probeTop = new Vector3(col.x, probeTopY, col.z);
                int n = Physics.RaycastNonAlloc(probeTop, Vector3.down, hitsBuf, ScanDepth, ~0, QueryTriggerInteraction.Ignore);
                if (n <= 0) continue;

                float floorY = float.MaxValue;
                for (int h = 0; h < n; h++)
                    if (hitsBuf[h].point.y < floorY) floorY = hitsBuf[h].point.y;

                Vector3 p = new Vector3(col.x, floorY + 0.06f, col.z);
                Vector3 bottom = p + Vector3.up * (radius + 0.02f);
                Vector3 top = p + Vector3.up * (height - radius + 0.02f);
                if (Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                landed = p;
                return true;
            }
        }

        return false;
    }

    /// <summary>玩家附近有没有门（穿门进行中不抢 transform）。</summary>
    private bool FindDoorNear(Vector3 pos, float radius)
    {
        var cols = Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            if (cols[i].GetComponentInParent<PortalWindow>() != null) return true;
        }
        return false;
    }

    [Header("换班冲击")]
    [SerializeField] private float swapShakeAmplitude = 0.075f;
    [SerializeField] private float swapShakeDuration = 0.55f;
    [SerializeField] private float swapShakeFrequency = 22f;
    [SerializeField] private float afterShakeAmplitude = 0.022f;

    /// <summary>主震结束后补一层余震，让错位感有个收束。</summary>
    private System.Collections.IEnumerator AfterShake(float wait)
    {
        yield return new WaitForSeconds(wait);
        CameraShake.Impulse(afterShakeAmplitude, 0.35f, 14f);
    }

    // ── Play 测试入口 ──

    /// <summary>立刻强制切到指定班次（测试用；不受时钟与解锁影响）。</summary>
    public bool ForceSwapTo(FactoryShift.Phase target)
    {
        pendingTarget = target;
        hasPending = true;
        ForceSwap();
        return LastSwapOk;
    }
}
