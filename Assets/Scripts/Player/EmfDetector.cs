using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 探索道具·电磁探测器：把异常「扫」出来。
///
/// 它驱动 FYP/ScanMatrix 那套全局量（_Global_Transition_value / _Global_Effect_center），
/// 球面波从玩家身上向外推，把挂了 <see cref="ScanTarget"/> 的物体显形。
/// 那个着色器是「显现」型——波没扫到就完全透明，所以异常物平时是看不见的，必须靠它找。
///
/// 两档（选中探测器格后按 F）：
///   · 短按 —— 近距脉冲：半径小、快、省电，找手边的异常物；
///   · 长按 —— 深度扫描：半径大、慢、费电，还额外把 envRoot 下的环境临时换上显隐材质
///     （envRoot 留空则退化为大号脉冲；环境的原材质在收回阶段还原，不会留下副作用）。
///
/// F 是全项目统一的"使用道具"键：滚轮选中哪一格，F 就归哪一格
/// （见 PlayerGear.UseKeyOwner——手电筒是默认归属，选中探测器后 F 让位给扫描）。
/// </summary>
[DisallowMultipleComponent]
public class EmfDetector : MonoBehaviour
{
    [Header("按键")]
    [SerializeField] private KeyCode scanKey = KeyCode.F;
    [Tooltip("按住超过这个秒数算深度扫描；更短则松手时触发近距脉冲")]
    [SerializeField] private float holdThreshold = 0.6f;

    [Header("近距脉冲（短按）—— 找手边的异常物")]
    [SerializeField] private float shortRadius = 12f;
    [SerializeField] private float shortExpand = 0.85f;
    [SerializeField] private float shortHold = 0.45f;
    [SerializeField] private float shortRetract = 1.2f;
    [Tooltip("一次近距脉冲的耗电")]
    [SerializeField] private float shortDrain = 3f;

    [Header("深度扫描（长按）—— 整间屋子显形")]
    [SerializeField] private float longRadius = 30f;
    [SerializeField] private float longExpand = 2f;
    [SerializeField] private float longHold = 0.9f;
    [SerializeField] private float longRetract = 2.2f;
    [Tooltip("一次深度扫描的耗电")]
    [SerializeField] private float longDrain = 8f;
    [Tooltip("深度扫描时额外扫的环境宿主（留空 = 只扫 ScanTarget 标记的物体）。\n它的渲染器只在扫描期间换成显隐材质，收回阶段还原")]
    [SerializeField] private Transform envRoot;
    [Tooltip("环境换材的渲染器上限，防止大场景一次换几千个")]
    [SerializeField] private int envMaxRenderers = 600;

    [Tooltip("另一个可选目标宿主：它的所有渲染器也算扫描目标（演示/临时用，正式内容请挂 ScanTarget）")]
    [SerializeField] private Transform targetRoot;

    [Header("素材（留空则从 Resources/Scan 自动加载）")]
    [SerializeField] private ComputeShader whiteNoiseGenerator;
    [SerializeField] private Texture fontTexture;
    [SerializeField] private string shaderName = "FYP/ScanMatrix";

    [Header("观感")]
    [Tooltip("显形亮度")]
    [SerializeField] private float intensity = 1.1f;
    [Tooltip("波前的软边宽度（米）")]
    [SerializeField] private float edgeSoftness = 1.5f;
    [SerializeField] private bool colored;
    [SerializeField] private bool showDebugOsd;

    private const int NoiseSize = 512;

    private Material scanMaterial;
    private RenderTexture whiteNoise;
    private Shader scanShader;

    private enum Phase { Idle, Expand, Hold, Retract }
    private Phase phase = Phase.Idle;
    private float phaseTimer;
    private float activeMaxRadius = 12f;
    private float activeExpand = 0.85f;
    private float activeHold = 0.45f;
    private float activeRetract = 1.2f;
    private bool deepScan;

    private bool pressing;
    private bool longFired;
    private float pressTime;

    // 目标物体：记下原材质，销毁/禁用时还原，避免离场后世界一直隐形
    private readonly List<Renderer> targetSwapped = new List<Renderer>();
    private readonly List<Material[]> targetOriginals = new List<Material[]>();

    // 环境临时换材（只在深度扫描期间存在）
    private readonly List<Renderer> envSwapped = new List<Renderer>();
    private readonly List<Material[]> envOriginals = new List<Material[]>();

    // ─────────────────────────────────────────────────────────────
    // 生命周期
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // 组件可能在域重载后才被启用（双玩家守卫会整组禁用玩家）：素材与目标都要重建
        if (scanMaterial == null) return;
        CollectTargets();
    }

    private void Start()
    {
        if (!ResolveAssets()) { enabled = false; return; }
        BuildWhiteNoise();
        BuildMaterial();
        CollectTargets();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyGlobals();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 世界是 Additive 加载的：新世界里的 ScanTarget 要当场接管
        CollectTargets();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EndEnvSwap();
        RestoreTargets();
        if (whiteNoise != null) { whiteNoise.Release(); Destroy(whiteNoise); whiteNoise = null; }
        if (scanMaterial != null) { Destroy(scanMaterial); scanMaterial = null; }
        // 本组件不在了，别让全局量继续把东西按在透明状态
        Shader.SetGlobalFloat("_Global_Transition_value", 1f);
    }

    private void OnDisable()
    {
        phase = Phase.Idle;
        phaseTimer = 0f;
        pressing = false;
        longFired = false;
        EndEnvSwap();
        RestoreTargets();
        Shader.SetGlobalFloat("_Global_Transition_value", 1f);
    }

    // ─────────────────────────────────────────────────────────────
    // 输入与状态机
    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        HandleInput();
        if (phase != Phase.Idle)
        {
            AdvancePhase();
            FeedNoise();
        }
        ApplyGlobals();
    }

    private void HandleInput()
    {
        // F 是共用键：只有当前选中格是探测器时才归它用，
        // 否则按下 F 是手电筒的事，这里必须完全不响应（连状态一起清掉）。
        if (PlayerGear.UseKeyOwner != 2)
        {
            pressing = false;
            longFired = false;
            return;
        }

        if (Input.GetKeyDown(scanKey))
        {
            pressing = true;
            longFired = false;
            pressTime = Time.time;
        }

        if (!pressing) return;

        if (!longFired && Input.GetKey(scanKey) && Time.time - pressTime >= holdThreshold)
        {
            longFired = true;
            StartScan(true);
        }

        if (Input.GetKeyUp(scanKey))
        {
            if (!longFired) StartScan(false);
            pressing = false;
            longFired = false;
        }
    }

    private void AdvancePhase()
    {
        phaseTimer += Time.deltaTime;
        switch (phase)
        {
            case Phase.Expand:
                if (phaseTimer >= activeExpand) { phase = Phase.Hold; phaseTimer = 0f; }
                break;
            case Phase.Hold:
                if (phaseTimer >= activeHold) { phase = Phase.Retract; phaseTimer = 0f; EndEnvSwap(); }
                break;
            case Phase.Retract:
                if (phaseTimer >= activeRetract) { phase = Phase.Idle; phaseTimer = 0f; }
                break;
        }
    }

    /// <summary>这两档要能被别处调用（剧情脚本 / 电闸联动），所以做成 public。</summary>
    public void StartScan(bool deep)
    {
        if (phase != Phase.Idle) return;
        if (scanMaterial == null) return;

        if (!PlayerGear.HasEmfDetector)
        {
            HintToast.Show("没有电磁探测器——实验室出发前记得带上");
            return;
        }

        var battery = BatterySystem.Instance;
        if (battery == null || battery.IsDead)
        {
            HintToast.Show("电量耗尽——电磁探测器开不了机");
            return;
        }

        deepScan = deep;
        activeMaxRadius = deep ? longRadius : shortRadius;
        activeExpand = deep ? longExpand : shortExpand;
        activeHold = deep ? longHold : shortHold;
        activeRetract = deep ? longRetract : shortRetract;

        scanMaterial.SetFloat("_Scan_Radius_Scale", activeMaxRadius);
        scanMaterial.SetFloat("_Scan_Softness", edgeSoftness);
        scanMaterial.SetFloat("_Scan_Intensity", intensity);

        battery.Drain(deep ? longDrain : shortDrain);

        if (deep && envRoot != null) BeginEnvSwap();

        phase = Phase.Expand;
        phaseTimer = 0f;
        HintToast.Show(deep ? "深度扫描——全场线路显形" : "近距脉冲——手边的异常显形");
    }

    private void FeedNoise()
    {
        if (whiteNoiseGenerator == null || whiteNoise == null) return;
        whiteNoiseGenerator.SetInt("_session_rand_seed", Mathf.CeilToInt(Time.time * 6f));
        whiteNoiseGenerator.SetTexture(0, "_white_noise", whiteNoise);
        whiteNoiseGenerator.Dispatch(0, NoiseSize / 8, NoiseSize / 8, 1);
    }

    // ─────────────────────────────────────────────────────────────
    // 全局量
    // ─────────────────────────────────────────────────────────────

    private Vector3 ResolveOrigin()
    {
        if (PlayerRef.Camera != null) return PlayerRef.Camera.transform.position;
        if (PlayerRef.Root != null) return PlayerRef.Root.position;
        return transform.position;
    }

    private void ApplyGlobals()
    {
        if (scanMaterial == null) return;

        float fraction;
        switch (phase)
        {
            case Phase.Expand:
                fraction = activeExpand > 0.0001f ? Mathf.Clamp01(phaseTimer / activeExpand) : 1f;
                break;
            case Phase.Hold:
                fraction = 1f;
                break;
            case Phase.Retract:
                fraction = activeRetract > 0.0001f ? 1f - Mathf.Clamp01(phaseTimer / activeRetract) : 0f;
                break;
            default:
                fraction = 0f;   // 待机＝波半径 0：挂了 ScanTarget 的物体是隐形的，得扫才现
                break;
        }

        Shader.SetGlobalFloat("_Global_Transition_value", fraction);
        Shader.SetGlobalVector("_Global_Effect_center", ResolveOrigin());
        Shader.SetGlobalTexture("global_font_texture", fontTexture);
        Shader.SetGlobalTexture("global_white_noise", whiteNoise);
        Shader.SetGlobalInt("global_colored", colored ? 1 : 0);
    }

    // ─────────────────────────────────────────────────────────────
    // 目标接管
    // ─────────────────────────────────────────────────────────────

    private bool ResolveAssets()
    {
        if (string.IsNullOrEmpty(shaderName)) shaderName = "FYP/ScanMatrix";
        scanShader = Shader.Find(shaderName);
        if (scanShader == null)
        {
            Debug.LogWarning("[EmfDetector] 找不到着色器 " + shaderName + "，扫描不生效", this);
            return false;
        }
        if (whiteNoiseGenerator == null) whiteNoiseGenerator = Resources.Load<ComputeShader>("Scan/GenerateWhiteNoise");
        if (fontTexture == null) fontTexture = Resources.Load<Texture>("Scan/fonts_textures");
        if (whiteNoiseGenerator == null || fontTexture == null)
        {
            Debug.LogWarning("[EmfDetector] 缺少 Resources/Scan 下的 GenerateWhiteNoise / fonts_textures，扫描不生效", this);
            return false;
        }
        return true;
    }

    private void BuildWhiteNoise()
    {
        whiteNoise = new RenderTexture(NoiseSize, NoiseSize, 0);
        whiteNoise.name = "EmfScanNoise";
        whiteNoise.enableRandomWrite = true;
        whiteNoise.useMipMap = false;
        whiteNoise.filterMode = FilterMode.Point;
        whiteNoise.wrapMode = TextureWrapMode.Repeat;
        whiteNoise.Create();
    }

    private void BuildMaterial()
    {
        scanMaterial = new Material(scanShader);
        scanMaterial.name = "EmfScan (Runtime)";
        scanMaterial.hideFlags = HideFlags.DontSave;
        scanMaterial.SetFloat("_Scan_Radius_Scale", shortRadius);
        scanMaterial.SetFloat("_Scan_Softness", edgeSoftness);
        scanMaterial.SetFloat("_Scan_Intensity", intensity);
    }

    private static bool IsDrawable(Renderer r)
    {
        return r is MeshRenderer || r is SkinnedMeshRenderer;
    }

    private static bool BelongsToPlayer(Renderer r)
    {
        var root = PlayerRef.Root;
        return root != null && r.transform.IsChildOf(root);
    }

    /// <summary>
    /// 门的一部分。门洞内板挂的是实时投影屏（Custom/PortalPanelProj，
    /// mainTexture 是对面世界的 RenderTexture），换成扫描材质会让传送门取景失效；
    /// 而且门本身是要走过去的关键物，被扫描材质吞掉就看不见了。
    /// 所以门整体排除在接管范围之外。
    ///
    /// 这里按类型名向上找 PortalWindow 而不是直接写 PortalWindow：
    /// 本组件在 FYP.Player，PortalWindow 在 FYP.World，而依赖方向是 World → Player
    /// （ADR-0004 单向依赖）。为了不反向引用，只能用名字判断。
    /// </summary>
    private static bool IsDoorPart(Renderer r)
    {
        var t = r.transform;
        while (t != null)
        {
            var comps = t.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null && comps[i].GetType().Name == "PortalWindow") return true;
            }
            t = t.parent;
        }
        return false;
    }

    /// <summary>把当前已加载世界里所有 ScanTarget（以及可选的 targetRoot）接管成显隐材质。</summary>
    private void CollectTargets()
    {
        RestoreTargets();

        var seen = new HashSet<Renderer>();

        var marked = UnityEngine.Object.FindObjectsOfType<ScanTarget>(true);
        for (int i = 0; i < marked.Length; i++)
        {
            var r = marked[i].GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < r.Length; j++) seen.Add(r[j]);
        }

        if (targetRoot != null)
        {
            var r = targetRoot.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < r.Length; j++) seen.Add(r[j]);
        }

        foreach (var r in seen)
        {
            if (r == null || !IsDrawable(r) || BelongsToPlayer(r) || IsDoorPart(r)) continue;
            var originals = r.sharedMaterials;
            var swap = new Material[originals.Length];
            for (int k = 0; k < swap.Length; k++) swap[k] = scanMaterial;
            targetSwapped.Add(r);
            targetOriginals.Add(originals);
            r.sharedMaterials = swap;
        }
    }

    private void RestoreTargets()
    {
        for (int i = 0; i < targetSwapped.Count; i++)
        {
            var r = targetSwapped[i];
            if (r != null) r.sharedMaterials = targetOriginals[i];
        }
        targetSwapped.Clear();
        targetOriginals.Clear();
    }

    private void BeginEnvSwap()
    {
        EndEnvSwap();
        if (envRoot == null) return;

        var all = envRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < all.Length && envSwapped.Count < envMaxRenderers; i++)
        {
            var r = all[i];
            if (r == null || !IsDrawable(r) || BelongsToPlayer(r) || IsDoorPart(r)) continue;
            var originals = r.sharedMaterials;
            var swap = new Material[originals.Length];
            for (int k = 0; k < swap.Length; k++) swap[k] = scanMaterial;
            envSwapped.Add(r);
            envOriginals.Add(originals);
            r.sharedMaterials = swap;
        }
    }

    private void EndEnvSwap()
    {
        for (int i = 0; i < envSwapped.Count; i++)
        {
            var r = envSwapped[i];
            if (r != null) r.sharedMaterials = envOriginals[i];
        }
        envSwapped.Clear();
        envOriginals.Clear();
    }

    private void OnGUI()
    {
        if (!showDebugOsd) return;
        string state;
        switch (phase)
        {
            case Phase.Expand: state = "扩散"; break;
            case Phase.Hold: state = "停留"; break;
            case Phase.Retract: state = "收回"; break;
            default: state = "待机"; break;
        }
        GUI.Label(new Rect(12f, 12f, 720f, 22f),
            "[电磁探测器] G 短按=近距脉冲  长按" + holdThreshold.ToString("0.0") + "s=深度扫描   状态=" + state
            + "   目标=" + targetSwapped.Count + "   环境=" + envSwapped.Count);
    }
}
