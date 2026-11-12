using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 异常源在场登记表（Core 静态桥，与 NoiseBus 同构）。
///
/// World 层的异常脚本在 OnEnable 注册自己的位置，事件进行中 SetActive(true/false)；
/// Player 层的探索仪器（拍照相机 / 温度计 / 录音笔）轮询「当前活跃的异常源在哪」。
/// 为什么要中转：仪器不可能定向引用每个异常脚本（异常会越加越多，仪器也会越加越多），
/// 双方只认识这一张登记表——和 NoiseBus（玩家→敌人）、EmfStorm（异常→HUD）是同一个模式。
///
/// 「活跃」的语义由注册方自己定：常驻异常（如对面的灯）恒为 true；
/// 事件型异常（幽灵进站 / 电磁风暴 / 敲击）只在演出进行中为 true——
/// 仪器只在异常「正在发生」时读得到它，这与玩家的直觉一致。
/// </summary>
public static class AnomalyPresence
{
    /// <summary>一次读数：某个活跃异常源的实时快照。</summary>
    public struct Reading
    {
        public string Id;
        public string DisplayName;
        public Vector3 Position;
    }

    private class Entry
    {
        public Transform Origin;
        public string DisplayName;
        public bool Active;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    /// <summary>注册一个异常源（默认不活跃；常驻异常注册后立刻 SetActive(true)）。
    /// 重复 id 覆盖旧条目——场景重载 / 域重载后异常重新注册自己的场景。</summary>
    public static void Register(string id, Transform origin, string displayName)
    {
        if (string.IsNullOrEmpty(id) || origin == null) return;
        entries[id] = new Entry { Origin = origin, DisplayName = displayName, Active = false };
    }

    /// <summary>注销异常源（OnDisable / OnDestroy 时调用；不调也只是读到陈旧位置，不致命）。</summary>
    public static void Unregister(string id)
    {
        if (!string.IsNullOrEmpty(id)) entries.Remove(id);
    }

    /// <summary>事件型异常在演出开始 / 结束时切换活跃状态。</summary>
    public static void SetActive(string id, bool active)
    {
        Entry e;
        if (entries.TryGetValue(id, out e)) e.Active = active;
    }

    public static bool IsActive(string id)
    {
        Entry e;
        return entries.TryGetValue(id, out e) && e.Active && e.Origin != null;
    }

    /// <summary>
    /// 把当前活跃的异常源快照进 buffer 并返回数量。调用方复用自己的 List 做到零分配
    ///（仪器每帧轮询，不能在这里 new）。Origin 已销毁的条目顺手丢弃。
    /// </summary>
    public static int CollectActive(List<Reading> buffer)
    {
        if (buffer == null) return 0;
        buffer.Clear();
        List<string> dead = null;
        foreach (var kv in entries)
        {
            var e = kv.Value;
            if (e.Origin == null) { if (dead == null) dead = new List<string>(); dead.Add(kv.Key); continue; }
            if (!e.Active) continue;
            buffer.Add(new Reading { Id = kv.Key, DisplayName = e.DisplayName, Position = e.Origin.position });
        }
        if (dead != null) for (int i = 0; i < dead.Count; i++) entries.Remove(dead[i]);
        return buffer.Count;
    }
}
