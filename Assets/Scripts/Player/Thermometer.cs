using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 温度计（探索道具，提案表 3）：被动仪表——滚轮选中即显示读数，靠近活跃异常源时
/// 读数沿距离线性骤降，贴身可降到负值。「数字变负的那一刻」就是它的全部机制：
/// 没有按钮、不耗电，占用道具格本身就是代价（选中它就看不了别的仪表）。
///
/// 异常源走 Core 的 AnomalyPresence（只在异常「正在发生」时读得到，与玩家直觉一致）。
/// 组件由 HeldItem 在拾取时挂到玩家根上。
/// </summary>
public class Thermometer : MonoBehaviour
{
    [Tooltip("环境基准温度（℃）")]
    [SerializeField] private float baseTemperature = 26f;
    [Tooltip("异常源对读数的影响半径（米）")]
    [SerializeField] private float influenceRadius = 16f;
    [Tooltip("贴身（距离归零）时的最大降温（℃）——base - drop 即最近可达的负值深度")]
    [SerializeField] private float maxDrop = 32.5f;
    [Tooltip("读数刷新间隔（秒）——数字跳太快像刷新率，不像温度")]
    [SerializeField] private float refreshInterval = 0.35f;

    private static readonly List<AnomalyPresence.Reading> readings = new List<AnomalyPresence.Reading>();
    private float shown = 26f;
    private float nextRefresh;

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        SampleNow();
    }

    /// <summary>立即重采样一次读数并返回（Update 按节流调用；测试可直接同步取值）。</summary>
    public float SampleNow()
    {
        // 读数 = 基准 - 最近活跃异常源的距离衰减降温（多源取最强，不叠加）
        shown = baseTemperature;
        AnomalyPresence.CollectActive(readings);
        var player = PlayerRef.Root;
        if (player != null && readings.Count > 0)
        {
            for (int i = 0; i < readings.Count; i++)
            {
                float d = Vector3.Distance(player.position, readings[i].Position);
                if (d >= influenceRadius) continue;
                float t = baseTemperature - maxDrop * (1f - d / influenceRadius);
                if (t < shown) shown = t;
            }
        }
        return shown;
    }

    private void OnGUI()
    {
        if (PlayerGear.SelectedIndex != PlayerGear.ThermometerSlot || !PlayerGear.HasThermometer) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // 准星正下方的小读数条：谁抬头看温度谁找不到——它属于脚下这个世界
        float w = CrtTheme.Px(150f), h = CrtTheme.Px(22f);
        var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f + CrtTheme.Px(34f), w, h);

        bool freezing = shown < 0f;
        bool cold = shown < 10f;
        Color c = freezing ? CrtTheme.Danger : (cold ? CrtTheme.Warn : CrtTheme.InkDim);

        // 过冷时读数末位抖动（±0.3 内）——数字自己也在发抖
        float v = shown;
        if (freezing) v += Random.Range(-0.3f, 0.3f);
        string text = v.ToString("F1") + " ℃";
        if (EmfStorm.IsStorming) text = EmfStorm.Scramble(text);

        var style = CrtTheme.Style(15, c, false, true, TextAnchor.MiddleCenter);
        var shadow = new Rect(r.x + CrtTheme.Px(1.5f), r.y + CrtTheme.Px(1.5f), r.width, r.height);
        GUI.Label(shadow, text, CrtTheme.Style(15, new Color(0f, 0f, 0f, 0.55f), false, true, TextAnchor.MiddleCenter));
        GUI.Label(r, text, style);
    }
}
