using UnityEngine;

/// <summary>
/// 交互提示条：屏幕下方的短时文字反馈（测线结果、拾取提示、断电提示…）。
/// 放在 Interaction 程序集：它是 E 键交互链路的反馈出口，
/// World 与 Content 都已依赖该程序集，避免把 UI 工具塞进 Core。
/// 首次调用自动创建常驻实例（DontDestroyOnLoad），双世界常驻下无需手动摆放。
/// </summary>
public class HintToast : MonoBehaviour
{
    private const float Width = 680f;
    private const float Height = 30f;

    private static HintToast instance;
    private static string message = "";
    private static float until;

    private GUIStyle style;
    private GUIStyle backStyle;

    /// <summary>显示一条提示，duration 秒后消失。新提示覆盖旧提示。</summary>
    public static void Show(string text, float duration = 2.5f)
    {
        if (string.IsNullOrEmpty(text)) return;
        Ensure();
        message = text;
        until = Time.unscaledTime + duration;
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

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            };
            style.normal.textColor = new Color(0.85f, 0.98f, 1f);
            backStyle = new GUIStyle(GUI.skin.box);
        }

        var rect = new Rect((Screen.width - Width) * 0.5f, Screen.height * 0.74f, Width, Height);
        GUI.Box(new Rect(rect.x - 10f, rect.y - 5f, rect.width + 20f, rect.height + 10f), "", backStyle);
        GUI.Label(rect, message, style);
    }
}
