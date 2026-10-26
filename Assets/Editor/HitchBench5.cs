using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 穿门卡顿实测 v5：**只测"传送到 Metro 那一刻"**，不做摆位模拟。
///
/// 与 v3/v4 的区别：不再试图把玩家一步步走进门里（那会和 PlayerMovement 抢位置），
/// 而是直接复刻穿门成功时的两步真实动作——
///   1) <c>PlayerRef.Teleport(落点, 朝向)</c>
///   2) <c>WorldStreamer.NotifyCrossed("Metro")</c>
/// 这两步之后玩家就已经站在 Metro 里了，接下来几帧的开销正是玩家抱怨的
/// 「传送到 metro 时不能操作的时间」。
///
/// 用法：进 Play → 菜单 <c>FYP/诊断/穿门卡顿实测v5</c> → 等 10 秒看 Console。
/// 全程用 EditorApplication.update 推进，不在测量中途调 MCP（否则会引入
/// 300~500ms 的工具帧假尖峰）。
/// </summary>
public static class HitchBench5
{
    private const float ThresholdMs = 50f;
    private const int BaselineFrames = 20;    // 穿门前先量一段基线
    private const int ObserveFrames = 150;    // 穿门后观察多久

    private static int stage;
    private static int frameInStage;
    private static int startFrame;
    private static readonly List<float> baseline = new List<float>();
    private static readonly List<float> after = new List<float>();
    private static readonly List<string> notes = new List<string>();
    private static readonly StringBuilder log = new StringBuilder();

    [MenuItem("FYP/诊断/穿门卡顿实测v5")]
    public static void Run()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[穿门v5] 需要先进 Play 模式。");
            return;
        }
        stage = 0;
        frameInStage = 0;
        baseline.Clear();
        after.Clear();
        notes.Clear();
        log.Length = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[穿门v5] 已启动，等待开局部署完成……");
    }

    private static void Stop(string reason)
    {
        EditorApplication.update -= Tick;
        if (!string.IsNullOrEmpty(reason)) notes.Add(reason);
        Report();
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { Stop("Play 已退出"); return; }
        frameInStage++;

        switch (stage)
        {
            case 0: // 等玩家与世界系统就绪
                if (PlayerRef.Root != null && WorldStreamer.PreloadingReady && frameInStage > 60)
                {
                    notes.Add("开局就绪，场景数=" + UnityEngine.SceneManagement.SceneManager.sceneCount);
                    stage = 1; frameInStage = 0;
                }
                else if (frameInStage > 3000) Stop("等就绪超时");
                break;

            case 1: // 基线
                baseline.Add(Time.unscaledDeltaTime * 1000f);
                if (baseline.Count >= BaselineFrames) { stage = 2; frameInStage = 0; }
                break;

            case 2: // 执行穿门
                DoTeleport();
                stage = 3; frameInStage = 0;
                break;

            case 3: // 观察
                after.Add(Time.unscaledDeltaTime * 1000f);
                if (after.Count >= ObserveFrames) Stop(null);
                break;
        }
    }

    private static void DoTeleport()
    {
        var win = FindWindow("SpecimenLab", "anywhere-door-Metro");
        if (win == null) { notes.Add("找不到 SpecimenLab/anywhere-door-Metro"); return; }

        // 落点：Metro 侧那扇门（配对门）的门槽位置
        var other = FindWindow("Metro", "anywhere-door-Metro-A");
        Transform src = GetTransform(win, "srcFrame");
        Vector3 dest;
        Quaternion facing;
        if (other != null)
        {
            Transform df = GetTransform(other, "srcFrame");
            dest = df.position - df.forward * 0.02f;
            facing = df.rotation;
        }
        else if (src != null)
        {
            dest = src.position + Vector3.up * 1f;
            facing = src.rotation;
        }
        else { notes.Add("找不到门 Transform"); return; }

        startFrame = Time.frameCount;
        log.Append("穿门前：世界=").Append(WorldPresence.Current)
           .Append(" 装载=").Append(WorldStreamer.DebugActiveSet())
           .Append(" 玩家=").Append(PlayerRef.Root.position.ToString("F2")).Append("\n");

        PlayerRef.Teleport(dest, facing);
        WorldStreamer.NotifyCrossed("Metro");

        log.Append("穿门后（同帧）：世界=").Append(WorldPresence.Current)
           .Append(" 玩家=").Append(PlayerRef.Root.position.ToString("F2")).Append("\n");
    }

    private static PortalWindow FindWindow(string scene, string doorName)
    {
        foreach (var w in Object.FindObjectsOfType<PortalWindow>(true))
        {
            if (w == null) continue;
            Transform f = GetTransform(w, "srcFrame");
            if (f == null) continue;
            if (f.gameObject.scene.name == scene && w.gameObject.name == doorName) return w;
        }
        return null;
    }

    private static Transform GetTransform(object o, string field)
    {
        var f = o.GetType().GetField(field,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        return f != null ? f.GetValue(o) as Transform : null;
    }

    private static void Report()
    {
        var sb = new StringBuilder();
        sb.Append("[穿门v5] 结果\n").Append(log);

        sb.Append("穿门前基线：");
        AppendStats(sb, baseline);
        sb.Append("穿门后 ").Append(after.Count).Append(" 帧：");
        AppendStats(sb, after);

        // 前 12 帧逐帧列出，方便看衰减曲线
        sb.Append("穿门后逐帧(ms)：");
        for (int i = 0; i < after.Count && i < 12; i++)
            sb.Append(" f+").Append(i + 1).Append("=").Append(after[i].ToString("F0"));
        sb.Append("\n");

        int over = 0;
        float worst = 0f;
        int worstIdx = -1;
        for (int i = 0; i < after.Count; i++)
        {
            if (after[i] > ThresholdMs) over++;
            if (after[i] > worst) { worst = after[i]; worstIdx = i; }
        }
        sb.Append("超 ").Append(ThresholdMs).Append("ms 的帧：").Append(over).Append(" 个");
        if (worstIdx >= 0) sb.Append("，最重在 f+").Append(worstIdx + 1).Append(" = ").Append(worst.ToString("F0")).Append("ms");
        sb.Append("\n");

        sb.Append("结束时：世界=").Append(WorldPresence.Current)
          .Append(" 装载=").Append(WorldStreamer.DebugActiveSet())
          .Append(" 场景数=").Append(UnityEngine.SceneManagement.SceneManager.sceneCount).Append("\n");

        foreach (var n in notes) sb.Append("注：").Append(n).Append("\n");
        Debug.Log(sb.ToString());
    }

    private static void AppendStats(StringBuilder sb, List<float> list)
    {
        if (list.Count == 0) { sb.Append("（无数据）\n"); return; }
        float sum = 0f, max = 0f;
        foreach (var v in list) { sum += v; if (v > max) max = v; }
        sb.Append("平均 ").Append((sum / list.Count).ToString("F1")).Append("ms，最大 ")
          .Append(max.ToString("F1")).Append("ms（").Append(list.Count).Append(" 帧）\n");
    }
}
