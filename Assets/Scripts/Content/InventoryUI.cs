using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具栏（屏幕底部中央）：五格，只放可用的探索道具。
///   0 手电筒——拾取后亮起；开灯时暖色高亮 + 呼吸描边。
///   1 万用表——拾取后亮起（配电箱测线用）。
///   2 探测器——拾取后亮起（把异常扫显形）。
///   3 任务提示板——拾取后亮起；开机时暖色高亮（右上角订单 HUD 随之显示）。
///   4 备用空位——留给后续道具，现在恒为空。
/// 鼠标滚轮 = 切换选中格（五格循环），选中格亮选中环 + 弹跳反馈。
/// F = 只用选中格那一件（见 PlayerGear.UseKeyOwner），一次按键只触发一件道具。
/// 随身异常物不再占格位，数量显示在右上角订单板（整备视图"携带 N" / 层域视图进度行）。
///
/// 运行时自建：优先复用场景里的 InventoryCanvas（清掉旧 Slots），没有就自建——不依赖场景接线。
/// 图标：Assets/Resources/UI/Icons/（ItemIconBaker 烘焙；缺图时静默降级只显框）。
/// 状态来源：PlayerGear（Core）+ Inventory（本程序集），逐帧轮询差分刷新（对域重载最稳）。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // 尺寸（画布单位；整体大小调这里——1280×720 参考分辨率）
    private const float SlotSize = 68f;
    private const float SlotGap = 8f;
    private const float PadX = 12f;
    private const float PadY = 10f;
    private const float BarMargin = 22f;   // 屏幕正下方外边距
    private const float IconInner = 50f;
    // 格数直接跟 Core 的 PlayerGear 走，避免两处各写一个数字而漂移。
    private const int SlotCount = PlayerGear.SlotCount;

    // CRT 终端配色（磷光绿单色 + 暗底；与 OrderBoard / 电量条同一套 CrtTheme 色板）
    private static readonly Color PanelFill = new Color(0.010f, 0.030f, 0.020f, 0.90f);
    private static readonly Color PanelLine = new Color(0.47f, 1f, 0.76f, 0.40f);
    private static readonly Color SlotFill = new Color(0.47f, 1f, 0.76f, 0.05f);
    private static readonly Color SlotLine = new Color(0.47f, 1f, 0.76f, 0.22f);
    private static readonly Color SlotActiveFill = new Color(0.47f, 1f, 0.76f, 0.12f);
    private static readonly Color SlotActiveLine = new Color(0.47f, 1f, 0.76f, 0.85f);
    private static readonly Color SlotWarmFill = new Color(1f, 0.78f, 0.32f, 0.34f);
    private static readonly Color SlotWarmLine = new Color(1f, 0.86f, 0.45f, 1f);
    private static readonly Color FocusFill = new Color(0.47f, 1f, 0.76f, 0.05f);
    private static readonly Color FocusLine = new Color(0.55f, 1f, 0.82f, 0.90f);
    private static readonly Color IconWarm = new Color(1f, 0.93f, 0.72f, 1f);

    private class ItemSlot
    {
        public RectTransform root;
        public Image focus;     // 选中环（最底层）
        public Image frame;
        public Image icon;
        public GameObject keycap; // F 键帽（只亮在选中且有货的那一格上）
        public float pop;       // 弹跳动画剩余进度（1→0）
    }

    private ItemSlot[] slots;

    private bool built;
    private Sprite spPanel, spSlotIdle, spSlotActive, spSlotWarm, spKeycap, spFocus, spScan;
    private Sprite[] slotIcons;
    private Font font;
    private Canvas canvas;
    private CanvasScaler canvasScaler;

    private bool[] lastHas;
    private int lastSelected = -1;
    private int scaledForHeight = -1;   // 画布缩放比的生效屏幕高度（分辨率变化时重设 scaleFactor）

    private void OnEnable()
    {
        if (!built) Build();
        // 首帧就把状态推一遍（域重载后也靠它补正）
        lastHas = new bool[SlotCount];
        lastSelected = -1;
    }

    private void Update()
    {
        // 自愈：域重载/重建中断等边缘情况丢了引用时，整体重建而不是空引用报错
        if (!built || slots == null || slots.Length != SlotCount)
        {
            built = false;
            Build();
            if (!built) return;
            // 重建后下一帧全量刷新（状态一次推齐）
            lastHas = new bool[SlotCount];
            lastSelected = -1;
        }

        // 分辨率/窗口大小变化时同步画布缩放比（与 IMGUI 侧 CrtTheme.Scale 保持同一倍率）
        if (Screen.height != scaledForHeight)
        {
            scaledForHeight = Screen.height;
            if (canvasScaler != null) canvasScaler.scaleFactor = CrtTheme.Scale;
        }

        // 鼠标滚轮：切换选中格（快速栏循环；上滚=上一个，下滚=下一个）
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f) PlayerGear.CycleSelection(scroll > 0f ? -1 : 1);

        int selectedIndex = PlayerGear.SelectedIndex;

        // 逐格差分：新获得的那一格弹一下
        for (int i = 0; i < SlotCount; i++)
        {
            bool has = HasItem(DisplayIndex(i));
            if (has == lastHas[i]) continue;
            lastHas[i] = has;
            if (has && slots[i] != null) slots[i].pop = 1f;
        }
        if (selectedIndex != lastSelected)
        {
            if (selectedIndex >= 0 && selectedIndex < SlotCount) slots[selectedIndex].pop = 1f;
            lastSelected = selectedIndex;
        }

        RefreshAppearance();
        for (int i = 0; i < SlotCount; i++) Animate(slots[i]);
    }

    /// <summary>
    /// 视觉格位 → 真实格位的映射。正常时恒等；<see cref="GlitchState.Kind.InventorySort"/>
    /// 发作期间整体错位 <see cref="GlitchState.SlotOffset"/> 格。
    /// 只错位"显示"，真实持有与选中仍是 <see cref="PlayerGear"/> 里的那一件——
    /// 玩家看到第 2 格是手电筒、按 F 却掏出万用表，游戏不解释（这就是故障）。
    /// </summary>
    private static int DisplayIndex(int visualIndex)
    {
        if (GlitchState.Active != GlitchState.Kind.InventorySort) return visualIndex;
        int n = SlotCount;
        int src = (visualIndex + GlitchState.SlotOffset) % n;
        if (src < 0) src += n;
        return src;
    }

    /// <summary>该格对应的道具是否已持有（第 4 格是备用空位，恒为 false）。</summary>
    private static bool HasItem(int index)
    {
        switch (index)
        {
            case 0: return PlayerGear.HasFlashlight;
            case 1: return PlayerGear.HasMultimeter;
            case 2: return PlayerGear.HasEmfDetector;
            case 3: return PlayerGear.HasOrderTerminal;
            default: return false;
        }
    }

    /// <summary>该格是否有"开机中"的暖色状态（手电筒开灯 / 提示板开机）。</summary>
    private static bool IsWarm(int index)
    {
        int real = DisplayIndex(index);
        if (real == 0) return PlayerGear.FlashlightOn;
        if (real == PlayerGear.OrderTerminalSlot) return PlayerGear.OrderTerminalOn;
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // 外观刷新
    // ─────────────────────────────────────────────────────────────

    private void RefreshAppearance()
    {
        int selected = PlayerGear.SelectedIndex;
        int owner = PlayerGear.UseKeyOwner;

        for (int i = 0; i < SlotCount; i++)
        {
            var s = slots[i];
            if (s == null) continue;

            bool has = HasItem(DisplayIndex(i));
            bool warm = IsWarm(i);

            SetFocus(s, selected == i);

            if (s.frame != null)
            {
                // 三态：空=暗、持有未用=绿框、使用中=暖色实底 + 呼吸。
                // "使用中"必须一眼看出来——玩家按了 F 之后要能确认道具真的在工作，
                // 否则会反复按 F（开了又关）以为没生效。
                s.frame.sprite = !has ? spSlotIdle : (warm ? spSlotWarm : spSlotActive);
                var c = Color.white;
                if (has && warm)
                {
                    // 开机中：呼吸描边（手电筒与提示板共用同一套"正在耗电"的视觉语言）
                    float breathe = 0.82f + 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3.4f));
                    c = new Color(1f, 1f, 1f, breathe);
                }
                s.frame.color = c;
            }
            int real = DisplayIndex(i);
            SetIcon(s, has ? slotIcons[real] : null, warm ? IconWarm : Color.white);

            // F 键帽跟着选中格走：只有"按 F 真的有反应"的那一格才亮键帽
            SetKeycap(s, has && owner == real);
        }
    }

    private static void SetKeycap(ItemSlot s, bool on)
    {
        if (s.keycap != null && s.keycap.activeSelf != on) s.keycap.SetActive(on);
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
        if (slots == null || index < 0 || index >= slots.Length) return null;
        return slots[index];
    }

    private static void Animate(ItemSlot s)
    {
        if (s == null || s.root == null) return;
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
        // 图标按格位索引排：0 手电筒 / 1 万用表 / 2 探测器 / 3 任务提示板 / 4 备用空位
        slotIcons = new Sprite[SlotCount];
        slotIcons[0] = LoadIcon("flashlight");
        slotIcons[1] = LoadIcon("multimeter");
        slotIcons[2] = LoadIcon("emf");
        slotIcons[3] = LoadIcon("orderterminal");
        font = GetFont();

        // 条体（屏幕正下方居中：不挡视线，也不与左上角电量/右上角终端抢位置）
        var barGO = new GameObject("ItemBar", typeof(RectTransform));
        barGO.transform.SetParent(canvas.transform, false);
        var barRT = (RectTransform)barGO.transform;
        barRT.anchorMin = new Vector2(0.5f, 0f);
        barRT.anchorMax = new Vector2(0.5f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, BarMargin);
        float width = PadX * 2f + SlotSize * SlotCount + SlotGap * (SlotCount - 1);
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

        slots = new ItemSlot[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            slots[i] = MakeSlot(barRT, i);
            // F 键帽：全项目只有 F 一个"使用道具"键，哪一格当前归它用就亮在哪一格
            // （见 PlayerGear.UseKeyOwner：选中哪件就归哪件）
            slots[i].keycap = MakeKeycap(slots[i].root, "F");
            slots[i].keycap.SetActive(false);
        }

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
        // 居中排布：索引 0..N-1 → 偏移 -(N-1)/2 .. +(N-1)/2 格
        rt.anchoredPosition = new Vector2((index - (SlotCount - 1) * 0.5f) * (SlotSize + SlotGap), 0f);
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
