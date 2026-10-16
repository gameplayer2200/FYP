using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 哆啦A梦风格"任意门"生成器（程序化建模，不依赖外部模型，无版权问题）。
///
/// ── 设计意图：怪诞感 ───────────────────────────────────────────────
/// 亮粉色 + 半圆拱顶 + 厚实门框 + 大球形金色门把，材质做成"卡通塑料感"
/// （高光滑度、零金属度）。它独立站立、没有墙面依托，放进写实的科幻场景里
/// 就形成风格冲突——这正是要的荒诞感。
///
/// ── 节点结构（重要） ──────────────────────────────────────────────
///   AnywhereDoor_Doraemon         根
///   ├── Frame                     门框（静态，拱形环）
///   ├── Hinge                     合页枢轴（开门时旋转）
///   │   └── Leaf                  门扇（拱形板 + 门把）
///   └── PortalSurface             传送面（静态，拱形薄板）★
///
///   ★ PortalSurface 挂在**根节点**下，是 Hinge 的**兄弟**而非子物体。
///     因此它完全不受开门/关门影响：门扇转走、传送面原地不动，
///     始终稳定地作为「另一个世界视野」的渲染目标。
///     做任何结构调整时**不要**把它挪到 Hinge 底下。
///
/// ── 与 PortalWindow 的对接 ────────────────────────────────────────
/// PortalWindow 需要两样东西，本项目用同一个对象兼任：
///   1) 门洞平面 —— 由它的网格包围盒反算四角，用于偏心视锥取景；
///   2) 投影屏   —— 运行时把它的材质换成 Custom/PortalPanelProj。
/// 在 PortalWindow 的 portalSurface 字段里挂上本节点即可（留空则回退到旧的
/// "Cube.001 + openingPath" 逻辑，兼容旧门）。
///
/// 菜单：FYP/生成任意门
/// </summary>
public static class AnywhereDoorBuilder
{
    // ---- 尺寸（米）----
    // 对齐现有传送门门洞实测值 1.225 x 2.106，避免换门后取景比例变化
    const float InnerW = 1.22f;      // 门洞宽
    const float InnerH = 2.10f;      // 门洞高（贴地，底部无框）
    const float BorderW = 0.13f;     // 门框边框宽度
    const float FrameDepth = 0.20f;  // 门框厚度
    const float LeafDepth = 0.11f;   // 门扇厚度
    const float LeafGap = 0.008f;    // 门扇与门框的缝隙

    const float FrameW = InnerW + BorderW * 2f;   // 1.48
    const float FrameH = InnerH + BorderW;        // 2.23

    const int ArcSegs = 28;          // 拱顶细分

    // ---- 颜色 ----
    static readonly Color PinkColor = new Color(0.96f, 0.55f, 0.74f, 1f);
    static readonly Color PinkDeep = new Color(0.86f, 0.40f, 0.62f, 1f);
    static readonly Color KnobColor = new Color(1.00f, 0.84f, 0.30f, 1f);
    static readonly Color ScreenDark = new Color(0.05f, 0.05f, 0.07f, 1f); // 传送面未激活时的底色

    const string OutDir = "Assets/Role/Player/Portal";
    const string PrefabPath = OutDir + "/AnywhereDoor_Doraemon.prefab";

    [MenuItem("FYP/生成任意门")]
    public static void Build()
    {
        EnsureDir(OutDir);

        // 先清掉旧产物再重建。
        // 坑：在同一次调用里「删掉某资产 + 在同一路径重新创建」会被 AssetDatabase 静默吞掉，
        // 表现为 prefab 里三个程序化网格的引用全变成 NULL（内置的 Sphere/Cylinder 不受影响）。
        // 所以删完必须先 Refresh 一次，再开始创建。
        DeleteIfExists(PrefabPath);
        DeleteIfExists(OutDir + "/Mesh_DoorFrame.asset");
        DeleteIfExists(OutDir + "/Mesh_DoorLeaf.asset");
        DeleteIfExists(OutDir + "/Mesh_PortalSurface.asset");
        AssetDatabase.Refresh();

        float leafW = InnerW - LeafGap * 2f;
        float leafH = InnerH - LeafGap;

        // ---- 网格 ----
        var frameMesh = BuildArchRing(FrameW, FrameH, InnerW, InnerH, FrameDepth, ArcSegs);
        var leafMesh = BuildArchSlab(leafW, leafH, LeafDepth, ArcSegs);
        var screenMesh = BuildArchPlate(InnerW, InnerH, ArcSegs);
        SaveMesh(frameMesh, OutDir + "/Mesh_DoorFrame.asset");
        SaveMesh(leafMesh, OutDir + "/Mesh_DoorLeaf.asset");
        SaveMesh(screenMesh, OutDir + "/Mesh_PortalSurface.asset");

        // ---- 材质 ----
        var matPink = MakeMat("M_DoorPink", PinkColor, 0.55f, 0f);
        var matPinkDeep = MakeMat("M_DoorPinkDeep", PinkDeep, 0.35f, 0f);
        var matKnob = MakeMat("M_DoorKnob", KnobColor, 0.85f, 0.9f);
        var matScreen = MakeMat("M_PortalSurfaceDark", ScreenDark, 0.1f, 0f);

        // ---- 组装 ----
        var root = new GameObject("AnywhereDoor_Doraemon");

        // 门框（静态）
        NewChild(root.transform, "Frame", Vector3.zero, frameMesh, matPink);

        // 合页 + 门扇
        var hinge = new GameObject("Hinge");
        hinge.transform.SetParent(root.transform, false);
        hinge.transform.localPosition = new Vector3(-InnerW * 0.5f, 0f, 0f);  // 枢轴在门洞左边缘

        var leaf = NewChild(hinge.transform, "Leaf",
            new Vector3(leafW * 0.5f, LeafGap * 0.5f, 0f), leafMesh, matPink);
        leaf.GetComponent<MeshRenderer>().sharedMaterials = new[] { matPink, matPinkDeep };

        // 门把：金色球 + 短柄（偏下，低幼感）
        float knobX = leafW * 0.5f - 0.12f;
        float knobY = leafH * 0.42f;
        var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knob.name = "Knob";
        knob.transform.SetParent(leaf.transform, false);
        knob.transform.localPosition = new Vector3(knobX, knobY, LeafDepth * 0.5f + 0.035f);
        knob.transform.localScale = Vector3.one * 0.105f;
        Object.DestroyImmediate(knob.GetComponent<Collider>());
        knob.GetComponent<MeshRenderer>().sharedMaterial = matKnob;

        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "KnobStem";
        stem.transform.SetParent(leaf.transform, false);
        stem.transform.localPosition = new Vector3(knobX, knobY, LeafDepth * 0.5f + 0.012f);
        stem.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        stem.transform.localScale = new Vector3(0.055f, 0.012f, 0.055f);
        Object.DestroyImmediate(stem.GetComponent<Collider>());
        stem.GetComponent<MeshRenderer>().sharedMaterial = matKnob;

        // ★ 传送面：挂在根下（Hinge 的兄弟），开关门不影响它。
        //   位置取**门框中心平面**（local z = 0），不是贴后侧。原因：
        //   门板也是居中的实体，中心平面正好落在门板厚度内部 —— 关门时被门板从
        //   正、反两面同时遮住，开门时才露出来。放在后侧的话，从门的背面看它会
        //   盖住门板，露出一块黑板。
        var screen = NewChild(root.transform, "PortalSurface",
            new Vector3(0f, 0f, 0f), screenMesh, matScreen);
        var sr = screen.GetComponent<MeshRenderer>();
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;

        // ---- 存 prefab ----
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[任意门] 已生成: " + PrefabPath
                  + "（门洞 " + InnerW + " x " + InnerH + " m，PortalSurface 独立于 Hinge）");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    // ==================== 网格生成 ====================

    /// <summary>
    /// 拱形轮廓（逆时针，X 居中）。底边平、顶部半圆。
    /// 返回点数 = 2 + arcSegs + 1，内外轮廓用同一参数化，便于成环时按索引对应。
    /// </summary>
    static List<Vector2> ArchOutline(float w, float h, int arcSegs)
    {
        var pts = new List<Vector2>(arcSegs + 3);
        float r = w * 0.5f;
        float straightTop = h - r;
        pts.Add(new Vector2(-r, 0f));                 // 左下
        pts.Add(new Vector2(-r, straightTop));        // 左侧直边顶端
        for (int i = 1; i <= arcSegs; i++)            // 圆弧：左上 → 右上（跳过与上一点重复的 i=0）
        {
            float t = Mathf.PI * (1f - (float)i / arcSegs);
            pts.Add(new Vector2(r * Mathf.Cos(t), straightTop + r * Mathf.Sin(t)));
        }
        pts.Add(new Vector2(r, 0f));                  // 右下
        return pts;
    }

    /// <summary>实心拱形板（前后封面 + 侧壁）。</summary>
    static Mesh BuildArchSlab(float w, float h, float depth, int arcSegs)
    {
        var o = ArchOutline(w, h, arcSegs);
        int n = o.Count;
        float hz = depth * 0.5f;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int i = 0; i < n; i++) verts.Add(new Vector3(o[i].x, o[i].y, hz));    // 正面 0..n-1
        for (int i = 0; i < n; i++) verts.Add(new Vector3(o[i].x, o[i].y, -hz));   // 背面 n..2n-1

        Vector2 c = Centroid(o);
        int ciF = verts.Count; verts.Add(new Vector3(c.x, c.y, hz));
        int ciB = verts.Count; verts.Add(new Vector3(c.x, c.y, -hz));

        for (int i = 0; i < n; i++)
        {
            int a = i, b = (i + 1) % n;
            // 绕序约定（用内置 Quad 反推得出）：逆时针 → 法线 +Z。
            // 拱形轮廓本身是顺时针，所以正面扇形要**反向**取 (b, a)。
            tris.Add(ciF); tris.Add(b); tris.Add(a);              // 正面（法线 +Z）
            tris.Add(ciB); tris.Add(n + a); tris.Add(n + b);      // 背面（法线 -Z）
            tris.Add(a); tris.Add(b); tris.Add(n + b);            // 侧壁
            tris.Add(a); tris.Add(n + b); tris.Add(n + a);
        }
        for (int i = 0; i < verts.Count; i++) uvs.Add(new Vector2(verts[i].x / w + 0.5f, verts[i].y / h));

        return Finish("Mesh_DoorLeaf", verts, tris, uvs);
    }

    /// <summary>拱形门框：外轮廓与内轮廓之间成环（含前后环面与内外侧壁）。</summary>
    static Mesh BuildArchRing(float ow, float oh, float iw, float ih, float depth, int arcSegs)
    {
        var outer = ArchOutline(ow, oh, arcSegs);
        var inner = ArchOutline(iw, ih, arcSegs);   // 两者 X 均已居中，可直接按索引配对
        int n = outer.Count;
        float hz = depth * 0.5f;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int i = 0; i < n; i++) verts.Add(new Vector3(outer[i].x, outer[i].y, hz));   // 0..n-1     外·正
        for (int i = 0; i < n; i++) verts.Add(new Vector3(inner[i].x, inner[i].y, hz));   // n..2n-1    内·正
        for (int i = 0; i < n; i++) verts.Add(new Vector3(outer[i].x, outer[i].y, -hz));  // 2n..3n-1   外·背
        for (int i = 0; i < n; i++) verts.Add(new Vector3(inner[i].x, inner[i].y, -hz));  // 3n..4n-1   内·背

        for (int i = 0; i < n; i++)
        {
            int a = i, b = (i + 1) % n;
            int ia = n + a, ib = n + b;
            int oa2 = 2 * n + a, ob2 = 2 * n + b;
            int ia2 = 3 * n + a, ib2 = 3 * n + b;

            tris.Add(a); tris.Add(ia); tris.Add(ib);          // 正面环
            tris.Add(a); tris.Add(ib); tris.Add(b);
            tris.Add(oa2); tris.Add(ib2); tris.Add(ia2);      // 背面环
            tris.Add(oa2); tris.Add(ob2); tris.Add(ib2);
            tris.Add(a); tris.Add(ob2); tris.Add(oa2);        // 外侧壁
            tris.Add(a); tris.Add(b); tris.Add(ob2);
            tris.Add(ia); tris.Add(ib2); tris.Add(ib);        // 内侧壁
            tris.Add(ia); tris.Add(ia2); tris.Add(ib2);
        }
        for (int i = 0; i < verts.Count; i++) uvs.Add(new Vector2(verts[i].x / ow + 0.5f, verts[i].y / oh));

        return Finish("Mesh_DoorFrame", verts, tris, uvs);
    }

    /// <summary>
    /// 拱形薄板（双面），用于传送面。
    /// 注意：它是「竖直板」——法线沿本地 Z，宽沿 X、高沿 Y。
    /// PortalWindow.ReadCorners 会据此判断朝向（最薄的轴 = 法线）。
    /// </summary>
    static Mesh BuildArchPlate(float w, float h, int arcSegs)
    {
        var o = ArchOutline(w, h, arcSegs);
        int n = o.Count;
        Vector2 c = Centroid(o);

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        // 正面（法线 +Z）。轮廓是顺时针，需反取 (b, a) 才是逆时针。
        for (int i = 0; i < n; i++) verts.Add(new Vector3(o[i].x, o[i].y, 0f));
        int ciF = verts.Count; verts.Add(new Vector3(c.x, c.y, 0f));
        for (int i = 0; i < n; i++)
        {
            int a = i, b = (i + 1) % n;
            tris.Add(ciF); tris.Add(b); tris.Add(a);
        }

        // 反面：**复制一份顶点**再反绕序。若与正面共用顶点，RecalculateNormals 会把
        // 两侧法线平均成零向量，结果是两面都不受光、整块发黑。
        int b0 = verts.Count;
        for (int i = 0; i < n; i++) verts.Add(new Vector3(o[i].x, o[i].y, 0f));
        int ciB = verts.Count; verts.Add(new Vector3(c.x, c.y, 0f));
        for (int i = 0; i < n; i++)
        {
            int a = i, b = (i + 1) % n;
            tris.Add(ciB); tris.Add(b0 + a); tris.Add(b0 + b);
        }

        for (int i = 0; i < verts.Count; i++) uvs.Add(new Vector2(verts[i].x / w + 0.5f, verts[i].y / h));
        return Finish("Mesh_PortalSurface", verts, tris, uvs);
    }

    static Vector2 Centroid(List<Vector2> pts)
    {
        Vector2 c = Vector2.zero;
        foreach (var p in pts) c += p;
        return c / pts.Count;
    }

    static Mesh Finish(string name, List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        var m = new Mesh { name = name };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.SetUVs(0, uvs);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ==================== 辅助 ====================

    static GameObject NewChild(Transform parent, string name, Vector3 localPos, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Material MakeMat(string name, Color c, float smoothness, float metallic)
    {
        string path = OutDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SaveMesh(Mesh m, string path)
    {
        AssetDatabase.CreateAsset(m, path);
    }

    static void DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            AssetDatabase.DeleteAsset(path);
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
