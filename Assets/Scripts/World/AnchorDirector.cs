using UnityEngine;

/// <summary>
/// 现实锚定值驱动器（World）：每帧推进 <see cref="RealityAnchor"/>，崩解时执行强制撤离。
///
/// 【为什么由 World 层驱动，而不是 Core 自己 Tick】
/// Core 是纯静态数据层，没有 MonoBehaviour 生命周期；而"玩家在不在层域"这件事
/// 要读 <see cref="WorldPresence"/>（也是 Core 静态，但由 WorldStreamer 每帧写），
/// 判断谁进谁出属于世界裁决，落在 World 程序集合规（ADR-0004 单向依赖）。
///
/// 【崩解处理】
/// 锚定值归零时不是简单扣血，而是**本地现实撕毁**：闪屏 + 门区失控 + 强制传送回实验室，
/// 本轮随身异常物全部丢失（CollapsePenalty 已有对应钩子），订单视为失败。
/// 这是本系统唯一的"硬惩罚"，因此演出要足够重。
/// </summary>
public class AnchorDirector : MonoBehaviour
{
    [Header("推进")]
    [Tooltip("每帧推进锚定值（关掉可手动测试）")]
    [SerializeField] private bool ticking = true;

    [Header("崩解演出")]
    [Tooltip("崩解时的屏幕震动强度")]
    [SerializeField] private float collapseShake = 0.16f;
    [Tooltip("崩解到传送到实验室之间的延迟（秒），留给玩家看清发生了什么")]
    [SerializeField] private float collapseDelay = 1.6f;
    [Tooltip("归零后警告提示的悬浮时长")]
    [SerializeField] private float warningToastTime = 3.5f;

    private bool inExpedition;
    private bool collapsing;
    private float collapseAt;

    /// <summary>最近一次崩解发生的时刻（诊断/测试用）。</summary>
    public float LastCollapseTime { get; private set; } = -1f;

    private void OnEnable()
    {
        RealityAnchor.OnCollapsed += HandleCollapsed;
        RealityAnchor.OnLevelChanged += HandleLevelChanged;
        RealityAnchor.OnPenalty += HandlePenalty;
    }

    private void OnDisable()
    {
        RealityAnchor.OnCollapsed -= HandleCollapsed;
        RealityAnchor.OnLevelChanged -= HandleLevelChanged;
        RealityAnchor.OnPenalty -= HandlePenalty;
    }

    /// <summary>失误扣分时弹提示（Core 不能直接调 HintToast，由 World 层代劳）。</summary>
    private void HandlePenalty(float amount, string reason)
    {
        if (string.IsNullOrEmpty(reason)) return;
        HintToast.Show(reason + "　锚定 -" + (int)amount, 2.2f);
    }

    private void Update()
    {
        // 进层域 / 回家 的边界检测：用 WorldPresence 的 InHome 而不是场景名，
        // 这样工厂昼夜两个世界共用一个场景也能正确判断。
        bool nowInExpedition = !string.IsNullOrEmpty(WorldPresence.Current) && !WorldPresence.InHome;

        if (nowInExpedition && !inExpedition)
        {
            inExpedition = true;
            collapsing = false;
            RealityAnchor.BeginExpedition();
            HintToast.Show("进入层域——现实锚定 " + (int)RealityAnchor.MaxValue + "%", 2.5f);
        }
        else if (!nowInExpedition && inExpedition)
        {
            inExpedition = false;
            RealityAnchor.EndExpedition();
            TensionMeter.Reset();
        }

        if (collapsing)
        {
            if (Time.time >= collapseAt)
            {
                collapsing = false;
                ExecuteCollapse();
            }
            return;
        }

        if (!ticking) return;
        if (!RealityAnchor.Active) return;

        RealityAnchor.Tick(
            Time.deltaTime,
            TensionMeter.Value,
            PlayerGear.FlashlightOn);
    }

    /// <summary>跨过警戒线/危急线时的提示。</summary>
    private void HandleLevelChanged(AnchorLevel lv)
    {
        switch (lv)
        {
            case AnchorLevel.Warn:
                HintToast.Show("现实开始松动——尽快回收并撤离", warningToastTime);
                CameraShake.Impulse(0.03f, 0.4f);
                break;
            case AnchorLevel.Critical:
                HintToast.Show("现实即将崩解！立刻撤回实验室", warningToastTime);
                CameraShake.Impulse(0.07f, 0.8f);
                break;
        }
    }

    /// <summary>归零：进入崩解演出，延迟后强制撤离。</summary>
    private void HandleCollapsed()
    {
        if (collapsing) return;
        collapsing = true;
        collapseAt = Time.time + collapseDelay;
        LastCollapseTime = Time.time;

        HintToast.Show("锚定归零——本地现实崩解", warningToastTime);
        CameraShake.Impulse(collapseShake, collapseDelay);
        EmfStorm.Set(true);   // 复用已有乱码系统做视觉撕裂
        Debug.Log("[Anchor] 锚定归零，准备强制撤离");
    }

    /// <summary>执行强制撤离：把玩家丢回实验室，清空随身异常物与锚定值。</summary>
    private void ExecuteCollapse()
    {
        RealityAnchor.Reset();

        // 强制回实验室：优先用实验室侧的任意门作为落点（与瘫倒重启同一套规则）。
        Transform home = FindHomeDoor();
        if (home != null)
        {
            Vector3 target = home.position + home.forward * 2.2f + Vector3.up * 0.2f;
            PlayerRef.Teleport(target, home.rotation);
            CollapsePenalty.Notify(true);
        }
        else
        {
            CollapsePenalty.Notify(false);
        }

        HintToast.Show("你在实验室里醒来——本轮回收作废", 4f);
        Debug.Log("[Anchor] 强制撤离完成");
    }

    /// <summary>找实验室侧的门（门名带 -A 后缀的那几扇，见 AGENTS.md §3 命名约定）。</summary>
    private Transform FindHomeDoor()
    {
        var doors = Object.FindObjectsOfType<PortalWindow>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;
            // 实验室侧的门名以 -A 结尾（Metro-A / FactoryDay-A）
            if (doors[i].name.EndsWith("-A"))
                return doors[i].transform;
        }
        // 兜底：第一个找到的门
        return doors.Length > 0 && doors[0] != null ? doors[0].transform : null;
    }

    /// <summary>调试用：立刻把锚定值打到某个值。</summary>
    public void DebugSetAnchor(float value)
    {
        if (value <= 0f)
        {
            RealityAnchor.BeginExpedition();
            RealityAnchor.Damaged(RealityAnchor.MaxValue + 1f);
            return;
        }
        RealityAnchor.BeginExpedition();
        RealityAnchor.Damaged(RealityAnchor.MaxValue - value);
    }
}
