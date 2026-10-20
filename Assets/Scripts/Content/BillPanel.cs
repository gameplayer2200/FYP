using UnityEngine;

/// <summary>
/// 电费账单（结算演出）：收容台入账时居中弹出约 6 秒——照明/环境/机动/受创
/// 分类耗电 + 合计（占满电百分比）+ 机器人味结语。数据读 BatterySystem 分类记账。
/// CRT 终端风格（CrtTheme）；双世界各挂一份时先到者独占渲染。
/// </summary>
public class BillPanel : MonoBehaviour
{
    private static readonly string[] Closing =
    {
        "感谢您为门阈行动节省的每一瓦特。",
        "本记录已同步至收容数据库。",
        "节能评级：合格调查员。",
        "电费不会真的从您身上扣除。大概。",
        "下次进站前，记得充满电。",
    };

    private static BillPanel renderOwner;

    private float showUntil;
    private float shownAt;
    private string closing = "";

    private void Awake()
    {
        if (renderOwner != null && renderOwner != this) return;
        renderOwner = this;
    }

    private void OnDestroy()
    {
        if (renderOwner == this) renderOwner = null;
        Inventory.OnDeposit -= Show;
    }

    private void Start()
    {
        Inventory.OnDeposit += Show;
    }

    private void Show(int count)
    {
        shownAt = Time.unscaledTime;
        showUntil = shownAt + 6.5f;
        closing = Closing[Random.Range(0, Closing.Length)];
    }

    private void OnGUI()
    {
        if (renderOwner != null && renderOwner != this) return;
        if (Time.unscaledTime >= showUntil) return;

        float W = CrtTheme.Px(360f);
        float H = CrtTheme.Px(196f);
        var panel = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.42f, W, H);

        float a = Mathf.Clamp01((showUntil - Time.unscaledTime) / 0.6f); // 收尾淡出
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, a);

        CrtTheme.Panel(panel, "FYP // 结算 · 电费账单");
        float x = panel.x + CrtTheme.Px(14f);
        float y = panel.y + CrtTheme.Px(28f);
        float w = panel.width - CrtTheme.Px(28f);

        GUI.Label(new Rect(x, y, w, CrtTheme.Px(18f)), "照明（手电筒）  " + BillingLedger.Lighting.ToString("F0") + " 电", CrtTheme.Style(13, CrtTheme.Ink, false));
        y += CrtTheme.Px(20f);
        GUI.Label(new Rect(x, y, w, CrtTheme.Px(18f)), "环境（层域规则）  " + BillingLedger.Ambient.ToString("F0") + " 电", CrtTheme.Style(13, CrtTheme.Ink, false));
        y += CrtTheme.Px(20f);
        GUI.Label(new Rect(x, y, w, CrtTheme.Px(18f)), "机动（技能）  " + BillingLedger.Device.ToString("F0") + " 电", CrtTheme.Style(13, CrtTheme.Ink, false));
        y += CrtTheme.Px(20f);
        GUI.Label(new Rect(x, y, w, CrtTheme.Px(18f)), "受创  " + BillingLedger.Hit.ToString("F0") + " 电", CrtTheme.Style(13, CrtTheme.Warn, false));
        y += CrtTheme.Px(24f);

        CrtTheme.Fill(new Rect(x, y, w, Mathf.Max(1f, Mathf.Round(CrtTheme.Scale))), new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.25f));
        y += CrtTheme.Px(8f);

        // Capacity 由 BatterySystem 启动上报；异常时序下可能未就绪——防御除零（NaN 转 int 会变 int.MinValue）
        int pct = BillingLedger.Capacity > 0.01f
            ? Mathf.RoundToInt(BillingLedger.Total / BillingLedger.Capacity * 100f)
            : 0;
        string total = "合计  " + BillingLedger.Total.ToString("F0") + " 电（满电量的 " + pct + "%）";
        GUI.Label(new Rect(x, y, w, CrtTheme.Px(18f)), total, CrtTheme.Style(13, CrtTheme.Done, false, true));
        y += CrtTheme.Px(24f);

        GUI.Label(new Rect(x, y, w, CrtTheme.Px(18f)), "> " + closing, CrtTheme.Style(12, CrtTheme.InkDim, false));

        GUI.color = prev;
    }
}
