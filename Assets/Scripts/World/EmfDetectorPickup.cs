using UnityEngine;

/// <summary>
/// 探索道具·电磁探测器拾取物：E 拾取 → PlayerGear.AcquireEmfDetector + 给玩家挂 EmfDetector 组件。
/// 模型：Art/Props/Exploration/EMF_Detector。
/// </summary>
public class EmfDetectorPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string acquireHint = "获得电磁探测器——滚轮选到它，短按 F 扫近处，按住 F 扫全场。它也吃电";
    [Tooltip("缓慢自转，提高暗处的可发现性")]
    [SerializeField] private bool spin = true;

    // 与手电筒拾取物一致：贴近才提示，隔得老远就弹 [E] 实机偏早（2026-10-18 定的 1.5m）
    public float InteractionRange { get { return 1.5f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        PlayerGear.AcquireEmfDetector();
        if (player != null && player.GetComponentInChildren<EmfDetector>(true) == null)
            player.AddComponent<EmfDetector>();
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
