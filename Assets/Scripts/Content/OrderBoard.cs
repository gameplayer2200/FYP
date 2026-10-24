using UnityEngine;

/// <summary>
/// 接单任务板（右上角 HUD，按世界切换形态——读 Core 的 WorldPresence）：
///  · 实验室（起始世界）：整备视图——收容档案、随身装备清单、出发提示；
///  · 层域（异世界）：订单视图——目标进度、下一步提示、撤离窗口倒计时（≤120s 红闪）。
/// Tab 展开详情；收容入账即完成订单（绿色提示）。吸收了旧 SpecimenHud 的计数职能。
///
/// 外观：CRT 终端面板（CrtTheme）——等宽字形、磷光绿、扫描线、"> " 提示符、方块光标。
/// 风暴中经 EmfStorm 乱码化（乱码字符集只含块符号，不会破坏富文本）。
/// </summary>
public class OrderBoard : MonoBehaviour
{
    [Tooltip("本局要求收容入账的异常物数量")]
    [SerializeField] private int targetCount = 2;
    [SerializeField] private string rewardText = "报酬：记忆核心配额 + 续期";
    [SerializeField] private string detailText = "目标散落在站台与车厢深处。\n带回实验室收容台按 E 入账，才算完成订单。\n瘫倒重启会散失一半随身异常物。";
    [SerializeField] private KeyCode detailKey = KeyCode.Tab;

    private int carrying;
    private int stored;
    private bool detail;
    private float detailShownAt;

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
        if (Input.GetKeyDown(detailKey))
        {
            detail = !detail;
            detailShownAt = Time.unscaledTime;
        }
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
            case "Shrine_game": return "荒废神社";
            case "OfficePack_game": return "废弃办公层";
            default: return string.IsNullOrEmpty(sceneName) ? "层域" : sceneName;
        }
    }

    private void OnGUI()
    {
        if (renderOwner != null && renderOwner != this) return; // 另一份已在画
        renderOwner = this;

        bool inHome = WorldPresence.InHome;
        bool armed = !inHome && ExtractionClock.Armed;
        bool shiftLine = !inHome && IsFactoryWorld(WorldPresence.Current);

        // 详情区高度：实验室形态 = 收容档案（按条数扩展）；层域形态 = 订单详情
        var inv = Inventory.Instance;
        int archCount = inHome && detail && inv != null ? inv.Archive.Count : 0;
        float detailH = inHome ? CrtTheme.Px(46f + Mathf.Max(1, archCount) * 20f + 18f) : CrtTheme.Px(112f);

        float W = CrtTheme.Px(340f);
        float h = CrtTheme.Px(30f);                      // 标题行 + 分隔线
        h += inHome ? CrtTheme.Px(96f) : CrtTheme.Px(72f); // 正文行
        if (armed) h += CrtTheme.Px(26f);                // 撤离窗口
        if (shiftLine) h += CrtTheme.Px(26f);            // 工厂排班钟
        h += CrtTheme.Px(22f);                           // 底栏（[Tab]）
        if (detail) h += detailH;                        // 详情区

        var j = EmfStorm.Jitter();
        var panel = new Rect(Screen.width - W - CrtTheme.Px(22f) + j.x, CrtTheme.Px(26f) + j.y, W, h);

        CrtTheme.Panel(panel, "FYP // 终端 " + (inHome ? "LAB" : "FIELD"));
        HeaderLamp(panel, inHome, armed);

        float x = panel.x + CrtTheme.Px(12f);
        float y = panel.y + CrtTheme.Px(27f);
        float w = panel.width - CrtTheme.Px(24f);

        if (inHome)
        {
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(20f)), Scramble("> 实验室 · 整备"), CrtTheme.Style(14, CrtTheme.Ink, false));
            y += CrtTheme.Px(24f);
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(19f)), Scramble("  已收容异常物 " + stored + " / 订单目标 " + targetCount
                + (carrying > 0 ? "  · 携带 " + carrying : "")), CrtTheme.Style(13, CrtTheme.Ink, false));
            y += CrtTheme.Px(21f);
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(19f)), Scramble("  装备 手电筒 " + Mark(PlayerGear.HasFlashlight)
                + " · 万用表 " + Mark(PlayerGear.HasMultimeter)
                + " · 探测器 " + Mark(PlayerGear.HasEmfDetector)), CrtTheme.Style(13, CrtTheme.Ink, false));
            y += CrtTheme.Px(23f);

            bool done = stored >= targetCount;
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(19f)), Scramble(done
                ? "> 订单完成 ✓ 休整后可再次出发"
                : "> 从任意一扇门出发，深入层域回收"), CrtTheme.Style(13, done ? CrtTheme.Done : CrtTheme.InkDim, false));
            y += CrtTheme.Px(22f);
        }
        else
        {
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(20f)), Scramble("> 订单：" + WorldDisplay(WorldPresence.Current) + " 回收"), CrtTheme.Style(14, CrtTheme.Ink, false));
            y += CrtTheme.Px(24f);
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(19f)), Scramble("  " + ProgressLine()), CrtTheme.Style(13, CrtTheme.Ink, false));
            y += CrtTheme.Px(21f);

            bool done = stored >= targetCount;
            GUI.Label(new Rect(x, y, w, CrtTheme.Px(19f)), Scramble("> " + HintLine()), CrtTheme.Style(13, done ? CrtTheme.Done : CrtTheme.InkDim, false));
            y += CrtTheme.Px(21f);

            // 工厂专属：排班钟状态（换班倒计时 / 已锁定——数据源 FactoryShift，Core 静态桥）
            if (shiftLine)
            {
                string text;
                if (FactoryShift.PuzzleSolved)
                {
                    text = FactoryShift.LockedPhase.HasValue
                        ? "  排班钟已停——锁定" + (FactoryShift.LockedPhase == FactoryShift.Phase.Day ? "白班" : "夜班") + "（排班表可改锁）"
                        : "  三台机器已就绪——回排班表锁定昼夜";
                }
                else
                {
                    float s = Mathf.Max(0f, FactoryShift.TimeLeft);
                    int mm = (int)s / 60, ss = (int)s % 60;
                    text = "  换班 " + mm + ":" + ss.ToString("00") + " → "
                        + (FactoryShift.Current == FactoryShift.Phase.Day ? "夜班" : "白班") + "（歇班的世界死寂）";
                }
                GUI.Label(new Rect(x, y, w, CrtTheme.Px(19f)), Scramble(text), CrtTheme.Style(13, CrtTheme.Warn, false));
                y += CrtTheme.Px(24f);
            }

            if (armed)
            {
                float r = ExtractionClock.Remaining;
                bool urgent = r <= 120f;
                bool visible = !urgent || Mathf.Repeat(Time.unscaledTime, 1f) < 0.62f;
                if (visible)
                {
                    int mm = (int)r / 60, ss = (int)r % 60;
                    string label = r <= 0f ? "  撤离窗口 已关闭" : "  撤离窗口 " + mm + ":" + ss.ToString("00");
                    GUI.Label(new Rect(x, y, w, CrtTheme.Px(19f)), Scramble(label), CrtTheme.Style(13, urgent ? CrtTheme.Danger : CrtTheme.Warn, false));
                }
                y += CrtTheme.Px(24f);
            }
        }

        // 底栏：[Tab] 提示 + 闪烁方块光标
        GUI.Label(new Rect(x, panel.yMax - CrtTheme.Px(20f), w, CrtTheme.Px(16f)), detail ? "[Tab] 收起" : "[Tab] 详情", CrtTheme.Style(11, CrtTheme.InkFaint, false));
        CrtTheme.Cursor(panel.xMax - CrtTheme.Px(22f), panel.yMax - CrtTheme.Px(19f), 7f, 12f, 0.75f);

        if (detail)
        {
            float dy = panel.yMax - detailH;
            CrtTheme.Fill(new Rect(panel.x + CrtTheme.Px(6f), dy, panel.width - CrtTheme.Px(12f), Mathf.Max(1f, Mathf.Round(CrtTheme.Scale))), new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.18f));

            if (inHome)
            {
                // 收容档案（图鉴）：#001 起逐条档案卡
                GUI.Label(new Rect(x, dy + CrtTheme.Px(8f), w, CrtTheme.Px(18f)),
                    Scramble("> 收容档案（" + (inv != null ? inv.Archive.Count : 0) + "）"),
                    CrtTheme.Style(13, CrtTheme.Ink, false));
                float ay = dy + CrtTheme.Px(30f);
                if (inv != null && inv.Archive.Count > 0)
                {
                    for (int i = 0; i < inv.Archive.Count; i++)
                    {
                        var arch = inv.Archive[i];
                        string name = string.IsNullOrEmpty(arch.name) ? "未登记样本" : arch.name;
                        string flavor = string.IsNullOrEmpty(arch.flavor) ? "" : " — " + arch.flavor;
                        GUI.Label(new Rect(x, ay, w, CrtTheme.Px(18f)),
                            Scramble("#" + (i + 1).ToString("000") + " " + name),
                            CrtTheme.Style(12, CrtTheme.Ink, false));
                        if (!string.IsNullOrEmpty(flavor))
                        {
                            GUI.Label(new Rect(x + CrtTheme.Px(34f), ay + CrtTheme.Px(2f), w - CrtTheme.Px(34f), CrtTheme.Px(16f)),
                                Scramble(flavor), CrtTheme.Style(11, CrtTheme.InkFaint, false));
                        }
                        ay += CrtTheme.Px(20f);
                    }
                }
                else
                {
                    GUI.Label(new Rect(x, ay, w, CrtTheme.Px(18f)), Scramble("（暂无收容记录——深入层域回收异常物）"),
                        CrtTheme.Style(12, CrtTheme.InkFaint, false));
                }
            }
            else
            {
                string body = EmfStorm.Scramble(DetailBody());
                body = CrtTheme.Reveal(body, Time.unscaledTime - detailShownAt, 90f);
                GUI.Label(new Rect(x, dy + CrtTheme.Px(8f), w, CrtTheme.Px(52f)), body, CrtTheme.Style(12, CrtTheme.InkDim));
                GUI.Label(new Rect(x, dy + CrtTheme.Px(62f), w, CrtTheme.Px(16f)), EmfStorm.Scramble("> " + rewardText), CrtTheme.Style(12, CrtTheme.InkFaint, false));
            }
        }
    }

    /// <summary>标题右侧的状态灯：实验室=常亮，层域武装=慢闪，未武装=暗。</summary>
    private static void HeaderLamp(Rect panel, bool inHome, bool armed)
    {
        bool on = inHome || (armed && CrtTheme.Blink(1.6f, 0.5f));
        var c = on ? CrtTheme.Ink : CrtTheme.InkFaint;
        float lx = panel.xMax - CrtTheme.Px(20f);
        float ly = panel.y + CrtTheme.Px(6f);
        CrtTheme.Fill(new Rect(lx, ly, CrtTheme.Px(7f), CrtTheme.Px(7f)), new Color(c.r, c.g, c.b, on ? 0.95f : 0.35f));
    }

    private static string Scramble(string s)
    {
        return EmfStorm.Scramble(s);
    }

    private static string Mark(bool owned)
    {
        return owned ? "✓" : "─";
    }

    private string ProgressLine()
    {
        if (stored >= targetCount) return "订单完成 ✓（收容 " + stored + "/" + targetCount + "）";
        int done = stored + Mathf.Min(carrying, targetCount - stored);
        return "异常物 " + done + "/" + targetCount + (carrying > 0 ? "（携带 " + carrying + " 未入账）" : "");
    }

    private static bool IsFactoryWorld(string sceneName)
    {
        return sceneName == "FactoryDay" || sceneName == "FactoryNight";
    }

    /// <summary>订单详情按世界区分：工厂讲班次规则，Metro 讲站台动线（serialized detailText）。</summary>
    private string DetailBody()
    {
        if (IsFactoryWorld(WorldPresence.Current))
        {
            return "排班钟每 4 分钟换班，歇班的世界死寂：机器停摆、深区门锁。\n"
                + "白班机器可启动但深区门锁死；夜班门全开但异常显形。\n"
                + "按值班室排班表顺序启动 3 台机器，可锁定想要的昼夜（冷却 60 秒可改）。";
        }
        return detailText;
    }

    private string HintLine()
    {
        if (stored >= targetCount) return "可继续探索，或经任意门返回";
        if (carrying > 0) return "回实验室——收容台按 E 入账";
        if (IsFactoryWorld(WorldPresence.Current))
        {
            return FactoryShift.PuzzleSolved
                ? "昼夜已锁定——回收异常物，经工厂·昼返回"
                : "按值班室排班表顺序启动 3 台机器，再回收异常物";
        }
        return "前往站台与车厢，寻找异常物";
    }
}
