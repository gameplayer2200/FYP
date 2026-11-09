# 敌人 AI（食尸鬼）实现说明

> 面向接手者。本文件记录 `<c>Assets/Scripts/World/EnemyGhoul.cs</c>` 的**为什么这样做**、全部参数、踩过的坑与验证记录。
> 改这个脚本前请先读「10. 已知限制与后续」。

---

## 1. 文件清单

| 文件 | 作用 |
|---|---|
| `Assets/Scripts/World/EnemyGhoul.cs` | 敌人 AI 本体（FYP.World）。状态机 / 感知 / 导航 / 动画 / 音效全在这一个文件里 |
| `Assets/Scripts/Core/NoiseBus.cs` | **听觉总线**（FYP.Core 静态类）。`NoiseBus.Emit(pos, loudness)` 广播，敌人订阅。放 Core 是因为 ADR-0004 的单向依赖：FYP.World 看得到 FYP.Player 的 API，但**反向不可见**，玩家脚步不能直接调敌人（同 `BillingLedger` / `EmfStorm` 的解耦模式） |
| `Assets/Prefab/Enemy/GhoulEnemy.prefab` | **可直接拖进任意场景的成品预制件**。根 = Transform + NavMeshAgent + CapsuleCollider + EnemyGhoul，子物体 `Ghoul` = 素材 `Ghoul.prefab` 的实例 |
| `Assets/Prefab/Enemy/Ghoul.prefab` | 美术素材本体（商店包：legacy `Animation` + `SkinnedMeshRenderer`，无 Animator、无 Collider）。**不要直接把它拖进场景当敌人**——没有 AI/碰撞/缩放 |
| `Assets/Scripts/Player/PlayerMovement.cs` | 末尾 `controller.Move(...)` 之后有**脚步噪音上报**（按累计行走距离触发 `NoiseBus.Emit`） |

---

## 2. 怎么用（面向关卡搭建）

1. 把 `Assets/Prefab/Enemy/GhoulEnemy.prefab` 拖进场景，**什么都不用配**就能跑：
   默认 `roamWholeScene = true`，敌人会在本场景烘焙出的 NavMesh 上随机挑可达点漫游（单程 15~70 m），
   所以不会出现「原地打转」。
2. 想让它走固定路线：关掉「整张地图漫游」，在 `patrolPoints` 里填**相对本物体的局部坐标**
   （摆 4 个点 = 绕圈，2 个点 = 来回；旋转实例时路线会跟着转）。
3. 一个场景可以拖多只：它们**共享同一次 NavMesh 烘焙**（烘焙范围取本场景所有敌人位置的并集/外框）。
4. 敌人只在 **Play 模式**动（NavMesh 是运行时烘焙的）。编辑模式选中实例时会用 Gizmo 画出巡逻点与视距圈，方便摆位。
5. 音效留空即静音，不会报错；想加声音就把 `breathClip` / `alertClip` / `stepClip` 填上。

---

## 3. 状态机与每帧流程

```
Patrol ──怀疑值满 1──> Chase ──丢视线超过 loseSightTime──> Search ──超时──> Patrol
                          ↑                                    │
                          └──── 搜索中再次看见（确认更快）──────┘
```

`Update()` 的执行顺序（**这个顺序是有意义的，别打乱**）：

1. `CanSeePlayer(out point)` —— 只负责「这一帧看不看得见」，**不再直接触发追击**；
   看得见就刷新 `lastSeenAt` / `lastSeenPosition`（视觉记忆，听觉不会覆盖它）。
2. `TickSuspicion(canSee)` —— 累积/衰减怀疑值，满了才 `EnterChase()`。
3. 按状态分发：`Chase`（丢视线超时 → `EnterSearch(lastSeenPosition, searchDuration)`）/ `Search` / `Patrol`。
4. `TickAnimation()` / `TickFootOffset()` / `TickAudio()`。

三个状态的职责：

| 状态 | 行为 | 速度 |
|---|---|---|
| `Patrol` | 漫游（`TickRoam`）或按巡逻点巡回（`TickPatrol`）；到点停下张望 `waypointPause` 秒 | `patrolSpeed` 1.6 |
| `Search` | 走到「最后看见玩家的位置 / 声音来源」，到了就原地左右扫视（`LookAround`，±55°、90°/s）；`searchDuration` 秒后回巡逻 | `chaseSpeed × searchSpeedScale` ≈ 2.24 |
| `Chase` | 每 `repathInterval` 秒重设一次目标（不是每帧 `SetDestination`，防路径抖动）；进 `attackRange` 停下挥击 | `chaseSpeed` 3.2 |

---

## 4. 感知：四条通道

四条通道是**与**关系里最外层的门：任何一条为真都要再过遮挡射线，所以不会隔墙发现玩家。

| 通道 | 判定 | 参数 |
|---|---|---|
| ① 视觉扇形 | 距离 ≤ `viewDistance` **且** 与 `transform.forward` 的水平夹角 ≤ `viewAngle/2` | 35 m / 70°（左右各 35°） |
| ② 贴身 | 距离 ≤ `proximityRadius` 时**不看角度**（站背后也会被发现），仍要求无遮挡 | 5 m |
| ③ 遮挡 | 从 `eyeHeight` 高度的眼睛，向玩家胸口（或蹲下时的 0.6 m 高度）打 `Physics.RaycastNonAlloc`，取最近的**非自身**命中物，必须属于玩家才算看见 | `sightMask = ~0` |
| ④ 听觉 | 订阅 `NoiseBus.OnNoise`：`loudness ≥ hearMinLoudness` 且 `距离 ≤ hearRange × loudness` → **进入 Search（去查看）**，不是直接锁定 | 18 m / 0.25 |

**蹲下（`PlayerMovement.SetCrouch` 把 CharacterController 高度 2.0 → 1.2）**：
- 视距乘 `crouchViewScale` 0.5（35 → 17.5 m）
- 贴身半径乘 `crouchProximityScale` 0.45（5 → 2.25 m）
- 发现耗时乘 `crouchDetectScale` 1.7
- 瞄准点从 1.0 m 降到 0.6 m（矮掩体后面更安全）
- 判定方式：`PlayerRef.Controller.height <= crouchDetectHeight(1.5)` —— **不去读 `PlayerMovement` 的私有 `crouching` 字段**（跨程序集私有字段不该被外部依赖）

**发现节奏（`TickSuspicion`）**：看见不等于立刻锁定。

```
need = Lerp(detectTimeNear=0.6, detectTimeFar=2.2, InverseLerp(proximityRadius=5, viewDistance=35, 距离))
      × (蹲下 ? 1.7 : 1) × (Search 态 ? 0.4 : 1)
suspicion += dt / need      // 看不见时 -= dt × suspicionDecay(0.55)
suspicion ≥ 1 → EnterChase()
```

实测：20 m 正视约 **1.9 s** 锁定（贴脸约 0.6 s）。这样玩家从掩体后闪一下不会被瞬间锁死，
而贴脸时仍然几乎立刻被发现。

---

## 5. 导航：运行时烘焙

工程里 7 个场景的 `NavMeshSettings` 全部指向**悬空 guid** `d2fc871308fdb5747b1bc6b5cefdaf00`
（Assets 下无对应资产，从未烘焙过），所以不能指望编辑器烘焙的 NavMesh。

- 首次 Play / 场景被 `WorldStreamer` 加载时 `BuildNavMeshOnce()`：
  `NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, empty, sources)`
  → `NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity)`
  → `NavMesh.AddNavMeshData(data)`（缓存进静态 `s_bakedData`，key = 场景名）。
- **烘焙代理尺寸与可见体型解耦**：`bakeAgentRadius=0.35` / `bakeAgentHeight=2.0` / `bakeAgentClimb=0.6`。
  可见体型是玩家的 1.5 倍（3 m 高、CC 1.2 m 宽），但按那个尺寸烘焙会把 1.35 m 宽的门洞判成不可通行。
- **烘焙范围**：
  - 本场景所有敌人都是「整图漫游」→ 中心取各敌人位置平均，边长 = `roamAreaSize`(150)；
  - 否则取「所有敌人 patrolPoints 并集 + `bakePadding`(8)」，且边长夹在 `[minBakeFootprint=40, maxRebakeSize=400]`。
- **落点吸附**（`SnapToNavMesh`）：先用 `GroundPointBelow(pos)` 从 `pos + up*12` 向下 `RaycastAll` 取**最低命中点**
  当真实地面（跳过自身子碰撞体），再用 `NavMesh.SamplePosition(probe, 1.5f)` 吸附。
  —— 半径不能给 10 m：那会把敌人吸到 y≈9.97 的**屋面**上（主厅屋顶 y=9.69），曾导致两只敌人跑到房顶巡逻。
- 刚 `AddNavMeshData` 的 NavMesh **当帧查不到**，所以 `Awake` 只置 `snapPending = true`，`Update` 里等一帧再吸附。
- **补烘**：追击中若玩家跑到烘焙范围外，`ReBakeToInclude(player.position)` 重建一份更大的（并集 + `rebakeMargin=60`），
  节流 `rebakeCooldown=2` s、上限 `rebakeTries < 3`。**只在「目标在范围外」时补烘**——早期版本把
  `pathStatus == PathPartial` 也算进去，结果每次追击连烘 3 次（每次上百 ms，明显卡顿）。

**「贴脸直冲」兜底（`directApproachRange` = 12 m）**：寻路 `PathPartial`、或 0.6 s 内一直没拿到路径，
且玩家直线可见时，直接 `agent.enabled = false`，用 Transform 朝玩家平移（前方 0.7 m 探一次障碍，
门洞能过、实墙过不去），进到攻击距离或丢视线后 `ExitDirectApproach()` 再 `Warp` 回 NavMesh。
**这是兜底**：真正的门洞不通问题已经由 `bakeAgentClimb=0.6` 解决了（见 §9）。

---

## 6. 动画与脚底贴地

legacy `Animation`（不是 Animator），四个可用剪辑：`Idle` 3.75 s / `Walk` 2.50 s / `Run` 2.46 s / `Attack1` 1.67 s。
`Awake` 里把 `Attack1`/`Attack2`/`Death` 的 `wrapMode` 改成 `Clamp`（素材里全被设成 Loop）。
`TickAnimation` 按 `Mathf.Max(agent.velocity.magnitude, animSpeedOverride)` 选 Idle/Walk/Run，
阈值 `(patrolSpeed + chaseSpeed) / 2` 以上播 Run。**每帧 `SetFloat` 那种做法这里没有**——克隆剪辑会泄漏。

**脚底补偿**：`Ghoul` 每个剪辑的最低点都不一样，而且**不是常数**（蒙皮包围盒不是每帧刷新，
逐帧读 `SkinnedMeshRenderer.bounds` 去纠正会来回抖 0.2 m）。所以改成「按剪辑给固定补偿」：

| 剪辑 | 实测最低点（相对根节点，按 scale 0.27 基准） | 补偿字段 |
|---|---|---|
| Idle | min −0.043 / max 0.004 | `footOffsetIdle = 0.03` |
| Walk | min +0.154 / max +0.253 | `footOffsetWalk = −0.15` |
| Run | min −0.014 / max +0.088 | `footOffsetRun = 0.01` |
| Attack1 | min +0.008 / max **+0.191**（末帧定格最高） | `footOffsetAttack = −0.19` |

补偿值按 `body.localScale.y / footOffsetReferenceScale(0.27)` **线性换算**，改体型不用重测；
切换时 `Mathf.Lerp(..., dt * footBlendSpeed=6)` 平滑，避免瞬跳。
攻击是特例：挥击中用 `footOffsetAttack`，等 `attackAnimUntil` 到了（姿势定格）再**实测一次**真实落差并吸附，
每次挥击重置 `attackFootMeasured`。

---

## 7. 音效

运行时自动 `AddComponent<AudioSource>()` 两个（不需要预制件预先挂）：

- `breathSource`（循环，3D 线性衰减）：音量 = `breathMaxVolume × (1 − 距离/breathHearDistance)²`，
  追击时抬到至少 60%（「听见它就在附近」）。用来给玩家压力，不依赖玩家转头。
- `fxSource`（一次性）：`alertClip`（发现瞬间吼一声，给玩家反应时间）、`stepClip`（按速度定间隔 0.38/0.62 s）。

`breathClip` / `alertClip` / `stepClip` 留空 → 静默，不报错。

---

## 8. 当前数值（`Assets/Prefab/Enemy/GhoulEnemy.prefab` 序列化值）

| 组 | 字段 | 值 | 说明 |
|---|---|---|---|
| 体型 | `Ghoul` 子物体 `localScale` | 0.423 | 成品身高 3.0 m = 玩家 2.0 m 的 **1.5 倍** |
| 体型 | 子物体 `localPosition.y` | 0.645 | 素材原点不在脚底，先垫到位，再叠脚底补偿 |
| 碰撞 | CapsuleCollider | h 3.0 / r 0.6 / center.y 1.5 | 与可见体型一致 |
| 导航 | NavMeshAgent | h 2.9 / r 0.6 / accel 16 / angularSpeed 360 | 运行期会被对齐成烘焙代理尺寸 |
| 巡逻 | `roamWholeScene` | true | 整图漫游；关掉才走 patrolPoints |
| 巡逻 | `roamMinDistance` / `roamMaxDistance` | 15 / 70 | 单程距离，太短像原地打转 |
| 巡逻 | `patrolSpeed` / `chaseSpeed` | 1.6 / 3.2 | = 玩家走 2 / 跑 4 的 **0.8 倍**（走路的玩家会被追上，奔跑的能甩开） |
| 感知 | `viewDistance` / `viewAngle` | 35 / 70 | 只发现正前方 ±35° |
| 感知 | `proximityRadius` | 5 | 圈内不看角度（背后也能发现） |
| 感知 | `loseSightTime` / `giveUpDistance` | 3 / 50 | 丢视线 3 s 转搜索；距离超 50 m 直接放弃 |
| 感知 | `detectTimeNear` / `detectTimeFar` | 0.6 / 2.2 | 发现耗时（近 / 远） |
| 搜索 | `searchDuration` / `searchSpeedScale` | 7 / 0.7 | 搜索时长 / 速度比例 |
| 听觉 | `hearRange` / `hearMinLoudness` | 18 / 0.25 | 响度 1.0 传 18 m；0.25 以下忽略 |
| 攻击 | `attackRange` / `attackCooldown` / `attackHitDelay` | 1.6 / 2.2 / 0.45 | 挥击前摇 0.45 s，命中前复核 |
| 烘焙 | `bakeAgentRadius/Height/Climb` | 0.35 / 2.0 / **0.6** | 与可见体型解耦；climb 必须 > 0.55（门口台阶） |
| 烘焙 | `roamAreaSize` / `maxRebakeSize` / `rebakeMargin` | 150 / 400 / 60 | 漫游烘焙外框边长 / 补烘上限 / 补烘留边 |
| 直冲 | `directApproachRange` | 12 | 寻路走不通时的直线逼近距离上限 |

---

## 9. 已修的 bug 与根因（时间线，改前必读）

1. **Agent 一直是 disabled** —— 编辑期故意关掉 agent（避免「not close enough to the NavMesh」刷屏），
   `Awake` 里烘完必须显式 `agent.enabled = true` 再 `SnapToNavMesh()`。
2. **模型悬空 0.2~0.4 m** —— 见 §6：脚底不是常数，改成按剪辑固定补偿（试过「每帧按包围盒锁地」，
   实测 `rel` 在 −0.22~+0.27 之间震荡，因为蒙皮包围盒不是每帧刷新）。
3. **敌人在房顶巡逻** —— `SamplePosition` 10 m 半径吸到了屋面（y≈9.97）。改成先 `GroundPointBelow`
   取最低地面、再 1.5 m 半径吸附，并把吸附推迟一帧。
4. **进不了门 / 出不了门**（用户实测）：门口有 **0.55 m 台阶**（屋外 Terrain y=2151.08，屋内地板 y=2151.63），
   而烘焙写死 `agentClimb = 0.4` → 导航面在门口断成两岛，`CalculatePath` 恒为 `PathPartial`。
   对照实验：`climb=0.4` 无论半径 0.45/0.35/0.30/0.20 都 `PathPartial`；**`climb=0.6` → `PathComplete`**。
   修法 = `bakeAgentClimb` 提到 0.6 + 运行期把 agent 尺寸对齐烘焙代理（此前网格按 0.35 烘、agent 按 0.6 跑，
   比网格还宽，会在窄门口被避障挤死）+ 「贴脸直冲」兜底。
5. **补烘暴走卡顿** —— 见 §5，`PathPartial` 也触发补烘 → 每次追击连烘 3 次；改成只在目标超范围时补烘 + 节流。
6. **0 帧锁定 / 丢人就原地漫游** —— 加了怀疑值累积、`Search` 状态与「去最后可见点环视」。
7. **隔墙掉电** —— `AttackHitRoutine` 前摇结束时**复核距离（≤ attackRange×1.3）+ 视线**，跑开或躲掩体后不掉电。
8. **进房间后追不到（Turn 8 之前）** —— 旧的 `loseSightTime` 一到就 `EnterPatrol`，现在先去最后可见点搜索 7 s。

---

## 10. 已知限制与后续

- **`s_bakedData` 没有 `OnDestroy` 释放**：运行期重复进出场景会残留 `NavMeshDataInstance`
  （一次量级很小，但长会话会累积）。做正式版时应按场景引用计数后 `Remove()`。
- **烘焙在主线程**：300×300 场景实测 175 ms、150×150 约 30 ms。大地图首次进入会有一帧顿挫。
  后续可考虑 `NavMeshBuilder.UpdateNavMeshDataAsync` 或分帧。
- **遮挡只打单点**（玩家胸口/蹲下 0.6 m）：贴着掩体只露头时可能误判。可扩成头/胸/脚 3 点采样取最优。
- **`sightMask = ~0`**：含 `Ignore Raycast(2)` 与 `UI(5)` 层，严格说不该参与遮挡判定。
- **转身 360°/s** 偏快，追击时贴脸转身像瞬间平移，可降到 180~240°/s。
- **伤害写死 20**（`BatterySystem.TakeHit()` 内部 `hitDrain`），没有受击反馈（镜头抖 `CameraShake.Impulse`
  必须在 `LateUpdate` 施加，因为 `PlayerMovement.Update` 每帧覆盖相机位置）。
- **玩家没有反制手段**：没有躲藏点、没有驱散道具、没有「装死」。设计上敌人目前是纯压迫来源。
- 3 m 身高塞不进 2.106 m 的门洞，**不会跟玩家穿门**回实验室（这是有意为之）。

---

## 11. 怎么在 Play 里验（避坑）

- 取玩家一律 `PlayerRef.Root`（域重载后会变 null，别缓存）；`PlayerRef.Teleport(pos, rot)` 会临时禁用 CharacterController。
- **别用 `Teleport` 把玩家硬塞进建筑内部的贴墙位置**：CharacterController 会因穿插被挤出并一路下坠
  （实测 y 从 2147 掉到 1166、再到 −4229）。判断「能不能走进去」要看导航网格本身，不要靠「让玩家走进去」。
- 反射调私有成员：`typeof(EnemyGhoul).GetField("state", NonPublic|Instance)`；
  `EnterChase` 在**父类 `MonoBehaviour`** 上，`GetMethod("EnterChase")` 会 `AmbiguousMatch`，
  要带参数类型精确匹配。
- 逐帧采样别用 `Thread.Sleep`（卡主线程），用
  `UnityEditor.EditorApplication.CallbackFunction tick = null; tick = delegate { ... }; EditorApplication.update += tick;`
- 编辑器失焦时 Play 不推进，测试要么聚焦窗口，要么反射手动驱动 `Update`。

**已跑过的验证记录**（Flooded_Grounds 场景、两只实例）：
发现 26 m 处约 1.9 s 锁定 → 追到 6.9 m；丢视线 3 s 转 `Search` 并走到最后可见点、原地环视、7 s 后回 `Patrol`；
`NoiseBus.Emit` 0.6 响度 9.4 m 外 → `Search`（目标点精确等于噪音点），0.2 响度与 30 m 外均被忽略；
挥击前摇中把玩家瞬移 6 m 外 → 电量 30 掉到 0 之外**没有掉电**（复核拦截生效）；控制台 0 error。

---

## 12. 第二只敌人：蜘蛛型（Arachnid）与爬墙

第二只敌人用的是 `Assets/Prefab/Enemy/enemy2/` 里的 Arachnid 素材包，与第一只共用同一份 `Assets/Scripts/World/EnemyGhoul.cs`（同一套状态机/感知/听觉/漫游），只在预制件上拨开关。

### 12.1 文件与尺寸

| 文件 | 说明 |
|---|---|
| `Assets/Prefab/Enemy/enemy2/ArachnidEnemy.prefab` | 成品，拖进地图即用（`canClimb=true`） |
| `Assets/Prefab/Enemy/enemy2/Arachnid.prefab` | 模型（原 `SKM_ArachnidAlienHead_Lite.prefab`） |
| `Assets/Prefab/Enemy/enemy2/ArachnidEnemy.controller` | 4 状态动画机：`Idle`(WalkFW@0.35) / `Walk`(WalkFW) / `Run`(WalkFW4@1.25) / `Attack`(Attack4) |
| `Assets/Prefab/Enemy/enemy2/ArachnidMaterial.mat` | 自建 Built-in Standard 材质（原包是 URP/HDRP，本工程没装 URP，直接用会粉红） |

- 体型：模型原生高 0.64 m，`Arachnid` 子物体 `localScale = 2.5` → 实测 1.61 m ≈ 玩家 2.0 m 的 **0.8 倍**；`localPosition.y = 0.475`（= 0.19 × 2.5）让脚底贴地。
- 速度：`patrolSpeed = 1.8`（0.9 × 玩家走 2）、`chaseSpeed = 3.6`（0.9 × 玩家跑 4）。
- 碰撞/导航：`CapsuleCollider` center (0, 0.8, 0) / h 1.6 / r 0.5；`NavMeshAgent` h 1.6 / r 0.4。

### 12.2 爬墙：四个判定条件

`TickChase` 里、**在「贴脸直冲」分支之前**调 `TryStartClimb()`（顺序错了就永远轮不到爬墙——直冲分支会 return）：

1. `player.y - transform.position.y >= climbMinHeight`（玩家真的在上面）；
2. 与玩家的水平距离 ≥ 0.3 m；
3. `FindClimbableWall` 命中一面**竖直**的面（`|normal.y| <= 0.35`）——注意射线要**从身体后方 1.2 m 起打**：NavMeshAgent 会把身体顶进墙里，从身体中心往前打时起点落在墙体内部，物理射线不打背面，会永远判成「前面没墙」；
4. `MeasureWallTop` 量出的墙高落在 `[climbMinHeight, climbMaxHeight]`。

两个量墙的坑：
- **必须沿垂直墙面方向量**（`-into`），不能朝玩家方向量——斜着量的距离随角度变化，会把 3.9 m 的墙量成 1.1 m。
- 墙上有**窗洞**时正对窗洞那一列会立刻探空，所以 `MeasureWallTop` 沿墙切线取 `-0.7 / 0 / +0.7` **三列**各量一次取最高。

### 12.3 攀爬姿势（「脚要贴墙」）

`ApplyClimbPose()`：`Quaternion.LookRotation(Vector3.up, climbNormal)`。

即让局部 **+Y（原本朝上）对准墙面法线**——身体翻转 90°，脚的平面压住墙面、头朝上爬。旧写法 `LookRotation(-normal, up)` 是直立面朝墙，脚在下方悬空。

- `climbFeetGap = 0.12`：贴墙姿势下脚掌与墙面的间隙（0.05~0.2 之间调）。
- `climbStickDistance = 0.55` 只给「直立爬」用；两者不要混。
- 想关掉翻转变直立爬：`climbAlignBody = false`。

### 12.4 翻上墙头落地

`FinishClimb(true)` 的顺序是：先向墙内挪 `climbMantleDistance(1.1)` + 上抬 0.6 m → `GroundPointBelow(target, false)`（**取最高命中**，站墙头而不是穿到楼下）→ `TryWarpToNavMeshNear(1.5f)`；失败就 `ReBakeToInclude(transform.position)` 把墙头/屋面烘进导航，再 `TryWarpToNavMeshNear(2.5f)` 试一次。

- `TryWarpToNavMeshNear` 有两条闸：高度差 ≤ 1.5 m，且**不许往下超过 0.6 m**（否则会从屋檐掉进房间）。
- **这里绝对不能调 `SnapToNavMesh()`**：那个按「最低地面」吸附，会把站在屋顶的身体一路拽回楼下。
- 两次 Warp 都失败（屋面没有导航面）时，置 `directMode = true` 留在屋顶用直冲继续追，而不是吸附下楼。

### 12.5 追到屋顶：屋檐那一角为什么会钻进房间

| # | 现象 | 根因 | 修法 |
|---|---|---|---|
| 1 | 站着不爬 | `MeasureWallTop` 的「同一平面」基准首次为 0，第一步就判成换面，墙高量成 1.1 m < `climbMinHeight` | 基准改由函数内部首个命中距离给出；三列取最高 |
| 2 | 在地面就跳马 | `wallGone` 在墙根为真时 `FinishClimb(true)`，而最高命中找到了屋顶 | `if (transform.position.y < climbTargetY - 0.6f) landed = false;` |
| 3 | 从屋檐掉进房间 | 落地走 `SnapToNavMesh()`（最低地面） | 改 `TryWarpToNavMeshNear` + 补烘兜底 |
| 4 | 翻上屋顶后又掉下去 | `ExitDirectApproach()` 找不到导航面时回落到 `SnapToNavMesh()` | 只在 Warp 成功时才退出直冲，失败保持直冲并节流 0.5 s |
| 5 | 屋顶上被拽到楼下 | `SnapDirectToGround()` 用最低命中 | 改有界射线（从身体 +0.6 m 往下 3 m） |
| 6 | 卡在屋面不动 | 直冲模式把屋顶斜坡当墙 | 前探过滤 `normal.y > 0.4` |
| 7 | 爬到窗口就「到顶」 | 贴墙射线穿窗而过，`wallGone` 误真 | `wallGone` 加高度下限 + `WallStillAround()` 三个高度各探一次 |
| 8 | 上了屋顶却静止 | 屋面坡度 > `agentSlope 45`，导航面在檐口断掉且 `pathStatus=PathComplete`（不触发 Partial 兜底） | `bakeAgentSlope = 60` + 「看得见却 0.6 s 没挪窝」切直冲 |

**实测（真实时间，非 Step 驱动）**：玩家站屋顶 (399, 2157.2, 299.8)，敌人放院子 (399, 2151.24, 313.0)，相距 14.5 m 且 `canSee=true` → 约 5 s 后 `enemy=(399.02, 2156.94, 300.67)`、`state=Chase`、`climbing=false`、`onNav=true` —— 自动发现 → 追到墙根 → 爬墙 → 翻上屋面并接回导航。
截图：`Assets/Screenshots/roof_chase_final.png`。

### 12.6 在 Play 里调爬墙的两个坑

- **`EditorApplication.Step()` 驱动时 NavMeshAgent 走得极慢**（0.17 m/s，正常 1.8 m/s），只适合验证状态机分支；「自然追上来 + 爬墙」必须走真实时间（不 Step、`isPaused=false`）。
- `refresh_unity` 之后 `PlayerRef.Root` 会变 null（域重载清空静态注册表），爬墙判定直接 return false，看起来像「不爬」。测试前先用反射 `PlayerRef.Register(...)` 重注册。
