using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 录音笔（探索道具，提案表 3）：「放在异常点旁录制，回实验室播放——叙事碎片投放器」。
///
/// 状态机（F 键，滚轮选中录音笔格）：
///   手上·空白 → F 放下（脚前生成 RecorderUnit，开录 captureWindow 秒；期间 8m 内
///               活跃过的异常源都会被记进磁带）；
///   已放下     → F 提示走过去按 E 收回（不能隔空取——走回去是代价的一部分）；
///   手上·有带  → 实验室里 F 播放（CRT 打字机面板逐行显字），其它世界提示回实验室；
///               播完磁带作废，可再去录。
///
/// 放在 FYP.Player：要直接生成/调用 RecorderUnit（同层）；异常感知走 Core 的
/// AnomalyPresence，不碰 World。组件由 RecorderPickup 在拾取时挂到玩家根上。
/// </summary>
public class VoiceRecorder : MonoBehaviour
{
    [SerializeField] private KeyCode useKey = KeyCode.F;
    [Tooltip("放下后录多久（秒）")]
    [SerializeField] private float captureWindow = 20f;
    [Tooltip("录制时捕捉异常源的距离（米）")]
    [SerializeField] private float captureRadius = 8f;

    private enum State { Idle, Deployed, CarriedTape }

    private State state = State.Idle;
    private RecorderUnit unit;
    private List<string> capturedIds;

    // 播放面板
    private static readonly HashSet<string> playedIds = new HashSet<string>();
    private string playText;
    private float playStart;
    private bool playing;

    private void Update()
    {
        // 放下的录音笔随世界卸载一起消失：仪器是你的，世界留不住它
        if (state == State.Deployed && unit == null)
        {
            state = State.Idle;
            HintToast.Show("录音笔自己滑回了收纳格");
        }

        if (Input.GetKeyDown(useKey) && PlayerGear.UseKeyOwner == PlayerGear.RecorderSlot) HandleKey();
        if (playing && (Input.GetKeyDown(useKey) || Input.GetKeyDown(KeyCode.Escape)))
        {
            // 播放中再按 F：已显完全部文字则关闭，否则快进到底
            float full = playText.Length / 38f;
            if (Time.unscaledTime - playStart >= full) playing = false;
            else playStart = Mathf.NegativeInfinity;
        }
    }

    private void HandleKey()
    {
        if (!PlayerGear.HasRecorder) return;
        switch (state)
        {
            case State.Deployed:
                HintToast.Show("录音笔放在原地——走过去按 E 收回");
                break;
            case State.Idle:
                Deploy();
                break;
            case State.CarriedTape:
                if (WorldPresence.InHome) StartPlayback();
                else HintToast.Show("回实验室再听——那边的喇叭才是好的");
                break;
        }
    }

    private void Deploy()
    {
        var player = PlayerRef.Root;
        if (player == null) return;

        Vector3 fwd = player.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 spawn = player.position + fwd * 0.8f + Vector3.up * 0.6f;
        if (Physics.Raycast(spawn, Vector3.down, 1.5f)) spawn -= Vector3.up * 1.4f; // 落到脚下地面

        unit = RecorderUnit.Create(this, spawn, Quaternion.LookRotation(fwd), captureWindow, captureRadius);
        state = State.Deployed;
        HintToast.Show("录音笔放下了——它在听（" + (int)captureWindow + " 秒）");
    }

    /// <summary>RecorderUnit 收回时回调（E 键）：带回录到内容或空白。</summary>
    internal void OnRetrieved(List<string> ids)
    {
        capturedIds = ids != null && ids.Count > 0 ? ids : null;
        unit = null;
        state = capturedIds != null ? State.CarriedTape : State.Idle;
        HintToast.Show(capturedIds != null
            ? "磁带里有东西。回实验室选中录音笔按 F 播放"
            : "什么都没录到——离异常再近一点放");
    }

    private void StartPlayback()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var id in capturedIds)
        {
            string[] lines;
            bool replay = playedIds.Contains(id);
            if (!replay && TapeLibrary.TryGetValue(id, out lines))
            {
                playedIds.Add(id);
                for (int i = 0; i < lines.Length; i++) sb.AppendLine(lines[i]);
            }
            else sb.AppendLine(replay ? "【回放】还是那些声音。一模一样，连停顿都一样。" : "【回放】（无法归档的杂音）");
            sb.AppendLine();
        }
        playText = sb.ToString().TrimEnd();
        playStart = Time.unscaledTime;
        playing = true;
        state = State.Idle;      // 播完磁带作废，可再去录
        capturedIds = null;
    }

    private void OnGUI()
    {
        if (!playing) return;
        float w = CrtTheme.Px(430f), h = CrtTheme.Px(150f);
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.62f, w, h);
        CrtTheme.Panel(r, "回放");
        float elapsed = Time.unscaledTime - playStart;
        string shown = CrtTheme.Reveal(playText, elapsed);
        GUI.Label(new Rect(r.x + CrtTheme.Px(12f), r.y + CrtTheme.Px(28f), r.width - CrtTheme.Px(24f), r.height - CrtTheme.Px(40f)),
            shown, CrtTheme.Style(13, CrtTheme.Ink, true, false, TextAnchor.UpperLeft));
        CrtTheme.Cursor(r.x + CrtTheme.Px(12f), r.yMax - CrtTheme.Px(22f));
    }

    /// <summary>磁带内容池（一次性叙事碎片，按异常源 id 取）。</summary>
    private static readonly Dictionary<string, string[]> TapeLibrary = new Dictionary<string, string[]>
    {
        { "metro.ghost-arrival", new[]
            {
                "【回放 00:11】轨道在响。风很大。",
                "【回放 00:19】广播念了一个不存在的站名。念了两遍。",
                "【回放 00:26】……没有刹车声。它根本没打算停。",
            } },
        { "metro.emf-storm", new[]
            {
                "【回放 00:04】滋滋——滋——",
                "【回放 00:12】（电流声里压着半句人话）「……别信读数……」",
                "【回放 00:17】之后只剩杂音。",
            } },
        { "metro.knock", new[]
            {
                "【回放 00:08】三下。很轻。是指节。",
                "【回放 00:15】又三下。这次在门的另一侧。",
                "【回放 00:23】（离磁带越来越近的）第四下。",
            } },
        { "metro.far-lamp", new[]
            {
                "【回放 00:02】安静。隧道深处有电流声。",
                "【回放 00:14】有什么在很远的地方，一直亮着。",
                "【回放 00:29】它不在等车。它在等走过去的人。",
            } },
    };
}
