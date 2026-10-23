using UnityEngine;

/// <summary>
/// 屏幕中心准心。自动生成（无需场景摆放），OnGUI 直绘，沿用 CrtTheme 的磷光绿。
///
/// 默认常显——第一人称的基本读数，也让"朝哪儿扫"有个参照；
/// 当随身带着电磁探测器且滚轮选中它时整体亮一档，等于告诉玩家
/// "现在按 F 就是往准心方向扫"。
///
/// 可读性优先：中心留空（不糊住屏幕正中要看的那个像素）、
/// 亮线下面垫一层深色描边（浅色地板上也能看清）、线宽取整防发虚。
/// </summary>
[DefaultExecutionOrder(31500)]
public class Crosshair : MonoBehaviour
{
    private static Crosshair instance;

    [Header("形状（720p 基准，随屏幕等比放大）")]
    [Tooltip("中心留空半径：准心不能挡住屏幕正中要看的那个像素")]
    [SerializeField] private float gap = 6f;

    [Tooltip("四条刻度的长度")]
    [SerializeField] private float length = 11f;

    [Tooltip("线宽（会取整成整数像素，避免发虚）")]
    [SerializeField] private float thickness = 2f;

    [Tooltip("正中小点的边长，0 = 不画")]
    [SerializeField] private float dot = 3f;

    [Header("颜色")]
    [Tooltip("平时的不透明度")]
    [SerializeField] private float alpha = 0.80f;

    [Tooltip("选中电磁探测器时的不透明度（亮一档）")]
    [SerializeField] private float readyAlpha = 1f;

    [Tooltip("亮线下面垫的深色描边，浅色场景靠它把准心从背景里拉出来。0 = 不描边")]
    [SerializeField] private float outlineAlpha = 0.55f;

    [Tooltip("常显。关掉后只在选中电磁探测器时出现")]
    [SerializeField] private bool alwaysOn = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (instance != null) return;

        // 场景里已经手动摆了一个就用它（方便在 Inspector 里调手感）
        var found = FindAnyObjectByType<Crosshair>();
        if (found != null)
        {
            instance = found;
            return;
        }

        var go = new GameObject("Crosshair");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Crosshair>();
    }

    private void OnGUI()
    {
        // 探测器在手 + 选中探测器格（2）= 准备扫描
        bool ready = PlayerGear.HasEmfDetector && PlayerGear.SelectedIndex == 2;
        if (!alwaysOn && !ready) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        float g = CrtTheme.Px(gap);
        float l = Mathf.Max(2f, CrtTheme.Px(length));
        float t = Mathf.Max(2f, Mathf.Round(CrtTheme.Scale * thickness));
        float d = Mathf.Max(0f, CrtTheme.Px(dot));

        // 描边比亮线粗一圈，压在下面
        if (outlineAlpha > 0.005f)
        {
            float ot = t + 2f;
            Bar(cx, cy, g - 1f, l + 1f, ot, new Color(0f, 0f, 0f, outlineAlpha));
            if (d >= 1f) Dot(cx, cy, d + 2f, new Color(0f, 0f, 0f, outlineAlpha));
        }

        float a = ready ? readyAlpha : alpha;
        Color c = ready
            ? new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, a)
            : new Color(CrtTheme.InkDim.r, CrtTheme.InkDim.g, CrtTheme.InkDim.b, a);

        Bar(cx, cy, g, l, t, c);
        if (d >= 1f) Dot(cx, cy, d, c);
    }

    private static void Bar(float cx, float cy, float gap, float len, float thick, Color c)
    {
        CrtTheme.Fill(new Rect(cx - thick * 0.5f, cy - gap - len, thick, len), c); // 上
        CrtTheme.Fill(new Rect(cx - thick * 0.5f, cy + gap, thick, len), c);       // 下
        CrtTheme.Fill(new Rect(cx - gap - len, cy - thick * 0.5f, len, thick), c); // 左
        CrtTheme.Fill(new Rect(cx + gap, cy - thick * 0.5f, len, thick), c);       // 右
    }

    private static void Dot(float cx, float cy, float size, Color c)
    {
        CrtTheme.Fill(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), c);
    }
}
