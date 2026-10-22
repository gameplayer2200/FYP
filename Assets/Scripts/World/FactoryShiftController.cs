using UnityEngine;

/// <summary>
/// 工厂排班中枢（每场景一份：昼/夜）：盒形判定玩家是否身处本世界（MetroBlackout 同构），
/// 玩家在厂内时驱动 Core 的 FactoryShift 排班钟并同步班时长——玩家离开工厂轮换暂停，
/// 回来接着这班走（世界状态不因玩家缺席推进）。歇班世界的死寂表现由各组件
/// 自听 FactoryShift 状态实现，本组件只管 zone、驱动与氛围底噪。
///
/// 注意：工厂永不调用 SetAmbientDrain——「夜班不掉电」是与 Metro 的刻意差异化
/// （压迫来自轮换混乱与异常显形，不靠耗电；实验室恒 0 也是同样的"没人调用"语义）。
/// </summary>
public class FactoryShiftController : MonoBehaviour
{
    [SerializeField] private FactoryShift.Phase world = FactoryShift.Phase.Day;

    [Header("世界范围（判定玩家是否身处本世界，盒形——由 FactorySceneDev 实测包围盒写入）")]
    [SerializeField] private Vector3 zoneCenter = new Vector3(-4324f, 6f, -38f);
    [SerializeField] private Vector3 zoneSize = new Vector3(120f, 40f, 170f);

    [Tooltip("每班时长（秒）——昼/夜两侧场景保持一致")]
    [SerializeField] private float shiftSeconds = 240f;

    [Tooltip("本世界氛围底噪（醒着且有人时渐入；可空）")]
    [SerializeField] private AudioSource ambience;

    private static int aliveCount;

    /// <summary>本场景代表的世界（昼/夜）。</summary>
    public FactoryShift.Phase World { get { return world; } }

    /// <summary>玩家当前是否身处本世界（每帧刷新；撤离计时器与异常事件用它做门槛）。</summary>
    public bool PlayerInZone { get; private set; }

    /// <summary>本世界此刻是否醒着（在班或被锁定）。</summary>
    public bool WorldAwake { get { return FactoryShift.IsAwake(world); } }

    private void Update()
    {
        // 与 MetroBlackout 同理：PlayerInZone 必须每帧刷新（锁定后也照刷，异常/撤离靠它判门槛）
        var player = PlayerRef.Root;
        PlayerInZone = player != null && InZone(player.position);

        if (PlayerInZone)
        {
            FactoryShift.Configure(shiftSeconds);
            FactoryShift.Tick(Time.deltaTime);
        }

        // 氛围底噪：醒着的厂有低鸣，歇班的厂死寂
        if (ambience != null)
        {
            float target = PlayerInZone && WorldAwake ? 0.6f : 0f;
            if (target > 0f && !ambience.isPlaying) ambience.Play();
            ambience.volume = Mathf.MoveTowards(ambience.volume, target, Time.deltaTime * 0.4f);
            if (target == 0f && ambience.volume <= 0.001f && ambience.isPlaying) ambience.Stop();
        }
    }

    /// <summary>盒形世界判定：昼/夜两场景 x 相差 14797，天然分离（双世界常驻、零重叠）。</summary>
    private bool InZone(Vector3 p)
    {
        Vector3 d = p - zoneCenter;
        return Mathf.Abs(d.x) <= zoneSize.x * 0.5f
            && Mathf.Abs(d.y) <= zoneSize.y * 0.5f
            && Mathf.Abs(d.z) <= zoneSize.z * 0.5f;
    }

    private void OnEnable() { aliveCount++; }

    private void OnDestroy()
    {
        // 双场景常驻的静态钟：最后一个控制器销毁（停 Play）时整局复位，防跨会话残留
        aliveCount--;
        if (aliveCount <= 0) FactoryShift.ResetAll();
    }
}
