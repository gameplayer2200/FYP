using UnityEngine;

/// <summary>
/// 配电箱里的一根线：滚轮选中万用表格后按 F = 检测带电与否并把该线接入电闸；
/// 没有万用表只提示检不了。
///
/// 只有 F 能"使用道具"，所以这里刻意不做成交互物（不响应 E）——
/// 否则同一件事会有 E 和 F 两个入口，违背"只有 F 能使用当前道具"。
/// 多根线同时在范围内时只有最近的一根响应，不会一次弹三条提示。
/// </summary>
public class PowerWire : MonoBehaviour
{
    [SerializeField] private PowerCabinet cabinet;
    [SerializeField] private int wireIndex;
    [SerializeField] private string displayName = "红线";
    [SerializeField] private float interactionRange = 2.5f;

    /// <summary>同一帧里只让一根线处理 F（否则范围内的每根线都会各弹一条提示）。</summary>
    private static int lastUseFrame = -1;

    /// <summary>这根线的名字（"红线" 等）。配电箱的回执文案要用，所以是公开只读属性。</summary>
    public string DisplayName { get { return displayName; } }

    /// <summary>测线并接入电闸。只由 F 路径调用；Editor 测试驱动（PT_SolvePuzzle）也直接调它。</summary>
    public void TestAndSelect()
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
        if (best != null) best.TestAndSelect();
        else HintToast.Show("附近没有能测的线路——靠近配电箱再按 F");
    }
}
