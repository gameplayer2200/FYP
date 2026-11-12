using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 像素方块放置体（VoiceRecorder 放下的那一只）：程序拼装的深灰方块 + 录制中的橙色按钮，
/// 开录后 captureWindow 秒内把 captureRadius 范围内活跃过的异常源记进磁带；
/// E 收回（把录到的 id 列表交还 VoiceRecorder 后自毁）。
///
/// 放在 FYP.Player（与 VoiceRecorder 同层互调；异常感知走 Core 的 AnomalyPresence）。
/// 机身外观运行时拼（Cube 壳 + 自发光指示灯），不依赖任何模型资产——
/// 视觉本来就只是个深色小盒子，重点是指示灯的呼吸。
/// </summary>
public class RecorderUnit : MonoBehaviour, IInteractable
{
    [Tooltip("交互范围（米）")]
    [SerializeField] private float interactionRange = 1.6f;
    [SerializeField] private string labelRecording = "收回像素方块（在录）";
    [SerializeField] private string labelDone = "收回像素方块";

    private VoiceRecorder owner;
    private readonly List<string> captured = new List<string>();
    private static readonly List<AnomalyPresence.Reading> readings = new List<AnomalyPresence.Reading>();
    private Renderer led;
    private Material ledMat;
    private InteractionLabel label;
    private bool recording;
    private bool retrieved;

    public float InteractionRange { get { return interactionRange; } }
    public Transform GetInteractionPoint() { return transform; }

    /// <summary>在指定位置生成一只放置体并开录。</summary>
    public static RecorderUnit Create(VoiceRecorder owner, Vector3 position, Quaternion rotation, float window, float radius)
    {
        var go = new GameObject("像素方块_放置");
        go.transform.SetPositionAndRotation(position, rotation);

        // 机身：深色小盒
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "机身";
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.14f, 0.05f, 0.08f);
        Object.Destroy(body.GetComponent<Collider>());   // 交互判定用根物体，壳子不参战
        var mat = new Material(Shader.Find("Standard")) { color = new Color(0.10f, 0.11f, 0.12f) };
        mat.SetFloat("_Glossiness", 0.35f);
        body.GetComponent<Renderer>().sharedMaterial = mat;

        // 指示灯：录制中呼吸的红点
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lamp.name = "指示灯";
        lamp.transform.SetParent(go.transform, false);
        lamp.transform.localPosition = new Vector3(0.045f, 0.028f, 0f);
        lamp.transform.localScale = new Vector3(0.016f, 0.008f, 0.016f);
        Object.Destroy(lamp.GetComponent<Collider>());
        var unit = go.AddComponent<RecorderUnit>();
        unit.led = lamp.GetComponent<Renderer>();
        unit.ledMat = new Material(Shader.Find("Standard"))
        {
            color = new Color(0.5f, 0.05f, 0.05f),
            enableInstancing = false
        };
        unit.ledMat.EnableKeyword("_EMISSION");
        unit.ledMat.SetColor("_EmissionColor", new Color(0.9f, 0.1f, 0.1f));
        unit.led.sharedMaterial = unit.ledMat;

        unit.owner = owner;
        unit.label = go.AddComponent<InteractionLabel>();
        unit.label.Text = unit.labelRecording;
        unit.StartCoroutine(unit.RecordRoutine(window, radius));
        return unit;
    }

    private IEnumerator RecordRoutine(float window, float radius)
    {
        recording = true;
        float t = 0f;
        while (t < window)
        {
            AnomalyPresence.CollectActive(readings);
            for (int i = 0; i < readings.Count; i++)
            {
                Vector3 d = readings[i].Position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= radius * radius && !captured.Contains(readings[i].Id))
                    captured.Add(readings[i].Id);   // 静默入带——录到了什么，回放才知道
            }
            yield return new WaitForSeconds(0.4f);
            t += 0.4f;
        }
        recording = false;
        if (label != null) label.Text = labelDone;
    }

    private void Update()
    {
        if (led != null)
            led.enabled = !recording || Mathf.Repeat(Time.time, 1.1f) < 0.55f;  // 录制中呼吸，录完常灭
    }

    public void Interact(GameObject player)
    {
        if (retrieved) return;
        retrieved = true;
        InteractionRegistry.Unregister(this);
        if (owner != null) owner.OnRetrieved(new List<string>(captured));
        Destroy(gameObject);
    }

    private void OnEnable() { InteractionRegistry.Register(this); }
    private void OnDisable() { InteractionRegistry.Unregister(this); }
}
