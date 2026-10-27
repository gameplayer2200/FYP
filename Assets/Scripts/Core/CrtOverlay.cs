using UnityEngine;

/// <summary>
/// 全屏 CRT 叠层：扫描线 + 显像管暗角 + 极轻微亮度漂移。
/// 自动生成（无需场景摆放），OnGUI 最后绘制（DefaultExecutionOrder 拉高），
/// 让整个 HUD 看起来是"贴在同一块显像管上"，而不是几块浮动面板。
/// 透明度刻意压得很低：可读性优先于特效。
///
/// 张力联动：<see cref="Tension"/> 由 <c>TensionDirector</c> 每帧写入。
/// 张力高时暗角收缩、扫描线加重、边缘泛起一层偏红的晕——
/// "视野在变窄"比任何文字提示都更直接地传达逼近感。
/// </summary>
[DefaultExecutionOrder(32000)]
public class CrtOverlay : MonoBehaviour
{
    private static CrtOverlay instance;

    /// <summary>0~1 的当前恐怖张力，由 TensionDirector 写入。</summary>
    public static float Tension { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (instance != null) return;
        var go = new GameObject("CRT Overlay");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CrtOverlay>();
    }

    private void OnGUI()
    {
        float w = Screen.width;
        float h = Screen.height;

        float t = Mathf.Clamp01(Tension);

        // 扫描线：全屏每 3px 一条，0.06 透明度（肉眼可见但不抢字）；张力高时略重
        CrtTheme.Scan(new Rect(0f, 0f, w, h), 0.06f + t * 0.05f);

        // 显像管暗角 + 缓慢亮度漂移（模拟电源纹波）
        float drift = 0.30f + 0.03f * Mathf.PerlinNoise(Time.unscaledTime * 0.8f, 0.37f);
        // 张力越高暗角越浓：视野被"压"住
        drift = Mathf.Min(1f, drift + t * 0.42f);
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, drift);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), CrtTheme.Vignette, ScaleMode.StretchToFill, true);

        // 高压时叠一层偏红的边缘晕（呼吸式，频率随张力加快）
        if (t > 0.5f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(2.2f, 5.5f, t));
            float a = (t - 0.5f) / 0.5f * Mathf.Lerp(0.10f, 0.26f, pulse);
            GUI.color = new Color(1f, 0.30f, 0.24f, a);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), CrtTheme.Vignette, ScaleMode.StretchToFill, true);
        }

        GUI.color = prev;
    }

    /// <summary>域重载后清场，避免上一局的张力残留到新 Play。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Tension = 0f;
        instance = null;
    }
}
