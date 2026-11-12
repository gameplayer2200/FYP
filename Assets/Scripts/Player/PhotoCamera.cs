using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 拍照相机（探索道具，提案表 3）：滚轮选中即举起取景器（CRT 暗角叠加），F 快门。
/// 快门 = 白闪过曝致盲自己约一秒；这一瞬间：
///   · 视野内（距离 + 视锥 + 无遮挡）的活跃异常源被记为观测证据（AnomalyPresence 查询）；
///   · 场景里的显影标记（PhotoRevealMark）短暂显形——「致盲退去时，照片上多了什么」。
/// 耗电一次性（快门走 BatterySystem.Drain，自动记入账单的仪器科目）。
///
/// F 键约定与手电筒/探测器一致：只有滚轮选中相机格时 F 才归这里（PlayerGear.UseKeyOwner）。
/// 组件由 HeldItem 在拾取时挂到玩家根上。
/// </summary>
public class PhotoCamera : MonoBehaviour
{
    [SerializeField] private KeyCode shutterKey = KeyCode.F;
    [Tooltip("快门一次耗电")]
    [SerializeField] private float drainPerShot = 4f;
    [Tooltip("快门冷却（秒）——连拍既毁节奏又刷屏")]
    [SerializeField] private float cooldown = 1.5f;
    [Tooltip("证据判定的最远距离（米）")]
    [SerializeField] private float evidenceRange = 30f;
    [Tooltip("证据判定的视锥半角（度）")]
    [SerializeField] private float evidenceAngle = 35f;
    [Tooltip("白闪时长（秒）= 致盲时长")]
    [SerializeField] private float flashTime = 0.9f;

    /// <summary>已观测到的异常源 id（本次会话，跨世界保持——仪器内存）。供结算/图鉴扩展用。</summary>
    public static readonly HashSet<string> ObservedIds = new HashSet<string>();

    public static int ObservedCount { get { return ObservedIds.Count; } }

    // 仪器每帧轮询异常源，复用同一 buffer（AnomalyPresence.CollectActive 的零分配约定）
    private static readonly List<AnomalyPresence.Reading> readings = new List<AnomalyPresence.Reading>();

    private float nextShotAt;
    private float flashUntil = -1f;
    private int frameCount;

    private void Update()
    {
        if (Input.GetKeyDown(shutterKey) && PlayerGear.UseKeyOwner == PlayerGear.CameraSlot) Snap();
    }

    /// <summary>快门（拾取后生效；测试可直接调用）。</summary>
    public void Snap()
    {
        if (!PlayerGear.HasCamera) return;
        if (Time.time < nextShotAt) return;
        var battery = BatterySystem.Instance;
        if (battery == null || battery.IsDead)
        {
            HintToast.Show("电量不足——快门按不下去");
            return;
        }

        nextShotAt = Time.time + cooldown;
        frameCount++;
        battery.Drain(drainPerShot);
        flashUntil = Time.unscaledTime + flashTime;

        // 证据判定：活跃异常源在 距离 + 视锥 + 无遮挡 内即被拍下；同一异常只记一次
        AnomalyPresence.CollectActive(readings);
        var cam = PlayerRef.Camera;
        if (cam != null && readings.Count > 0)
        {
            Vector3 eye = cam.transform.position;
            Vector3 fwd = cam.transform.forward;
            List<string> caught = null;
            for (int i = 0; i < readings.Count; i++)
            {
                Vector3 to = readings[i].Position - eye;
                float dist = to.magnitude;
                if (dist > evidenceRange) continue;
                if (Vector3.Angle(fwd, to) > evidenceAngle) continue;
                if (dist > 0.5f && Physics.Raycast(eye, to.normalized, dist)) continue; // 墙后拍不到
                if (ObservedIds.Contains(readings[i].Id)) continue;
                if (caught == null) caught = new List<string>();
                caught.Add(readings[i].DisplayName);
                ObservedIds.Add(readings[i].Id);
            }
            if (caught != null)
                HintToast.Show("已记录观测：" + string.Join("、", caught.ToArray()));
        }

        // 显影：快门瞬间照出肉眼看不到的东西
        PhotoRevealMark.Pulse(1.4f);
    }

    private void OnGUI()
    {
        // 取景器：只在选中相机格时叠加（= 举到眼前）；光标未锁（菜单/失焦）不叠
        if (PlayerGear.SelectedIndex != PlayerGear.CameraSlot || !PlayerGear.HasCamera) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var full = new Rect(0f, 0f, Screen.width, Screen.height);

        // 暗角 + 扫描线：像隔着一台旧取景器看世界
        GUI.DrawTexture(full, CrtTheme.Vignette);
        CrtTheme.Scan(full, 0.10f);

        // 四角取景框（屏幕缩进一圈）+ 中心对焦框
        float m = CrtTheme.Px(46f);
        var fr = new Rect(m, m, Screen.width - m * 2f, Screen.height - m * 2f);
        var ink = new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.8f);
        CrtTheme.Corners(fr, ink, 26f, 3f);
        float cs = CrtTheme.Px(30f);
        CrtTheme.Border(new Rect(Screen.width * 0.5f - cs * 0.5f, Screen.height * 0.5f - cs * 0.5f, cs, cs),
            new Color(CrtTheme.Ink.r, CrtTheme.Ink.g, CrtTheme.Ink.b, 0.45f));

        // 左下读数：张数 / 观测数（风暴中乱码——「仪器不可信」是全局约定）
        string shots = "F=" + frameCount.ToString("D3") + "  REC " + ObservedIds.Count;
        if (EmfStorm.IsStorming) shots = EmfStorm.Scramble(shots);
        GUI.Label(new Rect(fr.x + CrtTheme.Px(6f), fr.yMax - CrtTheme.Px(26f), CrtTheme.Px(260f), CrtTheme.Px(20f)),
            shots, CrtTheme.Style(13, CrtTheme.InkDim, false));

        // 白闪致盲：整屏白，alpha 从 1 衰到 0（致盲退去的后半段恰是显影标记可见的窗口）
        if (Time.unscaledTime < flashUntil)
        {
            float k = Mathf.Clamp01((flashUntil - Time.unscaledTime) / flashTime);
            CrtTheme.Fill(full, new Color(1f, 1f, 0.97f, Mathf.Pow(k, 0.7f)));
        }
    }
}
