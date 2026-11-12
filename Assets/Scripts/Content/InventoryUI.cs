using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具栏（屏幕底部中央）：八格，只放可用的探索道具。
///   0 手电筒——拾取后亮起；开灯时暖色高亮 + 呼吸描边。
///   1 万用表——拾取后亮起（配电箱测线用）。
///   2 探测器——拾取后亮起（把异常扫显形）。
///   3 任务提示板——拾取后亮起；开机时暖色高亮（右上角订单 HUD 随之显示）。
///   4 换班怀表——首次强制换班后到手（或在值班室取表）；按 F 强制昼夜互换。
///   5 拍照相机 / 6 奇异的像素方块 / 7 温度计（2026-11 Metro 轮）。
/// 鼠标滚轮 = 切换选中格（循环），选中格亮选中环 + 弹跳反馈；新到手的道具格弹跳强调。
/// F = 只用选中格那一件（见 PlayerGear.UseKeyOwner），一次按键只触发一件道具。
/// 随身异常物直接放进道具栏的现有空格里（还没持有工具的格），捡到即亮图标、弹跳，
/// 放进实验室收容台即让位；不加行不加格。
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

    // CRT 终端配色与绘制工具见 InventoryArt（同目录）。

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

    // ── 随身异常物（2026-11-12 实测定式：捡到的东西直接放进道具栏现有空格里）──
    // 不加行不加格：借用「还没持有对应工具」的空格显示图标；工具到手就挪窝/让位。
    private Sprite[] specimenAt;        // 按真实格位索引：该空格当前放的异常物图标
    private int specimenShown = -1;     // 上次刷到的携带数（差分用）

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

        // 逐格差分：新获得的那一格弹一下；工具持有变化还要让异常物挪窝重排
        bool toolsChanged = false;
        for (int i = 0; i < SlotCount; i++)
        {
            bool has = HasItem(DisplayIndex(i));
            if (has == lastHas[i]) continue;
            lastHas[i] = has;
            if (has && slots[i] != null) slots[i].pop = 1f;
            toolsChanged = true;
        }
        if (selectedIndex != lastSelected)
        {
            if (selectedIndex >= 0 && selectedIndex < SlotCount) slots[selectedIndex].pop = 1f;
            lastSelected = selectedIndex;
        }

        RefreshAppearance();
        for (int i = 0; i < SlotCount; i++) Animate(slots[i]);
        RefreshSpecimens(toolsChanged);
    }

    /// <summary>
    /// 随身异常物：直接放进道具栏**现有的空格**里（从左往右找还没持有工具的格），
    /// 一格一件带图标，新捡的弹一下；放进收容台/散失即让位。
    /// 道具栏永远 8 格不加行不加格（2026-11-12 实测定式）。工具到位时异常物自动挪窝；
    /// 空格不够放下的（8 件工具全满）不显示格，右上订单板仍有计数兜底。
    /// 图标按档案名映射：磁带→tape，其余→floppy。
    /// </summary>
    private void RefreshSpecimens(bool force = false)
    {
        var inv = Inventory.Instance;
        int count = inv != null ? inv.SpecimenCount : 0;
        if (!force && count == specimenShown) return;
        specimenShown = count;

        if (specimenAt == null || specimenAt.Length != SlotCount) specimenAt = new Sprite[SlotCount];
        for (int i = 0; i < SlotCount; i++) specimenAt[i] = null;

        var pending = inv != null ? inv.Pending : null;
        int next = 0;
        for (int i = 0; i < SlotCount && next < count; i++)
        {
            if (HasItem(i)) continue;   // 这格已有自己的工具
            string archName = pending != null && next < pending.Count ? pending[next].name : null;
            specimenAt[i] = InventoryArt.LoadIcon(archName != null && archName.Contains("磁带") ? "tape" : "floppy");
            if (slots[i] != null) slots[i].pop = 1f;   // 放进去的那格弹一下
            next++;
        }
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

    /// <summary>该格对应的道具是否已持有。</summary>
    private static bool HasItem(int index)
    {
        switch (index)
        {
            case 0: return PlayerGear.HasFlashlight;
            case 1: return PlayerGear.HasMultimeter;
            case 2: return PlayerGear.HasEmfDetector;
            case PlayerGear.OrderTerminalSlot: return PlayerGear.HasOrderTerminal;
            // 换班怀表（2026-11 接通）：这一格以前是恒 false 的备用空位，
            // 于是怀表拿了也永远不显示——图标管线本身是好的，卡在持有判定上。
            case PlayerGear.ShiftWatchSlot: return PlayerGear.HasShiftWatch;
            // Metro 轮新仪器（相机 / 录音笔 / 温度计）——同一条管线接上即显示
            case PlayerGear.CameraSlot: return PlayerGear.HasCamera;
            case PlayerGear.RecorderSlot: return PlayerGear.HasRecorder;
            case PlayerGear.ThermometerSlot: return PlayerGear.HasThermometer;
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

            int real = DisplayIndex(i);
            bool has = HasItem(real);
            // 空格借给随身异常物：捡到的东西直接显示在道具栏里（放回收容台即让位）
            var held = has ? slotIcons[real] : (specimenAt != null && real < specimenAt.Length ? specimenAt[real] : null);
            bool warm = IsWarm(i);

            SetFocus(s, selected == i);

            if (s.frame != null)
            {
                // 三态：空=暗、持有未用=绿框、使用中=暖色实底 + 呼吸。
                // "使用中"必须一眼看出来——玩家按了 F 之后要能确认道具真的在工作，
                // 否则会反复按 F（开了又关）以为没生效。
                s.frame.sprite = held == null ? spSlotIdle : (warm ? spSlotWarm : spSlotActive);
                var c = Color.white;
                if (has && warm)
                {
                    // 开机中：呼吸描边（手电筒与提示板共用同一套"正在耗电"的视觉语言）
                    float breathe = 0.82f + 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3.4f));
                    c = new Color(1f, 1f, 1f, breathe);
                }
                s.frame.color = c;
            }
            SetIcon(s, held, warm ? InventoryArt.IconWarm : Color.white);

            // F 键帽跟着选中格走：只有"按 F 真的有反应"的那一格才亮键帽（异常物不是工具，不亮）
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
        spPanel = InventoryArt.Sharp(InventoryArt.PanelFill, InventoryArt.PanelLine);
        spSlotIdle = InventoryArt.Sharp(InventoryArt.SlotFill, InventoryArt.SlotLine);
        spSlotActive = InventoryArt.Sharp(InventoryArt.SlotActiveFill, InventoryArt.SlotActiveLine);
        spSlotWarm = InventoryArt.Sharp(InventoryArt.SlotWarmFill, InventoryArt.SlotWarmLine);
        spFocus = InventoryArt.Sharp(InventoryArt.FocusFill, InventoryArt.FocusLine);
        spKeycap = InventoryArt.Sharp(new Color(0.03f, 0.07f, 0.05f, 0.95f), new Color(0.47f, 1f, 0.76f, 0.35f));
        spScan = InventoryArt.ScanStrip();
        // 图标按格位索引排：0 手电筒 / 1 万用表 / 2 探测器 / 3 任务提示板 / 4 换班怀表 / 5 相机 / 6 录音笔 / 7 温度计
        slotIcons = new Sprite[SlotCount];
        slotIcons[0] = InventoryArt.LoadIcon("flashlight");
        slotIcons[1] = InventoryArt.LoadIcon("multimeter");
        slotIcons[2] = InventoryArt.LoadIcon("emf");
        slotIcons[PlayerGear.OrderTerminalSlot] = InventoryArt.LoadIcon("orderterminal");
        slotIcons[PlayerGear.ShiftWatchSlot] = InventoryArt.LoadIcon("shiftwatch");
        slotIcons[PlayerGear.CameraSlot] = InventoryArt.LoadIcon("camera");
        slotIcons[PlayerGear.RecorderSlot] = InventoryArt.LoadIcon("recorder");
        slotIcons[PlayerGear.ThermometerSlot] = InventoryArt.LoadIcon("thermometer");
        font = InventoryArt.GetFont();

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

        var panel = InventoryArt.MakeImage(barGO.transform, "Panel", spPanel);
        InventoryArt.Stretch(panel.rectTransform);

        // 扫描线层（终端质感）：1×3 条纹平铺，压在面板之上、槽位之下
        var scan = InventoryArt.MakeImage(barGO.transform, "ScanLines", spScan);
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

        // 条体重建后异常物占位一并作废——下一帧按携带数重排
        specimenAt = null;
        specimenShown = -1;

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
        slot.focus = InventoryArt.MakeImage(go.transform, "Focus", spFocus);
        var frt = slot.focus.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.pivot = new Vector2(0.5f, 0.5f);
        frt.offsetMin = new Vector2(-5f, -5f);
        frt.offsetMax = new Vector2(5f, 5f);
        slot.focus.enabled = false;

        slot.frame = InventoryArt.MakeImage(go.transform, "Frame", spSlotIdle);
        InventoryArt.Stretch(slot.frame.rectTransform);

        slot.icon = InventoryArt.MakeImage(go.transform, "Icon", null);
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

        var img = InventoryArt.MakeImage(go.transform, "Bg", spKeycap);
        InventoryArt.Stretch(img.rectTransform);

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
            InventoryArt.Stretch((RectTransform)textGO.transform);
        }
        return go;
    }

    // 绘制工具与配色已拆到 InventoryArt（单文件上限 20 KB，见 .git/hooks/pre-commit）。
}