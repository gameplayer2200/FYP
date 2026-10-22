using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 值班播报（文字版语音日志）：进入层域时在屏幕左上打字机式播报当班规则提醒
/// （Metro=渐熄/撤离窗口/黑暗加倍；其余层域占位文案）。每世界每次会话只播一次。
/// CRT 终端风格；双世界各挂一份时先到者独占渲染。
/// </summary>
public class DutyBriefing : MonoBehaviour
{
    private static DutyBriefing renderOwner;
    private static readonly HashSet<string> played = new HashSet<string>();

    private string world = "";
    private string[] lines = new string[0];
    private float shownAt = -99f;
    private const float Duration = 7.5f;

    private void Awake()
    {
        if (renderOwner != null && renderOwner != this) return;
        renderOwner = this;
    }

    private void Start()
    {
        WorldPresence.OnChanged += HandleWorldChanged;
        // 从异世界直接进 Play（开发流）时开场即播
        if (!WorldPresence.InHome) HandleWorldChanged();
    }

    private void OnDestroy()
    {
        WorldPresence.OnChanged -= HandleWorldChanged;
        if (renderOwner == this) renderOwner = null;
    }

    private void HandleWorldChanged()
    {
        if (WorldPresence.InHome) return;
        string w = WorldPresence.Current;
        if (string.IsNullOrEmpty(w) || played.Contains(w)) return;
        played.Add(w);
        world = w;
        lines = BriefingOf(w);
        shownAt = Time.unscaledTime;
    }

    private static string[] BriefingOf(string world)
    {
        switch (world)
        {
            case "Metro":
                return new[]
                {
                    "值班日志 · Metro 站",
                    "该站照明每 90 秒熄灭一段；黑暗区段耗电 1.5 倍。",
                    "撤离窗口随进站开启——归途门将按时锁死。",
                    "手电筒（F）省命也费电。祝当班顺利。",
                };
            case "FactoryDay":
                return new[]
                {
                    "值班日志 · 工厂 · 白班",
                    "排班钟每 4 分钟换班：在班的世界醒着，歇班的世界死寂。",
                    "白班看得清机器，但深区的门不开——那是夜班的。",
                    "工厂不掉电：耗的是交接之间的耐心。",
                };
            case "FactoryNight":
                return new[]
                {
                    "值班日志 · 工厂 · 夜班",
                    "夜班深区门全开，但异常会显形：机器自转、巡检的光。",
                    "按值班室排班表顺序送电，可锁定想要的昼夜。",
                    "夜班不掉电。祝交接顺利。",
                };
            case "Flooded_Grounds":
                return new[]
                {
                    "值班日志 · 水淹区",
                    "积水区域注意短路风险（调查中）。",
                    "沿高处行进，保存电量。",
                };
            default:
                return new[] { "值班日志 · 未知层域", "无当班记录。保持谨慎。" };
        }
    }

    private void OnGUI()
    {
        if (renderOwner != null && renderOwner != this) return;
        float t = Time.unscaledTime - shownAt;
        if (t < 0f || t > Duration) return;

        float a = Mathf.Clamp01((Duration - t) / 0.8f);
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, a);

        float W = CrtTheme.Px(420f);
        float H = CrtTheme.Px(30f + lines.Length * 20f);
        var panel = new Rect(CrtTheme.Px(22f), CrtTheme.Px(26f), W, H);
        CrtTheme.Panel(panel, "FYP // 值班播报");

        float x = panel.x + CrtTheme.Px(12f);
        float y = panel.y + CrtTheme.Px(28f);
        float w = panel.width - CrtTheme.Px(24f);

        for (int i = 0; i < lines.Length; i++)
        {
            // 打字机：逐行揭示（每行 1.1s，跳过第一行标题的延迟）
            string line = CrtTheme.Reveal(EmfStorm.Scramble(lines[i]), t - i * 1.1f, 28f);
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(18f)), line,
                CrtTheme.Style(i == 0 ? 13 : 12, i == 0 ? CrtTheme.Ink : CrtTheme.InkDim, false));
            y += CrtTheme.Px(20f);
        }

        GUI.color = prev;
    }
}
