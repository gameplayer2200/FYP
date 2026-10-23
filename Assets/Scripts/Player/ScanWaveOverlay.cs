using UnityEngine;

/// <summary>
/// 探测器·场地数字波：把「周围场地被数字化扫过去」叠到画面上（全屏后处理）。
///
/// 为什么需要它：<see cref="EmfDetector"/> 原本只替换挂了 <see cref="ScanTarget"/> 的物体材质，
/// 于是扫描时只有异常物显形，玩家脚下的地板、四周的墙毫无反应——探测器像是"扫描空气"。
/// 本组件补上环境那一路：读深度还原世界坐标，只让球面壳附近的像素变成字符雨，
/// 地板上就是一圈扩散的环，墙面上是一道扫过去的弧。
///
/// 与 FYP/ScanMatrix 共用同一套全局量（球心 + 半径），所以物显形与场地扫过是同一个波前。
///
/// 挂在玩家相机上（EmfDetector 自动挂载）。没有材质/不支持时静默禁用，不影响其它系统。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ScanWaveOverlay : MonoBehaviour
{
    [Header("素材（留空则从 Resources/Scan 自动加载）")]
    [SerializeField] private ComputeShader whiteNoiseGenerator;
    [SerializeField] private Texture fontTexture;
    [SerializeField] private string shaderName = "FYP/ScanWaveOverlay";

    [Header("观感")]
    [Tooltip("字符雨颜色。默认取 CRT 磷光绿，与 HUD 同一个调子")]
    [SerializeField] private Color waveColor = new Color(0.47f, 1f, 0.76f, 1f);

    [Tooltip("亮度倍率")]
    [SerializeField] private float intensity = 1.35f;

    [Tooltip("波前厚度（米）——越大扫过的那一条越宽")]
    [SerializeField] private float band = 0.9f;

    [Tooltip("波扫过之后内侧残留的数字化强度。太高会整屏变绿、看不出波前在推进")]
    [SerializeField] private float innerTrail = 0.07f;

    [Tooltip("字符大小系数。越大字越大、行数越少")]
    [SerializeField] private float charScale = 0.75f;

    private const int NoiseSize = 512;

    private Camera cam;
    private Material mat;
    private ComputeShader noiseCs;
    private RenderTexture whiteNoise;
    private bool ready;

    // 由 EmfDetector 每帧推入
    private bool  waveActive;
    private Vector3 waveOrigin;
    private float waveRadius;
    private float waveBandScale = 1f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>由 EmfDetector 在 Start 里调用一次；失败返回 false，调用方据此降级。</summary>
    public bool Setup()
    {
        if (ready) return true;

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning("[ScanWaveOverlay] 找不到着色器 " + shaderName + "，场地数字波不生效（物体显形不受影响）", this);
            return false;
        }

        if (noiseCs == null) noiseCs = Resources.Load<ComputeShader>("Scan/GenerateWhiteNoise");
        if (fontTexture == null) fontTexture = Resources.Load<Texture>("Scan/fonts_textures");
        if (noiseCs == null || fontTexture == null)
        {
            Debug.LogWarning("[ScanWaveOverlay] 缺少 Resources/Scan 下的 GenerateWhiteNoise / fonts_textures", this);
            return false;
        }

        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return false;

        // 反投影要读场景深度，不声明的话 _CameraDepthTexture 永远是 1（远处）。
        // 这里常开而不是扫描时才开：切换 DepthTextureMode 会让 Unity 重建深度缓冲，
        // 每次按 F 都卡一下比一直多渲一张深度图更难受。本组件是拾取探测器后才挂上的，
        // 没带探测器的流程不受影响。
        cam.depthTextureMode |= DepthTextureMode.Depth;

        whiteNoise = new RenderTexture(NoiseSize, NoiseSize, 0)
        {
            name = "ScanWaveNoise",
            enableRandomWrite = true,
            useMipMap = false,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        whiteNoise.Create();
        noiseCs.SetTexture(0, "_white_noise", whiteNoise);

        mat = new Material(shader)
        {
            name = "ScanWaveOverlay (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        mat.SetTexture("_MainTex", Texture2D.whiteTexture);

        ready = true;
        enabled = false;   // 待机不跑 OnRenderImage
        return true;
    }

    /// <summary>推进波前。radius 是世界单位半径；bandScale 用来在深度扫描时放宽波前。</summary>
    public void Push(Vector3 origin, float radius, float bandScale)
    {
        waveActive = radius > 0.001f;
        waveOrigin = origin;
        waveRadius = radius;
        waveBandScale = Mathf.Max(0.05f, bandScale);

        // 只有波在推的时候才启用后处理，省掉待机时的整屏 Blit
        if (enabled != waveActive && ready) enabled = waveActive;
    }

    /// <summary>立刻收工（扫描结束 / 组件拆除）。</summary>
    public void Clear()
    {
        waveActive = false;
        if (ready) enabled = false;
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!ready || mat == null || whiteNoise == null || !waveActive || cam == null)
        {
            Graphics.Blit(src, dst);
            return;
        }

        FeedNoise();
        UploadUniforms();

        Graphics.Blit(src, dst, mat);
    }

    private void FeedNoise()
    {
        noiseCs.SetInt("_session_rand_seed", Mathf.CeilToInt(Time.time * 6f));
        noiseCs.SetTexture(0, "_white_noise", whiteNoise);
        noiseCs.Dispatch(0, NoiseSize / 8, NoiseSize / 8, 1);
    }

    private void UploadUniforms()
    {
        // 视锥四角的射线，已归一到「沿相机前方距离 = 1」。
        // 这样 世界点 = 相机位置 + 射线 * LinearEyeDepth（视空间 z 深度）直接成立。
        //
        // ⚠️ 别用 localToWorldMatrix 直接乘 (x, y, near)：Unity 相机朝本地 -Z，
        // 传正的 z 会得到指向相机背后的射线，反投距离全乱（曾导致波前跑到三倍远）。
        float tanH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanW = tanH * cam.aspect;

        Transform t = cam.transform;
        Vector3 f = t.forward, r = t.right, u = t.up;

        Vector3 bl = f - r * tanW - u * tanH;
        Vector3 br = f + r * tanW - u * tanH;
        Vector3 tl = f - r * tanW + u * tanH;
        Vector3 tr = f + r * tanW + u * tanH;

        mat.SetVector("_CamPos", t.position);
        mat.SetVector("_RayBL", bl);
        mat.SetVector("_RayBR", br);
        mat.SetVector("_RayTL", tl);
        mat.SetVector("_RayTR", tr);

        mat.SetVector("_WaveOrigin", waveOrigin);
        mat.SetFloat("_WaveRadius", waveRadius);
        mat.SetFloat("_WaveBand", band * waveBandScale);
        mat.SetFloat("_WaveInner", Mathf.Clamp01(innerTrail));
        mat.SetFloat("_WaveIntensity", intensity);
        mat.SetFloat("_WaveCharScale", Mathf.Max(0.05f, charScale));
        mat.SetVector("_WaveColor", waveColor);

        Shader.SetGlobalTexture("global_font_texture", fontTexture);
        Shader.SetGlobalTexture("global_white_noise", whiteNoise);
    }

    private void OnDestroy()
    {
        if (whiteNoise != null) { whiteNoise.Release(); Destroy(whiteNoise); whiteNoise = null; }
        if (mat != null) { Destroy(mat); mat = null; }
        if (cam != null) cam.depthTextureMode &= ~DepthTextureMode.Depth;
    }
}
