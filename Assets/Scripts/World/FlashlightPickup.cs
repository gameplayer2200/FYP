using UnityEngine;

/// <summary>
/// 探索道具·手电筒拾取物：E 拾取 → PlayerGear.AcquireFlashlight + 给玩家挂 Flashlight 组件。
/// 模型：Art/Props/Exploration/Flashlight。
/// </summary>
public class FlashlightPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string acquireHint = "获得手电筒——F 开关。黑暗里它救命，也费电";
    [Tooltip("缓慢自转（已按需求默认关闭：所有拾取物都不做旋转提示）")]
    [SerializeField] private bool spin = false;

    // 拾取范围刻意收紧到"贴近才提示"：2.5m 时离得老远就弹 [E]，实机偏早（2026-10-18）。
    public float InteractionRange { get { return 1.5f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        PlayerGear.AcquireFlashlight();
        if (player != null && player.GetComponentInChildren<Flashlight>(true) == null)
            player.AddComponent<Flashlight>();
        HintToast.Show(acquireHint);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (spin) transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
