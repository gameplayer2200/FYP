using UnityEngine;

/// <summary>
/// 换班挂钟（工厂提示件，摆在昼夜穿行门旁，昼/夜两场景镜像各一座）：指针 = 本班进度
/// （分针走满一圈 = 换班），盘面自发光 = 本世界醒着（磷光绿）/ 歇班（暗）。
/// 换班或改锁时若玩家在本世界，敲钟 + 提示（另一侧的钟不出声——只提示玩家所在的世界）。
/// 谜题锁定后指针停摆——「这一班不会再换了」。
/// </summary>
public class ShiftClock : MonoBehaviour
{
    [SerializeField] private FactoryShiftController controller;
    [Tooltip("分针（长针，走满一圈=换班；可空）")]
    [SerializeField] private Transform minuteHand;
    [Tooltip("时针（短针，一班=表盘一小格；可空）")]
    [SerializeField] private Transform hourHand;
    [Tooltip("盘面（发光 Renderer；MPB 改自发光，不动共享材质）")]
    [SerializeField] private Renderer face;
    [SerializeField] private Color awakeColor = new Color(0.25f, 1f, 0.45f);
    [SerializeField] private Color dormantColor = new Color(0.05f, 0.09f, 0.07f);
    [Tooltip("换班钟声（可空）")]
    [SerializeField] private AudioSource chime;
    [SerializeField] private string flipToDayHint = "换班了——白班开始";
    [SerializeField] private string flipToNightHint = "换班了——夜班开始";
    [SerializeField] private string lockHint = "排班钟停了——{0}被锁定了";

    private MaterialPropertyBlock mpb;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void OnEnable()
    {
        FactoryShift.OnPhaseFlipped += HandleFlip;
        FactoryShift.OnLockChanged += HandleLockChanged;
    }

    private void OnDisable()
    {
        FactoryShift.OnPhaseFlipped -= HandleFlip;
        FactoryShift.OnLockChanged -= HandleLockChanged;
    }

    private void Update()
    {
        bool frozen = FactoryShift.PuzzleSolved || FactoryShift.LockedPhase.HasValue;
        float frac = frozen
            ? 0f
            : 1f - Mathf.Clamp01(FactoryShift.TimeLeft / Mathf.Max(1f, FactoryShift.ShiftSeconds));

        if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0f, 0f, -360f * frac);
        if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0f, 0f, -30f * frac);

        if (face != null)
        {
            bool awake = controller == null || controller.WorldAwake;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            face.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, awake ? awakeColor * 1.7f : dormantColor);
            face.SetPropertyBlock(mpb);
        }
    }

    private void HandleFlip()
    {
        if (controller == null || !controller.PlayerInZone) return; // 只提示玩家所在的世界
        if (chime != null) chime.Play();
        HintToast.Show(FactoryShift.Current == FactoryShift.Phase.Day ? flipToDayHint : flipToNightHint, 3.5f);
    }

    private void HandleLockChanged()
    {
        if (controller == null || !controller.PlayerInZone) return;
        if (chime != null) chime.Play();
        string phase = FactoryShift.LockedPhase == FactoryShift.Phase.Day ? "白班" : "夜班";
        HintToast.Show(lockHint.Replace("{0}", phase), 3.5f);
    }
}
