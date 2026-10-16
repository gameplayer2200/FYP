using UnityEngine;

/// <summary>
/// 异常「对面的灯」：对面轨道尽头的灯，玩家一靠近就熄灭，走远又亮起——永远到不了。
/// 无机制、无收益；offDistance/onDistance 留出滞回带，防边界抖动。
/// </summary>
public class UnreachableLightAnomaly : MonoBehaviour
{
    [SerializeField] private Light lamp;
    [Tooltip("自发光面片（灯罩/灯管）")]
    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private float offDistance = 14f;
    [SerializeField] private float onDistance = 22f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock block;
    private Color glowOn = Color.white;

    private void Awake() => BuildCache();
    private void OnEnable() => BuildCache(); // 域重载后 Awake 不重跑，OnEnable 会

    private void BuildCache()
    {
        block = block ?? new MaterialPropertyBlock();
        if (glowRenderer != null && glowOn == default(Color))
        {
            var mat = glowRenderer.sharedMaterial;
            if (mat != null && mat.HasProperty(EmissionColor)) glowOn = mat.GetColor(EmissionColor);
        }
    }

    private void Update()
    {
        var player = PlayerRef.Root;
        if (player == null) return;
        float d = Vector3.Distance(player.position, transform.position);
        if (d <= offDistance) Apply(false);
        else if (d >= onDistance) Apply(true);
    }

    private void Apply(bool lit)
    {
        if (lamp != null && lamp.enabled != lit) lamp.enabled = lit;
        if (glowRenderer != null)
        {
            glowRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColor, lit ? glowOn : Color.black);
            glowRenderer.SetPropertyBlock(block);
        }
    }
}
