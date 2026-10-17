using UnityEngine;

/// <summary>
/// 接单任务板（右上角 HUD）：本局订单——回收目标、实时进度、下一步提示。
/// 携带/收容经 Inventory 事件刷新；Tab 展开详情。吸收了旧 SpecimenHud 的计数职能。
/// OnGUI 直绘，与电量条风格一致；风暴中经 EmfStorm 乱码化。
/// </summary>
public class OrderBoard : MonoBehaviour
{
    [SerializeField] private string orderTitle = "订单：Metro 站回收";
    [Tooltip("本局要求收容入账的异常物数量")]
    [SerializeField] private int targetCount = 2;
    [SerializeField] private string rewardText = "报酬：记忆核心配额 + 续期";
    [SerializeField] private string detailText = "目标散落在站台与车厢深处。\n带回实验室收容台按 E 入账，才算完成订单。";
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

    private void OnGUI()
    {
        if (renderOwner != null && renderOwner != this) return; // 另一份已在画
        renderOwner = this;

        var j = EmfStorm.Jitter();
        float x = Screen.width - 322f + j.x;
        float y = 40f + j.y;
        float h = detail ? 178f : 88f;

        var prev = GUI.color;
        GUI.DrawTexture(new Rect(x, y, 302f, h), Backing);
        GUI.color = prev;

        var title = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        title.normal.textColor = ThemeColor;
        GUI.Label(new Rect(x + 12f, y + 8f, 278f, 22f), EmfStorm.Scramble(orderTitle), title);

        var body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        body.normal.textColor = ThemeColor;
        GUI.Label(new Rect(x + 12f, y + 34f, 278f, 20f), EmfStorm.Scramble(ProgressLine()), body);

        var hint = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        hint.normal.textColor = stored >= targetCount ? DoneColor : DimColor;
        GUI.Label(new Rect(x + 12f, y + 56f, 278f, 24f), EmfStorm.Scramble(HintLine()), hint);

        var foot = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        foot.normal.textColor = DimColor;
        GUI.Label(new Rect(x + 12f, y + h - 22f, 278f, 18f), detail ? "[Tab] 收起" : "[Tab] 详情", foot);

        if (detail)
        {
            var rich = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            rich.normal.textColor = DimColor;
            GUI.Label(new Rect(x + 12f, y + 84f, 278f, 70f), EmfStorm.Scramble(detailText), rich);
            GUI.Label(new Rect(x + 12f, y + 132f, 278f, 20f), EmfStorm.Scramble(rewardText), rich);
        }
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
}
