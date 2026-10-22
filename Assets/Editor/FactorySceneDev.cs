using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 工厂昼/夜双世界开发工具（MetroSceneDev 同构）：场景勘察与分阶段搭建。
/// 昼夜场景布局镜像：Night = Day + (MirrorX, 0, 0)，x 恒差 14797（实测）。
/// 全部为纯编辑器静态方法，经 execute_code 逐个调用。
/// 术语遵守 GLOSSARY：世界/门/传送门/电量/异常/异常物/层域。
/// </summary>
public static class FactorySceneDev
{
    /// <summary>昼夜镜像偏移：Night = Day + (MirrorX, 0, 0)。</summary>
    public const float MirrorX = 14797f;

    // ================= 勘察 =================

    /// <summary>勘察报告：门 / 灯 / 关键件（风机·吊车·配电箱·大门·柜子·灯罩）/ 容器包围盒。</summary>
    public static string Report()
    {
        var sb = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        sb.AppendLine("== SCENE " + scene.name + " ==");

        sb.AppendLine("Fog: " + RenderSettings.fog + " ambient=" + RenderSettings.ambientLight
            + " skybox=" + RenderSettings.skybox);

        sb.AppendLine("== DOORS ==");
        foreach (var n in new[] { "anywhere-door-FactoryDay", "anywhere-door-FactoryNight", "anywhere-door-FactoryNight-A", "anywhere-door-Flooded-Grounds" })
        {
            var go = GameObject.Find(n);
            if (go != null)
                sb.AppendLine(n + " pos=" + go.transform.position.ToString("F2")
                    + " rot=" + go.transform.eulerAngles.ToString("F1") + " fwd=" + go.transform.forward.ToString("F2"));
        }

        sb.AppendLine("== LIGHTS ==");
        foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>(true))
        {
            sb.AppendLine(l.name + " | " + l.type + " | enabled=" + l.enabled
                + " | goActive=" + l.gameObject.activeInHierarchy
                + " | pos=" + l.transform.position.ToString("F2")
                + " | inten=" + l.intensity.ToString("F2") + " range=" + l.range.ToString("F1")
                + " | spot=" + l.spotAngle.ToString("F0"));
        }

        sb.AppendLine("== KEY PROPS（风机/吊车/配电箱/大门/工业门/柜子/灯罩/楼梯）==");
        string[] keys = { "Fan", "Crane", "PowerBox", "Gates", "DoorIndustrial", "MetalCabinet", "Lamp", "Stairs", "Extinguisher" };
        foreach (var go in scene.GetRootGameObjects())
            CollectByKey(go.transform, keys, sb, 0);

        sb.AppendLine("== ROOT CONTAINER BOUNDS ==");
        foreach (var n in new[] { "Factory1Items", "Factory2Items" })
        {
            var c = GameObject.Find(n);
            if (c == null) { sb.AppendLine(n + " MISSING"); continue; }
            sb.AppendLine(n + " " + BoundsOf(c.transform));
            foreach (Transform child in c.transform)
                sb.AppendLine("  " + child.name + " " + BoundsOf(child));
        }
        return sb.ToString();
    }

    /// <summary>参数化勘察截图（编辑模式可用；落 PortalDumps/）。</summary>
    public static string Shot(float px, float py, float pz, float lx, float ly, float lz, string fileName, float fov = 60f, int width = 960, int height = 540)
    {
        var go = new GameObject("DevShotCam");
        var cam = go.AddComponent<Camera>();
        go.transform.position = new Vector3(px, py, pz);
        go.transform.LookAt(new Vector3(lx, ly, lz));
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.02f, 0.02f, 1f);
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
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(go);
        string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "PortalDumps");
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        string full = System.IO.Path.Combine(dir, fileName);
        System.IO.File.WriteAllBytes(full, png);
        return full;
    }

    /// <summary>六连勘察截图：两门区 + Factory2 深处 + 主厅四角。</summary>
    public static string SurveyShots()
    {
        bool night = SceneManager.GetActiveScene().name.Contains("Night");
        float x = night ? MirrorX - 4324f : 0f; // 以昼侧坐标写，夜侧自动平移
        var sb = new StringBuilder();
        sb.AppendLine(Shot(-4324f + x, 2.2f, 6f, -4324f + x, 1.5f, 0f, "fac_door_lab.png", 70f));
        sb.AppendLine(Shot(-4324f + x, 2.2f, -72f, -4324.4f + x, 1.5f, -77.9f, "fac_door_cross.png", 70f));
        sb.AppendLine(Shot(-4330f + x, 4f, -50f, -4366f + x, 2f, -62f, "fac_factory2.png", 75f));
        sb.AppendLine(Shot(-4312f + x, 4f, 10f, -4312f + x, 2f, -20f, "fac_hall_look.png", 75f));
        sb.AppendLine(Shot(-4350f + x, 4f, -20f, -4312f + x, 2f, 0f, "fac_mid_look.png", 75f));
        sb.AppendLine(Shot(-4324f + x, 10f, -30f, -4324f + x, 0f, -30f, "fac_top_down.png", 70f));
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
        if (depth < 1)
            for (int i = 0; i < t.childCount; i++)
                CollectByKey(t.GetChild(i), keys, sb, depth + 1);
    }

    private static string BoundsOf(Transform t)
    {
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return "(no renderers)";
        bool first = true; Bounds b = default;
        foreach (var r in rends) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        return "bounds " + b.min.ToString("F1") + " ~ " + b.max.ToString("F1") + " size=" + b.size.ToString("F1");
    }

    // ================= 音效（程序合成，fal 无 key 的回退） =================

    /// <summary>合成工厂音效 5 段：换班钟声 / 机器嗡鸣（循环）/ 金属敲击 / 扫光刺点 / 闸门滚动。</summary>
    public static string Stage0_Sfx()
    {
        const int sr = 44100;
        string dir = "Assets/Audio/Factory";
        if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Audio", "Factory");
        var rng = new System.Random(7);

        // ① 换班钟声：非谐泛音簇（钟形）双击，指数衰减（3s）
        int n1 = sr * 3;
        var d1 = new float[n1];
        float[] partials = { 1f, 2.76f, 5.4f, 8.9f };
        float[] gains = { 0.5f, 0.22f, 0.1f, 0.05f };
        for (int i = 0; i < n1; i++)
        {
            float t = i / (float)sr;
            float v = 0f;
            for (int p = 0; p < 4; p++)
                v += Mathf.Sin(2f * Mathf.PI * 660f * partials[p] * t) * gains[p] * Mathf.Exp(-3.2f * t * (1f + p * 0.6f));
            if (t > 1.0f) // 第二击
            {
                float t2 = t - 1.0f;
                for (int p = 0; p < 4; p++)
                    v += Mathf.Sin(2f * Mathf.PI * 660f * partials[p] * t2) * gains[p] * 0.8f * Mathf.Exp(-3.2f * t2 * (1f + p * 0.6f));
            }
            d1[i] = Mathf.Clamp(v * 0.7f, -1f, 1f);
        }
        WriteWav(dir + "/factory_bell.wav", d1, sr);

        // ② 机器嗡鸣：55/110Hz + 叶频 165Hz 轻拍 + 慢 AM（8s，正弦对齐可循环）
        int n2 = sr * 8;
        var d2 = new float[n2];
        for (int i = 0; i < n2; i++)
        {
            float t = i / (float)sr;
            float v = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.4f
                    + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.18f
                    + Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.07f;
            float am = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t); // 0.5Hz×8s=4 周期整
            d2[i] = Mathf.Clamp(v * am, -1f, 1f);
        }
        WriteWav(dir + "/factory_hum.wav", d2, sr);

        // ③ 金属敲击：高频非谐簇 + 短噪声爆发，快衰减（0.6s）
        int n3 = sr * 0.6f > 60 ? (int)(sr * 0.6f) : 26460;
        var d3 = new float[n3];
        float[] cp = { 2130f, 3410f, 5180f, 7330f };
        for (int i = 0; i < n3; i++)
        {
            float t = i / (float)sr;
            float v = 0f;
            for (int p = 0; p < 4; p++)
                v += Mathf.Sin(2f * Mathf.PI * cp[p] * t) * (0.3f - p * 0.06f) * Mathf.Exp(-9f * t * (1f + p * 0.4f));
            v += ((float)rng.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-60f * t);
            d3[i] = Mathf.Clamp(v, -1f, 1f);
        }
        WriteWav(dir + "/factory_clank.wav", d3, sr);

        // ④ 扫光刺点：频率上扫正弦 + 噪声拖尾（0.5s）
        int n4 = sr / 2;
        var d4 = new float[n4];
        float ph = 0f;
        for (int i = 0; i < n4; i++)
        {
            float t = i / (float)sr;
            float f = 500f + 2600f * t / 0.5f;
            ph += 2f * Mathf.PI * f / sr;
            float v = Mathf.Sin(ph) * 0.5f * Mathf.Exp(-3.5f * t);
            v += ((float)rng.NextDouble() * 2f - 1f) * 0.2f * Mathf.Exp(-10f * t);
            d4[i] = Mathf.Clamp(v, -1f, 1f);
        }
        WriteWav(dir + "/factory_sting.wav", d4, sr);

        // ⑤ 闸门滚动：布朗噪声低频 + 40Hz 颤动（3s）
        int n5 = sr * 3;
        var d5 = new float[n5];
        float brown = 0f, lp = 0f;
        for (int i = 0; i < n5; i++)
        {
            float t = i / (float)sr;
            brown += ((float)rng.NextDouble() * 2f - 1f) * 0.03f;
            brown = Mathf.Clamp(brown, -0.6f, 0.6f);
            lp += (brown - lp) * 0.08f;
            float trem = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 40f * t);
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 3f)) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (3f - t) * 2f));
            d5[i] = Mathf.Clamp(lp * 2.2f * trem * env, -1f, 1f);
        }
        WriteWav(dir + "/gate_roll.wav", d5, sr);

        AssetDatabase.Refresh();
        return "sfx written: bell / hum / clank / sting / gate_roll → " + dir;
    }

    // ================= 搭建（Stage1~4，2026-10-22 勘察选址定稿） =================
    // 坐标系：昼侧世界坐标；夜侧 = 昼侧 + (MirrorX, 0, 0)。
    // 布局：Factory1 主厅（z −37.6~38.2，两层）+ Factory2 南厅走廊（z −36~−84，尽头昼夜穿行门）
    // + 东墙工业门（z=−70.1）通往程序化深区泵房——泵房在穿行动线之外，锁门不阻断轮换。

    // 关键点位（昼侧坐标，夜侧自动镜像）
    private static readonly Vector3 PosCtrl = new Vector3(-4324.5f, 6f, -22.5f);      // zone 盒中心
    private static readonly Vector3 PosDutyBoard = new Vector3(-4319.8f, 1.75f, 36.55f); // 排班表（北墙大门旁）
    private static readonly Vector3 PosFanEast = new Vector3(-4314.8f, 0.9f, 9.3f);   // 一号风机（东墙）
    private static readonly Vector3 PosFanWest = new Vector3(-4333.4f, 0.9f, 9.3f);   // 二号风机（西墙）
    private static readonly Vector3 PosBoxEast = new Vector3(-4316.1f, 1.02f, 9.3f);  // 控制箱（绿柜旁）
    private static readonly Vector3 PosBoxWest = new Vector3(-4332.1f, 1.02f, 9.3f);
    private static readonly Vector3 PosIndustrialDoor = new Vector3(-4317.8f, 0f, -70.1f); // 深区门
    private static readonly Vector3 PosShedCenter = new Vector3(-4313.5f, 0f, -70.1f);    // 泵房中心
    private static readonly Vector3 PosPump = new Vector3(-4313.5f, 0f, -70.4f);      // 送料机
    private static readonly Vector3 PosCrossClock = new Vector3(-4328.6f, 2.45f, -76.0f); // 挂钟（穿行门西侧）
    private static readonly Vector3 PosCard = new Vector3(-4324.9f, 1.12f, 27.1f);    // 昼·考勤卡（值班角柜顶）
    private static readonly Vector3 PosBook = new Vector3(-4311.6f, 0.62f, -72.4f);   // 夜·交接本（泵房木箱上）
    private static readonly Vector3 PosBeam = new Vector3(-4324.4f, 3.0f, -58.5f);    // 夜·巡检灯枢轴

    /// <summary>阶段1：核心玩法装配（昼/夜通用，night=true 镜像到夜场景）。
    /// 在对应工厂场景打开时执行；幂等。</summary>
    public static string Stage1_Core(bool night)
    {
        string wantScene = night ? "FactoryNight" : "FactoryDay";
        var scene = SceneManager.GetActiveScene();
        if (scene.name != wantScene) return "请在 " + wantScene + " 打开的状态下执行（当前：" + scene.name + "）";

        var sb = new StringBuilder();
        Vector3 P(Vector3 p) { return night ? p + new Vector3(MirrorX, 0f, 0f) : p; }
        var root = GameRoot();
        var clips = LoadClips();

        // 0) 停掉 Animated 风机的自动播放（歇班=停转；MachinePowerSwitch/GhostProduction 才有权让叶片转）
        int stopped = 0;
        foreach (var anim in UnityEngine.Object.FindObjectsOfType<Animation>(true))
        {
            if (!anim.name.Contains("FanBig")) continue;
            if (anim.playAutomatically) { anim.playAutomatically = false; stopped++; }
            anim.Stop();
        }
        if (stopped > 0) sb.AppendLine("animated fans stopped=" + stopped);

        // 1) 排班中枢（控制器 + 底噪）
        var ctrlT = EnsureChild(root, "排班中枢");
        var ctrl = ctrlT.GetComponent<FactoryShiftController>();
        if (ctrl == null) ctrl = ctrlT.gameObject.AddComponent<FactoryShiftController>();
        var cso = new SerializedObject(ctrl);
        cso.FindProperty("world").enumValueIndex = night ? 1 : 0;
        cso.FindProperty("zoneCenter").vector3Value = P(PosCtrl);
        cso.FindProperty("zoneSize").vector3Value = new Vector3(48f, 14f, 140f);
        cso.FindProperty("shiftSeconds").floatValue = 240f;
        cso.ApplyModifiedPropertiesWithoutUndo();
        var humSrc = EnsureChild(root, "底噪").GetComponent<AudioSource>();
        if (humSrc == null) humSrc = root.Find("底噪").gameObject.AddComponent<AudioSource>();
        humSrc.clip = clips.hum; humSrc.loop = true; humSrc.volume = 0f; humSrc.playOnAwake = false;
        SetPrivate(ctrl, "ambience", humSrc);
        sb.AppendLine("controller[" + (night ? "Night" : "Day") + "] ok");

        // 2) 值班室排班板（两场景镜像；谜题中枢，完成前读表、完成后锁定/改锁）
        if (root.Find("值班室/排班表") == null)
        {
            var duty = EnsureChild(root, "值班室");
            var board = BuildBoard(duty, P(PosDutyBoard), 180f);
            var b = board.AddComponent<ShiftScheduleBoard>();
            var bso = new SerializedObject(b);
            var names = bso.FindProperty("machineNames");
            names.arraySize = 3;
            names.GetArrayElementAtIndex(0).stringValue = "一号风机";
            names.GetArrayElementAtIndex(1).stringValue = "二号风机";
            names.GetArrayElementAtIndex(2).stringValue = "深区送料机";
            bso.ApplyModifiedPropertiesWithoutUndo();
            SetText(board.AddComponent<InteractionLabel>(), "text", "读排班表");
            sb.AppendLine("board ok");
        }

        // 3) 昼侧机器 ×2（主厅东西墙风机；槽位刻意选无 Animated 叠加的 z=9.3 对）
        if (!night)
        {
            BuildMachine(root, 0, "一号风机", PosFanEast, PosBoxEast, 90f, ctrl, clips);
            BuildMachine(root, 1, "二号风机", PosFanWest, PosBoxWest, -90f, ctrl, clips);
            sb.AppendLine("machines 0/1 ok（主厅风机）");
        }

        // 4) 深区门：工业门挂 ShiftGate（昼侧=白班锁死；夜侧=夜班开放；谜题后全开）
        var doorInd = FindNearest("DoorIndustrial01_1", P(PosIndustrialDoor));
        if (doorInd != null)
        {
            var gate = doorInd.GetComponent<ShiftGate>();
            if (gate == null)
            {
                gate = doorInd.gameObject.AddComponent<ShiftGate>();
                var blocker = doorInd.GetComponent<BoxCollider>();
                if (blocker == null)
                {
                    blocker = doorInd.gameObject.AddComponent<BoxCollider>();
                    blocker.center = new Vector3(0f, 1.3f, 0f);
                    blocker.size = new Vector3(3.0f, 2.6f, 0.4f);
                }
                SetPrivate(gate, "blockingCollider", blocker);
                var leaf = doorInd.Find("Left");
                if (leaf != null) SetPrivate(gate, "doorLeaf", leaf);
                SetVector3(gate, "openEuler", new Vector3(0f, -5f, 0f));
                SetPrivate(gate, "controller", ctrl);
                var roll = doorInd.GetComponent<AudioSource>();
                if (roll == null) { roll = doorInd.gameObject.AddComponent<AudioSource>(); roll.clip = clips.gateRoll; roll.playOnAwake = false; }
                SetPrivate(gate, "roll", roll);
                sb.AppendLine("gate ok @ " + doorInd.position.ToString("F1"));
            }
            else sb.AppendLine("gate exists");
        }
        else sb.AppendLine("DoorIndustrial NOT FOUND");

        // 5) 深区泵房（程序化围护，两场景镜像；西面借 annex 东墙与工业门）
        BuildShed(root, P(PosShedCenter));
        sb.AppendLine("shed ok");

        // 6) 夜侧：深区送料机（机器 2）+ 泵房灯 + 交接本；昼侧：泵房木箱
        if (night)
        {
            BuildMachine(root, 2, "深区送料机", P(PosPump), P(PosPump) + new Vector3(0f, 0f, 1.6f), 0f, ctrl, clips, proceduralPump: true);
            var lampT = root.Find("深区泵房/泵房灯");
            if (lampT == null)
            {
                var lampGo = new GameObject("泵房灯");
                lampGo.transform.SetParent(EnsureChild(root, "深区泵房"), false);
                lampGo.transform.position = P(PosShedCenter) + new Vector3(0f, 2.6f, 0f);
                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Point; lamp.color = new Color(1f, 0.85f, 0.6f);
                lamp.range = 7f; lamp.intensity = 0.75f; lamp.renderMode = LightRenderMode.ForcePixel;
                sb.AppendLine("shed lamp ok");
            }
            if (root.Find("Items/Specimen_交接本") == null)
            {
                var book = BuildBook(EnsureChild(root, "Items"), P(PosBook));
                var so2 = new SerializedObject(book.GetComponent<SpecimenPickup>());
                so2.FindProperty("specimenName").stringValue = "对不上班的交接本";
                so2.FindProperty("flavor").stringValue = "两个班次互相留言\"见字如面\"，落款日期是同一天";
                so2.ApplyModifiedPropertiesWithoutUndo();
                SetText(book.AddComponent<InteractionLabel>(), "text", "回收异常物·交接本");
                sb.AppendLine("specimen(夜·交接本) ok");
            }
        }
        else if (root.Find("深区泵房/木箱") == null)
        {
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "木箱";
            crate.transform.SetParent(EnsureChild(root, "深区泵房"), false);
            crate.transform.position = P(PosShedCenter) + new Vector3(1.2f, 0.3f, -1.6f);
            crate.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            crate.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryShed", new Color(0.16f, 0.13f, 0.09f));
        }

        // 7) 换班挂钟（穿行门旁；两场景镜像）
        if (root.Find("挂钟") == null)
        {
            var clock = BuildClock(root, P(PosCrossClock), 90f, ctrl, clips);
            sb.AppendLine("clock ok");
        }

        // 8) 昼·考勤卡（值班角柜顶）
        if (!night && root.Find("Items/Specimen_考勤卡") == null)
        {
            var card = BuildCard(EnsureChild(root, "Items"), P(PosCard));
            var so3 = new SerializedObject(card.GetComponent<SpecimenPickup>());
            so3.FindProperty("specimenName").stringValue = "停在 17:59 的考勤卡";
            so3.FindProperty("flavor").stringValue = "全厂的下班时间，只差一分钟";
            so3.ApplyModifiedPropertiesWithoutUndo();
            SetText(card.AddComponent<InteractionLabel>(), "text", "回收异常物·考勤卡");
            sb.AppendLine("specimen(昼·考勤卡) ok");
        }

        // 9) HUD（先到先得渲染；从工厂直接 Play 的开发流也能看到）
        var hud = root.GetComponent<OrderBoard>() == null ? root.gameObject.AddComponent<OrderBoard>() : null;
        if (root.GetComponent<DutyBriefing>() == null) root.gameObject.AddComponent<DutyBriefing>();
        if (root.GetComponent<BillPanel>() == null) root.gameObject.AddComponent<BillPanel>();
        sb.AppendLine(hud != null ? "hud ok（OrderBoard/Briefing/Bill）" : "hud exists");

        // 10) 撤离计时器（只挂昼场景；夜侧 zone 跨场景运行时查找）
        if (!night && root.Find("撤离计时器") == null)
        {
            var timerGo = new GameObject("撤离计时器");
            timerGo.transform.SetParent(root, false);
            var timer = timerGo.AddComponent<FactoryExtractionTimer>();
            var labDoor = GameObject.Find("anywhere-door-FactoryDay");
            if (labDoor != null) SetPrivate(timer, "exitDoor", labDoor.GetComponentInChildren<AnywhereDoor>(true));
            sb.AppendLine("extraction timer ok");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>阶段2（夜场景）：两个夜间异常——自开的生产线 + 巡检灯束。</summary>
    public static string Stage2_Anomalies()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "FactoryNight") return "请在 FactoryNight 打开的状态下执行（当前：" + scene.name + "）";
        var sb = new StringBuilder();
        var root = GameRoot();
        var clips = LoadClips();
        var ctrl = UnityEngine.Object.FindObjectsOfType<FactoryShiftController>(true)
            .FirstOrDefault(c => c.World == FactoryShift.Phase.Night);
        if (ctrl == null) return "夜侧控制器缺失——先跑 Stage1_Core(true)";

        // 自开的生产线：轰鸣 + 敲击池；转子 = 夜场景全部 FanBlades（事件临时驱动）
        var ghostT = EnsureChild(EnsureChild(root, "异常"), "自开的生产线");
        var ghost = ghostT.GetComponent<GhostProduction>();
        if (ghost == null)
        {
            ghost = ghostT.gameObject.AddComponent<GhostProduction>();
            var roar = ghostT.GetComponent<AudioSource>();
            if (roar == null) { roar = ghostT.gameObject.AddComponent<AudioSource>(); }
            roar.clip = clips.hum; roar.loop = true; roar.playOnAwake = false; roar.volume = 0f;
            roar.spatialBlend = 0.6f; roar.maxDistance = 60f; roar.rolloffMode = AudioRolloffMode.Linear;
            var clankA = new GameObject("敲击A"); clankA.transform.SetParent(ghostT, false);
            clankA.transform.localPosition = new Vector3(0f, 1.5f, 12f);
            var cA = clankA.AddComponent<AudioSource>(); cA.clip = clips.clank; cA.playOnAwake = false; cA.spatialBlend = 1f; cA.maxDistance = 40f;
            var clankB = new GameObject("敲击B"); clankB.transform.SetParent(ghostT, false);
            clankB.transform.localPosition = new Vector3(0f, 1.5f, -16f);
            var cB = clankB.AddComponent<AudioSource>(); cB.clip = clips.clank; cB.playOnAwake = false; cB.spatialBlend = 1f; cB.maxDistance = 40f;

            var rotors = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .Where(t => t.name == "FanBlades" && Mathf.Abs(t.position.x - (PosFanEast.x + MirrorX)) < 30f)
                .Cast<UnityEngine.Object>().ToArray();
            SetPrivateArray(ghost, "rotors", rotors);
            SetPrivate(ghost, "controller", ctrl);
            SetPrivate(ghost, "roar", roar);
            SetPrivateArray(ghost, "clanks", new UnityEngine.Object[] { cA, cB });
            sb.AppendLine("ghost production ok（rotors=" + rotors.Length + "）");
        }
        else sb.AppendLine("ghost exists");

        // 巡检灯束：annex 走廊中央枢轴，光锥来回扫
        var beamT = EnsureChild(EnsureChild(root, "异常"), "巡检灯");
        var beam = beamT.GetComponent<PatrolBeam>();
        if (beam == null)
        {
            beam = beamT.gameObject.AddComponent<PatrolBeam>();
            beamT.position = PosBeam + new Vector3(MirrorX, 0f, 0f);
            var spotGo = new GameObject("Spot");
            spotGo.transform.SetParent(beamT, false);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot; spot.spotAngle = 24f; spot.range = 22f;
            spot.intensity = 3.2f; spot.color = new Color(1f, 0.92f, 0.75f);
            spot.renderMode = LightRenderMode.ForcePixel;
            var sting = beamT.GetComponent<AudioSource>();
            if (sting == null) sting = beamT.gameObject.AddComponent<AudioSource>();
            sting.clip = clips.sting; sting.playOnAwake = false;
            SetPrivate(beam, "spot", spot);
            SetPrivate(beam, "controller", ctrl);
            SetPrivate(beam, "sting", sting);
            sb.AppendLine("patrol beam ok");
        }
        else sb.AppendLine("beam exists");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>阶段3（昼场景）：常驻开发玩家（双玩家守卫兜底）。</summary>
    public static string Stage3_Player()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "FactoryDay") return "请在 FactoryDay 打开的状态下执行（当前：" + scene.name + "）";
        if (GameObject.Find("Player(工厂开发)") != null) return "exists";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/player.prefab");
        if (prefab == null) return "player.prefab NOT FOUND";
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = "Player(工厂开发)";
        go.transform.position = new Vector3(-4320f, 1.1f, 3f);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        EditorSceneManager.MarkSceneDirty(scene);
        return "dev player ok @ (-4320, 1.1, 3)";
    }

    /// <summary>阶段4：订单目标 3→5（Lab+Metro 的订单板）+ 随身收容容量 3→5（Lab）。</summary>
    public static string Stage4_Orders()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { "Assets/Scenes/SpecimenLab.unity", "Assets/Scenes/Metro.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (var board in UnityEngine.Object.FindObjectsOfType<OrderBoard>(true))
            {
                SetInt(board, "targetCount", 5);
                sb.AppendLine("OrderBoard@" + scene.name + " target=5");
            }
            if (scene.name == "SpecimenLab")
            {
                foreach (var inv in UnityEngine.Object.FindObjectsOfType<Inventory>(true))
                {
                    SetInt(inv, "specimenCapacity", 5);
                    sb.AppendLine("Inventory capacity=5");
                }
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }
        return sb.ToString();
    }

    // ---------- 搭建小件 ----------

    /// <summary>视觉修正批次1（2026-10-22 截图评审）：排班板朝向/控制箱按钮尺寸/泵房西墙封缝/挂钟挪实墙/木箱观感。昼/夜通用。</summary>
    public static string FixPass1()
    {
        bool night = SceneManager.GetActiveScene().name.Contains("Night");
        Vector3 P(Vector3 p) { return night ? p + new Vector3(MirrorX, 0f, 0f) : p; }
        var root = GameRoot();
        var sb = new StringBuilder();

        // 1) 排班板：yaw 180 时板面(local −z)朝北进墙 → 转回 yaw 0 朝南（房间侧），微离墙
        var board = root.Find("值班室/排班表");
        if (board != null)
        {
            board.position = P(new Vector3(-4319.8f, 1.75f, 36.8f));
            board.rotation = Quaternion.Euler(0f, 0f, 0f);
            sb.AppendLine("board re-faced");
        }

        // 2) 控制箱按钮：父级箱体非均匀缩放 (0.34,0.5,0.22)，子物体局部值需按比例放大补偿
        foreach (var btn in root.GetComponentsInChildren<Transform>(true))
        {
            if (btn.name != "按钮") continue;
            btn.localPosition = new Vector3(0f, 0.2f, -0.545f); // 世界≈(0, +0.10, −0.12)
            btn.localScale = new Vector3(0.265f, 0.18f, 0.091f); // 世界≈(0.09, 0.09, 0.02)
            sb.AppendLine("button fixed @ " + btn.parent.name);
        }

        // 3) 泵房西墙封缝：整体西移贴住 annex 东墙（门洞两侧补墙段）
        var shed = root.Find("深区泵房");
        if (shed != null)
        {
            bool hadLamp = shed.Find("泵房灯") != null;
            UnityEngine.Object.DestroyImmediate(shed.gameObject);
            BuildShed(root, P(new Vector3(-4314.2f, 0f, -70.1f)));
            var shedNew = root.Find("深区泵房");
            if (hadLamp)
            {
                var lampGo = new GameObject("泵房灯");
                lampGo.transform.SetParent(shedNew, false);
                lampGo.transform.position = P(new Vector3(-4314.2f, 2.6f, -70.1f));
                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Point; lamp.color = new Color(1f, 0.85f, 0.6f);
                lamp.range = 7f; lamp.intensity = 0.75f; lamp.renderMode = LightRenderMode.ForcePixel;
            }
            if (!night)
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "木箱";
                crate.transform.SetParent(shedNew, false);
                crate.transform.position = P(new Vector3(-4312.6f, 0.4f, -72.7f));
                crate.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                crate.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryWood", new Color(0.42f, 0.3f, 0.18f));
            }
            sb.AppendLine("shed rebuilt（西墙封缝）");
        }

        // 4) 挂钟：窗洞前 → 穿行门西侧实墙（低于窗带），面北朝向走来的玩家
        var clock = root.Find("挂钟");
        if (clock != null)
        {
            clock.position = P(new Vector3(-4326.6f, 2.35f, -77.6f));
            clock.rotation = Quaternion.Euler(0f, 180f, 0f);
            sb.AppendLine("clock moved to solid wall");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>视觉修正批次2（2026-10-22 射线实测后精确定位）：板/钟贴真实墙面（探得北墙内面 z=36.0、南墙内面 z=−83.4），
    /// 钟面朝北（此前 yaw 180 背对玩家），泵房按新 BuildShed 重建。昼/夜通用。</summary>
    public static string FixPass2()
    {
        bool night = SceneManager.GetActiveScene().name.Contains("Night");
        Vector3 P(Vector3 p) { return night ? p + new Vector3(MirrorX, 0f, 0f) : p; }
        var root = GameRoot();
        var sb = new StringBuilder();

        var board = root.Find("值班室/排班表");
        if (board != null)
        {
            board.position = P(new Vector3(-4319.8f, 1.75f, 35.82f)); // 墙内面 z=36.0，板厚 0.05 留 0.13 悬挑
            board.rotation = Quaternion.Euler(0f, 0f, 0f);            // 板面(local −z)朝南面向房间
            sb.AppendLine("board on wall");
        }

        var clock = root.Find("挂钟");
        if (clock != null)
        {
            clock.position = P(new Vector3(-4329.0f, 2.35f, -83.15f)); // Factory2Wall03 内面 −83.4，悬挑 0.25
            clock.rotation = Quaternion.Euler(0f, 0f, 0f);             // 盘面(local +z)朝北面向玩家
            sb.AppendLine("clock on wall, face north");
        }

        sb.AppendLine(RebuildShed(root, night));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>视觉修正批次3（夜侧照明）：泵房灯提亮、交接本局部补光、送料机材质微自发光。</summary>
    public static string FixPass3()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "FactoryNight") return "请在 FactoryNight 打开时执行（当前：" + scene.name + "）";
        var root = GameRoot();
        var sb = new StringBuilder();

        var lamp = root.Find("深区泵房/泵房灯");
        if (lamp != null)
        {
            var l = lamp.GetComponent<Light>();
            l.intensity = 1.6f; l.range = 9f;
            sb.AppendLine("shed lamp 1.6/9");
        }

        var book = root.Find("Items/Specimen_交接本");
        if (book != null)
        {
            var lg = book.Find("补光");
            if (lg == null)
            {
                lg = new GameObject("补光").transform;
                lg.SetParent(book, false);
                lg.localPosition = new Vector3(0f, 0.35f, 0f);
                lg.gameObject.AddComponent<Light>();
            }
            var l2 = lg.GetComponent<Light>();
            l2.type = LightType.Point; l2.color = new Color(1f, 0.9f, 0.7f);
            l2.range = 5f; l2.intensity = 1.4f; l2.renderMode = LightRenderMode.ForcePixel;
            sb.AppendLine("book light 1.4/5");
        }

        // 材质提亮（资产级，改一次两场景生效）——异常物发光与 Metro 软盘同款先例
        var pages = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FactoryBookPages.mat");
        if (pages != null) { pages.EnableKeyword("_EMISSION"); pages.SetColor("_EmissionColor", new Color(0.85f, 0.83f, 0.75f) * 1.2f); sb.AppendLine("pages emissive x1.2"); }
        var rust = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FactoryPumpRust.mat");
        if (rust != null) { rust.EnableKeyword("_EMISSION"); rust.SetColor("_EmissionColor", new Color(0.42f, 0.24f, 0.12f) * 0.3f); sb.AppendLine("rust emissive x0.3"); }
        var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FactoryPumpMetal.mat");
        if (metal != null) { metal.EnableKeyword("_EMISSION"); metal.SetColor("_EmissionColor", new Color(0.3f, 0.32f, 0.34f) * 0.3f); sb.AppendLine("metal emissive x0.3"); }

        // 微调：内页从封皮 +x 侧露出一线（原先完全藏在封皮内）；飞轮外移避开料罐遮挡
        var pagesT = root.Find("Items/Specimen_交接本/内页");
        if (pagesT != null) { pagesT.localPosition = new Vector3(0.014f, 0.001f, 0f); pagesT.localScale = new Vector3(0.16f, 0.02f, 0.21f); sb.AppendLine("pages protrude"); }
        var flywheel = root.Find("机器/深区送料机/飞轮");
        if (flywheel != null) { flywheel.localPosition = new Vector3(0.58f, 1.0f, 0f); sb.AppendLine("flywheel out"); }

        EditorSceneManager.MarkSceneDirty(scene);
        return sb.ToString();
    }

    /// <summary>摧毁并按当前 BuildShed 重建泵房（保留泵房灯/木箱/异常物归属）。</summary>
    private static string RebuildShed(Transform root, bool night)
    {
        bool nightScene = SceneManager.GetActiveScene().name.Contains("Night");
        Vector3 P(Vector3 p) { return nightScene ? p + new Vector3(MirrorX, 0f, 0f) : p; }
        var shed = root.Find("深区泵房");
        if (shed == null) return "no shed";
        bool hadLamp = shed.Find("泵房灯") != null;
        UnityEngine.Object.DestroyImmediate(shed.gameObject);
        BuildShed(root, P(new Vector3(-4314.2f, 0f, -70.1f)));
        var shedNew = root.Find("深区泵房");
        if (hadLamp)
        {
            var lampGo = new GameObject("泵房灯");
            lampGo.transform.SetParent(shedNew, false);
            lampGo.transform.position = P(new Vector3(-4314.2f, 2.6f, -70.1f));
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = new Color(1f, 0.85f, 0.6f);
            lamp.range = 7f; lamp.intensity = 0.75f; lamp.renderMode = LightRenderMode.ForcePixel;
        }
        if (!nightScene && shedNew.Find("木箱") == null)
        {
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "木箱";
            crate.transform.SetParent(shedNew, false);
            crate.transform.position = P(new Vector3(-4312.6f, 0.4f, -72.7f));
            crate.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            crate.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryWood", new Color(0.42f, 0.3f, 0.18f));
        }
        return "shed rebuilt";
    }

    private struct FactoryClips { public AudioClip bell, hum, clank, sting, gateRoll; }

    private static FactoryClips LoadClips()
    {
        return new FactoryClips
        {
            bell = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Factory/factory_bell.wav"),
            hum = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Factory/factory_hum.wav"),
            clank = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Factory/factory_clank.wav"),
            sting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Factory/factory_sting.wav"),
            gateRoll = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Factory/gate_roll.wav"),
        };
    }

    private static Transform FindNearest(string nameContains, Vector3 pos)
    {
        Transform best = null; float bd = float.MaxValue;
        foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
        {
            if (!t.name.Contains(nameContains)) continue;
            float d = (t.position - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = t; }
        }
        return best;
    }

    private static Material GetFactoryMat(string name, Color color, float emission = 0f)
    {
        string path = "Assets/Material/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            m.color = color;
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
            }
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    /// <summary>机器装配：控制箱 + MachinePowerSwitch（rotor=就近 FanBlades，或程序化送料机飞轮）。</summary>
    private static void BuildMachine(Transform root, int id, string dispName, Vector3 fanPos, Vector3 boxPos, float boxYaw,
        FactoryShiftController ctrl, FactoryClips clips, bool proceduralPump = false)
    {
        bool night = ctrl.World == FactoryShift.Phase.Night;
        string goName = "机器_" + dispName;
        if (root.Find("机器/" + goName) != null) return;
        var machines = EnsureChild(root, "机器");
        Transform rotor = null;

        if (proceduralPump)
        {
            var pump = BuildPump(machines, fanPos);
            rotor = pump.transform.Find("飞轮");
        }
        else
        {
            var blades = FindNearest("FanBlades", fanPos);
            rotor = blades;
        }

        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = goName + "_控制箱";
        box.transform.SetParent(machines, false);
        box.transform.position = boxPos;
        box.transform.rotation = Quaternion.Euler(0f, boxYaw, 0f);
        box.transform.localScale = new Vector3(0.34f, 0.5f, 0.22f);
        box.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryPanel", new Color(0.22f, 0.26f, 0.24f));

        var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
        button.name = "按钮";
        button.transform.SetParent(box.transform, false);
        // 父级箱体非均匀缩放 (0.34,0.5,0.22)：局部值按比例补偿，世界尺寸≈(0.09,0.09,0.02)、贴出 −z 面约 12cm
        button.transform.localPosition = new Vector3(0f, 0.2f, -0.545f);
        button.transform.localScale = new Vector3(0.265f, 0.18f, 0.091f);
        button.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryButton", new Color(0.9f, 0.25f, 0.15f), 0.8f);
        UnityEngine.Object.DestroyImmediate(button.GetComponent<Collider>());

        var sw = box.AddComponent<MachinePowerSwitch>();
        SetPrivate(sw, "controller", ctrl);
        SetInt(sw, "machineId", id);
        SetText(sw, "displayName", dispName);
        if (rotor != null) SetPrivate(sw, "rotor", rotor);
        // 墙面风机的叶轮绕其朝向轴（本地 Z，即墙面法线）；送料机飞轮是立式圆柱，绕本地 Y
        SetVector3(sw, "rotorAxis", proceduralPump ? Vector3.up : Vector3.forward);
        var hum = box.AddComponent<AudioSource>();
        hum.clip = clips.hum; hum.loop = true; hum.playOnAwake = false; hum.volume = 0f;
        hum.spatialBlend = 1f; hum.maxDistance = 14f;
        SetPrivate(sw, "hum", hum);
        SetText(box.AddComponent<InteractionLabel>(), "text", "启动" + dispName);
    }

    /// <summary>排班表告示板（板面 + 边框 + 自发光提示条 + 照明灯）。</summary>
    private static GameObject BuildBoard(Transform parent, Vector3 pos, float yaw)
    {
        var rootGo = new GameObject("排班表");
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.position = pos;
        rootGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "边框";
        frame.transform.SetParent(rootGo.transform, false);
        frame.transform.localScale = new Vector3(1.05f, 0.75f, 0.05f);
        frame.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryBoardFrame", new Color(0.12f, 0.12f, 0.13f));

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "板面";
        panel.transform.SetParent(rootGo.transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, -0.005f);
        panel.transform.localScale = new Vector3(0.92f, 0.62f, 0.055f);
        panel.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryBoardPanel", new Color(0.55f, 0.55f, 0.48f));
        UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());

        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "提示条";
        strip.transform.SetParent(rootGo.transform, false);
        strip.transform.localPosition = new Vector3(0f, 0.24f, -0.02f);
        strip.transform.localScale = new Vector3(0.7f, 0.07f, 0.045f);
        strip.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryBoardStrip", new Color(0.25f, 1f, 0.45f), 1.4f);
        UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());

        var lamp = new GameObject("照明灯").AddComponent<Light>();
        lamp.transform.SetParent(rootGo.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 0.55f, -0.5f);
        lamp.type = LightType.Point; lamp.color = new Color(1f, 0.9f, 0.7f);
        lamp.range = 4.5f; lamp.intensity = 0.7f; lamp.renderMode = LightRenderMode.ForcePixel;
        return rootGo;
    }

    /// <summary>换班挂钟：盘面（发光）+ 时/分针（枢轴子物体）+ 钟声音源。
    /// 根朝 yaw（面向走廊）；圆柱件本地 X 转 90° 使轴向=本地 Z（盘面朝外）。</summary>
    private static GameObject BuildClock(Transform parent, Vector3 pos, float yaw, FactoryShiftController ctrl, FactoryClips clips)
    {
        var rootGo = new GameObject("挂钟");
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.position = pos;
        rootGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Quaternion faceTilt = Quaternion.Euler(90f, 0f, 0f); // 圆柱轴向 → 本地 Z

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "外圈";
        rim.transform.SetParent(rootGo.transform, false);
        rim.transform.localRotation = faceTilt;
        rim.transform.localScale = new Vector3(0.55f, 0.035f, 0.55f);
        rim.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryClockRim", new Color(0.1f, 0.1f, 0.11f));
        UnityEngine.Object.DestroyImmediate(rim.GetComponent<Collider>());

        var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        face.name = "盘面";
        face.transform.SetParent(rootGo.transform, false);
        face.transform.localRotation = faceTilt;
        face.transform.localPosition = new Vector3(0f, 0f, 0.012f); // 微凸于外圈
        face.transform.localScale = new Vector3(0.46f, 0.04f, 0.46f);
        face.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryClockFace", new Color(0.35f, 0.5f, 0.4f), 0.6f);
        UnityEngine.Object.DestroyImmediate(face.GetComponent<Collider>());

        var minutePivot = new GameObject("分针枢轴");
        minutePivot.transform.SetParent(rootGo.transform, false);
        minutePivot.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        var minuteHand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        minuteHand.name = "分针";
        minuteHand.transform.SetParent(minutePivot.transform, false);
        minuteHand.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        minuteHand.transform.localScale = new Vector3(0.018f, 0.18f, 0.014f);
        minuteHand.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryClockHand", new Color(0.9f, 0.15f, 0.1f), 0.5f);
        UnityEngine.Object.DestroyImmediate(minuteHand.GetComponent<Collider>());

        var hourPivot = new GameObject("时针枢轴");
        hourPivot.transform.SetParent(rootGo.transform, false);
        hourPivot.transform.localPosition = new Vector3(0f, 0f, 0.045f);
        var hourHand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hourHand.name = "时针";
        hourHand.transform.SetParent(hourPivot.transform, false);
        hourHand.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        hourHand.transform.localScale = new Vector3(0.024f, 0.12f, 0.014f);
        hourHand.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryClockHand", new Color(0.9f, 0.15f, 0.1f), 0.5f);
        UnityEngine.Object.DestroyImmediate(hourHand.GetComponent<Collider>());

        var clock = rootGo.AddComponent<ShiftClock>();
        SetPrivate(clock, "controller", ctrl);
        SetPrivate(clock, "face", face.GetComponent<Renderer>());
        SetPrivate(clock, "minuteHand", minutePivot.transform);
        SetPrivate(clock, "hourHand", hourPivot.transform);
        var chime = rootGo.AddComponent<AudioSource>();
        chime.clip = clips.bell; chime.playOnAwake = false;
        chime.spatialBlend = 0.8f; chime.maxDistance = 30f; chime.volume = 0.8f;
        SetPrivate(clock, "chime", chime);
        return rootGo;
    }

    /// <summary>深区泵房围护：地板 + 四墙（西墙两段，中间留工业门洞）+ 顶。西缘贴 annex 东墙（x=−4318）。</summary>
    private static void BuildShed(Transform root, Vector3 center)
    {
        var shed = EnsureChild(root, "深区泵房");
        var mat = GetFactoryMat("FactoryShed", new Color(0.16f, 0.13f, 0.09f));
        Action<string, Vector3, Vector3> wall = (name, c, s) =>
        {
            if (shed.Find(name) != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(shed, false);
            go.transform.position = c;
            go.transform.localScale = s;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        };
        wall("地板", center + new Vector3(0f, -0.1f, 0f), new Vector3(8.2f, 0.2f, 8f));
        wall("北墙", center + new Vector3(-0.1f, 1.5f, 4f), new Vector3(8.4f, 3f, 0.2f));
        wall("南墙", center + new Vector3(-0.1f, 1.5f, -4f), new Vector3(8.4f, 3f, 0.2f));
        wall("东墙", center + new Vector3(3.7f, 1.5f, 0f), new Vector3(0.3f, 3f, 7.8f));
        wall("顶", center + new Vector3(0f, 3.05f, 0f), new Vector3(8.4f, 0.2f, 8.2f));
        // 西墙两段：中间留工业门洞（z −67.7 ~ −71.4）；西缘嵌入 annex 东墙（x=−418.14）0.1m 堵缝
        wall("西墙A", center + new Vector3(-3.85f, 1.5f, 3.05f), new Vector3(0.3f, 3f, 1.9f));
        wall("西墙B", center + new Vector3(-3.85f, 1.5f, -2.85f), new Vector3(0.3f, 3f, 3.1f));
    }

    /// <summary>深区送料机（图元拼装：基座 + 料罐 + 飞轮【转子】+ 立管）。</summary>
    private static GameObject BuildPump(Transform parent, Vector3 pos)
    {
        var rootGo = new GameObject("深区送料机");
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.position = pos;
        var metal = GetFactoryMat("FactoryPumpMetal", new Color(0.3f, 0.32f, 0.34f));
        var rust = GetFactoryMat("FactoryPumpRust", new Color(0.42f, 0.24f, 0.12f));

        void Part(string name, PrimitiveType type, Vector3 lp, Vector3 ls, Vector3 le, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(rootGo.transform, false);
            go.transform.localPosition = lp;
            go.transform.localScale = ls;
            if (le != Vector3.zero) go.transform.localRotation = Quaternion.Euler(le);
            go.GetComponent<Renderer>().sharedMaterial = m;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        Part("基座", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.9f), Vector3.zero, metal);
        Part("料罐", PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0f), new Vector3(0.85f, 0.65f, 0.85f), Vector3.zero, rust);
        Part("飞轮", PrimitiveType.Cylinder, new Vector3(0.52f, 1.0f, 0f), new Vector3(0.6f, 0.045f, 0.6f), new Vector3(0f, 0f, 90f), metal);
        Part("立管", PrimitiveType.Cylinder, new Vector3(0.18f, 1.95f, 0f), new Vector3(0.14f, 0.55f, 0.14f), Vector3.zero, metal);
        return rootGo;
    }

    /// <summary>异常物·考勤卡（薄卡 + 红条，微发光提高暗处可发现性）。</summary>
    private static GameObject BuildCard(Transform parent, Vector3 pos)
    {
        var rootGo = new GameObject("Specimen_考勤卡");
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.position = pos;
        rootGo.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

        var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        card.name = "卡体";
        card.transform.SetParent(rootGo.transform, false);
        card.transform.localScale = new Vector3(0.1f, 0.006f, 0.065f);
        card.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryCard", new Color(0.88f, 0.86f, 0.78f), 0.25f);
        UnityEngine.Object.DestroyImmediate(card.GetComponent<Collider>());

        var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "红条";
        stripe.transform.SetParent(rootGo.transform, false);
        stripe.transform.localPosition = new Vector3(0f, 0.004f, -0.018f);
        stripe.transform.localScale = new Vector3(0.085f, 0.004f, 0.012f);
        stripe.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryCardStripe", new Color(0.85f, 0.2f, 0.15f), 0.6f);
        UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());

        rootGo.AddComponent<SpecimenPickup>();
        return rootGo;
    }

    /// <summary>异常物·交接本（封皮 + 内页）。</summary>
    private static GameObject BuildBook(Transform parent, Vector3 pos)
    {
        var rootGo = new GameObject("Specimen_交接本");
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.position = pos;
        rootGo.transform.rotation = Quaternion.Euler(0f, -25f, 0f);

        var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cover.name = "封皮";
        cover.transform.SetParent(rootGo.transform, false);
        cover.transform.localScale = new Vector3(0.17f, 0.026f, 0.23f);
        cover.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryBookCover", new Color(0.12f, 0.2f, 0.15f));
        UnityEngine.Object.DestroyImmediate(cover.GetComponent<Collider>());

        var pages = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pages.name = "内页";
        pages.transform.SetParent(rootGo.transform, false);
        pages.transform.localPosition = new Vector3(0.006f, 0.001f, 0f);
        pages.transform.localScale = new Vector3(0.15f, 0.02f, 0.21f);
        pages.GetComponent<Renderer>().sharedMaterial = GetFactoryMat("FactoryBookPages", new Color(0.85f, 0.83f, 0.75f), 0.18f);
        UnityEngine.Object.DestroyImmediate(pages.GetComponent<Collider>());

        rootGo.AddComponent<SpecimenPickup>();
        return rootGo;
    }

    private static void SetVector3(UnityEngine.Object target, string field, Vector3 value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).vector3Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================= 通用小工具 =================

    private static Transform GameRoot()
    {
        var root = GameObject.Find("FactoryGameplay");
        if (root == null) root = new GameObject("FactoryGameplay");
        return root.transform;
    }

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
        if (go == null) go = UnityEngine.Object.Instantiate(src);
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

    private static void SetPrivate(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetText(UnityEngine.Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(UnityEngine.Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(UnityEngine.Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>保存当前场景。</summary>
    public static string Save()
    {
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene);
        return "saved: " + scene.name;
    }

    // ================= Play 模式测试驱动（编辑器失焦 Play 不推进 → 反射手动驱动） =================

    private static Transform PlayerRoot()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
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
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
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

    private static readonly System.Reflection.BindingFlags All = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

    /// <summary>反射直调排班钟相位（PT 专用：跳过真实换班等待）。</summary>
    private static void SetShiftPhase(FactoryShift.Phase p)
    {
        typeof(FactoryShift).GetProperty("Current").GetSetMethod(true).Invoke(null, new object[] { p });
    }

    private static int MachineIdOf(MachinePowerSwitch s)
    {
        return (int)typeof(MachinePowerSwitch).GetField("machineId", All).GetValue(s);
    }

    /// <summary>PT0：后台也跑 + 排班钟回零 + 电量回满。</summary>
    public static string PT_Setup()
    {
        Application.runInBackground = true;
        FactoryShift.ResetAll();
        var bs = UnityEngine.Object.FindObjectsOfType<BatterySystem>(true);
        if (bs.Length > 0)
        {
            bs[0].SetAmbientDrain(0f);
            typeof(BatterySystem).GetProperty("Battery").SetValue(bs[0], 100f, null);
        }
        return "factory PT setup ok（shift=Day/240s 未解谜, battery=100）";
    }

    /// <summary>手动驱动工厂组件 Update（每帧 dt=0.05s）：控制器 / 撤离计时 / 深区门 / 挂钟。
    /// 黑窗驱动同款思路——后台无帧也能推进逻辑。</summary>
    public static string PT_Drive(int frames)
    {
        const float dt = 0.05f;
        int driven = 0;
        for (int f = 0; f < frames; f++)
        {
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                var t = mb.GetType();
                if (t == typeof(FactoryShiftController) || t == typeof(FactoryExtractionTimer)
                    || t == typeof(ShiftGate) || t == typeof(ShiftClock))
                {
                    var m = t.GetMethod("Update", All);
                    if (m != null) { try { m.Invoke(mb, null); driven++; } catch { } }
                }
            }
        }
        return "drove " + frames + " frames × dt=" + dt + "（updates invoked=" + driven + "）";
    }

    /// <summary>状态汇总：玩家 / 排班钟 / 控制器 / 机器 / 深区门 / 撤离窗口。</summary>
    public static string PT_Status()
    {
        var sb = new StringBuilder();
        var root = PlayerRoot();
        sb.AppendLine("player=" + (root != null ? root.position.ToString("F2") : "null"));
        sb.AppendLine("shift: current=" + FactoryShift.Current + " timeLeft=" + FactoryShift.TimeLeft.ToString("F0")
            + " solved=" + FactoryShift.PuzzleSolved + " locked=" + FactoryShift.LockedPhase
            + " started=" + FactoryShift.StartedMachines + " order=" + string.Join(",", Array.ConvertAll(FactoryShift.PeekOrder(), x => x.ToString()))
            + " switchCd=" + FactoryShift.SwitchCooldownLeft.ToString("F0"));
        foreach (var c in UnityEngine.Object.FindObjectsOfType<FactoryShiftController>(true))
            sb.AppendLine("controller[" + c.World + "] enabled=" + c.enabled + " inZone=" + c.PlayerInZone + " awake=" + c.WorldAwake);
        foreach (var s in UnityEngine.Object.FindObjectsOfType<MachinePowerSwitch>(true))
            sb.AppendLine("machine[" + MachineIdOf(s) + "] " + s.DisplayName + " started=" + s.Started);
        foreach (var g in UnityEngine.Object.FindObjectsOfType<ShiftGate>(true))
            sb.AppendLine("gate " + g.name + " shouldOpen=" + g.ShouldOpen + " interactable=" + g.enabled);
        sb.AppendLine("extraction: armed=" + ExtractionClock.Armed + " remaining=" + ExtractionClock.Remaining.ToString("F0"));
        return sb.ToString();
    }

    /// <summary>PT：谜题全链——读表 → 错序（火花复位）→ 按序三台 → 锁定 → 时钟冻结。</summary>
    public static string PT_SolvePuzzle()
    {
        var sb = new StringBuilder();
        FactoryShift.ResetAll();
        var root = PlayerRoot();
        var go = root != null ? root.gameObject : null;

        var boards = UnityEngine.Object.FindObjectsOfType<ShiftScheduleBoard>(true);
        if (boards.Length == 0) return "board NOT FOUND（先跑 Stage 搭建）";
        boards[0].Interact(go); // 读表 = 掷序
        var order = FactoryShift.PeekOrder();
        sb.AppendLine("order=" + string.Join(",", Array.ConvertAll(order, x => x.ToString())));

        var switches = UnityEngine.Object.FindObjectsOfType<MachinePowerSwitch>(true);
        if (switches.Length < 3)
        {
            // FactoryNight 常驻但未激活（WorldStreamer 走"玩家走近"裁决）——把玩家传到穿行门旁等激活
            CallPlayer("Teleport", new object[] { new Vector3(-4324f, 1.1f, -72f), Quaternion.identity });
            return "夜场景未激活（机器=" + switches.Length + "）——已传送到穿行门旁，2 秒后再调一次";
        }
        MachinePowerSwitch ById(int id) { return switches.First(s => MachineIdOf(s) == id); }
        var controllers = UnityEngine.Object.FindObjectsOfType<FactoryShiftController>(true);

        // 错序分支：先碰一台非首位的机器
        int wrongId = order.First(x => x != order[0]);
        var wrong = ById(wrongId);
        var wrongCtrl = controllers.First(c => c.World == (MachineWorld(wrong) == 0 ? FactoryShift.Phase.Day : FactoryShift.Phase.Night));
        sb.AppendLine("wrong[" + wrongId + "] worldAwakeFirst=" + wrongCtrl.WorldAwake);
        SetShiftPhase(MachineWorld(wrong) == 0 ? FactoryShift.Phase.Day : FactoryShift.Phase.Night);
        wrong.Interact(go);
        sb.AppendLine("wrong-first → started=" + wrong.Started + "（应 False） progress=" + FactoryShift.StartedMachines + "（应 0）");

        // 正序：把相位设到机器所属世界再启动
        foreach (int id in order)
        {
            var sw = ById(id);
            SetShiftPhase(MachineWorld(sw) == 0 ? FactoryShift.Phase.Day : FactoryShift.Phase.Night);
            sw.Interact(go);
            sb.AppendLine("machine[" + id + "] " + sw.DisplayName + " started=" + sw.Started);
        }
        sb.AppendLine("solved=" + FactoryShift.PuzzleSolved + "（应 True）");

        // 锁定：E 排班表（首次 = 当前班）
        boards[0].Interact(go);
        sb.AppendLine("locked=" + FactoryShift.LockedPhase + "（应非空）");

        // 时钟冻结复查
        var cur = FactoryShift.Current;
        FactoryShift.Tick(9999f);
        sb.AppendLine("frozen=" + (FactoryShift.Current == cur) + "（应 True）");

        // 门复查（锁定后双侧全开）
        foreach (var g in UnityEngine.Object.FindObjectsOfType<ShiftGate>(true))
            sb.AppendLine("gate " + g.name + " shouldOpen=" + g.ShouldOpen + "（应 True）");
        return sb.ToString();
    }

    /// <summary>机器所属世界（0=昼侧 / 2=夜侧——按控制器引用判定，比 id 猜测可靠）。</summary>
    private static int MachineWorld(MachinePowerSwitch s)
    {
        var ctrl = (FactoryShiftController)typeof(MachinePowerSwitch).GetField("controller", All).GetValue(s);
        return ctrl != null && ctrl.World == FactoryShift.Phase.Night ? 2 : 0;
    }

    /// <summary>PT：锁定流转——冷却拦截 + 跳过冷却手动改锁 + 异常平息判定。</summary>
    public static string PT_LockFlow()
    {
        var sb = new StringBuilder();
        if (!FactoryShift.PuzzleSolved) return "先跑 PT_SolvePuzzle";
        var root = PlayerRoot();
        var go = root != null ? root.gameObject : null;
        var boards = UnityEngine.Object.FindObjectsOfType<ShiftScheduleBoard>(true);
        var before = FactoryShift.LockedPhase;

        boards[0].Interact(go); // 冷却中 → 应失败
        sb.AppendLine("cooldown-block=" + (FactoryShift.LockedPhase == before) + "（应 True） cd=" + FactoryShift.SwitchCooldownLeft.ToString("F0"));

        // 跳过冷却再 E → 改锁
        typeof(FactoryShift).GetProperty("NextSwitchAt").GetSetMethod(true).Invoke(null, new object[] { 0f });
        boards[0].Interact(go);
        var after = FactoryShift.LockedPhase;
        sb.AppendLine("switched=" + (after != null && after != before) + "（应 True） locked=" + after);

        // 异常平息判定：锁夜 → AnomaliesActive=False
        sb.AppendLine("anomaliesActive=" + FactoryShift.AnomaliesActive + "（锁夜应 False；锁昼=True 但夜已歇班）");
        return sb.ToString();
    }

    /// <summary>PT：撤离窗口——进厂武装 → 归零锁门 → 回实验室复位解锁。</summary>
    public static string PT_Extraction()
    {
        var sb = new StringBuilder();
        FactoryShift.ResetAll();
        var doorGo = GameObject.Find("anywhere-door-FactoryDay");
        var door = doorGo != null ? doorGo.GetComponentInChildren<AnywhereDoor>(true) : null;
        if (door == null) return "exitDoor NOT FOUND";

        // 玩家进厂（昼侧门区）→ 驱动 → 应武装
        CallPlayer("Teleport", new object[] { new Vector3(-4320f, 1.1f, 2f), Quaternion.identity });
        PT_Drive(3);
        sb.AppendLine("armed=" + ExtractionClock.Armed + "（应 True） remaining=" + ExtractionClock.Remaining.ToString("F0"));

        // 窗口直接静态快进（PT_Drive 的 Update 里 Time.deltaTime 是真实帧长，帧数不足以凑满窗口）
        ExtractionClock.Reset();
        ExtractionClock.Arm(2f);
        ExtractionClock.Tick(2.6f); // 归零 → OnLocked → HandleLocked（zones 已在第一步驱动中缓存）
        var respond = (bool)typeof(AnywhereDoor).GetField("respondToKey", All).GetValue(door);
        sb.AppendLine("locked=" + (ExtractionClock.Armed && ExtractionClock.Remaining <= 0f) + " doorRespond=" + respond + "（应 True / False）");

        // 回实验室 → 驱动 → 复位解锁
        CallPlayer("Teleport", new object[] { new Vector3(4f, 1.1f, 8f), Quaternion.identity });
        PT_Drive(3);
        respond = (bool)typeof(AnywhereDoor).GetField("respondToKey", All).GetValue(door);
        sb.AppendLine("reset=" + (!ExtractionClock.Armed) + " doorRespond=" + respond + "（应 True / True）");
        return sb.ToString();
    }

    // ================= WAV 写出（MetroSceneDev 同款） =================

    private static void WriteAscii(System.IO.BinaryWriter w, string s)
    {
        foreach (var c in s) w.Write((byte)c);
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
