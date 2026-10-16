using System.Collections;
using UnityEngine;

/// <summary>
/// 配电箱电闸：E 合闸，由 PowerCabinet 验证接线。接对→锁死闭合（不再可交互）；
/// 接错→火花抖动后弹回原位，可重试。
/// </summary>
public class BreakerSwitch : MonoBehaviour, IInteractable
{
    [SerializeField] private PowerCabinet cabinet;
    [Tooltip("闸刀杆（旋转演出用，留空则不转）")]
    [SerializeField] private Transform lever;
    [SerializeField] private Vector3 openEuler = new Vector3(-40f, 0f, 0f);
    [SerializeField] private Vector3 closedEuler = new Vector3(25f, 0f, 0f);
    [SerializeField] private float sparkDuration = 0.5f;
    [SerializeField] private float interactionRange = 2.5f;

    private bool closed;
    private Coroutine anim;

    public float InteractionRange { get { return interactionRange; } }

    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (closed) return;
        if (cabinet != null) cabinet.TryCloseBreaker();
    }

    /// <summary>PowerCabinet 回调：合闸成功。</summary>
    public void SetClosed(bool value)
    {
        closed = value;
        if (anim != null) { StopCoroutine(anim); anim = null; }
        if (lever != null) lever.localRotation = Quaternion.Euler(value ? closedEuler : openEuler);
        if (value) enabled = false; // 已合闸，让出交互位
    }

    /// <summary>PowerCabinet 回调：接错线，火花抖动后弹回。</summary>
    public void Spark()
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(SparkRoutine());
    }

    private IEnumerator SparkRoutine()
    {
        var t = lever != null ? lever : transform;
        float e = 0f;
        while (e < sparkDuration)
        {
            e += Time.deltaTime;
            float k = e / sparkDuration; // 抖动幅度随时间衰减
            t.localRotation = Quaternion.Euler(openEuler + new Vector3(Mathf.Sin(k * 45f) * 14f * (1f - k), 0f, 0f));
            yield return null;
        }
        t.localRotation = Quaternion.Euler(openEuler);
        anim = null;
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
