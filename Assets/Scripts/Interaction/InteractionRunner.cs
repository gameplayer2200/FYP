using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 交互中枢：挂 player。遍历可交互物登记表，选最近的为当前目标；
/// 按 E 只通知它一个——E 键单点分发，任何新交互物实现接口并自注册即可接入，无需改本类。
/// 不做物理扫描：密集几何场景里 OverlapSphere 缓冲区会被静态碰撞体填满，交互物反而被挤出结果。
/// </summary>
public class InteractionRunner : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float scanInterval = 0.2f; // 扫描节流（不必每帧）
    [Tooltip("打印每次按键选中的目标，排查交互失灵时开。")]
    [SerializeField] private bool verbose;

    private readonly List<IInteractable> nearby = new List<IInteractable>();
    private float nextScan;
    private IInteractable current;
    private string currentLabel = "交互";

    /// <summary>当前选中的交互物（供提示 UI 读取；可能为 null）。</summary>
    public IInteractable Current { get { return current; } }

    private void OnGUI()
    {
        if (current == null) return;
        GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.62f, 240f, 24f),
            "[E] " + currentLabel);
    }

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + scanInterval;
            ScanNearest();
        }
        if (Input.GetKeyDown(interactKey))
        {
            if (verbose)
                Debug.Log("[Interact] 选中=" + (current != null ? ((Component)current).gameObject.name : "无目标")
                          + " 范围内=" + nearby.Count);
            if (current != null) current.Interact(gameObject);
        }
    }

    private void ScanNearest()
    {
        nearby.Clear();
        IInteractable best = null;
        float bestDist = float.MaxValue;
        var pos = transform.position;
        var items = InteractionRegistry.Items;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            // 已销毁的残留项顺手清出登记表
            if (!IsAlive(it))
            {
                InteractionRegistry.RemoveAt(i);
                continue;
            }
            nearby.Add(it);
            float d = Vector3.Distance(pos, it.GetInteractionPoint().position);
            if (d > it.InteractionRange) continue; // 不在有效距离内
            if (d < bestDist) { bestDist = d; best = it; }
        }
        current = best;
        currentLabel = "交互";
        if (best != null)
        {
            // 挂了 InteractionLabel 的交互物用自定义提示文案
            var c = best as Component;
            if (!ReferenceEquals(c, null))
            {
                var label = c.GetComponent<InteractionLabel>();
                if (label != null) currentLabel = label.Text;
            }
        }
    }

    /// <summary>
    /// 判活。注意不能用 `it as Component == null`：`as` 不走 Unity 重载的 ==，
    /// 已销毁的 MonoBehaviour 转换结果并非 null。必须用 ReferenceEquals 区分
    /// "不是 Unity 对象"和"是 Unity 对象但已销毁"两种情况。
    /// </summary>
    private static bool IsAlive(IInteractable it)
    {
        if (ReferenceEquals(it, null)) return false;
        var c = it as Component;
        if (ReferenceEquals(c, null)) return true; // 纯 C# 实现
        return c != null;                          // Unity 对象：重载的 == 能识别已销毁
    }
}
