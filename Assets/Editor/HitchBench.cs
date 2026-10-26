using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 装载卡顿实测驱动器（编辑器专用）。
///
/// 为什么要"在游戏内自己走完整个流程"：每次从外部（MCP execute_code）调用 Unity 都要
/// Roslyn 编译代码，会造出 300~500ms 的**假帧尖峰**。实测：连续调用期间每次都有
/// 350~500ms 尖峰，而完全不调用 Unity 静置 25 秒一次都不出现——那些全是测量工具自己造成的。
///
/// 所以本工具由 EditorApplication.update 单步推进，全程不接收外部调用，
/// 只在最后把结论写进 Console，供人一次性读走。
///
/// 菜单：FYP/诊断/装载卡顿实测
/// </summary>
public static class HitchBench
{
    private enum Step
    {
        WaitReady,     // 等开局部署完成（WorldStreamer 初始化 + 玩家可动）
        Clear,         // 清空看门狗记录
        Park,          // 把玩家放到远离所有门的位置，等世界卸载
        Approach,      // 把玩家移到 Metro 门前 —— 触发"第一次靠近"
        Settle,        // 静置观察
        Report,        // 输出结论
        Done
    }

    private static Step step = Step.WaitReady;
    private static int framesInStep;
    private static int approachFrames;      // 从 Approach 开始记的帧数
    private static bool running;

    [MenuItem("FYP/诊断/装载卡顿实测")]
    public static void StartBench()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[卡顿实测] 必须先进入 Play 模式");
            return;
        }
        if (running) { Debug.LogWarning("[卡顿实测] 已经在跑了"); return; }

        step = Step.WaitReady;
        framesInStep = 0;
        running = true;
        EditorApplication.update += Tick;
        Debug.Log("[卡顿实测] 已启动：等待开局部署完成后，自动走一遍「远离门 → 走到 Metro 门前」。"
                  + "整个过程不接收外部调用，测出的帧时长是玩家真实体感。");
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { Stop("Play 模式已退出"); return; }

        framesInStep++;

        switch (step)
        {
            case Step.WaitReady:
                // 等开局部署把 PreloadWindow 激活完、玩家解锁
                if (WorldStreamer.PreloadingReady && !PlayerInputGate.Locked && Root() != null)
                {
                    Debug.Log("[卡顿实测] 开局就绪，窗口=" + string.Join(",", WorldStreamer.PreloadWindow)
                              + "  已加载=" + LoadedScenes());
                    Go(Step.Clear);
                }
                else if (framesInStep > 60 * 60) Stop("等待开局就绪超时（60s）");
                break;

            case Step.Clear:
                if (framesInStep == 1)
                {
                    var wd = FrameHitchWatchdog.Instance;
                    if (wd != null) { wd.ClearRecords(); }
                    var wdType = typeof(FrameHitchWatchdog);
                    wdType.GetMethod("Suppress", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                          .Invoke(null, new object[] { 3 });
                    Debug.Log("[卡顿实测] 记录已清空，准备把玩家移到远处");
                }
                if (framesInStep >= 3) Go(Step.Park);
                break;

            case Step.Park:
                if (framesInStep == 1) Teleport(new Vector3(3.78f, -1023.72f, 26f), "远处 z=26（离 Metro 门 ~21m）");
                // 给世界卸载/状态机收敛留时间
                if (framesInStep >= 90) Go(Step.Approach);
                break;

            case Step.Approach:
                if (framesInStep == 1)
                {
                    Debug.Log("[卡顿实测] >>> 现在把玩家瞬移到 Metro 门前 3m（z=2.14），这一帧起记录卡顿 <<<");
                    Teleport(new Vector3(3.78f, -1023.72f, 2.14f), "Metro 门前 3m");
                }
                approachFrames = framesInStep;
                if (framesInStep >= 240) Go(Step.Settle);   // 约 2 秒 @120fps
                break;

            case Step.Settle:
                if (framesInStep >= 180) Go(Step.Report);
                break;

            case Step.Report:
                DumpReport();
                Go(Step.Done);
                break;

            case Step.Done:
                Stop(null);
                break;
        }
    }

    private static void Go(Step next) { step = next; framesInStep = 0; }

    private static void Stop(string why)
    {
        EditorApplication.update -= Tick;
        running = false;
        if (!string.IsNullOrEmpty(why)) Debug.LogWarning("[卡顿实测] 中止：" + why);
        else Debug.Log("[卡顿实测] 结束。");
    }

    private static Transform Root()
    {
        var pr = FindType("PlayerRef");
        if (pr == null) return null;
        var p = pr.GetProperty("Root", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        return p == null ? null : (Transform)p.GetValue(null);
    }

    private static void Teleport(Vector3 pos, string label)
    {
        var root = Root();
        if (root == null) { Debug.LogWarning("[卡顿实测] 找不到玩家，跳过传送"); return; }
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        root.position = pos;
        if (cc != null) cc.enabled = true;
        Debug.Log("[卡顿实测] 玩家 → " + label);
    }

    private static string LoadedScenes()
    {
        var list = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++) list.Add(SceneManager.GetSceneAt(i).name);
        return string.Join(",", list);
    }

    private static void DumpReport()
    {
        var sb = new StringBuilder();
        sb.Append("[卡顿实测结果] ===== 进入 Metro 的真实卡顿 =====\n");
        sb.Append("  已加载世界：").Append(LoadedScenes()).Append('\n');
        sb.Append("  预载窗口：").Append(string.Join(",", WorldStreamer.PreloadWindow)).Append('\n');
        sb.Append("  当前所在：").Append(string.IsNullOrEmpty(WorldPresence.Current) ? "(未知)" : WorldPresence.Current).Append('\n');
        sb.Append("  接近阶段帧数：").Append(approachFrames).Append('\n');

        var root = Root();
        if (root != null) sb.Append("  玩家位置：").Append(root.position.ToString("F2")).Append('\n');

        // 各门就绪状态
        sb.Append("  门：\n");
        foreach (var w in Object.FindObjectsOfType<PortalWindow>(true))
        {
            var rf = typeof(PortalWindow).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool ready = rf != null && (bool)rf.GetValue(w);
            sb.Append("    ").Append(w.gameObject.scene.name).Append('/').Append(w.name)
              .Append(" target=").Append(w.TargetWorldName)
              .Append(" ready=").Append(ready).Append('\n');
        }

        var wd = FrameHitchWatchdog.Instance;
        if (wd != null)
        {
            var type = wd.GetType();
            sb.Append("  看门狗阈值：").Append(type.GetField("ThresholdMs").GetValue(wd)).Append("ms\n");
            var dump = type.GetMethod("DumpSummary", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            // 让看门狗自己往 Console 里打一份分组的（读起来更方便）
            dump.Invoke(wd, null);
        }
        else sb.Append("  看门狗：未找到（本轮没记录到数据）\n");

        Debug.Log(sb.ToString());
    }

    private static System.Type FindType(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var t = asm.GetType(name); if (t != null) return t; } catch { }
        }
        return null;
    }
}
