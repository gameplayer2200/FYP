using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具栏（屏幕底部中央）：显示拾取到的道具图片与状态。
///   手电筒——拾取后亮起；开灯（F）时暖色高亮 + 呼吸描边。
///   万用表——拾取后亮起（配电箱测线用）。
///   异常物——软盘图标 + 携带数角标（×N）；收容台上交后归零。
/// 鼠标滚轮 = 切换选中格（三格循环），选中格亮蓝环 + 弹跳反馈。
/// 未获得 = 槽位留空（不画图标）；获得瞬间一次弹跳动画。
///
/// 运行时自建：优先复用场景里的 InventoryCanvas（清掉旧 Slots），没有就自建——不依赖场景接线。
/// 图标：Assets/Resources/UI/Icons/（ItemIconBaker 烘焙；缺图时静默降级只显框）。
/// 状态来源：PlayerGear（Core）+ Inventory（本程序集），逐帧轮询差分刷新（对域重载最稳）。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // 尺寸（画布单位；整体大小调这里——1280×720 参考分辨率）
    private const float SlotSize = 86f;
    private const float SlotGap = 10f;
    private const float PadX = 17f;
    private const float PadY = 14f;
    private const float BarMargin = 26f;   // 右下角外边距
    private const float IconInner = 64f;

    // CRT 终端配色（磷光绿单色 + 暗底；与 OrderBoard / 电量条同一套 CrtTheme 色板）
    private static readonly Color PanelFill = new Color(0.010f, 0.030f, 0.020f, 0.90f);
    private static readonly Color PanelLine = new Color(0.47f, 1f, 0.76f, 0.40f);
    private static readonly Color SlotFill = new Color(0.47f, 1f, 0.76f, 0.05f);
    private static readonly Color SlotLine = new Color(0.47f, 1f, 0.76f, 0.22f);
    private static readonly Color SlotActiveFill = new Color(0.47f, 1f, 0.76f, 0.12f);
    private static readonly Color SlotActiveLine = new Color(0.47f, 1f, 0.76f, 0.85f);
    private static readonly Color SlotWarmFill = new Color(1f, 0.78f, 0.32f, 0.12f);
    private static readonly Color SlotWarmLine = new Color(1f, 0.78f, 0.32f, 0.75f);
    private static readonly Color FocusFill = new Color(0.47f, 1f, 0.76f, 0.05f);
    private static readonly Color FocusLine = new Color(0.55f, 1f, 0.82f, 0.90f);
    private static readonly Color IconWarm = new Color(1f, 0.90f, 0.70f, 1f);
    private static readonly Color BadgeColor = new Color(0.55f, 1f, 0.80f, 1f);

    private class ItemSlot
    {
        public RectTransform root;
        public Image focus;     // 选中环（最底层）
        public Image frame;
        public Image icon;
        public Text badge;      // 角标（异常物 ×N）
        public GameObject keycap; // 键帽（手电筒 F）
        public float pop;       // 弹跳动画剩余进度（1→0）
    }

    private ItemSlot flashSlot, meterSlot, specimenSlot;

    private bool built;
    private Sprite spPanel, spSlotIdle, spSlotActive, spSlotWarm, spKeycap, spFocus, spScan;
    private Sprite spriteFlash, spriteMeter, spriteFloppy;
    private Font font;
    private Canvas canvas;
    private CanvasScaler canvasScaler;

    private bool lastHasFlash, lastHasMeter, lastFlashOn;
    private int lastSpecimen = -1;
    private int lastSelected = -1;
    private int scaledForHeight = -1;   // 画布缩放比的生效屏幕高度（分辨率变化时重设 scaleFactor）

    private void OnEnable()
    {
        if (!built) Build();
        // 首帧就把状态推一遍（域重载后也靠它补正）
        lastHasFlash = lastHasMeter = lastFlashOn = false;
        lastSpecimen = -1;
        lastSelected = -1;
    }

    private void Update()
    {
        // 自愈：域重载/重建中断等边缘情况丢了引用时，整体重建而不是空引用报错
        if (!built || flashSlot == null || meterSlot == null || specimenSlot == null)
        {
            built = false;
            Build();
            if (!built) return;
            // 重建后下一帧全量刷新（角标/状态一次推齐）
            lastHasFlash = lastHasMeter = lastFlashOn = false;
            lastSpecimen = -1;
        }

        // 分辨率/窗口大小变化时同步画布缩放比（与 IMGUI 侧 CrtTheme.Scale 保持同一倍率）
        if (Screen.height != scaledForHeight)
        {
            scaledForHeight = Screen.height;
            if (canvasScaler != null) canvasScaler.scaleFactor = CrtTheme.Scale;
        }

        // 鼠标滚轮：切换选中格（三格快速栏循环；上滚=上一个，下滚=下一个）
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f) PlayerGear.CycleSelection(scroll > 0f ? -1 : 1);

        bool hasFlash = PlayerGear.HasFlashlight;
        bool hasMeter = PlayerGear.HasMultimeter;
        bool flashOn = PlayerGear.FlashlightOn;
        int specimens = Inventory.Instance != null ? Inventory.Instance.SpecimenCount : 0;
        int selected = PlayerGear.SelectedIndex;

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
        if (selected != lastSelected)
        {
            if (lastSelected != -1 && selected >= 0 && selected <= 2) SlotAt(selected).pop = 1f;
            lastSelected = selected;
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
        int selected = PlayerGear.SelectedIndex;
        SetFocus(flashSlot, selected == 0);
        SetFocus(meterSlot, selected == 1);
        SetFocus(specimenSlot, selected == 2);

        // 手电筒：未持有=槽位留空；持有=青色点亮；开灯=暖色 + 呼吸
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
        SetIcon(flashSlot, lastHasFlash ? spriteFlash : null, lastFlashOn ? IconWarm : Color.white);
        if (flashSlot.keycap != null) flashSlot.keycap.SetActive(lastHasFlash);

        // 万用表
        if (meterSlot.frame != null)
        {
            meterSlot.frame.sprite = lastHasMeter ? spSlotActive : spSlotIdle;
            meterSlot.frame.color = Color.white;
        }
        SetIcon(meterSlot, lastHasMeter ? spriteMeter : null, Color.white);

        // 异常物：有携带就点亮
        bool hasSpec = lastSpecimen > 0;
        if (specimenSlot.frame != null)
        {
            specimenSlot.frame.sprite = hasSpec ? spSlotActive : spSlotIdle;
            specimenSlot.frame.color = Color.white;
        }
        SetIcon(specimenSlot, hasSpec ? spriteFloppy : null, Color.white);
    }

    /// <summary>sprite 为空 = 还没获得：不画图标（槽位留空）。</summary>
    private static void SetIcon(ItemSlot s, Sprite sprite, Color color)
    {
        if (s.icon == null) return;
        if (s.icon.sprite != sprite) s.icon.sprite = sprite;
        s.icon.color = sprite != null ? color : new Color(1f, 1f, 1f, 0f);
    }

    private static void SetFocus(ItemSlot s, bool on)
    {
        if (s.focus != null && s.focus.enabled != on) s.focus.enabled = on;
    }

    private ItemSlot SlotAt(int index)
    {
        return index == 0 ? flashSlot : (index == 1 ? meterSlot : specimenSlot);
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
            canvasGO.AddComponent<CanvasScaler>();   // 参数统一在下面归一化
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // 幂等：清掉旧结构与旧版 Slots（连线在场景里的老槽位）
        var oldBar = canvas.transform.Find("ItemBar");
        if (oldBar != null) Destroy(oldBar.gameObject);
        var oldSlots = canvas.transform.Find("Slots");
        if (oldSlots != null) Destroy(oldSlots.gameObject);

        // 统一画布缩放策略：与 IMGUI 侧共用 CrtTheme.Scale（720p 基准整数倍）。
        // 用 ConstantPixelSize 而不是 ScaleWithScreenSize——缩放比直接等于屏幕倍率，
        // 字形按设备像素渲染后整体放大，1080p/4K 下不会出现半像素发虚（"分辨率低"的根因）。
        canvasScaler = canvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null) canvasScaler = canvas.gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.scaleFactor = CrtTheme.Scale;
        canvasScaler.referencePixelsPerUnit = 100f;
        scaledForHeight = Screen.height;

        // 程序化 Sprite（直角九宫格 —— 终端不用圆角；无美术资产依赖）
        spPanel = Sharp(PanelFill, PanelLine);
        spSlotIdle = Sharp(SlotFill, SlotLine);
        spSlotActive = Sharp(SlotActiveFill, SlotActiveLine);
        spSlotWarm = Sharp(SlotWarmFill, SlotWarmLine);
        spFocus = Sharp(FocusFill, FocusLine);
        spKeycap = Sharp(new Color(0.03f, 0.07f, 0.05f, 0.95f), new Color(0.47f, 1f, 0.76f, 0.35f));
        spScan = ScanStrip();
        spriteFlash = LoadIcon("flashlight");
        spriteMeter = LoadIcon("multimeter");
        spriteFloppy = LoadIcon("floppy");
        font = GetFont();

        // 条体（右下角：与左下角电量条同处底部，但左右分开——任何宽高比都不打架）
        var barGO = new GameObject("ItemBar", typeof(RectTransform));
        barGO.transform.SetParent(canvas.transform, false);
        var barRT = (RectTransform)barGO.transform;
        barRT.anchorMin = new Vector2(1f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot = new Vector2(1f, 0f);
        barRT.anchoredPosition = new Vector2(-BarMargin, BarMargin);
        float width = PadX * 2f + SlotSize * 3f + SlotGap * 2f;
        float height = PadY * 2f + SlotSize;
        barRT.sizeDelta = new Vector2(width, height);

        var panel = MakeImage(barGO.transform, "Panel", spPanel);
        Stretch(panel.rectTransform);

        // 扫描线层（终端质感）：1×3 条纹平铺，压在面板之上、槽位之下
        var scan = MakeImage(barGO.transform, "ScanLines", spScan);
        scan.rectTransform.SetSiblingIndex(1);
        var srt = scan.rectTransform;
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(2f, 2f);
        srt.offsetMax = new Vector2(-2f, -2f);
        scan.type = Image.Type.Tiled;
        scan.pixelsPerUnitMultiplier = 100f;
        scan.color = new Color(0f, 0f, 0f, 0.30f);

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

        // 选中环（比槽位大一圈，垫在底层）
        slot.focus = MakeImage(go.transform, "Focus", spFocus);
        var frt = slot.focus.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.pivot = new Vector2(0.5f, 0.5f);
        frt.offsetMin = new Vector2(-5f, -5f);
        frt.offsetMax = new Vector2(5f, 5f);
        slot.focus.enabled = false;

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
            text.color = new Color(0.55f, 1f, 0.80f, 0.95f);
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
        // CRT 主题的等宽字体（Noto Sans Mono CJK SC）；缺字体时由 CrtTheme 逐级降级
        var f = CrtTheme.Mono;
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    /// <summary>程序化直角矩形（1px 描边，Point 采样保持像素锐利），九宫格切片用——终端不用圆角。</summary>
    private static Sprite Sharp(Color fill, Color line)
    {
        const int size = 12;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                pixels[y * size + x] = edge ? (Color32)line : (Color32)fill;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(2f, 2f, 2f, 2f));
    }

    /// <summary>1×3 扫描线条纹（一行暗、两行透明），配合 Image.Tiled 平铺出 CRT 扫描线。</summary>
    private static Sprite ScanStrip()
    {
        var tex = new Texture2D(1, 3, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 1f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, 3f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
