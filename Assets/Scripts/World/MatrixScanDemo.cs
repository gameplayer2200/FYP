using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 扫描特效演示：把 MatrixVFX 的三平面矩阵雨做成「从一点向外扩散的球面扫描波」。
///
/// 原理：专用着色器 FYP/ScanMatrix 里，可视半径 = _Global_Transition_value × _Scan_Radius_Scale，
/// 球心 = _Global_Effect_center。本组件把「想要的半径」换算成 transition 下发，
/// 同一帧里改球心就是一颗球从球心向外扫。
///
/// 为什么不用原版 Unlit/TriPlanarMatrix：原版最后一步有 split_from_midle（屏幕空间竖向裁切），
/// 那是「从左到右擦屏」的转场效果，会让显形区域变成屏幕中间一条竖带，不是球形扫描。
/// FYP/ScanMatrix 去掉了那一行，并给球形裁切加了软边。
///
/// 依赖本地素材（许可受限，见 Docs/ASSET-SOURCING.md §7.5）：
///   Assets/Art/VFX/MatrixScan/Shaders/Resources/GenerateWhiteNoise.compute
///   Assets/Art/VFX/MatrixScan/Texture/fonts_textures.png
/// 素材缺失时只打一条警告并自我禁用，不影响场景其它内容。
///
/// 注意：这个着色器是「显现」型——材质上的物体在扫描波到达前是完全透明的，
/// 波扫过才显出矩阵雨。想要「物体本来就在、扫描时叠加高亮」需要另写一版着色器。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("FYP/扫描特效演示")]
public class MatrixScanDemo : MonoBehaviour
{
    [Header("素材（留空则自动查找）")]
    [SerializeField] private ComputeShader whiteNoiseGenerator;
    [SerializeField] private Texture fontTexture;
    [SerializeField] private string shaderName = "FYP/ScanMatrix";

    [Header("作用对象（留空则用本物体下全部子物体）")]
    [SerializeField] private Transform targetRoot;

    [Header("扫描参数")]
    [SerializeField] private KeyCode triggerKey = KeyCode.G;
    [SerializeField] private bool autoLoop = false;
    [Tooltip("扫描波最大半径（米）")]
    [SerializeField] private float maxRadius = 30f;
    [Tooltip("从球心扩散到最大半径用多久（秒）")]
    [SerializeField] private float expandTime = 1.2f;
    [Tooltip("到达最大半径后停留多久（秒）")]
    [SerializeField] private float holdTime = 0.35f;
    [Tooltip("从最大半径收回用多久（秒）")]
    [SerializeField] private float retractTime = 1.6f;

    [Header("外观")]
    [Tooltip("彩色矩阵雨（每个字符随机色相），关掉是经典绿")]
    [SerializeField] private bool colored = false;
    [Tooltip("亮度倍率")]
    [SerializeField] private float intensity = 1f;
    [Tooltip("波前软边宽度（米），越大过渡越柔和")]
    [SerializeField] private float edgeSoftness = 1.5f;

    [Header("扫描原点")]
    [Tooltip("留空则用玩家相机；再没有就用本物体位置")]
    [SerializeField] private Transform originOverride;
    [SerializeField] private bool followOrigin = true;

    private const int NoiseSize = 512;

    private RenderTexture whiteNoise;
    private Material scanMaterial;
    private readonly List<Renderer> probes = new List<Renderer>();
    private float radius;
    private float phaseTimer;
    private int phase; // 0=待机 1=扩散 2=停留 3=收回
    private bool ready;

    private void Start()
    {
        if (!ResolveAssets())
        {
            enabled = false;
            return;
        }

        BuildWhiteNoise();
        BuildMaterial();
        CollectProbes();

        ApplyGlobals(0f);
        ready = true;
    }

    private bool ResolveAssets()
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning("[MatrixScanDemo] 找不到着色器 " + shaderName +
                             "，扫描特效已停用。素材路径见 Docs/ASSET-SOURCING.md §7.5。", this);
            return false;
        }

        if (fontTexture == null)
        {
            Debug.LogWarning("[MatrixScanDemo] 未指定字体图集 fonts_textures.png，扫描特效已停用。" +
                             "可在 Inspector 里手动拖入。", this);
            return false;
        }

        if (whiteNoiseGenerator == null)
        {
            Debug.LogWarning("[MatrixScanDemo] 未指定 GenerateWhiteNoise.compute，扫描特效已停用。" +
                             "可在 Inspector 里手动拖入。", this);
            return false;
        }

        return true;
    }

    private void BuildWhiteNoise()
    {
        whiteNoise = new RenderTexture(NoiseSize, NoiseSize, 0)
        {
            name = "MatrixScanWhiteNoise",
            enableRandomWrite = true,
            useMipMap = false,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        whiteNoise.Create();
        whiteNoiseGenerator.SetTexture(0, "_white_noise", whiteNoise);
    }

    private void BuildMaterial()
    {
        Shader shader = Shader.Find(shaderName);
        scanMaterial = new Material(shader)
        {
            name = "MatrixScanDemo (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        scanMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        scanMaterial.SetFloat("_Scan_Radius_Scale", Mathf.Max(1f, maxRadius));
        scanMaterial.SetFloat("_Scan_Softness", Mathf.Max(0.01f, edgeSoftness));
        scanMaterial.SetFloat("_Scan_Intensity", Mathf.Max(0f, intensity));
    }

    private void CollectProbes()
    {
        probes.Clear();
        Transform root = targetRoot != null ? targetRoot : transform;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = scanMaterial;
            probes.Add(r);
        }

        if (probes.Count == 0)
            Debug.LogWarning("[MatrixScanDemo] 作用范围内没有 Renderer，扫描波扫不出东西。", this);
    }

    private void Update()
    {
        if (!ready) return;

        if (Input.GetKeyDown(triggerKey)) StartScan();
        if (autoLoop && phase == 0) StartScan();

        AdvancePhase();
        FeedNoise();
        ApplyGlobals(radius / Mathf.Max(1f, maxRadius));
    }

    /// <summary>开始一次扫描（外部也可调用）。</summary>
    public void StartScan()
    {
        if (phase != 0) return;
        radius = 0f;
        phaseTimer = 0f;
        phase = 1;
    }

    private void AdvancePhase()
    {
        float dt = Time.deltaTime;

        switch (phase)
        {
            case 1:
                phaseTimer += dt;
                radius = maxRadius * Mathf.Clamp01(expandTime <= 0f ? 1f : phaseTimer / expandTime);
                if (phaseTimer >= expandTime)
                {
                    radius = maxRadius;
                    phase = 2;
                    phaseTimer = 0f;
                }
                break;

            case 2:
                phaseTimer += dt;
                if (phaseTimer >= holdTime)
                {
                    phase = 3;
                    phaseTimer = 0f;
                }
                break;

            case 3:
                phaseTimer += dt;
                radius = maxRadius * (1f - Mathf.Clamp01(retractTime <= 0f ? 1f : phaseTimer / retractTime));
                if (phaseTimer >= retractTime)
                {
                    radius = 0f;
                    phase = 0;
                    phaseTimer = 0f;
                }
                break;
        }
    }

    private void FeedNoise()
    {
        int seed = Mathf.CeilToInt(Time.time * 6f);
        whiteNoiseGenerator.SetInt("_session_rand_seed", seed);
        whiteNoiseGenerator.Dispatch(0, NoiseSize / 8, NoiseSize / 8, 1);
    }

    private void ApplyGlobals(float transition)
    {
        Vector3 center = followOrigin ? ResolveOrigin() : transform.position;

        Shader.SetGlobalTexture("global_font_texture", fontTexture);
        Shader.SetGlobalTexture("global_white_noise", whiteNoise);
        Shader.SetGlobalInt("global_colored", colored ? 1 : 0);
        Shader.SetGlobalInt("_session_rand_seed", Mathf.CeilToInt(Time.time * 6f));
        Shader.SetGlobalFloat("_Global_Transition_value", Mathf.Clamp01(transition));
        Shader.SetGlobalVector("_Global_Effect_center", center);
    }

    private Vector3 ResolveOrigin()
    {
        if (originOverride != null) return originOverride.position;
        if (PlayerRef.Camera != null) return PlayerRef.Camera.transform.position;
        if (Camera.main != null) return Camera.main.transform.position;
        return transform.position;
    }

    private void OnDestroy()
    {
        if (whiteNoise != null)
        {
            whiteNoise.Release();
            Destroy(whiteNoise);
            whiteNoise = null;
        }

        if (scanMaterial != null)
        {
            Destroy(scanMaterial);
            scanMaterial = null;
        }

        // 收尾时把全局量复位到「全部可见」，避免影响场景里其它用到同一批全局量的材质
        Shader.SetGlobalFloat("_Global_Transition_value", 1f);
    }

    private void OnGUI()
    {
        if (!ready) return;
        string state = phase == 0 ? "待机" : phase == 1 ? "扩散" : phase == 2 ? "停留" : "收回";
        GUI.Label(new Rect(12f, 12f, 480f, 60f),
            "[扫描特效] 按 " + triggerKey + " 触发   状态=" + state +
            "   半径=" + radius.ToString("0.0") + "m / " + maxRadius.ToString("0.0") + "m" +
            (autoLoop ? "   自动循环=开" : ""));
    }
}
