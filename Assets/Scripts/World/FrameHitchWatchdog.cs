using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 帧尖峰看门狗：在**游戏内部**记录长帧，把"卡了一下"变成可查的证据。
///
/// 为什么需要它：用编辑器外部工具（MCP execute_code / Profiler 附加）测帧时间不可靠——
/// 每次执行代码本身要 Roslyn 编译，会造出 300~500ms 的**假尖峰**，与真实卡顿混在一起。
/// 实测教训：连续调用 execute_code 期间每次都有 350~500ms "尖峰"，但完全不调用 Unity
/// 静置 25 秒，一次尖峰都不出现——说明那些全是测量工具自己造成的。
/// 本组件跑在游戏自己的 Update 里，除一次减法外零开销，拿到的才是玩家真实体感。
///
/// 记录内容：帧时长、当时正在激活/预载的世界、玩家所在世界、GC 是否暴涨。
/// 最后一行汇总按"世界 + 阶段"归类，直接回答"是哪个世界激活时卡的、卡了多少毫秒"。
///
/// 开关：FrameHitchWatchdog.Enabled（默认 true，阈值 阈值内不产生任何日志与分配）。
/// 关闭方法：给任意一个物体上挂本组件并取消勾选，或把 ThresholdMs 调到很大。
/// </summary>
[DisallowMultipleComponent]
public class FrameHitchWatchdog : MonoBehaviour
{
    /// <summary>判定为"尖峰"的帧时长阈值（毫秒）。低于它的帧完全静默。</summary>
    public float ThresholdMs = 120f;

    /// <summary>最多保留多少条明细（防内存堆积；汇总仍会统计全部）。</summary>
    public int MaxRecords = 200;

    /// <summary>总开关。关掉后 Update 里只剩一次 return。</summary>
    public static bool Enabled = true;

    /// <summary>
    /// 抑制到第几帧为止。诊断脚本/编辑器工具执行时本身要编译代码，会造出 300~500ms 的
    /// **假尖峰**；调工具的一方在动手前调 Suppress(3) 就能把这几帧排除掉，
    /// 让记录里只剩玩家真实会遇到的卡顿。
    /// </summary>
    private static int suppressUntilFrame = -1;

    public static void Suppress(int frames)
    {
        int until = Time.frameCount + Mathf.Max(1, frames);
        if (until > suppressUntilFrame) suppressUntilFrame = until;
    }

    /// <summary>最近一次尖峰的世界与耗时，供外部（HUD/诊断）读取。</summary>
    public static string LastHitch { get; private set; }

    private struct Hitch
    {
        public float ms;
        public int frame;
        public string world;
        public string activeSet;
        public string note;
    }

    private readonly List<Hitch> records = new List<Hitch>();
    private float lastRealtime;
    private long lastGc;
    private bool primed;

    private void OnEnable()
    {
        lastRealtime = Time.realtimeSinceStartup;
        lastGc = System.GC.GetTotalMemory(false);
        primed = true;
    }

    private void Update()
    {
        if (!Enabled || !primed) return;

        float now = Time.realtimeSinceStartup;
        float ms = (now - lastRealtime) * 1000f;
        lastRealtime = now;

        // 被抑制的帧（诊断工具正在执行）不记账，避免把工具开销算成游戏卡顿
        if (Time.frameCount <= suppressUntilFrame) { lastGc = System.GC.GetTotalMemory(false); return; }

        // 取 GC 变化：装载/激活期间大量临时对象会触发 GC，是"卡一下"的常见帮凶
        long gc = System.GC.GetTotalMemory(false);
        long gcDelta = gc - lastGc;
        lastGc = gc;

        // 编辑器里断点/暂停/首次编译会造出巨大间隔，那不是游戏卡顿，丢掉
        if (ms < ThresholdMs || ms > 5000f) return;

        string note = gcDelta > 8 * 1024 * 1024
            ? "GC +" + (gcDelta / 1048576) + "MB"
            : (gcDelta < -8 * 1024 * 1024 ? "GC 回收 " + (-gcDelta / 1048576) + "MB" : "");

        string activeSet = WorldStreamer.DebugActiveSet();

        if (records.Count < Mathf.Max(1, MaxRecords))
        {
            records.Add(new Hitch
            {
                ms = ms,
                frame = Time.frameCount,
                world = WorldPresence.Current,
                activeSet = activeSet,
                note = note
            });
        }

        LastHitch = ms.ToString("F0") + "ms @" + WorldPresence.Current;

        // 每条尖峰都打一行（尖峰本来就稀少，不会刷屏）
        Debug.Log("[帧尖峰] " + ms.ToString("F0") + "ms  frame=" + Time.frameCount
                  + "  世界=" + WorldPresence.Current
                  + "  装载=" + (string.IsNullOrEmpty(activeSet) ? "无" : activeSet)
                  + (string.IsNullOrEmpty(note) ? "" : "  " + note));
    }

    /// <summary>打印按"世界 + 装载集"归类的汇总——谁在卡，一目了然。</summary>
    public void DumpSummary()
    {
        if (records.Count == 0)
        {
            Debug.Log("[帧尖峰] 没有记录（阈值 " + ThresholdMs.ToString("F0") + "ms）");
            return;
        }

        var groups = new Dictionary<string, List<float>>();
        float total = 0f, worst = 0f;
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            total += r.ms;
            if (r.ms > worst) worst = r.ms;
            string key = r.world + " | " + (string.IsNullOrEmpty(r.activeSet) ? "无装载" : r.activeSet);
            List<float> list;
            if (!groups.TryGetValue(key, out list)) { list = new List<float>(); groups[key] = list; }
            list.Add(r.ms);
        }

        var sb = new StringBuilder();
        sb.Append("[帧尖峰汇总] 共 ").Append(records.Count).Append(" 次，总 ")
          .Append(total.ToString("F0")).Append("ms，最坏 ").Append(worst.ToString("F0")).Append("ms\n");
        foreach (var kv in groups)
        {
            float sum = 0f, max = 0f;
            for (int i = 0; i < kv.Value.Count; i++)
            {
                sum += kv.Value[i];
                if (kv.Value[i] > max) max = kv.Value[i];
            }
            sb.Append("  ").Append(kv.Key)
              .Append("  ×").Append(kv.Value.Count)
              .Append("  合计").Append(sum.ToString("F0")).Append("ms")
              .Append("  最坏").Append(max.ToString("F0")).Append("ms\n");
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>清空记录（测新的一段流程前用）。</summary>
    public void ClearRecords()
    {
        records.Clear();
        LastHitch = null;
    }

    /// <summary>全局快捷：找场上那一个实例（没有就静默返回）。</summary>
    public static FrameHitchWatchdog Instance
    {
        get { return FindObjectOfType<FrameHitchWatchdog>(); }
    }

    /// <summary>
    /// 自动常驻。诊断工具不该要求美术/策划去场景里挂组件，也不该在玩家开玩前被忘掉；
    /// 开局自己建一个 DontDestroyOnLoad 的宿主，跨世界装卸不丢记录。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        if (FindObjectOfType<FrameHitchWatchdog>() != null) return;

        var go = new GameObject("[FrameHitchWatchdog]");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<FrameHitchWatchdog>();
    }
}
