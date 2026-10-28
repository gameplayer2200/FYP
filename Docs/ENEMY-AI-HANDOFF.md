# 敌人 AI 与模型 — 交接文档

> 面向接手的 AI agent / 组员。**先读 `AGENTS.md`（项目手册）与 `GLOSSARY.md`（术语强制），再读本文。**
> 本文所有结论均为**代码与场景实测**（2026-10-28，提交 `514d9f3`），不是推测。凡标「⚠️」的是会直接踩坑的地方。

---

## 0. 一句话前提

**当前仓库里没有任何敌人、怪物、NavMeshAgent 代码，也没有任何敌人模型。** 你是从零开始，不是来改现成的。

```
$ grep -rln "Enemy|敌人|怪物|Monster|NavMeshAgent" Assets/Scripts
Assets/Scripts/Player/BatterySystem.cs     ← 只有一行注释「受创接口：怪物攻击/危险区调用」
```

`Assets/Art/Characters/` 是**空目录**（角色素材已按体积清理移出，见 `Docs/ART-ASSETS.md`）。

所以第一件事不是写 AI，是**先定敌人是什么、长什么样、怎么动**——见 §6 的素材现状与 §7 的设计约束。

---

## 1. 这是个什么游戏（决定敌人该长什么样）

《门阈行动》是第一人称心理恐怖。玩家从实验室接单，推门进入异世界（层域），回收异常物，在归途门锁死前撤回。

**它的恐怖来源不是「被追」**，而是三件事，这决定了敌人的定位：

1. **缺席**——「幽灵进站」有进站的全部征兆（风压轰鸣、灯管波动、死灯自己亮），车永远不来。
2. **不确定**——Glitch 系统会让道具栏顺序、世界名字、听到的话偶尔撒谎一次，游戏不解释、不纠正。
3. **不可靠的环境**——灯会一组组熄灭，异常物在对面的隧道里，你只能看着它。

**因此：默认不要做「会追着你跑的怪物」。** 那会把游戏从「心理恐怖」变成「生存恐怖」，是另一种游戏。要做的话，先读 §7 的三个可选定位并与项目所有者确认。

---

## 2. 硬约束（违反会直接编译失败或运行时崩）

### 2.1 程序集分层（`Docs/adr/0004`，依赖单向，不可成环）

| 程序集 | 路径 | references |
|---|---|---|
| `FYP.Core` | `Assets/Scripts/Core/` | **（空）** |
| `FYP.Interaction` | `Assets/Scripts/Interaction/` | Core |
| `FYP.Player` | `Assets/Scripts/Player/` | Core, Interaction |
| `FYP.World` | `Assets/Scripts/World/` | Core, Interaction, Player |
| `FYP.Content` | `Assets/Scripts/Content/` | Core, Interaction（**不含 World / Player**） |

**敌人代码放哪：**

- 敌人本体、AI、感知、导航 → **`Assets/Scripts/World/`（FYP.World）**。它需要 `PlayerRef`（Core）、`HintToast`（Interaction），可能还要读玩家装备状态（`PlayerGear`，Core）——World 全部够得着。
- 如果需要新程序集（例如 `FYP.Enemy` 依赖 World），**先改 asmdef 并同步 `Docs/adr/0004`**，不要偷偷加引用。

⚠️ **不要为了图方便把敌人塞进 `FYP.Core`。** Core 是零依赖的底座，塞进去会让 Core 反向依赖 Player/World，整个分层崩掉。

### 2.2 世界图层隔离会**在运行时改写 layer**

`Assets/Scripts/World/WorldLightIsolation.cs` 在每个世界场景装载后，把该场景**全部渲染体改挂到该世界的专属图层**，并把该世界的灯收敛为只照本世界图层。

现有图层（`ProjectSettings/TagManager.asset`，编号从 0 起）：

```
0 Default            6 FirstPersonBody     13 World_Flooded_Grounds
1 TransparentFX      7 World_Metro         17 World_OfficePack
2 Ignore Raycast     8 World_FactoryDay     18 World_Shrine
4 Water              9 World_FactoryNight   19 World_Laboratory
5 UI                10 World_Flooded
                    11 World_Other
```

**对你的影响：**

- ⚠️ **不要用 `LayerMask` 硬编码世界图层来判定「敌人在哪个世界」**——运行时会被改写，而且新增世界会自动从空闲池分配图层（代码注释明确说「新增世界不用改代码」）。
- 判定「玩家和敌人在不在同一个世界」请用 **`WorldPresence.Current`**（见 §3.6）或直接比 `gameObject.scene.name`。
- 敌人的**碰撞**要用物理层（Physics）而不是渲染层（Rendering）来设计。当前 `ProjectSettings/DynamicsManager.asset` 未做特殊碰撞矩阵配置，默认全通——如果要做「敌人撞不穿墙但能穿过玩家」，需要新加一个 layer 并改矩阵，改之前先看 `WorldLightIsolation` 的自动分配逻辑会不会抢这个层。

### 2.3 玩家只有一个（双玩家守卫）

每个世界场景里都放了一个「常驻开发玩家」（`Player(Metro开发)`、`Player(工厂开发)`）。`Assets/Scripts/Player/PlayerMovement.cs` 的 `Awake` 有先到先得守卫：

```csharp
if (PlayerRef.IsReady && PlayerRef.Root != transform) { gameObject.SetActive(false); return; }
```

**运行时永远只有一个活跃玩家。** 你的敌人要拿玩家位置，**必须走 `PlayerRef.Root`，不要 `GameObject.Find("player")`**——名字在各世界不一样，且可能被禁用。

---

## 3. 可直接调用的公开 API（全部已核实存在）

### 3.1 玩家定位与传送 — `FYP.Core.PlayerRef`

```csharp
public static Transform Root { get; }              // null 表示玩家未就绪
public static Camera Camera { get; }
public static CharacterController Controller { get; }
public static bool IsReady { get; }                // Root != null
public static void Register(Transform root, Camera camera, CharacterController controller);
public static void Unregister(Transform root);
public static void Teleport(Vector3 position, Quaternion rotation);   // 先禁 Controller 再设位置再启用，故意不清速度
```

⚠️ 域重载后 `PlayerRef.Root` 会变 `null` 而 `Awake` 不重跑。测试时需手工 `Register`。

### 3.2 伤害入口 — `FYP.Player.BatterySystem`

**这是唯一的伤害接口。电量就是生命值。**

```csharp
public void TakeHit()          // 扣 hitDrain（默认 20），播受创动画，扣到 0 触发瘫倒
public void Drain(float amount) // 按量扣电，不播受创动画（主动技能/持续伤害用这个）
```

`TakeHit()` 内部（`Assets/Scripts/Player/BatterySystem.cs:73`）：

```csharp
if (IsDead) return;
float before = Battery;
Battery = Mathf.Max(0f, Battery - hitDrain);
BillingLedger.AddHit(before - Battery);
if (anim != null) anim.PlayHit();
if (Battery <= 0f) Die(); else Notify();
```

相关字段：`hitDrain=20`、`restartBattery=30`、`chargeRate=5`、`sprintLockThreshold=50`、`jumpLockThreshold=25`、`restartDelay=3`、`getUpDuration=1.5`。属性 `IsDead` / `IsRecovering` / `Battery`。

⚠️ **持续伤害用 `Drain(amount * Time.deltaTime)`，不要每帧 `TakeHit()`**——后者会每帧播受创动画、每帧写账单，且 20 点一帧会瞬间秒杀。

⚠️ 敌人**不要直接改 `Battery` 字段**（它是属性，且绕过账单统计会让结算反馈层失真）。

### 3.3 恐怖张力 — `FYP.Core.TensionMeter`

已有完整张力系统（`TensionDirector` 在层域里每帧推进）。**敌人的存在应当影响张力，而不是另起一套。**

```csharp
public static float Value { get; }                 // 0~1
public static TensionLevel Level { get; }
public static event Action<float> OnChanged;
public static event Action<TensionLevel> OnLevelChanged;
public static void Add(float amount);
public static void Set(float value, bool forceBroadcast);
public static void Reset();
public static void BeginExpedition();
public static void Tick(float delta, float risePerSecond, float relief);
public static void Decay(float delta, float fallPerSecond);
public static TensionLevel LevelOf(float value);
public static string LevelName(TensionLevel level);   // 平静 / 不安 / 紧逼 / 濒临失控
```

常量：`EntryValue=0.12f`、`UneasyAt=0.35f`、`TenseAt=0.65f`、`CriticalAt=0.88f`、`LightRelief=0.55f`。
枚举：`TensionLevel { Calm=0, Uneasy=1, Tense=2, Critical=3 }`。

**推荐接法：** 敌人在附近 / 正在注视你 / 刚被甩掉时，`TensionMeter.Add(小量)`；不要 `Set`（会覆盖 `TensionDirector` 的推进）。

### 3.4 镜头抖动 — `FYP.Core.CameraShake`

```csharp
public static float Amplitude { get; }
public static float Remaining { get; }
public static void Impulse(float amplitude, float duration, float frequency = 18f);  // 取 max 不累加
public static void Clear();
```

⚠️ **必须在 `LateUpdate` 施加**：`PlayerMovement.Update` 每帧用 Head 骨骼覆盖相机世界位置（`Assets/Scripts/Player/PlayerMovement.cs:282-294`），在 `Update` 里抖会被立刻抹掉。现有 `CameraShakeDriver` 已按此实现，直接用 `Impulse` 即可。

### 3.5 提示与故障 — `HintToast` / `EmfStorm`

```csharp
// FYP.Interaction
public static void Show(string text, float duration = 2.5f);

// FYP.Core
public static bool EmfStorm.IsStorming { get; }
public static void EmfStorm.Set(bool on);
public static void EmfStorm.Scramble(string text);   // 让 HUD 全线乱码
public static void EmfStorm.Jitter();
```

⚠️ `EmfStorm` 是**全局单例状态**。Metro 的电磁风暴与工厂的巡检灯束都用它做短脉冲，用完**必须 `Set(false)`**（`PatrolBeam.cs` 的 `OnDisable` 里有兜底，照抄那个模式）。敌人如果也用它，要接受「同一时刻只有一个风暴源」这个互斥。

### 3.6 世界判定 — `FYP.Core.WorldPresence`

```csharp
public static string Home { get; }        // 实验室
public static string Current { get; }     // 玩家当前所在世界
public static bool InHome { get; }
public static event Action OnChanged;
public static void SetHome(string world);
public static void SetCurrent(string world);
```

⚠️ `SetCurrent` 由 `WorldStreamer` 每帧写入。**测试时手动 `SetCurrent("Metro")` 会被立刻覆盖回去**，正确做法是反射改 `WorldStreamer` 的私有静态字段 `currentWorld`（`BindingFlags.NonPublic | Static`）。

### 3.7 世界装卸 — `FYP.World.WorldStreamer`

```csharp
public static string CurrentWorld { get; }
public static int LoadedWorldCount { get; }
public static bool IsWorldReady(string world);
public static void RequestActivation(string world);
public static void NotifyCrossed(string arrivedWorld);
public static bool PreloadingReady { get; }
```

**为什么要在意：** 玩家离开一个世界后，该世界会被卸载（`MaxLoadedWorlds=5` 封顶 + 远离卸载）。⚠️ **敌人不能假设自己的场景一直存在**——`OnDisable` / `OnDestroy` 必须干净收尾（停协程、清 `EmfStorm`、清 `CameraShake`），否则跨世界会留下脏状态。`PatrolBeam` 和 `SoundOcclusion` 都是照这个原则写的，可参考。

---

## 4. 场景与空间事实

Build Settings 顺序（7 个场景）：

| # | 场景 | 说明 | 文件体积 |
|---|---|---|---|
| 0 | `Assets/Scenes/SpecimenLab.unity` | 实验室（起始世界 / 安全区） | 1.8 MB |
| 1 | `Assets/Scenes/Metro.unity` | Metro 站 | 2.5 MB |
| 2 | `Assets/Scenes/FactoryDay.unity` | 工厂·昼 | 1.2 MB |
| 3 | `Assets/Scenes/FactoryNight.unity` | 工厂·夜 | 1.2 MB |
| 4 | `Assets/Scenes/Flooded_Grounds.unity` | 水淹区 | 10.2 MB |
| 5 | `Assets/Scenes/OfficePack.unity` | 办公区 | 20.3 MB |
| 6 | `Assets/Scenes/Laboratory.unity` | 另一实验室 | 6.0 MB |

**门拓扑（链式）**：实验室 ↔ Metro；实验室 ↔ 工厂昼 ↔ 工厂夜 ↔ 水淹区。门命名约定：主门 `anywhere-door-X`，目标世界副门 `anywhere-door-X-A`。

⚠️ **场景整体有根偏移，别把坐标当绝对世界坐标看。** Play 中实验室根对象 y ≈ **−1024**，Metro 根 ≈ y 94，工厂根 ≈ x −4324，水淹区 ≈ y 2148。这是世界槽位系统（`WorldStreamer` 的世界偏移）造成的**正常现象**，不是 bug。

⚠️ **敌人巡逻点/出生点必须相对本场景的根来摆**，不要写死世界坐标。

---

## 5. NavMesh 现状（要先处理，否则 AI 走不动）

**实测结论：7 个场景都有 `NavMeshSettings` 块，但都指向同一个 guid `d2fc871308fdb5747b1bc6b5cefdaf00`，而这个 guid 在 `Assets/` 下没有任何对应资产文件；场景文件里也没有内嵌 `NavMeshData` 对象块。**

```
$ grep -c "^NavMeshData:" Assets/Scenes/*.unity
（全部为 0）
$ grep -rl "d2fc871308fdb5747b1bc6b5cefdaf00" . --include=*.meta
（空）
```

也就是说：**NavMesh 实际上没有烘焙数据**（是第三方素材包带来的悬空引用）。同时全工程没有任何 `NavMeshAgent` / `NavMeshObstacle` 组件。

**所以第一件工程活是：**

1. 打开目标场景（建议从 **Metro** 开始，它是第一个异世界且空间规整）。
2. `Window > AI > Navigation` → Bake 面板 → 确认 `Agent Radius 0.4 / Height 1 / Slope 60`（场景里已有这些值）→ **Bake**。
3. 检查烘焙结果覆盖了玩家实际能走的地面（Metro 有抬高 +93.9 的偏移，烘焙前先确认场景根位置）。
4. 烘焙产物会写回场景文件（体积会增加，属正常）。

⚠️ **烘焙是场景级操作，必须在 Unity 编辑器里做**，不能靠改文本文件。做完后 `git status` 会看到 `.unity` 变化，属预期。

---

## 6. 素材现状（这是最大的缺口）

### 6.1 可用的动画（`Assets/Art/Animations/UAL/`，CC0 @Quaternius）

20 个 `.anim` 片段，**都是人形骨骼**，来自 `Assets/Art/Animations/UAL/UAL1/Unity/UAL1_Standard.fbx`：

```
UAL1_Idle_Loop          UAL1_Walk_Loop          UAL1_Sprint_Loop
UAL1_Crouch_Idle_Loop   UAL1_Crouch_Fwd_Loop    UAL1_Jump_Start
UAL1_Jump_Loop          UAL1_Jump_Land          UAL1_Roll
UAL1_Death01            UAL1_Hit_Chest          UAL1_Interact
UAL1_PickUp_Table       UAL1_Dance_Loop         UAL1_Idle_Torch_Loop
UAL1_Sitting_Idle_Loop  UAL1_Spell_Simple_Enter UAL1_Spell_Simple_Exit
UAL1_Spell_Simple_Idle_Loop   UAL2_LayToIdle
```

**关键限制：这套是「玩家角色」的动画库，没有怪物/生物动画。** 里面能勉强复用的只有：`UAL1_Walk_Loop` / `UAL1_Sprint_Loop`（拖行的行走）、`UAL1_Crouch_Fwd_Loop`（爬行）、`UAL1_Hit_Chest`（受击反应）、`UAL1_Death01`（倒地）。

### 6.2 缺什么

- ❌ **没有敌人模型**（`Assets/Art/Characters/` 空）。
- ❌ **没有敌人动画**（无攻击、无嘶吼、无四肢爬行）。
- ❌ 没有敌人音效（现有 `Assets/Audio/Metro/`、`Assets/Audio/Factory/` 都是**程序合成**的环境音）。

### 6.3 素材合规（⚠️ 必读 `Docs/ASSET-SOURCING.md`）

- **Sketchfab 全站禁用**——其许可 2.2(b) 禁止把素材作为裸文件再分发，而本仓库是公开 GitHub。历史上有一次违规入库（`sci-fi-train`），已整目录移出。
- **BBC / freepbr / Zapsplat 排除。**
- 可用来源：**Poly Haven（CC0）**、**ambientCG（CC0）**、**Quaternius（CC0）**、**Mixamo（需确认条款）**。
- 新增素材前**必须先查 `Docs/ASSET-SOURCING.md`**，入库后同步更新该文件。

### 6.4 现实建议

**在拿到合规模型之前，先用程序化几何体做「能跑通逻辑」的敌人。** 这不是妥协，是标准做法：

- 用 `Capsule` / 几个 Cube 拼一个占位敌人（本项目已有先例：`TensionDirector.ShadowEvent` 和 `GlitchDirector.CopyRoutine` 都是运行时生成 Capsule 剪影，`GhostProduction` 的送料机是程序化图元拼装）。
- 先把感知、移动、状态机、伤害、音效、张力联动全部跑通并调好手感。
- 模型到位后只替换渲染层，逻辑一行不改。

**而且「看不清形状的影子」本身就是这个游戏的恐怖语言**——`ShadowEvent` 已经在用。占位体不一定要被替换掉。

---

## 7. 设计约束（决定敌人该做什么）

### 7.1 三条可选定位（需与项目所有者确认后再动手）

| 定位 | 玩法 | 实现难度 | 与本作契合度 |
|---|---|---|---|
| **A. 不可战胜的巡游者** | 在固定区域按路径巡逻，被看到/听到就逼近，玩家只能躲与跑，不能打 | 中 | 高——贴合「你只能看着它」 |
| **B. 环境威胁（非实体）** | 没有身体，表现为「某片区域不能久留」（灯灭、声音逼近、张力飙升） | 低 | 最高——已有 `PatrolBeam` / `GhostArrivalEvent` 是这个路子 |
| **C. 可驱散的实体** | 用手电筒/探测器短暂驱退，可重复遭遇 | 高 | 中——需要战斗平衡，容易破坏恐怖 |

**建议先做 B，再考虑 A。C 需要谨慎。**

### 7.2 必须遵守的设计原则

1. **敌人不能破坏「门」的规则。** 玩家靠门逃生是核心循环，敌人若跟穿门会摧毁整个玩法。跨世界追击**不要做**。
2. **敌人必须与撤离窗口共存。** `ExtractionClock`（Metro）/ `FactoryExtractionTimer`（工厂）是全局唯一一口钟，归零锁门。敌人若让玩家无法在窗口内撤离，就是设计缺陷。
3. **敌人必须尊重「歇班的世界死寂」。** 工厂有 `FactoryShift`（昼⇄夜 240s 轮换），歇班世界的机器停摆、门锁死。敌人若不看班次就活动，会自相矛盾。
4. **张力是共享资源。** 见 §3.3，用 `Add` 不用 `Set`。
5. **声音走 3D 音源。** 项目已有 `SoundOcclusion`（射线遮挡 → 低通 + 降音量，自动接管所有 `spatialBlend > 0.01` 的 `AudioSource`）。敌人的脚步声/呼吸声只要是 3D 音源就自动获得遮挡效果，**不需要额外写代码**。

### 7.3 现有「恐怖事件」的写法范式（照抄这个结构）

`Assets/Scripts/World/GhostProduction.cs` 是标准范式：

```csharp
public class GhostProduction : MonoBehaviour
{
    [SerializeField] private FactoryShiftController controller;   // 触发条件来源
    [SerializeField] private Transform[] rotors;
    [SerializeField] private AudioSource roar;
    [SerializeField] private float firstDelay = 45f;
    [SerializeField] private float minInterval = 120f;
    [SerializeField] private float maxInterval = 240f;
    [SerializeField] private float rampTime = 6f;
    [SerializeField] private float runTime = 9f;
    [SerializeField] private string startHint = "…";
    [SerializeField] private string endHint = "…";

    private float nextIn;
    private bool running;

    private void Update()
    {
        if (running) return;
        // 条件不满足就 return（在不在正确世界/班次/区域）
        // 计时到点 → StartCoroutine(RunRoutine())
    }
    // RunRoutine：三段式（渐强 → 持续 → 骤停死寂），每段都 HintToast.Show
}
```

**要点：** 所有参数 `[SerializeField]` 可调；`Update` 早退；状态用 `bool running` 防重入；`OnDisable` 收尾。

---

## 8. 建议的实施顺序

| 阶段 | 内容 | 验收 |
|---|---|---|
| **0** | 决定敌人定位（§7.1），确认素材来源（§6.3） | 项目所有者点头 |
| **1** | 在 Metro 烘焙 NavMesh（§5），确认玩家能走的范围 | Play 中放一个 `NavMeshAgent` 能跟着玩家走 |
| **2** | 占位敌人（Capsule）+ 状态机骨架（Idle/Patrol/Suspicious/Hunt） | 能巡逻、能发现玩家、能放弃追踪 |
| **3** | 感知：视锥 + 距离 + 射线遮挡；听觉（脚步/奔跑） | 蹲着走不被发现、跑起来被发现 |
| **4** | 接触伤害接 `BatterySystem.TakeHit()`，加冷却 | 被追上掉电 20，能逃离 |
| **5** | 张力联动 `TensionMeter.Add()` + 音效（3D 音源自动吃遮挡） | 靠近时 HUD 变红、心跳加快 |
| **6** | 替换为合规模型（若已找到），保留逻辑 | 视觉验收 |
| **7** | 在工厂昼/夜、水淹区复制配置（注意班次与图层） | 各世界都能跑 |

**每个阶段都要 Play 实测**，不要攒到最后。项目手册 §6 有 MCP 自动化流程。

---

## 9. 测试与验证的坑（项目手册 §6/§7 的浓缩）

1. **`Thread.Sleep` 会卡住主线程**，`Time.frameCount` 不前进，看起来像游戏不跑——是假象。
2. **`execute_code` 每次调用要 Roslyn 编译，会造出 300~500ms 假帧尖峰**。测帧率/卡顿必须用游戏内看门狗（`Assets/Scripts/World/FrameHitchWatchdog.cs` 已有），不要在外部循环里测。
3. **域重载后静态注册表全空**（`PlayerRef`、`BatterySystem.Instance`、`PlayerGear`、`WorldStreamer` 静态字段），`Awake` 不重跑。测试前先 `stop` + `play` 重进一次，确认关键引用非空。
4. **`PlayerMovement` 每帧覆盖相机位置/朝向**。手动摆相机做测试时，必须先 `enabled = false` 掉 `PlayerMovement`，且每帧重摆（否则玩家会持续坠落）。
5. **Play 失焦时帧不推进**（`runInBackground=true` 也一样）。计时逻辑要反射手动调 `Update`/`LateUpdate`。
6. **新建 `.cs` 后 `refresh_unity` 必须 `scope:"all"`**，用 `scope:"scripts"` 不会导入新文件，报 `CS0103`。
7. **判活不要用 `as Component == null`**，要用 `ReferenceEquals` 再判 Unity 对象空（见 `InteractionRunner.IsAlive` 注释）。
8. **不要用 `GameObject.Find` 找玩家**，用 `PlayerRef.Root`。

---

## 10. 参考：Project Doors 的敌人 AI（逆向工程，仅供思路）

`D:\SteamLibrary\steamapps\common\Project Doors Demo\_ReverseOutput\AGENT_GAMECODE.md`（20MB，57 万行 Ghidra 伪 C）里有完整的商业级实现，相关章节行号：

| 类 | 行号 | 用途 |
|---|---|---|
| `AIBehaviour.FSM.DuneController` | 1003 | 自研 FSM 调度器 |
| `AIBehaviour.FSM.Enemy.Brain.BrainBehaviour` | 1128 | 敌人主脑（含 20+ 协程） |
| `BrainConfig` | 5993 | 配置 |
| `EnemyCommonConfig` | 8280 | 通用参数 |
| `EnemyMultipliers` | 8531 | 难度倍率 |
| `FSMNavMeshBehaviour` | 8718 | NavMesh 移动基类 |
| `MoveBaseToRandomPointInRoom` | 8793 | 房间内随机游走 |
| `RandomPointOnIsland` | 9160 | 岛内取点 |

**⚠️ 只作思路参考，不要照抄。** 那是一个联机、HDRP、有 Addressables 和 DOTS 的工程；本项目是单机、Built-in 管线。它的 `FSMNavMeshBehaviour` 依赖 Netcode 的 `NetworkBehaviour`，直接搬会编译不过。

**真正值得借鉴的两点：**

1. **`CellOptimizerScheduler`（行 91417）的分帧节流**——每帧固定处理 1 项，先进先出。如果敌人感知要扫很多目标，照这个模式写，别一帧全扫。
2. **敌人按房间（Cell）配参数**——每个房间有自己的渲染距离、灯光方案、音频混响。本项目对应的是「每个世界一个场景」，可以在场景层面配。

---

## 11. 交付前自查清单

- [ ] 代码在 `FYP.World`（或新建程序集并更新 ADR-0004），没有反向依赖
- [ ] 用 `PlayerRef.Root` 拿玩家，不是 `GameObject.Find`
- [ ] 伤害走 `BatterySystem.TakeHit()` / `Drain()`，没有直接改 `Battery`
- [ ] 持续伤害按 `Time.deltaTime` 缩放
- [ ] 张力用 `TensionMeter.Add()`，没有 `Set()`
- [ ] `CameraShake` 只在 `LateUpdate` 生效
- [ ] `EmfStorm` 用完一定 `Set(false)`，`OnDisable` 有兜底
- [ ] `OnDisable` / `OnDestroy` 干净收尾（停协程、清全局状态）
- [ ] 没有硬编码世界图层做判定
- [ ] 敌人不跨世界追击，不阻断撤离窗口
- [ ] 工厂敌人看 `FactoryShift` 班次
- [ ] 音效是 3D 音源（自动获得 `SoundOcclusion`）
- [ ] 素材合规（查过 `Docs/ASSET-SOURCING.md`，更新了该文件）
- [ ] Play 实测过（不是只看编译通过）
- [ ] `read_console types:["error"]` 为 0

---

## 12. 一句话总结

**先用占位体把「感知 → 巡逻 → 发现 → 逼近 → 伤害 → 张力」这条链跑通，再去解决模型问题。**
这个游戏的敌人最好「不可战胜、看不清、只能躲」——因为它的恐怖来自缺席与不确定，不是来自战斗。
