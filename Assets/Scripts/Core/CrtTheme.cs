using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CRT / 终端风格 UI 主题（IMGUI 共用件）。
/// 放在 FYP.Core：Player（电量条）/ Interaction（提示条）/ Content（订单板）三层都要用，
/// 且只依赖 UnityEngine，不引入 UGUI 依赖（UGUI 侧只借这里的字体与配色）。
///
/// 观感三件套：等宽字形（Noto Sans Mono CJK SC，中英同宽）＋ 磷光绿单色 ＋ 扫描线/光标/打字机。
/// 字体走 Resources（Assets/Resources/Sourced/Fonts），缺字体时逐级降级，任何一层都不抛异常。
/// </summary>
public static class CrtTheme
{
    // ── 磷光绿单色调色板（终端的味道来自"单一色相"，不是彩虹 HUD）──
    public static readonly Color Ink = new Color(0.47f, 1f, 0.76f);
    public static readonly Color InkDim = new Color(0.42f, 0.74f, 0.58f);
    public static readonly Color InkFaint = new Color(0.28f, 0.52f, 0.42f);
    public static readonly Color Done = new Color(0.58f, 1f, 0.62f);
    public static readonly Color Warn = new Color(1f, 0.78f, 0.32f);
    public static readonly Color Danger = new Color(1f, 0.38f, 0.30f);
    public static readonly Color PanelBg = new Color(0.010f, 0.030f, 0.020f, 0.90f);

    // ── 字体 ──
    private static Font mono;
    private static Font pixel;
    private static Font legacy;

    /// <summary>等宽主字体（中英同宽，CRT 网格的基础）。</summary>
    public static Font Mono
    {
        get
        {
            if (mono == null) mono = Resources.Load<Font>("Sourced/Fonts/NotoSansMonoCJKsc-Regular");
            if (mono == null) mono = Pixel;
            return mono;
        }
    }

    /// <summary>像素字（VT323，仅 ASCII/数字——用于大号读数，比如电量百分比）。</summary>
    public static Font Pixel
    {
        get
        {
            if (pixel == null) pixel = Resources.Load<Font>("Sourced/Fonts/VT323-Regular");
            if (pixel == null) pixel = Legacy;
            return pixel;
        }
    }

    /// <summary>引擎内置字体（最后兜底，保证 HUD 永远不会因为缺字体而不显示）。</summary>
    public static Font Legacy
    {
        get
        {
            if (legacy == null) legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacy == null) legacy = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return legacy;
        }
    }

    // ── 分辨率自适应 ──
    // HUD 以 720p 为基准等比放大：1080p→1.5×、1440p→2×、4K→3×。
    // 下限锁 1 = 窗口再小也是 1:1 像素（绝不缩小导致字形与 1px 线发虚）。
    private static float scaleCache = -1f;
    private static int scaleForHeight = -1;

    /// <summary>当前 HUD 缩放系数（按屏幕高度，1~3）。</summary>
    public static float Scale
    {
        get
        {
            if (Screen.height == scaleForHeight && scaleCache > 0f) return scaleCache;
            scaleForHeight = Screen.height;
            scaleCache = Mathf.Clamp(Screen.height / 720f, 1f, 3f);
            return scaleCache;
        }
    }

    /// <summary>尺寸换算并取整（整数像素才不会出现半像素发虚）。</summary>
    public static float Px(float v)
    {
        return Mathf.Round(v * Scale);
    }

    /// <summary>字号换算（IMGUI 字号必须是整数）。</summary>
    public static int S(float px)
    {
        return Mathf.Max(1, Mathf.RoundToInt(px * Scale));
    }

    // ── 样式缓存（OnGUI 每帧调用，避免每帧 new GUIStyle 的 GC）──
    private static readonly Dictionary<string, GUIStyle> Styles = new Dictionary<string, GUIStyle>();

    /// <summary>取一个 CRT 文本样式（等宽、无富文本歧义、指定字号/颜色/对齐）。</summary>
    public static GUIStyle Style(int size, Color color, bool wrap = true, bool pixel = false, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        int fs = S(size);
        string key = (pixel ? "p" : "m") + fs + "|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + (wrap ? 1 : 0) + "|" + (int)anchor;
        GUIStyle st;
        if (Styles.TryGetValue(key, out st) && st != null) return st;

        var baseSkin = GUI.skin != null ? GUI.skin.label : new GUIStyle();
        st = new GUIStyle(baseSkin)
        {
            font = pixel ? Pixel : Mono,
            fontSize = fs,
            wordWrap = wrap,
            richText = true,
            alignment = anchor,
            clipping = TextClipping.Clip,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };
        st.normal.textColor = color;
        st.hover.textColor = color;
        st.active.textColor = color;
        st.focused.textColor = color;
        Styles[key] = st;
        return st;
    }

    // ── 贴图 ──
    private static Texture2D white;
    private static Texture2D scan;

    /// <summary>1×1 白纹理：所有实底/描边都靠 GUI.color 上色，避免按颜色建贴图。</summary>
    public static Texture2D White
    {
        get
        {
            if (white == null)
            {
                white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            return white;
        }
    }

    /// <summary>1×3 扫描线纹理（一行亮、两行暗），Repeat 平铺。</summary>
    public static Texture2D ScanTex
    {
        get
        {
            if (scan == null)
            {
                scan = new Texture2D(1, 3, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point
                };
                scan.SetPixel(0, 0, new Color(0f, 0f, 0f, 1f));
                scan.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
                scan.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
                scan.Apply();
            }
            return scan;
        }
    }

    /// <summary>实心矩形（tint 由 GUI.color 完成）。</summary>
    public static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, White);
        GUI.color = prev;
    }

    // ── 圆角块（电量电池这类"器件"图标需要圆角，面板/边框仍走直角）──
    private static readonly Dictionary<int, Texture2D> roundCache = new Dictionary<int, Texture2D>();

    /// <summary>
    /// 圆角实心块贴图（按半径缓存）。用圆角矩形的有向距离场生成，
    /// 中间留 2px 可拉伸区，配合 <see cref="RoundFill"/> 做九宫格绘制。
    /// </summary>
    public static Texture2D RoundSolid(int radius)
    {
        int rad = Mathf.Clamp(radius, 1, 48);
        Texture2D t;
        if (roundCache.TryGetValue(rad, out t) && t != null) return t;

        int size = rad * 2 + 2;
        t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[size * size];
        float half = size * 0.5f;
        float r = rad;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float qx = Mathf.Abs(dx) - (half - r);
                float qy = Mathf.Abs(dy) - (half - r);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                float a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply();
        roundCache[rad] = t;
        return t;
    }

    /// <summary>九宫格铺一张圆角贴图（四角不变形、四边与中心拉伸）。</summary>
    private static void Round9(Rect dst, Texture2D tex, float cornerPx, float midPx)
    {
        float s = tex.width;
        float u0 = 0f, u1 = cornerPx / s, u2 = (cornerPx + midPx) / s, u3 = 1f;
        float[] us = { u0, u1, u2, u3 };
        float[] xs = { dst.x, dst.x + cornerPx, dst.xMax - cornerPx, dst.xMax };
        float[] ys = { dst.y, dst.y + cornerPx, dst.yMax - cornerPx, dst.yMax };
        for (int iy = 0; iy < 3; iy++)
        {
            for (int ix = 0; ix < 3; ix++)
            {
                var d = new Rect(xs[ix], ys[iy], xs[ix + 1] - xs[ix], ys[iy + 1] - ys[iy]);
                if (d.width <= 0f || d.height <= 0f) continue;
                // 圆角矩形上下左右对称，v 不必翻转
                var uv = new Rect(us[ix], us[iy], us[ix + 1] - us[ix], us[iy + 1] - us[iy]);
                GUI.DrawTextureWithTexCoords(d, tex, uv);
            }
        }
    }

    /// <summary>圆角实心块（tint 由 GUI.color 完成，内部会临时改回）。</summary>
    public static void RoundFill(Rect r, Color c, int radius)
    {
        if (r.width <= 0f || r.height <= 0f) return;
        var tex = RoundSolid(radius);
        var prev = GUI.color;
        GUI.color = c;
        Round9(r, tex, Mathf.Min(radius, r.width * 0.5f), 2f);
        GUI.color = prev;
    }

    /// <summary>圆角描边（外壳用）。IMGUI 的 alpha 混合不能"擦除"，所以描边是独立的环形贴图，不是"铺满再挖空"。</summary>
    public static void RoundBorder(Rect r, Color c, int radius, float thickness)
    {
        if (r.width <= 0f || r.height <= 0f) return;
        int rad = Mathf.Clamp(radius, 1, 48);
        int th = Mathf.Clamp(Mathf.RoundToInt(thickness), 1, 12);
        var tex = RoundRing(rad, th);
        var prev = GUI.color;
        GUI.color = c;
        // 九宫格切片必须与贴图内部布局严格一致：
        // 环形贴图的角区边长是 CornerOf(rad, th)，中段固定 2px（见 RoundRing）。
        // 传错会让四个角被切掉一部分，外壳看起来像"括号"而不是圆角矩形。
        Round9(r, tex, CornerOf(rad, th), 2f);
        GUI.color = prev;
    }

    private static readonly Dictionary<int, Texture2D> ringCache = new Dictionary<int, Texture2D>();

    private static readonly Dictionary<int, Texture2D> exactRingCache = new Dictionary<int, Texture2D>();

    /// <summary>
    /// 按目标尺寸**1:1 生成**的圆角描边（不做九宫格拉伸）。
    ///
    /// 为什么需要这个：环形贴图的中段横向切片里同时含左、右两条边，
    /// 九宫格把它横向拉伸时两条边会一起被拉宽/错位，窄尺寸（如 34×58 的电池）
    /// 上直接糊成"括号"。这类小器件尺寸固定、数量极少，按实际像素生成一次再缓存，
    /// 既准确又只多一张 34×58 的贴图。
    /// </summary>
    public static void RoundBorderExact(Rect r, Color c, int radius, float thickness)
    {
        if (r.width <= 0f || r.height <= 0f) return;
        int w = Mathf.Max(2, Mathf.RoundToInt(r.width));
        int h = Mathf.Max(2, Mathf.RoundToInt(r.height));
        int rad = Mathf.Clamp(radius, 1, Mathf.Min(w, h) / 2);
        int th = Mathf.Clamp(Mathf.RoundToInt(thickness), 1, Mathf.Min(w, h) / 2);

        int key = ((w * 512 + h) * 64 + rad) * 16 + th;
        Texture2D t;
        if (!exactRingCache.TryGetValue(key, out t) || t == null)
        {
            t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            // 圆角矩形环的有向距离场：盒心线 = 半径 rad、半尺寸 (w/2 - th/2, h/2 - th/2)
            float bx = w * 0.5f - th * 0.5f;
            float by = h * 0.5f - th * 0.5f;
            float rIn = Mathf.Max(0.5f, rad - th * 0.5f);
            float hr = th * 0.5f;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - w * 0.5f) - (bx - rIn);
                    float dy = Mathf.Abs(y + 0.5f - h * 0.5f) - (by - rIn);
                    float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - rIn;
                    float a = Mathf.Clamp01(hr - Mathf.Abs(d) + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            t.Apply();
            exactRingCache[key] = t;
        }

        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(Mathf.Round(r.x), Mathf.Round(r.y), w, h), t);
        GUI.color = prev;
    }

    /// <summary>圆角矩形"边框环"贴图（有向距离场生成，按半径+线宽缓存）。</summary>
    private static Texture2D RoundRing(int radius, int thickness)
    {
        int key = radius * 100 + thickness;
        Texture2D t;
        if (ringCache.TryGetValue(key, out t) && t != null) return t;

        // 贴图布局（九宫格切片就按这个算，两边必须一致）：
        //   角区 = CornerOf(radius, thickness)，中段 = 2px
        int corner = CornerOf(radius, thickness);
        int size = corner * 2 + 2;
        t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // 描边中心线 = 圆角矩形（半径 radius、半边长 half - thickness/2），
        // 环 = 到中心线的距离落在 ±thickness/2 内。
        float half = size * 0.5f;
        float box = half - thickness * 0.5f;
        float r = Mathf.Max(0.5f, radius - thickness * 0.5f);
        float hr = Mathf.Max(0.5f, thickness * 0.5f);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half);
                float dy = Mathf.Abs(y + 0.5f - half);
                float qx = dx - (box - r);
                float qy = dy - (box - r);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                float a = Mathf.Clamp01(0.5f + (hr - Mathf.Abs(d)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply();
        ringCache[key] = t;
        return t;
    }

    /// <summary>环形贴图的角区边长（RoundRing 与 RoundBorder 必须用同一个公式）。</summary>
    private static int CornerOf(int radius, int thickness)
    {
        return Mathf.Max(2, Mathf.RoundToInt(radius + thickness * 0.5f) + 1);
    }

    private static readonly Dictionary<string, Texture2D> glyphCache = new Dictionary<string, Texture2D>();

    /// <summary>
    /// 像素图标：rows 每行等宽的 '.'=透明、其它=实心。
    ///
    /// 实现是"先把字符网格烘成一张小贴图，再整张缩放画上去"，而不是逐格 DrawTexture：
    /// 逐格画时每格都要取整到设备像素（防半像素缝），22px 画 9 列时每格只剩 2px，
    /// 格与格之间就会露出缝，箭头看起来是一串点。烘成贴图后由采样器负责插值，边缘连续。
    /// </summary>
    public static void PixelGlyph(Rect r, string[] rows, Color c)
    {
        if (rows == null || rows.Length == 0 || r.width <= 0f || r.height <= 0f) return;
        int cols = 0;
        for (int i = 0; i < rows.Length; i++) if (rows[i] != null && rows[i].Length > cols) cols = rows[i].Length;
        if (cols == 0) return;

        string key = cols + "x" + rows.Length + ":" + string.Join("|", rows);
        Texture2D t;
        if (!glyphCache.TryGetValue(key, out t) || t == null)
        {
            t = new Texture2D(cols, rows.Length, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,   // 像素图标要硬边，不能模糊
            };
            var px = new Color32[cols * rows.Length];
            for (int y = 0; y < rows.Length; y++)
            {
                string row = rows[y] ?? "";
                for (int x = 0; x < cols; x++)
                {
                    bool on = x < row.Length && row[x] != '.' && row[x] != ' ';
                    // 贴图 y 向上，字符行 y 向下 → 翻转
                    px[(rows.Length - 1 - y) * cols + x] = new Color32(255, 255, 255, on ? (byte)255 : (byte)0);
                }
            }
            t.SetPixels32(px);
            t.Apply();
            glyphCache[key] = t;
        }

        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height)), t);
        GUI.color = prev;
    }

    /// <summary>描边（线宽随 HUD 缩放，且取整防糊）。</summary>
    public static void Border(Rect r, Color c)
    {
        float t = Mathf.Max(1f, Mathf.Round(Scale));
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    /// <summary>四角加粗的"取景角"——终端窗口感来自这里，而不是圆角。</summary>
    public static void Corners(Rect r, Color c, float len = 8f, float thick = 2f)
    {
        float L = Mathf.Max(2f, Px(len));
        float T = Mathf.Max(1f, Px(thick));
        Fill(new Rect(r.x, r.y, L, T), c);
        Fill(new Rect(r.x, r.y, T, L), c);
        Fill(new Rect(r.xMax - L, r.y, L, T), c);
        Fill(new Rect(r.xMax - T, r.y, T, L), c);
        Fill(new Rect(r.x, r.yMax - T, L, T), c);
        Fill(new Rect(r.x, r.yMax - L, T, L), c);
        Fill(new Rect(r.xMax - L, r.yMax - T, L, T), c);
        Fill(new Rect(r.xMax - T, r.yMax - L, T, L), c);
    }

    /// <summary>扫描线覆盖（每 3px 一条暗线；按设备像素，不随 HUD 缩放）。</summary>
    public static void Scan(Rect r, float alpha = 0.12f)
    {
        if (r.height <= 0f) return;
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTextureWithTexCoords(r, ScanTex, new Rect(0f, 0f, 1f, r.height / 3f));
        GUI.color = prev;
    }

    /// <summary>终端面板：底色 + 扫描线 + 描边 + 取景角 +（可选）标题行。</summary>
    public static void Panel(Rect r, string title = null)
    {
        Fill(r, PanelBg);
        Scan(r, 0.13f);
        Border(r, new Color(Ink.r, Ink.g, Ink.b, 0.40f));
        Corners(r, new Color(Ink.r, Ink.g, Ink.b, 0.85f));

        if (!string.IsNullOrEmpty(title))
        {
            GUI.Label(new Rect(r.x + Px(10f), r.y + Px(4f), r.width - Px(20f), Px(16f)), "▌ " + title, Style(12, InkDim, false));
            Fill(new Rect(r.x + Px(4f), r.y + Px(21f), r.width - Px(8f), Mathf.Max(1f, Mathf.Round(Scale))), new Color(Ink.r, Ink.g, Ink.b, 0.20f));
        }
    }

    // ── 终端小件 ──

    /// <summary>光标闪烁（开/关），周期与占空比可调。</summary>
    public static bool Blink(float period = 1.1f, float duty = 0.55f)
    {
        return Mathf.Repeat(Time.unscaledTime, period) < period * duty;
    }

    /// <summary>实心方块光标。</summary>
    public static void Cursor(float x, float y, float w = 7f, float h = 13f, float alpha = 0.85f)
    {
        if (!Blink()) return;
        Fill(new Rect(x, y, Px(w), Px(h)), new Color(Ink.r, Ink.g, Ink.b, alpha));
    }

    /// <summary>打字机：按经过时间逐字显示（多行一起推进）。</summary>
    public static string Reveal(string text, float elapsed, float cps = 38f)
    {
        if (string.IsNullOrEmpty(text)) return text;
        int n = Mathf.Clamp(Mathf.FloorToInt(elapsed * cps), 0, text.Length);
        return n >= text.Length ? text : text.Substring(0, n);
    }

    /// <summary>进度条（终端风格：方块格 + 右侧读数）。</summary>
    public static void Gauge(Rect r, float value01, int cells, Color color)
    {
        float gap = Mathf.Max(1f, Mathf.Round(Scale));
        float cw = (r.width - gap * (cells - 1)) / cells;
        int on = Mathf.Clamp(Mathf.RoundToInt(value01 * cells), 0, cells);
        for (int i = 0; i < cells; i++)
        {
            var cell = new Rect(r.x + i * (cw + gap), r.y, cw, r.height);
            if (i < on) Fill(cell, color);
            else Fill(cell, new Color(color.r, color.g, color.b, 0.16f));
        }
    }

    /// <summary>竖排"胶片"扫描遮罩：给整屏叠一层极淡暗角，让 UI 像贴在显像管上。</summary>
    private static Texture2D vignette;

    public static Texture2D Vignette
    {
        get
        {
            if (vignette != null) return vignette;
            const int N = 64;
            vignette = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float nx = (x + 0.5f) / N * 2f - 1f;
                    float ny = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny) / 1.414f;
                    float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(a * a * 255f));
                }
            }
            vignette.SetPixels32(px);
            vignette.Apply();
            return vignette;
        }
    }
}
