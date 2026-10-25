using UnityEngine;

/// <summary>
/// 任务提示板拾取物：E 拾取 → 装备进道具栏（第 4 格）+ 给玩家挂 OrderTerminal 组件。
/// 模型：Art/Props/ai-data-scanner（AI_data_scanner.glb）。
///
/// 它和手电筒/万用表/探测器是同一类东西：拾取只代表"能选中它"，
/// 想真的看订单信息还得滚轮选到第 4 格、按 F 开机（耗电）。
/// 所以这里要 AddComponent，而不是像早先那样只调 AcquireOrderTerminal 就完事。
/// </summary>
public class OrderTerminalPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string acquireHint = "获得任务提示板——滚轮选到它，按 F 查看订单（耗电）";
    [Tooltip("缓慢自转（已按需求默认关闭：所有拾取物都不做旋转提示）")]
    [SerializeField] private bool spin = false;

    // 与其它拾取物一致：贴近才提示（2026-10-18 定的 1.5m）。
    public float InteractionRange { get { return 1.5f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        PlayerGear.AcquireOrderTerminal();

        // 玩家侧组件：F 开关 + 耗电通道。挂根对象，和 Flashlight/EmfDetector 一致。
        if (player != null && player.GetComponent<OrderTerminal>() == null)
            player.AddComponent<OrderTerminal>();

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
