using System.Collections;
using UnityEngine;

/// <summary>
/// 开局部署：进游戏时先把"当前世界 + 预载窗口里的邻居世界"全部真正装载完，
/// 再放开玩家。而不是让玩家开局就站在实验室里，边玩边等世界在后台一个个点亮。
///
/// 为什么要这样：世界激活（场景实例化）是整帧级的重活——Metro ≈100ms、
/// OfficePack/Laboratory 这种几千上万物体的更高。之前的做法是"预载 + 走到门前才激活"，
/// 结果每次开门/走路都会顿一下。开局一次性付掉、并且用一块加载屏遮住，
/// 玩家的体验就变成"进游戏等一次"，之后全程流畅。
///
/// 为什么用 RuntimeInitializeOnLoadMethod 而不是挂在场景里：
/// 它必须在**任何**起始场景都能生效（玩家可能从实验室起，也可能直接 Play 某个世界做测试），
/// 挂在场景里的组件只能覆盖它自己那个场景，而且在 Play 中增删场景对象容易漏保存。
///
/// 冻结靠 PlayerInputGate（Core 层中立开关），Loader 只负责"等"和"报告进度"。
/// </summary>
public class StartupDeployment : MonoBehaviour
{
    /// <summary>兜底超时：任何世界加载失败都不能把玩家永久锁死。</summary>
    private const float TimeoutSeconds = 90f;

    /// <summary>布署完成后保持屏幕一小会儿，让"完成"这一帧能被看到，而不是瞬间闪掉。</summary>
    private const float DoneHoldSeconds = 0.45f;

    private float startedAt;
    private float doneAt = -1f;
    private bool finished;
    private bool ready;          // 画面已经显示出来（第一帧别抢 CrtOverlay 的初始化）

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("StartupDeployment");
        DontDestroyOnLoad(go);
        go.AddComponent<StartupDeployment>();
    }

    private void Awake()
    {
        startedAt = Time.unscaledTime;
        PlayerInputGate.Set(true);       // 先冻住：Awake 早于玩家的第一帧输入
        ThresholdLoadScreen.ShowDeployment("开局部署");

        // 字体图集预热：HUD 用动态 CJK 字体，字形首次上屏才光栅化、图集满了还会翻倍重建
        // （实测累计 4000 字时一次重建 142ms）。值班播报正好是"刚进层域弹一批新字"，
        // 表现为「传送到 Metro 时卡一下」。趁加载屏盖着、玩家被冻住时一次付掉。
        // 放在 Awake 而不是 Update：要在第一帧 HUD 绘制之前完成，否则那帧已经先卡了。
        CrtFontPrewarm.Run();
    }

    private void Update()
    {
        if (finished)
        {
            // 结束后停留一小会儿再关屏，然后自毁——不留一个空组件常驻
            if (doneAt >= 0f && Time.unscaledTime - doneAt >= DoneHoldSeconds)
            {
                ThresholdLoadScreen.Hide();
                Destroy(gameObject);
            }
            return;
        }

        // 第一帧 WorldStreamer 还没跑过 Tick，homeWorld / neighborhood 都是空的，
        // 这一帧只报"正在展开预载窗口"，不要据此判断完成。
        if (!WorldStreamer.PreloadingReady)
        {
            ThresholdLoadScreen.SetDeploymentProgress(0.05f, "展开层域预载窗口");
            return;
        }

        var window = WorldStreamer.PreloadWindow;
        int total = 0, done = 0;
        foreach (string world in window)
        {
            total++;
            if (WorldStreamer.IsWorldReady(world)) { done++; continue; }
            // 每帧都请求：forced 在世界就绪后会被清掉，没就绪就继续排队。
            WorldStreamer.RequestActivation(world);
        }

        float progress = total > 0 ? (float)done / total : 1f;
        ThresholdLoadScreen.SetDeploymentProgress(progress * 0.98f,
            done < total
                ? "装载层域 " + done + " / " + total
                : "校准门阈");

        bool timedOut = Time.unscaledTime - startedAt > TimeoutSeconds;
        if (done >= total || timedOut)
        {
            if (timedOut && done < total)
                Debug.LogWarning("[StartupDeployment] 部署超时（" + done + "/" + total + "），先放开玩家，未就绪的世界走到门前再激活。");

            ThresholdLoadScreen.SetDeploymentProgress(1f, "层域就绪");
            PlayerInputGate.Set(false);
            finished = true;
            doneAt = Time.unscaledTime;
            Debug.Log("[StartupDeployment] 开局部署完成：" + done + "/" + total
                      + " 个世界就绪，用时 " + (Time.unscaledTime - startedAt).ToString("F1") + "s");
        }
    }

    private void OnDestroy()
    {
        // 无论怎么销毁都要解锁，否则玩家会被永久冻住
        PlayerInputGate.Set(false);
    }
}
