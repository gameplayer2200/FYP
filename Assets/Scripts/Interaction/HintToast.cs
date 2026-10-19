using UnityEngine;

/// <summary>
/// 交互提示条：屏幕下方的短时文字反馈（测线结果、拾取提示、断电提示…）。
/// 放在 Interaction 程序集：它是 E 键交互链路的反馈出口，
/// World 与 Content 都已依赖该程序集，避免把 UI 工具塞进 Core。
/// 首次调用自动创建常驻实例（DontDestroyOnLoad），双世界常驻下无需手动摆放。
///
/// 外观：CRT 终端条（CrtTheme）——等宽字形、磷光绿、扫描线、"> " 命令行前缀、打字机逐字显示 + 方块光标。
/// </summary>
public class HintToast : MonoBehaviour
{
    private const float Width = 660f;
    private const float Height = 34f;

    private static HintToast instance;
    private static string message = "";
    private static float until;
    private static float shownAt;

    /// <summary>显示一条提示，duration 秒后消失。新提示覆盖旧提示。</summary>
    public static void Show(string text, float duration = 2.5f)
    {
        if (string.IsNullOrEmpty(text)) return;
        Ensure();
        message = text;
        until = Time.unscaledTime + duration;
        shownAt = Time.unscaledTime;
    }

    private static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("HintToast");
        instance = go.AddComponent<HintToast>();
        DontDestroyOnLoad(go);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(message) || Time.unscaledTime >= until) return;

        float w = CrtTheme.Px(Width);
        float h = CrtTheme.Px(Height);
        var rect = new Rect(Mathf.Round((Screen.width - w) * 0.5f), Mathf.Round(Screen.height * 0.74f), w, h);
        CrtTheme.Panel(rect);

        var style = CrtTheme.Style(15, CrtTheme.Ink);
        string body = CrtTheme.Reveal("> " + message, Time.unscaledTime - shownAt, 55f);
        GUI.Label(new Rect(rect.x + CrtTheme.Px(12f), rect.y + CrtTheme.Px(9f), rect.width - CrtTheme.Px(26f), h - CrtTheme.Px(16f)), body, style);

        // 方块光标紧跟文字末尾（打字机完成后闪烁）
        float tw = style.CalcSize(new GUIContent(body)).x;
        float cx = rect.x + CrtTheme.Px(12f) + tw + CrtTheme.Px(2f);
        if (cx < rect.xMax - CrtTheme.Px(16f)) CrtTheme.Cursor(cx, rect.y + CrtTheme.Px(10f), 7f, 13f);
    }
}
