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
    [Tooltip("巡逻点，**相对本物体**的局部坐标（会把实例的旋转/缩放一起带上）——把预制件拖进任何地图后" +
             "只需改这组点就能定路线，摆成 4 个点就是绕圈，2 个点就是来回；留空 = 原地待机")]
    [SerializeField] private Vector3[] patrolPoints = new Vector3[0];
    [Tooltip("巡逻速度 = 玩家走路速度(2 m/s) × 0.8")]
    [SerializeField] private float patrolSpeed = 1.6f;
    [Tooltip("到点判定半径（米）")]
    [SerializeField] private float arriveRadius = 0.8f;
    [Tooltip("在巡逻点停留的秒数（站着张望）")]
    [SerializeField] private float waypointPause = 1.5f;

    [Header("感知")]
    [SerializeField] private float viewDistance = 13f;
    [Tooltip("视野总角度（度）——100 表示左右各 50")]
    [SerializeField] private float viewAngle = 100f;
    [Tooltip("眼睛高度（米，相对根节点）")]
    [SerializeField] private float eyeHeight = 2.3f;
    [SerializeField] private LayerMask sightMask = ~0;

    [Header("追击")]
    [Tooltip("追击速度 = 玩家奔跑速度(4 m/s) × 0.8：追得上走路的玩家，跑起来的玩家能甩开它")]
    [SerializeField] private float chaseSpeed = 3.2f;
    [Tooltip("重新下发追击目标点的间隔（秒）——不要每帧 SetDestination")]
    [SerializeField] private float repathInterval = 0.25f;
    [Tooltip("失去视线多久后放弃（秒）")]
    [SerializeField] private float loseSightTime = 6f;
    [Tooltip("玩家超过这个距离直接脱战（米）")]
    [SerializeField] private float giveUpDistance = 18f;
    [Tooltip("转身速度（度/秒）")]
    [SerializeField] private float turnSpeed = 360f;

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
    [Tooltip("巡逻点外扩的净空（米）")]
    [SerializeField] private float bakePadding = 8f;
    [SerializeField] private float bakeHeight = 14f;

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

    /// <summary>共享的射线命中缓冲（避免每帧分配）。</summary>
    private static readonly RaycastHit[] s_hits = new RaycastHit[16];

    /// <summary>已烘过 NavMesh 的场景（按场景名记，一场景一份、场景内所有敌人共享，世界分批加载时各自烘焙）。</summary>
    private static readonly HashSet<string> s_bakedScenes = new HashSet<string>();

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

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animation>(true);

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
        if (snapPending)
        {
            // 等一帧：刚 AddNavMeshData 的 NavMesh 这帧才可查询
            snapPending = false;
            SnapToNavMesh();
        }

        if (agent == null || !agent.isOnNavMesh) return;

        bool canSee = CanSeePlayer(out Vector3 seenPoint);
        if (canSee)
        {
            lastSeenAt = Time.time;
            lastSeenPosition = seenPoint;
            if (state != State.Chase) EnterChase();
        }
        else if (state == State.Chase && Time.time - lastSeenAt > loseSightTime)
        {
            EnterPatrol();
        }

        if (state == State.Chase) TickChase(canSee);
        else TickPatrol();

        TickAnimation();
        TickFootOffset();
    }

    // ───────────────────────────── 巡逻 ─────────────────────────────

    /// <summary>巡逻点是局部坐标：实例被移动/旋转后路线跟着走，预制件才能直接拖进任意地图用。</summary>
    private Vector3 WorldPatrolPoint(int index)
    {
        return transform.TransformPoint(patrolPoints[index]);
    }

    private void TickPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.isStopped = true;
            return;
        }

        if (Time.time < pauseUntil)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.updateRotation = true;

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
            EnterPatrol();
            return;
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

        if (Time.time >= nextRepathAt)
        {
            agent.SetDestination(canSee ? player.position : lastSeenPosition);
            nextRepathAt = Time.time + repathInterval;
        }
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

        if (!spottedHintShown)
        {
            spottedHintShown = true;
            HintToast.Show(spottedHint, 2.5f);
        }
        TensionMeter.Add(spottedTension);
    }

    private void EnterPatrol()
    {
        state = State.Patrol;
        pauseUntil = 0f;
        attackAnimUntil = 0f;
        attackStateUntil = 0f;
        if (agent != null) agent.updateRotation = true;
    }

    // ───────────────────────────── 感知 ─────────────────────────────

    /// <summary>
    /// 距离 + 视野角 + 射线遮挡三重判定。返回 true 时 <paramref name="point"/> 是玩家胸口位置。
    /// 取最近的非自身命中物，判断它是否属于玩家（CharacterController 本身就是 Collider）。
    /// </summary>
    private bool CanSeePlayer(out Vector3 point)
    {
        point = Vector3.zero;

        Transform player = PlayerRef.Root;
        if (player == null) return false;

        point = player.position + Vector3.up * 1.0f;
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 to = point - eye;
        float distance = to.magnitude;
        if (distance > viewDistance || distance < 0.01f) return false;

        Vector3 flat = new Vector3(to.x, 0f, to.z);
        if (flat.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;

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

        float speed = agent.velocity.magnitude;
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

        string sceneKey = gameObject.scene.IsValid() ? gameObject.scene.name : "untitled";
        if (s_bakedScenes.Contains(sceneKey)) return;
        s_bakedScenes.Add(sceneKey);

        Bounds bounds = SceneBakeBounds(sceneKey);
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        // 用 agent 自身尺寸烘焙，体型变化后不需要改代码
        settings.agentRadius = agent != null ? agent.radius : 0.4f;
        settings.agentHeight = agent != null ? agent.height : 1.8f;
        settings.agentClimb = 0.4f;
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
                NavMesh.AddNavMeshData(data);
                Debug.Log("[EnemyGhoul] 运行时 NavMesh 已烘焙：sources=" + sources.Count +
                          " bounds=" + bounds.center + " size=" + bounds.size);
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
    /// 烘焙范围 = 本场景内所有敌人巡逻点（转世界坐标）的并集 + bakePadding 净空。
    /// 这样把一个场景里拖多只敌人时只烘一次，且每只的路线都会被覆盖。
    /// </summary>
    private Bounds SceneBakeBounds(string sceneKey)
    {
        bool any = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        float sumY = 0f;
        int count = 0;

        foreach (EnemyGhoul other in FindObjectsOfType<EnemyGhoul>(true))
        {
            if (other == null) continue;
            string key = other.gameObject.scene.IsValid() ? other.gameObject.scene.name : "untitled";
            if (key != sceneKey) continue;

            count++;
            sumY += other.transform.position.y;

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
            sumY = transform.position.y;
            count = 1;
        }

        Vector3 center = (min + max) * 0.5f;
        center.y = (count > 0 ? sumY / count : transform.position.y) + bakeHeight * 0.35f;

        Vector3 size = max - min + Vector3.one * (bakePadding * 2f);
        size.y = bakeHeight;
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
