using UnityEngine;

/// <summary>
/// 手电筒（探索道具）：F 开关。灯锥挂在视角相机下自动跟随；
/// 开灯持续耗电（BatterySystem 独立通道），且照明中不吃黑暗区段加成
/// （MetroBlackout 读 PlayerGear.FlashlightOn）。黑暗里它救命也费电。
/// 组件由 FlashlightPickup 在拾取时挂到玩家根上，无需场景预摆。
///
/// F 是全项目统一的"使用道具"键，一次只触发一件：
/// 只有滚轮选中手电筒格（0）时 F 才开灯（见 PlayerGear.UseKeyOwner）。
/// 选中别的道具时按 F 是那一件的事，这里完全不响应，否则一次按键会同时开灯又扫描。
/// </summary>
public class Flashlight : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F;
    [Tooltip("开灯时每秒额外耗电")]
    [SerializeField] private float drainPerSecond = 0.5f;
    [SerializeField] private float range = 18f;
    [SerializeField] private float spotAngle = 36f;
    [SerializeField] private float intensity = 3.6f;
    [Tooltip("近场补偿点光：低多边形暗材质下保证手电'摸黑也有光'的体感")]
    [SerializeField] private float fillRange = 4.5f;
    [SerializeField] private float fillIntensity = 0.7f;

    private Light spot;
    private Light fill;

    private void Update()
    {
        if (spot == null) TryCreateSpot();
        if (Input.GetKeyDown(toggleKey) && PlayerGear.UseKeyOwner == 0) Toggle();
    }

    private void TryCreateSpot()
    {
        var cam = PlayerRef.Camera;
        if (cam == null) return; // 玩家引用未就绪，下帧再试
        var go = new GameObject("手电锥");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0.14f, -0.14f, 0.1f); // 右下，像手持
        go.transform.localRotation = Quaternion.identity;               // 沿视线
        spot = go.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.intensity = intensity;
        spot.color = new Color(1f, 0.95f, 0.82f);
        // Metro 一类灯多的场景里，普通灯会被排序挤出逐像素名额（只剩顶点光照，
        // 视觉上"手电不亮"）——强制逐像素，手电必须有画面优先级
        spot.renderMode = LightRenderMode.ForcePixel;

        var fillGo = new GameObject("手电近场");
        fillGo.transform.SetParent(cam.transform, false);
        fillGo.transform.localPosition = Vector3.zero;
        fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.range = fillRange;
        fill.intensity = fillIntensity;
        fill.color = new Color(1f, 0.93f, 0.8f);
        fill.renderMode = LightRenderMode.ForcePixel;

        ApplyVisual();
    }

    private void ApplyVisual()
    {
        bool on = PlayerGear.FlashlightOn;
        if (spot != null) spot.enabled = on;
        if (fill != null) fill.enabled = on;
    }

    /// <summary>开关手电筒（拾取后生效；测试可直接调用）。</summary>
    public void Toggle()
    {
        if (!PlayerGear.HasFlashlight)
        {
            HintToast.Show("没有手电筒——实验室出发前记得带上");
            return;
        }
        PlayerGear.SetFlashlight(!PlayerGear.FlashlightOn);
        ApplyState();
    }

    private void ApplyState()
    {
        ApplyVisual();
        if (BatterySystem.Instance != null)
            BatterySystem.Instance.SetFlashlightDrain(PlayerGear.FlashlightOn ? drainPerSecond : 0f);
        HintToast.Show(PlayerGear.FlashlightOn ? "手电筒：开（耗电中）" : "手电筒：关");
    }

    private void OnDisable()
    {
        // 玩家被禁用（双玩家守卫）或组件下线时，停掉耗电通道
        if (BatterySystem.Instance != null && PlayerGear.FlashlightOn)
        {
            BatterySystem.Instance.SetFlashlightDrain(0f);
            PlayerGear.SetFlashlight(false);
        }
    }
}
