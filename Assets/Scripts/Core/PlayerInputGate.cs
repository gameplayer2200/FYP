/// <summary>
/// 玩家输入闸门：开局部署期间把玩家冻住（能看、不能动）。
///
/// 为什么放在 Core 而不是 World：Player 程序集不能反向引用 World（ADR-0004 单向依赖），
/// 而开局装载器在 World 层——两边只能靠一个中立的静态开关通信，与
/// WorldPresence / EmfStorm / BillingLedger 同一个模式。
///
/// 为什么不是"禁用组件"：Play 里开关组件会打断 Awake/OnEnable 建立的缓存
/// （相机引用、重力累积、Animator 参数），恢复时容易漏一两项。这里只做"每帧早退"，
/// 组件状态原封不动，放开的那一帧就跟没冻过一样。
///
/// 视觉仍要保留：鼠标可以转视角，让玩家在等待时能看看实验室。
/// </summary>
public static class PlayerInputGate
{
    /// <summary>true = 禁止移动/跳跃/蹲伏/交互，但允许转视角。</summary>
    public static bool Locked { get; private set; }

    /// <summary>冻结/解冻。重复调用同一状态无副作用。</summary>
    public static void Set(bool locked) { Locked = locked; }

    /// <summary>任何代码都可以问一句"现在能动吗"，省得各自判空。</summary>
    public static bool CanAct { get { return !Locked; } }
}
