# AGENTS.md — 《门阈行动》项目对接手册

> 面向**接手的开发者与 AI agent**。阅读顺序：本文件 → `GLOSSARY.md`（术语强制）→ `Docs/adr/0001~0005`（设计决策）。
> 文中数值均为**实测 / 代码核对**结论（截至提交 `104f838` + 第六轮素材入库，2026-10-21）。改门 / 传送逻辑前，先读 §4 和 §7。

---

## 0. 项目速览

- **《门阈行动 (Operation: Threshold)》**：第一人称心理恐怖游戏。玩家从实验室接单出发，推开一扇门进入异世界（层域），回收异常物，在归途门锁死前撤回实验室。
- 当前仓库 = MVP 纵向切片 + Metro / 工厂昼夜双世界玩法闭环：**链式六世界**（实验室 ↔ Metro；实验室 ↔ 工厂昼 ↔ 工厂夜 ↔ 水淹区）、完整门系统（实时取景 + 穿行传送 + 开关同步 + `autoPair` 自动配对）、WorldStreamer 世界装卸裁决、玩家基础（电量 / 降级 / 瘫倒 / 起身）、交互与背包、CRT 终端风 HUD、接单任务板 / 电费账单 / 收容图鉴 / 值班播报。
- 内容方向与扩展规划见 `FYP_Game_Proposal.md`（四支柱：主世界 + 三大世界 + 探索道具 + 异常体道具；单次流程 30~45 分钟）；素材许可规范见 `Docs/ASSET-SOURCING.md`，美术素材盘点（体积 / 引用 / 可回收）见 `Docs/ART-ASSETS.md`。

## 1. 环境与工具链

| 项 | 值 |
|---|---|
| Unity | **2022.3.62f3c1**（Windows；用 Unity Hub 打开仓库根目录） |
| 分支 | `main` |
| 场景（Build Settings 顺序） | `Assets/Scenes/SpecimenLab.unity`（起始/实验室）→ 第三方样例 `country house` → `Assets/Scenes/Metro.unity` → `Assets/Scenes/FactoryDay.unity` → `Assets/Scenes/FactoryNight.unity` → `Assets/Scenes/Flooded_Grounds.unity`（后四者 = 异世界） |
| 编辑器自动化 | 已装 **MCP for Unity**（包内 `package.json` = v10.3.1-beta.11，本地包 `Packages/com.coplaydev.unity-mcp`，经 `file:` 引用——**勿改回 git URL**，UPM 在本机解析 git 包会报 `not in a git directory`；MCP server 侧 PyPI 包 `mcpforunityserver` = 10.3.0）。**通道是 stdio**（Unity 注册表 `MCPForUnity.UseHttpTransport` 写 0，插件不回连 8080，桥监听 `127.0.0.1:6400`），DSH 侧直接跑常驻 uv tool `C:\Users\14763\.local\bin\mcp-for-unity.exe --transport stdio`、工具名 `mcp__unity__*`（**48 个**）；`manage_editor` 的合法 action 只有 telemetry_status/telemetry_ping/play/pause/stop/set_active_tool/add_tag/remove_tag/add_layer/remove_layer/deploy_package/restore_package/undo/redo（**没有 get_state**）。日志 `Library/MCPForUnity/Logs/`；安装与排障见 §6 |

关键提交（新→旧）：`104f838` 第六轮（sci-fi-train 素材入库 + 收容台 casedeco 根因修复）→ `0c909e0` 新增 HANDOVER.md → `e4e2d03` 第五轮结算反馈层（电费账单/收容图鉴/值班播报）+ 文档补账 → `0f3d68a` 第三/四轮（撤离窗口+手电筒+粉笔箭头+HUD 按世界切换）→ 队友六连（`85a91fb` CRT UI+CC0 素材库 / `345e4ec` 背包交互+收容台两段式 / `e725eae` 图标+验证截图 / `e13c571` autoPair+玩家 prefab 化 / `b3248f1` 起身动画+门配对工具 / `bbd4050` 链式布局落盘）→ `5cf464f` 接单板+电磁风暴/幽灵进站 → `22226b6` Metro 渐熄闭环 → `bca6118` Metro 抬高 +93.9 → `31185c2` 门收尾（判定提前/门同步/出向修正/SelfTest）。


## 2. 目录地图

程序集分层见 `Docs/adr/0004`（依赖单向：Core ← Player ← Interaction ← World ← Content）。

```
Assets/Scripts/     按程序集分层
  Core/         PlayerRef PlayerGear WorldPresence ExtractionClock EmfStorm
                BillingLedger CollapsePenalty CrtTheme CrtOverlay FactoryShift NoiseBus
  Player/       PlayerMovement BatterySystem Flashlight PlayerAnimatorProxy EmfDetector
  Interaction/  IInteractable InteractionRegistry InteractionRunner（唯一 E 键）
                InteractionLabel HintToast
  World/        AnywhereDoor PortalWindow WorldLink WorldStreamer WorldLightIsolation LoadDiag
                Metro 玩法（MetroBlackout/LightGroup/ExtractionTimer/Power*/Breaker*）
                工厂玩法（FactoryShiftController/ShiftScheduleBoard/MachinePowerSwitch/ShiftGate/*）
                异常（EmfStorm/GhostArrival/UnreachableLight/ChalkTrail/GhostProduction/PatrolBeam）
                EnemyGhoul（敌人 AI，详见 Docs/ENEMY-AI.md）
  Content/      Inventory InventoryUI OrderBoard Specimen* BillPanel DutyBriefing
Assets/Editor/       MetroSceneDev FactorySceneDev DoorLinkAutoRepair DoorPairFixer
                     ItemIconBaker PortalDiagnostics SceneOptimizer WorldSlotAudit ...
Assets/Prefab/       player.prefab door/ Tables/ ChargeStation/ Enemy/ Build/
Assets/WorldLinks/   8 条门配对（autoPair 后只作参数兜底）
Assets/Shaders/      PortalPanelProj.shader
Assets/Art/          第三方素材（Worlds/ Props/ Characters/ Animations/ VFX/）
                     ⚠️ 保持包内结构原样，勿按文件拆散
Assets/Resources/    Sourced/ 为 CC0 素材；内部路径被 Resources.Load 字符串依赖，勿动
Assets/Material/     FYP 自制材质
```

文档：`HANDOVER.md`（交接入门）· `GLOSSARY.md`（术语强制）· `Docs/adr/0001~0005`（设计决策）
`Docs/ENEMY-AI.md`（敌人 AI）· `Docs/ASSET-SOURCING.md`（素材许可）· `Docs/ART-ASSETS.md`（美术清单）
`Docs/CHANGELOG-history.md`（历史演进与逐轮验收证据）

（根目录 `.agents/.claude/.vscode` 等为工具配置，保持现状。）


## 3. 门系统架构（核心）

一句话：**门是实体，传送门是配置**（2026-10-18 起 = 门名约定 + 参数兜底）。

- **门拓扑为链式**（2026-10-18 队友重构，替代旧的"实验室中庭一排三扇"）：实验室 ↔ Metro、实验室 ↔ 工厂昼、工厂昼 ↔ 工厂夜、工厂夜 ↔ 水淹区。**命名约定 `anywhere-door-X-A` ↔ `anywhere-door-X`**（`-A` 后缀在"来路侧"：实验室的 Metro-A/FactoryDay-A、工厂夜侧的 FactoryNight-A、水淹区侧的 Flooded-Grounds-A）。
- **`PortalWindow.autoPair`（默认 true）**：运行时按门名自动推导配对（剥掉 `-A` 后缀匹配），WorldLink 资产退化为**参数兜底**（rtHeight/preloadRadius 等）；`DoorLinkAutoRepair` / `DoorPairFixer`（Editor）可一键重配。
- **门 `AnywhereDoor`**（挂在门板 `Cube` 上，八扇如上）：`IInteractable`，E 键开关；`SetOpen(open, scheduleAutoClose)` / `SetRespondToKey(bool)`（撤离锁门用）；动画 = 挥法 `spellWindup` → 旋转 `duration`（smoothstep）→ `spellOutro`。
- **实时取景 `PortalWindow`**：把本门 `Cube.001` 的材质槽 `backSlot=0` 换成 `Custom/PortalPanelProj` 投影屏；偏心视锥相机渲染对面世界（渲染时隐藏对面门体全部 Renderer，防递归）；关门时换回原材质/暗板。
- **穿行 `PortalWindow.TryCross()`**（LateUpdate）：
  - 触发：玩家在门平面法线上的带符号距离**跨过提前触发面**即传送；触发面在门根平面往接近侧偏 `crossPlaneLead = 0.25m`（≈面板前 0.2m，修「先穿模再传送」）；
  - 只认**朝门里走**（inward）；`crossPlaneTolerance=1.5` 防远处误触发；`crossCooldown=0.6s` 防双触发；门关着不许穿；
  - 落点 = local 坐标镜像到对门 + `exitAdvance=0.02m`（落点恒在门洞槽内 raw≈−0.02）；**朝向与位置成对**（取景与瞬移共用同一映射）；穿行后 `WorldStreamer.NotifyCrossed()` 更新当前世界快照（WorldPresence）。
  - 传送后两侧 `ResetCrossBaseline()`（prevDot=0 哨兵），否则对面窗会把玩家立刻弹回来。
- **开关同步 `SyncPairedDoor`**：只在本门状态**变化**时向对门下发 `SetOpen(_, false)`；自动关计时只由被交互门排（防级联）。
- **世界装卸 = `WorldStreamer`（ADR-0005，2026-10-18 落地）**：场景装卸的唯一裁决者，三规则——①玩家进门 `preloadRadius` 装载；②所有关联门远离 2×preloadRadius+2m 卸载；③同时存活上限 `MaxLoadedWorlds=5`（封顶淘汰"需求最弱"）。永不卸载：实验室（homeWorld）、玩家当前世界（穿门登记）、门开着的世界。`WorldLightIsolation` 随装载做各世界光照隔离（世界灯只照本世界图层）。

## 4. 关键数值与不变量（改前必读）

- 门根 `localScale = 0.6048`；`raw = 门局部 z × scale`（米）——raw 为负 = 门前方（玩家侧）。
- **八扇门 = 链式四对**（对象名与坐标以场景为准）：
  - SpecimenLab：`anywhere-door-Metro-A` @ (3.78, 0.02, 5.14)、`anywhere-door-FactoryDay-A` @ (6.17, 0, −4.34)；
  - Metro：`anywhere-door-Metro` @ (−47.89, **94.03**, 132.69)（**Metro 世界整体抬高 +93.9**，根对象 `__EA_ART__`/`__EA_SYSTEM__`/`MetroGameplay` 均在 y≈93.9）；
  - FactoryDay：`anywhere-door-FactoryDay` @ (−4324, ≈0, 0)、`anywhere-door-FactoryNight`（去往工厂夜）；
  - FactoryNight：`anywhere-door-FactoryNight-A`、`anywhere-door-Flooded-Grounds` @ (10473, ≈1.7, ...)；
  - Flooded_Grounds：`anywhere-door-Flooded-Grounds-A` @ (402, 2147.83, 330)（内容整体抬高 y+2134，地面 ≈2147.81）；
  - **门后必须留 ≥4.5m 净空**：取景相机在对门后方 ≈ 玩家到门的距离处，门后贴墙会取景进墙内（门洞一片扁平纯色）。
- 门洞 ≈ 1.225 × 2.106 m；`Cube.001` 两个材质槽：**槽 0 = 门洞内板（投影屏，必须填 0）**；槽 1 = 门框环（填 1 会「门框消失」）。
- WorldLink（共 8 条，链式命名如 `SpecimenLabToMetro`/`FactoryDayToFactoryNight`）：均 `mapYaw180=1`、`mirrorU=0`、`rtHeight=720`、`openingPath=Cube.001/Plane`、`preloadRadius=15`（2026-10-18 统一回调为 15，MaxLoadedWorlds=5 兜住总量）。
- AnywhereDoor 当前实例参数（以提交为准）：

| 参数 | Metro-Lab 门 | Metro 世界门（变体继承） | 三世界六门（portal 预制件） |
|---|---|---|---|
| openAngle | 95 | −180 | 95 |
| duration | 0.8 | 0.8 | 0.8 |
| autoCloseDelay | 10 | 10 | 10 |
| spellWindup / spellOutro | 1.2 / 0.6 | 1.2 / 0.6 | 1.2 / 0.6 |
| startOpen | 0（开局关） | 0 | 0 |

- `autoCloseDelay=10` 的含义：**E 开门后 10 秒自动关**（并同步给对门）。代码 `SetOpen(true, false)` 不排计时——测试时门会一直开着。
- PortalWindow（八实例参数一致）：`backSlot=0`、`crossCooldown=0.6`、`crossPlaneTolerance=1.5`；`crossPlaneLead` 尚未序列化进场景（生效值 = 类默认 **0.25**；在 Inspector 调过它之后记得保存场景）；`showDebugOsd` 正式门全为 0（⚠️ 基座 `anywhere-door.prefab` 里为 1，是调试默认——从基座新建门后记得关）。
- `AnywhereDoor.swingAxis`（2026-10-24 新增，枚举 LocalZ/LocalX/LocalY，默认 LocalZ）：门板绕哪条**本地轴**摆。八扇现役门（枢轴在本地 Z）零影响；第三方套件门按「哪条本地轴指向世界上方」实测配置。第三方门挂脚本流程见 §6 末尾。
- **世界槽位规则（2026-10-24 定式，工具 `FYP/世界槽位/` 菜单 = `Assets/Editor/WorldSlotAudit.cs`）**：全部世界常驻同一坐标空间、靠 WorldStreamer 装卸，几何穿插只能靠「各占一片远方坐标」避免——
  - 槽位 = **5000m 网格**，内容包围盒中心对齐槽心（x,z 均为 5000 的倍数）；新世界沿 **−X** 递进（现有：OfficePack=−20000、Shrine=−35000，后续 −30000、−40000…）；
  - 硬约束：任意两世界实体 AABB 净距 **≥2000m**、坐标绝对值 **≤50000**（单精度浮点，越远模型抖动）；
  - 超大世界（如 Shrine 实体盒宽 22km 的远景巨石阵）不强求槽心对齐，满足净距即可；
  - **现状豁免**：Metro↔实验室 81m（垂直分层设计）、Flooded↔Metro 1986m（垂直隔离，差 14m 达标线）、FactoryDay 未对槽（x=−4324 历史位置，净距充足不动）；
  - 新世界入链流程：导入场景 → 菜单 `FYP/世界槽位/审计 + 未入槽世界自动归槽` → 复跑审计全 ✅ → 再接门。
  - ⚠️ 实测坑：整世界平移后**子渲染器的 `Renderer.bounds` 在编辑器进程内保留旧缓存**（跨工具调用仍在），世界坐标审计一律用「网格本地包围盒 × localToWorldMatrix 现算」——工具已内置此法，别再手写 bounds 扫描。

## 5. 验收与自检（动传送相关必做）

### 5.1 `PortalWindow.SelfTest`（数值自检，权威）

```csharp
// execute_code 片段（注意门对象名已是 -A 后缀约定，见 §3）
var win = GameObject.Find("anywhere-door-Metro-A").GetComponentInChildren<PortalWindow>(true);
var m = typeof(PortalWindow).GetMethod("SelfTest");
var args = new object[]{ null };
bool ok = (bool)m.Invoke(win, args);
return ok + " :: " + (string)args[0];
```

合格标准：门洞四角 NDC = ±1.0000；采样轴 |u|=|v|=1.0000（w = 门洞实长 1.225 / 2.106）；镜像判据 dot(U轴, 相机右) = +1.0 → `PASS（偏心视锥正确）`。
注意：玩家站门洞内 / 贴太近时会**假 FAIL**（玩家自身被量进去）；挪开 2m+ 再测。

### 5.2 回归清单（自然管线）

0. 新增世界门最低验收（2026-10-17 定式）：① 实验室窗 `SelfTest` PASS；② 双向穿行落点 = 对门门槽（raw≈−0.02、朝向保持行进方向）；③ 站世界侧门前 2~4m 反看门洞，应看到实验室内部（门后净空 ≥4.5m，见 §4）。
1. Play → 走近实验室门（进 `preloadRadius`=15）→ 目标世界预载、两窗就绪（WorldStreamer 装载，灯随装载隔离）；
2. E 开门（等动画到位，约 2.6s）→ 走过去：应在**贴到面板前 ~0.2m** 触发（不再穿模）；落点在对面门洞槽内、朝向保持行进方向；
3. 从 Metro 侧走回来：同样通过；
4. 门外折返、绕门框蹭走：不误触发；门关着不能穿；
5. 开 / 关任一门：对面 1~2 帧内同步；
6. 双窗内容：Metro 侧窗内显示 Lab 世界（明亮）；Lab 侧窗内是 Metro 房间（本身是暗场景，偏暗正常）；
7. 控制台无报错（基线：§7 第 5 条——只有 6 条历史 missing-script 警告，无 error）。

## 6. Unity MCP 工作流（agent 专用）

- 通道 **stdio**，DSH 侧跑常驻 `mcp-for-unity.exe --transport stdio`，工具名 `mcp__unity__*`（48 个）。
- `manage_editor` 合法 action 只有 telemetry_status/telemetry_ping/play/pause/stop/
  set_active_tool/add_tag/remove_tag/add_layer/remove_layer/deploy_package/
  restore_package/undo/redo（**没有 get_state**）。
- 日志：`Library/MCPForUnity/Logs/`
- **从零接入 / 重装步骤 → `Docs/MCP-UNITY-SETUP.md`**（一次性，已配置完成）
- 第三方门挂脚本流程见 §7 末条。

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
10. **Play 失焦冻结 → 穿门「穿不回去」**：`LateUpdate` 里 `cooldown -= Time.deltaTime`，冻结时 dt=0 → 首次穿门写入的 0.6s 冷却永不归零、`TryCross` 不再执行（症状：正向穿行成功、反向不动、prevDot 卡在哨兵 0）。测试法：反射把该窗 `cooldown` 置 0 再驱动 `LateUpdate`；真实游玩时钟正常、无此问题。
11. **新建 .cs 后 refresh 必须 `scope:"all"`**：用 `scope:"scripts"` 时 Unity 不会导入新文件，引用处报 `CS0103: The name 'X' does not exist in the current context`（改已有脚本才用 scripts）。
12. **MCP 实例名会瞬时抖动**：偶发报 `Unity instance 'FYP@<hash>' not found. Available instances: ['Lesson06_Assets@...']`，重试即恢复；`refresh_unity` 每次都会先回一次 `Connection closed before reading expected bytes` 然后 `Refresh recovered after Unity disconnect/retry`，属常态。
13. **误删预制件 → 场景只剩空壳**：删掉 `anywhere-door-Metro Variant.prefab` 后，Metro 场景的门变成 "Missing Prefab" 空壳（kids=1、无 `Cube.001/Plane`）并刷控制台报错；`git checkout` 恢复预制件 + `AssetDatabase.Refresh` 后场景内自动复原（只要场景文件没在缺失期间被保存过）。教训：门实例引用的预制件别直接删，先改场景再删。


## 8. 当前状态

**待办（按优先级）**

1. 玩家实测 Metro + 工厂昼夜闭环 + 三世界门动线
2. 水淹区玩法闭环（下一个大项）
3. 收容图鉴扩展：水淹区配 2 件 + 差异化档案
4. 仓库卫生：`Docs/tools/node_modules`（418 文件已入库）、截图目录跟踪策略

**相关文档**

| 内容 | 位置 |
|---|---|
| 历史演进 / 逐轮验收证据 / 素材清理记录 | `Docs/CHANGELOG-history.md` |
| 死代码审计（已核查，无可删项） | `Docs/DEADCODE-AUDIT.md` |
| 大文件清单与拆分建议 | `Docs/FILE-SIZE-AUDIT.md` |
| MCP for Unity 从零接入步骤 | `Docs/MCP-UNITY-SETUP.md` |

**提交约束**：`.git/hooks/pre-commit` 会拦截超过 20 KB 的 `.cs`（只查自己写的代码，
忽略第三方包与二进制资产）。被拦下就按职责拆文件，别绕过。

写新函数、新规则前，先搜项目里有没有现成的。

## 9. 术语速查（完整见 GLOSSARY.md）

世界=场景；实验室=起始世界；异世界=其余世界；门=可交互实体（门框/门板/门洞）；传送门=门的运行时行为；门配对=WorldLink 配置；电量=生命值；瘫倒=电量归零后重启；交互物=可按 E 的物体；交互中枢=玩家身上唯一持有 E 键的组件；异常=层域中违反常理的事件；异常物=要回收的目标物。

写代码 / 注释 / 文档必须使用表内术语；用「任意门 / 传送点 / 地图 / 血条」这类外来词会被视为缺陷。
