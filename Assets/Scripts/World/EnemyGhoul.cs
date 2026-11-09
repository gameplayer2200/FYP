using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 「食尸鬼」敌人：在层域里满地图漫游，**发现节奏 + 视野扇形 + 贴身感知 + 听觉**四路感知玩家，
/// 看见即追击、跟丢后去最后可见点搜索、超时回巡逻，够近就挥击（BatterySystem.TakeHit）。
///
/// <para><b>用法</b>：把 <c>Assets/Prefab/Enemy/GhoulEnemy.prefab</c> 拖进任意场景即可，无需任何手动配置。
/// 根节点自带 NavMeshAgent + CapsuleCollider + 本脚本，Ghoul 模型已按「玩家的 1.5 倍」缩放；
/// 默认 <see cref="roamWholeScene"/> = 开，会在本场景烘焙出的 NavMesh 上随机挑可达点漫游（不是原地打转）。
/// 想固定路线就关掉漫游、在 <see cref="patrolPoints"/> 里填**局部坐标**（摆 4 个点绕圈，2 个点来回）。</para>
///
/// <para><b>为什么自己烘 NavMesh</b>：工程里 7 个场景的 NavMeshSettings 都指向已丢失的资产 guid
/// （<c>d2fc871308fdb5747b1bc6b5cefdaf00</c>，Assets 下没有对应文件，从仓库建立至今从未烘焙过），
/// 所以每个场景首次进入 Play、或该场景被 WorldStreamer 加载时，用 <c>NavMeshBuilder</c> 现收现烘一份
/// （每个场景一份，静态 <c>s_bakedData</c> 缓存 NavMeshDataInstance；多只敌人共享同一次烘焙）。
/// 烘焙用**独立的小号代理尺寸**（bakeAgentRadius/Height/Climb），与 3 m 高的可见体型解耦——见下方
/// <see cref="bakeAgentClimb"/> 的注释，这是「进不了门」那个 bug 的根因。</para>
///
/// <para><b>动画</b>：legacy <c>Animation</c>（Ghoul.prefab 自带，不是 Animator，没有 Avatar/Controller）。
/// Idle/Walk/Run 循环、Attack1 单次（运行期把 wrapMode 改成 Clamp）。<b>不使用 root motion</b>——
/// Ghoul 的四个剪辑实测都是 in-place（hips 只在原地微动、无位移曲线），位移一律由 NavMeshAgent 驱动。</para>
///
/// <para><b>踩过的坑（改代码前务必读，别回退）</b>：
/// ① 脚底高度不是常数——每个剪辑的最低点不同（Idle -0.043 / Walk +0.154 / Run -0.014 / Attack1 末帧 +0.191，
///    相对根节点，按 0.27 基准缩放实测），所以用「按剪辑固定补偿 + 运行时按缩放换算」，而不是每帧读
///    <c>SkinnedMeshRenderer.bounds</c>（蒙皮包围盒不是每帧刷新，按它逐帧纠正会来回抖）。
/// ② 落点吸附不能用大半径 <c>NavMesh.SamplePosition</c>——10 m 半径会把敌人吸到 y≈9.97 的**屋面**上；
///    现在先用 <see cref="GroundPointBelow"/> 取真实地面，再用 1.5 m 半径吸附。
/// ③ 刚 <c>AddNavMeshData</c> 的 NavMesh 当帧查不到，必须等一帧（<c>snapPending</c>）。
/// ④ 门洞走不通：climb 默认 0.4 < 门口 0.55 m 台阶 → 导航面在门口断成两岛；现在 climb 提到 0.6，
///    另加 <see cref="directApproachRange"/> 的「贴脸直冲」兜底（寻路 Partial 且直线可见时脱离 agent 直接走）。
/// ⑤ <c>NavMeshPath</c> 不能在字段初始化器里 new（Unity 会报 InitializeNavMeshPath 不允许），要在 Awake 里建。</para>
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyGhoul : MonoBehaviour
{
    private enum State { Patrol, Search, Chase }

    [Header("巡逻")]
    [Tooltip("**整张地图漫游（默认开）**：在本场景烘焙出的 NavMesh 上随机挑可达点巡游，不设点也能满地图走；" +
             "关掉它才使用下面的巡逻点路线")]
    [SerializeField] private bool roamWholeScene = true;
    [Tooltip("漫游单程最短距离（米）——太短会看起来在原地打转")]
    [SerializeField] private float roamMinDistance = 15f;
    [Tooltip("漫游单程最长距离（米）")]
    [SerializeField] private float roamMaxDistance = 70f;
    [Tooltip("巡逻点，**相对本物体**的局部坐标（会把实例的旋转/缩放一起带上）——只在关掉「整张地图漫游」时生效，" +
             "摆成 4 个点就是绕圈，2 个点就是来回")]
    [SerializeField] private Vector3[] patrolPoints = new Vector3[0];
    [Tooltip("巡逻速度 = 玩家走路速度(2 m/s) × 0.8")]
    [SerializeField] private float patrolSpeed = 1.6f;
    [Tooltip("到点判定半径（米）")]
    [SerializeField] private float arriveRadius = 0.8f;
    [Tooltip("在巡逻点停留的秒数（站着张望）")]
    [SerializeField] private float waypointPause = 1.5f;

    [Header("感知")]
    [Tooltip("视距（米）——越大越早发现玩家")]
    [SerializeField] private float viewDistance = 35f;
    [Tooltip("视野总角度（度）——只在正前方这个扇形里才会发现玩家（70 表示左右各 35）")]
    [SerializeField] private float viewAngle = 70f;
    [Tooltip("眼睛高度（米，相对根节点）")]
    [SerializeField] private float eyeHeight = 2.3f;
    [SerializeField] private LayerMask sightMask = ~0;

    [Tooltip("贴身感知半径（米）：进这个圈就不看视野角了，站它背后也会被发现（仍要求中间没有遮挡）")]
    [SerializeField] private float proximityRadius = 5f;
    [Tooltip("蹲下时视距系数（0.5 = 只在这个比例的距离内才会被看到）")]
    [SerializeField] private float crouchViewScale = 0.5f;
    [Tooltip("蹲下时贴身感知半径系数")]
    [SerializeField] private float crouchProximityScale = 0.45f;
    [Tooltip("玩家碰撞体高度低于这个值即判定为蹲下（站立 2.0 / 蹲下 1.2）")]
    [SerializeField] private float crouchDetectHeight = 1.5f;

    [Header("感知 — 发现节奏")]
    [Tooltip("贴身距离下锁定玩家所需的秒数（不是 0 帧发现：玩家闪一下不会被立刻锁死）")]
    [SerializeField] private float detectTimeNear = 0.6f;
    [Tooltip("视距边缘处锁定玩家所需的秒数（越远越慢）")]
    [SerializeField] private float detectTimeFar = 2.2f;
    [Tooltip("看不见玩家时怀疑值每秒衰减多少")]
    [SerializeField] private float suspicionDecay = 0.55f;
    [Tooltip("蹲下时发现耗时的倍率（越大越难被发现）")]
    [SerializeField] private float crouchDetectScale = 1.7f;

    [Header("搜索（丢失视线后）")]
    [Tooltip("冲到「最后看见玩家的位置」并在原地环视的总时长（秒）")]
    [SerializeField] private float searchDuration = 7f;
    [Tooltip("搜索时的移动速度相对于追击速度的比例")]
    [SerializeField] private float searchSpeedScale = 0.7f;
    [Tooltip("搜索时原地环视的转身速度（度/秒）")]
    [SerializeField] private float searchLookSpeed = 90f;
    [Tooltip("环视时左右各偏多少度")]
    [SerializeField] private float searchLookAngle = 55f;

    [Header("听觉")]
    [Tooltip("响度 1.0 的声音最远能听到多少米（其他响度按比例缩短）")]
    [SerializeField] private float hearRange = 18f;
    [Tooltip("低于这个响度的声音直接忽略（比如蹲着挪动的细微声响）")]
    [SerializeField] private float hearMinLoudness = 0.25f;
    [Tooltip("听到声音后给搜索状态的额外时长（秒）")]
    [SerializeField] private float searchDurationFromNoise = 5f;

    [Header("追击")]
    [Tooltip("追击速度 = 玩家奔跑速度(4 m/s) × 0.8：追得上走路的玩家，跑起来的玩家能甩开它")]
    [SerializeField] private float chaseSpeed = 3.2f;
    [Tooltip("重新下发追击目标点的间隔（秒）——不要每帧 SetDestination")]
    [SerializeField] private float repathInterval = 0.25f;
    [Tooltip("失去视线多久后放弃追击、就地继续巡逻（秒）")]
    [SerializeField] private float loseSightTime = 3f;
    [Tooltip("玩家超过这个距离直接脱战（米）")]
    [SerializeField] private float giveUpDistance = 50f;
    [Tooltip("转身速度（度/秒）")]
    [SerializeField] private float turnSpeed = 360f;
    [Tooltip("贴脸直冲距离（米）：寻路走不通（PathPartial/无路）但玩家在直线可视范围内时，" +
             "放弃导航直接朝他走——专门治「卡在门口进不去也出不来」")]
    [SerializeField] private float directApproachRange = 12f;

    [Header("攻击")]
    [SerializeField] private bool attackEnabled = true;
    [SerializeField] private float attackRange = 1.6f;
    [Tooltip("两次攻击的最小间隔（秒）")]
    [SerializeField] private float attackCooldown = 2.2f;
    [Tooltip("攻击动画播到第几秒才判定命中")]
    [SerializeField] private float attackHitDelay = 0.45f;
    [SerializeField] private string attackClip = "Attack1";

    [Header("动画（Ghoul 剪辑名）")]
    [SerializeField] private string idleClip = "Idle";
    [SerializeField] private string walkClip = "Walk";
    [SerializeField] private string runClip = "Run";
    [SerializeField] private float fadeTime = 0.2f;

    [Header("反应")]
    [SerializeField] private string spottedHint = "有东西看见你了";
    [Tooltip("首次发现时抬升的张力（TensionMeter.Add）")]
    [SerializeField] private float spottedTension = 0.12f;

    [Header("音效（留空则静音，不会报错）")]
    [Tooltip("呼吸/低吼循环：按距离与状态调节音量，贴得越近越响")]
    [SerializeField] private AudioClip breathClip;
    [Tooltip("发现玩家时的吼叫（一次性）")]
    [SerializeField] private AudioClip alertClip;
    [Tooltip("脚步（一次性，按移动速度触发）")]
    [SerializeField] private AudioClip stepClip;
    [Tooltip("呼吸的最大音量（贴近时）")]
    [SerializeField] private float breathMaxVolume = 0.55f;
    [Tooltip("呼吸开始能听见的距离（米）")]
    [SerializeField] private float breathHearDistance = 22f;
    [Tooltip("脚步的音量")]
    [SerializeField] private float stepVolume = 0.7f;

    [Header("运行时烘焙 NavMesh")]
    [Tooltip("整图漫游时铺开的场地边长（米）——越大巡视范围越广，烘焙也越慢")]
    [SerializeField] private float roamAreaSize = 150f;
    [Tooltip("巡逻点外扩的净空（米）")]
    [SerializeField] private float bakePadding = 8f;
    [Tooltip("烘焙场地的**最小**边长（米）——漫游需要一块像样的范围，太小会看起来在原地打转")]
    [SerializeField] private float minBakeFootprint = 40f;
    [SerializeField] private float bakeHeight = 14f;
    [Tooltip("烘焙用的代理半径（米）——只影响能不能挤进门，和可见模型的胖瘦无关。" +
             "门宽 0.9m 需要 ≤0.35；门宽 1.35m 时 0.45 也够。设太大会出现「玩家躲进屋里它进不来」")]
    [SerializeField] private float bakeAgentRadius = 0.35f;
    [Tooltip("烘焙用的代理身高（米）——必须小于场景里的门高，否则门会被判成不可通行")]
    [SerializeField] private float bakeAgentHeight = 2.0f;
    [Tooltip("烘焙用的可跨越台阶高度（米）——门槛/台阶比这个高，屋里屋外就会断成两块不连通的" +
             "导航面（表现为它卡在门口进不去也出不来）。木屋门口常见 0.3~0.6 m 台阶")]
    [SerializeField] private float bakeAgentClimb = 0.6f;
    [Tooltip("烘焙时的最大可行走坡度（度）。45° 走不上木屋屋顶的斜屋面，60° 才能顺着屋面爬上去追")]
    [SerializeField] private float bakeAgentSlope = 60f;
    [Tooltip("追击目标跑到已烘焙范围外时，补烘一块能同时罩住双方的区域（米）")]
    [SerializeField] private float maxRebakeSize = 400f;
    [Tooltip("补烘时给目标点留的余量（米）")]
    [SerializeField] private float rebakeMargin = 60f;
    [Tooltip("两次补烘的最小间隔（秒），避免一直重烘")]
    [SerializeField] private float rebakeCooldown = 2f;

    [Header("爬墙（只有本组打开时才会爬；Ghoul 用不到，蜘蛛型敌人用）")]
    [Tooltip("允许爬墙。GhoulEnemy 关着，ArachnidEnemy 打开")]
    [SerializeField] private bool canClimb = false;
    [Tooltip("爬墙时的上升速度（米/秒）")]
    [SerializeField] private float climbSpeed = 1.6f;
    [Tooltip("玩家高出自己多少米才考虑爬墙")]
    [SerializeField] private float climbMinHeight = 1.2f;
    [Tooltip("墙最高能有多高（米），超过就不爬（防对着大楼无限爬）")]
    [SerializeField] private float climbMaxHeight = 14f;
    [Tooltip("向前探墙的距离（米）——从身体后方 1.2m 起打，所以要贴到约这个距离以内才判得出墙")]
    [SerializeField] private float climbProbeDistance = 1.8f;
    [Tooltip("直立爬墙时身体离墙的间距（米）。贴墙姿势用 climbFeetGap，这个只在 climbAlignBody 关掉时生效")]
    [SerializeField] private float climbStickDistance = 0.55f;
    [Tooltip("探墙时身体露出的高度（米）")]
    [SerializeField] private float climbProbeHeight = 1.1f;
    [Tooltip("爬到顶后向墙内翻的位移（米）")]
    [SerializeField] private float climbMantleDistance = 1.1f;
    [Tooltip("爬墙时把身体翻成「贴墙」姿势（脚掌/肚皮朝墙、头朝上）。关掉就保持直立往上升，脚会悬空")]
    [SerializeField] private bool climbAlignBody = true;
    [Tooltip("贴墙姿势下脚掌与墙面留的间隙（米）：0.05~0.2 之间；太大看着悬空，太小会穿模")]
    [SerializeField] private float climbFeetGap = 0.12f;

    [Header("可见模型")]
    [Tooltip("Ghoul.prefab 实例（留空 = 自动取第一个子物体）")]
    [SerializeField] private Transform body;
    [Tooltip("可见模型在根节点下的基准高度（scene 存值）")]
    [SerializeField] private float bodyBaseY = 0.412f;
    [Tooltip("各剪辑的脚底补偿（scale=0.27 下实测；正数抬高、负数下压）")]
    [SerializeField] private float footOffsetIdle = 0.03f;
    [SerializeField] private float footOffsetWalk = -0.15f;
    [SerializeField] private float footOffsetRun = 0.01f;
    [SerializeField] private float footOffsetAttack = -0.19f;
    [Tooltip("脚底补偿的参考缩放（上述数值是在 body.localScale=0.27 时实测的），运行时会按实际缩放自动换算")]
    [SerializeField] private float footOffsetReferenceScale = 0.27f;
    [Tooltip("切换剪辑时脚底补偿的过渡速度")]
    [SerializeField] private float footBlendSpeed = 6f;

    // 当前脚底补偿（在剪辑切换时平滑过渡，避免上下跳一下）
    private float _footOffset;
    // 攻击末帧定格后再实测一次的补偿（每次攻击只测一次，避免每帧反馈造成上下抖）
    private bool attackFootMeasured;
    private float attackFootOffset;
    // 攻击态（含末帧定格观察期）到什么时候结束
    private float attackStateUntil;

    // 漫游目标点
    private Vector3 roamTarget;
    private bool roamTargetValid;

    /// <summary>共享的射线命中缓冲（避免每帧分配）。</summary>
    private static readonly RaycastHit[] s_hits = new RaycastHit[16];

    /// <summary>已烘过 NavMesh 的场景（按场景名记，一场景一份、场景内所有敌人共享，世界分批加载时各自烘焙）。</summary>
    private static readonly HashSet<string> s_bakedScenes = new HashSet<string>();

    /// <summary>各场景烘焙用的范围（漫游时从这里随机取点）。</summary>
    private static readonly Dictionary<string, Bounds> s_sceneBakeBounds = new Dictionary<string, Bounds>();

    /// <summary>各场景已加入的 NavMesh 数据句柄（补烘时要先移除旧的）。</summary>
    private static readonly Dictionary<string, NavMeshDataInstance> s_bakedData = new Dictionary<string, NavMeshDataInstance>();

    /// <summary>补烘时指定的烘焙范围（优先于按巡逻点/漫游算出来的默认范围）。</summary>
    private static readonly Dictionary<string, Bounds> s_bakeOverride = new Dictionary<string, Bounds>();

    /// <summary>复用的寻路结果（避免每次挑点时分配；NavMeshPath 只能在 Awake 之后创建）。</summary>
    private NavMeshPath _path;

    /// <summary>场景标识（WorldStreamer 分批加载不同世界时，每个场景各烘一份）。</summary>
    private string SceneKey()
    {
        return gameObject.scene.IsValid() ? gameObject.scene.name : "untitled";
    }

    /// <summary>本场景的烘焙范围（没烘过就现算一份）。</summary>
    private Bounds CurrentBakeBounds()
    {
        string sceneKey = SceneKey();
        if (s_sceneBakeBounds.TryGetValue(sceneKey, out Bounds cached)) return cached;

        Bounds fresh = SceneBakeBounds(sceneKey);
        s_sceneBakeBounds[sceneKey] = fresh;
        return fresh;
    }

    /// <summary>
    /// 目标跑出已烘焙范围时补烘一块同时罩住「自己 + 目标」的区域。
    /// 追进远处建筑、跨过整张地图都靠它——不然 NavMesh 只覆盖出生点附近，追到边界就原地卡住。
    /// </summary>
    private void ReBakeToInclude(Vector3 target)
    {
        if (Time.time < nextRebakeAt) return;
        nextRebakeAt = Time.time + Mathf.Max(0.5f, rebakeCooldown);

        string sceneKey = SceneKey();

        float minX = Mathf.Min(transform.position.x, target.x) - rebakeMargin;
        float maxX = Mathf.Max(transform.position.x, target.x) + rebakeMargin;
        float minZ = Mathf.Min(transform.position.z, target.z) - rebakeMargin;
        float maxZ = Mathf.Max(transform.position.z, target.z) + rebakeMargin;

        float sizeX = Mathf.Clamp(maxX - minX, minBakeFootprint, maxRebakeSize);
        float sizeZ = Mathf.Clamp(maxZ - minZ, minBakeFootprint, maxRebakeSize);
        Vector3 center = new Vector3((minX + maxX) * 0.5f, transform.position.y + bakeHeight * 0.35f, (minZ + maxZ) * 0.5f);

        s_bakeOverride[sceneKey] = new Bounds(center, new Vector3(sizeX, bakeHeight, sizeZ));

        if (s_bakedData.TryGetValue(sceneKey, out NavMeshDataInstance old))
        {
            if (old.valid) old.Remove();
            s_bakedData.Remove(sceneKey);
        }
        s_bakedScenes.Remove(sceneKey);
        s_sceneBakeBounds.Remove(sceneKey);

        BuildNavMeshOnce();
        snapPending = true; // 新 NavMesh 下一帧才可查询，交给 Update 重新吸附
    }

    private NavMeshAgent agent;
    private Animation anim;
    private Animator animator;
    private State state = State.Patrol;
    private int patrolIndex;
    private float pauseUntil;
    private float lastSeenAt = -999f;
    private Vector3 lastSeenPosition;
    private float nextRepathAt;
    private float nextAttackAt;
    private float attackAnimUntil;
    private string currentClip;
    private bool spottedHintShown;
    private bool snapPending;
    private bool snapWarningShown;
    private float nextRebakeAt;
    private int rebakeTries;

    // 贴脸直冲：寻路走不通（门口台阶/门框把导航面切断）时脱离 NavMeshAgent 直线逼近
    private bool directMode;
    private float chaseStartedAt;
    private float stuckTimer;      // 追击中「看得见却挪不动」的累计时间，用于切直冲兜底
    private float animSpeedOverride;

    // ── 爬墙运行时状态 ──
    private bool climbing;
    private Vector3 climbNormal;
    private float climbTargetY;
    private Vector3 climbStartPosition;
    private float nextClimbProbeAt;
    private float nextDirectExitAt;

    // 怀疑值 0~1：看见玩家后累积到 1 才真正锁定（越近越快，蹲下更慢）
    private float suspicion;
    // 搜索状态：去哪找、找到什么时候、环视基准朝向
    private Vector3 searchTarget;
    private float searchUntil;
    private float searchBaseYaw;
    private float searchLookSign = 1f;
    private float nextLookFlipAt;
    private bool searchLookInit;
    // 音效
    private AudioSource breathSource;
    private AudioSource fxSource;
    private float nextStepAt;

    private void OnEnable()
    {
        // 玩家脚步等动静经 NoiseBus（FYP.Core）中转，敌人只订阅，不认识 PlayerMovement
        NoiseBus.OnNoise += HandleNoise;
        EnsureAudio();
    }

    private void OnDisable()
    {
        NoiseBus.OnNoise -= HandleNoise;
        if (breathSource != null) breathSource.Stop();
    }

    /// <summary>
    /// 听到动静：过去看看（进入搜索），而不是直接锁定追击。
    /// 响度决定能传多远：hearRange * loudness。
    /// </summary>
    private void HandleNoise(NoiseBus.Noise noise)
    {
        if (!isActiveAndEnabled) return;
        if (state == State.Chase) return;              // 已经在追了，不用理噪声
        if (noise.Loudness < hearMinLoudness) return;  // 太轻（蹲着挪）听不见

        float range = hearRange * Mathf.Clamp01(noise.Loudness);
        if (Vector3.Distance(noise.Position, transform.position) > range) return;

        // 声音不覆盖视觉记忆：lastSeenPosition 只由真正看见时刷新
        EnterSearch(noise.Position, searchDurationFromNoise);
    }

    private void EnsureAudio()
    {
        if (breathSource == null)
        {
            breathSource = gameObject.AddComponent<AudioSource>();
            breathSource.playOnAwake = false;
            breathSource.loop = true;
            breathSource.spatialBlend = 1f;
            breathSource.rolloffMode = AudioRolloffMode.Linear;
            breathSource.minDistance = 3f;
            breathSource.maxDistance = Mathf.Max(5f, breathHearDistance);
            breathSource.volume = 0f;
            breathSource.clip = breathClip;
            if (breathClip != null) breathSource.Play();
        }

        if (fxSource == null)
        {
            fxSource = gameObject.AddComponent<AudioSource>();
            fxSource.playOnAwake = false;
            fxSource.spatialBlend = 1f;
            fxSource.rolloffMode = AudioRolloffMode.Linear;
            fxSource.minDistance = 2f;
            fxSource.maxDistance = 45f;
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || fxSource == null) return;
        fxSource.PlayOneShot(clip, volume);
    }

    /// <summary>呼吸/脚步：呼吸按距离与状态淡入淡出，脚步按移动速度定间隔。</summary>
    private void TickAudio()
    {
        if (breathSource != null && breathClip != null)
        {
            Transform player = PlayerRef.Root;
            float dist = player != null ? Vector3.Distance(transform.position, player.position) : 999f;
            float near = 1f - Mathf.Clamp01(dist / Mathf.Max(1f, breathHearDistance));
            float target = breathMaxVolume * near * near; // 平方衰减，近了才明显
            if (state == State.Chase) target = Mathf.Max(target, breathMaxVolume * 0.6f);

            breathSource.volume = Mathf.Lerp(breathSource.volume, target, Time.deltaTime * 2.5f);
            if (!breathSource.isPlaying) breathSource.Play();
        }

        if (stepClip == null || fxSource == null) return;

        float speed = Mathf.Max(agent != null ? agent.velocity.magnitude : 0f, animSpeedOverride);
        if (speed < 0.25f || Time.time < nextStepAt) return;

        PlayOneShot(stepClip, stepVolume);
        nextStepAt = Time.time + (speed > (patrolSpeed + chaseSpeed) * 0.5f ? 0.38f : 0.62f);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animation>(true);
        // 第二套模型（蜘蛛型）用的是 Animator + AnimatorController，没有 legacy Animation。
        // 两套后端都能驱动，具体走哪条由 anim 是否为 null 决定。
        animator = anim == null ? GetComponentInChildren<Animator>(true) : null;
        _path = new NavMeshPath();

        if (animator != null)
        {
            animator.applyRootMotion = false;      // 位移由 agent/脚本驱动（剪辑本身是原地动作）
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // Ghoul.prefab 的 Animation 是 playAutomatically=True、默认剪辑是 15.4s 合集，
        // 这里改成完全由本脚本按状态播单条剪辑。
        if (anim != null)
        {
            anim.playAutomatically = false;
            foreach (AnimationState st in anim)
            {
                if (st == null) continue;
                if (st.name == attackClip || st.name == "Attack2" || st.name == "Death")
                    st.wrapMode = WrapMode.Clamp;
            }
        }

        // 编辑期把 agent 关掉可以避免「not close enough to the NavMesh」刷屏；
        // 运行时等 NavMesh 烘好再打开，Warp 才不会失败。
        BuildNavMeshOnce();

        // 运行时 agent 的胖瘦必须和烘焙代理一致：比网格宽的话，它会在门口/窄道里被避障挤住
        //（表现就是「卡在门框里一动不动」）。可见模型的胖瘦由 CapsuleCollider 负责，与导航无关。
        agent.radius = bakeAgentRadius;
        agent.height = bakeAgentHeight;

        if (!agent.enabled) agent.enabled = true;

        // 新加的 NavMeshData 要到下一帧导航更新才可查询，所以吸附放到第一帧 Update 里做
        snapPending = true;
    }

    private void Start()
    {
        // 蒙皮包围盒随姿态变化（Idle/Walk/Run 各自的最低点不同，Walk 剪辑自带约 0.15m 抬腿），
        // 实测各剪辑的脚底偏差后按剪辑做固定补偿 + 平滑过渡（比每帧读包围盒稳定，不会抖）。
        if (body == null && transform.childCount > 0) body = transform.GetChild(0);
        if (body != null)
        {
            Vector3 lp = body.localPosition;
            bodyBaseY = lp.y;
        }
        _footOffset = footOffsetIdle;
    }

    /// <summary>按当前剪辑把可见模型抬/压到脚底贴地（值来自实测，见字段 Tooltip）。</summary>
    private void TickFootOffset()
    {
        if (body == null) return;

        // 攻击期间 currentClip 被清空（等 Attack1 播完），所以攻击态要额外用 attackStateUntil 判
        bool attacking = currentClip == attackClip || Time.time < attackStateUntil;

        float target;
        bool measured = false;

        if (attacking)
        {
            target = footOffsetAttack;

            // 攻击是「挥完停在末帧」：末帧的脚底比挥击过程中高出一大截（3m 体型下差约 0.34m）。
            // 剪辑播完（pose 已定格在末帧）后实测一次包围盒，把脚底精确压到地面并直接吸附到位，
            // 这次攻击内保持不再动——每帧读包围盒会来回冲，只在定格后量一次才稳。
            if (Time.time >= attackAnimUntil)
            {
                if (!attackFootMeasured)
                {
                    attackFootMeasured = true;
                    float gap = FootGapAboveGround();
                    attackFootOffset = (gap > 0.02f && gap < 2f) ? _footOffset - gap : _footOffset;
                    _footOffset = attackFootOffset; // 定格姿态不再平滑过渡，直接吸附
                }
                target = attackFootOffset;
                measured = true;
            }
        }
        else if (currentClip == runClip) target = footOffsetRun;
        else if (currentClip == walkClip) target = footOffsetWalk;
        else target = footOffsetIdle;

        // 补偿量随体型线性缩放：实测基准是 footOffsetReferenceScale，换成 1.5 倍体型后要乘同样的倍数
        if (!measured)
        {
            float scaleRatio = footOffsetReferenceScale > 0.001f
                ? body.localScale.y / footOffsetReferenceScale
                : 1f;
            target *= scaleRatio;
        }

        _footOffset = Mathf.Lerp(_footOffset, target, Time.deltaTime * footBlendSpeed);

        Vector3 lp = body.localPosition;
        lp.y = bodyBaseY + _footOffset;
        body.localPosition = lp;
    }

    /// <summary>可见模型当前脚底相对根节点的高度（正 = 悬空）。</summary>
    private float FootGapAboveGround()
    {
        if (body == null) return 0f;
        SkinnedMeshRenderer smr = body.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null) return 0f;
        return smr.bounds.min.y - transform.position.y;
    }

    private void Update()
    {
        EnsureRuntimeRefs();
        if (agent == null) return;

        // 爬墙时 agent 是关着的，不能走下面的吸附/导航流程（会把它按最低地面拽回楼下）
        if (climbing)
        {
            TickClimb();
            TickAnimation();
            TickFootOffset();
            TickAudio();
            return;
        }

        if (snapPending)
        {
            // 等一帧：刚 AddNavMeshData 的 NavMesh 这帧才可查询
            snapPending = false;
            SnapToNavMesh();
        }

        if (!directMode && !agent.isOnNavMesh)
        {
            // 补烘/换场景后可能暂时脱离导航面，先试着吸附回来；实在不行这帧不动作
            SnapToNavMesh();
            if (!agent.isOnNavMesh) return;
        }

        bool canSee = CanSeePlayer(out Vector3 seenPoint);
        if (canSee)
        {
            lastSeenAt = Time.time;
            lastSeenPosition = seenPoint;
        }

        // 看见不等于立刻锁定：怀疑值累积到 1 才真正进入追击
        TickSuspicion(canSee);

        if (state == State.Chase)
        {
            // 丢失视线 → 先去最后看见的位置找一圈，而不是原地开始漫游
            if (!canSee && Time.time - lastSeenAt > loseSightTime) EnterSearch(lastSeenPosition, searchDuration);
            else TickChase(canSee);
        }
        else if (state == State.Search)
        {
            TickSearch();
        }
        else
        {
            TickPatrol();
        }

        TickAnimation();
        TickFootOffset();
        TickAudio();
    }

    /// <summary>
    /// 怀疑值：看见就按「距离 / 是否蹲下」的速率累积，看不见就衰减。
    /// 累积满 1 才 EnterChase——这样玩家从掩体后闪一下不会被瞬间锁死，
    /// 而贴到它脸上的时候仍然几乎立刻被发现（detectTimeNear）。
    /// </summary>
    private void TickSuspicion(bool canSee)
    {
        if (canSee && PlayerRef.Root != null)
        {
            float dist = Vector3.Distance(transform.position, PlayerRef.Root.position);
            float far = Mathf.Max(proximityRadius + 0.01f, viewDistance);
            float t = Mathf.InverseLerp(proximityRadius, far, dist);
            float need = Mathf.Lerp(detectTimeNear, detectTimeFar, t);

            if (IsPlayerCrouching()) need *= Mathf.Max(1f, crouchDetectScale);
            if (state == State.Search) need *= 0.4f; // 已经在找它了，再看见几乎立刻确认

            suspicion = Mathf.Clamp01(suspicion + Time.deltaTime / Mathf.Max(0.05f, need));
        }
        else
        {
            suspicion = Mathf.Clamp01(suspicion - Time.deltaTime * suspicionDecay);
        }

        if (suspicion >= 1f && state != State.Chase) EnterChase();
    }

    // ───────────────────────────── 搜索 ─────────────────────────────

    /// <summary>丢视线/听到声音时的搜索：先走过去，再原地左右环视，超时才回巡逻。</summary>
    private void EnterSearch(Vector3 point, float duration)
    {
        ExitDirectApproach();
        state = State.Search;
        searchTarget = point;
        searchUntil = Time.time + Mathf.Max(1f, duration);
        searchLookInit = false;
        suspicion = Mathf.Max(suspicion, 0.4f);
        nextRepathAt = 0f;

        if (agent != null)
        {
            agent.enabled = true;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = chaseSpeed * Mathf.Clamp01(searchSpeedScale);
        }
    }

    private void TickSearch()
    {
        if (agent == null) return;

        if (Time.time >= searchUntil)
        {
            EnterPatrol();
            return;
        }

        Vector3 flat = searchTarget - transform.position;
        flat.y = 0f;
        bool arrived = flat.magnitude <= Mathf.Max(0.5f, arriveRadius);

        if (!arrived)
        {
            if (Time.time >= nextRepathAt)
            {
                if (!agent.enabled) agent.enabled = true;
                agent.isStopped = false;
                agent.speed = chaseSpeed * Mathf.Clamp01(searchSpeedScale);
                agent.updateRotation = true;
                agent.SetDestination(searchTarget);
                nextRepathAt = Time.time + repathInterval;
            }
            return;
        }

        // 到了：停下原地左右扫视，像在找人
        agent.isStopped = true;
        agent.updateRotation = false;
        LookAround();
    }

    /// <summary>
    /// 到了「最后看见玩家/听到声音」的位置之后原地扫视：以到达时的朝向为基准，
    /// 每隔 0.9~1.7 s 左右翻转一次，最多偏 ±55°，用 90°/s 转过去（慢慢扭头，比瞬间转身可信）。
    /// </summary>
    private void LookAround()
    {
        if (!searchLookInit)
        {
            searchLookInit = true;
            searchBaseYaw = transform.eulerAngles.y;
            nextLookFlipAt = Time.time + 1.2f;
        }

        if (Time.time >= nextLookFlipAt)
        {
            searchLookSign = -searchLookSign;
            nextLookFlipAt = Time.time + Random.Range(0.9f, 1.7f);
        }

        float targetYaw = searchBaseYaw + searchLookSign * searchLookAngle;
        Quaternion want = Quaternion.Euler(0f, targetYaw, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, want, searchLookSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 运行时引用自检。脚本改动触发的域重载（尤其 Play 中改脚本）不会重跑 Awake，
    /// 私有字段会被重置成 null，所以每个 Update 先兜一次底。
    /// </summary>
    private void EnsureRuntimeRefs()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (anim == null) anim = GetComponentInChildren<Animation>(true);
        if (anim == null && animator == null) animator = GetComponentInChildren<Animator>(true);
        if (_path == null) _path = new NavMeshPath();
    }

    /// <summary>玩家是否已经跑出「当前已烘焙的导航范围」（留 5 m 余量，避免边界上反复补烘）。</summary>
    private bool TargetOutsideBakeArea(Vector3 target)
    {
        Bounds b = CurrentBakeBounds();
        const float inset = 5f;
        return target.x < b.min.x + inset || target.x > b.max.x - inset ||
               target.z < b.min.z + inset || target.z > b.max.z - inset;
    }

    // ───────────────────────────── 巡逻 ─────────────────────────────

    /// <summary>巡逻点是局部坐标：实例被移动/旋转后路线跟着走，预制件才能直接拖进任意地图用。</summary>
    private Vector3 WorldPatrolPoint(int index)
    {
        return transform.TransformPoint(patrolPoints[index]);
    }

    /// <summary>
    /// 巡逻态的总入口：先看是否还在到点停顿里，再按 roamWholeScene 选「整图漫游」或「巡逻点巡回」。
    /// 此处不处理感知——看见了会在更外层的 TickSuspicion 里把状态推成 Chase。
    /// </summary>
    private void TickPatrol()
    {
        if (Time.time < pauseUntil)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.updateRotation = true;

        // 默认满地图漫游：不依赖手摆的巡逻点，随机挑可达点走
        if (roamWholeScene)
        {
            TickRoam();
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.isStopped = true;
            return;
        }

        Vector3 target = WorldPatrolPoint(patrolIndex);
        agent.SetDestination(target);

        Vector3 flat = target - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude <= arriveRadius * arriveRadius)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            pauseUntil = Time.time + waypointPause;
        }
    }

    /// <summary>
    /// 漫游：在本场景烘焙出的 NavMesh 上随机挑一个「走得到、且不太近」的点走过去，
    /// 到了就停 <see cref="waypointPause"/> 秒再挑下一个——所以看起来是在地图上各处巡视，不是原地转圈。
    /// </summary>
    private void TickRoam()
    {
        if (!roamTargetValid)
        {
            if (!TryPickRoamTarget(out roamTarget))
            {
                agent.isStopped = true;
                return;
            }
            roamTargetValid = true;
            agent.isStopped = false;
            agent.SetDestination(roamTarget);
            return;
        }

        agent.SetDestination(roamTarget);

        Vector3 flat = roamTarget - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude <= arriveRadius * arriveRadius)
        {
            roamTargetValid = false;
            pauseUntil = Time.time + waypointPause;
        }
    }

    /// <summary>随机挑一个漫游点：必须在本场景 NavMesh 上、距当前位置在 [roamMinDistance, roamMaxDistance] 内、且路径完整可达。</summary>
    private bool TryPickRoamTarget(out Vector3 point)
    {
        point = transform.position;

        Bounds bounds = CurrentBakeBounds();
        Vector3 from = transform.position;

        for (int i = 0; i < 24; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                from.y + 1.5f,
                Random.Range(bounds.min.z, bounds.max.z));

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;

            Vector3 flat = hit.position - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance < roamMinDistance || distance > roamMaxDistance) continue;

            if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, _path)) continue;
            if (_path.status != NavMeshPathStatus.PathComplete) continue;

            point = hit.position;
            return true;
        }

        return false;
    }

    // ───────────────────────────── 追击 ─────────────────────────────

    /// <summary>
    /// 追击态每帧：①玩家跑出 giveUpDistance 直接放弃；②够近且看得见就停下挥击；
    /// ③玩家跑出烘焙范围时补烘（有节流，别让它每帧烘）；④按 repathInterval 重设目标
    /// （不是每帧 SetDestination——那样路径会抖、性能也白花）；⑤寻路走不通就切「贴脸直冲」。
    /// </summary>
    private void TickChase(bool canSee)
    {
        Transform player = PlayerRef.Root;
        if (player == null)
        {
            EnterPatrol();
            return;
        }

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude > giveUpDistance)
        {
            ExitDirectApproach();
            EnterPatrol();
            return;
        }

        // 爬墙：目标明显在上方、而寻路走不上去时，贴墙爬。只有 canClimb=true 的敌人会走这里。
        // 必须放在「直冲模式」之前：直冲会一直顶着墙走、提前 return，永远轮不到爬墙判定。
        if (canClimb && TryStartClimb()) return;

        // 已经在导航面上了就别再直冲：翻上屋顶后 agent 是开着的，卡在直冲分支里会原地不动。
        if (directMode && agent != null && agent.enabled && agent.isOnNavMesh && TryWarpToNavMeshNear(1.2f))
        {
            directMode = false;
        }

        // 直冲模式：已经贴着走（`agent` 关着）。进到攻击距离就切回导航开打
        if (directMode)
        {
            if (flat.magnitude <= attackRange)
            {
                ExitDirectApproach();
            }
            else
            {
                TickDirectApproach(canSee);
                return;
            }
        }

        // 够得着就停下来打
        if (flat.magnitude <= attackRange && canSee)
        {
            agent.isStopped = true;
            agent.updateRotation = false;
            FaceTowards(player.position);
            TryAttack(player);
            return;
        }

        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.updateRotation = true;

        // 玩家跑出了已烘焙的导航范围 → 补烘一块罩住双方的场地，
        // 这样它能跟到地图另一头的建筑里，而不是在烘焙边界上原地打转。
        // 注意：只有「目标在烘焙范围之外」才补烘；目标在范围内却走不通（关门/夹缝）
        // 补烘是白费功夫（会每次追击连烘三遍、每遍上百毫秒），此时交给寻路自然停在最近可达点。
        bool outside = TargetOutsideBakeArea(player.position);
        if (outside && rebakeTries < 3 && Time.time >= nextRebakeAt)
        {
            rebakeTries++;
            ReBakeToInclude(player.position);
            return; // 补烘后这帧先不动，等下一帧吸附完成
        }
        if (agent.pathStatus == NavMeshPathStatus.PathComplete) rebakeTries = 0;

        if (Time.time >= nextRepathAt)
        {
            agent.SetDestination(canSee ? player.position : lastSeenPosition);
            nextRepathAt = Time.time + repathInterval;
        }

        // 导航确实走不过去（门口台阶/门框把导航面切成两岛、或整块不可达）但玩家就在直线可视距离内
        // → 放弃导航，直接朝他走过去（门洞本身是空的，能挤过去）。治「进不了门也出不了门」。
        bool blocked = agent.pathStatus == NavMeshPathStatus.PathPartial ||
                       (!agent.hasPath && !agent.pathPending && Time.time - chaseStartedAt > 0.6f);

        // 导航面到头了但不是 Partial（例如屋顶的可行走面到坡度上限就断了，目的地被夹到脚下），
        // 表现为「站着不动」。这里用「看得见、却 0.6 秒没挪窝」兜底，同样切到直冲。
        if (canSee && flat.magnitude > attackRange && agent.velocity.sqrMagnitude < 0.04f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 0.6f) blocked = true;
        }
        else
        {
            stuckTimer = 0f;
        }

        if (blocked && canSee && flat.magnitude <= directApproachRange)
        {
            EnterDirectApproach();
            TickDirectApproach(canSee);
        }
    }

    // ─────────────────────── 贴脸直冲（不走导航的兜底） ───────────────────────

    /// <summary>
    /// 关掉 NavMeshAgent 并解除导航约束。之后由 <see cref="TickDirectApproach"/> 直接推 transform，
    /// 因为门口被台阶/门框切开的导航面是「死路」，再怎么调烘焙参数也过不去。
    /// </summary>
    private void EnterDirectApproach()
    {
        if (directMode) return;
        directMode = true;
        if (agent != null)
        {
            agent.isStopped = true;
            agent.updateRotation = false;
            agent.enabled = false;
        }
    }

    /// <summary>回到导航：重新打开 agent 并把当前位置 Warp 到最近的导航面上。</summary>
    private void ExitDirectApproach()
    {
        if (!directMode) return;
        directMode = false;
        animSpeedOverride = 0f;
        if (agent == null) return;

        agent.enabled = true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            SnapToNavMesh();
        }
        agent.isStopped = false;
        agent.updateRotation = true;
    }

    /// <summary>直冲：朝玩家水平推进 + 贴地 + 短程墙检（门洞这种空口能过，实墙过不去）。</summary>
    private void TickDirectApproach(bool canSee)
    {
        Transform player = PlayerRef.Root;
        if (player == null) return;

        Vector3 target = canSee ? player.position : lastSeenPosition;
        Vector3 dir = target - transform.position;
        dir.y = 0f;

        bool moved = false;
        if (dir.sqrMagnitude > 0.0001f)
        {
            dir.Normalize();
            FaceTowards(target);

            // 只探前面 0.7 m：门洞是空的所以能过，实墙/关着的门会被挡住
            Vector3 probeFrom = transform.position + Vector3.up * 0.9f;
            bool wallAhead = false;
            RaycastHit[] hits = Physics.RaycastAll(
                probeFrom, dir, 0.7f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;         // 自己身上的碰撞体
                if (h.collider.transform.IsChildOf(player)) continue;            // 玩家自己
                if (h.normal.y > 0.4f) continue;                                 // 地面/屋顶斜坡不算墙，能顺着爬
                wallAhead = true;
                break;
            }

            if (!wallAhead)
            {
                transform.position += dir * (chaseSpeed * Time.deltaTime);
                moved = true;
            }
        }

        animSpeedOverride = moved ? chaseSpeed : 0f;
        SnapDirectToGround();
    }

    /// <summary>直冲时按实际地面（含台阶）平滑贴地，避免上台阶那一下整只跳起来。</summary>
    private void SnapDirectToGround()
    {
        // 有界射线（只在脚附近 3 m 内找），不用「最低命中」——在屋顶上会被拽到楼下地板
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            return;
        if (hit.collider != null && hit.collider.transform.IsChildOf(transform)) return;

        Vector3 p = transform.position;
        p.y = Mathf.Lerp(p.y, hit.point.y + 0.05f, 1f - Mathf.Exp(-12f * Time.deltaTime));
        transform.position = p;
    }

    /// <summary>
    /// 挥击：冷却到了才起手，播 Attack1（单次）+ 记攻击结束时间，然后交给
    /// <see cref="AttackHitRoutine"/> 在前摇末尾结算伤害（那里会再复核距离与视线）。
    /// </summary>
    private void TryAttack(Transform player)
    {
        if (!attackEnabled || Time.time < nextAttackAt) return;
        nextAttackAt = Time.time + attackCooldown;

        // 每次挥击都重新实测末帧的脚底高度（体型改了也不用改代码）
        attackFootMeasured = false;

        if (anim != null && HasClip(attackClip))
        {
            AnimationState st = anim[attackClip];
            st.wrapMode = WrapMode.Clamp;
            anim.Play(attackClip);
            attackAnimUntil = Time.time + Mathf.Max(0.2f, st.length);
            attackStateUntil = attackAnimUntil + 0.35f; // 留出末帧定格的观察期，用来实测脚底
            currentClip = null; // 攻击结束后允许 CrossFade 回移动动画
        }
        else if (animator != null && HasClip(attackClip))
        {
            // Animator 后端（蜘蛛型）：按状态名 CrossFade，时长从 runtimeAnimatorController 里取
            animator.CrossFade(attackClip, 0.08f, 0, 0f);
            attackAnimUntil = Time.time + Mathf.Max(0.2f, GetClipLength(attackClip));
            attackStateUntil = attackAnimUntil + 0.35f;
            currentClip = null;
        }

        StartCoroutine(AttackHitRoutine(player));
    }

    private IEnumerator AttackHitRoutine(Transform player)
    {
        yield return new WaitForSeconds(attackHitDelay);
        if (player == null) yield break;

        // 命中必须在落手这一帧复核：前摇 0.45s 内玩家完全可能已经跑开或缩到掩体后，
        // 否则会出现「隔着墙/隔着两米被打掉 20 电」的不讲理伤害。
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude > attackRange * 1.3f) yield break;
        if (!CanSeePlayer(out _)) yield break;

        BatterySystem battery = player.GetComponent<BatterySystem>();
        if (battery != null && !battery.IsDead) battery.TakeHit();
    }

    private void EnterChase()
    {
        if (state == State.Chase) return;
        state = State.Chase;
        nextRepathAt = 0f;
        rebakeTries = 0;
        chaseStartedAt = Time.time;
        searchLookInit = false;
        suspicion = 1f;

        if (agent != null)
        {
            agent.enabled = true;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
        }

        // 惊觉表现：先吼一声，玩家才有反应时间
        PlayOneShot(alertClip, 1f);

        if (!spottedHintShown)
        {
            spottedHintShown = true;
            HintToast.Show(spottedHint, 2.5f);
        }
        TensionMeter.Add(spottedTension);
    }

    private void EnterPatrol()
    {
        if (climbing) FinishClimb(false); // 爬一半被打断：回墙根，别挂在墙上
        ExitDirectApproach();
        state = State.Patrol;
        pauseUntil = 0f;
        attackAnimUntil = 0f;
        attackStateUntil = 0f;
        roamTargetValid = false; // 回到巡逻后重新挑一个漫游点
        searchLookInit = false;
        suspicion = Mathf.Min(suspicion, 0.25f); // 没找到人，留一点点警觉
        if (agent != null)
        {
            agent.enabled = true;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = patrolSpeed;
        }
    }

    // ───────────────────────────── 感知 ─────────────────────────────

    /// <summary>
    /// 贴身感知 + 视野角 + 射线遮挡三重判定。返回 true 时 <paramref name="point"/> 是玩家胸口位置。
    /// 取最近的非自身命中物，判断它是否属于玩家（CharacterController 本身就是 Collider）。
    /// 蹲下会同时压缩视距与贴身半径，并把瞄准点降到半蹲高度（低矮掩体后面更安全）。
    /// </summary>
    private bool CanSeePlayer(out Vector3 point)
    {
        point = Vector3.zero;

        Transform player = PlayerRef.Root;
        if (player == null) return false;

        bool crouching = IsPlayerCrouching();
        float view = viewDistance * (crouching ? crouchViewScale : 1f);
        float near = proximityRadius * (crouching ? crouchProximityScale : 1f);

        point = player.position + Vector3.up * (crouching ? 0.6f : 1.0f);
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 to = point - eye;
        float distance = to.magnitude;
        if (distance < 0.01f || distance > Mathf.Max(view, near)) return false;

        // 贴身圈内不看视野角：贴到它身上，站背后/侧面也会被察觉
        if (distance > near)
        {
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;
        }

        int count = Physics.RaycastNonAlloc(
            eye, to / distance, s_hits, distance, sightMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        Transform nearestHit = null;
        for (int i = 0; i < count; i++)
        {
            Transform hit = s_hits[i].transform;
            if (hit == null) continue;
            if (hit == transform || hit.IsChildOf(transform)) continue; // 自己
            if (s_hits[i].distance < nearest)
            {
                nearest = s_hits[i].distance;
                nearestHit = hit;
            }
        }

        return nearestHit != null && (nearestHit == player || nearestHit.IsChildOf(player));
    }

    /// <summary>蹲下判定：玩家碰撞体被压低到蹲姿高度（PlayerMovement 蹲下时 2.0 → 1.2）。</summary>
    private bool IsPlayerCrouching()
    {
        CharacterController cc = PlayerRef.Controller;
        return cc != null && cc.height <= crouchDetectHeight;
    }

    /// <summary>水平朝向玩家（只转 yaw、不低头抬头，避免模型前倾/后仰）。</summary>
    private void FaceTowards(Vector3 worldPosition)
    {
        Vector3 flat = worldPosition - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(flat.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    // ───────────────────────────── 动画 ─────────────────────────────

    private void TickAnimation()
    {
        if (anim == null && animator == null) return;
        if (Time.time < attackAnimUntil) return; // 攻击没播完

        float speed = Mathf.Max(agent != null ? agent.velocity.magnitude : 0f, animSpeedOverride);
        string clip = speed < 0.15f
            ? idleClip
            : (speed < (patrolSpeed + chaseSpeed) * 0.5f ? walkClip : runClip);

        if (clip == currentClip) return;
        if (!HasClip(clip)) return;

        currentClip = clip;
        PlayAnim(clip);
    }

    /// <summary>播放一条动画：legacy Animation 用 CrossFade，Animator 用状态名 CrossFade。</summary>
    private void PlayAnim(string clipName)
    {
        if (anim != null) anim.CrossFade(clipName, fadeTime);
        else if (animator != null) animator.CrossFade(clipName, fadeTime, 0, 0f);
    }

    private bool HasClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return false;
        if (anim != null) return anim[clipName] != null;
        if (animator != null && animator.runtimeAnimatorController != null)
            return animator.HasState(0, Animator.StringToHash(clipName));
        return false;
    }

    /// <summary>取剪辑时长：legacy 读 AnimationState.length，Animator 遍历 controller 里的剪辑。</summary>
    private float GetClipLength(string clipName)
    {
        if (anim != null && anim[clipName] != null) return anim[clipName].length;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                if (c != null && c.name == clipName) return c.length;
        }
        return 0.5f;
    }

    // ───────────────────────────── 导航 ─────────────────────────────

    /// <summary>
    /// 首次进入 Play（或该场景首次被加载）时烘焙一份 NavMesh：
    /// 范围为**本场景内所有敌人**巡逻点的并集 + 净空，之后该场景复用。
    /// </summary>
    private void BuildNavMeshOnce()
    {
        if (!Application.isPlaying) return;

        string sceneKey = SceneKey();
        if (s_bakedScenes.Contains(sceneKey)) return;
        s_bakedScenes.Add(sceneKey);

        Bounds bounds = s_bakeOverride.TryGetValue(sceneKey, out Bounds over) ? over : SceneBakeBounds(sceneKey);
        s_sceneBakeBounds[sceneKey] = bounds;
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        // 烘焙用「代理」尺寸而不是可见模型尺寸：3 m 高的身体塞不进 2 m 的门，
        // 但导航必须能追进屋里，所以这两个值单独给、可调
        settings.agentRadius = bakeAgentRadius;
        settings.agentHeight = bakeAgentHeight;
        settings.agentClimb = bakeAgentClimb;
        settings.agentSlope = bakeAgentSlope;

        var markups = new List<NavMeshBuildMarkup>();
        var sources = new List<NavMeshBuildSource>();

        // 烘焙期间临时关掉角色碰撞体，否则会在玩家/敌人脚下烘出空洞
        var disabled = new List<Collider>();
        Collider selfCollider = GetComponent<Collider>();
        if (selfCollider != null && selfCollider.enabled)
        {
            selfCollider.enabled = false;
            disabled.Add(selfCollider);
        }
        foreach (CharacterController cc in FindObjectsOfType<CharacterController>())
        {
            if (cc == null || !cc.enabled) continue;
            cc.enabled = false;
            disabled.Add(cc);
        }

        try
        {
            NavMeshBuilder.CollectSources(
                bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                settings, sources, bounds, Vector3.zero, Quaternion.identity);

            if (data != null)
            {
                s_bakedData[sceneKey] = NavMesh.AddNavMeshData(data);
                Debug.Log("[EnemyGhoul] 运行时 NavMesh 已烘焙：sources=" + sources.Count +
                          " bounds=" + bounds.center + " size=" + bounds.size +
                          " agent r=" + settings.agentRadius + " h=" + settings.agentHeight);
            }
            else
            {
                s_bakedScenes.Remove(sceneKey); // 失败允许下次重试
                Debug.LogWarning("[EnemyGhoul] NavMesh 烘焙失败，敌人不会移动", this);
            }
        }
        finally
        {
            foreach (Collider c in disabled) if (c != null) c.enabled = true;
        }
    }

    /// <summary>
    /// 烘焙范围：
    /// · 整图漫游（<see cref="roamWholeScene"/>）= 本场景所有敌人位置的包围中心 + <see cref="roamAreaSize"/> 见方，
    ///   这样裸拖进一张大地图也有足够大的巡视范围（不会被手摆的 4 个巡逻点框死在小圈里）；
    /// · 指定巡逻点 = 所有敌人巡逻点（转世界坐标）的并集 + <see cref="bakePadding"/> 净空。
    /// 一个场景只烘一次，场景里多只敌人共享同一份 NavMesh。
    /// </summary>
    private Bounds SceneBakeBounds(string sceneKey)
    {
        bool any = false;
        bool allRoam = true;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        float sumX = 0f, sumY = 0f, sumZ = 0f;
        int count = 0;

        foreach (EnemyGhoul other in FindObjectsOfType<EnemyGhoul>(true))
        {
            if (other == null) continue;
            string key = other.gameObject.scene.IsValid() ? other.gameObject.scene.name : "untitled";
            if (key != sceneKey) continue;

            count++;
            sumX += other.transform.position.x;
            sumY += other.transform.position.y;
            sumZ += other.transform.position.z;

            if (!other.roamWholeScene) allRoam = false;

            Vector3[] pts = other.patrolPoints;
            if (pts == null || pts.Length == 0)
            {
                if (!any) { min = max = other.transform.position; any = true; }
                else { min = Vector3.Min(min, other.transform.position); max = Vector3.Max(max, other.transform.position); }
                continue;
            }

            foreach (Vector3 lp in pts)
            {
                Vector3 wp = other.transform.TransformPoint(lp);
                if (!any) { min = max = wp; any = true; }
                else { min = Vector3.Min(min, wp); max = Vector3.Max(max, wp); }
            }
        }

        if (!any)
        {
            min = max = transform.position;
            sumX = transform.position.x;
            sumY = transform.position.y;
            sumZ = transform.position.z;
            count = 1;
        }

        Vector3 center = (min + max) * 0.5f;
        float avgY = count > 0 ? sumY / count : transform.position.y;

        Vector3 size;
        if (allRoam && count > 0)
        {
            // 整图漫游：以敌人群的中心为准，铺一块 roamAreaSize 见方的场地
            center = new Vector3(sumX / count, 0f, sumZ / count);
            size = new Vector3(roamAreaSize, bakeHeight, roamAreaSize);
        }
        else
        {
            size = max - min + Vector3.one * (bakePadding * 2f);
            // 指定巡逻点时也别小于最小场地，否则拖进地图会看起来在原地打转
            size.x = Mathf.Max(size.x, minBakeFootprint);
            size.z = Mathf.Max(size.z, minBakeFootprint);
            size.y = bakeHeight;
        }

        center.y = avgY + bakeHeight * 0.35f;
        return new Bounds(center, size);
    }

    /// <summary>
    /// 把敌人吸到脚下这块 NavMesh 上。
    /// 先往下扫出**真正的地面**（取最低的那个命中点，避免被箱子/平台面骗到上面去），
    /// 再在它附近 1.5 m 内找 NavMesh——不用大半径空搜，否则会吸到头顶的屋面/上层平台
    /// （FactoryDay 主厅有 y≈9.7 的屋顶与 y≈4.1 的台阶平台）。
    /// </summary>
    private void SnapToNavMesh()
    {
        Vector3 probe = GroundPointBelow(transform.position);

        if (agent != null && !agent.enabled)
        {
            // 直冲模式里 agent 是关着的，Warp 会报错，直接站到地面上
            transform.position = probe;
            return;
        }

        if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            // 没有 NavMesh 也至少站到地面上，避免悬空
            transform.position = probe;
            if (snapWarningShown) return;
            snapWarningShown = true;
            Debug.LogWarning(
                "[EnemyGhoul] 出生点脚下没有可行走面——检查 NavMesh 烘焙范围是否盖住巡逻点", this);
        }
    }

    /// <summary>从高处往下扫，返回最低的命中点（= 该 xz 处真正的地面）。</summary>
    private Vector3 GroundPointBelow(Vector3 pos)
    {
        return GroundPointBelow(pos, true);
    }

    /// <summary>
    /// 从高处往下扫。<paramref name="takeLowest"/> = true 取最低命中（找真正的地面，出生/吸附用）；
    /// false 取最高命中（翻墙落地用：站墙头/屋顶，而不是穿到楼下）。
    /// </summary>
    private Vector3 GroundPointBelow(Vector3 pos, bool takeLowest)
    {
        Vector3 origin = pos + Vector3.up * 12f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);

        float best = takeLowest ? float.MaxValue : float.MinValue;
        Vector3 found = pos;
        Transform player = PlayerRef.Root;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue; // 跳过自己身上的碰撞体
            // 跳过玩家：翻墙落地找「最高命中」时，站在屋顶的玩家会先被算成地面
            if (player != null && (h.collider.transform == player || h.collider.transform.IsChildOf(player))) continue;
            if (takeLowest ? h.point.y >= best : h.point.y <= best) continue;
            best = h.point.y;
            found = h.point;
        }
        return found;
    }

    /// <summary>
    /// 就近找一块导航面并把 agent 拽上去（高度差超过 1.5 m 视为「不是同一层」，宁可不用）。
    /// 翻墙落地时用：墙头/屋顶如果已经烘进导航，就直接站上去。
    /// </summary>
    private bool TryWarpToNavMeshNear(float maxDistance)
    {
        if (agent == null) return false;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas)) return false;
        if (Mathf.Abs(hit.position.y - transform.position.y) > 1.5f) return false;
        // 只许「同一层」：绝不往下 Warp 太多。屋顶边上的最近导航面往往是楼下房间的地板，
        // 一旦吸附下去，人就从屋檐掉进屋里了（用户实测到的就是这个）。
        if (hit.position.y < transform.position.y - 0.6f) return false;

        agent.enabled = true;
        agent.Warp(hit.position);
        return true;
    }

    // ───────────────────────────── 爬墙 ─────────────────────────────

    /// <summary>
    /// 追玩家时试一次「开始爬墙」。四道门：够近、够高、正面有可爬的竖面、墙高在范围内。
    /// 只有 <see cref="canClimb"/> 打开（蜘蛛型敌人）才会走到这里。
    /// </summary>
    private bool TryStartClimb()
    {
        if (!canClimb || climbing) return false;
        if (Time.time < nextClimbProbeAt) return false;
        nextClimbProbeAt = Time.time + 0.4f; // 别每帧打射线

        Transform player = PlayerRef.Root;
        if (player == null) return false;

        // ① 玩家得在我们头上够多，否则正常绕路追就行
        float heightDiff = player.position.y - transform.position.y;
        if (heightDiff < climbMinHeight) return false;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.09f) return false; // 贴太近不爬（0.3 m）
        Vector3 dir = toPlayer.normalized;

        // ② 正面要有墙，且是竖面
        if (!FindClimbableWall(transform.position, dir, out RaycastHit wall)) return false;

        // 墙面法线（水平化）：它指向身体这一侧，取反就是「往墙里」的方向
        Vector3 into = wall.normal;
        into.y = 0f;
        if (into.sqrMagnitude < 0.01f) return false;
        into.Normalize();

        // ③ 量墙顶：沿着**垂直墙面**的方向往上探（不要用「朝玩家」的斜方向——
        //    斜着打射线距离会随距离变化，墙高会被量短，表现为「明明是高墙却判定不爬」）
        float topY = MeasureWallTop(transform.position, -into, wall.distance);
        float wallHeight = topY - transform.position.y;
        if (wallHeight < climbMinHeight || wallHeight > climbMaxHeight) return false;

        climbNormal = into;

        climbTargetY = topY;
        climbStartPosition = transform.position;
        climbing = true;
        ExitDirectApproach();
        agent.isStopped = true;
        agent.enabled = false; // 爬的这段完全自己驱动，避免 agent 把身体往墙里顶
        ApplyClimbPose();
        return true;
    }

    /// <summary>
    /// 找面前的墙。<paramref name="pointOnBody"/> 是身体位置，<paramref name="dir"/> 是水平朝向。
    /// **射线起点要往后退 1.2 m**：NavMeshAgent 会把身体顶进墙里，从身体中心往前打时
    /// 起点已经落在墙体内部，物理射线不打背面，永远判定「前面没墙」（实测踩过的坑）。
    /// </summary>
    private bool FindClimbableWall(Vector3 pointOnBody, Vector3 dir, out RaycastHit wall)
    {
        return FindClimbableWall(pointOnBody, dir, climbProbeHeight, out wall);
    }

    private bool FindClimbableWall(Vector3 pointOnBody, Vector3 dir, float probeHeight, out RaycastHit wall)
    {
        const float backOff = 1.2f;
        Vector3 origin = pointOnBody - dir * backOff + Vector3.up * probeHeight;
        float range = backOff + climbProbeDistance;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Ignore);
        wall = default;
        float nearest = float.MaxValue;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue; // 自己
            if (IsSelfOrPlayer(h.collider)) continue;
            if (Mathf.Abs(h.normal.y) > 0.35f) continue; // 地面/坡面不是墙
            if (h.distance >= nearest) continue;
            nearest = h.distance;
            wall = h;
            wall.distance = h.distance - backOff; // 换算回「离身体多远」
            found = true;
        }
        return found;
    }

    /// <summary>
    /// 自下往上量墙顶：每 0.4 m 打一次**垂直于墙面**的水平射线，返回最后一次仍命中同一面墙的高度。
    /// 第一次命中的距离被当作基准，后面的命中距离偏离太多就认为换面了
    /// （窗口、墙角、别的建筑都会让距离突变，不判就会把墙高量到天上去）。
    /// </summary>
    private float MeasureWallTop(Vector3 bodyPos, Vector3 dir, float unusedReference)
    {
        // 墙面上可能有窗洞/门洞：正对窗洞那一列会立刻断掉，所以沿墙横向再试两边，取最高的结果
        Vector3 tangent = Vector3.Cross(Vector3.up, dir);
        if (tangent.sqrMagnitude > 0.001f) tangent.Normalize();
        else tangent = Vector3.right;

        float best = bodyPos.y;
        for (int i = -1; i <= 1; i++)
        {
            Vector3 column = bodyPos + tangent * (i * 0.7f);
            float top = MeasureWallColumn(column, dir, bodyPos.y);
            if (top > best) best = top;
        }
        return best;
    }

    /// <summary>量一列墙面：从身体高度往上每 0.4 m 打一次垂直射线，返回最高仍命中的高度。</summary>
    private float MeasureWallColumn(Vector3 bodyPos, Vector3 dir, float bodyY)
    {
        float top = bodyY;
        float reference = -1f;
        for (float y = bodyY + 0.6f; y < bodyY + climbMaxHeight; y += 0.4f)
        {
            Vector3 from = bodyPos - dir * 1.2f;
            from.y = y;
            if (!Physics.Raycast(from, dir, out RaycastHit h, 1.2f + climbProbeDistance, ~0, QueryTriggerInteraction.Ignore))
                break;
            if (h.collider == null || IsSelfOrPlayer(h.collider)) break;
            if (Mathf.Abs(h.normal.y) > 0.35f) break;
            if (reference < 0f) reference = h.distance;
            else if (Mathf.Abs(h.distance - reference) > 0.6f) break;
            top = y;
        }
        return top;
    }

    private bool IsSelfOrPlayer(Collider c)
    {
        if (c == null) return true;
        if (c.transform.IsChildOf(transform)) return true;
        Transform p = PlayerRef.Root;
        return p != null && (c.transform == p || c.transform.IsChildOf(p));
    }

    /// <summary>爬墙的身体姿态：把局部 +Y（头）朝上、局部 +Z 压到墙面上，四肢/脚掌就贴着墙。</summary>
    private void ApplyClimbPose()
    {
        transform.rotation = climbAlignBody
            ? Quaternion.LookRotation(Vector3.up, climbNormal) // 头朝上、脚掌朝墙
            : Quaternion.LookRotation(-climbNormal, Vector3.up); // 直立面朝墙（脚会悬空）
    }

    /// <summary>每帧的攀爬推进：贴墙 → 上升 → 到顶/到时限就结束。</summary>
    private void TickClimb()
    {
        Transform player = PlayerRef.Root;
        if (player != null)
        {
            Vector3 flat = player.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude > giveUpDistance)
            {
                FinishClimb(false);
                return;
            }
        }

        // ① 贴墙：保持脚掌平面离墙一个固定间隙
        if (FindClimbableWall(transform.position, -climbNormal, out RaycastHit wall))
        {
            float standOff = climbAlignBody ? Mathf.Max(0.02f, climbFeetGap) : climbStickDistance;
            float delta = wall.distance - standOff;
            transform.position += -climbNormal * delta;
        }

        // ② 上升
        transform.position += Vector3.up * (climbSpeed * Time.deltaTime);
        ApplyClimbPose();
        animSpeedOverride = climbSpeed;

        // ③ 到顶或前方墙没了
        //    注意两件事：窗口会让贴墙射线穿过去；快到檐口时探头会先打到屋檐底面（那是斜的，被过滤掉）。
        //    所以「墙没了」要在多个高度都不再命中竖面才算，否则会在离墙顶 1 m 处误判到顶。
        if (transform.position.y >= climbTargetY - 0.05f || (!WallStillAround() && transform.position.y >= climbTargetY - 1.2f))
        {
            FinishClimb(true);
        }
    }

    /// <summary>身体附近还有没有可爬的竖面（在三个高度各探一次，任一命中就算还在）。</summary>
    private bool WallStillAround()
    {
        // 注意：探高是「相对身体」的绝对高度，不能再叠加 climbProbeHeight
        float[] probeHeights = { 0.25f, 0.75f, 1.25f };
        foreach (float h in probeHeights)
        {
            if (FindClimbableWall(transform.position, -climbNormal, h, out _)) return true;
        }
        return false;
    }

    /// <summary>
    /// 结束攀爬。<paramref name="landed"/> = true 表示翻上去了：往墙内挪一点、站在墙头/屋顶上，
    /// 有导航面就接回导航，没有就继续用直冲模式——**绝不往下吸附**（那会从屋檐掉进房间）。
    /// </summary>
    private void FinishClimb(bool landed)
    {
        // 没真的爬到顶就执行跳马，会把人在半墙高度瞬移到屋顶上去
        if (transform.position.y < climbTargetY - 0.6f) landed = false;

        climbing = false;
        animSpeedOverride = 0f;
        nextClimbProbeAt = Time.time + 0.4f;

        if (!landed)
        {
            // 回到起爬点，不要往下吸附（屋里/楼下可能是完全另一块地）
            if (agent != null && !agent.enabled && !directMode)
            {
                transform.position = climbStartPosition + Vector3.up * 0.05f;
            }
            if (agent != null)
            {
                agent.enabled = true;
                agent.updateRotation = true;
                agent.isStopped = false;
                agent.speed = chaseSpeed;
            }
            return;
        }

        // 往墙内翻一点，站到墙头/屋面上
        Vector3 target = transform.position - climbNormal * climbMantleDistance + Vector3.up * 0.6f;
        target = GroundPointBelow(target, false); // 取最高命中：站墙头，别穿到楼下
        transform.position = target + Vector3.up * 0.03f;

        if (!TryWarpToNavMeshNear(1.5f))
        {
            // 墙头/屋顶还没烘进导航：补烘一块再试
            ReBakeToInclude(transform.position);
            if (!TryWarpToNavMeshNear(2.5f))
            {
                // 还是没有导航面——留在屋顶上用直冲追，不要吸附下楼
                if (agent != null) agent.enabled = false;
                directMode = true;
                chaseStartedAt = Time.time;
                Debug.Log("[EnemyGhoul] 屋顶没有导航面，改用直冲模式继续追", this);
            }
        }

        // 恢复直立（贴墙姿势下「上」是墙法线，不能直接继续用）
        if (climbNormal.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(-climbNormal, Vector3.up);

        if (agent != null)
        {
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
        }
        if (agent != null && agent.enabled && agent.isOnNavMesh) directMode = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.8f);
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            // 巡逻点存的是局部坐标，画到世界里要给实例的位移/旋转带上
            Vector3 a = transform.TransformPoint(patrolPoints[i]);
            Vector3 b = transform.TransformPoint(patrolPoints[(i + 1) % patrolPoints.Length]);
            Gizmos.DrawWireSphere(a, 0.4f);
            Gizmos.DrawLine(a, b);
        }

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
    }
#endif
}
