using System;

/// <summary>水淹区闭环里要找的东西的种类。</summary>
public enum FloodedItemKind
{
    Tool,   // 修理工具（能拧开保险箱机械面板的东西）
    Fuel,   // 燃料（润滑锈死的传动轴）
    Key     // 钥匙（最终开门用）
}

/// <summary>
/// 水淹区「进门锁死 → 修保险箱 → 拿钥匙 → 开门」这条闭环的进度。
///
/// 为什么单独放一个静态状态、而不是塞进 Inventory：
/// 1) Inventory 是共用脚本，本轮约定「不碰别人的代码」，避免 GitHub 合并冲突；
/// 2) Inventory 的规则是「随身收容有容量上限、瘫倒会散失」，而钥匙这类**任务道具不能丢**，
///    否则玩家会陷入无法通关的硬锁。任务进度独立存放，天然免疫散失规则。
///
/// 注意：静态字段在本次 Play 会话内保留，退出 Play（域重载）即清空，正好等于「每次进关重新开始」。
/// </summary>
public static class FloodedLoopState
{
    public static bool HasTool { get; private set; }
    public static bool HasFuel { get; private set; }
    public static bool HasKey { get; private set; }
    public static bool SafeOpened { get; private set; }

    /// <summary>任意进度变化时广播（HUD、提示、门锁都可以订阅）。</summary>
    public static event Action OnChanged;

    public static bool Has(FloodedItemKind kind)
    {
        switch (kind)
        {
            case FloodedItemKind.Tool: return HasTool;
            case FloodedItemKind.Fuel: return HasFuel;
            case FloodedItemKind.Key: return HasKey;
        }
        return false;
    }

    public static void Give(FloodedItemKind kind)
    {
        switch (kind)
        {
            case FloodedItemKind.Tool: HasTool = true; break;
            case FloodedItemKind.Fuel: HasFuel = true; break;
            case FloodedItemKind.Key: HasKey = true; break;
        }
        Raised();
    }

    public static void MarkSafeOpened()
    {
        SafeOpened = true;
        Raised();
    }

    /// <summary>还缺什么（保险箱按「先工具、后燃料」的顺序提示）。</summary>
    public static string MissingForSafe()
    {
        if (!HasTool) return "机械面板被撬坏了——需要能拧开螺丝的工具";
        if (!HasFuel) return "传动轴锈死了——需要燃料把它润开";
        return string.Empty;
    }

    /// <summary>回到实验室或重开一局时清空。</summary>
    public static void ResetAll()
    {
        HasTool = false;
        HasFuel = false;
        HasKey = false;
        SafeOpened = false;
        Raised();
    }

    private static void Raised()
    {
        if (OnChanged != null) OnChanged();
    }
}
