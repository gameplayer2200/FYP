# AGENTS.md — 《门阈行动》项目对接手册

> 面向**接手的开发者与 AI agent**。阅读顺序：本文件 → `GLOSSARY.md`（术语强制）→ `Docs/adr/0001~0004`（设计决策）。
> 文中数值均为**实测 / 代码核对**结论（截至提交 `31185c2`，2026-10-16）。改门 / 传送逻辑前，先读 §4 和 §7。

---

## 0. 项目速览

- **《门阈行动 (Operation: Threshold)》**：第一人称心理恐怖游戏。玩家从实验室接单出发，推开一扇门进入异世界（层域），回收异常物，在归途门锁死前撤回实验室。
- 当前仓库 = MVP 纵向切片：双世界（实验室 ↔ Metro 异世界）、完整门系统（实时取景 + 穿行传送 + 开关同步）、玩家基础（电量 / 降级 / 瘫倒）、交互与背包。
- 内容方向与扩展规划见 `FYP_Game_Proposal.md`（四支柱：主世界 + 三大世界 + 探索道具 + 异常体道具；单次流程 30~45 分钟）。

## 1. 环境与工具链

| 项 | 值 |
|---|---|
| Unity | **2022.3.62f3c1**（Windows；用 Unity Hub 打开仓库根目录） |
| 分支 | `main` |
| 场景（Build Settings 顺序） | `Assets/Scenes/SpecimenLab.unity`（起始/实验室）→ 第三方样例 `country house` → `Assets/Scenes/Metro.unity`（异世界） |
| 编辑器自动化 | 已装 **Unity MCP (mcpforunity)**；`.agents/skills/`（及 `.claude/skills/`）内有每个工具的技能说明，用前先读 |

关键提交（新→旧）：`31185c2` 门收尾（判定提前/门同步/出向修正/SelfTest）→ `151f3b6` 关门态暗板 → `abadeae` 开门后门板碰撞体 → `501c95e` 穿门判定符号翻转 → `71a77bb` 黑板投影。

## 2. 目录地图

```
Assets/Scripts/              按程序集分层（Docs/adr/0004，依赖单向）
  Core/        FYP.Core      PlayerRef（玩家引用注册表，判空用 IsReady）
  Player/      FYP.Player    PlayerMovement / BatterySystem / PlayerAnimatorProxy
  Interaction/ FYP.Interaction  IInteractable / InteractionRegistry / InteractionRunner（唯一 E 键）
  World/       FYP.World     AnywhereDoor / PortalWindow / WorldLink / ChargeStation
  Content/     FYP.Content   Inventory / InventoryUI
Assets/Editor/                AnywhereDoorBuilder / FixHingeTool / PortalDiagnostics / SceneOptimizer
Assets/Prefab/                anywhere-door.prefab（基座）+ anywhere-door-Metro Variant.prefab + ChargeStation/Wall
Assets/WorldLinks/            LabToMetro.asset / MetroToLab.asset（门配对数据资产）
Assets/Shaders/               PortalPanelProj.shader（投影屏：世界坐标投影采样，不走网格 UV）
Assets/items/anywhere-door-pintu-kemana-saja/   门模型来源（印尼语「想去哪就去哪的门」）
Docs/adr/0001~0004            设计决策记录；Docs/tools/ 提案生成脚本（node）
GLOSSARY.md                   术语表（写代码/注释/文档必须遵守）
FYP_Game_Proposal.md          内容提案
PortalDumps/ + Assets/Screenshots/   验证截图（部分已入库、部分未跟踪）
```

（根目录其余 `.agents/.claude/.codex/.workbuddy-ai/.vscode` 等为工具配置目录，保持现状。）

## 3. 门系统架构（核心）

一句话：**门是实体，传送门是配置**。

- **门 `AnywhereDoor`**（挂在门板 `Cube` 上，SpecimenLab/Metro 各一扇）：`IInteractable`，E 键开关；`SetOpen(open, scheduleAutoClose)`；动画 = 挥法 `spellWindup` → 旋转 `duration`（smoothstep）→ `spellOutro`。无碰撞体时 Awake 自动补一个 trigger BoxCollider（供交互发现）。
- **门配对 `WorldLink` 资产**：`worldSceneName / worldDoorName / openingPath / rtHeight / preloadRadius / mapYaw180 / mirrorU`。新增一扇门 = 新增一条配对资产，不改代码（ADR-0001/0002）。
- **实时取景 `PortalWindow`**：把本门 `Cube.001` 的材质槽 `backSlot=0` 换成 `Custom/PortalPanelProj` 投影屏；偏心视锥相机渲染对面世界（渲染时隐藏对面门体全部 Renderer，防递归）；关门时换回原材质/暗板。
- **穿行 `PortalWindow.TryCross()`**（LateUpdate）：
  - 触发：玩家在门平面法线上的带符号距离**跨过提前触发面**即传送；触发面在门根平面往接近侧偏 `crossPlaneLead = 0.25m`（≈面板前 0.2m，修「先穿模再传送」）；
  - 只认**朝门里走**（inward）；`crossPlaneTolerance=1.5` 防远处误触发；`crossCooldown=0.6s` 防双触发；门关着不许穿；
  - 落点 = local 坐标镜像到对门，再沿对门法线补偿 `exitAdvance=0.02m`（落点恒在门洞槽内 raw≈−0.02）；**朝向与位置成对**（取景与瞬移共用同一 `MapLocalPoint/MapLocalDirection`，`mapYaw180=true`）；
  - 传送后两侧 `ResetCrossBaseline()`（prevDot=0 哨兵），否则对面窗会把玩家立刻弹回来。
- **开关同步 `SyncPairedDoor`**：只在本门状态**变化**时向对门下发 `SetOpen(_, false)`；自动关计时只由被交互门排（防级联）。
- **世界加载**：双世界常驻 Additive（ADR-0003）；每扇门在玩家进 `preloadRadius=15m` 内才加载目标世界，对面已加载则复用；取景渲染距离 = 2×preloadRadius。

## 4. 关键数值与不变量（改前必读）

- 门根 `localScale = 0.6048`；`raw = 门局部 z × scale`（米）——raw 为负 = 门前方（玩家侧）。
- **两扇门**：
  - `anywhere-door-Metro-Lab` @ (3.78, 0.02, 5.14)，forward=+Z（在 SpecimenLab.unity，内建对象）
  - `anywhere-door-Metro` @ (−47.89, 0.13, 132.69)，forward=−Z（在 Metro.unity，prefab 实例）
- 门洞 ≈ 1.225 × 2.106 m；`Cube.001` 两个材质槽：**槽 0 = 门洞内板（投影屏，必须填 0）**；槽 1 = 门框环（填 1 会「门框消失」）。
- WorldLink：两资产均 `mapYaw180=1`、`mirrorU=0`、`preloadRadius=15`、`rtHeight=720`。
- AnywhereDoor 当前实例参数（以提交为准）：

| 参数 | Lab 门 | Metro 门（prefab 继承） |
|---|---|---|
| openAngle | 95 | −180 |
| duration | 0.8 | 0.8 |
| autoCloseDelay | 10 | 10 |
| spellWindup / spellOutro | 1.2 / 0.6 | 1.2 / 0.6 |
| startOpen | 0（开局关） | 0 |

- `autoCloseDelay=10` 的含义：**E 开门后 10 秒自动关**（并同步给对门）。代码 `SetOpen(true, false)` 不排计时——测试时门会一直开着。
- PortalWindow 两实例：`backSlot=0`、`crossCooldown=0.6`、`crossPlaneTolerance=1.5`；`crossPlaneLead` 尚未序列化进场景（生效值 = 类默认 **0.25**；在 Inspector 调过它之后记得保存场景）；`showDebugOsd`：Lab 门=0（关）、Metro 门=1（开）——**定稿前两门都设 0**。

## 5. 验收与自检（动传送相关必做）

### 5.1 `PortalWindow.SelfTest`（数值自检，权威）

```csharp
// execute_code 片段
var win = GameObject.Find("anywhere-door-Metro-Lab").GetComponentInChildren<PortalWindow>(true);
var m = typeof(PortalWindow).GetMethod("SelfTest");
var args = new object[]{ null };
bool ok = (bool)m.Invoke(win, args);
return ok + " :: " + (string)args[0];
```

合格标准：门洞四角 NDC = ±1.0000；采样轴 |u|=|v|=1.0000（w = 门洞实长 1.225 / 2.106）；镜像判据 dot(U轴, 相机右) = +1.0 → `PASS（偏心视锥正确）`。
注意：玩家站门洞内 / 贴太近时会**假 FAIL**（玩家自身被量进去）；挪开 2m+ 再测。

### 5.2 回归清单（自然管线）

1. Play → 走近实验室门（<15m）→ Metro 世界预载、两窗就绪；
2. E 开门（等动画到位，约 2.6s）→ 走过去：应在**贴到面板前 ~0.2m** 触发（不再穿模）；落点在对面门洞槽内、朝向保持行进方向；
3. 从 Metro 侧走回来：同样通过；
4. 门外折返、绕门框蹭走：不误触发；门关着不能穿；
5. 开 / 关任一门：对面 1~2 帧内同步；
6. 双窗内容：Metro 侧窗内显示 Lab 世界（明亮）；Lab 侧窗内是 Metro 房间（本身是暗场景，偏暗正常）；
7. 控制台无报错（基线见 §7.5）。

## 6. Unity MCP 工作流（agent 专用）

- 工具速查：
  - `manage_editor {action:"play"|"stop"}`；进 Play 触发域重载，偶发「Suppressed duplicate command resent…」提示属正常。
  - `execute_code {action:"execute", code:"..."}`：C# 片段（已引用运行时程序集，`typeof(PortalWindow)` 可用；**`PlayerRef` 在 FYP.Core，片段里直接写 `typeof(PlayerRef)` 会失败** → 用 `AppDomain.CurrentDomain.GetAssemblies()` 扫 `GetType("PlayerRef")`）。
  - `read_console {action:"get", types:["error","warning"], count:50, format:"plain"}`。
  - `refresh_unity {mode:"force", scope:"scripts", compile:"request", wait_for_ready:true}`（改脚本后）。
  - `manage_camera {action:"screenshot", capture_source:"game_view", screenshot_file_name:"名字", include_image:false}` → `Assets/Screenshots/<名字>.png`（重名自动 -1；**仅 Play 模式有效**）。
  - GameObject / 组件 / 资产操作走 `gameobject-*` / `assets-*` 系列——**用前先读 `.agents/skills/<工具名>/SKILL.md`**。
  - 多实例：同时开多个 Unity 工程时先 `set_active_instance`（`Name@hash` 或端口）。
- Play 就绪轮询（轮询到 `True|True` 再开始验证；某扇门为 null = 其世界未加载，先靠近/等待）：

```csharp
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var rf = typeof(PortalWindow).GetField("ready", bf);
var a = GameObject.Find("anywhere-door-Metro-Lab").GetComponentInChildren<PortalWindow>(true);
var b = GameObject.Find("anywhere-door-Metro").GetComponentInChildren<PortalWindow>(true);
return (bool)rf.GetValue(a) + "|" + (bool)rf.GetValue(b);
```

- 传送玩家做测试（AppDomain 扫描 + Teleport）：

```csharp
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{ try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { } }
pr.GetMethod("Teleport").Invoke(null, new object[]{
    new Vector3(-47.89f, 1.0f, 135.19f),                // 例：Metro 门前 2.5m
    Quaternion.LookRotation(new Vector3(0f, 0f, -1f)) });
```

  ⚠️ 手动跨世界传送后：**落点离门 >1.5m**，并调用 `ResetCrossBaseline()`（两窗），否则会被穿行判定误触发。
- 纪律：MCP 调用短平快、失败就重试；**不要塞永不 resolve 的长 await**；**不要在 Play 运行时在 Assets/ 里增删文件**（原因见 §7.1）。

## 7. 已踩过的坑与排查

1. **Play 运行时别动 Assets 文件**：在 Play 中增删 Assets 里的文件会触发资产刷新，可能让运行中的材质/窗口状态失效——曾导致 SelfTest 读出 0 向量 + 4 条 `_UDir` 材质报错（重开会话即恢复，非代码缺陷）。清理截图请先停 Play。
2. **编辑模式截图无效**：先确认 `Application.isPlaying` 为 true 再截。
3. **「门自己关了」**：先看 `autoCloseDelay`（E 开门 10 秒后自动关，设计行为）；再确认是否有人按 E（`InteractionRunner` 选最近交互物，门的 InteractionRange = 3m）。
4. **SelfTest 假 FAIL**：玩家站门洞内 / 贴太近（见 §5.1）。
5. **控制台 6 条 missing-script 警告**：历史遗留（第三方资产），与门系统无关；「0 报错」是正常基线。
6. **OSD 调试叠加**（如「PortalWindow SpecimenLab 映射Y180…」）：由 `showDebugOsd` 控制；定稿前两门都设 0。
7. **判活不要用 `as Component == null`**：见 `InteractionRunner.IsAlive` 注释（必须 `ReferenceEquals` 再判 Unity 对象空）。
8. **脚本搬迁必须用 `AssetDatabase.MoveAsset`** 保 .meta GUID，否则场景引用全变 Missing（ADR-0004）。
9. **execute_code 片段写法**：`Vector3` / `float` 字面量带 `f`；失败响应结构 `{success:false,data:{errors:[...]}}`；长脚本先落盘再执行。

## 8. 当前状态 / 待办 / 仓库卫生

- ✅ 已修并实测（2026-10-16，`31185c2`）：判定提前 `crossPlaneLead=0.25`（≈面板前 0.2m，只认朝门里走 + 落点补偿）；两世界门开关同步（双向 1~2 帧）；穿门出向/落点（dot=+1.000、raw=−0.020）；双窗 `SelfTest` 全 PASS；控制台 0 报错。证据截图在 `PortalDumps/`（如 `portal_metro_lookback4.png`、`portal_lab_open3.png`）。
- ⏳ 待办（按优先级）：
  1. 定稿前关 `showDebugOsd`（见 §4）；
  2. 内容按 `FYP_Game_Proposal.md` 推进——**世界数量要到 4~5 个时，必须把「常驻/加载」收敛到可替换的 WorldStreamer**（ADR-0003 的复查条件）；
  3. `mirrorU` 的可视复验（SelfTest 已间接证明，可选）；
  4. 仓库卫生：`Docs/tools/node_modules`（418 个文件已入库）、截图目录（PortalDumps / Assets/Screenshots）的跟踪与忽略策略。

## 9. 术语速查（完整见 GLOSSARY.md）

世界=场景；实验室=起始世界；异世界=其余世界；门=可交互实体（门框/门板/门洞）；传送门=门的运行时行为；门配对=WorldLink 配置；电量=生命值；瘫倒=电量归零后重启；交互物=可按 E 的物体；交互中枢=玩家身上唯一持有 E 键的组件；异常=层域中违反常理的事件；异常物=要回收的目标物。

写代码 / 注释 / 文档必须使用表内术语；用「任意门 / 传送点 / 地图 / 血条」这类外来词会被视为缺陷。
