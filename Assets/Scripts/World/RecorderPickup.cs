using UnityEngine;

/// <summary>
/// 探索道具·录音笔拾取物：E 拾取（取走后整件失活，与 ShiftWatchPickup 同构——
/// 录音笔是「领仪器」不是可丢下的物理件，不进 HeldItem 体系）。
/// 模型由 Stage7 程序拼装（小机身 + 指示灯），摆 Metro 站厅检票亭。
/// </summary>
public class RecorderPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string takenHint = "获得录音笔——滚轮选中后按 F 放在异常旁，录完 E 收回，回实验室按 F 播放";

    public float InteractionRange { get { return 1.5f; } }
    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        PlayerGear.AcquireRecorder();
        if (player != null && player.GetComponentInChildren<VoiceRecorder>(true) == null)
            player.AddComponent<VoiceRecorder>();
        HintToast.Show(takenHint);
        Vanish();
    }

    /// <summary>取走：注销交互并失活整个物体（同步、立刻从准星与提示里消失）。</summary>
    private void Vanish()
    {
        InteractionRegistry.Unregister(this);
        gameObject.SetActive(false);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
