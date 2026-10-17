using System;
using UnityEngine;

/// <summary>
/// 玩家所处世界的全局快照（Core：WorldStreamer 写，Content 的 HUD 等读——分层合法）。
/// HUD 依据 InHome / 当前世界名切换显示形态。
/// </summary>
public static class WorldPresence
{
    /// <summary>起始世界（实验室）名；空 = 尚未初始化。</summary>
    public static string Home { get; private set; }

    /// <summary>玩家当前所在世界名（穿门时更新）。</summary>
    public static string Current { get; private set; }

    /// <summary>是否身处起始世界（未初始化时视为真，HUD 走实验室形态）。</summary>
    public static bool InHome
    {
        get { return string.IsNullOrEmpty(Home) || Current == Home; }
    }

    /// <summary>所处世界变化时触发。</summary>
    public static event Action OnChanged;

    public static void SetHome(string world)
    {
        if (Home == world) return;
        Home = world;
        if (Current == null) Current = world;
        if (OnChanged != null) OnChanged();
    }

    public static void SetCurrent(string world)
    {
        if (string.IsNullOrEmpty(world) || Current == world) return;
        Current = world;
        if (OnChanged != null) OnChanged();
    }
}
