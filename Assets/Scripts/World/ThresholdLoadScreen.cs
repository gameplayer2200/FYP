using UnityEngine;

/// <summary>
/// 门阈校准：开门时对面世界还没就绪，用一块 CRT 校准面板把等待遮住。
///
/// 为什么需要它：世界改成"按 E 开门时才激活"之后（见 WorldStreamer.ActivateOnApproach），
/// 激活那一帧要同步实例化整个场景（GameObject/Awake/OnEnable/物理体/首帧着色器），
/// 快则几帧、慢则一两秒。这段时间如果什么都不显示，玩家看到的就是"按了 E 门不开、画面卡住"——
/// 像故障而不像加载。给这段等待一个明确的视觉解释，观感就从"卡了"变成"在校准"。
///
/// 为什么不常驻：正常情况下门是即开即走的（对面世界已预载），显示出来反而添乱。
/// 所以 Show 之后还有一小段"犹豫期"（graceSeconds）：若等待在这段时间内结束就直接不画，
/// 一闪而过的面板比不显示更糟。
///
/// 放在 FYP.World 程序集：它与 WorldStreamer 一体，只被 AnywhereDoor 调用。
/// </summary>
[DefaultExecutionOrder(31900)]   // 在 CrtOverlay(32000) 之前画，让扫描线覆盖到它
public class ThresholdLoadScreen : MonoBehaviour
{
    /// <summary>犹豫期：等待短于这个时长就当没发生，不显示面板。</summary>
    private const float GraceSeconds = 0.18f;

    /// <summary>进度条从 0 爬到 MaxShownProgress 用多久（纯观感爬行，不映射真实进度）。</summary>
    private const float CrawlSeconds = 1.1f;
    private const float MaxShownProgress = 0.92f;

    /// <summary>乱码字符池（与 EmfStorm 同观感，但这里独立使用：加载不是电磁风暴）。</summary>
    private const string Noise = "01<>/\\[]{}=+*#%&@$?";

    private static ThresholdLoadScreen instance;

    private static string worldName;
    private static float shownAt;
    private static bool active;
    private static float lastScrambleAt;
    private static string scrambled = "";

    /// <summary>
    /// 开局部署模式：进度由装载器真实驱动（而不是门阈那套"爬行到 92% 等 Hide"）。
    /// 两者的差别在于有没有真实进度可报——门阈只有"还没好/好了"，开局部署有 N/M 个世界。
    /// </summary>
    private static bool deploymentMode;
    private static float deploymentProgress;
    private static string deploymentLine = "";

    /// <summary>开始遮蔽：对面世界正在激活。</summary>
    public static void Show(string world)
    {
        worldName = string.IsNullOrEmpty(world) ? "未知层域" : world;
        shownAt = Time.unscaledTime;
        lastScrambleAt = 0f;
        scrambled = "";
        deploymentMode = false;
        active = true;
        Ensure();
    }

    /// <summary>开局部署：进入真实进度模式。</summary>
    public static void ShowDeployment(string title)
    {
        worldName = string.IsNullOrEmpty(title) ? "开局部署" : title;
        shownAt = Time.unscaledTime;
        lastScrambleAt = 0f;
        scrambled = "";
        deploymentMode = true;
        deploymentProgress = 0f;
        deploymentLine = "";
        active = true;
        Ensure();
    }

    /// <summary>更新部署进度（0~1）与下面的说明行。</summary>
    public static void SetDeploymentProgress(float progress, string line)
    {
        deploymentProgress = Mathf.Clamp01(progress);
        if (!string.IsNullOrEmpty(line)) deploymentLine = line;
    }

    /// <summary>结束遮蔽。重复调用安全（门可能因为别的原因提前收手）。</summary>
    public static void Hide()
    {
        active = false;
        worldName = null;
    }

    private static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("ThresholdLoadScreen");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ThresholdLoadScreen>();
    }

    private void OnEnable()
    {
        if (instance == null) instance = this;
    }

    private void Update()
    {
        // 实例只活到不需要为止：不遮蔽时销毁自己，不留一个空 OnGUI 常驻。
        if (!active && instance == this) { Destroy(gameObject); }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>按固定节奏重新洗一遍标题里的乱码（每帧重洗会看不出字形，只会闪）。</summary>
    private static string Scramble(string text, float elapsed)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (elapsed - lastScrambleAt < 0.07f) return scrambled;
        lastScrambleAt = elapsed;
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ' ' || chars[i] == '「' || chars[i] == '」') continue;
            if (Random.value < 0.25f) chars[i] = Noise[Random.Range(0, Noise.Length)];
        }
        scrambled = new string(chars);
        return scrambled;
    }

    private void OnGUI()
    {
        if (!active) return;

        float waited = Time.unscaledTime - shownAt;
        if (waited < GraceSeconds) return;       // 犹豫期内什么都不画

        float w = Screen.width;
        float h = Screen.height;

        // 整屏压暗：让视线从门抽离，也盖住激活瞬间可能出现的半成品画面
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01((waited - GraceSeconds) * 6f) * 0.72f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), CrtTheme.White, ScaleMode.StretchToFill, true);
        GUI.color = prev;

        // 面板：屏幕中线偏下，不挡准心
        float pw = Mathf.Min(w * 0.62f, CrtTheme.Px(560f));
        float ph = CrtTheme.Px(150f);
        var panel = new Rect((w - pw) * 0.5f, h * 0.58f, pw, ph);
        CrtTheme.Panel(panel, deploymentMode ? "开局部署" : "门阈校准");

        float x = panel.x + CrtTheme.Px(18f);
        float y = panel.y + CrtTheme.Px(44f);
        float innerW = panel.width - CrtTheme.Px(36f);

        GUI.Label(new Rect(x, y, innerW, CrtTheme.Px(20f)),
            Scramble(deploymentMode ? "正在建立层域连接：" + worldName
                                    : "正在校准通往「" + worldName + "」的门阈", waited),
            CrtTheme.Style(15, CrtTheme.Ink));

        y += CrtTheme.Px(28f);

        // 进度：部署模式报真实 N/M；门阈模式只能爬行（没有真实进度，完成由 Hide 表达）
        float progress;
        if (deploymentMode) progress = deploymentProgress;
        else
        {
            float cr = Mathf.Clamp01((waited - GraceSeconds) / CrawlSeconds);
            progress = MaxShownProgress * (1f - (1f - cr) * (1f - cr));
        }
        CrtTheme.Gauge(new Rect(x, y, innerW, CrtTheme.Px(14f)), progress, 24, CrtTheme.InkDim);

        y += CrtTheme.Px(24f);
        string hint = deploymentMode
            ? (string.IsNullOrEmpty(deploymentLine) ? "读取层域数据" : deploymentLine)
            : (progress >= MaxShownProgress ? "层域响应迟缓……保持门阈稳定" : "读取层域数据");
        GUI.Label(new Rect(x, y, innerW, CrtTheme.Px(18f)),
            CrtTheme.Reveal(hint + " · " + Mathf.RoundToInt(progress * 100f) + "%",
                            waited - GraceSeconds, 42f),
            CrtTheme.Style(13, CrtTheme.InkFaint));
    }
}
