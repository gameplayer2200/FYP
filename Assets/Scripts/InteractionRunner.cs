using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 交互中枢：挂 player。每帧扫描附近所有 IInteractable，选最近的为当前目标；
/// 按 E 只通知它一个——E 键单点分发，任何新交互物实现接口即可接入，无需改本类。
/// </summary>
public class InteractionRunner : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float scanInterval = 0.2f; // 扫描节流（不必每帧）

    private readonly List<IInteractable> nearby = new List<IInteractable>();
    private readonly Collider[] overlap = new Collider[32];
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
        // 半径 6m 内找所有碰撞体上的 IInteractable（含 trigger）
        int n = Physics.OverlapSphereNonAlloc(transform.position, 6f, overlap, ~0, UnityEngine.QueryTriggerInteraction.Collide); // 必须含 Trigger：交互体大多是触发器
        for (int i = 0; i < n; i++)
        {
            var col = overlap[i];
            if (col == null) continue;
            var inter = col.GetComponentInParent<IInteractable>();
            if (inter != null && !nearby.Contains(inter)) nearby.Add(inter);
        }
        // 选"在其有效距离内、且交互点最近"的一个
        IInteractable best = null;
        float bestDist = float.MaxValue;
        var pos = transform.position;
        foreach (var it in nearby)
        {
            if (it == null) continue; // 可能已被销毁（如拾取物）
            float d = Vector3.Distance(pos, it.GetInteractionPoint().position);
            if (d > it.InteractionRange) continue;
            if (d < bestDist) { bestDist = d; best = it; }
        }
        current = best;
    }
}
