using UnityEngine;

/// <summary>
/// 探索道具·万用表（电力检测仪）：E 拾取后 PlayerGear.HasMultimeter 置位，
/// 配电箱里的线从此可用它检测带电与否。
/// </summary>
public class MultimeterPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string acquireHint = "获得万用表——可检测配电箱里哪根线带电";
    [Tooltip("缓慢自转，提高暗处的可发现性")]
    [SerializeField] private bool spin = true;

    private void Update()
    {
        if (spin) transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
    }

    // 拾取范围刻意收紧到"贴近才提示"：2.5m 时离得老远就弹 [E]，实机偏早（2026-10-18）。
    public float InteractionRange { get { return 1.5f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        PlayerGear.AcquireMultimeter();
        HintToast.Show(acquireHint);
        Destroy(gameObject);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
