using UnityEngine;

/// <summary>
/// 配电箱里的一根线：持万用表时 E = 检测带电与否并把该线接入电闸；
/// 没有万用表只提示检不了。
///
/// F 是统一的"使用道具"键：滚轮选中万用表格后按 F 也等于测线
/// （多根线同时在范围内时只有最近的一根响应，不会一次弹三条提示）。
/// </summary>
public class PowerWire : MonoBehaviour, IInteractable
{
    [SerializeField] private PowerCabinet cabinet;
    [SerializeField] private int wireIndex;
    [SerializeField] private string displayName = "红线";
    [SerializeField] private float interactionRange = 2.5f;

    /// <summary>同一帧里只让一根线处理 F（否则范围内的每根线都会各弹一条提示）。</summary>
    private static int lastUseFrame = -1;

    public string DisplayName { get { return displayName; } }

    public float InteractionRange { get { return interactionRange; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (cabinet == null) return;
        if (!PlayerGear.HasMultimeter)
        {
            HintToast.Show("这根线看不出有没有电——需要万用表");
            return;
        }
        bool live = cabinet.IsLive(wireIndex);
        HintToast.Show(displayName + "：" + (live ? "带电！" : "没有电") + "（" + cabinet.Select(wireIndex) + "）");
    }

    private void Update()
    {
        if (PlayerGear.UseKeyOwner != 1) return;
        if (!Input.GetKeyDown(KeyCode.F)) return;
        if (lastUseFrame == Time.frameCount) return;

        var root = PlayerRef.Root;
        if (root == null) return;

        lastUseFrame = Time.frameCount;

        PowerWire best = null;
        float bestDist = float.MaxValue;
        var all = FindObjectsOfType<PowerWire>();
        for (int i = 0; i < all.Length; i++)
        {
            float d = Vector3.Distance(root.position, all[i].transform.position);
            if (d > all[i].interactionRange) continue;
            if (d < bestDist) { bestDist = d; best = all[i]; }
        }
        if (best != null) best.Interact(root.gameObject);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
