using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 张力导演（World 程序集）：把 <see cref="TensionMeter"/> 从一个数字变成"会发生事"。
///
/// 参考工程 §2.2：房间带 <c>MinTensionInCell</c> / <c>IncreaseTensionSpeed</c>，张力上升后
/// 由 <c>RandomEventsManager</c> 按张力挑选 <c>DimDoorEvent_*</c>（地震/重力/声音/窥视）触发。
///
/// 本项目做法（无联机、无房间单元，用"层域 + 停留时长"表达同一件事）：
///   1. <b>推进</b>——在层域里按 <see cref="baseRisePerSecond"/> 上升；手电开/光照充足时打折；
///      回实验室按 <see cref="homeDecayPerSecond"/> 衰减。
///   2. <b>门槛</b>——张力跨过档位时才允许抽事件（不安 20%、紧逼 45%、濒临 100%），
///      避免低张力时被噪音轰炸。
///   3. <b>事件</b>——按张力权重从候选里抽一个：震动、幻听、灯光抽搐、余光人影、电流杂音。
///      每个事件自己管演出，控制器只负责冷却与挑选。
///   4. <b>画面</b>——把张力推给 <see cref="CrtOverlay"/> 做暗角收缩与呼吸，不额外加料。
///
/// 自动常驻：<c>[RuntimeInitializeOnLoadMethod]</c> 自建 DontDestroyOnLoad 实例，
/// 场景里不用摆任何东西（与 CrtOverlay / HintToast 同款）。
/// </summary>
[DefaultExecutionOrder(-50)]
public class TensionDirector : MonoBehaviour
{
    [Header("推进")]
    [Tooltip("层域里每秒基础上升量（0.012 → 约 73 秒涨满，配合门槛足够一局用）")]
    public float baseRisePerSecond = 0.012f;
    [Tooltip("回实验室每秒衰减量")]
    public float homeDecayPerSecond = 0.35f;
    [Tooltip("手电筒开着时的上升折扣（1 = 无减免）")]
    public float flashlightRelief = 0.55f;

    [Header("事件调度")]
    [Tooltip("两次随机事件之间的最短间隔（秒）")]
    public float minInterval = 22f;
    [Tooltip("两次随机事件之间的最长间隔（秒）")]
    public float maxInterval = 55f;
    [Tooltip("进层域后多久才开始抽事件（给玩家一点喘息）")]
    public float firstDelay = 40f;
    [Tooltip("总开关；关掉只保留张力数值，不触发任何演出")]
    public bool eventsEnabled = true;

    [Header("本局统计")]
    [SerializeField] private int eventsFired;

    private float nextEventIn;
    private bool cooling;
    private bool wasInWorld;
    private float riseAccum;

    /// <summary>本局已触发的随机事件次数（HUD / 账单可用）。</summary>
    public int EventsFired { get { return eventsFired; } }

    private static TensionDirector instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (instance != null) return;
        var go = new GameObject("Tension Director");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TensionDirector>();
    }

    private void OnEnable()
    {
        instance = this;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool inWorld = WorldPresence.Current != null && !WorldPresence.InHome;

        if (inWorld != wasInWorld)
        {
            wasInWorld = inWorld;
            if (inWorld)
            {
                TensionMeter.BeginExpedition();
                // 给一段缓冲，别刚落地就抽事件
                nextEventIn = firstDelay + Random.Range(0f, 8f);
            }
            else
            {
                CameraShake.Clear();
                nextEventIn = 0f;
            }
        }

        if (inWorld)
        {
            // 手电开着 / 光照充足 → 上升变慢。这是"省电关灯"的恐怖代价。
            float relief = PlayerGear.FlashlightOn ? flashlightRelief : 1f;
            TensionMeter.Tick(dt, baseRisePerSecond, relief);
            TickEvents(dt);
        }
        else
        {
            if (TensionMeter.Value > 0f) TensionMeter.Decay(dt, homeDecayPerSecond);
        }

        // 把张力推给全屏叠层（CrtOverlay 读它做暗角收缩）
        CrtOverlay.Tension = TensionMeter.Value;
    }

    private void TickEvents(float dt)
    {
        if (!eventsEnabled || cooling) return;
        if (TensionMeter.Level == TensionLevel.Calm) return;

        if (nextEventIn <= 0f)
        {
            nextEventIn = Random.Range(minInterval, maxInterval);
            // 门槛：低张力大概率不触发，高张力几乎必触发
            if (Random.value <= ChanceFor(TensionMeter.Level))
                StartCoroutine(FireRandomEvent());
            return;
        }

        nextEventIn -= dt;
    }

    /// <summary>各档位"到点后真的触发"的概率。</summary>
    private static float ChanceFor(TensionLevel lv)
    {
        switch (lv)
        {
            case TensionLevel.Uneasy: return 0.20f;
            case TensionLevel.Tense: return 0.45f;
            case TensionLevel.Critical: return 1.00f;
            default: return 0f;
        }
    }

    private IEnumerator FireRandomEvent()
    {
        cooling = true;
        eventsFired++;

        // 张力越高，抽到"吓人"的事件的权重越大
        float t = TensionMeter.Value;
        var pool = new List<System.Action>();
        pool.Add(ShakeEvent);                                  // 总是可选：轻微震动
        pool.Add(WhisperEvent);                                // 总是可选：幻听
        if (t >= TensionMeter.UneasyAt) pool.Add(LightTwitchEvent);
        if (t >= TensionMeter.TenseAt) { pool.Add(ShakeEvent); pool.Add(ShadowEvent); }
        if (t >= TensionMeter.CriticalAt) { pool.Add(ShadowEvent); pool.Add(StaticEvent); }

        pool[Random.Range(0, pool.Count)]();

        // 事件后升一截张力：坏事会让你更紧张——正反馈，但靠实验室衰减兜住
        TensionMeter.Add(0.04f + t * 0.05f);

        yield return new WaitForSeconds(2.5f);
        cooling = false;
    }

    /// <summary>震动：地震/结构呻吟。张力越高越明显。</summary>
    private void ShakeEvent()
    {
        float t = TensionMeter.Value;
        float amp = Mathf.Lerp(0.012f, 0.075f, Mathf.InverseLerp(0.35f, 1f, t));
        CameraShake.Impulse(amp, Mathf.Lerp(1.6f, 3.2f, t), Mathf.Lerp(14f, 26f, t));
        HintToast.Show(Random.value < 0.5f
            ? "脚下的地面在轻轻震动……"
            : "很远的地方传来一声闷响，像是什么塌了", 3f);
    }

    /// <summary>幻听：身后有人念了一句听不清的话。</summary>
    private void WhisperEvent()
    {
        string[] lines =
        {
            "刚才是不是有人在念我的名字？",
            "身后有呼吸声……回头只有走廊。",
            "有人在门那边敲了三下。",
            "耳机里钻进一句听不清的话。"
        };
        HintToast.Show(lines[Random.Range(0, lines.Length)], 3.2f);
    }

    /// <summary>灯光抽搐：整批灯先闪后暗一瞬（不动渐熄进度）。</summary>
    private void LightTwitchEvent()
    {
        var groups = FindObjectsOfType<LightGroup>();
        if (groups == null || groups.Length == 0) return;
        StartCoroutine(TwitchRoutine(groups));
    }

    private IEnumerator TwitchRoutine(LightGroup[] groups)
    {
        var wasOn = new bool[groups.Length];
        for (int i = 0; i < groups.Length; i++) wasOn[i] = groups[i] != null && groups[i].IsOn;

        for (int k = 0; k < 3; k++)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null) continue;
                if (Random.value < 0.55f) groups[i].SetOn(false, false);
            }
            yield return new WaitForSeconds(0.06f);
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] != null) groups[i].SetOn(wasOn[i], false);
            yield return new WaitForSeconds(0.09f);
        }

        // 收尾对齐到原状态，绝不把灯永久关掉（渐熄由 MetroBlackout 管）
        for (int i = 0; i < groups.Length; i++)
            if (groups[i] != null) groups[i].SetOn(wasOn[i], false);
    }

    /// <summary>余光人影：在玩家侧后方生成一个暗色剪影，转头即消失。</summary>
    private void ShadowEvent()
    {
        Transform root = PlayerRef.Root;
        if (root == null) return;
        StartCoroutine(ShadowRoutine(root));
    }

    private IEnumerator ShadowRoutine(Transform root)
    {
        // 生成在玩家背后 6m、偏 30° 的位置——正好在余光里
        float yaw = root.eulerAngles.y + 180f + Random.Range(-30f, 30f) * 0.1f;
        float dist = Random.Range(5f, 8f);
        Vector3 pos = root.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * dist;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Tension Shadow";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.02f, 0.03f, 0.03f, 1f);
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        HintToast.Show("走廊尽头好像站着什么东西。", 2.6f);

        // 存活 1.6s；若玩家转头看向它（夹角变小）则提前消失——"看清就没了"
        float t = 0f;
        while (t < 1.6f)
        {
            t += Time.deltaTime;
            Camera cam = PlayerRef.Camera;
            if (cam != null && go != null)
            {
                Vector3 to = go.transform.position - cam.transform.position;
                if (Vector3.Angle(cam.transform.forward, to) < 22f) break;
            }
            yield return null;
        }

        if (go != null) Destroy(go);
        if (mat != null) Destroy(mat);
    }

    /// <summary>电流杂音：短暂套用 EMF 风暴的 HUD 乱码（不触发灯组乱闪）。</summary>
    private void StaticEvent()
    {
        StartCoroutine(StaticRoutine());
    }

    private IEnumerator StaticRoutine()
    {
        float dur = 1.4f;
        EmfStorm.Set(true);
        yield return new WaitForSeconds(dur);
        EmfStorm.Set(false);
    }

    /// <summary>调试：立刻抽一个事件（Play 测试用）。</summary>
    public void ForceEvent()
    {
        if (cooling) return;
        StartCoroutine(FireRandomEvent());
    }
}
