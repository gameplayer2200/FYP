using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 道具图标烘焙：把三维道具模型渲染成透明背景 PNG 图标（道具栏用）。
///
/// 渲染策略：临时对象（模型 + 相机 + 三盏灯）挪到远空 y=20000 处渲染，结束即毁——
/// 不依赖 PreviewScene（实测其手动 cam.Render() 会渲染出整屏糊图），也不进 Play。
/// 渲染期间临时关场景平行光 / 雾，环境光改常数，finally 全量还原。
/// 正交感相机 + 两轮视口拟合自动取景，SSAA 2× 渲染后降采样，编码 PNG。
/// 项目是线性色彩空间（m_ActiveColorSpace=1）：目标 RT 必须按 sRGB 写读，
/// 否则像素亮度会出错。
///
/// 产物：Assets/Resources/UI/Icons/*.png（自动配成 Sprite），
/// 运行时 InventoryUI 用 Resources.Load&lt;Sprite&gt;("UI/Icons/xx") 取。
///
/// 用法：菜单 FYP/UI/烘焙道具图标；或 MCP execute_code 直呼 ItemIconBaker.BakeAll()。
/// 改角度 / 亮度后重跑即可覆盖。
/// </summary>
public static class ItemIconBaker
{
    private const int Size = 256;        // 成品边长（px）
    private const int Super = 2;         // SSAA 倍率
    private const string OutDir = "Assets/Resources/UI/Icons";
    private static readonly Vector3 Anchor = new Vector3(0f, 20000f, 0f);   // 远空：隔离场景几何

    [MenuItem("FYP/UI/烘焙道具图标")]
    public static void BakeAllMenu()
    {
        Debug.Log(BakeAll());
    }

    /// <summary>烘焙全部道具图标，返回逐行结果（MCP 直接读）。</summary>
    public static string BakeAll()
    {
        if (Application.isPlaying) return "正在 Play，先停止再烘焙。";
        var sb = new StringBuilder();
        // 参数：图标名、模型资产、相机俯角/偏航（球面机位）、材质提亮系数
        sb.AppendLine(One("flashlight", "Assets/items/Exploration objects/Flashlight/Flashlight.fbx", 20f, 70f, 1.15f));
        sb.AppendLine(One("multimeter", "Assets/items/RandomObjects/multimeter/multimeter.fbx", 30f, 50f, 1.0f));
        sb.AppendLine(Floppy());
        AssetDatabase.Refresh();
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────
    // 图标定义
    // ─────────────────────────────────────────────────────────────

    private static string One(string name, string modelPath, float pitch, float yaw, float brighten)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (prefab == null) return name + "：找不到模型 " + modelPath;
        return Render(name, Object.Instantiate(prefab), pitch, yaw, brighten);
    }

    /// <summary>程序化软盘（与 MetroSceneDev.BuildFloppy 同构：盘体 / 滑片 / 标签三块图元）。</summary>
    private static string Floppy()
    {
        var root = new GameObject("FloppyIcon");
        AddBox(root.transform, "盘体", Vector3.zero, new Vector3(0.32f, 0.03f, 0.32f),
            LoadOrMat("Assets/Material/MetroFloppy_Body.mat", new Color(0.13f, 0.17f, 0.28f)));
        AddBox(root.transform, "金属滑片", new Vector3(0f, 0.001f, 0.095f), new Vector3(0.13f, 0.034f, 0.075f),
            LoadOrMat("Assets/Material/MetroFloppy_Shutter.mat", new Color(0.75f, 0.78f, 0.82f)));
        AddBox(root.transform, "标签", new Vector3(0f, 0.001f, -0.075f), new Vector3(0.22f, 0.033f, 0.11f),
            LoadOrMat("Assets/Material/MetroFloppy_Label.mat", new Color(0.82f, 0.8f, 0.72f)));
        return Render("floppy", root, 52f, 200f, 1.6f);
    }

    private static void AddBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = pos;
        box.transform.localScale = scale;
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Material LoadOrMat(string assetPath, Color fallbackColor)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (m != null) return m;
        var made = new Material(Shader.Find("Standard"));
        made.color = fallbackColor;
        return made;
    }

    // ─────────────────────────────────────────────────────────────
    // 渲染
    // ─────────────────────────────────────────────────────────────

    private static string Render(string name, GameObject root, float pitch, float yaw, float brighten)
    {
        GameObject camGO = null, l1go = null, l2go = null, l3go = null;
        var clones = new List<Material>();
        var rtBig = new RenderTexture(Size * Super, Size * Super, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rtBig.filterMode = FilterMode.Bilinear;
        bool fogWas = RenderSettings.fog;
        var ambModeWas = RenderSettings.ambientMode;
        var ambLightWas = RenderSettings.ambientLight;
        var dirOff = new List<Light>();      // 临时关掉的场景平行光，finally 还原
        try
        {
            root.transform.position = Anchor;

            // 材质提亮 + 收集世界边界
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds bounds = default(Bounds);
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (brighten != 1f) LiftMaterials(r, brighten, clones);
                if (!has) { bounds = r.bounds; has = true; } else bounds.Encapsulate(r.bounds);
            }
            if (!has) return name + "：没有可见渲染器";

            // 渲染环境隔离（只影响本次渲染，finally 还原）
            foreach (var l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional && l.enabled) { dirOff.Add(l); l.enabled = false; }
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.31f, 0.34f);

            // 相机：正交感 + 球面机位（位移量按边界大小自适应）
            camGO = new GameObject("IconBakeCam");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = 1f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            Vector3 center = bounds.center;
            Vector3 dir = SphericalDir(pitch, yaw);
            cam.transform.position = center + dir * (bounds.extents.magnitude * 3f + 5f);
            cam.transform.LookAt(center);

            // 两轮视口拟合取景（12% 边距）
            for (int it = 0; it < 2; it++)
            {
                float minx = 2f, miny = 2f, maxx = -1f, maxy = -1f;
                foreach (var r in renderers)
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var bb = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = new Vector3(
                            (i & 1) == 0 ? bb.min.x : bb.max.x,
                            (i & 2) == 0 ? bb.min.y : bb.max.y,
                            (i & 4) == 0 ? bb.min.z : bb.max.z);
                        var v = cam.WorldToViewportPoint(c);
                        if (v.z <= 0f) continue;
                        minx = Mathf.Min(minx, v.x); maxx = Mathf.Max(maxx, v.x);
                        miny = Mathf.Min(miny, v.y); maxy = Mathf.Max(maxy, v.y);
                    }
                }
                if (minx > maxx) break;
                float half = Mathf.Max(Mathf.Max(maxx - minx, maxy - miny) * 0.5f, 1e-4f);
                cam.orthographicSize = cam.orthographicSize * half * 2f * 1.12f;
                float cx = (minx + maxx) * 0.5f, cy = (miny + maxy) * 0.5f;
                float worldPerView = cam.orthographicSize * 2f;   // aspect=1，横竖同尺
                cam.transform.position += cam.transform.right * (cx - 0.5f) * worldPerView
                                         + cam.transform.up * (cy - 0.5f) * worldPerView;
            }

            // 三点灯：主光左上 / 补光右侧 / 反光板下后方（都相对相机朝向）
            l1go = new GameObject("IconBakeKey");
            var l1 = l1go.AddComponent<Light>();
            l1.type = LightType.Directional; l1.intensity = 1.10f; l1.color = new Color(1f, 0.97f, 0.93f);
            l1go.transform.rotation = cam.transform.rotation * Quaternion.Euler(32f, 24f, 0f);
            l2go = new GameObject("IconBakeFill");
            var l2 = l2go.AddComponent<Light>();
            l2.type = LightType.Directional; l2.intensity = 0.42f; l2.color = new Color(0.88f, 0.93f, 1f);
            l2go.transform.rotation = cam.transform.rotation * Quaternion.Euler(8f, -52f, 0f);
            l3go = new GameObject("IconBakeBounce");
            var l3 = l3go.AddComponent<Light>();
            l3.type = LightType.Directional; l3.intensity = 0.22f;
            l3go.transform.rotation = cam.transform.rotation * Quaternion.Euler(-30f, 175f, 0f);

            // 渲染（SSAA 大图）
            cam.targetTexture = rtBig;
            cam.Render();
            cam.targetTexture = null;

            // 读回：优先降采样读（SSAA）；若读回异常（全透明）退回全分辨率直读
            var tex = ReadRT(rtBig, Size);
            float occ = Occupancy(tex);
            string how = "SSAA降采样";
            if (occ < 0.01f)
            {
                Object.DestroyImmediate(tex);
                tex = ReadRT(rtBig, rtBig.width);
                occ = Occupancy(tex);
                how = "全分辨率直读";
            }

            Directory.CreateDirectory(OutDir);
            string path = OutDir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSprite(path);
            return string.Format("{0} → {1}  边界{2:F2}m 视高{3:F3}m 占用{4:P0}（{5}）",
                name, path, bounds.size.magnitude, cam.orthographicSize, occ, how);
        }
        finally
        {
            foreach (var l in dirOff) if (l != null) l.enabled = true;
            RenderSettings.fog = fogWas;
            RenderSettings.ambientMode = ambModeWas;
            RenderSettings.ambientLight = ambLightWas;
            if (camGO != null) Object.DestroyImmediate(camGO);
            if (l1go != null) Object.DestroyImmediate(l1go);
            if (l2go != null) Object.DestroyImmediate(l2go);
            if (l3go != null) Object.DestroyImmediate(l3go);
            if (root != null) Object.DestroyImmediate(root);
            foreach (var m in clones) if (m != null) Object.DestroyImmediate(m);
            Object.DestroyImmediate(rtBig);
        }
    }

    private static Texture2D ReadRT(RenderTexture src, int size)
    {
        if (size >= src.width) return ReadRaw(src, src.width);
        var tmp = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, tmp);
        var t = ReadRaw(tmp, size);
        RenderTexture.ReleaseTemporary(tmp);
        return t;
    }

    private static Texture2D ReadRaw(RenderTexture rt, int size)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
        t.Apply();
        RenderTexture.active = prev;
        return t;
    }

    private static float Occupancy(Texture2D t)
    {
        var px = t.GetPixels32();
        int occ = 0;
        for (int i = 0; i < px.Length; i++) if (px[i].a > 26) occ++;
        return occ / (float)px.Length;
    }

    private static Vector3 SphericalDir(float pitchDeg, float yawDeg)
    {
        float p = pitchDeg * Mathf.Deg2Rad, y = yawDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(p) * Mathf.Sin(y), Mathf.Sin(p), Mathf.Cos(p) * Mathf.Cos(y));
    }

    /// <summary>实例级材质提亮：暗色道具（软盘深蓝盘体）在深色 UI 面板上需要抬高明度才读得出来。</summary>
    private static void LiftMaterials(Renderer r, float f, List<Material> clones)
    {
        var src = r.sharedMaterials;
        var dst = new Material[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            if (src[i] == null) { dst[i] = null; continue; }
            var m = new Material(src[i]);
            clones.Add(m);
            string prop = m.HasProperty("_Color") ? "_Color" : (m.HasProperty("_BaseColor") ? "_BaseColor" : null);
            if (prop != null)
            {
                Color c = m.GetColor(prop);
                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                v = Mathf.Clamp01(v * f);
                m.SetColor(prop, Color.HSVToRGB(h, s, v));
            }
            if (m.HasProperty("_EmissionColor"))
            {
                var e = m.GetColor("_EmissionColor");
                m.SetColor("_EmissionColor", e * f);
            }
            dst[i] = m;
        }
        r.sharedMaterials = dst;
    }

    private static void ConfigureSprite(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 512;
        ti.SaveAndReimport();
    }
}
