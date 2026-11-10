using UnityEngine;

/// <summary>
/// 工厂昼夜环境气氛（World）：换班时切换**场景级**的雾与环境光。
///
/// 为什么必须单独有这个组件：RenderSettings 是**场景级全局量**，不是分层量。
/// 昼夜两层叠在同一个 Factory 场景里，共用同一份 fog / ambient——只调灯光的话，
/// 白天进去仍是夜里的黑雾和黑环境光，玩家分不出自己在哪一班。
/// 这是"日夜看不出差别"的根因之一（2026-11 实测：Day 只有 2 盏灯、总强度 0.7，
/// Night 7 盏、总强度 7.7，Directional Light 强度还是 0）。
///
/// 分工：本组件管**环境**（雾色/雾距/环境光三色），灯光强度由场景里的 Light 自身配。
/// 只订阅 FactoryShift 的换班事件，不持有任何玩法状态，随时可摘。
/// </summary>
[DisallowMultipleComponent]
public class FactoryAtmosphere : MonoBehaviour
{
    [Header("昼班（白班）——日光、薄雾、明亮的天空环境光")]
    [Tooltip("环境光三色（Trilight 模式）：天顶 / 地平 / 地面反照")]
    [SerializeField] private Color daySky = new Color(0.42f, 0.46f, 0.52f);
    [SerializeField] private Color dayEquator = new Color(0.30f, 0.32f, 0.35f);
    [SerializeField] private Color dayGround = new Color(0.16f, 0.16f, 0.17f);
    [SerializeField] private Color dayFogColor = new Color(0.55f, 0.57f, 0.60f);
    [Tooltip("指数平方雾的密度（昼：视野舒展）")]
    [SerializeField] private float dayFogDensity = 0.012f;

    [Header("夜班——只有应急灯，浓雾压近")]
    [SerializeField] private Color nightSky = new Color(0.010f, 0.013f, 0.019f);
    [SerializeField] private Color nightEquator = new Color(0.006f, 0.007f, 0.010f);
    [SerializeField] private Color nightGround = new Color(0.003f, 0.003f, 0.004f);
    [SerializeField] private Color nightFogColor = new Color(0.012f, 0.015f, 0.021f);
    [SerializeField] private float nightFogDensity = 0.055f;

    private void Awake() { Apply(FactoryShift.Current); }

    private void OnEnable() { FactoryShift.OnPhaseFlipped += HandleFlip; }

    private void OnDisable() { FactoryShift.OnPhaseFlipped -= HandleFlip; }

    private void HandleFlip() { Apply(FactoryShift.Current); }

    /// <summary>按班次套用整套环境气氛（公开供测试直接驱动）。</summary>
    public void Apply(FactoryShift.Phase phase)
    {
        bool night = phase == FactoryShift.Phase.Night;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = night ? nightSky : daySky;
        RenderSettings.ambientEquatorColor = night ? nightEquator : dayEquator;
        RenderSettings.ambientGroundColor = night ? nightGround : dayGround;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = night ? nightFogColor : dayFogColor;
        RenderSettings.fogDensity = night ? nightFogDensity : dayFogDensity;
    }
}
