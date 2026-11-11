using UnityEngine;

/// <summary>
/// 值班室落地钟（换班钟）：E 取走表芯。
///
/// 设计（2026-11 定）：这口钟**不是**开关，是怀表的来源。玩家第一次按 E，
/// 表芯被取走、怀表进背包、钟面熄灭、整口钟从场景里消失（不再可交互）——
/// 之后换班一律靠怀表（滚轮切到怀表格按 F），钟留在原地只会让人以为还能按。
///
/// 为什么用 SetActive(false) 而不是 Destroy：
/// 交互中枢每帧扫注册表，销毁一个正在被它持有的注册项要等 OnDestroy 那一帧，
/// 中间会出现"按了 E 没反应但准星还显示名字"的空窗；失活是同步的、立刻干净。
/// 而且钟是场景地标，日后想改成"取走表芯、留个哑钟壳"只要把下面那行换掉。
/// </summary>
public class ShiftWatchPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string takenHint = "表芯到手——钟面暗了下去。滚轮切到怀表，F 自己拨班";
    [SerializeField] private string alreadyHint = "口袋里已经有怀表了";

    public float InteractionRange { get { return 1.8f; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        // 已经拿过（或被首次强制换班自动发过）：钟面上没东西可取。
        if (PlayerGear.HasShiftWatch)
        {
            HintToast.Show(alreadyHint);
            Vanish();
            return;
        }

        // 首次碰钟：先强制换一次班（时空切换），再发怀表。
        // 顺序关键：ForceFlip 置 LastFlipWasManual=true，FactoryTimeSwap 里
        // 「首次强制换班自动发怀表」的守卫不会二次发表；即使偶发被锁定/冷却挡下
        // （返回 false）也照常发怀表——首次必得表，换班尽力而为。
        FactoryShift.ForceFlip();

        PlayerGear.AcquireShiftWatch();
        if (player != null && player.GetComponentInChildren<ShiftWatch>(true) == null)
        {
            player.AddComponent<ShiftWatch>();
        }

        HintToast.Show(takenHint);
        Vanish();
    }

    /// <summary>取下钟：注销交互并失活整个物体（同步、立刻从准星与提示里消失）。</summary>
    private void Vanish()
    {
        InteractionRegistry.Unregister(this);
        gameObject.SetActive(false);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
