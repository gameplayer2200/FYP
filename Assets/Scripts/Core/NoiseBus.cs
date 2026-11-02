using System;
using UnityEngine;

/// <summary>
/// 世界噪声总线（Core 静态桥）。
///
/// 玩家侧广播「我在什么位置弄出了多大动静」，敌人侧按距离 / 响度决定要不要被吸引过去。
/// 为什么要中转：程序集依赖是单向的（ADR-0004，FYP.World → FYP.Player，反向不可见），
/// 玩家脚本没法定向调用敌人脚本。和 BillingLedger（BatterySystem → BillPanel）、
/// EmfStorm（异常 → HUD）一样，走 FYP.Core 里的静态桥解耦。
///
/// 响度约定（0~1）：静步 0.2 / 走路 0.55 / 奔跑 1.0 / 摔门等重物 0.9~1.0。
/// </summary>
public static class NoiseBus
{
    /// <summary>一次噪声事件。</summary>
    public struct Noise
    {
        public Vector3 Position;
        /// <summary>响度 0~1，敌人用它换算可听半径。</summary>
        public float Loudness;
    }

    /// <summary>有人发出了声音。敌人脚本在 OnEnable 订阅、OnDisable 退订。</summary>
    public static event Action<Noise> OnNoise;

    /// <summary>广播一次噪声。响度 ≤ 0 直接丢弃（避免每帧空转回调）。</summary>
    public static void Emit(Vector3 position, float loudness)
    {
        if (loudness <= 0f) return;

        Action<Noise> handler = OnNoise;
        if (handler == null) return;

        handler(new Noise { Position = position, Loudness = Mathf.Clamp01(loudness) });
    }
}
