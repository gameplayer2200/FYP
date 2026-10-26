using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 卡顿归因实验（编辑器专用）：在同一 Play 会话里重复同一段动作 3 轮。
///
/// 要回答的问题：Metro 门前那 400~500ms 到底是
///   A) 首次渲染时的运行时着色器编译 —— 那第 2、3 轮应该明显变快（编译结果被缓存）
///   B) 世界重装载 / 场景实例化        —— 那每轮都会一样慢
///   C) 一次性初始化（缓存/图构建等）  —— 第 2 轮起消失
///
/// 每轮动作完全一致：从门前 3m 移到远处 30m → 停 3s（让世界卸载）→ 移回门前 3m → 停 3s。
/// 全程由 EditorApplication.update 单步推进，不接收外部调用（外部调用会造 300~500ms 假尖峰）。
///
/// 菜单：FYP/诊断/卡顿归因（重复3轮）
/// </summary>
public static class HitchAttribution
{
    private const float ThresholdMs = 40f;
    private const int RoundCount = 3;

    private static readonly float[] roundWorst = new float[RoundCount];
    private static readonly int[] roundCount = new int[RoundCount];
    private static readonly List<string> detail = new List<string>();

    private static bool running;
    private static int round;          // 0..RoundCount-1
    private static int stage;          // 0=远离 1=远处静止 2=移回 3=门前静止
    private static int frames;
    private static float lastT;

    [MenuItem("FYP/诊断/卡顿归因（重复3轮）")]
    public static void Start()
    {
        if (!Application.isPlaying) { Debug.LogError("[归因] 需要 Play 模式"); return; }
        if (running) { Debug.LogWarning("[归因] 正在跑"); return; }

        for (int i = 0; i < RoundCount; i++) { roundWorst[i] = 0f; roundCount[i] = 0; }
        detail.Clear();

        running = true;
        round = 0; stage = 0; frames = 0;
        lastT = Time.realtimeSinceStartup;

        EditorApplication.update += Tick;
        Debug.Log("[归因] 启动：重复 " + RoundCount + " 轮完全相同的「移远→移回门前」，阈值 " + ThresholdMs + "ms。");
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { Finish("Play 退出"); return; }

        float now = Time.realtimeSinceStartup;
        float ms = (now - lastT) * 1000f;
        lastT = now;
        frames++;

        if (ms >= ThresholdMs && ms < 5000f)
        {
            roundWorst[round] = Mathf.Max(roundWorst[round], ms);
            roundCount[round]++;
            string d = "  第" + (round + 1) + "轮 " + ms.ToString("F0") + "ms"
                       + "  stage=" + stage
                       + "  f=" + Time.frameCount
                       + "  场景=" + SceneManager.sceneCount
                       + "  装载=" + (string.IsNullOrEmpty(WorldStreamer.DebugActiveSet()) ? "无" : WorldStreamer.DebugActiveSet());
            if (detail.Count < 200) detail.Add(d);
            Debug.Log("[归因]" + d);
        }

        switch (stage)
        {
            case 0:  // 移到远处
                if (frames == 1) Move(new Vector3(3.78f, -1023.72f, 30f));
                if (frames > 10) { stage = 1; frames = 0; }
                break;
            case 1:  // 远处停 3 秒
                if (frames > 3 * 120) { stage = 2; frames = 0; }
                break;
            case 2:  // 移回门前
                if (frames == 1) Move(new Vector3(3.78f, -1023.72f, 2.14f));
                if (frames > 10) { stage = 3; frames = 0; }
                break;
            case 3:  // 门前停 3 秒
                if (frames > 3 * 120)
                {
                    Debug.Log("[归因] 第 " + (round + 1) + " 轮完成，最坏 "
                              + roundWorst[round].ToString("F0") + "ms，超阈 " + roundCount[round] + " 次，"
                              + "当前场景数=" + SceneManager.sceneCount);
                    round++;
                    frames = 0; stage = 0;
                    if (round >= RoundCount) Finish(null);
                }
                break;
        }
    }

    private static void Move(Vector3 pos)
    {
        var pr = FindType("PlayerRef");
        if (pr == null) return;
        var root = (Transform)pr.GetProperty("Root", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null);
        if (root == null) return;
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        root.position = pos;
        if (cc != null) cc.enabled = true;
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
        if (!string.IsNullOrEmpty(why)) Debug.LogWarning("[归因] 中止：" + why);

        var sb = new StringBuilder();
        sb.Append("[归因结果] 同一 Play 会话内重复 ").Append(RoundCount).Append(" 轮相同动作\n");
        for (int i = 0; i < RoundCount; i++)
        {
            sb.Append("  第").Append(i + 1).Append("轮：最坏 ")
              .Append(roundWorst[i].ToString("F0")).Append("ms，超阈 ")
              .Append(roundCount[i]).Append(" 次\n");
        }
        sb.Append('\n');
        float first = roundWorst[0], last = roundWorst[RoundCount - 1];
        if (first > 100f && last < first * 0.35f)
            sb.Append("→ 结论：第 1 轮慢、后续明显变快 = **一次性初始化**（着色器编译 / 缓存构建）。\n");
        else if (first > 100f && last > first * 0.7f)
            sb.Append("→ 结论：每轮都一样慢 = **世界重装载 / 场景实例化**，无法靠预热消除，只能靠「不卸载」。\n");
        else
            sb.Append("→ 结论：各轮都不显著（首轮 ").Append(first.ToString("F0")).Append("ms，末轮 ")
              .Append(last.ToString("F0")).Append("ms）。\n");
        Debug.Log(sb.ToString());
    }
}
