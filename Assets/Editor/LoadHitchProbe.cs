using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界装载卡顿探针（编辑器诊断，仅 Editor 程序集，不会进打包）。
///
/// 用途：回答"加载世界时卡在哪一步"——逐帧记录耗时，把超过阈值的帧连同
/// **前后 1 秒内的日志**（WorldStreamer 的预载/激活/卸载、WorldLightIsolation
/// 的光影隔离、PortalWindow 的取景建立都打了日志）一起留档，一眼看出尖峰归属。
///
/// 用法（Play 中调用，execute_code / 菜单均可）：
///   1) LoadHitchProbe.Begin();          // 开始记录
///   2) ……复现卡顿（开局 / 走近门 / 穿门）……
///   3) LoadHitchProbe.End(); Debug.Log(LoadHitchProbe.Report());
///
/// 判读：
///   · 预载阶段的帧只是"略高"（读盘/反序列化按帧预算摊开）＝正常；
///   · 单帧尖峰应能在前后日志里找到归属（"激活世界 X" / "世界「X」光影隔离" /
///     "取景已建立"）；找不到归属的尖峰通常是 GC 或资源上传，报告里带 GC 次数。
/// </summary>
public static class LoadHitchProbe
{
    private struct FrameRec
    {
        public int frame;
        public double t;
        public float dt;
        public int gc;           // GC.CollectionCount(0) 累计（差值 = 本帧发生过 GC）
        public string scenes;    // 本帧在册场景（只在超阈值帧记录，平时为空省开销）
    }

    private struct LogRec
    {
        public double t;
        public string msg;
        public LogType type;
    }

    private struct MarkRec
    {
        public double t;
        public string label;
    }

    /// <summary>单帧超过这个毫秒数就记成"尖峰"（33ms ≈ 30fps 一帧）。</summary>
    private static float spikeMs = 33f;

    private static readonly List<FrameRec> frames = new List<FrameRec>();
    private static readonly List<LogRec> logs = new List<LogRec>();
    private static readonly List<MarkRec> marks = new List<MarkRec>();

    private static bool running;
    private static int lastFrame = -1;
    private static double startedAt;
    private static double stoppedAt;
    private static int logCap = 4000;

    public static bool Running { get { return running; } }

    // ─────────────────────────────────────────────────────────────
    // 控制
    // ─────────────────────────────────────────────────────────────
    [MenuItem("FYP/诊断/卡顿探针 开始记录")]
    public static void BeginMenu() { Begin(); Debug.Log("[LoadHitchProbe] 开始记录（菜单）"); }

    [MenuItem("FYP/诊断/卡顿探针 结束并输出报告")]
    public static void EndAndReportMenu()
    {
        End();
        Debug.Log(Report());
    }

    /// <summary>
    /// 打开"进 Play 自动开录"：省得手动掐点，开局第一帧就在记录里。
    /// 存 EditorPrefs 而不是静态字段——进 Play 会做域重载，静态字段全清，
    /// 订阅也会断；本类的 [InitializeOnLoadMethod] 会在重载后据 EditorPrefs 复原订阅。
    /// </summary>
    public static bool AutoBegin
    {
        get { return EditorPrefs.GetBool(AutoBeginKey, false); }
        set
        {
            EditorPrefs.SetBool(AutoBeginKey, value);
            if (value) EditorApplication.playModeStateChanged += OnPlayModeChanged;
            else EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
    }

    private const string AutoBeginKey = "LoadHitchProbe.AutoBegin";

    [InitializeOnLoadMethod]
    private static void RestoreAutoBegin()
    {
        if (!EditorPrefs.GetBool(AutoBeginKey, false)) return;
        // 域重载后重挂：先摘再挂，避免重复订阅
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange st)
    {
        if (st == PlayModeStateChange.EnteredPlayMode) Begin();
        else if (st == PlayModeStateChange.ExitingPlayMode) End();   // 数据留着，退 Play 后还能出报告
    }

    [MenuItem("FYP/诊断/卡顿探针 进 Play 自动开录（当前：关）")]
    private static void AutoBeginOn() { AutoBegin = true; }

    [MenuItem("FYP/诊断/卡顿探针 关闭自动开录")]
    private static void AutoBeginOff() { AutoBegin = false; }

    public static void Begin() { Begin(33f); }

    public static void Begin(float thresholdMs)
    {
        if (running) End();
        frames.Clear();
        logs.Clear();
        marks.Clear();
        spikeMs = Mathf.Max(1f, thresholdMs);
        startedAt = EditorApplication.timeSinceStartup;
        lastFrame = -1;
        running = true;
        LoadDiag.Enabled = true;      // 让"装载链路内部几段重活"也打出耗时（[LoadDiag] 行）
        Application.logMessageReceived += OnLog;
        EditorApplication.update += OnUpdate;
    }

    public static void End()
    {
        if (!running) return;
        running = false;
        LoadDiag.Enabled = false;
        stoppedAt = EditorApplication.timeSinceStartup;
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= OnUpdate;
    }

    /// <summary>打一个阶段标记（复现流程用，报告里会和时间线对齐）。</summary>
    public static void Mark(string label)
    {
        if (!running) return;
        marks.Add(new MarkRec { t = EditorApplication.timeSinceStartup, label = label });
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        if (!running) return;
        if (logs.Count >= logCap) logs.RemoveAt(0);
        logs.Add(new LogRec { t = EditorApplication.timeSinceStartup, msg = msg, type = type });
    }

    private static void OnUpdate()
    {
        if (!running) return;
        int f = Time.frameCount;
        if (f == lastFrame) return;      // 编辑器多跑的空更新：没有新游戏帧就跳过
        float dt = Time.unscaledDeltaTime;
        lastFrame = f;
        int gc = System.GC.CollectionCount(0);
        string scenes = "";
        if (dt * 1000f >= spikeMs)
        {
            // 只在尖峰帧采集场景清单（有点开销，平时不采）
            var sb = new StringBuilder();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (sb.Length > 0) sb.Append("+");
                sb.Append(s.name);
            }
            scenes = sb.ToString();
        }
        frames.Add(new FrameRec { frame = f, t = EditorApplication.timeSinceStartup, dt = dt, gc = gc, scenes = scenes });
    }

    // ─────────────────────────────────────────────────────────────
    // 报告
    // ─────────────────────────────────────────────────────────────
    public static string Report() { return Report(20); }

    public static string Report(int topHitches)
    {
        if (frames.Count == 0) return "[LoadHitchProbe] 没有记录到任何帧（是否忘了 Begin？）";

        double span = frames[frames.Count - 1].t - frames[0].t;
        double sum = 0;
        float max = 0f;
        var sorted = new List<float>();
        for (int i = 0; i < frames.Count; i++)
        {
            float ms = frames[i].dt * 1000f;
            sum += ms;
            sorted.Add(ms);
            if (ms > max) max = ms;
        }
        sorted.Sort();

        var sb = new StringBuilder();
        sb.AppendLine("[LoadHitchProbe] 帧数 " + frames.Count + "  时长 " + span.ToString("F1") + "s"
                      + "  平均 " + (sum / frames.Count).ToString("F1") + "ms"
                      + "  中位 " + sorted[sorted.Count / 2].ToString("F1") + "ms"
                      + "  P95 " + sorted[Mathf.Clamp((int)(sorted.Count * 0.95f), 0, sorted.Count - 1)].ToString("F1") + "ms"
                      + "  最大 " + max.ToString("F1") + "ms"
                      + "  尖峰阈值 " + spikeMs.ToString("F0") + "ms");

        // ── 尖峰榜（按耗时降序，同一处只留一条）──
        var spikes = new List<FrameRec>();
        for (int i = 0; i < frames.Count; i++)
            if (frames[i].dt * 1000f >= spikeMs) spikes.Add(frames[i]);
        spikes.Sort((a, b) => b.dt.CompareTo(a.dt));

        int shown = Mathf.Min(topHitches, spikes.Count);
        sb.AppendLine("── 尖峰 " + spikes.Count + " 帧，按耗时取前 " + shown + " ──");
        for (int i = 0; i < shown; i++)
        {
            var fr = spikes[i];
            sb.AppendLine("· +" + (fr.t - frames[0].t).ToString("F2") + "s  帧#" + fr.frame
                          + "  " + (fr.dt * 1000f).ToString("F0") + "ms"
                          + "  场景=" + (string.IsNullOrEmpty(fr.scenes) ? "?" : fr.scenes));
            // 该帧前后 1s 内的日志（含本帧之后 0.3s：激活帧的"光影隔离""取景已建立"往往紧跟其后）
            string lastMsg = null;
            int repeat = 0;
            for (int j = 0; j < logs.Count; j++)
            {
                var lg = logs[j];
                if (lg.t < fr.t - 1.0 || lg.t > fr.t + 0.3) continue;
                string msg = OneLine(lg.msg);
                if (msg == lastMsg) { repeat++; continue; }
                if (repeat > 0) { sb.AppendLine("      （同上 ×" + (repeat + 1) + "，省略）"); repeat = 0; }
                lastMsg = msg;
                string tag = lg.type == LogType.Error || lg.type == LogType.Exception ? "!!" : (lg.type == LogType.Warning ? "!" : "·");
                sb.AppendLine("      " + tag + " [" + (lg.t - frames[0].t).ToString("F2") + "s] " + msg);
            }
            if (repeat > 0) sb.AppendLine("      （同上 ×" + (repeat + 1) + "，省略）");
        }

        // ── 时间线（阶段标记 + WorldStreamer 状态转移日志）──
        sb.AppendLine("── 时间线（标记 / 装载相关日志）──");
        var merged = new List<LogRec>();
        for (int i = 0; i < logs.Count; i++)
        {
            string m = logs[i].msg;
            if (m.StartsWith("[WorldStreamer]") || m.StartsWith("[WorldLightIsolation]") || m.StartsWith("[PortalWindow]"))
                merged.Add(logs[i]);
        }
        int mi = 0, ki = 0;
        string lastLine = null;
        int same = 0;
        while (mi < merged.Count || ki < marks.Count)
        {
            bool takeMark = ki < marks.Count && (mi >= merged.Count || marks[ki].t <= merged[mi].t);
            string line;
            if (takeMark) { line = "★ +" + (marks[ki].t - frames[0].t).ToString("F2") + "s  " + marks[ki].label; ki++; }
            else { line = "  +" + (merged[mi].t - frames[0].t).ToString("F2") + "s  " + OneLine(merged[mi].msg); mi++; }

            if (line == lastLine) { same++; continue; }
            if (same > 0) { sb.AppendLine("    （同上 ×" + (same + 1) + "，省略）"); same = 0; }
            lastLine = line;
            sb.AppendLine(line);
        }
        if (same > 0) sb.AppendLine("    （同上 ×" + (same + 1) + "，省略）");

        // ── GC ──
        int gcTotal = 0;
        for (int i = 1; i < frames.Count; i++)
            gcTotal += Mathf.Max(0, frames[i].gc - frames[i - 1].gc);
        sb.AppendLine("── GC(gen0) 次数 " + gcTotal + " ──");
        return sb.ToString();
    }

    private static string OneLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length > 160 ? s.Substring(0, 160) + "…" : s;
    }

    /// <summary>当前记录的原始帧数据条数（脚本轮询用）。</summary>
    public static int FrameCount { get { return frames.Count; } }
}
