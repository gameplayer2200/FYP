using UnityEngine;

/// <summary>
/// 探索道具·手电筒拾取物：E 拾取 → PlayerGear.AcquireFlashlight + 给玩家挂 Flashlight 组件。
/// 模型：items/Exploration objects/Flashlight。
/// </summary>
public class FlashlightPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private string acquireHint = "获得手电筒——L 开关。黑暗里它救命，也费电";
    [Tooltip("缓慢自转，提高暗处的可发现性")]
    [SerializeField] private bool spin = true;

    public float InteractionRange { get { return interactionRange; } }

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
