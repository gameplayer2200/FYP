using UnityEngine;

/// <summary>
/// 残档阅读窗（CRT 终端风格）：居中弹出一块面板，逐字显示正文，按 E 或 Esc 关闭。
///
/// 【为什么不复用 NotePresenerWindow 那套】
/// 参考工程用的是 TMP + 独立窗口预制件；本项目 HUD 全线走 IMGUI（CrtTheme），
/// 沿用同一套绘制能保证磷光绿、扫描线、圆角边框完全一致，也不用再往场景里加 Canvas。
///
/// 【打字机与关闭的冲突】
/// 打字未完成时按 E 是"跳过动画直接显示全文"，而不是关闭——
/// 玩家不想等的时候按一下就该看到全部，再按一次才关。
/// 这是阅读界面的标准两段式，少了它每次都要等好几秒。
/// </summary>
public class ArchiveReader : MonoBehaviour
{
    private static ArchiveReader instance;

    private ArchiveLog.Entry entry;
    private float openedAt;
    private float scroll;
    private float contentHeight;

    // 打开前玩家是否已被别处冻住（开局部署等）。关闭时必须还原成原状态，
    // 不能无脑 Set(false)——那会把别人的锁解开。PlayerInputGate 只有一个全局开关，没有引用计数。
    private bool hadLockBefore;

    /// <summary>阅读窗是否打开（其他系统可以据此锁输入）。</summary>
    public static bool IsOpen { get { return instance != null && instance.entry != null; } }

    /// <summary>打开一篇残档。</summary>
    public static void Open(ArchiveLog.Entry e)
    {
        if (e == null) return;
        Ensure();
        if (instance.entry == null) instance.hadLockBefore = PlayerInputGate.Locked;
        instance.entry = e;
        instance.openedAt = Time.unscaledTime;
        instance.scroll = 0f;
        instance.contentHeight = 0f;
    }

    /// <summary>关闭。</summary>
    public static void Close()
    {
        if (instance == null) return;
        instance.ShutDown();
    }

    /// <summary>关窗并按打开前的状态还原输入闸门。</summary>
    private void ShutDown()
    {
        if (entry == null) return;
        entry = null;
        PlayerInputGate.Set(hadLockBefore);
    }

    private static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("[ArchiveReader]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ArchiveReader>();
    }

    private void Update()
    {
        if (entry == null) return;

        // 打字机未完成 → E/Esc 先补完全文
        float elapsed = Time.unscaledTime - openedAt;
        bool typing = elapsed < TypingDuration();
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (typing)
            {
                openedAt = Time.unscaledTime - TypingDuration();
            }
            else
            {
                entry = null;
            }
            return;
        }

        // 滚轮翻页（正文可能超出一屏）
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            scroll = Mathf.Clamp(scroll - wheel * CrtTheme.Px(28f), 0f, Mathf.Max(0f, contentHeight - ViewHeight()));
        }

        // 锁住玩家输入：阅读时不该还能走/开灯
        PlayerInputGate.Set(true);
    }

    private void OnDisable()
    {
        PlayerInputGate.Set(hadLockBefore);
    }

    /// <summary>全文总字数决定打字时长（约 45 字/秒，最少 0.8 秒）。</summary>
    private float TypingDuration()
    {
        if (entry == null) return 0f;
        int chars = 0;
        for (int i = 0; i < entry.paragraphs.Length; i++)
            if (entry.paragraphs[i] != null) chars += entry.paragraphs[i].Length;
        return Mathf.Max(0.8f, chars / 45f);
    }

    private float ViewHeight() { return CrtTheme.Px(250f); }

    private void OnGUI()
    {
        if (entry == null) return;

        float W = CrtTheme.Px(560f);
        float H = CrtTheme.Px(360f);
        var panel = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);

        // 背景压暗，聚焦到正文
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        CrtTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), Color.black);
        GUI.color = prev;

        CrtTheme.Panel(panel, "FYP // 残档查阅");

        float pad = CrtTheme.Px(16f);
        float x = panel.x + pad;
        float y = panel.y + CrtTheme.Px(30f);
        float w = panel.width - pad * 2f;

        GUI.Label(new Rect(x, y, w, CrtTheme.Px(22f)), entry.title, CrtTheme.Style(15, CrtTheme.Ink, false, true));
        y += CrtTheme.Px(26f);

        if (!string.IsNullOrEmpty(entry.signer))
        {
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(16f)), "— " + entry.signer + "　／　" + entry.world, CrtTheme.Style(11, CrtTheme.InkFaint, false));
            y += CrtTheme.Px(20f);
        }

        CrtTheme.Fill(new Rect(x, y, w, Mathf.Max(1f, Mathf.Round(CrtTheme.Scale))), new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.22f));
        y += CrtTheme.Px(10f);

        // 正文区（可滚动）
        var body = new Rect(x, y, w, H - (y - panel.y) - CrtTheme.Px(34f));
        GUI.BeginGroup(body);

        float elapsed = Time.unscaledTime - openedAt;
        int budget = Mathf.FloorToInt(elapsed * 45f);
        float cy = -scroll;
        var style = CrtTheme.Style(13, CrtTheme.Ink, true);

        for (int i = 0; i < entry.paragraphs.Length; i++)
        {
            string raw = entry.paragraphs[i] ?? "";
            string shown = budget <= 0 ? "" : (raw.Length <= budget ? raw : raw.Substring(0, budget));
            budget -= raw.Length;

            float h = style.CalcHeight(new GUIContent(shown), body.width);
            GUI.Label(new Rect(0, cy, body.width, h), shown, style);
            cy += h + CrtTheme.Px(10f);

            if (budget <= 0 && i < entry.paragraphs.Length - 1) break;
        }
        contentHeight = cy + scroll;

        GUI.EndGroup();

        // 底部状态行
        float by = panel.y + panel.height - CrtTheme.Px(26f);
        bool typing = elapsed < TypingDuration();
        string hint = typing
            ? "[E] 显示全文"
            : (contentHeight > ViewHeight() ? "[滚轮] 翻页　　[E] 关闭" : "[E] 关闭");
        GUI.Label(new Rect(x, by, w, CrtTheme.Px(18f)), hint, CrtTheme.Style(11, CrtTheme.InkDim, false));

        // 右下角光标闪烁
        if (typing && CrtTheme.Blink())
            CrtTheme.Cursor(x, by - CrtTheme.Px(2f));
    }
}
