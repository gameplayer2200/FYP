using UnityEngine;

/// <summary>
/// 门边的钥匙孔：手里有钥匙时按 E 开锁，否则给一句提示。
/// 它是「用钥匙」这个动作的落点——玩家不需要对门按 E，而是对这个孔按 E，
/// 这样「门锁着」和「门能开」两种状态在场景里都有实体可指。
/// </summary>
public class FloodedKeyhole : MonoBehaviour, IInteractable
{
    [Tooltip("门锁控制器（拖同一个场景里的 FloodedDoorLock）")]
    [SerializeField] private FloodedDoorLock doorLock;

    [Tooltip("没有钥匙时的提示")]
    [SerializeField] private string needKeyHint = "锁孔里锈得像被焊死了——需要一把钥匙";

    [Tooltip("开锁成功的提示")]
    [SerializeField] private string unlockHint = "钥匙转到底，锁舌退开了——门能走了";

    public float InteractionRange { get { return 1.9f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (doorLock == null)
        {
            HintToast.Show("锁孔连着门，但门锁控制器没接上");
            return;
        }

        if (!doorLock.IsLocked)
        {
            HintToast.Show("门锁已经开着了");
            return;
        }

        if (!FloodedLoopState.HasKey)
        {
            HintToast.Show(needKeyHint, 3f);
            return;
        }

        doorLock.Unlock();
        HintToast.Show(unlockHint, 3.5f);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
