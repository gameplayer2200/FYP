using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 穿门卡顿实测（v4）——真正驱动一次完整穿门，并记录穿门后一段时间的逐帧开销。
///
/// 相对 v3 的两处修正：
///   1) **关掉 PlayerMovement**。v3 手动摆位后，PlayerMovement 每帧用 CharacterController
///      把玩家拉回原位，导致 s 永远停在 -1.52、11 步走完都没穿过去。
///   2) **用 Time.unscaledDeltaTime 判帧长**，不再用 realtimeSinceStartup 的间隔——
///      后者量到的是编辑器 tick 间隔，会把正常的 50~70ms 抖动误报成尖峰。
///
/// 关注点：穿门后 1-hop 邻居（Metro → Shrine，96.5MB）会被拉进预载窗口并激活，
/// 激活瞬间要实例化整场景，这很可能就是玩家感到"不能操作"的那一下。
/// </summary>
public static class HitchBench4
{
    private const float HitchMs = 60f;

    private static int phase;
    private static int waitFrames;
    private static int observeFrames;

    private static Transform root;
    private static Transform srcFrame;
    private static PortalWindow window;
    private static AnywhereDoor door;
    private static readonly List<MonoBehaviour> parkedMovement = new List<MonoBehaviour>();

    private static MethodInfo lateUpdateMethod;
    private static FieldInfo fCooldown;

    private static readonly List<string> notes = new List<string>();
    private static readonly List<float> postFrames = new List<float>();
    private static bool crossed;

    private static float baseStart;

    [MenuItem("FYP/诊断/穿门卡顿实测v4")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[穿门v4] 请先进入 Play 模式");
            return;
        }

        notes.Clear();
        postFrames.Clear();
        parkedMovement.Clear();
        phase = 0;
        waitFrames = 0;
        observeFrames = 0;
        crossed = false;
        baseStart = Time.realtimeSinceStartup;

        root = PlayerRef.Root;
        if (root == null)
        {
            Debug.LogError("[穿门v4] PlayerRef.Root 为空——先跑 snippets/repair_singletons.cs");
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
            Debug.LogError("[穿门v4] 找不到 SpecimenLab/anywhere-door-Metro");
            return;
        }

        Type winType = typeof(PortalWindow);
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance;
        lateUpdateMethod = winType.GetMethod("LateUpdate", BF);
        fCooldown = winType.GetField("cooldown", BF);
        FieldInfo fSrc = winType.GetField("srcFrame", BF);
        if (fSrc != null) srcFrame = fSrc.GetValue(window) as Transform;
        door = window.GetComponentInChildren<AnywhereDoor>(true);

        // 关键：把所有 PlayerMovement 停掉，否则它每帧把玩家拉回原位，手动摆位无效。
        PlayerMovement[] pms = UnityEngine.Object.FindObjectsOfType<PlayerMovement>(true);
        for (int i = 0; i < pms.Length; i++)
        {
            if (pms[i] == null) continue;
            pms[i].enabled = false;
            parkedMovement.Add(pms[i]);
        }

        notes.Add("阈值 " + HitchMs + "ms｜门=" + window.name
                  + "｜srcFrame=" + (srcFrame != null ? srcFrame.name : "NULL")
                  + "｜已停用 PlayerMovement=" + parkedMovement.Count
                  + "｜LateUpdate=" + (lateUpdateMethod != null ? "ok" : "NULL"));

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Note(string s) { notes.Add(s); }

    private static void Next(int p) { phase = p; waitFrames = 0; }

    private static float SignedDistance()
    {
        if (root == null || srcFrame == null) return 0f;
        return Vector3.Dot(root.position - srcFrame.position, srcFrame.forward);
    }

    private static void MoveTo(float s)
    {
        if (root == null || srcFrame == null) return;
        Vector3 target = srcFrame.position + srcFrame.forward * s;
        CharacterController cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        root.position = target;
        if (cc != null) cc.enabled = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }

        float frameMs = Time.unscaledDeltaTime * 1000f;

        if (phase >= 2 && frameMs > HitchMs && frameMs < 5000f)
            Note("  [帧] " + frameMs.ToString("F0") + "ms  阶段=" + phase
                 + " frame=" + Time.frameCount + " 世界=" + WorldPresence.Current
                 + " 装载=" + WorldStreamer.DebugActiveSet());

        switch (phase)
        {
            case 0:
                waitFrames++;
                if (IsReady() || waitFrames > 900)
                {
                    Note("阶段0 取景就绪=" + IsReady() + "（等 " + waitFrames + " 帧）");
                    if (door != null)
                    {
                        door.SetOpen(true, false, false);
                        Note("阶段0 已开门 IsOpen=" + door.IsOpen);
                    }
                    MoveTo(-1.6f);
                    Note("阶段0 玩家 → 门前 1.6m，s=" + SignedDistance().ToString("F2"));
                    Next(1);
                }
                break;

            case 1:
                // 等开门动画（spellWindup≈1.2s）走完，别把开门开销算进穿门
                waitFrames++;
                if (waitFrames >= 90)
                {
                    Note("阶段1 开门动画已等 90 帧，s=" + SignedDistance().ToString("F2"));
                    Next(2);
                }
                break;

            case 2:
                {
                    float s = SignedDistance();
                    if (crossed || s > 1.0f)
                    {
                        Note("阶段2 结束：crossed=" + crossed + " s=" + s.ToString("F2"));
                        Next(3);
                        break;
                    }

                    float to = s + 0.25f;
                    MoveTo(to);
                    if (fCooldown != null) fCooldown.SetValue(window, -1f);

                    if (lateUpdateMethod != null)
                    {
                        try { lateUpdateMethod.Invoke(window, null); }
                        catch (Exception e)
                        {
                            Exception inner = e.InnerException;
                            Note("  !LateUpdate 异常：" + (inner != null ? inner.Message : e.Message));
                        }
                    }

                    if (WorldPresence.Current == "Metro")
                    {
                        crossed = true;
                        Note("★ 穿门成功 @ s=" + to.ToString("F2") + " frame=" + Time.frameCount
                             + " 邻居=" + Join(WorldStreamer.PreloadWindow)
                             + " 装载=" + WorldStreamer.DebugActiveSet());
                        Next(3);
                    }
                    break;
                }

            case 3:
                // 穿门后观察 240 帧：重点看有没有世界被拉进来激活、激活那一下多贵
                postFrames.Add(frameMs);
                observeFrames++;
                if (observeFrames == 1)
                    Note("阶段3 开始观察：世界=" + WorldPresence.Current
                         + " 邻居=" + Join(WorldStreamer.PreloadWindow)
                         + " 装载=" + WorldStreamer.DebugActiveSet());
                if (observeFrames == 60 || observeFrames == 120 || observeFrames == 240)
                    Note("  观察 " + observeFrames + " 帧：邻居=" + Join(WorldStreamer.PreloadWindow)
                         + " 装载=" + WorldStreamer.DebugActiveSet());
                if (observeFrames >= 240) Next(4);
                break;

            case 4:
                Finish();
                break;
        }
    }

    private static bool IsReady()
    {
        if (window == null) return false;
        FieldInfo f = typeof(PortalWindow).GetField("ready", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;
        try { return (bool)f.GetValue(window); } catch { return false; }
    }

    private static string Join(IEnumerable<string> xs)
    {
        if (xs == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (string x in xs) { if (sb.Length > 0) sb.Append(","); sb.Append(x); }
        return sb.ToString();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;

        // 把停掉的 PlayerMovement 还回去，别把 Play 会话留在坏状态
        for (int i = 0; i < parkedMovement.Count; i++)
            if (parkedMovement[i] != null) parkedMovement[i].enabled = true;

        StringBuilder sb = new StringBuilder();
        sb.Append("[穿门v4 结果] 阈值 ").Append(HitchMs).Append("ms｜总耗时 ")
          .Append((Time.realtimeSinceStartup - baseStart).ToString("F1")).Append("s\n");

        sb.Append("穿门=").Append(crossed ? "成功" : "未发生")
          .Append("｜最终世界=").Append(WorldPresence.Current)
          .Append("｜玩家=").Append(root != null ? root.position.ToString("F2") : "NULL").Append("\n");

        if (postFrames.Count > 0)
        {
            float worst = 0f, total = 0f;
            int over = 0;
            for (int i = 0; i < postFrames.Count; i++)
            {
                total += postFrames[i];
                if (postFrames[i] > worst) worst = postFrames[i];
                if (postFrames[i] > HitchMs) over++;
            }
            sb.Append("穿门后 ").Append(postFrames.Count).Append(" 帧：平均 ")
              .Append((total / postFrames.Count).ToString("F1")).Append("ms，最重 ")
              .Append(worst.ToString("F0")).Append("ms，超阈 ").Append(over).Append(" 帧\n");
        }

        sb.Append("邻居=").Append(Join(WorldStreamer.PreloadWindow))
          .Append("\n装载=").Append(WorldStreamer.DebugActiveSet())
          .Append("\n── 明细 ──\n");
        for (int i = 0; i < notes.Count; i++) sb.Append(notes[i]).Append("\n");

        Debug.Log(sb.ToString());
    }
}
