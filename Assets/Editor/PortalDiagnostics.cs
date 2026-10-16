using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 传送门数值自检 + 投影屏槽位修正。
///
/// 验证目标一：虚拟相机的偏心视锥是否**精确**穿过目标门洞四角。
/// 做法：把 Metro 门洞四角用 (投影 × 视图) 矩阵投回 NDC，结果应当精确为 (±1, ±1)。
///
/// 验证目标二：投影屏（PortalWindow.backSlot）贴的部位对不对。
/// 做法：把 Cube.001 每个子网格的包围盒投到门的右/上/前三个轴上量尺寸，
/// 看被选中的那一槽是不是「尺寸和门洞相当」的那块内板。见 CheckBackSlot 的注释。
///
/// 两步都不需要肉眼，也不需要进 Play 模式。
///
/// 菜单：Tools / 传送门 / 数值自检 (v3)
///       Tools / 传送门 / 修正投影屏槽位
/// </summary>
public static class PortalDiagnostics
{
    private const string MenuRoot = "Tools/传送门/";
    private const string LabScenePath = "Assets/Scenes/SpecimenLab.unity";
    private const string MetroScenePath = "Assets/Scenes/Metro.unity";
    private const string OpeningPath = "Cube.001/Plane";
    private const string LabDoorName = "anywhere-door";
    private const string MetroDoorName = "anywhere-door";

    public static string SentinelPath { get { return Application.dataPath + "/Editor/PortalDiagnostics.run"; } }

    [MenuItem(MenuRoot + "数值自检", false, 10)]
    public static void RunSelfTest()
    {
        var sb = new StringBuilder();
        sb.AppendLine("========== PortalDiagnostics 数值自检 ==========");

        // 两个场景都以 Additive 方式补齐，绝不 Single 打开——避免覆盖用户当前编辑的场景
        var opened = new System.Collections.Generic.List<Scene>();
        Scene lab = EnsureAdditive(LabScenePath, opened);
        Scene metro = EnsureAdditive(MetroScenePath, opened);
        try
        {
            RunInner(sb, lab, metro);
        }
        finally
        {
            // 收尾：关掉本次为自检打开的临时场景，恢复用户原状
            foreach (var s in opened)
                if (s.IsValid() && s.isLoaded) EditorSceneManager.CloseScene(s, true);
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 一键把场景里各扇门的 backSlot 修到「尺寸与门洞相当」的那一槽（见 CheckBackSlot）。
    /// 幂等：已经对了就什么都不写；只有真的改了才存场景。
    /// </summary>
    [MenuItem(MenuRoot + "修正投影屏槽位", false, 11)]
    public static void FixBackSlots()
    {
        var sb = new StringBuilder();
        sb.AppendLine("========== 投影屏槽位修正 ==========");

        var opened = new System.Collections.Generic.List<Scene>();
        Scene lab = EnsureAdditive(LabScenePath, opened);
        Scene metro = EnsureAdditive(MetroScenePath, opened);
        bool changed = false;
        try
        {
            changed |= FixOne(sb, lab, LabDoorName, "实验室门");
            changed |= FixOne(sb, metro, MetroDoorName, "Metro 门");
            if (changed)
            {
                if (lab.IsValid() && lab.isLoaded) EditorSceneManager.SaveScene(lab);
                if (metro.IsValid() && metro.isLoaded) EditorSceneManager.SaveScene(metro);
                sb.AppendLine("已保存场景。");
            }
            else sb.AppendLine("无需改动。");
        }
        finally
        {
            foreach (var s in opened)
                if (s.IsValid() && s.isLoaded) EditorSceneManager.CloseScene(s, true);
        }
        Debug.Log(sb.ToString());
    }

    private static bool FixOne(StringBuilder sb, Scene scene, string doorName, string label)
    {
        if (!scene.IsValid() || !scene.isLoaded) { sb.AppendLine(label + "：场景未加载，跳过"); return false; }
        Transform door = FindDoor(scene, doorName);
        if (door == null) { sb.AppendLine(label + "：找不到门，跳过"); return false; }
        Transform plane = FindOpening(door);
        if (plane == null) { sb.AppendLine(label + "：找不到门洞平面，跳过"); return false; }
        return CheckBackSlot(sb, door, plane, label, true);
    }

    private static void RunInner(StringBuilder sb, Scene lab, Scene metro)
    {
        if (!lab.IsValid() || !metro.IsValid())
        {
            sb.AppendLine("FAIL：场景未能加载（lab=" + lab.IsValid() + " metro=" + metro.IsValid() + "）");
            return;
        }

        Transform labDoor = FindDoor(lab, LabDoorName);
        Transform metroDoor = FindDoor(metro, MetroDoorName);
        sb.AppendLine("实验室门：" + Describe(labDoor));
        sb.AppendLine("Metro 门 ：" + Describe(metroDoor));
        if (labDoor == null || metroDoor == null)
        {
            sb.AppendLine("FAIL：找不到门");
            return;
        }

        Transform labPlane = FindOpening(labDoor);
        Transform metroPlane = FindOpening(metroDoor);
        if (labPlane == null || metroPlane == null)
        {
            sb.AppendLine("FAIL：找不到门洞平面（lab=" + (labPlane != null) + " metro=" + (metroPlane != null) + "）");
            return;
        }
        sb.AppendLine("实验室门洞平面：" + PathOf(labPlane, labDoor));
        sb.AppendLine("Metro 门洞平面：" + PathOf(metroPlane, metroDoor));

        var src = new Vector3[4];
        var dst = new Vector3[4];
        ReadCorners(labPlane, src);
        ReadCorners(metroPlane, dst);

        sb.AppendLine("实验室门洞 宽=" + (src[1] - src[0]).magnitude.ToString("F3")
                    + " 高=" + (src[3] - src[0]).magnitude.ToString("F3")
                    + " 中心=" + ((src[0] + src[2]) * 0.5f).ToString("F2"));
        sb.AppendLine("Metro 门洞 宽=" + (dst[1] - dst[0]).magnitude.ToString("F3")
                    + " 高=" + (dst[3] - dst[0]).magnitude.ToString("F3")
                    + " 中心=" + ((dst[0] + dst[2]) * 0.5f).ToString("F2"));

        // 玩家实际站位（判断玩家在门的哪一侧）——决定 mapYaw180 该取哪个值
        var player = GameObject.Find("player");
        if (player != null)
        {
            Vector3 pl = labDoor.InverseTransformPoint(player.transform.position);
            sb.AppendLine("玩家在实验室门本地坐标 = " + pl.ToString("F2")
                        + "   → 玩家在门的 " + (pl.z >= 0f ? "+Z 侧" : "-Z 侧"));
        }
        else sb.AppendLine("（找不到 player，跳过站位判定）");

        // 用几个不同的眼位各测一遍（门两侧都测）：偏心视锥的正确性与眼位无关，都应当 PASS
        var eyeOffsets = new[]
        {
            new Vector3(0f, 1.6f, 2.5f),
            new Vector3(0f, 1.6f, -2.5f),
            new Vector3(0.6f, 1.6f, 2.0f),
            new Vector3(-0.8f, 1.5f, 3.5f),
        };

        bool allOk = true;

        // 投影屏槽位自检：贴错部位是「门框消失 / 看不见对面世界」那一类错误的根因
        allOk &= CheckBackSlot(sb, labDoor, labPlane, "实验室门", false);
        allOk &= CheckBackSlot(sb, metroDoor, metroPlane, "Metro 门", false);
        for (int k = 0; k < eyeOffsets.Length; k++)
        {
            Vector3 eyeWorld = labDoor.position + labDoor.TransformVector(eyeOffsets[k]);
            allOk &= CheckOneEye(eyeWorld, labDoor, metroDoor, dst, sb, k);
        }

        sb.AppendLine("========== 总结：" + (allOk ? "PASS —— 偏心视锥构造正确" : "FAIL —— 见上面各行") + " ==========");
    }

    private static Scene EnsureAdditive(string path, System.Collections.Generic.List<Scene> opened)
    {
        var s = SceneManager.GetSceneByPath(path);
        if (s.IsValid() && s.isLoaded) return s;
        try
        {
            var loaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (loaded.IsValid()) opened.Add(loaded);
            return loaded;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[PortalDiagnostics] 打开场景失败 " + path + " : " + e.Message);
            return default;
        }
    }

    private static Transform FindOpening(Transform door)
    {
        var t = door.Find(OpeningPath);
        if (t != null) return t;
        foreach (var c in door.GetComponentsInChildren<Transform>(true))
            if (c.name.ToLower().Contains("plane")) return c;
        return null;
    }

    private static string PathOf(Transform t, Transform root)
    {
        var s = t.name;
        var p = t.parent;
        while (p != null && p != root) { s = p.name + "/" + s; p = p.parent; }
        return s;
    }

    private static bool CheckOneEye(Vector3 eyeWorld, Transform labDoor, Transform metroDoor, Vector3[] dst, StringBuilder sb, int k)
    {
        // —— 与 PortalWindow.UpdatePortalCamera 完全相同的算法 ——
        Quaternion srcRot = labDoor.rotation;
        Quaternion destRot = metroDoor.rotation * Quaternion.Euler(0f, 180f, 0f); // mapYaw180 = true
        Vector3 localEye = Quaternion.Inverse(srcRot) * (eyeWorld - labDoor.position);
        Vector3 camPos = metroDoor.position + destRot * localEye;

        Vector3 planeCenter = (dst[0] + dst[2]) * 0.5f;
        Vector3 nrm = Vector3.Cross(dst[1] - dst[0], dst[3] - dst[0]).normalized;
        if (Vector3.Dot(planeCenter - camPos, nrm) < 0f) nrm = -nrm;
        Vector3 up = (dst[3] - dst[0]).normalized;
        Quaternion camRot = Quaternion.LookRotation(nrm, up);

        Vector3 cr = camRot * Vector3.right, cu = camRot * Vector3.up, cf = camRot * Vector3.forward;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        float zMin = float.MaxValue, zMax = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = dst[i] - camPos;
            float x = Vector3.Dot(d, cr), y = Vector3.Dot(d, cu), z = Vector3.Dot(d, cf);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            zMin = Mathf.Min(zMin, z); zMax = Mathf.Max(zMax, z);
        }

        const float near = 0.05f, far = 200f;
        float planeZ = (zMin + zMax) * 0.5f;
        if (planeZ <= 0.001f)
        {
            sb.AppendLine("  眼位#" + k + " FAIL：门洞平面在相机背后（z=" + planeZ.ToString("F3") + "）→ 法线方向约定选反了");
            return false;
        }
        float s = near / planeZ;
        var proj = Matrix4x4.Frustum(minX * s, maxX * s, minY * s, maxY * s, near, far);
        // 关键：Unity 的 Camera.worldToCameraMatrix 含 Z 翻转（相机看向 -Z，OpenGL 约定），
        // 自检必须用同一套，否则 y 会整体偏移。
        var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(camPos, camRot, Vector3.one).inverse;

        var line = new StringBuilder("  眼位#" + k + " (局部 " + (eyeWorld - labDoor.position).ToString("F2") + ") → ");
        bool ok = true;
        for (int i = 0; i < 4; i++)
        {
            Vector4 clip = (proj * view) * new Vector4(dst[i].x, dst[i].y, dst[i].z, 1f);
            Vector3 ndc = new Vector3(clip.x / clip.w, clip.y / clip.w, clip.z / clip.w);
            bool okX = Mathf.Abs(Mathf.Abs(ndc.x) - 1f) < 0.01f;
            bool okY = Mathf.Abs(Mathf.Abs(ndc.y) - 1f) < 0.01f;
            if (!okX || !okY) ok = false;
            line.Append("c" + i + "(" + ndc.x.ToString("F3") + "," + ndc.y.ToString("F3") + ") ");
        }
        line.Append(ok ? "  [PASS]" : "  [FAIL]");
        sb.AppendLine(line.ToString());
        return ok;
    }

    // ── 投影屏槽位 ────────────────────────────────────────────────
    /// <summary>
    /// 检查（write=true 时顺带修正）投影屏槽位。
    ///
    /// 判据来自实测：门模型 Cube.001 有两个子网格，一槽是"堵住门洞的那块内板"
    /// （正面看得见的一大片，约 1.22×2.19，和门洞 1.23×2.11 基本同尺寸），
    /// 另一槽是"门框环"（只有四周一圈 8~14 cm 的边，外廓约 1.39×2.39）。
    /// 投影屏必须贴内板那一槽；贴到门框环上的话，环上显示的是投影边缘被 saturate
    /// 拉伸出来的像素（看起来"左右门框消失"），而门洞中间还是门自己的暗板
    /// （看起来"对面世界跑到别处去了"）。
    ///
    /// 所以判据只认几何：**哪一槽的宽高最接近门洞，哪一槽就是内板**。
    /// 这条判据与材质名、与三角面数都无关，换门模型也仍然成立。
    /// </summary>
    private static bool CheckBackSlot(StringBuilder sb, Transform door, Transform plane, string label, bool write)
    {
        var pw = door.GetComponentInChildren<PortalWindow>(true);
        if (pw == null) { sb.AppendLine(label + " 槽位自检：跳过（门上没有 PortalWindow）"); return true; }

        var so = new SerializedObject(pw);
        var prop = so.FindProperty("backSlot");
        if (prop == null) { sb.AppendLine(label + " 槽位自检：跳过（找不到 backSlot 字段）"); return true; }
        int chosen = prop.intValue;

        var frame = door.Find("Cube.001");
        var mf = frame != null ? frame.GetComponent<MeshFilter>() : null;
        var mr = frame != null ? frame.GetComponent<MeshRenderer>() : null;
        var mesh = mf != null ? mf.sharedMesh : null;
        if (mesh == null || mr == null)
        {
            sb.AppendLine(label + " 槽位自检：FAIL（找不到 Cube.001 的网格）");
            return false;
        }
        if (mesh.subMeshCount < 2)
        {
            sb.AppendLine(label + " 槽位自检：跳过（Cube.001 只有 " + mesh.subMeshCount + " 个子网格）");
            return true;
        }

        float openW, openH;
        MeasureOpening(door, plane, out openW, out openH);
        sb.AppendLine(label + " 槽位自检（门洞 宽" + openW.ToString("F3") + " 高" + openH.ToString("F3")
                    + "，backSlot=" + chosen + "）：");

        const float tolerance = 0.15f;   // 宽+高 的总误差上限（米）：内板与门框环差 0.5m 量级，够区分
        int best = -1;
        float bestErr = float.MaxValue, chosenErr = float.MaxValue;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            float w, h, dep;
            MeasureSlot(frame, mesh, i, door, out w, out h, out dep);
            float err = Mathf.Abs(w - openW) + Mathf.Abs(h - openH);
            if (err < bestErr) { bestErr = err; best = i; }
            if (i == chosen) chosenErr = err;
            sb.AppendLine("    槽" + i + "：" + (mesh.GetSubMesh(i).indexCount / 3) + " 三角面  宽"
                        + w.ToString("F3") + " 高" + h.ToString("F3") + " 厚" + dep.ToString("F3")
                        + "  与门洞误差 " + err.ToString("F3") + (i == chosen ? "   ← backSlot（当前）" : ""));
        }

        bool ok = (best == chosen) && chosenErr <= tolerance;
        if (ok)
        {
            sb.AppendLine("    判定：PASS —— 槽" + chosen + " 就是门洞里那块内板");
            return true;
        }

        sb.AppendLine("    判定：FAIL —— 与门洞尺寸最接近的是槽" + best + "（误差 " + bestErr.ToString("F3")
                    + "），当前 backSlot=" + chosen + "（误差 " + chosenErr.ToString("F3") + "）");

        if (!write)
        {
            sb.AppendLine("    → 投影屏贴错了部位。菜单 Tools/传送门/修正投影屏槽位 可一键改到槽" + best + "。");
            return false;
        }

        prop.intValue = best;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pw);
        EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
        sb.AppendLine("    → 已修正：backSlot " + chosen + " → " + best);
        return true;
    }

    /// <summary>门洞在门坐标系里的宽高（米），由四角点投到门的右/上轴得到。</summary>
    private static void MeasureOpening(Transform door, Transform plane, out float w, out float h)
    {
        var c = new Vector3[4];
        ReadCorners(plane, c);
        float wMin = float.MaxValue, wMax = float.MinValue, hMin = float.MaxValue, hMax = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            float pw = Vector3.Dot(c[i], door.right), ph = Vector3.Dot(c[i], door.up);
            wMin = Mathf.Min(wMin, pw); wMax = Mathf.Max(wMax, pw);
            hMin = Mathf.Min(hMin, ph); hMax = Mathf.Max(hMax, ph);
        }
        w = wMax - wMin; h = hMax - hMin;
    }

    /// <summary>
    /// 一个子网格在门坐标系里的宽、高、厚。
    /// 用 SubMeshDescriptor 的包围盒（不需要网格开 Read/Write），
    /// 把它的 8 个角点变换到世界再投到门的三个轴上。
    /// </summary>
    private static void MeasureSlot(Transform frame, Mesh mesh, int slot, Transform door,
                                    out float w, out float h, out float dep)
    {
        var b = mesh.GetSubMesh(slot).bounds;
        Vector3 mn = b.min, mx = b.max;
        float wMin = float.MaxValue, wMax = float.MinValue;
        float hMin = float.MaxValue, hMax = float.MinValue;
        float dMin = float.MaxValue, dMax = float.MinValue;
        for (int c = 0; c < 8; c++)
        {
            Vector3 p = frame.TransformPoint(new Vector3(
                (c & 1) == 0 ? mn.x : mx.x,
                (c & 2) == 0 ? mn.y : mx.y,
                (c & 4) == 0 ? mn.z : mx.z));
            float pw = Vector3.Dot(p, door.right), ph = Vector3.Dot(p, door.up), pd = Vector3.Dot(p, door.forward);
            wMin = Mathf.Min(wMin, pw); wMax = Mathf.Max(wMax, pw);
            hMin = Mathf.Min(hMin, ph); hMax = Mathf.Max(hMax, ph);
            dMin = Mathf.Min(dMin, pd); dMax = Mathf.Max(dMax, pd);
        }
        w = wMax - wMin; h = hMax - hMin; dep = dMax - dMin;
    }

    // ── 工具 ──
    private static Transform FindDoor(Scene scene, string name)
    {
        string key = name.ToLower();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.ToLower().Contains(key)) return root.transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains(key)) return t;
        }
        return null;
    }

    private static string Describe(Transform t)
    {
        if (t == null) return "(未找到)";
        return t.name + " pos=" + t.position.ToString("F2") + " euler=" + t.eulerAngles.ToString("F1");
    }

    private static void ReadCorners(Transform plane, Vector3[] outC)
    {
        var mf = plane.GetComponent<MeshFilter>();
        Vector3 hx, hy;
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            hx = plane.TransformVector(new Vector3(b.extents.x, 0f, 0f));
            hy = plane.TransformVector(new Vector3(0f, 0f, b.extents.z));
        }
        else { hx = plane.right * 0.5f; hy = plane.up * 1f; }
        Vector3 c = plane.position;
        outC[0] = c - hx - hy; outC[1] = c + hx - hy; outC[2] = c + hx + hy; outC[3] = c - hx + hy;
    }
}

/// <summary>
/// 一次性自动执行钩子：只在哨兵文件存在时跑一次自检，然后删掉哨兵。
/// 目的是让自检结果能落到 Editor.log，便于无人值守读取。跑完可整体删除本类。
/// </summary>
[InitializeOnLoad]
public static class PortalDiagnosticsAutoRun
{
    static PortalDiagnosticsAutoRun()
    {
        if (!System.IO.File.Exists(PortalDiagnostics.SentinelPath)) return;
        try { System.IO.File.Delete(PortalDiagnostics.SentinelPath); } catch { }
        EditorApplication.delayCall += () => PortalDiagnostics.RunSelfTest();
    }
}
