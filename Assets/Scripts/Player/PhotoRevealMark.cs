using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 显影标记（配合拍照相机，提案「拍照相机：致盲一秒里照片上多了什么」）：
/// 平时完全不可见的墙字，只在快门闪过的瞬间显形片刻。
/// 标记自己完全被动——PhotoCamera 快门时调 <see cref="Pulse"/>，在场标记同时显形。
///
/// 放在 FYP.Player（而不是 World）：它只服务相机这一件仪器，被 PhotoCamera 直接调用；
/// 与 HeldItem 同理，Player 脚本照样可以摆进场景当道具/摆件（ADR-0004 的依赖是
/// 程序集引用方向，不是"谁能进场景"）。
/// 文字用 TextMesh（世界空间、无 UGUI 依赖），按墙摆；亮暗场景都不补光——它本来就不该被看见。
/// </summary>
public class PhotoRevealMark : MonoBehaviour
{
    private static readonly List<PhotoRevealMark> marks = new List<PhotoRevealMark>();

    [Tooltip("显影时才可见的文字")]
    [SerializeField] private string text = "别开门";
    [Tooltip("字色")]
    [SerializeField] private Color color = new Color(0.75f, 1f, 0.85f, 0.95f);
    [Tooltip("TextMesh 字号（世界单位高）")]
    [SerializeField] private float characterSize = 0.32f;
    [Tooltip("单次显影最短持续秒数（Pulse 传更长的值则听 Pulse 的）")]
    [SerializeField] private float duration = 1.4f;

    private TextMesh mesh;
    private MeshRenderer render;
    private float until = -1f;

    /// <summary>快门脉冲：所有在场标记显形 duration 秒。</summary>
    public static void Pulse(float duration)
    {
        for (int i = 0; i < marks.Count; i++)
            if (marks[i] != null) marks[i].Show(duration);
    }

    private void Awake()
    {
        mesh = GetComponent<TextMesh>();
        if (mesh == null)
        {
            // 场景里只摆了空物体 + 本脚本：这里按序列化字段补全文字件
            mesh = gameObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
        }
        render = GetComponent<MeshRenderer>();
        if (render != null) render.enabled = false;   // 平时不可见——这是它的全部存在方式
    }

    private void OnEnable() { marks.Add(this); }

    private void OnDisable()
    {
        marks.Remove(this);
        if (render != null) render.enabled = false;
    }

    private void Show(float d)
    {
        until = Time.unscaledTime + Mathf.Max(d, duration);
    }

    private void Update()
    {
        bool on = Time.unscaledTime < until;
        if (render != null && render.enabled != on) render.enabled = on;
    }
}
