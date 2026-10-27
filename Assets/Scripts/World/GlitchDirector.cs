using System.Collections;
using UnityEngine;

/// <summary>
/// 「故障导演」：在高张力时随机让世界撒一次谎（对照 Project Doors 的 PlayerGlitch* 系列）。
///
/// 为什么单独一层而不是塞进 TensionDirector：
/// 张力事件是"加压力"（震动、灯光、幻听），故障是"改信息"（道具栏顺序、世界名字）。
/// 前者玩家会归因为"这里很危险"，后者会归因于"我是不是看错了"——两类体验要能分别调参、分别关掉。
///
/// 触发条件（三条同时成立，避免在实验室/低张力时乱来）：
///   1. 在层域里（WorldPresence.InHome == false）；
///   2. 张力 ≥ <see cref="minTension"/>（默认 0.55，正对应 TensionMeter.TenseAt 附近）；
///   3. 距上次故障 ≥ 随机间隔（<see cref="minInterval"/>~<see cref="maxInterval"/>）。
///
/// 每种故障都是"发作 → 持续 → 无条件复原"的形状，绝不把状态留在脏的一侧：
/// 玩家可能正看着道具栏、可能正盯着右上角，离开时必须和进来前一模一样。
/// </summary>
[DefaultExecutionOrder(-40)]
public class GlitchDirector : MonoBehaviour
{
    [Header("触发条件")]
    [Tooltip("张力低于这个值不发作（0.55 ≈ 紧逼档起点）")]
    public float minTension = 0.55f;
    [Tooltip("两次故障之间的最短/最长间隔（秒）")]
    public float minInterval = 55f;
    public float maxInterval = 110f;
    [Tooltip("进层域后先安静这么久，别刚落地就开始改信息")]
    public float firstDelay = 70f;

    [Header("各故障持续时间")]
    public float sortDuration = 2.6f;
    public float nameDuration = 3.4f;
    public float chatDuration = 4.2f;
    public float copyDuration = 2.2f;

    [Header("开关")]
    public bool enabledGlitches = true;

    private float nextIn;
    private bool cooling;
    private bool wasInWorld;
    private int fired;

    /// <summary>已发作次数（Play 测试读它）。</summary>
    public int Fired { get { return fired; } }

    private static GlitchDirector instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (instance != null) return;
        var go = new GameObject("Glitch Director");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GlitchDirector>();
    }

    private void OnEnable()
    {
        instance = this;
        // 回实验室 / 瘫倒重启 → 立刻收手，别让玩家在安全区看到"信息在骗人"
        WorldPresence.OnChanged -= HandleWorldChanged;
        WorldPresence.OnChanged += HandleWorldChanged;
    }

    private void OnDisable()
    {
        WorldPresence.OnChanged -= HandleWorldChanged;
        GlitchState.Clear();
    }

    private static void HandleWorldChanged()
    {
        if (WorldPresence.InHome) GlitchState.Clear();
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
                nextIn = firstDelay + Random.Range(0f, 15f);
            }
            else
            {
                GlitchState.Clear();
                nextIn = 0f;
            }
        }

        if (!inWorld || !enabledGlitches || cooling) return;
        if (TensionMeter.Value < minTension) return;

        if (nextIn <= 0f)
        {
            nextIn = Random.Range(minInterval, maxInterval);
            StartCoroutine(FireRoutine());
            return;
        }
        nextIn -= dt;
    }

    /// <summary>故障池：按"改信息的破坏力"从轻到重，高张力才解锁后两种。</summary>
    private IEnumerator FireRoutine()
    {
        cooling = true;
        fired++;

        var kinds = new System.Collections.Generic.List<GlitchState.Kind>();
        kinds.Add(GlitchState.Kind.InventorySort);   // 最轻：只是顺序错了
        kinds.Add(GlitchState.Kind.WorldName);        // 中：名字错了
        if (TensionMeter.Value >= 0.7f) kinds.Add(GlitchState.Kind.PhantomChat);
        if (TensionMeter.Value >= 0.85f) kinds.Add(GlitchState.Kind.PlayerCopy);

        float dur;
        switch (kinds[Random.Range(0, kinds.Count)])
        {
            case GlitchState.Kind.InventorySort: dur = sortDuration; yield return SortRoutine(); break;
            case GlitchState.Kind.WorldName: dur = nameDuration; yield return NameRoutine(); break;
            case GlitchState.Kind.PhantomChat: dur = chatDuration; yield return ChatRoutine(); break;
            default: dur = copyDuration; yield return CopyRoutine(); break;
        }

        // 收尾固定留一段冷却：连续两次故障会让人从"害怕"变成"烦"
        yield return new WaitForSeconds(dur + 1.5f);
        cooling = false;
    }

    // ── 1) 道具栏错位 ──
    // 只改"显示"，不改 PlayerGear 的真实持有与选中：玩家按 F 用掉的是真的那一件。
    // 恐怖点在于"我以为第 2 格是手电，按了却是万用表"——但游戏不解释。
    private IEnumerator SortRoutine()
    {
        int offset = Random.Range(1, PlayerGear.SlotCount);
        GlitchState.Begin(GlitchState.Kind.InventorySort, offset);
        yield return new WaitForSeconds(sortDuration);
        GlitchState.Clear();
    }

    // ── 2) 世界名字替换 ──
    // 换成"像那么回事"但不属于任何世界的名字：玩家会怀疑自己记错了层域。
    private IEnumerator NameRoutine()
    {
        string[] aliases = { "未登记层域", "此处", "第三候车室", "你刚离开的地方", "原路返回" };
        GlitchState.Begin(GlitchState.Kind.WorldName, 0, aliases[Random.Range(0, aliases.Length)]);
        yield return new WaitForSeconds(nameDuration);
        GlitchState.Clear();
    }

    // ── 3) 幻听对话 ──
    // 用 HintToast 的样式假装是"另一个玩家"在发消息。单机游戏里没有别的玩家——这才是重点。
    private IEnumerator ChatRoutine()
    {
        string[] lines =
        {
            "别回头。",
            "你左边那扇门刚才是关着的。",
            "我数到三，我们一起跑。",
            "你为什么不回话？",
            "信号……只剩下你一个人了吗"
        };
        GlitchState.Begin(GlitchState.Kind.PhantomChat);
        HintToast.Show("[未知用户] " + lines[Random.Range(0, lines.Length)], 3.2f);
        yield return new WaitForSeconds(chatDuration);
        GlitchState.Clear();
    }

    // ── 4) 玩家复制体 ──
    // 正前方站着另一个"你"。玩家靠近或直视超过 1.2s 就消失——永远看不清楚。
    private IEnumerator CopyRoutine()
    {
        Transform root = PlayerRef.Root;
        if (root == null) yield break;

        GlitchState.Begin(GlitchState.Kind.PlayerCopy);

        // 站在正前方 7~10m（不在余光里，是"一抬头就在那儿"的位置）
        Vector3 pos = root.position + root.forward * Random.Range(7f, 10f);
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Glitch Copy";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.62f, 0.95f, 0.62f);

        var mat = new Material(Shader.Find("Sprites/Default"));
        // 用玩家自己的轮廓色（不是纯黑）：看起来像个人，但细节全无
        mat.color = new Color(0.10f, 0.13f, 0.12f, 1f);
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        float t = 0f;
        float stared = 0f;
        while (t < copyDuration)
        {
            t += Time.deltaTime;
            Camera cam = PlayerRef.Camera;
            if (cam != null && go != null)
            {
                Vector3 to = go.transform.position - cam.transform.position;
                bool looking = Vector3.Angle(cam.transform.forward, to) < 25f;
                // 直视满 1.2 秒就消失：盯久了它反而没了，比"扑上来"更让人不安
                if (looking)
                {
                    stared += Time.deltaTime;
                    if (stared > 1.2f) break;
                }
                else stared = 0f;
            }
            yield return null;
        }

        if (go != null) Destroy(go);
        if (mat != null) Destroy(mat);
        GlitchState.Clear();
    }

    /// <summary>调试：立刻抽一次故障（Play 测试用）。</summary>
    public void ForceGlitch()
    {
        if (cooling) return;
        StartCoroutine(FireRoutine());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }
}
