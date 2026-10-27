using UnityEngine;

/// <summary>
/// 相机震动（Core 静态 + 自动挂载的驱动组件）。
///
/// 用法：任意系统调 <see cref="Impulse"/> 给一次冲击，驱动组件在 <c>LateUpdate</c> 里
/// 把位移叠到玩家相机上。<b>必须放 LateUpdate</b>——<c>PlayerMovement.Update</c> 每帧都会
/// 把相机位置重设为头部骨骼位置，在 Update 里加位移会被同一帧覆盖掉。
///
/// 设计来源：参考工程 §2.2 的 `DimDoorEvent_Earthshake` 与 `ScreenEffectsController.Shake`——
/// 地震事件本质就是一次可叠加的相机抖动。这里做成通用冲击队列，
/// 恐怖事件、机械撞击、坍塌都能复用。
/// </summary>
public static class CameraShake
{
    /// <summary>当前累计振幅（米）。驱动组件每帧读取并按噪声抖动。</summary>
    public static float Amplitude { get; private set; }

    /// <summary>剩余时长。</summary>
    public static float Remaining { get; private set; }

    /// <summary>抖动频率（Hz）。</summary>
    public static float Frequency { get; private set; } = 18f;

    /// <summary>叠一层震动。振幅以米计（0.02 轻微、0.08 明显、0.2 站不稳）。</summary>
    public static void Impulse(float amplitude, float duration, float frequency = 18f)
    {
        if (amplitude <= 0f || duration <= 0f) return;
        // 取较大者而不是相加：连续小震动不该把画面震散
        Amplitude = Mathf.Max(Amplitude, amplitude);
        Remaining = Mathf.Max(Remaining, duration);
        Frequency = Mathf.Max(6f, frequency);
        EnsureDriver();
    }

    /// <summary>清空（重生/传门/回实验室时调，避免落地还在抖）。</summary>
    public static void Clear()
    {
        Amplitude = 0f;
        Remaining = 0f;
    }

    internal static void Consume(float delta)
    {
        if (Remaining <= 0f) return;
        Remaining -= delta;
        if (Remaining <= 0f)
        {
            Remaining = 0f;
            Amplitude = 0f;
        }
    }

    private static void EnsureDriver()
    {
        Camera cam = PlayerRef.Camera;
        if (cam == null) return;
        if (cam.GetComponent<CameraShakeDriver>() == null)
            cam.gameObject.AddComponent<CameraShakeDriver>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Amplitude = 0f;
        Remaining = 0f;
        Frequency = 18f;
    }
}

/// <summary>相机震动的执行者，由 <see cref="CameraShake.Impulse"/> 自动挂到玩家相机上。</summary>
[DefaultExecutionOrder(100)] // 排在 PlayerMovement 之后
public class CameraShakeDriver : MonoBehaviour
{
    private Vector3 appliedOffset;
    private float appliedRoll;

    private void LateUpdate()
    {
        // 先撤销上一帧的偏移，再按当前振幅叠新的——避免误差累积把相机推走
        transform.position -= appliedOffset;
        if (!Mathf.Approximately(appliedRoll, 0f))
        {
            Vector3 e = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(e.x, e.y, e.z - appliedRoll);
        }
        appliedOffset = Vector3.zero;
        appliedRoll = 0f;

        CameraShake.Consume(Time.unscaledDeltaTime);
        float amp = CameraShake.Amplitude;
        if (amp <= 0f) return;

        // 用 Perlin 噪声而不是 Random：噪声连续，看起来像"地面在晃"而不是"画面在乱闪"
        float t = Time.unscaledTime * CameraShake.Frequency;
        float ox = (Mathf.PerlinNoise(t, 0.13f) - 0.5f) * 2f;
        float oy = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f;
        float roll = (Mathf.PerlinNoise(t * 0.7f, t * 0.7f + 4.2f) - 0.5f);

        appliedOffset = new Vector3(ox * amp, oy * amp, 0f);
        appliedRoll = roll * amp * 28f; // 位移越小滚转越明显：0.02m → 0.56°

        transform.position += appliedOffset;
        Vector3 e2 = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(e2.x, e2.y, e2.z + appliedRoll);
    }

    private void OnDisable()
    {
        // 组件被禁用时把偏移还回去，防止相机永久歪着
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
        appliedRoll = 0f;
    }
}
