using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效遮挡：被墙隔开的声音会变闷、变小（对照 Project Doors 的
/// <c>SoundOcclusionArea</c> / <c>SoundOcclusionPortal</c>）。
///
/// 为什么值得做：本项目的恐怖主要靠"听"——Metro 的风压轰鸣、工厂的机器嗡鸣、
/// 死端的环境音都挂在场景里固定的位置上。没有遮挡时，隔着一堵墙和一扇门的声音
/// 与站在它面前听完全一样，玩家就无法用耳朵判断"那东西离我多远、在哪边"，
/// 声音退化成背景噪音。加上遮挡之后，走廊拐角、门后、楼层之间的声音会自然分层，
/// 玩家才会开始"靠听来走路"——这是恐怖游戏的空间感基础。
///
/// 实现取舍（不做完整的声学系统，只做最值的那一层）：
///   · 从听者到声源打一条射线；命中任何非触发器几何体 → 判定被遮挡。
///   · 遮挡时把音量乘一个系数，并用 <see cref="AudioLowPassFilter"/> 压低高频
///     （真实世界里"闷"就是高频先衰减，只降音量听起来像"远了"而不是"隔墙了"）。
///   · 逐帧平滑过渡，绝不硬切——硬切会变成"咔哒"的爆音。
///   · 每帧只处理一小批声源（分帧轮转），避免几十个声源同时射线检测。
///
/// 与 <see cref="WorldLightIsolation"/> 同属"世界隔离"层：世界装了才生效，
/// 不改变任何序列化音频参数（<c>baseVolume</c> 在运行时抓取并缓存）。
/// </summary>
[DefaultExecutionOrder(-30)]
public class SoundOcclusion : MonoBehaviour
{
    [Header("遮挡判定")]
    [Tooltip("每隔多久重新打一次遮挡射线（秒）。越小越灵敏，代价越高。")]
    public float probeInterval = 0.22f;
    [Tooltip("每帧最多探测几个声源（分帧轮转）。")]
    public int probesPerFrame = 3;
    [Tooltip("声源离听者多远之外才做遮挡判定——贴脸的声音不需要")]
    public float minDistance = 2.5f;
    [Tooltip("超过这个距离不参与（超出听觉范围，音量本来就接近 0）")]
    public float maxDistance = 60f;

    [Header("遮挡效果")]
    [Tooltip("被墙隔开后的音量系数")]
    [Range(0f, 1f)] public float occludedVolume = 0.34f;
    [Tooltip("被墙隔开后的低通截止频率（Hz）——越低越闷")]
    public float occludedCutoff = 760f;
    [Tooltip("无遮挡时的低通截止频率（接近不滤波）")]
    public float openCutoff = 21000f;
    [Tooltip("音量/滤波过渡速度（每秒的比例），越大切换越干脆")]
    public float blendSpeed = 5.5f;
    [Tooltip("勾上后不检查遮挡（回归对比用）")]
    public bool bypass = false;

    /// <summary>每个受管声源的运行时状态（原始音量必须缓存，否则乘一次就没法还原）。</summary>
    private class Entry
    {
        public AudioSource src;
        public AudioLowPassFilter filter;
        public float baseVolume = 1f;
        public bool occluded;
        public float lastProbe;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private float rescanAt;
    private int cursor;
    private Transform listener;

    /// <summary>当前判定为被遮挡的声源数量（Play 测试读它）。</summary>
    public int OccludedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < entries.Count; i++) if (entries[i].occluded && entries[i].src != null) n++;
            return n;
        }
    }

    /// <summary>受管声源总数（Play 测试读它）。</summary>
    public int TrackedCount { get { return entries.Count; } }

    private static SoundOcclusion instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (instance != null) return;
        var go = new GameObject("Sound Occlusion");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SoundOcclusion>();
    }

    private void OnEnable()
    {
        instance = this;
        rescanAt = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (Time.unscaledTime >= rescanAt)
        {
            rescanAt = Time.unscaledTime + 3f;   // 世界装卸会带来新声源，定期重扫
            Rescan();
        }

        if (entries.Count == 0) return;

        if (listener == null)
        {
            var l = FindObjectOfType<AudioListener>();
            if (l != null) listener = l.transform;
            else return;
        }

        Vector3 ear = listener.position;

        // 分帧轮转：每帧只看一小批，几十个声源也不会在一帧里打出几十条射线
        int budget = Mathf.Max(1, probesPerFrame);
        for (int k = 0; k < budget && entries.Count > 0; k++)
        {
            cursor = (cursor + 1) % entries.Count;
            var e = entries[cursor];
            if (e == null || e.src == null) continue;
            if (!e.src.isActiveAndEnabled || !e.src.isPlaying) continue;
            if (Time.unscaledTime - e.lastProbe < probeInterval) continue;
            e.lastProbe = Time.unscaledTime;

            Vector3 p = e.src.transform.position;
            float d = Vector3.Distance(ear, p);
            if (d < minDistance || d > maxDistance)
            {
                e.occluded = false;
            }
            else
            {
                // 从耳朵往声源打；命中点比声源近就说明中间有东西挡着。
                // 0.92 的收尾系数避开"声源自己贴在墙面上"时打到自己那面墙的假遮挡。
                Vector3 dir = (p - ear) / Mathf.Max(1e-4f, d);
                e.occluded = Physics.Raycast(ear, dir, d * 0.92f, ~0, QueryTriggerInteraction.Ignore);
            }
        }

        // 平滑过渡（音量 + 低通）
        float k2 = Mathf.Clamp01(blendSpeed * dt);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.src == null) continue;

            float wantVol = e.baseVolume * (e.occluded ? occludedVolume : 1f);
            e.src.volume = Mathf.Lerp(e.src.volume, wantVol, k2);

            if (e.filter != null)
            {
                e.filter.cutoffFrequency = Mathf.Lerp(e.filter.cutoffFrequency,
                    e.occluded ? occludedCutoff : openCutoff, k2);
            }
        }
    }

    /// <summary>重扫场景里的声源。跳过 2D 音效（UI/旁白不吃空间遮挡）。</summary>
    private void Rescan()
    {
        var all = FindObjectsOfType<AudioSource>(true);
        // 复用已有条目，避免每 3 秒重建列表造成分配
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null) entries[i].occluded = false;

        var seen = new HashSet<AudioSource>();
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null) continue;
            if (s.spatialBlend <= 0.01f) continue;   // 2D 音效不参与
            if (s.clip == null) continue;
            seen.Add(s);

            Entry found = null;
            for (int j = 0; j < entries.Count; j++)
            {
                if (entries[j] != null && entries[j].src == s) { found = entries[j]; break; }
            }
            if (found == null)
            {
                found = new Entry();
                found.src = s;
                found.baseVolume = s.volume;
                found.filter = s.GetComponent<AudioLowPassFilter>();
                if (found.filter == null) found.filter = s.gameObject.AddComponent<AudioLowPassFilter>();
                found.filter.cutoffFrequency = openCutoff;
                entries.Add(found);
            }
        }

        // 剔除已销毁/已不被管理的
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e == null || e.src == null || !seen.Contains(e.src)) entries.RemoveAt(i);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }
}
