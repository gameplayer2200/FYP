using UnityEngine;

/// <summary>
/// 接单任务板（右上角 HUD，按世界切换形态——读 Core 的 WorldPresence）：
///  · 实验室（起始世界）：整备视图——收容档案、随身装备清单、出发提示；
///  · 层域（异世界）：订单视图——目标进度、下一步提示、撤离窗口倒计时（≤120s 红闪）。
/// Tab 展开详情；收容入账即完成订单（绿色提示）。吸收了旧 SpecimenHud 的计数职能。
/// OnGUI 直绘与电量条风格一致；风暴中经 EmfStorm 乱码化。
/// </summary>
public class OrderBoard : MonoBehaviour
{
    [Tooltip("本局要求收容入账的异常物数量")]
    [SerializeField] private int targetCount = 2;
    [SerializeField] private string rewardText = "报酬：记忆核心配额 + 续期";
    [SerializeField] private string detailText = "目标散落在站台与车厢深处。\n带回实验室收容台按 E 入账，才算完成订单。\n瘫倒重启会散失一半随身异常物。";
    [SerializeField] private KeyCode detailKey = KeyCode.Tab;

    private static readonly Color ThemeColor = new Color(0.22f, 0.86f, 0.95f);
    private static readonly Color DimColor = new Color(0.62f, 0.78f, 0.82f);
    private static readonly Color DoneColor = new Color(0.45f, 0.95f, 0.55f);

    private int carrying;
    private int stored;
    private bool detail;
    private Texture2D backing;

    // 双世界常驻、两侧场景各挂一份时，先到者独占渲染，避免重叠双画
    private static OrderBoard renderOwner;

    private void Awake()
    {
        if (renderOwner != null && renderOwner != this) return;
        renderOwner = this;
    }

    private void Start()
    {
        if (Inventory.Instance != null)
        {
            carrying = Inventory.Instance.SpecimenCount;
            stored = Inventory.Instance.StoredSpecimenCount;
        }
        Inventory.OnSpecimenChanged += Refresh;
    }

    private void OnDestroy()
    {
        Inventory.OnSpecimenChanged -= Refresh;
        if (renderOwner == this) renderOwner = null;
    }

    private void Refresh(int carryingCount, int storedCount)
    {
        carrying = carryingCount;
        stored = storedCount;
    }

    private void Update()
    {
        if (Input.GetKeyDown(detailKey)) detail = !detail;
    }

    private static string WorldDisplay(string sceneName)
    {
        switch (sceneName)
        {
            case "SpecimenLab": return "实验室";
            case "Metro": return "Metro 站";
            case "FactoryDay": return "工厂·昼";
            case "FactoryNight": return "工厂·夜";
            case "Flooded_Grounds": return "水淹区";
            default: return string.IsNullOrEmpty(sceneName) ? "层域" : sceneName;
        }
    }

    private void OnGUI()
    {
        if (renderOwner != null && renderOwner != this) return; // 另一份已在画
        renderOwner = this;

        bool inHome = WorldPresence.InHome;
        int lineCount = inHome ? 3 : 4; // 层域形态多一行撤离窗口（武装时）
        float h = (detail ? 106f : 0f) + (inHome ? 118f : (lineCount * 24f) + 42f);
        var j = EmfStorm.Jitter();
        float x = Screen.width - 322f + j.x;
        float y = 40f + j.y;

        var prev = GUI.color;
        GUI.DrawTexture(new Rect(x, y, 302f, h), Backing);
        GUI.color = prev;

        float cy = y + 8f;
        var title = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        title.normal.textColor = ThemeColor;
        var body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        body.normal.textColor = ThemeColor;
        var dim = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        dim.normal.textColor = DimColor;

        if (inHome)
        {
            GUI.Label(new Rect(x + 12f, cy, 278f, 22f), EmfStorm.Scramble("实验室 · 整备"), title); cy += 30f;
            GUI.Label(new Rect(x + 12f, cy, 278f, 20f), EmfStorm.Scramble("已收容异常物 " + stored + "（订单目标 " + targetCount + "）"), body); cy += 24f;
            GUI.Label(new Rect(x + 12f, cy, 278f, 20f), EmfStorm.Scramble("装备：手电筒 " + Mark(PlayerGear.HasFlashlight)
                + " · 万用表 " + Mark(PlayerGear.HasMultimeter)), body); cy += 24f;
            var hint = stored >= targetCount ? DoneColor : DimColor;
            var hStyle = new GUIStyle(dim) { fontSize = 12 };
            hStyle.normal.textColor = hint;
            GUI.Label(new Rect(x + 12f, cy, 278f, 22f), EmfStorm.Scramble(stored >= targetCount
                ? "订单完成 ✓——休整后可再次出发" : "从任意一扇门出发，深入层域回收"), hStyle); cy += 26f;
        }
        else
        {
            GUI.Label(new Rect(x + 12f, cy, 278f, 22f), EmfStorm.Scramble("订单：" + WorldDisplay(WorldPresence.Current) + " 回收"), title); cy += 30f;
            GUI.Label(new Rect(x + 12f, cy, 278f, 20f), EmfStorm.Scramble(ProgressLine()), body); cy += 24f;
            var hStyle = new GUIStyle(dim);
            hStyle.normal.textColor = stored >= targetCount ? DoneColor : DimColor;
            GUI.Label(new Rect(x + 12f, cy, 278f, 22f), EmfStorm.Scramble(HintLine()), hStyle); cy += 26f;

            if (ExtractionClock.Armed)
            {
                float r = ExtractionClock.Remaining;
                var clock = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                bool urgent = r <= 120f;
                clock.normal.textColor = urgent ? new Color(0.95f, 0.3f, 0.2f) : ThemeColor;
                if (!urgent || Mathf.Repeat(Time.unscaledTime, 1f) < 0.62f)
                {
                    int mm = (int)r / 60, ss = (int)r % 60;
                    string label = r <= 0f ? "撤离窗口 已关闭" : "撤离窗口 " + mm + ":" + ss.ToString("00");
                    GUI.Label(new Rect(x + 12f, cy, 278f, 20f), EmfStorm.Scramble(label), clock);
                }
                cy += 24f;
            }
        }

        var foot = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        foot.normal.textColor = DimColor;
        GUI.Label(new Rect(x + 12f, y + h - 22f, 278f, 18f), detail ? "[Tab] 收起" : "[Tab] 详情", foot);

        if (detail)
        {
            var rich = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            rich.normal.textColor = DimColor;
            GUI.Label(new Rect(x + 12f, cy + 2f, 278f, 66f), EmfStorm.Scramble(detailText), rich);
            GUI.Label(new Rect(x + 12f, y + h - 44f, 278f, 20f), EmfStorm.Scramble(rewardText), rich);
        }
    }

    private static string Mark(bool owned)
    {
        return owned ? "✓" : "—";
    }

    private string ProgressLine()
    {
        if (stored >= targetCount) return "订单完成 ✓（收容 " + stored + "/" + targetCount + "）";
        int done = stored + Mathf.Min(carrying, targetCount - stored);
        return "异常物 " + done + "/" + targetCount + (carrying > 0 ? "（携带 " + carrying + " 未入账）" : "");
    }

    private string HintLine()
    {
        if (stored >= targetCount) return "可继续探索，或经任意门返回";
        if (carrying > 0) return "回实验室——收容台按 E 入账";
        return "前往站台与车厢，寻找异常物";
    }

    /// <summary>1×1 实底纹理：GUI.Box 的默认纹理乘半透明色在暗场景几乎不可见。</summary>
    private Texture2D Backing
    {
        get
        {
            if (backing == null)
            {
                backing = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                backing.SetPixel(0, 0, new Color(0.01f, 0.02f, 0.03f, 0.82f));
                backing.Apply();
                backing.hideFlags = HideFlags.DontSave;
            }
            return backing;
        }
    }
}
