using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 「食尸鬼」敌人：在层域里沿巡逻点巡回，视野内看见玩家即转入追击。
/// **用法 = 把 Assets/Prefab/Enemy/GhoulEnemy.prefab 拖进任意场景**：根节点自带 NavMeshAgent +
/// CapsuleCollider + 本脚本，Ghoul 模型已是正确缩放；只需在 Inspector 里调巡逻点（局部坐标，
/// 会跟随实例的位移与旋转）即可定路线。一份预制件可在一个场景拖多只，互不干扰。
/// 工程里 7 个场景的 NavMeshSettings 都指向已丢失的资产（从未烘焙过），所以每个场景首次进入 Play、
/// 或该场景被 WorldStreamer 加载时，会按**本场景全部敌人**巡逻点的并集现场烘焙一份 NavMesh（每场景一份）。
/// 动画是 legacy Animation（Ghoul.prefab 自带，不是 Animator）：Idle/Walk/Run 循环，Attack1 单次。
/// 不使用 root motion——Ghoul 的动画全是 in-place，位移一律由 NavMeshAgent 驱动。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyGhoul : MonoBehaviour
{
    private enum State { Patrol, Chase }

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
    [Tooltip("追击目标跑到已烘焙范围外时，补烘一块能同时罩住双方的区域（米）")]
    [SerializeField] private float maxRebakeSize = 400f;
    [Tooltip("补烘时给目标点留的余量（米）")]
    [SerializeField] private float rebakeMargin = 60f;
    [Tooltip("两次补烘的最小间隔（秒），避免一直重烘")]
    [SerializeField] private float rebakeCooldown = 2f;

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
    private float animSpeedOverride;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animation>(true);
        _path = new NavMeshPath();

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
            if (state != State.Chase) EnterChase();
        }
        else if (state == State.Chase && Time.time - lastSeenAt > loseSightTime)
        {
            EnterPatrol(); // 就地继续巡逻（不回到出生点）
        }

        if (state == State.Chase) TickChase(canSee);
        else TickPatrol();

        TickAnimation();
        TickFootOffset();
    }

    /// <summary>
    /// 运行时引用自检。脚本改动触发的域重载（尤其 Play 中改脚本）不会重跑 Awake，
    /// 私有字段会被重置成 null，所以每个 Update 先兜一次底。
    /// </summary>
    private void EnsureRuntimeRefs()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (anim == null) anim = GetComponentInChildren<Animation>(true);
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
        Vector3 ground = GroundPointBelow(transform.position);
        Vector3 p = transform.position;
        p.y = Mathf.Lerp(p.y, ground.y + 0.05f, 1f - Mathf.Exp(-12f * Time.deltaTime));
        transform.position = p;
    }

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

        StartCoroutine(AttackHitRoutine(player));
    }

    private IEnumerator AttackHitRoutine(Transform player)
    {
        yield return new WaitForSeconds(attackHitDelay);
        if (player == null) yield break;

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

        if (!spottedHintShown)
        {
            spottedHintShown = true;
            HintToast.Show(spottedHint, 2.5f);
        }
        TensionMeter.Add(spottedTension);
    }

    private void EnterPatrol()
    {
        ExitDirectApproach();
        state = State.Patrol;
        pauseUntil = 0f;
        attackAnimUntil = 0f;
        attackStateUntil = 0f;
        roamTargetValid = false; // 回到巡逻后重新挑一个漫游点
        if (agent != null) agent.updateRotation = true;
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
        if (anim == null) return;
        if (Time.time < attackAnimUntil) return; // 攻击没播完

        float speed = Mathf.Max(agent.velocity.magnitude, animSpeedOverride);
        string clip = speed < 0.15f
            ? idleClip
            : (speed < (patrolSpeed + chaseSpeed) * 0.5f ? walkClip : runClip);

        if (clip == currentClip) return;
        if (!HasClip(clip)) return;

        currentClip = clip;
        anim.CrossFade(clip, fadeTime);
    }

    private bool HasClip(string clipName)
    {
        return anim != null && !string.IsNullOrEmpty(clipName) && anim[clipName] != null;
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
        settings.agentSlope = 45f;

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
        Vector3 origin = pos + Vector3.up * 12f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);

        float lowest = float.MaxValue;
        Vector3 found = pos;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue; // 跳过自己身上的碰撞体
            if (h.point.y >= lowest) continue;
            lowest = h.point.y;
            found = h.point;
        }
        return found;
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
