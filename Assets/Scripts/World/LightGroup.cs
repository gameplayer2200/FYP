using System.Collections;
using UnityEngine;

/// <summary>
/// 灯组：一段空间内的一批灯 + 自发光材质，MetroBlackout 按顺序整组熄灭/恢复。
/// 熄灭走"闪烁渐灭"演出，恢复瞬时点亮。
/// 自发光用 MaterialPropertyBlock 改 _EmissionColor（不动共享材质）；
/// 若材质未开启自发光关键字则只表现为灯本体开关，不影响正确性。
/// </summary>
public class LightGroup : MonoBehaviour
{
    [SerializeField] private Light[] lights;
    [Tooltip("灯管/霓虹等自发光 Renderer")]
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private float flickerDuration = 1.2f;
    [SerializeField] private float flickerStep = 0.08f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock block;
    private Color[] emissionOn;
    private bool on = true;
    private Coroutine flickerRoutine;

    /// <summary>目标状态（闪烁演出期间即视为已熄）。</summary>
    public bool IsOn { get { return on; } }

    private void Awake() => BuildCache();

    // 域重载（Play 中重编译脚本）不会重跑 Awake，但会重跑 OnEnable——缓存在这补建
    private void OnEnable() => BuildCache();

    private void BuildCache()
    {
        block = block ?? new MaterialPropertyBlock();
        int count = emissiveRenderers == null ? 0 : emissiveRenderers.Length;
        if (emissionOn != null && emissionOn.Length == count) return;
        emissionOn = new Color[count];
        for (int i = 0; i < count; i++)
        {
            Color c = Color.black;
            var mat = emissiveRenderers[i].sharedMaterial;
            if (mat != null && mat.HasProperty(EmissionColor)) c = mat.GetColor(EmissionColor);
            emissionOn[i] = c;
        }
    }

    /// <summary>整组开关。熄灭可带闪烁演出，恢复始终瞬时（复明是一瞬间的事）。</summary>
    public void SetOn(bool value, bool flicker = true)
    {
        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }
        on = value;
        if (!value && flicker)
        {
            flickerRoutine = StartCoroutine(FlickerOut());
            return;
        }
        ApplyVisual(value);
    }

    private IEnumerator FlickerOut()
    {
        float t = 0f;
        while (t < flickerDuration)
        {
            // 越接近熄灭，亮的比例越低、闪烁间隔越长
            float k = t / flickerDuration;
            ApplyVisual(Random.value > k);
            float step = Mathf.Lerp(flickerStep * 0.5f, flickerStep * 2f, k);
            t += step;
            yield return new WaitForSeconds(step);
        }
        ApplyVisual(false);
        flickerRoutine = null;
    }

    private void ApplyVisual(bool lit)
    {
        if (lights != null)
        {
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].enabled = lit;
        }
        if (emissiveRenderers != null)
        {
            for (int i = 0; i < emissiveRenderers.Length; i++)
            {
                var r = emissiveRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, lit ? emissionOn[i] : Color.black);
                r.SetPropertyBlock(block);
            }
        }
    }
}
