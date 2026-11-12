using System.Collections;
using UnityEngine;

/// <summary>
/// 异常「敲击」（Metro 专属，提案表 8）：关着的归途门后传来三下敲门声，开门空无一物；
/// 敲击后 pushWindow 秒内穿门，落点会被"另一侧的它"侧向推偏 pushDistance 米。
///
/// 与 TensionDirector.WhisperEvent 的幻听文字（"有人在门那边敲了三下"）刻意分层：
/// 幻听只有文字没有声音；这里是真实的 3D 定位敲门声 + 门板微抖——听得到、找得到方向，
/// 开门却是空的。「真敲」不配旁白文字，声音本身就是全部信息。
///
/// 落点偏移的实现红线（AGENTS §4/§7）：绝不触碰 PortalWindow / AnywhereDoor / WorldStreamer。
/// 做法是订阅 WorldPresence.OnChanged（穿门后触发），等一帧让传送落定，再用
/// CharacterController.Move 分帧把玩家侧推——Move 自带碰撞滑动，推不进墙里。
///
/// 敲门声为运行时程序合成（项目惯例的无素材回退，见 MetroSceneDev.Stage4_MakeSfx 的
/// 合成思路；这里改成 AudioClip.Create 在内存里生成，省掉资产文件与导入开销），
/// Inspector 塞了 clip 则优先生效。
/// </summary>
public class KnockAnomaly : MonoBehaviour
{
    private const string PresenceId = "metro.knock";

    [SerializeField] private MetroBlackout blackout;
    [Tooltip("归途门（anywhere-door-Metro 的门板）。敲击声源、微抖对象、异常源坐标都以它为准")]
    [SerializeField] private AnywhereDoor exitDoor;

    [Header("节奏")]
    [Tooltip("首次敲击距玩家进站的最短秒数")]
    [SerializeField] private float firstDelay = 90f;
    [SerializeField] private float minInterval = 180f;
    [SerializeField] private float maxInterval = 300f;
    [Tooltip("本次会话最多敲几次——稀有事件，不无限复读")]
    [SerializeField] private int maxKnocks = 2;

    [Header("落点偏移")]
    [Tooltip("敲击后多少秒内穿门会被推偏")]
    [SerializeField] private float pushWindow = 30f;
    [Tooltip("穿门落点被推偏的距离（米）")]
    [SerializeField] private float pushDistance = 2f;
    [Tooltip("推力分帧摊开的时长（秒）——一次 Move 2 米是瞬移手感，摊开 0.25 秒才是「被撞了一下」")]
    [SerializeField] private float pushDuration = 0.25f;
    [SerializeField] private string pushHint = "过门的瞬间，肩膀被什么撞了一下";

    [Header("音效（全部可留空：运行时程序合成）")]
    [Tooltip("单下敲门声。留空 = 运行时合成")]
    [SerializeField] private AudioClip knockClip;

    private AudioSource doorKnock;   // 3D 定位在门板上
    private AudioSource flatThud;    // 2D：过门被推时玩家耳边一声闷响
    private float nextIn;
    private int knocks;
    private bool running;
    private float pushUntil = -1f;
    private bool pushedThisWindow;
    private string lastWorld;
    private string metroWorld;       // 世界名跟门走（= 门所在场景名），不硬编码字符串

    private void Awake()
    {
        metroWorld = exitDoor != null ? exitDoor.gameObject.scene.name : gameObject.scene.name;
        if (knockClip == null) knockClip = SynthesizeKnock();
        // 3D 声源挂在门板下：门在哪儿声在哪儿，玩家靠双耳找门
        var srcGo = new GameObject("敲门声");
        srcGo.transform.SetParent(exitDoor != null ? exitDoor.transform : transform, false);
        doorKnock = srcGo.AddComponent<AudioSource>();
        doorKnock.clip = knockClip;
        doorKnock.spatialBlend = 1f;
        doorKnock.rolloffMode = AudioRolloffMode.Linear;
        doorKnock.minDistance = 1.5f;
        doorKnock.maxDistance = 35f;
        // 2D 闷响：被推发生在穿门后的世界侧，3D 声源还在 Metro 门上听不见
        flatThud = gameObject.AddComponent<AudioSource>();
        flatThud.clip = knockClip;
        flatThud.pitch = 0.7f;
        flatThud.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        AnomalyPresence.Register(PresenceId, exitDoor != null ? exitDoor.transform : transform, "敲击");
        WorldPresence.OnChanged += HandleWorldChanged;
    }

    private void OnDisable()
    {
        AnomalyPresence.Unregister(PresenceId);
        WorldPresence.OnChanged -= HandleWorldChanged;
    }

    private void Update()
    {
        if (running) return;
        if (knocks >= maxKnocks) return;
        if (blackout == null || !blackout.PlayerInZone) return;
        if (exitDoor == null) return;
        if (exitDoor.IsOpen)
        {
            // 门开着没有「门后」——延后 5 秒再看（到点时门多半已被 autoClose 关上）
            if (nextIn <= 0f) nextIn = 5f;
            nextIn -= Time.deltaTime;
            return;
        }

        if (nextIn <= 0f) nextIn = firstDelay + Random.Range(0f, 60f);
        nextIn -= Time.deltaTime;
        if (nextIn <= 0f)
        {
            nextIn = Random.Range(minInterval, maxInterval);
            knocks++;
            StartCoroutine(KnockRoutine());
        }
    }

    private IEnumerator KnockRoutine()
    {
        running = true;
        var leaf = exitDoor.transform;
        Vector3 basePos = leaf.localPosition;

        for (int i = 0; i < 3; i++)
        {
            doorKnock.pitch = 1f + 0.07f * (i - 1);   // 三下略有人手轻重
            doorKnock.Play();

            // 门板微抖：沿门板右向高频衰减摆动，结束精确归位（不动 rotation——
            // AnywhereDoor 的开关动画独占 localRotation，位置才是无主之地）
            float t = 0f;
            while (t < 0.3f)
            {
                float k = 1f - t / 0.3f;
                leaf.localPosition = basePos + leaf.right * (Mathf.Sin(t * 70f) * 0.006f * k);
                t += Time.deltaTime;
                yield return null;
            }
            leaf.localPosition = basePos;
            if (i < 2) yield return new WaitForSeconds(0.26f);
        }

        // 窗口开启：这 30 秒里「它」在门那边——异常源在场（相机/温度计可感知），
        // 且穿门会被推
        pushUntil = Time.time + pushWindow;
        pushedThisWindow = false;
        AnomalyPresence.SetActive(PresenceId, true);
        running = false;
        StartCoroutine(WindowEndRoutine());
    }

    private IEnumerator WindowEndRoutine()
    {
        yield return new WaitForSeconds(pushWindow);
        if (Time.time >= pushUntil - 0.05f) AnomalyPresence.SetActive(PresenceId, false);
    }

    /// <summary>穿门（任意方向）且迁移涉及 Metro 且在窗口内 → 落点被推偏一次。</summary>
    private void HandleWorldChanged()
    {
        string current = WorldPresence.Current;
        bool involvesMetro = lastWorld == metroWorld || current == metroWorld;
        lastWorld = current;
        if (!involvesMetro) return;
        if (pushedThisWindow || Time.time >= pushUntil) return;
        pushedThisWindow = true;
        StartCoroutine(PushRoutine());
    }

    private IEnumerator PushRoutine()
    {
        // 传送发生在 PortalWindow 的 LateUpdate；OnChanged 在其之后同步广播，
        // 再等一帧确保落点完全写定才动手
        yield return null;
        var player = PlayerRef.Root;
        var cc = PlayerRef.Controller;
        if (player == null || cc == null) yield break;

        // 推的方向 = 行进方向的侧向（"从肩膀撞了一下"）。先短射线挑净空侧：
        // 门侧常有墙，闭眼往右推可能整段滑量被墙吃掉
        Vector3 fwd = player.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 origin = player.position + Vector3.up * 1.2f;
        bool rightBlocked = Physics.Raycast(origin, right, pushDistance + 0.6f);
        bool leftBlocked = Physics.Raycast(origin, -right, pushDistance + 0.6f);
        Vector3 dir = rightBlocked && !leftBlocked ? -right : right;

        if (flatThud != null) flatThud.Play();
        HintToast.Show(pushHint);

        float moved = 0f;
        float speed = pushDistance / Mathf.Max(0.05f, pushDuration);
        while (moved < pushDistance)
        {
            float step = Mathf.Min(speed * Time.deltaTime, pushDistance - moved);
            cc.Move(dir * step);
            moved += step;
            yield return null;
        }
    }

    /// <summary>程序合成单下敲门声（~0.25s）：低频闷响体 + 起音瞬态。确定性波形，每次运行一致。</summary>
    private static AudioClip SynthesizeKnock()
    {
        const int sr = 44100;
        const float len = 0.25f;
        int n = Mathf.CeilToInt(sr * len);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 26f);                     // 敲击包络：快起快衰
            float body = Mathf.Sin(2f * Mathf.PI * 62f * t)      // 闷响主体：低频
                       + Mathf.Sin(2f * Mathf.PI * 124f * t) * 0.4f;
            float attack = (t < 0.004f ? (0.004f - t) / 0.004f : 0f) // 指节叩击的极短瞬态
                         * Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.5f;
            data[i] = Mathf.Clamp((body * 0.75f + attack) * env, -1f, 1f);
        }
        var clip = AudioClip.Create("knock_synthetic", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
