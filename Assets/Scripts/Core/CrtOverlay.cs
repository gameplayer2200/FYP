using UnityEngine;

/// <summary>
/// 全屏 CRT 叠层：扫描线 + 显像管暗角 + 极轻微亮度漂移。
/// 自动生成（无需场景摆放），OnGUI 最后绘制（DefaultExecutionOrder 拉高），
/// 让整个 HUD 看起来是"贴在同一块显像管上"，而不是几块浮动面板。
/// 透明度刻意压得很低：可读性优先于特效。
/// </summary>
[DefaultExecutionOrder(32000)]
public class CrtOverlay : MonoBehaviour
{
    private static CrtOverlay instance;

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

        // 扫描线：全屏每 3px 一条，0.06 透明度（肉眼可见但不抢字）
        CrtTheme.Scan(new Rect(0f, 0f, w, h), 0.06f);

        // 显像管暗角 + 缓慢亮度漂移（模拟电源纹波）
        float drift = 0.30f + 0.03f * Mathf.PerlinNoise(Time.unscaledTime * 0.8f, 0.37f);
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, drift);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), CrtTheme.Vignette, ScaleMode.StretchToFill, true);
        GUI.color = prev;
    }
}
