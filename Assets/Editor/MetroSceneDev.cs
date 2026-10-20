using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Metro 异世界开发工具：场景勘察与分阶段搭建（阶段 0~3）。
/// 全部为纯编辑器静态方法，经 execute_code 逐个调用，避免长片段转义问题。
/// 术语遵守 GLOSSARY：世界/门/传送门/电量/异常/异常物。
/// </summary>
public static class MetroSceneDev
{
    /// <summary>勘察报告：灯、雾、关键道具位置、整体包围盒、假人残留。</summary>
    public static string Report()
    {
        var sb = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        sb.AppendLine("== SCENE " + scene.name + " ==");

        // 雾与环境
        sb.AppendLine("Fog: " + RenderSettings.fog + " color=" + RenderSettings.fogColor
            + " mode=" + RenderSettings.fogMode + " start=" + RenderSettings.fogStartDistance
            + " end=" + RenderSettings.fogEndDistance + " density=" + RenderSettings.fogDensity);
        sb.AppendLine("Ambient: " + RenderSettings.ambientLight + " skybox=" + RenderSettings.skybox);

        // 灯
        sb.AppendLine("== LIGHTS ==");
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            sb.AppendLine(l.name + " | " + l.type + " | enabled=" + l.enabled
                + " | goActive=" + l.gameObject.activeInHierarchy
                + " | pos=" + l.transform.position.ToString("F2")
                + " | inten=" + l.intensity.ToString("F2") + " range=" + l.range.ToString("F1")
                + " | color=" + l.color + " | spot=" + l.spotAngle.ToString("F0"));
        }

        // 关键道具
        sb.AppendLine("== KEY PROPS ==");
        string[] keys = { "Electric", "Cabble", "TechRoom", "staircase", "Arch_Tunel", "Wall_Main", "door", "Train", "euroPalet", "Crate", "case" };
        foreach (var go in scene.GetRootGameObjects())
        {
            CollectByKey(go.transform, keys, sb, 0);
        }

        // 整体包围盒（所有渲染器）
        sb.AppendLine("== BOUNDS ==");
        bool first = true; Bounds total = default;
        int rendererCount = 0, colliderCount = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (first) { total = r.bounds; first = false; }
            else total.Encapsulate(r.bounds);
            rendererCount++;
        }
        foreach (var c in Object.FindObjectsOfType<Collider>(true)) colliderCount++;
        sb.AppendLine("renderers=" + rendererCount + " colliders=" + colliderCount
            + " total=" + total.min.ToString("F1") + " ~ " + total.max.ToString("F1"));

        // 假人/展示残留
        sb.AppendLine("== SHOWCASE LEFTOVERS ==");
        foreach (var n in new[] { "Player", "Camera", "CameaHolder", "Showcase_DirectionalLight", "__EA_SYSTEM__", "__EA_ART__", "Metro_PRO_Information" })
        {
            var go = GameObject.Find(n);
            if (go != null)
                sb.AppendLine(n + " active=" + go.activeSelf
                    + " pos=" + go.transform.position.ToString("F1")
                    + " comps=" + (go.GetComponent<Camera>() != null ? "Camera " : "") + (go.GetComponent<Light>() != null ? "Light " : ""));
        }

        // 门
        sb.AppendLine("== DOOR ==");
        var door = GameObject.Find("anywhere-door-Metro");
        if (door != null)
        {
            sb.AppendLine("door pos=" + door.transform.position.ToString("F2")
                + " rot=" + door.transform.eulerAngles.ToString("F1")
                + " fwd=" + door.transform.forward.ToString("F2"));
        }
        return sb.ToString();
    }

    private static void CollectByKey(Transform t, string[] keys, StringBuilder sb, int depth)
    {
        string n = t.name;
        foreach (var k in keys)
        {
            if (n.ToLowerInvariant().Contains(k.ToLowerInvariant()))
            {
                sb.AppendLine(n + " | active=" + t.gameObject.activeSelf
                    + " | pos=" + t.position.ToString("F1") + " | childCount=" + t.childCount);
                break;
            }
        }
        if (depth < 2)
            for (int i = 0; i < t.childCount; i++)
                CollectByKey(t.GetChild(i), keys, sb, depth + 1);
    }

    /// <summary>离屏渲染一张 PNG（编辑模式可用）。path 相对工程根（写到 PortalDumps/ 避免进 Assets）。hotBg=true 用亮品红背景探几何缺口。</summary>
    public static string Screenshot(float px, float py, float pz, float lx, float ly, float lz, string fileName, int width = 960, int height = 540, float fov = 60f, bool hotBg = false)
    {
        var go = new GameObject("DevShotCam");
        var cam = go.AddComponent<Camera>();
        go.transform.position = new Vector3(px, py, pz);
        go.transform.LookAt(new Vector3(lx, ly, lz));
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 250f;
        cam.clearFlags = CameraClearFlags.SolidColor; // 深色背景，避免默认蓝天色误导缺口判断
        cam.backgroundColor = hotBg ? new Color(1f, 0f, 1f, 1f) : new Color(0.01f, 0.02f, 0.02f, 1f);
        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        byte[] png = ImageConversion.EncodeToPNG(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "PortalDumps");
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        string full = System.IO.Path.Combine(dir, fileName);
        System.IO.File.WriteAllBytes(full, png);
        return full;
    }

    /// <summary>一次性拍布局勘察六连图。</summary>
    public static string SurveyShots()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Screenshot(-44f, 3.5f, 140f, -47.9f, 1.5f, 132.7f, "dev_door_area.png"));
        sb.AppendLine(Screenshot(-45f, 2.5f, 131f, -42f, 2f, 165f, "dev_tunnelA_look+Z.png"));
        sb.AppendLine(Screenshot(-39f, 4f, 120f, -55f, 3f, 95f, "dev_tunnelB_look.png", fov: 75f));
        sb.AppendLine(Screenshot(-12f, 22f, 168f, -16f, 15f, 152f, "dev_upperhall.png", fov: 70f));
        sb.AppendLine(Screenshot(-18f, 12f, 122f, -24f, 8f, 132f, "dev_stairs.png"));
        sb.AppendLine(Screenshot(-47f, 4f, 50f, -48f, 2f, 30f, "dev_farZ_end.png"));
        return sb.ToString();
    }

    // ================= 阶段 0：环境整备 =================

    /// <summary>玩法对象总根（场景层级组织用）。</summary>
    private static Transform GameRoot()
    {
        var root = GameObject.Find("MetroGameplay");
        if (root == null) root = new GameObject("MetroGameplay");
        return root.transform;
    }

    /// <summary>阶段0：禁展示残留/无效 Volume，调雾与灯，加门区常亮灯。可重复执行。</summary>
    public static string Stage0_Env()
    {
        var sb = new StringBuilder();

        // 1) 展示残留（PAEA01 假人/展示相机/信息牌/展示平行光）与 Built-in 下无效的 Volume
        string[] disableNames = { "Player", "Player (#1)", "CameaHolder", "Camera", "Camera (#1)",
            "Metro_PRO_Information", "Showcase_DirectionalLight", "Showcase_DirectionalLight (#1)",
            "Global Volume", "Global Volume (#1)", "Volume (#1)" };
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            DisableByName(root.transform, disableNames, sb);

        // 2) 雾：保留青绿基调，拉远减淡（修「取景近乎全黑」）
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 15f;
        RenderSettings.fogEndDistance = 170f;
        RenderSettings.fogColor = new Color(0f, 0.42f, 0.33f, 1f);
        sb.AppendLine("fog set 15~170 teal-dark");

        // 3) 灯：隧道两列点灯与上层暗灯提亮
        int tuned = 0;
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            if (l.name == "Point Light (10)" && l.intensity < 1.7f) { l.intensity = 1.7f; tuned++; }
            else if (l.name == "Point Light (9)" && l.intensity < 2.2f) { l.intensity = 2.2f; tuned++; }
            else if (l.name.StartsWith("Area Light - start game") && l.intensity < 0.35f) { l.intensity = 0.35f; tuned++; }
        }
        sb.AppendLine("lights tuned: " + tuned);

        // 4) 门区常亮灯（归途灯塔，不进任何灯组）
        var rootT = GameRoot();
        Transform beacon = rootT.Find("DoorBeaconLight");
        if (beacon == null)
        {
            var bgo = new GameObject("DoorBeaconLight");
            bgo.transform.SetParent(rootT, false);
            bgo.transform.position = new Vector3(-47.9f, 3.4f, 132.7f);
            var bl = bgo.AddComponent<Light>();
            bl.type = LightType.Point; bl.range = 13f; bl.intensity = 1.3f;
            bl.color = new Color(1f, 0.85f, 0.6f);
            sb.AppendLine("door beacon created");
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    private static void DisableByName(Transform t, string[] names, StringBuilder sb)
    {
        foreach (var n in names)
        {
            if (t.name == n && t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(false);
                sb.AppendLine("disabled: " + n + " (parent=" + (t.parent != null ? t.parent.name : "<root>") + ")");
            }
        }
        for (int i = 0; i < t.childCount; i++) DisableByName(t.GetChild(i), names, sb);
    }

    /// <summary>验证阶段0结果 + 重拍对比图。</summary>
    public static string Verify0()
    {
        var sb = new StringBuilder();
        foreach (var n in new[] { "Player", "Camera", "CameaHolder", "Metro_PRO_Information", "Showcase_DirectionalLight", "Global Volume" })
        {
            var g = GameObject.Find(n);
            sb.Append(n + "=" + (g == null ? "GONE" : (g.activeSelf ? "ON" : "OFF")) + "; ");
        }
        sb.AppendLine();
        sb.AppendLine(Screenshot(-44f, 3.5f, 140f, -47.9f, 1.5f, 132.7f, "dev_door_v2.png"));
        sb.AppendLine(Screenshot(-45f, 2.5f, 131f, -42f, 2f, 165f, "dev_tunnelA_v2.png"));
        sb.AppendLine(Screenshot(-12f, 22f, 168f, -16f, 15f, 152f, "dev_upperhall_v2.png", 960, 540, 70f));
        return sb.ToString();
    }

    /// <summary>上层两级平台玩家眼高四向快照（查真实漏空与可读性）。</summary>
    public static string UpperHallShots()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Screenshot(-12f, 8.6f, 190f, -12f, 8.0f, 150f, "dev_L1_look-Z.png"));   // 6.9 层向 -Z
        sb.AppendLine(Screenshot(-12f, 8.6f, 150f, -12f, 8.0f, 195f, "dev_L1_look+Z.png"));   // 6.9 层向 +Z
        sb.AppendLine(Screenshot(-14f, 16.6f, 158f, -14f, 16.0f, 190f, "dev_L2_look+Z.png")); // 14.9 层向 +Z
        sb.AppendLine(Screenshot(-14f, 16.6f, 180f, -14f, 16.0f, 150f, "dev_L2_look-Z.png")); // 14.9 层向 -Z
        sb.AppendLine(Screenshot(-14f, 16.6f, 165f, -3f, 16.0f, 165f, "dev_L2_look+E.png"));  // 14.9 层向东
        return sb.ToString();
    }

    /// <summary>亮品红背景探缺：疑似缺口方向复拍（显示品红=真缺口）。</summary>
    public static string GapProbeShots()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Screenshot(-12f, 8.6f, 190f, -32f, 7.5f, 190f, "gap_L1_west.png", 960, 540, 90f, true));
        sb.AppendLine(Screenshot(-14f, 16.6f, 168f, -14f, 16.0f, 138f, "gap_L2_south.png", 960, 540, 90f, true));
        sb.AppendLine(Screenshot(-14f, 17.2f, 165f, -14f, 26f, 165f, "gap_L2_up.png", 960, 540, 95f, true));
        sb.AppendLine(Screenshot(-14f, 8.6f, 170f, -14f, 8.0f, 210f, "gap_L1_east.png", 960, 540, 90f, true));
        return sb.ToString();
    }

    /// <summary>定点查地面：对若干 (x,z) 列出全部命中（高度+法线y），不做过滤，供人工判读。</summary>
    public static string FloorAt()
    {
        Vector2[] spots = {
            new Vector2(-46f, 133f), new Vector2(-44f, 129f), new Vector2(-41f, 130f),   // 门区/隧道A
            new Vector2(-41f, 185f), new Vector2(-41f, 190f), new Vector2(-41f, 195f), new Vector2(-44f, 190f), // +Z 隧道端（车厢选址）
            new Vector2(-47f, 32f), new Vector2(-45f, 35f), new Vector2(-50f, 30f), new Vector2(-52f, 33f)      // -Z 死端（配电柜）
        };
        var sb = new StringBuilder();
        foreach (var s in spots)
        {
            var hits = Physics.RaycastAll(new Vector3(s.x, 30f, s.y), Vector3.down, 60f);
            var list = new System.Collections.Generic.List<float>();
            foreach (var h in hits)
            {
                if (h.collider != null && h.collider.name.StartsWith("Seal_")) continue;
                list.Add(Mathf.Round(h.point.y * 2f) / 2f);
            }
            list.Sort();
            var parts = new System.Collections.Generic.List<string>();
            foreach (var y in list) parts.Add(y.ToString("F1"));
            sb.AppendLine("(" + s.x + "," + s.y + ") -> " + string.Join("/", parts.ToArray()));
        }
        return sb.ToString();
    }

    /// <summary>列出若干点半径内的碰撞体（名称+包围盒），判断地面到底是什么。</summary>
    public static string ColliderDump()
    {
        Vector3[] centers = {
            new Vector3(-46f, 1.2f, 133f),  // 门区地面
            new Vector3(-41f, 1.2f, 160f),  // 隧道A 中段地面
            new Vector3(-47f, 2f, 32f),     // -Z 死端
            new Vector3(-41f, 2f, 190f),    // +Z 隧道端
            new Vector3(-12f, 13f, 160f)    // 大厅空洞
        };
        var sb = new StringBuilder();
        foreach (var c in centers)
        {
            sb.AppendLine("== around " + c.ToString("F1") + " ==");
            var cols = Physics.OverlapSphere(c, 5f);
            foreach (var col in cols)
            {
                var b = col.bounds;
                sb.AppendLine(col.name + " [" + col.GetType().Name + "] y:" + b.min.y.ToString("F1") + "~" + b.max.y.ToString("F1")
                    + " x:" + b.min.x.ToString("F0") + "~" + b.max.x.ToString("F0") + " z:" + b.min.z.ToString("F0") + "~" + b.max.z.ToString("F0"));
            }
        }
        return sb.ToString();
    }

    /// <summary>大厅地板空洞测绘：细网格找「无 y∈[13,17] 命中」的列，输出矩形洞区清单。</summary>
    public static string HallHoles()
    {
        const float x0 = -22f, x1 = 0f, z0 = 138f, z1 = 196f, step = 3f;
        var sb = new StringBuilder();
        int holes = 0;
        for (float x = x0; x <= x1; x += step)
        {
            var row = new System.Text.StringBuilder();
            for (float z = z0; z <= z1; z += step)
            {
                var hits = Physics.RaycastAll(new Vector3(x, 20f, z), Vector3.down, 35f);
                bool hasFloor = false;
                foreach (var h in hits)
                {
                    // 排除封壳顶/墙，但 Seal_Floor_* 楼板是有效地面
                    if (h.collider != null && (h.collider.name.StartsWith("Seal_Wall_") || h.collider.name.StartsWith("Seal_Ceiling"))) continue;
                    if (h.point.y >= 13f && h.point.y <= 17.5f) { hasFloor = true; break; }
                }
                row.Append(hasFloor ? '#' : 'o');
                if (!hasFloor) holes++;
            }
            sb.AppendLine("x=" + x.ToString("F0") + " " + row.ToString());
        }
        sb.AppendLine("hole cells: " + holes);
        return sb.ToString();
    }

    /// <summary>地面测绘：网格射线向下打点，只取法线朝上的命中（=地面），输出 x z y。</summary>
    public static string FloorMap()
    {
        var sb = new StringBuilder();
        for (float z = 28f; z <= 210f; z += 10f)
        {
            for (float x = -60f; x <= -2f; x += 6f)
            {
                var origin = new Vector3(x, 30f, z);
                var hits = Physics.RaycastAll(origin, Vector3.down, 60f);
                float best = float.MinValue;
                foreach (var h in hits)
                {
                    if (h.collider != null && h.collider.name.StartsWith("Seal_")) continue; // 封壳不算地面
                    if (h.normal.y > 0.5f && h.point.y > best) best = h.point.y; // 最高的朝上面 = 该柱真正地面
                }
                if (best > float.MinValue)
                {
                    float y = Mathf.Round(best * 2f) / 2f;
                    sb.AppendLine(Mathf.RoundToInt(x) + " " + Mathf.RoundToInt(z) + " " + y.ToString("F1"));
                }
            }
        }
        return sb.ToString();
    }

    // ================= 阶段 0c：补地板（轨道层/上层两级） =================

    /// <summary>轨道层抽样：30 个点向下射线，统计 y<1 命中数（判断是否缺地面）。</summary>
    public static string TrackFloorCheck()
    {
        int hit = 0, miss = 0;
        var sb = new StringBuilder();
        for (int i = 0; i < 30; i++)
        {
            float x = -62f + (i % 6) * 6f;      // -62..-32
            float z = 40f + (i / 6) * 30f;      // 40..190
            var hits = Physics.RaycastAll(new Vector3(x, 8f, z), Vector3.down, 12f);
            float best = float.MinValue;
            foreach (var hh in hits)
                if (hh.collider != null && !hh.collider.name.StartsWith("Seal_") && hh.point.y < 1f && hh.point.y > best)
                    best = hh.point.y;
            if (best > float.MinValue) { hit++; sb.Append("(" + x + "," + z + ")=" + best.ToString("F1") + " "); }
            else { miss++; }
        }
        return "hit=" + hit + " miss=" + miss + "  " + sb.ToString();
    }

    /// <summary>阶段0c：两块暗色楼板补齐上层缺失地面（L1 6.9 / 大厅 14.9，顶面与既有地板齐平或略低 5cm）。轨道层经抽样已有 y=0.1 地板，无需补。</summary>
    public static string Stage0c_Floors()
    {
        var root = GameRoot();
        Transform seal = root.Find("Sealing");
        if (seal == null) seal = new GameObject("Sealing").transform;
        seal.SetParent(root, false);
        var mat = GetSealMaterial();

        Vector3[] c = {
            new Vector3(-11f, 6.75f, 193f),    // L1：x -23~1, z 174~212
            new Vector3(-11f, 14.75f, 165.5f)  // 大厅：x -23~1, z 134~197
        };
        Vector3[] s = {
            new Vector3(24f, 0.3f, 38f),
            new Vector3(24f, 0.3f, 63f)
        };
        string[] names = { "Seal_Floor_L1", "Seal_Floor_Hall" };

        for (int i = 0; i < names.Length; i++)
        {
            if (seal.Find(names[i]) != null) continue;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = names[i];
            go.transform.SetParent(seal, false);
            go.transform.position = c[i];
            go.transform.localScale = s[i];
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "floors added (3)";
    }

    // ================= 通用小工具 =================

    private static Transform EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    /// <summary>实例化一个模型并归一化到目标最大边长（米），返回根。</summary>
    private static GameObject InstantiateModel(string assetPath, string goName, Vector3 pos, float targetSize, float yaw)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (src == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        if (go == null) go = Object.Instantiate(src);
        go.name = goName;
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            bool first = true; Bounds b = default;
            foreach (var r in rends) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 0.001f) go.transform.localScale = go.transform.localScale * (targetSize / maxDim);
        }
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    private static void SetPrivate(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetText(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Light FindLightByNameAndBox(string name, Vector3 bmin, Vector3 bmax)
    {
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            if (l.name != name) continue;
            var p = l.transform.position;
            if (p.x >= bmin.x && p.x <= bmax.x && p.y >= bmin.y && p.y <= bmax.y && p.z >= bmin.z && p.z <= bmax.z)
                return l;
        }
        return null;
    }

    /// <summary>程序化软盘（源 fbx/glb 资产导入失败，改用图元拼装；低多边形风格与场景统一）。</summary>
    private static GameObject BuildFloppy(string name, Vector3 pos, float yaw)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "盘体";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.32f, 0.03f, 0.32f);
        var bm = new Material(Shader.Find("Standard"));
        bm.color = new Color(0.13f, 0.17f, 0.28f);
        bm.EnableKeyword("_EMISSION");
        bm.SetColor("_EmissionColor", new Color(0.04f, 0.1f, 0.16f));
        AssetDatabase.CreateAsset(bm, "Assets/Material/MetroFloppy_Body.mat");
        body.GetComponent<Renderer>().sharedMaterial = bm;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var shutter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shutter.name = "金属滑片";
        shutter.transform.SetParent(root.transform, false);
        shutter.transform.localPosition = new Vector3(0f, 0.001f, 0.095f);
        shutter.transform.localScale = new Vector3(0.13f, 0.034f, 0.075f);
        var sm = new Material(Shader.Find("Standard"));
        sm.color = new Color(0.75f, 0.78f, 0.82f);
        sm.SetFloat("_Metallic", 0.85f);
        AssetDatabase.CreateAsset(sm, "Assets/Material/MetroFloppy_Shutter.mat");
        shutter.GetComponent<Renderer>().sharedMaterial = sm;
        Object.DestroyImmediate(shutter.GetComponent<Collider>());

        var label = GameObject.CreatePrimitive(PrimitiveType.Cube);
        label.name = "标签";
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.001f, -0.075f);
        label.transform.localScale = new Vector3(0.22f, 0.033f, 0.11f);
        var lm = new Material(Shader.Find("Standard"));
        lm.color = new Color(0.82f, 0.8f, 0.72f);
        AssetDatabase.CreateAsset(lm, "Assets/Material/MetroFloppy_Label.mat");
        label.GetComponent<Renderer>().sharedMaterial = lm;
        Object.DestroyImmediate(label.GetComponent<Collider>());

        return root;
    }

    // ================= 阶段 1：异常物回收闭环 =================

    /// <summary>阶段1a：Metro 内摆放 软盘#1（门区箱上）与 万用表（上层大厅木板上）。</summary>
    public static string Stage1_Items()
    {
        var items = EnsureChild(GameRoot(), "Items");
        var sb = new StringBuilder();

        var floppy = BuildFloppy("Specimen_软盘1", new Vector3(-47.6f, 1.35f, 128.3f), 25f);
        floppy.transform.SetParent(items, true);
        floppy.AddComponent<SpecimenPickup>();
        SetText(floppy.AddComponent<InteractionLabel>(), "text", "回收异常物·软盘");
        sb.AppendLine("floppy1 ok(procedural)");

        var meter = InstantiateModel("Assets/items/RandomObjects/multimeter/multimeter.glb", "道具_万用表",
            new Vector3(-8f, 15.5f, 160.5f), 0.28f, 160f);
        if (meter != null)
        {
            meter.transform.SetParent(items, true);
            meter.AddComponent<MultimeterPickup>();
            SetText(meter.AddComponent<InteractionLabel>(), "text", "拾取万用表");
            sb.AppendLine("multimeter ok");
        }
        else sb.AppendLine("multimeter MISSING MODEL");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>阶段1b：实验室加 收容台（case 模型 + SpecimenDeposit）与 SpecimenHud。</summary>
    public static string Stage1_Lab()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SpecimenLab.unity", OpenSceneMode.Additive);

        // 实验室自己的包围盒（只统计 Lab 场景的渲染器，用于校准 Metro 世界判定盒）
        bool first = true; Bounds lab = default;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r.gameObject.scene != scene) continue;
            if (first) { lab = r.bounds; first = false; } else lab.Encapsulate(r.bounds);
        }
        sb.AppendLine("Lab bounds: " + lab.min.ToString("F1") + " ~ " + lab.max.ToString("F1"));

        var depositGo = GameObject.Find("收容台");
        if (depositGo == null)
        {
            var prevActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            depositGo = InstantiateModel("Assets/items/RandomObjects/case/case_low.glb", "收容台",
                new Vector3(5.6f, 0.35f, 8.4f), 0.7f, -15f);
            if (depositGo != null)
            {
                SceneManager.MoveGameObjectToScene(depositGo, scene);
                depositGo.AddComponent<SpecimenDeposit>();
                SetText(depositGo.AddComponent<InteractionLabel>(), "text", "收容异常物");
                sb.AppendLine("deposit ok");
            }
            else sb.AppendLine("deposit MISSING MODEL");
            SceneManager.SetActiveScene(prevActive);
        }
        else sb.AppendLine("deposit exists");

        var wm = GameObject.Find("WorldManager");
        if (wm != null && wm.GetComponent<SpecimenHud>() == null)
        {
            wm.AddComponent<SpecimenHud>();
            sb.AppendLine("hud ok");
        }

        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return sb.ToString();
    }

    // ================= 阶段 2：渐渐熄灭 + 配电室谜题 =================

    /// <summary>阶段2a：建 5 个灯组 + MetroBlackout 中枢（全部序列化接线）。</summary>
    public static string Stage2_Blackout()
    {
        var blk = EnsureChild(GameRoot(), "Blackout");
        var sb = new StringBuilder();
        var allLights = Object.FindObjectsOfType<Light>(true);

        System.Func<string, GameObject> mkGroup = (string n) =>
        {
            var t = blk.Find(n);
            if (t == null) { var g = new GameObject(n); t = g.transform; t.SetParent(blk, false); }
            if (t.GetComponent<LightGroup>() == null) t.gameObject.AddComponent<LightGroup>();
            return t.gameObject;
        };
        var g1 = mkGroup("LG1_上层远厅");
        var g2 = mkGroup("LG2_上层中段");
        var g3 = mkGroup("LG3_对面隧道");
        var g4 = mkGroup("LG4_隧道A远段");
        var g5 = mkGroup("LG5_门区与楼梯");

        var l1 = new System.Collections.Generic.List<Light>();
        var l2 = new System.Collections.Generic.List<Light>();
        var l3 = new System.Collections.Generic.List<Light>();
        var l4 = new System.Collections.Generic.List<Light>();
        var l5 = new System.Collections.Generic.List<Light>();

        bool InBox(Vector3 p, Vector3 bmin, Vector3 bmax)
        {
            return p.x >= bmin.x && p.x <= bmax.x && p.y >= bmin.y && p.y <= bmax.y && p.z >= bmin.z && p.z <= bmax.z;
        }

        foreach (var l in allLights)
        {
            if (!l.gameObject.activeInHierarchy) continue;
            var p = l.transform.position;
            if (l.name == "Point Light (9)") { l3.Add(l); continue; }                 // 隧道B 全部
            if (l.name == "Point Light (10)") { if (p.z < 95f) l4.Add(l); else l5.Add(l); continue; } // 隧道A 远/近
            if (InBox(p, new Vector3(-20f, 8f, 158f), new Vector3(0f, 23.5f, 213f))) l1.Add(l);      // 上层远
            else if (InBox(p, new Vector3(-27f, 12f, 138f), new Vector3(0f, 23.5f, 158f))) l2.Add(l); // 上层中
            else if (InBox(p, new Vector3(-50f, 6f, 118f), new Vector3(-18f, 24f, 140f))) l5.Add(l); // 门区+楼梯
        }
        sb.AppendLine("L1=" + l1.Count + " L2=" + l2.Count + " L3=" + l3.Count + " L4=" + l4.Count + " L5=" + l5.Count);

        // 自发光材质（灯带/霓虹）按盒子归组
        System.Action<GameObject, System.Collections.Generic.List<Light>, Vector3, Vector3> wire =
            (go, lights, bmin, bmax) =>
        {
            var ems = new System.Collections.Generic.List<Renderer>();
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
            {
                if (ems.Count >= 60) break;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                var c = mat.GetColor("_EmissionColor");
                if (c.r < 0.02f && c.g < 0.02f && c.b < 0.02f) continue;
                var p = r.bounds.center;
                if (p.x >= bmin.x && p.x <= bmax.x && p.y >= bmin.y && p.y <= bmax.y && p.z >= bmin.z && p.z <= bmax.z)
                    ems.Add(r);
            }
            var lg = go.GetComponent<LightGroup>();
            SetPrivateArray(lg, "lights", lights.ToArray());
            SetPrivateArray(lg, "emissiveRenderers", ems.ToArray());
        };
        wire(g1, l1, new Vector3(-21f, 8f, 158f), new Vector3(1f, 24f, 213f));
        wire(g2, l2, new Vector3(-28f, 12f, 138f), new Vector3(1f, 24f, 158f));
        wire(g3, l3, new Vector3(-63f, 0f, 24f), new Vector3(-49f, 8.5f, 196f));
        wire(g4, l4, new Vector3(-48f, 0f, 24f), new Vector3(-33f, 8.5f, 95f));
        wire(g5, l5, new Vector3(-50f, 0f, 95f), new Vector3(-18f, 24f, 196f));

        // 中枢
        var mbGo = blk.GetComponentInChildren<MetroBlackout>(true);
        if (mbGo == null)
        {
            mbGo = new GameObject("MetroBlackout").AddComponent<MetroBlackout>();
            mbGo.transform.SetParent(blk, false);
        }
        SetPrivateArray(mbGo, "groups", new Object[] {
            g1.GetComponent<LightGroup>(), g2.GetComponent<LightGroup>(), g3.GetComponent<LightGroup>(),
            g4.GetComponent<LightGroup>(), g5.GetComponent<LightGroup>() });

        // 世界判定盒：z 下界抬高避开实验室（Lab 实测 z ≤ 28.4，Metro 死端谜题在 z ≥ 30）
        var soMb = new SerializedObject(mbGo);
        soMb.FindProperty("zoneCenter").vector3Value = new Vector3(-34f, 11f, 125.75f);
        soMb.FindProperty("zoneSize").vector3Value = new Vector3(72f, 42f, 192.5f);
        soMb.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("blackout wired");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>阶段2b：-Z 死端配电室谜题（平台 + 配电箱 + 三色线 + 电闸）。</summary>
    public static string Stage2_Cabinet()
    {
        var puzzle = EnsureChild(GameRoot(), "配电室");
        var sb = new StringBuilder();

        // 平台（死端碎石上找平）
        if (puzzle.Find("检修平台") == null)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "检修平台";
            plate.transform.SetParent(puzzle, false);
            plate.transform.position = new Vector3(-45.8f, 1.95f, 31.5f);
            plate.transform.localScale = new Vector3(3.4f, 0.25f, 3.2f);
            plate.GetComponent<Renderer>().sharedMaterial = GetSealMaterial();
        }

        // 配电箱：复制场景里现成的 Electric_Box_01b
        var cabGo = puzzle.Find("配电箱") != null ? puzzle.Find("配电箱").gameObject : null;
        if (cabGo == null)
        {
            Renderer src = null;
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
                if (r.name.Contains("Electric_Box_01b_PRE")) { src = r; break; }
            if (src != null)
            {
                cabGo = Object.Instantiate(src.gameObject);
                cabGo.name = "配电箱";
                cabGo.transform.SetParent(puzzle, false);
                cabGo.transform.position = new Vector3(-46.4f, 2.15f, 30.7f);
                cabGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 面朝 +Z（来路）
                sb.AppendLine("cabinet cloned");
            }
            else sb.AppendLine("cabinet SRC NOT FOUND");
        }
        var cabinet = cabGo != null && cabGo.GetComponent<PowerCabinet>() == null
            ? cabGo.AddComponent<PowerCabinet>() : (cabGo != null ? cabGo.GetComponent<PowerCabinet>() : null);

        // 三色线
        Color[] wc = { new Color(0.9f, 0.15f, 0.1f), new Color(0.95f, 0.8f, 0.1f), new Color(0.2f, 0.45f, 1f) };
        string[] wn = { "红线", "黄线", "蓝线" };
        var wires = new System.Collections.Generic.List<PowerWire>();
        for (int i = 0; i < 3; i++)
        {
            var wt = puzzle.Find("线_" + wn[i]);
            GameObject wgo;
            if (wt == null)
            {
                wgo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wgo.name = "线_" + wn[i];
                wgo.transform.SetParent(puzzle, false);
                wgo.transform.position = new Vector3(-46.8f + i * 0.4f, 2.85f, 31.15f);
                wgo.transform.localScale = new Vector3(0.045f, 0.3f, 0.045f);
                var mat = new Material(Shader.Find("Standard"));
                mat.color = wc[i];
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", wc[i] * 0.4f);
                AssetDatabase.CreateAsset(mat, "Assets/Material/MetroWire_" + i + ".mat");
                wgo.GetComponent<Renderer>().sharedMaterial = mat;
            }
            else wgo = wt.gameObject;
            var pw = wgo.GetComponent<PowerWire>();
            if (pw == null)
            {
                pw = wgo.AddComponent<PowerWire>();
                SetPrivate(pw, "cabinet", cabinet);
                SetInt(pw, "wireIndex", i);
                SetText(pw, "displayName", wn[i]);
                SetText(wgo.AddComponent<InteractionLabel>(), "text", "检测 " + wn[i]);
            }
            wires.Add(pw);
        }
        SetPrivateArray(cabinet, "wires", wires.ToArray());

        // 电闸
        var bt = puzzle.Find("电闸");
        GameObject bgo;
        if (bt == null)
        {
            bgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bgo.name = "电闸";
            bgo.transform.SetParent(puzzle, false);
            bgo.transform.position = new Vector3(-45.2f, 2.75f, 31.1f);
            bgo.transform.localScale = new Vector3(0.3f, 0.42f, 0.16f);
            bgo.GetComponent<Renderer>().sharedMaterial = GetSealMaterial();
            var lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "闸刀";
            lever.transform.SetParent(bgo.transform, false);
            lever.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            lever.transform.localScale = new Vector3(0.07f, 0.36f, 0.07f);
            Object.DestroyImmediate(lever.GetComponent<Collider>());
            var lmat = new Material(Shader.Find("Standard"));
            lmat.color = new Color(0.85f, 0.3f, 0.05f);
            lmat.EnableKeyword("_EMISSION");
            lmat.SetColor("_EmissionColor", new Color(0.6f, 0.15f, 0f));
            AssetDatabase.CreateAsset(lmat, "Assets/Material/MetroBreakerLever.mat");
            lever.GetComponent<Renderer>().sharedMaterial = lmat;
        }
        else bgo = bt.gameObject;
        var breaker = bgo.GetComponent<BreakerSwitch>();
        if (breaker == null)
        {
            breaker = bgo.AddComponent<BreakerSwitch>();
            SetPrivate(breaker, "cabinet", cabinet);
            SetPrivate(breaker, "lever", bgo.transform.Find("闸刀"));
            SetText(bgo.AddComponent<InteractionLabel>(), "text", "合上电闸");
        }
        SetPrivate(cabinet, "breaker", breaker);
        SetPrivate(cabinet, "blackout", Object.FindObjectsOfType<MetroBlackout>(true).Length > 0
            ? Object.FindObjectsOfType<MetroBlackout>(true)[0] : null);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString() + "cabinet wired";
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>阶段2c：「对面的灯」异常 + 门区充电桩（RequiresPower 门控）。</summary>
    public static string Stage2_Anomalies()
    {
        var sb = new StringBuilder();
        var anomaly = EnsureChild(GameRoot(), "异常");

        // 对面的灯：对面隧道（B）死端高处，永远到不了
        if (anomaly.Find("对面的灯") == null)
        {
            var lampGo = new GameObject("对面的灯");
            lampGo.transform.SetParent(anomaly, false);
            lampGo.transform.position = new Vector3(-55f, 5.6f, 34f);
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "灯珠";
            glow.transform.SetParent(lampGo.transform, false);
            glow.transform.localScale = Vector3.one * 0.22f;
            var gm = new Material(Shader.Find("Standard"));
            gm.color = new Color(0.85f, 1f, 1f);
            gm.EnableKeyword("_EMISSION");
            gm.SetColor("_EmissionColor", new Color(0.65f, 1f, 1f));
            AssetDatabase.CreateAsset(gm, "Assets/Material/MetroFarLight.mat");
            glow.GetComponent<Renderer>().sharedMaterial = gm;
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            var pt = lampGo.AddComponent<Light>();
            pt.type = LightType.Point; pt.range = 9f; pt.intensity = 1.6f;
            pt.color = new Color(0.75f, 1f, 1f);
            var anom = lampGo.AddComponent<UnreachableLightAnomaly>();
            SetPrivate(anom, "lamp", pt);
            SetPrivate(anom, "glowRenderer", glow.GetComponent<Renderer>());
            sb.AppendLine("far light ok");
        }

        // 充电桩：门区东侧，停电期间断电
        var items = EnsureChild(GameRoot(), "Items");
        if (items.Find("充电桩_Metro") == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/ChargeStation.prefab");
            if (prefab != null)
            {
                var cs = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                cs.name = "充电桩_Metro";
                cs.transform.SetParent(items, true);
                cs.transform.position = new Vector3(-43.6f, 0.1f, 130.2f);
                cs.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                cs.AddComponent<RequiresPower>();
                SetText(cs.AddComponent<InteractionLabel>(), "text", "充电桩（断电）");
                sb.AppendLine("charge ok");
            }
            else sb.AppendLine("charge PREFAB NOT FOUND");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    // ================= 阶段 3：尾节车厢 + 氛围 =================

    /// <summary>阶段3：隧道A 塌方前拼装尾节车厢（战利品区，复明解锁）+ 环境音频。</summary>
    public static string Stage3_Carriage()
    {
        var sb = new StringBuilder();
        var car = EnsureChild(GameRoot(), "尾节车厢");
        var mat = GetSealMaterial();

        // 车厢壳：x -42.8~-40.2, z 170~180, 地板顶 0.45
        System.Action<string, Vector3, Vector3> mk = (string n, Vector3 c, Vector3 s) =>
        {
            if (car.Find(n) != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(car, false);
            go.transform.position = c;
            go.transform.localScale = s;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        };
        mk("车厢_地板", new Vector3(-41.5f, 0.3f, 175f), new Vector3(2.7f, 0.3f, 10f));
        mk("车厢_左墙", new Vector3(-42.75f, 1.75f, 175f), new Vector3(0.15f, 2.6f, 10f));
        mk("车厢_右墙", new Vector3(-40.25f, 1.75f, 175f), new Vector3(0.15f, 2.6f, 10f));
        mk("车厢_顶", new Vector3(-41.5f, 3.1f, 175f), new Vector3(2.9f, 0.18f, 10f));
        mk("车厢_尾墙", new Vector3(-41.5f, 1.75f, 180f), new Vector3(2.7f, 2.6f, 0.15f));
        mk("门框_左柱", new Vector3(-42.75f, 1.6f, 170f), new Vector3(0.2f, 2.6f, 0.2f));
        mk("门框_右柱", new Vector3(-40.65f, 1.6f, 170f), new Vector3(0.2f, 2.6f, 0.2f));
        mk("门框_楣", new Vector3(-41.7f, 2.75f, 170f), new Vector3(2.3f, 0.3f, 0.2f));

        // 端门（铰链在左柱，门板为子物体；复明后 PowerLockedDoor 转开）
        var hingeT = car.Find("端门_铰链");
        if (hingeT == null)
        {
            var hinge = new GameObject("端门_铰链");
            hinge.transform.SetParent(car, false);
            hinge.transform.position = new Vector3(-42.65f, 1.55f, 170f);
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "端门_门板";
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(0.95f, 0f, 0f);
            leaf.transform.localScale = new Vector3(1.9f, 2.5f, 0.1f);
            leaf.GetComponent<Renderer>().sharedMaterial = mat;
            var door = hinge.AddComponent<PowerLockedDoor>();
            SetPrivate(door, "doorLeaf", leaf.transform);
            SetPrivate(door, "blockingCollider", leaf.GetComponent<Collider>());
            SetText(hinge.AddComponent<InteractionLabel>(), "text", "车厢门");
            sb.AppendLine("door ok");
        }

        // 车厢内灯（复明后亮）
        if (car.Find("车厢灯") == null)
        {
            var lg = new GameObject("车厢灯");
            lg.transform.SetParent(car, false);
            lg.transform.position = new Vector3(-41.5f, 2.85f, 175f);
            var pl = lg.AddComponent<Light>();
            pl.type = LightType.Point; pl.range = 6f; pl.intensity = 1.1f;
            pl.color = new Color(1f, 0.9f, 0.7f);
            var powered = lg.AddComponent<PoweredLight>();
            SetPrivate(powered, "lamp", pl);
            sb.AppendLine("light ok");
        }

        // 战利品：软盘#2 + 木箱
        var items = EnsureChild(GameRoot(), "Items");
        if (items.Find("Specimen_软盘2") == null)
        {
            var f2 = BuildFloppy("Specimen_软盘2", new Vector3(-41.9f, 0.95f, 178.5f), 40f);
            f2.transform.SetParent(items, true);
            f2.AddComponent<SpecimenPickup>();
            SetText(f2.AddComponent<InteractionLabel>(), "text", "回收异常物·软盘");
            sb.AppendLine("floppy2 ok(procedural)");
        }
        mk("战利品_木箱1", new Vector3(-42.1f, 0.85f, 178.4f), new Vector3(0.75f, 0.75f, 0.75f));
        mk("战利品_木箱2", new Vector3(-40.9f, 0.7f, 176.8f), new Vector3(0.55f, 0.55f, 0.55f));

        // 环境音频：对面隧道低鸣 / 隧道A 风啸 / 大厅滴水（Flooded_Grounds 无 3D_Water，用 3D_Taps）
        sb.AppendLine(PlaceAudio("3D_DeepRattle", new Vector3(-55f, 2.5f, 90f)));
        sb.AppendLine(PlaceAudio("3D_WindHowl", new Vector3(-41f, 3f, 50f)));
        sb.AppendLine(PlaceAudio("3D_Taps", new Vector3(-12f, 16f, 180f)));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    private static string PlaceAudio(string prefabName, Vector3 pos)
    {
        var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
        if (guids.Length == 0) return prefabName + " NOT FOUND";
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) return prefabName + " LOAD FAIL";
        var audio = EnsureChild(GameRoot(), "音频");
        if (audio.Find(prefabName) != null) return prefabName + " exists";
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        go.name = prefabName;
        go.transform.SetParent(audio, false);
        go.transform.position = pos;
        return prefabName + " ok @" + path;
    }

    /// <summary>保存当前 Metro 场景。</summary>
    public static string SaveAll()
    {
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene);
        return "saved: " + scene.name;
    }

    private static Material sealMat;

    /// <summary>深色密封材质（一次性创建到 Assets/Material/）。</summary>
    private static Material GetSealMaterial()
    {
        if (sealMat != null) return sealMat;
        const string path = "Assets/Material/MetroSeal.mat";
        sealMat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sealMat == null)
        {
            sealMat = new Material(Shader.Find("Standard"));
            sealMat.color = new Color(0.05f, 0.06f, 0.07f);
            AssetDatabase.CreateAsset(sealMat, path);
        }
        return sealMat;
    }

    /// <summary>阶段0b：给整个 Metro 加暗色封壳（顶盖+四周围墙，带碰撞），封掉所有露背景的缺口。</summary>
    public static string Stage0b_Seal()
    {
        var root = GameRoot();
        Transform seal = root.Find("Sealing");
        if (seal == null) seal = new GameObject("Sealing").transform;
        seal.SetParent(root, false);

        var mat = GetSealMaterial();
        // (名, 中心, 尺寸)：顶盖 + 四周围墙
        Vector3[] c = {
            new Vector3(-33f, 24f, 119f), new Vector3(-68.5f, 13f, 119f), new Vector3(2f, 13f, 119f),
            new Vector3(-33f, 13f, 19.5f), new Vector3(-33f, 13f, 218.5f)
        };
        Vector3[] s = {
            new Vector3(72f, 2f, 200f), new Vector3(1f, 26f, 200f), new Vector3(1f, 26f, 200f),
            new Vector3(72f, 26f, 1f), new Vector3(72f, 26f, 1f)
        };
        string[] names = { "Seal_Ceiling", "Seal_Wall_W", "Seal_Wall_E", "Seal_Wall_S", "Seal_Wall_N" };

        for (int i = 0; i < names.Length; i++)
        {
            if (seal.Find(names[i]) != null) continue;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = names[i];
            go.transform.SetParent(seal, false);
            go.transform.position = c[i];
            go.transform.localScale = s[i];
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return "sealed: 5 panels, mat=" + mat.name;
    }

    /// <summary>玩法点验证截图（门区/死端谜题/车厢/对面的灯）。</summary>
    public static string GameplayShots()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Screenshot(-47f, 2.2f, 126.5f, -47.6f, 1.3f, 128.3f, "gp_door_floppy.png", 960, 540, 70f));
        sb.AppendLine(Screenshot(-43.6f, 1.8f, 133.5f, -43.6f, 1f, 130.2f, "gp_charger.png", 960, 540, 70f));
        sb.AppendLine(Screenshot(-45.8f, 3.8f, 35.5f, -46.4f, 2.6f, 30.9f, "gp_deadend_cabinet.png", 960, 540, 70f));
        sb.AppendLine(Screenshot(-41.5f, 1.8f, 165.5f, -41.5f, 1.6f, 172f, "gp_carriage_door.png", 960, 540, 70f));
        sb.AppendLine(Screenshot(-50f, 3.5f, 45f, -55f, 5.4f, 34f, "gp_farlight.png", 960, 540, 55f));
        sb.AppendLine(Screenshot(-11f, 16.5f, 158f, -8f, 15.5f, 160.5f, "gp_hall_meter.png", 960, 540, 70f));
        return sb.ToString();
    }

    /// <summary>视觉修正批次1：按截图评审调整各玩法点。</summary>
    public static string FixPass1()
    {
        var sb = new StringBuilder();
        var items = EnsureChild(GameRoot(), "Items");

        // 软盘：抬到箱面中央 + 本体自发光提亮（材质资产两块软盘共用）
        var f1 = items.Find("Specimen_软盘1");
        if (f1 != null) { f1.position = new Vector3(-48f, 1.22f, 128f); sb.AppendLine("floppy1 moved"); }
        var bodyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/MetroFloppy_Body.mat");
        if (bodyMat != null) bodyMat.SetColor("_EmissionColor", new Color(0.12f, 0.35f, 0.55f));

        // 充电桩：挪到隧道A东侧墙根 + 深色底座
        var cs = items.Find("充电桩_Metro");
        if (cs != null)
        {
            cs.position = new Vector3(-39.2f, 0.32f, 131f);
            cs.rotation = Quaternion.Euler(0f, -90f, 0f);
            if (items.parent.Find("充电桩_底座") == null)
            {
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseGo.name = "充电桩_底座";
                baseGo.transform.SetParent(GameRoot(), false);
                baseGo.transform.position = new Vector3(-39.2f, 0.16f, 131f);
                baseGo.transform.localScale = new Vector3(1f, 0.32f, 1f);
                baseGo.GetComponent<Renderer>().sharedMaterial = GetSealMaterial();
            }
            sb.AppendLine("charger moved");
        }

        // 配电室：工作灯（常亮，不入灯组）+ 平台支撑腿 + 线材自发光提亮
        var puzzle = EnsureChild(GameRoot(), "配电室");
        if (puzzle.Find("配电室工作灯") == null)
        {
            var wl = new GameObject("配电室工作灯").AddComponent<Light>();
            wl.transform.SetParent(puzzle, false);
            wl.transform.position = new Vector3(-46.2f, 3.7f, 31.8f);
            wl.type = LightType.Point; wl.range = 6.5f; wl.intensity = 1.2f;
            wl.color = new Color(1f, 0.8f, 0.55f);
        }
        if (puzzle.Find("平台支腿1") == null)
        {
            for (int i = 0; i < 2; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "平台支腿" + (i + 1);
                leg.transform.SetParent(puzzle, false);
                leg.transform.position = new Vector3(-47.2f + i * 2.6f, 0.95f, 32.9f);
                leg.transform.localScale = new Vector3(0.22f, 1.9f, 0.22f);
                leg.GetComponent<Renderer>().sharedMaterial = GetSealMaterial();
            }
        }
        for (int i = 0; i < 3; i++)
        {
            var wm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/MetroWire_" + i + ".mat");
            Color[] wc = { new Color(0.9f, 0.15f, 0.1f), new Color(0.95f, 0.8f, 0.1f), new Color(0.2f, 0.45f, 1f) };
            if (wm != null) wm.SetColor("_EmissionColor", wc[i] * 0.85f);
        }
        sb.AppendLine("cabinet lit");

        // 对面的灯：加大 + 提亮 + 灯罩
        var far = EnsureChild(GameRoot(), "异常").Find("对面的灯");
        if (far != null)
        {
            far.Find("灯珠").localScale = Vector3.one * 0.38f;
            var lamp = far.GetComponent<Light>();
            if (lamp != null) { lamp.intensity = 2.2f; lamp.range = 12f; }
            if (far.Find("灯罩") == null)
            {
                var hood = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hood.name = "灯罩";
                hood.transform.SetParent(far, false);
                hood.transform.localPosition = new Vector3(0f, 0.32f, 0f);
                hood.transform.localScale = new Vector3(0.55f, 0.2f, 0.55f);
                hood.GetComponent<Renderer>().sharedMaterial = GetSealMaterial();
                Object.DestroyImmediate(hood.GetComponent<Collider>());
            }
            var fm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/MetroFarLight.mat");
            if (fm != null) fm.SetColor("_EmissionColor", new Color(1.1f, 1.6f, 1.6f));
            sb.AppendLine("far light boosted");
        }

        // 万用表：放大 + 微型指示灯（暗厅里可发现）
        var meter = items.Find("道具_万用表");
        if (meter != null)
        {
            meter.localScale = meter.localScale * 1.6f;
            meter.position = new Vector3(-8f, 15.65f, 160.5f);
            if (meter.Find("指示灯") == null)
            {
                var bl = new GameObject("指示灯").AddComponent<Light>();
                bl.transform.SetParent(meter, false);
                bl.type = LightType.Point; bl.range = 2.6f; bl.intensity = 0.55f;
                bl.color = new Color(0.4f, 1f, 1f);
            }
            sb.AppendLine("meter boosted");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    /// <summary>视觉修正批次2：亮度与尺寸再校（以游戏内可辨认为准）。</summary>
    public static string FixPass2()
    {
        var sb = new StringBuilder();
        var puzzle = EnsureChild(GameRoot(), "配电室");
        var wl = puzzle.Find("配电室工作灯");
        if (wl != null)
        {
            wl.position = new Vector3(-46.2f, 4.2f, 31.6f);
            var l = wl.GetComponent<Light>();
            if (l != null) { l.intensity = 2.2f; l.range = 9f; }
        }
        Color[] wc = { new Color(0.9f, 0.15f, 0.1f), new Color(0.95f, 0.8f, 0.1f), new Color(0.2f, 0.45f, 1f) };
        for (int i = 0; i < 3; i++)
        {
            var wm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/MetroWire_" + i + ".mat");
            if (wm != null) wm.SetColor("_EmissionColor", wc[i] * 1.6f);
        }

        var far = EnsureChild(GameRoot(), "异常").Find("对面的灯");
        if (far != null)
        {
            far.Find("灯珠").localScale = Vector3.one * 0.7f;
            var lamp = far.GetComponent<Light>();
            if (lamp != null) { lamp.intensity = 3f; lamp.range = 14f; }
            var hood = far.Find("灯罩");
            if (hood != null)
            {
                hood.localPosition = new Vector3(0f, 0.55f, 0f);
                hood.localScale = new Vector3(0.95f, 0.25f, 0.95f);
            }
            var fm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/MetroFarLight.mat");
            if (fm != null) fm.SetColor("_EmissionColor", new Color(1.4f, 2f, 2f));
            sb.AppendLine("farlight x2");
        }

        var items = EnsureChild(GameRoot(), "Items");
        var meter = items.Find("道具_万用表");
        if (meter != null)
        {
            var bl = meter.Find("指示灯");
            if (bl != null)
            {
                var bll = bl.GetComponent<Light>();
                if (bll != null) { bll.intensity = 1.2f; bll.range = 3.5f; }
            }
            if (meter.Find("屏幕") == null)
            {
                var scr = GameObject.CreatePrimitive(PrimitiveType.Cube);
                scr.name = "屏幕";
                scr.transform.SetParent(meter, false);
                scr.transform.localPosition = new Vector3(0f, 0.09f, 0.02f);
                scr.transform.localScale = new Vector3(0.12f, 0.07f, 0.015f);
                var smat = new Material(Shader.Find("Standard"));
                smat.color = new Color(0.1f, 0.3f, 0.3f);
                smat.EnableKeyword("_EMISSION");
                smat.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 0.9f));
                AssetDatabase.CreateAsset(smat, "Assets/Material/MetroMeterScreen.mat");
                scr.GetComponent<Renderer>().sharedMaterial = smat;
                Object.DestroyImmediate(scr.GetComponent<Collider>());
            }
            sb.AppendLine("meter screen");
        }

        var car = EnsureChild(GameRoot(), "尾节车厢");
        if (car.Find("车厢门头灯") == null)
        {
            var hl = new GameObject("车厢门头灯").AddComponent<Light>();
            hl.transform.SetParent(car, false);
            hl.transform.position = new Vector3(-41.5f, 3.3f, 170.4f);
            hl.type = LightType.Point; hl.range = 5.5f; hl.intensity = 0.7f;
            hl.color = new Color(0.5f, 0.7f, 1f);
            sb.AppendLine("carriage headlight");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    /// <summary>镜像探针：从地铁侧离屏渲染门洞（取景屏含红/蓝标记），返回两色像素的平均 x（0~1，&lt;0.5=左）。</summary>
    public static string MirrorProbe()
    {
        int w = 960, h = 540;
        var go = new GameObject("MirrorProbeCam");
        var cam = go.AddComponent<Camera>();
        go.transform.position = new Vector3(-45.2f, 1.55f, 136.8f);
        go.transform.LookAt(new Vector3(-47.89f, 1.6f, 132.7f));
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 250f;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        Object.DestroyImmediate(go);

        var px = tex.GetPixels();
        double redSum = 0; int redN = 0; double blueSum = 0; int blueN = 0; int bright = 0;
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            float lum = c.r + c.g + c.b;
            if (lum > 0.25f) bright++;
            if (c.r > 0.55f && c.g < 0.3f && c.b < 0.3f) { redSum += i % w; redN++; }
            else if (c.b > 0.55f && c.r < 0.3f && c.g < 0.45f) { blueSum += i % w; blueN++; }
        }
        string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "PortalDumps");
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "mirror_probe.png"), ImageConversion.EncodeToPNG(tex));
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        return "bright=" + (bright * 100 / px.Length) + "%"
            + " red: n=" + redN + " x=" + (redN > 0 ? (redSum / redN / w).ToString("F2") : "none")
            + " blue: n=" + blueN + " x=" + (blueN > 0 ? (blueSum / blueN / w).ToString("F2") : "none");
    }

    // ================= Play 模式测试驱动（每方法可独立调用） =================

    private static Transform PlayerRoot()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType("PlayerRef");
                if (t != null) return (Transform)t.GetProperty("Root").GetValue(null);
            }
            catch { }
        }
        return null;
    }

    private static object CallPlayer(string method, object[] args)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType("PlayerRef");
                if (t != null) return t.GetMethod(method).Invoke(null, args);
            }
            catch { }
        }
        return null;
    }

    /// <summary>PT0：后台也跑 + 重建红蓝标记 + 暂停渐熄 + 电量回满。</summary>
    public static string PT_Setup()
    {
        Application.runInBackground = true;
        foreach (var mk in new[] { "MARK_RED", "MARK_BLUE" })
        {
            var old = GameObject.Find(mk);
            if (old != null) Object.DestroyImmediate(old);
        }
        var red = GameObject.CreatePrimitive(PrimitiveType.Cube);
        red.name = "MARK_RED"; red.transform.position = new Vector3(4.8f, 1.2f, 6.7f); red.transform.localScale = Vector3.one * 0.4f;
        var rm = new Material(Shader.Find("Standard")); rm.color = Color.red;
        rm.EnableKeyword("_EMISSION"); rm.SetColor("_EmissionColor", new Color(1.4f, 0f, 0f));
        red.GetComponent<Renderer>().material = rm;
        var blue = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blue.name = "MARK_BLUE"; blue.transform.position = new Vector3(2.8f, 1.2f, 6.7f); blue.transform.localScale = Vector3.one * 0.4f;
        var bm = new Material(Shader.Find("Standard")); bm.color = Color.blue;
        bm.EnableKeyword("_EMISSION"); bm.SetColor("_EmissionColor", new Color(0f, 0f, 1.8f));
        blue.GetComponent<Renderer>().material = bm;

        foreach (var mb in Object.FindObjectsOfType<MetroBlackout>(true)) mb.enabled = false;
        var bs = Object.FindObjectsOfType<BatterySystem>(true);
        if (bs.Length > 0)
        {
            bs[0].SetAmbientDrain(0f);
            typeof(BatterySystem).GetProperty("Battery").SetValue(bs[0], 100f, null);
        }
        return "setup ok (runInBackground=true, markers fresh, blackout paused, battery=100)";
    }

    /// <summary>PT1：实验室开门 → 分两步推玩家过门（先站门前，调用方等待后再推）。</summary>
    public static string PT_StageAtLabDoor()
    {
        var door = GameObject.Find("anywhere-door-Metro-Lab").GetComponentInChildren<AnywhereDoor>(true);
        door.SetOpen(true, false);
        CallPlayer("Teleport", new object[] { new Vector3(3.78f, 1f, 4.1f), Quaternion.LookRotation(new Vector3(0, 0, 1)) });
        return "staged at lab door (wait 1s, then PT_PushThrough)";
    }

    public static string PT_PushThrough()
    {
        CallPlayer("Teleport", new object[] { new Vector3(3.78f, 1f, 6.0f), Quaternion.LookRotation(new Vector3(0, 0, 1)) });
        return "pushed through lab door plane";
    }

    /// <summary>PT2：镜像探针（站地铁侧看门洞，判红蓝左右）。</summary>
    public static string PT_Mirror()
    {
        var door = GameObject.Find("anywhere-door-Metro").GetComponentInChildren<AnywhereDoor>(true);
        door.SetOpen(true, false);
        return MirrorProbe();
    }

    /// <summary>PT3：加速渐熄（interval=2s）并启用，玩家站站台。</summary>
    public static string PT_FastBlackout()
    {
        var root = PlayerRoot();
        if (root != null) CallPlayer("Teleport", new object[] { new Vector3(-41f, 1f, 150f), Quaternion.identity });
        foreach (var mb in Object.FindObjectsOfType<MetroBlackout>(true))
        {
            var so = new SerializedObject(mb);
            so.FindProperty("segmentInterval").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            mb.enabled = true;
        }
        return "fast blackout on (2s/组)，等 14s 后调 PT_Status";
    }

    /// <summary>PT4：状态汇总（玩家位/电量/各组开关/耗电速率/复明标志）。</summary>
    public static string PT_Status()
    {
        var sb = new StringBuilder();
        var root = PlayerRoot();
        sb.AppendLine("player=" + (root != null ? root.position.ToString("F1") : "null"));
        var bs = Object.FindObjectsOfType<BatterySystem>(true);
        if (bs.Length > 0)
        {
            sb.AppendLine("battery=" + bs[0].Battery.ToString("F1") + " dead=" + bs[0].IsDead);
            var drain = typeof(BatterySystem).GetField("ambientDrain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sb.AppendLine("ambientDrain=" + drain.GetValue(bs[0]));
        }
        foreach (var mb in Object.FindObjectsOfType<MetroBlackout>(true))
        {
            var killed = typeof(MetroBlackout).GetField("killedCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sb.AppendLine("blackout enabled=" + mb.enabled + " killed=" + killed.GetValue(mb) + " restoredStatic=" + MetroBlackout.PowerRestored);
        }
        foreach (var lg in Object.FindObjectsOfType<LightGroup>(true))
            sb.AppendLine(lg.name + " on=" + lg.IsOn);
        return sb.ToString();
    }

    /// <summary>PT5：完整解谜（拿表→测带电线→合闸），返回每步结果。</summary>
    public static string PT_SolvePuzzle()
    {
        var sb = new StringBuilder();
        PlayerGear.AcquireMultimeter();
        sb.AppendLine("multimeter=" + PlayerGear.HasMultimeter);

        var cabinet = Object.FindObjectsOfType<PowerCabinet>(true);
        if (cabinet.Length == 0) return "cabinet NOT FOUND";
        var live = typeof(PowerCabinet).GetField("liveIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int liveIdx = (int)live.GetValue(cabinet[0]);
        sb.AppendLine("liveIndex=" + liveIdx);

        var wires = typeof(PowerCabinet).GetField("wires", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var wireArr = (PowerWire[])wires.GetValue(cabinet[0]);
        var root = PlayerRoot();
        // 先测一根死线（看提示分支），再测带电线
        int deadIdx = (liveIdx + 1) % wireArr.Length;
        wireArr[deadIdx].Interact(root != null ? root.gameObject : null);
        sb.AppendLine("probed dead wire " + deadIdx);
        wireArr[liveIdx].Interact(root != null ? root.gameObject : null);
        sb.AppendLine("probed live wire " + liveIdx);

        var breaker = Object.FindObjectsOfType<BreakerSwitch>(true);
        if (breaker.Length > 0)
        {
            breaker[0].Interact(root != null ? root.gameObject : null);
            sb.AppendLine("breaker closed → restored=" + MetroBlackout.PowerRestored);
        }

        var carDoor = Object.FindObjectsOfType<PowerLockedDoor>(true);
        sb.AppendLine("carriageDoor opened=" + (carDoor.Length > 0 && !carDoor[0].enabled));
        var rs = Object.FindObjectsOfType<RequiresPower>(true);
        sb.AppendLine("chargeGate active=" + (rs.Length > 0 && rs[0].enabled));
        var pl = Object.FindObjectsOfType<PoweredLight>(true);
        if (pl.Length > 0)
        {
            var lamp = pl[0].GetComponentInChildren<Light>(true);
            sb.AppendLine("carriageLight on=" + (lamp != null && lamp.enabled));
        }
        return sb.ToString();
    }

    /// <summary>PT6：拾取两块软盘 + 回实验室收容。</summary>
    public static string PT_Pickups()
    {
        var sb = new StringBuilder();
        var root = PlayerRoot();
        var go = root != null ? root.gameObject : null;
        foreach (var sp in Object.FindObjectsOfType<SpecimenPickup>(true))
            if (sp.gameObject.activeSelf) { sp.Interact(go); sb.AppendLine("picked: " + sp.name); }
        var inv = Object.FindObjectsOfType<Inventory>(true);
        if (inv.Length > 0) sb.AppendLine("carrying=" + inv[0].SpecimenCount);
        if (root != null) CallPlayer("Teleport", new object[] { new Vector3(5.0f, 1f, 8.0f), Quaternion.identity });
        foreach (var dep in Object.FindObjectsOfType<SpecimenDeposit>(true))
        {
            dep.Interact(go);
            sb.AppendLine("deposited, stored=" + inv[0].StoredSpecimenCount + " carrying=" + inv[0].SpecimenCount);
        }
        return sb.ToString();
    }

    /// <summary>PT7：对面的灯 追近/远离 判定。</summary>
    public static string PT_FarLight()
    {
        var sb = new StringBuilder();
        var anom = Object.FindObjectsOfType<UnreachableLightAnomaly>(true);
        if (anom.Length == 0) return "anomaly NOT FOUND";
        var lamp = anom[0].GetComponentInChildren<Light>(true);
        CallPlayer("Teleport", new object[] { new Vector3(-52f, 1f, 42f), Quaternion.identity });
        return "player staged 8m from far light; wait 1s then调 PT_FarLightCheck";
    }

    public static string PT_FarLightCheck()
    {
        var anom = Object.FindObjectsOfType<UnreachableLightAnomaly>(true);
        if (anom.Length == 0) return "anomaly NOT FOUND";
        var lamp = anom[0].GetComponentInChildren<Light>(true);
        bool near = lamp != null && lamp.enabled == false; // 近=灭
        CallPlayer("Teleport", new object[] { new Vector3(-44f, 1f, 120f), Quaternion.identity });
        return "near(应灭)=" + near + "；已传远，wait 1s 再调 PT_FarLightCheck2";
    }

    public static string PT_FarLightCheck2()
    {
        var anom = Object.FindObjectsOfType<UnreachableLightAnomaly>(true);
        var lamp = anom[0].GetComponentInChildren<Light>(true);
        return "far(应亮)=" + (lamp != null && lamp.enabled);
    }

    /// <summary>玩家位置 + 双窗就绪 快查。</summary>
    public static string PT_Pos()
    {
        var root = PlayerRoot();
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var rf = typeof(PortalWindow).GetField("ready", bf);
        var a = GameObject.Find("anywhere-door-Metro-Lab");
        var b = GameObject.Find("anywhere-door-Metro");
        return "player=" + (root != null ? root.position.ToString("F2") : "null")
            + " labReady=" + (a != null && (bool)rf.GetValue(a.GetComponentInChildren<PortalWindow>(true)))
            + " metroReady=" + (b != null && (bool)rf.GetValue(b.GetComponentInChildren<PortalWindow>(true)));
    }

    /// <summary>场景收尾：灯组 GO 摆到灯泡质心（供黑暗区段判定用）+ 配电箱重链渐熄中枢。</summary>
    public static string Finalize2()
    {
        var sb = new StringBuilder();
        var lightsField = typeof(LightGroup).GetField("lights",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var lg in Object.FindObjectsOfType<LightGroup>(true))
        {
            var lights = (Light[])lightsField.GetValue(lg);
            if (lights == null || lights.Length == 0) continue;
            Vector3 c = Vector3.zero; int n = 0;
            foreach (var l in lights) { if (l == null) continue; c += l.transform.position; n++; }
            if (n > 0)
            {
                c /= n;
                lg.transform.position = c;
                sb.AppendLine(lg.name + " -> " + c.ToString("F1"));
            }
        }
        var cab = Object.FindObjectsOfType<PowerCabinet>(true);
        var mb = Object.FindObjectsOfType<MetroBlackout>(true);
        if (cab.Length > 0 && mb.Length > 0)
        {
            SetPrivate(cab[0], "blackout", mb[0]);
            sb.AppendLine("cabinet->blackout relinked");
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    // ================= 阶段 4：接单任务板 + 新异常（2026-10-17 第二轮） =================

    /// <summary>阶段4a：修渐熄判定盒——Metro 世界抬高 +93.9 后 y 区间失效。</summary>
    public static string Stage4_Fixes()
    {
        var mb = Object.FindObjectsOfType<MetroBlackout>(true);
        if (mb.Length == 0) return "no blackout";
        var so = new SerializedObject(mb[0]);
        so.FindProperty("zoneCenter").vector3Value = new Vector3(-34f, 104.9f, 125.75f);
        so.FindProperty("zoneSize").vector3Value = new Vector3(72f, 56f, 192.5f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "zone fixed: y∈[76.9,132.9]（覆盖抬高后的车站与上层）";
    }

    /// <summary>阶段4b：实验室 WorldManager 挂 OrderBoard，旧 SpecimenHud 下岗；Metro 侧也挂一份（先到先得渲染）。</summary>
    public static string Stage4_Orders()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SpecimenLab.unity", OpenSceneMode.Additive);
        var wm = GameObject.Find("WorldManager");
        if (wm == null)
        {
            EditorSceneManager.CloseScene(scene, true);
            return "no WorldManager";
        }
        var hud = wm.GetComponent<SpecimenHud>();
        if (hud != null) { hud.enabled = false; sb.AppendLine("SpecimenHud disabled（职能并入订单板）"); }
        if (wm.GetComponent<OrderBoard>() == null)
        {
            wm.AddComponent<OrderBoard>();
            sb.AppendLine("OrderBoard added (Lab)");
        }
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);

        var mg = GameObject.Find("MetroGameplay");
        if (mg != null && mg.GetComponent<OrderBoard>() == null)
        {
            mg.AddComponent<OrderBoard>();
            sb.AppendLine("OrderBoard added (Metro)");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        return sb.ToString();
    }

    /// <summary>阶段4c：电磁风暴 + 幽灵进站 挂到 MetroGameplay/异常 并接线。</summary>
    public static string Stage4_Anomalies()
    {
        var sb = new StringBuilder();
        var anom = EnsureChild(GameRoot(), "异常");
        var mb = Object.FindObjectsOfType<MetroBlackout>(true);
        var groups = Object.FindObjectsOfType<LightGroup>(true);
        if (mb.Length == 0 || groups.Length == 0) return "blackout/groups missing";
        Object[] groupObjs = groups;

        var stormGo = anom.Find("电磁风暴") != null ? anom.Find("电磁风暴").gameObject : new GameObject("电磁风暴");
        stormGo.transform.SetParent(anom, false);
        var storm = stormGo.GetComponent<EmfStormAnomaly>();
        if (storm == null) storm = stormGo.AddComponent<EmfStormAnomaly>();
        SetPrivate(storm, "blackout", mb[0]);
        SetPrivateArray(storm, "groups", groupObjs);
        sb.AppendLine("storm wired (" + groups.Length + " 组)");

        var ghostGo = anom.Find("幽灵进站") != null ? anom.Find("幽灵进站").gameObject : new GameObject("幽灵进站");
        ghostGo.transform.SetParent(anom, false);
        var ghost = ghostGo.GetComponent<GhostArrivalEvent>();
        if (ghost == null) ghost = ghostGo.AddComponent<GhostArrivalEvent>();
        SetPrivate(ghost, "blackout", mb[0]);
        SetPrivateArray(ghost, "groups", groupObjs);

        var roarT = ghostGo.transform.Find("风压声");
        AudioSource roar = roarT != null ? roarT.GetComponent<AudioSource>() : null;
        if (roar == null)
        {
            var roarGo = new GameObject("风压声");
            roarGo.transform.SetParent(ghostGo.transform, false);
            roarGo.transform.localPosition = new Vector3(-41f, 1.6f, 55f); // 隧道A 中段深处（世界≈95.5 高）
            roar = roarGo.AddComponent<AudioSource>();
            roar.loop = true;
            roar.playOnAwake = false;
            roar.spatialBlend = 1f;
            roar.minDistance = 8f;
            roar.maxDistance = 90f;
            roar.rolloffMode = AudioRolloffMode.Linear;
            roar.volume = 0f;
            AudioClip clip = null;
            foreach (var g in AssetDatabase.FindAssets("Wind t:AudioClip"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("Flooded_Grounds")) { clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p); break; }
            }
            if (clip != null) roar.clip = clip;
            sb.AppendLine("roar source: " + (clip != null ? clip.name : "无 Wind 声源（待生成）"));
        }
        SetPrivate(ghost, "roar", roar);
        sb.AppendLine("ghost wired");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>给电磁风暴接上生成的电流干扰声（音频生成成功后调用）。</summary>
    public static string Stage4_StormSfx(string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) return "clip NOT FOUND: " + clipPath;
        var anom = EnsureChild(GameRoot(), "异常");
        var stormGo = anom.Find("电磁风暴");
        if (stormGo == null) return "电磁风暴 GO missing";
        var sfxT = stormGo.transform.Find("干扰声");
        AudioSource src = sfxT != null ? sfxT.GetComponent<AudioSource>() : null;
        if (src == null)
        {
            var go = new GameObject("干扰声");
            go.transform.SetParent(stormGo, false);
            src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 风暴是全局现象，2D 直接呈现"仪器被干扰"
            src.volume = 0.55f;
        }
        src.clip = clip;
        SetPrivate(stormGo.GetComponent<EmfStormAnomaly>(), "sfx", src);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "storm sfx wired: " + clipPath;
    }

    /// <summary>程序合成两段音效 WAV（fal 无 key 的回退方案）：电流干扰 + 风压轰鸣。</summary>
    public static string Stage4_MakeSfx()
    {
        const int sr = 44100;
        string dir = "Assets/Audio/Metro";
        if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Audio", "Audio" == "Audio" ? "Metro" : "Metro");

        // ① 电流干扰：50Hz 蜂鸣 + 白噪 + 随机爆点（10s）
        int n1 = sr * 10;
        var d1 = new float[n1];
        var rng = new System.Random(42);
        float spike = 0f; int spikeLeft = 0;
        for (int i = 0; i < n1; i++)
        {
            float t = i / (float)sr;
            float buzz = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.16f;
            buzz += Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.06f;
            float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.16f;
            if (spikeLeft <= 0 && rng.NextDouble() < 0.0015) { spikeLeft = (int)(sr * rng.Next(3, 15) / 1000f * 10) + 40; spike = 1f; }
            if (spikeLeft > 0) { spike *= 0.86f; spikeLeft--; }
            d1[i] = Mathf.Clamp(buzz + noise + spike * ((float)rng.NextDouble() * 2f - 1f) * 0.7f, -1f, 1f);
        }
        WriteWav(dir + "/emf_storm.wav", d1, sr);

        // ② 风压轰鸣：布朗噪声低通 + 慢起伏（20s，可循环）
        int n2 = sr * 20;
        var d2 = new float[n2];
        float brown = 0f, lp = 0f;
        for (int i = 0; i < n2; i++)
        {
            float t = i / (float)sr;
            brown += ((float)rng.NextDouble() * 2f - 1f) * 0.02f;
            brown = Mathf.Clamp(brown, -0.6f, 0.6f);
            lp += (brown - lp) * 0.045f; // 单极低通 → 只剩低频隆隆
            float pulse = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.37f * t);
            d2[i] = Mathf.Clamp(lp * 1.9f * pulse, -1f, 1f);
        }
        WriteWav(dir + "/arrival_pressure.wav", d2, sr);

        AssetDatabase.Refresh();
        return "sfx written: emf_storm.wav + arrival_pressure.wav";
    }

    /// <summary>给幽灵进站换上合成的风压轰鸣（比现成 Wind 更沉）。</summary>
    public static string Stage4_RoarSfx()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Metro/arrival_pressure.wav");
        if (clip == null) return "arrival_pressure.wav NOT FOUND";
        var roarT = EnsureChild(GameRoot(), "异常").Find("幽灵进站");
        if (roarT == null) return "幽灵进站 GO missing";
        var src = roarT.Find("风压声");
        if (src == null) return "风压声 source missing";
        src.GetComponent<AudioSource>().clip = clip;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "roar = arrival_pressure";
    }

    private static void WriteAscii(System.IO.BinaryWriter w, string s)
    {
        foreach (var c in s) w.Write((byte)c);
    }

    // ================= 阶段 6：结算反馈层 + 档案 + 清理（2026-10-20 第五轮） =================

    /// <summary>阶段6a：账单/播报挂载（Lab WorldManager + Metro MetroGameplay 双挂，先到先得渲染）。</summary>
    public static string Stage6_Hud()
    {
        var sb = new StringBuilder();
        var lab = EditorSceneManager.OpenScene("Assets/Scenes/SpecimenLab.unity", OpenSceneMode.Additive);
        var wm = GameObject.Find("WorldManager");
        if (wm != null)
        {
            if (wm.GetComponent<BillPanel>() == null) { wm.AddComponent<BillPanel>(); sb.AppendLine("BillPanel@Lab"); }
            if (wm.GetComponent<DutyBriefing>() == null) { wm.AddComponent<DutyBriefing>(); sb.AppendLine("Briefing@Lab"); }
            EditorSceneManager.SaveScene(lab);
        }
        EditorSceneManager.CloseScene(lab, true);

        var mg = GameObject.Find("MetroGameplay");
        if (mg != null)
        {
            if (mg.GetComponent<BillPanel>() == null) mg.AddComponent<BillPanel>();
            if (mg.GetComponent<DutyBriefing>() == null) mg.AddComponent<DutyBriefing>();
            sb.AppendLine("BillPanel/Briefing@Metro");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        return sb.ToString();
    }

    /// <summary>阶段6b：Metro 两块软盘配档案 + 站厅放第三异常物「复读磁带」。</summary>
    public static string Stage6_MetroArchive()
    {
        var sb = new StringBuilder();
        var items = EnsureChild(GameRoot(), "Items");

        // 软盘档案（差异化图鉴条目）
        var f1 = items.Find("Specimen_软盘1");
        if (f1 != null)
        {
            var so = new SerializedObject(f1.GetComponent<SpecimenPickup>());
            so.FindProperty("specimenName").stringValue = "值班员的备份";
            so.FindProperty("flavor").stringValue = "标签写着\"别再弄丢了\"，可谁会弄丢一块绑在钥匙上的软盘";
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine("软盘1 档案 ok");
        }
        var f2 = items.Find("Specimen_软盘2");
        if (f2 != null)
        {
            var so = new SerializedObject(f2.GetComponent<SpecimenPickup>());
            so.FindProperty("specimenName").stringValue = "末班车的行车记录";
            so.FindProperty("flavor").stringValue = "最后一班的司机日志——到站时间是明天";
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine("软盘2 档案 ok");
        }

        // 第三异常物：复读磁带（Poly Haven 磁带机，站厅轨道边）
        if (items.Find("Specimen_复读磁带") == null)
        {
            var tape = InstantiateModel("Assets/Resources/Sourced/Models/cassette_player.fbx", "Specimen_复读磁带",
                new Vector3(-44.2f, 95.35f, 141.5f), 0.34f, 35f);
            if (tape != null)
            {
                tape.transform.SetParent(items, true);
                var sp = tape.AddComponent<SpecimenPickup>();
                var so = new SerializedObject(sp);
                so.FindProperty("specimenName").stringValue = "复读磁带";
                so.FindProperty("flavor").stringValue = "同一句「……你终于来了」，每播一次语气都不耐烦一点";
                so.ApplyModifiedPropertiesWithoutUndo();
                SetText(tape.AddComponent<InteractionLabel>(), "text", "回收异常物·磁带");
                sb.AppendLine("复读磁带 ok");
            }
            else sb.AppendLine("磁带模型 MISSING");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>阶段6c：Flooded_Grounds 清理 Demo 残留。⚠️ 只删脚本所在对象本身与具名残留，
    /// 绝不按"子树含 Demo 脚本"删根——环境主体全挂在 "scene" 根容器下（首版误删已从 git 恢复）。</summary>
    public static string Stage6_FloodedClean()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Flooded_Grounds.unity", OpenSceneMode.Single);
        var doomed = new System.Collections.Generic.List<GameObject>();

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "player (1)") { doomed.Add(root); continue; } // 布位残留的玩家实例
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var tn = mb.GetType().Name;
                if (tn == "FPSDisplay" || tn == "CharController_Motor" || tn == "FpsController")
                {
                    if (!doomed.Contains(mb.gameObject)) doomed.Add(mb.gameObject); // 只删脚本宿主本体
                }
            }
        }
        foreach (var go in doomed)
        {
            sb.AppendLine("删除: " + go.name + "（父: " + (go.transform.parent != null ? go.transform.parent.name : "<root>") + "）");
            Object.DestroyImmediate(go);
        }
        EditorSceneManager.SaveScene(scene);
        int terr = Object.FindObjectsOfType<Terrain>(true).Length;
        int lights = Object.FindObjectsOfType<Light>(true).Length;
        sb.AppendLine("环境校验: 地形=" + terr + " 灯=" + lights + "（应 1/16）");
        return sb.ToString();
    }


    /// <summary>阶段5a（Metro）：ExtractionTimer 挂载 + 手电筒拾取物（前人遗留）+ 粉笔箭头。</summary>
    public static string Stage5_Wiring()
    {
        var sb = new StringBuilder();
        var root = GameRoot();
        var mb = Object.FindObjectsOfType<MetroBlackout>(true);
        var groups = Object.FindObjectsOfType<LightGroup>(true);
        Object[] groupObjs = groups;
        var doorGo = GameObject.Find("anywhere-door-Metro");
        var exitDoor = doorGo != null ? doorGo.GetComponentInChildren<AnywhereDoor>(true) : null;
        if (mb.Length == 0 || exitDoor == null) return "blackout/exitDoor missing";

        // 撤离计时器
        var timerT = root.Find("撤离计时器");
        GameObject timerGo;
        if (timerT == null) { timerGo = new GameObject("撤离计时器"); timerGo.transform.SetParent(root, false); }
        else timerGo = timerT.gameObject;
        var timer = timerGo.GetComponent<ExtractionTimer>();
        if (timer == null) timer = timerGo.AddComponent<ExtractionTimer>();
        SetPrivate(timer, "blackout", mb[0]);
        SetPrivate(timer, "exitDoor", exitDoor);
        sb.AppendLine("timer wired");

        // 手电筒拾取物（Metro 门区，前人遗留）
        var items = EnsureChild(root, "Items");
        if (items.Find("道具_手电筒_Metro") == null)
        {
            var fl = InstantiateModel("Assets/items/Exploration objects/Flashlight/Flashlight.fbx", "道具_手电筒_Metro",
                new Vector3(-46.2f, 94.85f, 130.6f), 0.3f, 120f);
            if (fl != null)
            {
                fl.transform.SetParent(items, true);
                fl.AddComponent<FlashlightPickup>();
                SetText(fl.AddComponent<InteractionLabel>(), "text", "拾取手电筒");
                sb.AppendLine("flashlight(Metro) ok");
            }
            else sb.AppendLine("flashlight model MISSING");
        }

        // 粉笔箭头：站台→死端沿线，逆行惊吓区在车厢端
        var anom = EnsureChild(root, "异常");
        var chalkT = anom.Find("粉笔箭头");
        GameObject chalkGo;
        if (chalkT == null) { chalkGo = new GameObject("粉笔箭头"); chalkGo.transform.SetParent(anom, false); }
        else chalkGo = chalkT.gameObject;
        chalkGo.transform.position = new Vector3(-41.5f, 94.12f, 125f);
        var chalk = chalkGo.GetComponent<ChalkTrail>();
        if (chalk == null) chalk = chalkGo.AddComponent<ChalkTrail>();
        var so = new SerializedObject(chalk);
        var pts = so.FindProperty("localPoints");
        pts.arraySize = 6;
        for (int i = 0; i < 6; i++)
            pts.GetArrayElementAtIndex(i).vector3Value = new Vector3((i % 2 == 0 ? -0.6f : 0.6f), 0f, -12f - i * 14f);
        so.FindProperty("scareCenter").vector3Value = new Vector3(0f, 2f, 50f);
        so.FindProperty("scareSize").vector3Value = new Vector3(8f, 5f, 14f);
        so.ApplyModifiedPropertiesWithoutUndo();
        SetPrivate(chalk, "blackout", mb[0]);
        SetPrivateArray(chalk, "groups", groupObjs);
        sb.AppendLine("chalk wired (6 箭头, 惊吓区在车厢端)");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>阶段5b（Lab）：实验室门旁放手电筒（出发前带上）。</summary>
    public static string Stage5_Lab()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SpecimenLab.unity", OpenSceneMode.Additive);
        var sb = new StringBuilder();
        if (GameObject.Find("道具_手电筒_Lab") == null)
        {
            var prevActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var fl = InstantiateModel("Assets/items/Exploration objects/Flashlight/Flashlight.fbx", "道具_手电筒_Lab",
                new Vector3(2.6f, 0.55f, 4.0f), 0.3f, 40f);
            if (fl != null)
            {
                SceneManager.MoveGameObjectToScene(fl, scene);
                fl.AddComponent<FlashlightPickup>();
                SetText(fl.AddComponent<InteractionLabel>(), "text", "拾取手电筒");
                sb.AppendLine("flashlight(Lab) ok");
            }
            else sb.AppendLine("flashlight model MISSING");
            SceneManager.SetActiveScene(prevActive);
        }
        else sb.AppendLine("exists");
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return sb.ToString();
    }


    private static void WriteWav(string path, float[] data, int sampleRate)
    {
        int n = data.Length;
        var ms = new System.IO.MemoryStream(44 + n * 2);
        var w = new System.IO.BinaryWriter(ms);
        WriteAscii(w, "RIFF"); w.Write(36 + n * 2); WriteAscii(w, "WAVE");
        WriteAscii(w, "fmt "); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(sampleRate); w.Write(sampleRate * 2); w.Write((short)2); w.Write((short)16);
        WriteAscii(w, "data"); w.Write(n * 2);
        for (int i = 0; i < n; i++) w.Write((short)(Mathf.Clamp(data[i], -1f, 1f) * 32760f));
        w.Flush();
        System.IO.File.WriteAllBytes(path, ms.ToArray());
        w.Close();
    }
}
