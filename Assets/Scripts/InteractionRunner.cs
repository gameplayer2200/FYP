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

    private readonly List<IInteractable> nearby = new List<IInteractable>();
    private float nextScan;
    private IInteractable current;

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + scanInterval;
            ScanNearest();
        }
        if (Input.GetKeyDown(interactKey))
        {
            Debug.Log("[Interact] E按下 current=" + (current != null ? ((Component)current).gameObject.name : "无目标") + " nearby=" + nearby.Count);
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
            if (it == null || it.Equals(null) || (object)((Component)it) == null)
            {
                items.RemoveAt(i);
                continue;
            }
            nearby.Add(it);
            float d = Vector3.Distance(pos, it.GetInteractionPoint().position);
            if (d > it.InteractionRange) continue; // 不在有效距离内
            if (d < bestDist) { bestDist = d; best = it; }
        }
        current = best;
    }
}
