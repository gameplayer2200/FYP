using UnityEngine;

/// <summary>一切可交互物的统一接口：E 键由 InteractionRunner 单点分发，杜绝多脚本抢键。</summary>
public interface IInteractable
{
    /// <summary>交互点位置（用于测距选最近目标）。</summary>
    Transform GetInteractionPoint();

    /// <summary>有效交互距离（不同物可不同）。</summary>
    float InteractionRange { get; }

    /// <summary>玩家按下 E 时被调用（只会有一个目标收到）。</summary>
    void Interact(GameObject player);
}
