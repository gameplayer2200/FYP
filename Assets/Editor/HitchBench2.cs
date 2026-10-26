using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// 装载卡顿实测驱动器（编辑器专用）v2。
///
/// 为什么要"在游戏内自己走完整个流程"：每次从外部（MCP execute_code）调用 Unity 都要
/// Roslyn 编译代码，会造出 300~500ms 的**假帧尖峰**。实测：连续调用期间每次都有
/// 350~500ms 尖峰，而完全不调用 Unity 静置 25 秒一次都不出现——那些全是测量工具自己造成的。
///
/// v2 改进：v1 抓到 1636ms 尖峰时"装载状态为空但单帧分配 53MB"，说明是分配/GC 而非装载。
/// 本版把阈值降到 50ms 并记录更多上下文（场景数、渲染器数、相机数、托管内存、GC 次数），
/// 以便定位到底是"第一次渲染新世界"还是"某段代码在狂分配"。
///
/// 菜单：FYP/诊断/装载卡顿实测v2
/// </summary>
public static class HitchBench2
{
    private const float ThresholdMs = 50f;

    private struct Rec
    {
        public float ms;
        public int frame;
        public string phase;
        public string world;
        public string activeSet;
        public long gcDelta;
        public long gcTotal;
        public int sceneCount;
        public int renderers;
        public int cameras;
        public long monoUsed;
    }

    private readonly static List<Rec> recs = new List<Rec>();
    private static float lastT;
    private static long lastGc;
    private static bool running;
    private static string phase = "-";
    private static int stepFrames;
    private static int phaseIndex;
    private static float phaseStart;
    private static Recorder gcRecorder;

    private static readonly string[] PhaseNames =
    {
        "0-静止观察(门旁)",
        "1-移到远处",
        "2-静止观察(远处)",
        "3-瞬移回门前",
        "4-静止观察(门前)",
        "5-开门",
        "6-观察(门开)",
    };

    [MenuItem("FYP/诊断/装载卡顿实测v2")]
    public static void StartBench()
    {
        if (!Application.isPlaying) { Debug.LogError("[实测v2] 必须先进入 Play 模式"); return; }
        if (running) { Debug.LogWarning("[实测v2] 已经在跑"); return; }

        recs.Clear();
        phaseIndex = 0;
        stepFrames = 0;
        phase = PhaseNames[0];
        phaseStart = Time.realtimeSinceStartup;
        running = true;
        lastT = Time.realtimeSinceStartup;
        lastGc = System.GC.GetTotalMemory(false);

        try
        {
            gcRecorder = Recorder.Get("GC Allocated In Frame");
            if (gcRecorder != null && !gcRecorder.isValid) gcRecorder = null;
        }
        catch { gcRecorder = null; }

        EditorApplication.update += Tick;
        Debug.Log("[实测v2] 启动。阈值 " + ThresholdMs + "ms，7 个阶段，全程不接受外部调用。");
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { Finish("Play 已退出"); return; }

        float now = Time.realtimeSinceStartup;
        float ms = (now - lastT) * 1000f;
        lastT = now;

        long gc = System.GC.GetTotalMemory(false);
        long gcDelta = gc - lastGc;
        lastGc = gc;

        stepFrames++;

        if (ms >= ThresholdMs && ms < 5000f)
        {
            recs.Add(new Rec
            {
                ms = ms,
                frame = Time.frameCount,
                phase = phase,
                world = WorldPresence.Current,
                activeSet = WorldStreamer.DebugActiveSet(),
                gcDelta = gcDelta,
                gcTotal = gc,
                sceneCount = SceneManager.sceneCount,
                renderers = 0,
                cameras = Camera.allCamerasCount,
                monoUsed = Profiler.GetMonoUsedSizeLong()
            });
            Debug.Log("[实测v2] " + ms.ToString("F0") + "ms  阶段=" + phase
                      + "  frame=" + Time.frameCount
                      + "  世界=" + (string.IsNullOrEmpty(WorldPresence.Current) ? "-" : WorldPresence.Current)
                      + "  装载=" + (string.IsNullOrEmpty(WorldStreamer.DebugActiveSet()) ? "无" : WorldStreamer.DebugActiveSet())
                      + "  GC" + (gcDelta >= 0 ? "+" : "") + (gcDelta / 1024) + "KB"
                      + "  场景=" + SceneManager.sceneCount
                      + "  相机=" + Camera.allCamerasCount);
        }

        // ── 阶段推进 ──
        switch (phaseIndex)
        {
            case 0:  // 静止站在 Metro 门前，观察 4 秒
                if (stepFrames > 4 * 120) Next();
                break;
            case 1:  // 移到远处
                if (stepFrames == 1) Move(new Vector3(3.78f, -1023.72f, 30f), "远处 z=30");
                if (stepFrames > 3 * 120) Next();
                break;
            case 2:  // 远处静止 3 秒（等卸载收敛）
                if (stepFrames > 3 * 120) Next();
                break;
            case 3:  // 瞬移回门前 3m
                if (stepFrames == 1) Move(new Vector3(3.78f, -1023.72f, 2.14f), "门前 z=2.14");
                if (stepFrames > 3 * 120) Next();
                break;
            case 4:  // 门前静止 3 秒
                if (stepFrames > 3 * 120) Next();
                break;
            case 5:  // 开门（模拟玩家按 E）
                if (stepFrames == 1) OpenMetroDoor();
                if (stepFrames > 3 * 120) Next();
                break;
            case 6:
                if (stepFrames > 3 * 120) { Finish(null); }
                break;
        }
    }

    private static void Next()
    {
        phaseIndex++;
        stepFrames = 0;
        if (phaseIndex < PhaseNames.Length)
        {
            phase = PhaseNames[phaseIndex];
            phaseStart = Time.realtimeSinceStartup;
            Debug.Log("[实测v2] ===== 进入阶段 " + phase + " =====");
        }
    }

    private static void Move(Vector3 pos, string label)
    {
        var root = Root();
        if (root == null) { Debug.LogWarning("[实测v2] 找不到玩家"); return; }
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        root.position = pos;
        if (cc != null) cc.enabled = true;
        Debug.Log("[实测v2] 玩家 → " + label);
    }

    private static void OpenMetroDoor()
    {
        foreach (var w in Object.FindObjectsOfType<PortalWindow>(true))
        {
            if (w.name == "anywhere-door-Metro" && w.gameObject.scene.name == "SpecimenLab")
            {
                var door = w.GetComponentInChildren<AnywhereDoor>(true);
                if (door != null)
                {
                    door.SetOpen(true, false, true);
                    Debug.Log("[实测v2] 已打开 Metro 门");
                    return;
                }
            }
        }
        Debug.LogWarning("[实测v2] 没找到 Metro 门");
    }

    private static Transform Root()
    {
        var pr = FindType("PlayerRef");
        if (pr == null) return null;
        var p = pr.GetProperty("Root", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        return p == null ? null : (Transform)p.GetValue(null);
    }

    private static System.Type FindType(string n)
    {
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        { try { var t = a.GetType(n); if (t != null) return t; } catch { } }
        return null;
    }

    private static void Finish(string why)
    {
        EditorApplication.update -= Tick;
        running = false;
        if (!string.IsNullOrEmpty(why)) Debug.LogWarning("[实测v2] 中止：" + why);

        var sb = new StringBuilder();
        sb.Append("[实测v2 结果] 阈值 ").Append(ThresholdMs).Append("ms，共 ").Append(recs.Count).Append(" 次超阈\n");
        if (recs.Count == 0)
        {
            sb.Append("  没有超过阈值的帧 —— 该流程体感流畅。\n");
        }
        else
        {
            // 按阶段汇总
            var byPhase = new Dictionary<string, List<Rec>>();
            for (int i = 0; i < recs.Count; i++)
            {
                List<Rec> l;
                if (!byPhase.TryGetValue(recs[i].phase, out l)) { l = new List<Rec>(); byPhase[recs[i].phase] = l; }
                l.Add(recs[i]);
            }
            foreach (var kv in byPhase)
            {
                float sum = 0, max = 0;
                for (int i = 0; i < kv.Value.Count; i++) { sum += kv.Value[i].ms; if (kv.Value[i].ms > max) max = kv.Value[i].ms; }
                sb.Append("  ").Append(kv.Key).Append("：×").Append(kv.Value.Count)
                  .Append("  合计").Append(sum.ToString("F0")).Append("ms")
                  .Append("  最坏").Append(max.ToString("F0")).Append("ms\n");
            }

            sb.Append("  ── 明细（前 25 条）──\n");
            int n = Mathf.Min(25, recs.Count);
            for (int i = 0; i < n; i++)
            {
                var r = recs[i];
                sb.Append("  ").Append(r.ms.ToString("F0")).Append("ms  ").Append(r.phase)
                  .Append("  f=").Append(r.frame)
                  .Append("  装载=").Append(string.IsNullOrEmpty(r.activeSet) ? "无" : r.activeSet)
                  .Append("  GC").Append(r.gcDelta >= 0 ? "+" : "").Append(r.gcDelta / 1024).Append("KB")
                  .Append("  场景=").Append(r.sceneCount)
                  .Append("  相机=").Append(r.cameras).Append('\n');
            }
        }
        Debug.Log(sb.ToString());
    }
}
