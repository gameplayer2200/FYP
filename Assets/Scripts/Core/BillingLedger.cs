using UnityEngine;

/// <summary>
/// 电费账单记账（Core：BatterySystem 写，Content 的账单演出读——分层合法，同 EmfStorm/ExtractionClock 模式）。
/// 本局累计，瘫倒不清零；Reset 供未来"新一班"重开时用。
/// </summary>
public static class BillingLedger
{
    /// <summary>照明：手电通道累计（电）。</summary>
    public static float Lighting { get; private set; }

    /// <summary>环境：渐熄/黑暗等世界规则累计（电）。</summary>
    public static float Ambient { get; private set; }

    /// <summary>机动：超级跳等主动技能累计（电）。</summary>
    public static float Device { get; private set; }

    /// <summary>受创累计（电）。</summary>
    public static float Hit { get; private set; }

    /// <summary>满电量（BatterySystem 启动时上报，算百分比用）。</summary>
    public static float Capacity { get; private set; }

    public static float Total
    {
        get { return Lighting + Ambient + Device + Hit; }
    }

    public static void AddLighting(float v) { Lighting += Mathf.Max(0f, v); }
    public static void AddAmbient(float v) { Ambient += Mathf.Max(0f, v); }
    public static void AddDevice(float v) { Device += Mathf.Max(0f, v); }
    public static void AddHit(float v) { Hit += Mathf.Max(0f, v); }

    /// <summary>由 BatterySystem 在 Start 上报。</summary>
    public static void SetCapacity(float capacity) { Capacity = Mathf.Max(1f, capacity); }

    /// <summary>清账（瘫倒不清——账单是整局累计；留给"新一班"用）。</summary>
    public static void Reset()
    {
        Lighting = Ambient = Device = Hit = 0f;
    }
}
