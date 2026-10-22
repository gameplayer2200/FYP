using System.Collections;
using UnityEngine;

/// <summary>
/// 机器电源开关（工厂谜题件，挂场景现成的 PowerBox 配电箱上）：E 启动机器——
/// 只有本世界在班时才有反应（歇班 = 机器沉睡，推不上去）；
/// 排班顺序对 → 机器运转（转子旋转 + 嗡鸣），错 → 火花 + 抖动 + 全厂已启动机器复位。
/// 启动状态跨班次保留：歇班只是停转，进度不回滚（像 Metro 渐熄不回滚一样），
/// 只有错序才复位——排班表是唯一的线索。
/// </summary>
public class MachinePowerSwitch : MonoBehaviour, IInteractable
{
    [SerializeField] private FactoryShiftController controller;

    [Tooltip("机器编号：0/1=昼侧主厅，2=夜侧深区（与排班表 machineNames 下标对应）")]
    [SerializeField] private int machineId;
    [SerializeField] private string displayName = "机器";

    [Header("运转演出")]
    [Tooltip("运转时旋转的部位（风机叶片 / 联轴节；可空）")]
    [SerializeField] private Transform rotor;
    [SerializeField] private Vector3 rotorAxis = Vector3.forward;
    [SerializeField] private float rotorSpeed = 300f;
    [Tooltip("运转嗡鸣（循环 Audio；可空）")]
    [SerializeField] private AudioSource hum;
    [Tooltip("错序火花（可空）")]
    [SerializeField] private ParticleSystem sparks;

    [SerializeField] private float interactionRange = 2.5f;

    public bool Started { get; private set; }
    public string DisplayName { get { return displayName; } }

    public float InteractionRange { get { return interactionRange; } }
    public Transform GetInteractionPoint() { return transform; }

    public void Interact(GameObject player)
    {
        if (Started)
        {
            HintToast.Show(displayName + " 在转了——排班表上没有第二遍");
            return;
        }
        if (controller == null || !controller.WorldAwake)
        {
            HintToast.Show("推不上去——" + SideName + "还没开始，机器沉睡着");
            return;
        }
        if (FactoryShift.TryStart(machineId))
        {
            Started = true;
            bool solved = FactoryShift.PuzzleSolved;
            HintToast.Show(solved
                ? displayName + " 启动——三台全转起来了。回排班表前做决定吧"
                : displayName + " 启动（" + FactoryShift.StartedMachines + "/" + FactoryShift.MachineCount + "）");
        }
        else
        {
            WrongOrder();
        }
    }

    /// <summary>错序：火花 + 抖动 + 全厂复位（各开关听 OnProgressReset 一致停机）。</summary>
    private void WrongOrder()
    {
        if (sparks != null) sparks.Play();
        FactoryShift.ResetProgress();
        HintToast.Show("顺序不对——" + displayName + " 炸出火花，已启动的机器全停了");
        StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        var t = transform;
        Vector3 basePos = t.localPosition;
        float e = 0f;
        while (e < 0.5f)
        {
            e += Time.deltaTime;
            float k = 1f - e / 0.5f; // 抖动幅度随时间衰减（BreakerSwitch 火花同款）
            t.localPosition = basePos + new Vector3(Mathf.Sin(e * 60f) * 0.012f * k, 0f, 0f);
            yield return null;
        }
        t.localPosition = basePos;
    }

    private void Update()
    {
        // 运转 = 已启动 且 本世界醒着（歇班停转、复班续转）
        bool running = Started && controller != null && controller.WorldAwake;
        if (running && rotor != null) rotor.Rotate(rotorAxis, rotorSpeed * Time.deltaTime, Space.Self);
        if (hum != null)
        {
            float target = running ? 0.8f : 0f;
            if (target > 0f && !hum.isPlaying) hum.Play();
            hum.volume = Mathf.MoveTowards(hum.volume, target, Time.deltaTime * 1.5f);
            if (target == 0f && hum.volume <= 0.001f && hum.isPlaying) hum.Stop();
        }
    }

    private string SideName
    {
        get { return controller != null && controller.World == FactoryShift.Phase.Night ? "夜班" : "白班"; }
    }

    private void HandleProgressReset()
    {
        Started = false;
    }

    private void OnEnable()
    {
        InteractionRegistry.Register(this);
        FactoryShift.OnProgressReset += HandleProgressReset;
    }

    private void OnDisable()
    {
        InteractionRegistry.Unregister(this);
        FactoryShift.OnProgressReset -= HandleProgressReset;
    }
}
