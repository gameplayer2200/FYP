using UnityEngine;

/// <summary>
/// 工厂昼夜的光暗分界（World）：按当前班次改写全局环境光与雾，让昼夜一眼可辨。
///
/// 为什么需要它：昼夜两层合并进同一个场景后，全局环境光是一个共用状态——当前
/// ambientSky 被填成 (2,2,2) 纯白，把两层的明暗差整个抹平了。站在 Day 和站在
/// Night，画面亮度一样，玩家分不清自己换没换层。全局状态无法按层隔离，只能在换班
/// 那一刻改写它。
///
/// 分工：
///  ① 白天：环境光给足、雾淡偏暖、平行光留阴影——看得清，但有灰。
///  ② 夜里：环境光压很低、雾偏冷蓝且更浓、平行光近乎关掉——只剩几盏工作灯。
///     夜里能看清路靠的是 Night 层自己的泵房灯/排班表灯/巡检灯，不是全局环境光。
///
/// 与 FactoryTimeSwap 的分工：那个负责搬人，这个负责改光。两者都听
/// FactoryShift.OnPhaseFlipped，互不依赖；锁定时也要设一次（锁定后不再换班，
/// 但光必须停在锁定那一层的样子）。
/// </summary>
[DisallowMultipleComponent]
public class FactoryLighting : MonoBehaviour
{
    [Header("白天")]
    [SerializeField] private Color dayAmbientSky = new Color(0.62f, 0.66f, 0.72f, 1f);
    [SerializeField] private Color dayAmbientEquator = new Color(0.44f, 0.44f, 0.42f, 1f);
    [SerializeField] private Color dayAmbientGround = new Color(0.22f, 0.20f, 0.17f, 1f);
    [SerializeField] private float dayAmbientIntensity = 1f;
    [SerializeField] private Color dayFogColor = new Color(0.60f, 0.60f, 0.58f, 1f);
    [SerializeField] private float dayFogDensity = 0.0045f;

    [Header("夜晚")]
    [SerializeField] private Color nightAmbientSky = new Color(0.010f, 0.013f, 0.019f, 1f);
    [SerializeField] private Color nightAmbientEquator = new Color(0.006f, 0.007f, 0.010f, 1f);
    [SerializeField] private Color nightAmbientGround = new Color(0.003f, 0.003f, 0.004f, 1f);
    [SerializeField] private float nightAmbientIntensity = 0.25f;
    [SerializeField] private Color nightFogColor = new Color(0.012f, 0.015f, 0.021f, 1f);
    [SerializeField] private float nightFogDensity = 0.055f;

    [Header("平行光（白天的主光；夜里近乎关掉）")]
    [Tooltip("留空 = 运行时自动找场景里的 Directional Light")]
    [SerializeField] private Light sun;
    [SerializeField] private float daySunIntensity = 1.2f;
    [SerializeField] private float nightSunIntensity = 0.0f;
    [SerializeField] private Color nightSunColor = new Color(0.42f, 0.50f, 0.68f, 1f);

    private Color daySunColor = Color.white;
    private bool captured;

    private void Awake()
    {
        ResolveSun();
        CaptureDaySun();
        Apply(FactoryShift.Current);
    }

    private void OnEnable()
    {
        FactoryShift.OnPhaseFlipped += HandleFlip;
        FactoryShift.OnLockChanged += HandleLock;
    }

    private void OnDisable()
    {
        FactoryShift.OnPhaseFlipped -= HandleFlip;
        FactoryShift.OnLockChanged -= HandleLock;
    }

    private void HandleFlip() { Apply(FactoryShift.Current); }

    /// <summary>锁定时也要重设：锁定后 Current 已是锁定班次，光要停在那一层。</summary>
    private void HandleLock() { Apply(FactoryShift.Current); }

    private void ResolveSun()
    {
        if (sun != null) return;
        var scene = gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional) { sun = l; return; }
            }
        }
    }

    /// <summary>记下白天平行光的原始颜色（换到夜里改色，换回来要能还原）。</summary>
    private void CaptureDaySun()
    {
        if (captured || sun == null) return;
        captured = true;
        daySunColor = sun.color;
    }

    /// <summary>把环境光/雾/主光一次性设成某一层的样子。</summary>
    public void Apply(FactoryShift.Phase phase)
    {
        bool night = phase == FactoryShift.Phase.Night;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = night ? nightAmbientSky : dayAmbientSky;
        RenderSettings.ambientEquatorColor = night ? nightAmbientEquator : dayAmbientEquator;
        RenderSettings.ambientGroundColor = night ? nightAmbientGround : dayAmbientGround;
        RenderSettings.ambientIntensity = night ? nightAmbientIntensity : dayAmbientIntensity;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = night ? nightFogColor : dayFogColor;
        RenderSettings.fogDensity = night ? nightFogDensity : dayFogDensity;

        ResolveSun();
        if (sun != null)
        {
            CaptureDaySun();
            sun.intensity = night ? nightSunIntensity : daySunIntensity;
            sun.color = night ? nightSunColor : daySunColor;
        }
    }
}
