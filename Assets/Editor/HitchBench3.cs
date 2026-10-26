using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 穿门卡顿实测（v3）——专测「传送那一刻」。
///
/// 为什么另起一套：v2 只测「走到门前」的取景首次渲染，那部分已修好。
/// 玩家反馈「传送到 Metro 时仍不能操作」是**穿门那一瞬**，走的是另一条路径：
///   PortalWindow.TryCross() → PlayerRef.Teleport() → WorldStreamer.NotifyCrossed()
///   → 下一帧 Tick 重算邻居（PreloadHops=1，从 Metro 出发会带出 96.5MB 的 Shrine）
///
/// 测量纪律（踩过的坑）：绝不在测量期间调用 MCP execute_code——它自身 Roslyn 编译
/// 会造 300~500ms 假尖峰。全程由 EditorApplication.update 推进，测完只读一次 Console。
///
/// 穿门驱动方式：把玩家放在门前，按 0.25m 步长逼近，每步手动调用一次 LateUpdate
/// （先把 crossCooldown 置 -1 绕过冷却），这与真实走路穿门走的是同一条代码路径。
/// </summary>
public static class HitchBench3
{
    private const float ThresholdMs = 50f;

    private struct StepRec
    {
        public int n;
        public float ms;
        public float s;
        public bool crossed;
    }

    private static readonly List<StepRec> steps = new List<StepRec>();
    private static readonly List<string> notes = new List<string>();

    private static int phase;
    private static int waitFrames;
    private static Transform root;
    private static Transform srcFrame;
    private static PortalWindow window;
    private static AnywhereDoor door;

    private static Type winType;
    private static MethodInfo lateUpdateMethod;
    private static FieldInfo fCooldown;
    private static FieldInfo fReady;
    private static FieldInfo fSrcFrame;

    private static float baseStart;
    private static float lastRealtime;
    private static bool teleportSeen;

    [MenuItem("FYP/诊断/穿门卡顿实测v3")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[穿门v3] 请先进入 Play 模式");
            return;
        }

        steps.Clear();
        notes.Clear();
        phase = 0;
        waitFrames = 0;
        teleportSeen = false;

        root = PlayerRef.Root;
        if (root == null)
        {
            Debug.LogError("[穿门v3] PlayerRef.Root 为空——域重载后未修复，先跑 snippets/repair_singletons.cs");
            return;
        }

        window = null;
        PortalWindow[] all = UnityEngine.Object.FindObjectsOfType<PortalWindow>(true);
        for (int i = 0; i < all.Length; i++)
        {
            PortalWindow w = all[i];
            if (w == null) continue;
            if (w.gameObject.scene.name == "SpecimenLab" && w.TargetWorldName == "Metro") { window = w; break; }
        }
        if (window == null)
        {
            Debug.LogError("[穿门v3] 找不到 SpecimenLab/anywhere-door-Metro");
            return;
        }

        winType = typeof(PortalWindow);
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance;
        lateUpdateMethod = winType.GetMethod("LateUpdate", BF);
        fCooldown = winType.GetField("cooldown", BF);
        fReady = winType.GetField("ready", BF);
        fSrcFrame = winType.GetField("srcFrame", BF);
        if (fSrcFrame != null) srcFrame = fSrcFrame.GetValue(window) as Transform;
        door = window.GetComponentInChildren<AnywhereDoor>(true);

        baseStart = Time.realtimeSinceStartup;
        lastRealtime = baseStart;

        notes.Add("阈值 " + ThresholdMs + "ms｜门=" + window.name
                  + "｜srcFrame=" + (srcFrame != null ? srcFrame.name : "NULL")
                  + "｜LateUpdate=" + (lateUpdateMethod != null ? "ok" : "NULL")
                  + "｜cooldown 字段=" + (fCooldown != null ? "ok" : "NULL"));

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Note(string s) { notes.Add(s); }

    private static void Next(int p) { phase = p; waitFrames = 0; }

    private static bool Ready()
    {
        if (window == null || fReady == null) return false;
        try { return (bool)fReady.GetValue(window); } catch { return false; }
    }

    private static float SignedDistance()
    {
        if (root == null || srcFrame == null) return 0f;
        return Vector3.Dot(root.position - srcFrame.position, srcFrame.forward);
    }

    private static void MoveTo(float s)
    {
        if (root == null || srcFrame == null) return;
        // 必须站在门洞中轴线上（横向偏移 0）：TryCross 的 IsInsideOpening 要求玩家
        // 真的在门洞里，贴着门框侧面蹭过去不算穿门。保留出生点那 2.79m 的横向偏移
        // 会让判定永远失败——第一版就栽在这里（11 步走完都没穿过去）。
        Vector3 target = srcFrame.position + srcFrame.forward * s;
        CharacterController cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        root.position = target;
        if (cc != null) cc.enabled = true;
    }

    private static string Join(IEnumerable<string> xs)
    {
        if (xs == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (string x in xs) { if (sb.Length > 0) sb.Append(","); sb.Append(x); }
        return sb.ToString();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }

        float now = Time.realtimeSinceStartup;
        float frameMs = (now - lastRealtime) * 1000f;
        lastRealtime = now;

        if (phase >= 1 && frameMs > ThresholdMs && frameMs < 3000f)
            Note("  [帧] " + frameMs.ToString("F0") + "ms  阶段=" + phase + " frame=" + Time.frameCount
                 + " 世界=" + WorldPresence.Current + " 装载=" + WorldStreamer.DebugActiveSet());

        if (root != null && !teleportSeen && WorldPresence.Current == "Metro")
        {
            teleportSeen = true;
            Note("  [穿门已发生] frame=" + Time.frameCount + " 玩家=" + root.position.ToString("F2")
                 + " 邻居=" + Join(WorldStreamer.PreloadWindow) + " 装载=" + WorldStreamer.DebugActiveSet());
        }

        switch (phase)
        {
            case 0:
                waitFrames++;
                if (Ready() || waitFrames > 900)
                {
                    Note("阶段0 门就绪=" + Ready() + "（等 " + waitFrames + " 帧）");
                    Next(1);
                }
                break;

            case 1:
                // TryCross 里有「门关着不许穿」的判定，必须先开门
                if (door != null)
                {
                    door.SetOpen(true, false, false);
                    Note("阶段1 已开门 IsOpen=" + door.IsOpen);
                }
                MoveTo(-1.6f);
                Note("阶段1 玩家 → 门前 1.6m，s=" + SignedDistance().ToString("F2"));
                Next(2);
                break;

            case 2:
                {
                    // 门开有挥法前摇（spellWindup≈1.2s），IsOpen 立刻为真但门板还没转开。
                    // 等 90 帧再开始逼近，避免把开门动画的开销算进穿门测量。
                    waitFrames++;
                    if (waitFrames < 90) break;

                    float s = SignedDistance();
                    if (teleportSeen || s > 1.0f) { Next(3); break; }

                    float from = s;
                    float to = s + 0.25f;
                    MoveTo(to);
                    if (fCooldown != null) fCooldown.SetValue(window, -1f);

                    float t0 = Time.realtimeSinceStartup;
                    if (lateUpdateMethod != null)
                    {
                        try { lateUpdateMethod.Invoke(window, null); }
                        catch (Exception e)
                        {
                            Exception inner = e.InnerException;
                            Note("  !LateUpdate 异常：" + (inner != null ? inner.Message : e.Message));
                        }
                    }
                    float ms = (Time.realtimeSinceStartup - t0) * 1000f;

                    bool crossed = WorldPresence.Current == "Metro";
                    steps.Add(new StepRec { n = steps.Count, ms = ms, s = to, crossed = crossed });

                    if (ms > ThresholdMs)
                        Note("  [握手] s=" + from.ToString("F2") + "→" + to.ToString("F2")
                             + " 耗时 " + ms.ToString("F0") + "ms  crossed=" + crossed
                             + " 装载=" + WorldStreamer.DebugActiveSet());

                    if (crossed)
                    {
                        Note("阶段2 穿门成功 @ s=" + to.ToString("F2"));
                        Next(3);
                    }
                }
                break;

            case 3:
                waitFrames++;
                if (waitFrames > 120) Next(4);
                break;

            case 4:
                Finish();
                break;
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;

        StringBuilder sb = new StringBuilder();
        sb.Append("[穿门v3 结果] 阈值 ").Append(ThresholdMs).Append("ms｜总耗时 ")
          .Append((Time.realtimeSinceStartup - baseStart).ToString("F1")).Append("s\n");

        float worst = 0f, total = 0f;
        for (int i = 0; i < steps.Count; i++)
        {
            total += steps[i].ms;
            if (steps[i].ms > worst) worst = steps[i].ms;
        }
        sb.Append("逼近共 ").Append(steps.Count).Append(" 步，合计 ").Append(total.ToString("F0"))
          .Append("ms，最重一步 ").Append(worst.ToString("F0")).Append("ms\n");

        for (int i = 0; i < steps.Count; i++)
            if (steps[i].ms > ThresholdMs)
                sb.Append("  重步 #").Append(steps[i].n).Append(" s=").Append(steps[i].s.ToString("F2"))
                  .Append(" → ").Append(steps[i].ms.ToString("F0")).Append("ms\n");

        sb.Append("最终世界=").Append(WorldPresence.Current)
          .Append(" 玩家=").Append(root != null ? root.position.ToString("F2") : "NULL")
          .Append("\n邻居=").Append(Join(WorldStreamer.PreloadWindow))
          .Append("\n装载=").Append(WorldStreamer.DebugActiveSet())
          .Append("\n字体预热=").Append(CrtFontPrewarm.LastReport ?? "(未执行)")
          .Append("\n── 明细 ──\n");

        for (int i = 0; i < notes.Count; i++) sb.Append(notes[i]).Append("\n");

        Debug.Log(sb.ToString());
    }
}
