using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具栏的绘制工具与配色（从 InventoryUI 拆出，2026-11-10）。
///
/// 为什么拆：InventoryUI 逼近仓库的 20 KB 单文件上限（`.git/hooks/pre-commit`
/// 会拦），而它里面真正占体量的是这一整段"怎么画"——程序化九宫格、扫描线、
/// 取图标、取字体。这部分跟"道具栏有什么格、选中哪一格、什么时候亮"是两件事，
/// 按理就该分开；拆开之后 InventoryUI 只管状态与交互，这里只管像素。
///
/// 全部是 static：这些方法不碰实例状态，只按参数产出 Sprite 或摆 RectTransform。
/// </summary>
internal static class InventoryArt
{
    // CRT 终端配色（磷光绿单色 + 暗底；与 OrderBoard / 电量条同一套 CrtTheme 色板）
    internal static readonly Color PanelFill = new Color(0.010f, 0.030f, 0.020f, 0.90f);
    internal static readonly Color PanelLine = new Color(0.47f, 1f, 0.76f, 0.40f);
    internal static readonly Color SlotFill = new Color(0.47f, 1f, 0.76f, 0.05f);
    internal static readonly Color SlotLine = new Color(0.47f, 1f, 0.76f, 0.22f);
    internal static readonly Color SlotActiveFill = new Color(0.47f, 1f, 0.76f, 0.12f);
    internal static readonly Color SlotActiveLine = new Color(0.47f, 1f, 0.76f, 0.85f);
    internal static readonly Color SlotWarmFill = new Color(1f, 0.78f, 0.32f, 0.34f);
    internal static readonly Color SlotWarmLine = new Color(1f, 0.86f, 0.45f, 1f);
    internal static readonly Color FocusFill = new Color(0.47f, 1f, 0.76f, 0.05f);
    internal static readonly Color FocusLine = new Color(0.55f, 1f, 0.82f, 0.90f);
    internal static readonly Color IconWarm = new Color(1f, 0.93f, 0.72f, 1f);

    internal static Image MakeImage(Transform parent, string name, Sprite sprite)
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

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>取烘焙好的道具图标；缺图返回 null，调用方按"只显框"降级。</summary>
    internal static Sprite LoadIcon(string name)
    {
        return Resources.Load<Sprite>("UI/Icons/" + name);
    }

    internal static Font GetFont()
    {
        // CRT 主题的等宽字体（Noto Sans Mono CJK SC）；缺字体时由 CrtTheme 逐级降级
        var f = CrtTheme.Mono;
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    /// <summary>程序化直角矩形（1px 描边，Point 采样保持像素锐利），九宫格切片用——终端不用圆角。</summary>
    internal static Sprite Sharp(Color fill, Color line)
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
    internal static Sprite ScanStrip()
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
