using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具栏（屏幕底部中央）：显示拾取到的道具图片与状态。
///   手电筒——拾取后亮起；开灯（F）时暖色高亮 + 呼吸描边。
///   万用表——拾取后亮起（配电箱测线用）。
///   异常物——软盘图标 + 携带数角标（×N）；收容台上交后归零。
/// 未获得 = 低透明暗刻；获得瞬间一次弹跳动画。
///
/// 运行时自建：优先复用场景里的 InventoryCanvas（清掉旧 Slots），没有就自建——不依赖场景接线。
/// 图标：Assets/Resources/UI/Icons/（ItemIconBaker 烘焙；缺图时静默降级只显框）。
/// 状态来源：PlayerGear（Core）+ Inventory（本程序集），逐帧轮询差分刷新（对域重载最稳）。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    private const float SlotSize = 100f;
    private const float SlotGap = 12f;
    private const float PadX = 20f;
    private const float PadY = 16f;
    private const float BarBottom = 30f;
    private const float IconInner = 76f;

    // 主题色（与 OrderBoard 同一套深底青调）
    private static readonly Color PanelFill = new Color(0.043f, 0.055f, 0.067f, 0.82f);
    private static readonly Color PanelLine = new Color(0.22f, 0.86f, 0.95f, 0.16f);
    private static readonly Color SlotFill = new Color(1f, 1f, 1f, 0.045f);
    private static readonly Color SlotLine = new Color(1f, 1f, 1f, 0.10f);
    private static readonly Color SlotActiveFill = new Color(0.22f, 0.86f, 0.95f, 0.10f);
    private static readonly Color SlotActiveLine = new Color(0.22f, 0.86f, 0.95f, 0.55f);
    private static readonly Color SlotWarmFill = new Color(1f, 0.82f, 0.55f, 0.10f);
    private static readonly Color SlotWarmLine = new Color(1f, 0.82f, 0.55f, 0.60f);
    private static readonly Color IconDim = new Color(0.62f, 0.68f, 0.74f, 0.40f);
    private static readonly Color IconWarm = new Color(1f, 0.95f, 0.82f, 1f);
    private static readonly Color BadgeColor = new Color(0.55f, 0.92f, 1f, 1f);

    private class ItemSlot
    {
        public RectTransform root;
        public Image frame;
        public Image icon;
        public Text badge;      // 角标（异常物 ×N）
        public GameObject keycap; // 键帽（手电筒 F）
        public float pop;       // 弹跳动画剩余进度（1→0）
    }

    private ItemSlot flashSlot, meterSlot, specimenSlot;

    private bool built;
    private Sprite spPanel, spSlotIdle, spSlotActive, spSlotWarm, spKeycap;
    private Sprite spriteFlash, spriteMeter, spriteFloppy;
    private Font font;

    private bool lastHasFlash, lastHasMeter, lastFlashOn;
    private int lastSpecimen = -1;

    private void OnEnable()
    {
        if (!built) Build();
        // 首帧就把状态推一遍（域重载后也靠它补正）
        lastHasFlash = lastHasMeter = lastFlashOn = false;
        lastSpecimen = -1;
    }

    private void Update()
    {
        if (!built) return;

        bool hasFlash = PlayerGear.HasFlashlight;
        bool hasMeter = PlayerGear.HasMultimeter;
        bool flashOn = PlayerGear.FlashlightOn;
        int specimens = Inventory.Instance != null ? Inventory.Instance.SpecimenCount : 0;

        if (hasFlash != lastHasFlash) { lastHasFlash = hasFlash; if (hasFlash) flashSlot.pop = 1f; }
        if (hasMeter != lastHasMeter) { lastHasMeter = hasMeter; if (hasMeter) meterSlot.pop = 1f; }
        if (specimens != lastSpecimen)
        {
            lastSpecimen = specimens;
            if (specimens > 0) specimenSlot.pop = 1f;
            if (specimenSlot.badge != null)
            {
                specimenSlot.badge.text = "×" + specimens;
                specimenSlot.badge.enabled = specimens > 0;
            }
        }
        lastFlashOn = flashOn;

        RefreshAppearance();
        Animate(flashSlot);
        Animate(meterSlot);
        Animate(specimenSlot);
    }

    // ─────────────────────────────────────────────────────────────
    // 外观刷新
    // ─────────────────────────────────────────────────────────────

    private void RefreshAppearance()
    {
        // 手电筒：未持有=暗刻；持有=青色点亮；开灯=暖色 + 呼吸
        if (flashSlot.frame != null)
        {
            Sprite s = !lastHasFlash ? spSlotIdle : (lastFlashOn ? spSlotWarm : spSlotActive);
            flashSlot.frame.sprite = s;
            var c = Color.white;
            if (lastHasFlash && lastFlashOn)
            {
                float breathe = 0.82f + 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3.4f));
                c = new Color(1f, 1f, 1f, breathe);
            }
            flashSlot.frame.color = c;
        }
        SetIcon(flashSlot, spriteFlash, !lastHasFlash ? IconDim : (lastFlashOn ? IconWarm : Color.white));
        if (flashSlot.keycap != null) flashSlot.keycap.SetActive(lastHasFlash);

        // 万用表
        if (meterSlot.frame != null)
        {
            meterSlot.frame.sprite = lastHasMeter ? spSlotActive : spSlotIdle;
            meterSlot.frame.color = Color.white;
        }
        SetIcon(meterSlot, spriteMeter, lastHasMeter ? Color.white : IconDim);

        // 异常物：有携带就点亮
        bool hasSpec = lastSpecimen > 0;
        if (specimenSlot.frame != null)
        {
            specimenSlot.frame.sprite = hasSpec ? spSlotActive : spSlotIdle;
            specimenSlot.frame.color = Color.white;
        }
        SetIcon(specimenSlot, spriteFloppy, hasSpec ? Color.white : IconDim);
    }

    private static void SetIcon(ItemSlot s, Sprite sprite, Color color)
    {
        if (s.icon == null) return;
        if (s.icon.sprite != sprite) s.icon.sprite = sprite;
        s.icon.color = color;
    }

    private static void Animate(ItemSlot s)
    {
        if (s.root == null) return;
        if (s.pop <= 0f)
        {
            if (s.root.localScale != Vector3.one) s.root.localScale = Vector3.one;
            return;
        }
        s.pop = Mathf.Max(0f, s.pop - Time.unscaledDeltaTime / 0.42f);
        float t = 1f - s.pop;
        float k = 1f + 0.20f * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.55f);
        s.root.localScale = new Vector3(k, k, 1f);
    }

    // ─────────────────────────────────────────────────────────────
    // 构建（运行时自建 UI）
    // ─────────────────────────────────────────────────────────────

    private void Build()
    {
        var canvasGO = GameObject.Find("InventoryCanvas");
        Canvas canvas;
        if (canvasGO != null && canvasGO.GetComponent<Canvas>() != null)
        {
            canvas = canvasGO.GetComponent<Canvas>();
        }
        else
        {
            canvasGO = new GameObject("InventoryCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // 幂等：清掉旧结构与旧版 Slots（连线在场景里的老槽位）
        var oldBar = canvas.transform.Find("ItemBar");
        if (oldBar != null) Destroy(oldBar.gameObject);
        var oldSlots = canvas.transform.Find("Slots");
        if (oldSlots != null) Destroy(oldSlots.gameObject);

        // 程序化 Sprite（圆角九宫格；无美术资产依赖）
        spPanel = Rounded(18, PanelFill, PanelLine, 1.5f);
        spSlotIdle = Rounded(12, SlotFill, SlotLine, 1.5f);
        spSlotActive = Rounded(12, SlotActiveFill, SlotActiveLine, 1.5f);
        spSlotWarm = Rounded(12, SlotWarmFill, SlotWarmLine, 1.5f);
        spKeycap = Rounded(5, new Color(0.07f, 0.09f, 0.11f, 0.92f), new Color(1f, 1f, 1f, 0.22f), 1.5f);
        spriteFlash = LoadIcon("flashlight");
        spriteMeter = LoadIcon("multimeter");
        spriteFloppy = LoadIcon("floppy");
        font = GetFont();

        // 条体
        var barGO = new GameObject("ItemBar", typeof(RectTransform));
        barGO.transform.SetParent(canvas.transform, false);
        var barRT = (RectTransform)barGO.transform;
        barRT.anchorMin = new Vector2(0.5f, 0f);
        barRT.anchorMax = new Vector2(0.5f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, BarBottom);
        float width = PadX * 2f + SlotSize * 3f + SlotGap * 2f;
        float height = PadY * 2f + SlotSize;
        barRT.sizeDelta = new Vector2(width, height);

        var panel = MakeImage(barGO.transform, "Panel", spPanel);
        Stretch(panel.rectTransform);

        flashSlot = MakeSlot(barRT, 0);
        meterSlot = MakeSlot(barRT, 1);
        specimenSlot = MakeSlot(barRT, 2);

        // 手电筒键帽（F）
        flashSlot.keycap = MakeKeycap(flashSlot.root, "F");
        flashSlot.keycap.SetActive(false);

        // 异常物角标
        specimenSlot.badge = MakeBadge(specimenSlot.root);
        specimenSlot.badge.enabled = false;

        built = true;
        RefreshAppearance();
    }

    private ItemSlot MakeSlot(RectTransform parent, int index)
    {
        var slot = new ItemSlot();
        var go = new GameObject("Slot" + (index + 1), typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);
        rt.anchoredPosition = new Vector2((index - 1) * (SlotSize + SlotGap), 0f);
        slot.root = rt;

        slot.frame = MakeImage(go.transform, "Frame", spSlotIdle);
        Stretch(slot.frame.rectTransform);

        slot.icon = MakeImage(go.transform, "Icon", null);
        var iconRT = slot.icon.rectTransform;
        iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.sizeDelta = new Vector2(IconInner, IconInner);
        iconRT.anchoredPosition = Vector2.zero;
        slot.icon.preserveAspect = true;

        return slot;
    }

    private GameObject MakeKeycap(RectTransform slot, string label)
    {
        var go = new GameObject("Keycap", typeof(RectTransform));
        go.transform.SetParent(slot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(20f, 18f);
        rt.anchoredPosition = new Vector2(-3f, 3f);

        var img = MakeImage(go.transform, "Bg", spKeycap);
        Stretch(img.rectTransform);

        if (font != null)
        {
            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.85f, 0.95f, 1f, 0.95f);
            text.raycastTarget = false;
            Stretch((RectTransform)textGO.transform);
        }
        return go;
    }

    private Text MakeBadge(RectTransform slot)
    {
        var go = new GameObject("Badge", typeof(RectTransform));
        go.transform.SetParent(slot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(44f, 20f);
        rt.anchoredPosition = new Vector2(-4f, 4f);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = "×0";
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.LowerRight;
        text.color = BadgeColor;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    // ─────────────────────────────────────────────────────────────
    // 小工具
    // ─────────────────────────────────────────────────────────────

    private static Image MakeImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        if (sprite == null) img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Sprite LoadIcon(string name)
    {
        var s = Resources.Load<Sprite>("UI/Icons/" + name);
        return s;
    }

    private static Font GetFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    /// <summary>程序化圆角矩形（圆角 + 描边，SDF 抗锯齿），九宫格切片用。</summary>
    private static Sprite Rounded(int radius, Color fill, Color line, float lineWidth)
    {
        int size = radius * 2 + 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;
                float bx = half - 1.25f - radius;
                float by = half - 1.25f - radius;
                float qx = Mathf.Abs(px) - bx;
                float qy = Mathf.Abs(py) - by;
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float sd = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                Color c;
                if (sd > 0.5f) c = new Color(0f, 0f, 0f, 0f);
                else if (sd > -lineWidth) c = line;
                else c = fill;
                c.a *= Mathf.Clamp01(0.5f - sd);   // 外缘 1px 抗锯齿
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
    }
}
