using UnityEngine;

/// <summary>
/// 复明后才点亮的灯（尾节车厢内灯等）：初始灭，监听 MetroBlackout.OnPowerRestored。
/// 与灯组的区别：这类灯不属于「渐渐熄灭」序列，而是谜题奖励的一部分。
/// </summary>
public class PoweredLight : MonoBehaviour
{
    [SerializeField] private Light lamp;
    [Tooltip("自发光面片（可选）")]
    [SerializeField] private Renderer glowRenderer;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;
    private Color glowOn = Color.white;

    private void Awake() => BuildCache();

    // 域重载后 Awake 不重跑，OnEnable 会——缓存在这补建（再订阅事件）
    private void OnEnable()
    {
        BuildCache();
        Apply(false); // 初始熄灭；若电已恢复，下面立刻点亮
        MetroBlackout.OnPowerRestored += HandlePowerRestored;
        if (MetroBlackout.PowerRestored) HandlePowerRestored();
    }

    private void BuildCache()
    {
        block = block ?? new MaterialPropertyBlock();
        if (glowRenderer != null && glowOn == default(Color))
        {
            var mat = glowRenderer.sharedMaterial;
            if (mat != null && mat.HasProperty(EmissionColor)) glowOn = mat.GetColor(EmissionColor);
        }
    }

    private void OnDisable()
    {
        MetroBlackout.OnPowerRestored -= HandlePowerRestored;
    }

    private void HandlePowerRestored()
    {
        Apply(true);
    }

    private void Apply(bool lit)
    {
        if (lamp != null) lamp.enabled = lit;
        if (glowRenderer != null)
        {
            glowRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColor, lit ? glowOn : Color.black);
            glowRenderer.SetPropertyBlock(block);
        }
    }
}
