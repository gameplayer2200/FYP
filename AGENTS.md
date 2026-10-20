# AGENTS.md — 《门阈行动》项目对接手册

> 面向**接手的开发者与 AI agent**。阅读顺序：本文件 → `GLOSSARY.md`（术语强制）→ `Docs/adr/0001~0005`（设计决策）。
> 文中数值均为**实测 / 代码核对**结论（截至提交 `85a91fb` + 第五轮结算反馈层，2026-10-20）。改门 / 传送逻辑前，先读 §4 和 §7。

---

## 0. 项目速览

- **《门阈行动 (Operation: Threshold)》**：第一人称心理恐怖游戏。玩家从实验室接单出发，推开一扇门进入异世界（层域），回收异常物，在归途门锁死前撤回实验室。
- 当前仓库 = MVP 纵向切片 + Metro 完整玩法闭环：**链式六世界**（实验室 ↔ Metro；实验室 ↔ 工厂昼 ↔ 工厂夜 ↔ 水淹区）、完整门系统（实时取景 + 穿行传送 + 开关同步 + `autoPair` 自动配对）、WorldStreamer 世界装卸裁决、玩家基础（电量 / 降级 / 瘫倒 / 起身）、交互与背包、CRT 终端风 HUD、接单任务板 / 电费账单 / 收容图鉴 / 值班播报。
- 内容方向与扩展规划见 `FYP_Game_Proposal.md`（四支柱：主世界 + 三大世界 + 探索道具 + 异常体道具；单次流程 30~45 分钟）；素材许可规范见 `Docs/ASSET-SOURCING.md`。

## 1. 环境与工具链

| 项 | 值 |
|---|---|
| Unity | **2022.3.62f3c1**（Windows；用 Unity Hub 打开仓库根目录） |
| 分支 | `main` |
| 场景（Build Settings 顺序） | `Assets/Scenes/SpecimenLab.unity`（起始/实验室）→ 第三方样例 `country house` → `Assets/Scenes/Metro.unity` → `Assets/Scenes/FactoryDay.unity` → `Assets/Scenes/FactoryNight.unity` → `Assets/Scenes/Flooded_Grounds.unity`（后四者 = 异世界） |
| 编辑器自动化 | 已装 **MCP for Unity**（v10.3.1-beta.7，本地包 `Packages/com.coplaydev.unity-mcp`，经 `file:` 引用——**勿改回 git URL**，UPM 在本机解析 git 包会报 `not in a git directory`）。服务端口 **8080**（日志 `Library/MCPForUnity/Logs/`）；`unity-mcp-cli` 的 `/api/tools/...` 路径与本服务不匹配，**用 MCP JSON-RPC（POST /mcp）调用**，用法见 §6 |

关键提交（新→旧）：`0f3d68a` 第三/四轮（撤离窗口+手电筒+粉笔箭头+HUD 按世界切换）→ 队友六连（`85a91fb` CRT UI+CC0 素材库 / `345e4ec` 背包交互+收容台两段式 / `e725eae` 图标+验证截图 / `e13c571` autoPair+玩家 prefab 化 / `b3248f1` 起身动画+门配对工具 / `bbd4050` 链式布局落盘）→ `5cf464f` 接单板+电磁风暴/幽灵进站 → `22226b6` Metro 渐熄闭环 → `bca6118` Metro 抬高 +93.9 → `31185c2` 门收尾（判定提前/门同步/出向修正/SelfTest）。

## 2. 目录地图

```
Assets/Scripts/              按程序集分层（Docs/adr/0004，依赖单向）
  Core/        FYP.Core      PlayerRef / PlayerGear / WorldPresence / ExtractionClock / EmfStorm /
                              BillingLedger / CollapsePenalty / CrtTheme / CrtOverlay
  Player/      FYP.Player    PlayerMovement（含双玩家守卫/蓄力跳）/ BatterySystem（双通道耗电+记账上报）/
                              Flashlight（F 开关）/ PlayerAnimatorProxy（含起身动画链）
  Interaction/ FYP.Interaction  IInteractable / InteractionRegistry / InteractionRunner（唯一 E 键）/
                              InteractionLabel / HintToast
  World/       FYP.World     AnywhereDoor / PortalWindow（autoPair）/ WorldLink / WorldStreamer（装卸裁决）/
                              WorldLightIsolation / LoadDiag / Metro 渐熄玩法全套（MetroBlackout/LightGroup/
                              ExtractionTimer/PowerCabinet/PowerWire/BreakerSwitch/PowerLockedDoor/
                              PoweredLight/RequiresPower）/ 异常（EmfStormAnomaly/GhostArrivalEvent/
                              UnreachableLightAnomaly/ChalkTrail）/ 道具（MultimeterPickup/FlashlightPickup/ChargeStation）
  Content/     FYP.Content   Inventory（含档案）/ InventoryUI（底部道具栏+图标）/ OrderBoard（按世界切换形态+收容档案）/
                              SpecimenPickup（带档案）/ SpecimenDeposit（两段式）/ SpecimenHud /
                              BillPanel（电费账单）/ DutyBriefing（值班播报）
Assets/Editor/                MetroSceneDev（分阶段搭建+测试驱动）+ DoorLinkAutoRepair / DoorPairFixer /
                              ItemIconBaker / LoadHitchProbe / FixHingeTool / PortalDiagnostics / SceneOptimizer
Assets/Prefab/                player.prefab（玩家整体，场景里是其实例）+ door/（anywhere-door.prefab 基座 +
                              anywhere-door-portal.prefab + 各世界专属门 prefab）+ Tables/ + ChargeStation/Wall
Assets/WorldLinks/            8 条门配对（WorldLink，autoPair 后只作参数兜底）：
                              SpecimenLab↔Metro、SpecimenLab↔FactoryDay、FactoryDay↔FactoryNight、
                              FactoryNight↔Flooded_Grounds 各正反
Assets/Shaders/               PortalPanelProj.shader（投影屏：世界坐标投影采样，不走网格 UV）
Assets/Resources/Sourced/     Poly Haven/ambientCG CC0 素材（模型/贴图/HDRI/字体/音效）——许可见 Docs/ASSET-SOURCING.md
Assets/Audio/Metro/           程序合成音效（emf_storm / arrival_pressure）
Assets/items/                 旧素材（门模型 / Exploration objects / RandomObjects）
Docs/adr/0001~0005            设计决策记录；Docs/tools/ 提案生成脚本（node）；Docs/ASSET-SOURCING.md 素材审计
GLOSSARY.md                   术语表（写代码/注释/文档必须遵守）
FYP_Game_Proposal.md          内容提案
PortalDumps/ + Assets/Screenshots/   验证截图（部分已入库、部分未跟踪）
```

（根目录其余 `.agents/.claude/.codex/.workbuddy-ai/.vscode` 等为工具配置目录，保持现状。）

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
7. 控制台无报错（基线见 §7.5）。

## 6. Unity MCP 工作流（agent 专用）

> 2026-10-17 更新：经 8080 端口的 MCP JSON-RPC 直连（curl）是唯一验证可用的通道；`manage_editor`/`execute_code`/`read_console`/`refresh_unity`/`manage_camera`/`manage_scene` 等工具名不变。

- **调用方式**（每次调用 = initialize → notifications/initialized → tools/call；响应可能是 SSE `data:` 行或纯 JSON，两者都要兼容）：
  ```bash
  # 长代码先落盘成 .cs（工程根 snippets/ 目录），再用 python json.dumps 包成 args.json，杜绝转义问题
  curl -s -X POST http://localhost:8080/mcp -H "Content-Type: application/json" \
    -H "Accept: application/json, text/event-stream" -H "Mcp-Session-Id: <id>" -d @args.json
  ```
  首次握手 initialize 取 `Mcp-Session-Id` 响应头。`snippets/` 已有可复用片段（`pos_check`/`reregister`/`repair_singletons`/`blackdrive`/`drive_lateupdate`/`farlight`）。
- 工具速查：
  - `manage_editor {action:"play"|"stop"}`；进 Play 触发域重载。
  - `execute_code {action:"execute", code:"..."}`：可直呼 `MetroSceneDev`（public static class）的公开静态方法；**`PlayerRef` 在 FYP.Core（asmdef），片段直接写 `typeof(PlayerRef)` 编译不过** → 用反射：`AppDomain` 扫类型名 `"PlayerRef"`，取 `Root`/`Camera` 属性与 `Teleport` 方法（`MetroSceneDev.PlayerRoot()` 是 **private**，同样要反射）。反射小抄：`BindingFlags` 记得带 `Static`；`GetMethod("Update")` 单参匹配会 AmbiguousMatch（用按参数类型精确匹配的助手）；`SelfTest` 签名 `(out string)`；`PortalWindow` 的 `link/srcFrame/portalCam/cooldown` 等均为 private 字段（`GetField` 带 `NonPublic|Public|Instance` 读）。
  - `read_console {action:"get", types:["error","warning"], count:50, format:"plain"}`。
  - `refresh_unity {mode:"force", scope:"scripts", compile:"request", wait_for_ready:true}`（改脚本后；**Play 中改脚本 → 域重载 → 静态注册表全灭**，修法见下）。
  - `manage_camera {action:"screenshot", capture_source:"game_view", ...}` → `Assets/Screenshots/`（**仅 Play 模式有效**；编辑器失焦可能截到旧帧）。
  - 复杂搭建/验证走 `Assets/Editor/MetroSceneDev.cs`（分阶段幂等：`Stage0_Env/Stage0b_Seal/Stage0c_Floors/Stage1_Items/Stage1_Lab/Stage2_Blackout/Stage2_Cabinet/Stage2_Anomalies/Stage3_Carriage/FixPass1/2/Finalize2` + `PT_*` Play 测试驱动 + `Screenshot/FloorMap/ColliderDump` 勘察）。
- **编辑器失焦时 Play 不推进**（`runInBackground=true` 也一样）：计时类逻辑（渐熄、耗电、穿行 LateUpdate）后台测试须反射手动调 `Update`/`LateUpdate`（`snippets/blackdrive.cs`、`drive_lateupdate.cs`）。
- **域重载陷阱**：静态注册表（`PlayerRef`、`BatterySystem.Instance`、`Inventory.Instance`）清空且组件 `Awake` 不重跑 → 跑 `snippets/repair_singletons.cs`；组件内缓存初始化放 `OnEnable`（LightGroup/UnreachableLightAnomaly/PoweredLight 已照此实现）。
- Play 就绪轮询：`MetroSceneDev.PT_Pos()`；Metro 窗要玩家进 15m 预载半径后才就绪（穿行测试范式：`PT_StageAtLabDoor` → 等 1s → `PT_PushThrough` → `drive_lateupdate.cs`）。
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
10. **Play 失焦冻结 → 穿门「穿不回去」**：`LateUpdate` 里 `cooldown -= Time.deltaTime`，冻结时 dt=0 → 首次穿门写入的 0.6s 冷却永不归零、`TryCross` 不再执行（症状：正向穿行成功、反向不动、prevDot 卡在哨兵 0）。测试法：反射把该窗 `cooldown` 置 0 再驱动 `LateUpdate`；真实游玩时钟正常、无此问题。
11. **误删预制件 → 场景只剩空壳**：删掉 `anywhere-door-Metro Variant.prefab` 后，Metro 场景的门变成 "Missing Prefab" 空壳（kids=1、无 `Cube.001/Plane`）并刷控制台报错；`git checkout` 恢复预制件 + `AssetDatabase.Refresh` 后场景内自动复原（只要场景文件没在缺失期间被保存过）。教训：门实例引用的预制件别直接删，先改场景再删。

## 8. 当前状态 / 待办 / 仓库卫生

- ✅ 已修并实测（2026-10-16，`31185c2`）：判定提前 `crossPlaneLead=0.25`（≈面板前 0.2m，只认朝门里走 + 落点补偿）；两世界门开关同步（双向 1~2 帧）；穿门出向/落点（dot=+1.000、raw=−0.020）；双窗 `SelfTest` 全 PASS；控制台 0 报错。证据截图在 `PortalDumps/`（如 `portal_metro_lookback4.png`、`portal_lab_open3.png`）。
- ✅ Metro「渐渐熄灭」完整闭环（2026-10-17，待玩家实测）：
  - **场景整备**：雾 15~170 青绿减淡、隧道/上层 36 盏灯提亮、门区常亮灯塔；展示假人/展示相机/无效 Volume 全部禁用；封壳（顶盖+四周围墙，`MetroGameplay/Sealing`）+ 两块补地板（L1 6.9 / 大厅 14.9）封掉所有露背景缺口与地板空洞（空洞清零）。
  - **渐熄规则**：5 个灯组按「上层远厅→上层中段→对面隧道→隧道A远段→门区与楼梯」每 90s 熄一组（`MetroBlackout`，灯组 GO 摆在各组灯泡质心）；停电期间玩家 1 电/秒、所在区段已熄 ×1.5（`BatterySystem.SetAmbientDrain`，实验室恒 0）；世界判定为盒形（z≥29.5，避开实验室 z≤28.4）。
  - **配电室谜题**（-Z 死端）：检修平台+配电箱+红黄蓝三线（每局随机 1 根带电）+电闸；持万用表 E 线=测线+接入；合闸接对→全站复明并广播 `MetroBlackout.OnPowerRestored`，接错→火花弹回可重试。
  - **奖励级联**：尾节车厢端门解锁（`PowerLockedDoor`）+ 车厢灯点亮（`PoweredLight`）+ 门区充电桩恢复供电（`RequiresPower` 门控，停电期间提示断电）。
  - **异常物回收**：软盘 ×2（门区箱上 + 车厢内，程序化拼装——`items/RandomObjects/floppydisk` 的 fbx/glb 资产损坏无法导入）+ 实验室收容台（case 模型，`SpecimenDeposit`）+ 右上角 HUD（`SpecimenHud`）；拾取/收容经 `Inventory` 扩展（knob 逻辑不动）。
  - **异常「对面的灯」**：对面隧道死端高处，玩家 <14m 熄、>22m 亮（滞回）；门区充电桩旁常亮、隧道深处 DeepRattle/WindHowl/Taps 三处 3D 环境音。
  - **回归结论**：实验室窗 SelfTest PASS；穿门落点 (-47.89, 1.12, 132.71) 精确门槽（**抬高前**基线）。⚠️ **Metro 侧窗 SelfTest 的镜像判据 dot=−1 属已知表现**（自检只补偿 mirrorU 不补偿 mapYaw180 的 180° 映射；门系统代码与 WorldLink 零改动，以实验室窗 + 实际穿行朝向为准）。
  - **2026-10-17 抬高 +93.9（`bca6118`）后复验**：双向穿行通过——实验室→Metro 落点 (−47.89, 94.27, 132.71)、Metro→实验室落点 (3.78, 0.26, 5.12)，均精确门槽；实验室窗 SelfTest PASS；控制台 0 报错。
  - 新增交互提示：`InteractionRunner` 显示 `[E] + InteractionLabel 文案`；`HintToast`（Interaction 程序集）为全玩法反馈出口。
- ✅ Metro 第二轮：接单任务板 + 两个新异常（2026-10-17，待玩家实测）：
  - **修复**：渐熄世界判定盒抬高后失效（y∈[-10,32] 不含 94 高的玩家）→ 改 y∈[76.9,132.9]，复验 killed/掉电恢复生效；**双玩家守卫**（`PlayerMovement.Awake` 先到先得，后加载场景的玩家整组禁用）——Metro 场景现也常驻一个玩家供直接开发/测试，从实验室正常开局不受影响（复验 players=2 active=1）。
  - **接单任务板 `OrderBoard`（Content）**：右上角 HUD——订单标题/异常物进度/下一步提示，Tab 展开详情与报酬；收容入账即完成（绿色提示）。吸收旧 `SpecimenHud` 职能（Lab 侧已禁用）；Lab 与 Metro 各挂一份，静态 renderOwner 先到先得避免双画。程序集注意：`FYP.Content` 现引用 `FYP.Core`（EmfStorm 所在）。
  - **异常「电磁风暴」`EmfStormAnomaly`（World）**：停电进行中（KilledGroups>0）随机触发（首延 45s+，间隔 120~240s，持续 10s）——灯组乱闪 + HUD 全线乱码抖动（`EmfStorm.Scramble/Jitter`，Core 静态；OrderBoard/电量条已接）+ 2D 电流干扰声；结束恢复风暴前灯态。
  - **异常「幽灵进站」`GhostArrivalEvent`（World）**：玩家在站随机触发（首延 60s+，间隔 180~300s）——风压轰鸣渐强 9s（3D 声源在隧道深处）+ 灯管异常波动 + 已熄死灯自己亮起闪 → 最响 5s → **骤停死寂 2.2s** → 恢复。「有进站的全部征兆、车永远不来」。
  - **程序合成音效**（fal 无 key 的回退，`MetroSceneDev.Stage4_MakeSfx`）：`Assets/Audio/Metro/emf_storm.wav`（50Hz 蜂鸣+白噪+爆点）与 `arrival_pressure.wav`（布朗噪声低通轰鸣）；`generate_audio` 工具需先在 Unity 的 MCP Asset Generation 页配置 fal key。
  - 复验：风暴/进站协程启动即生效（IsStorming/running/roar 死灯闪亮 ✓）；订单流转「0/2→携带 2→完成 ✓」全通；从 Metro 直接 Play 时订单板即时可见（v3 截图验收）。
- ✅ Metro 第三轮：撤离窗口 + 手电筒 + 粉笔箭头（2026-10-17，待玩家实测）：
  - **撤离窗口**（提案「在归途门锁死前撤回」落地）：进站武装 `ExtractionClock`（默认 600s，Inspector 可调），订单板显示倒计时（≤120s 红色闪烁）；归零→`ExtractionTimer` 关闭 Metro 门并 `SetRespondToKey(false)` 锁死（门关=穿行自然被拦）；玩家回实验室（含瘫倒重启传送）→ 窗口复位、门解锁。**瘫倒惩罚**：重启时携带异常物减半（`CollapsePenalty` Core 钩子解耦 Player→Content，已收容不动）。
  - **手电筒**（探索道具）：实验室门旁 + Metro 门区各一个拾取物（`FlashlightPickup`，Exploration objects 模型）；拾取即给玩家挂 `Flashlight` 组件；**F 开关**——灯锥挂视角相机自动跟随，开灯 +0.5 电/秒（`BatterySystem` 双通道：环境+手电独立叠加），且**照明中不吃黑暗 ×1.5 加成**（MetroBlackout 读 `PlayerGear.FlashlightOn`）。实测：开灯 1.0 / 关灯 1.5。
  - **站台粉笔箭头**（backup 稿「前人痕迹」）：隧道沿线 6 枚程序绘制粉笔箭头（无贴图资产，运行时生成）指向 -Z 死端/配电室；**逆行**（车厢方向）进入惊吓区触发一次「箭头是给出去的人看的……」+ 灯光骤闪全灭再复原。
  - **Metro 场景常驻开发玩家**：实验室玩家（含全套组件）复制为 `Player(Metro开发)` 存入 Metro.unity（@-46.5,94.2,137），从 Metro 直接 Play 即有玩家；双玩家守卫保证运行时只活一个。⚠️ 之前"放了玩家没保存"（预制件实例未落盘、重开场景即丢）的坑由此终结。
  - 分层调整：`FYP.Player` 增加 `FYP.Interaction` 引用（Flashlight 用 HintToast）。
  - 备注：队友的 `WorldStreamer`（远离卸载+封顶淘汰+门保护）已落地，ADR-0003 收敛完成；本轮门系统零改动，未重跑 SelfTest（锁门仅调用门公开 API 且已实测）。
- ✅ 第四轮修复与迭代（2026-10-17，待玩家实测）：
  - **手电筒 Metro 不亮修复**：根因 = Metro 66 盏灯把普通灯挤出内置管线逐像素名额（只剩顶点光照，视觉上等于不亮；实验室灯少所以正常）。修法 = 灯锥 `renderMode = ForcePixel` + 强度 2.4→3.6 + 近场补偿点光（range 4.5 / int 0.7，同样 ForcePixel）。实测死端近处细节清晰可见。
  - **右上角 HUD 按世界切换形态**（新 `WorldPresence`，Core 静态：Home/Current/InHome，WorldStreamer 的 TickInner/NotifyCrossed 各加一行写入）：实验室=「整备视图」（已收容档案、装备清单 手电筒/万用表 ✓/—、出发提示）；层域=「订单视图」（订单：{世界名} 回收 + 进度 + 提示 + 撤离窗口倒计时）。世界显示名映射：Metro→Metro 站、FactoryDay→工厂·昼、FactoryNight→工厂·夜、Flooded_Grounds→水淹区。
  - **Metro 常驻开发玩家补充**：`Player(Metro开发)` 已存盘；注意从 SpecimenLab 正常开局时它会被双玩家守卫禁用（实测 players=2 active=1）。
- ✅ 三世界传送门（2026-10-17）：FactoryDay / FactoryNight / Flooded_Grounds 各一对（实验室侧中庭一排 z=−3.5、`preloadRadius` 4.5/15、统一 `anywhere-door-portal.prefab` 实例 + 六条 WorldLink）。三对双向穿行实测全通过（落点均在对门门槽 raw≈−0.02、朝向保持行进方向）；实验室窗 `SelfTest` 全 PASS；修复「世界侧看门洞全绿」（排门后墙太近 → 挪至中庭）；Metro 门误删的 Variant 已恢复并复验。证据截图在 `PortalDumps/`（`v_labrow4.png`、`p_fix_labview2.png` 等）。
- ✅ 队友六连（2026-10-18~20，`bbd4050`→`85a91fb`，此前未同步进本手册——2026-10-20 补记）：
  - **门拓扑链式重构**：实验室中庭三扇 → 链式（实验室↔Metro、实验室↔工厂昼↔工厂夜↔水淹区）；`PortalWindow.autoPair` 按门名自动配对（`-A` 后缀约定），WorldLink 只作参数兜底；Editor 工具 `DoorLinkAutoRepair`/`DoorPairFixer`。
  - **WorldStreamer 终态**（ADR-0005）：三规则装卸 + 永不卸载护栏，`MaxLoadedWorlds=5`、全 preloadRadius=15、预载完成即激活（延迟激活方案试过两版均回退，踩坑记录见 ADR）。
  - **玩家 prefab 化**：整体抽成 `Assets/Prefab/player.prefab`，场景里是实例；死亡起身动画（Dead→GetUp→Idle，GetUp→Idle 不用 hasExitTime）；蓄力超级跳（耗电 1）。
  - **CRT 终端 UI 主题**（`CrtTheme`/`CrtOverlay`，Core）：全 HUD 磷光绿等宽字 + 扫描线 + 打字机；**手电筒开关改 F 键**；背包底部道具栏 + 物品图标（`ItemIconBaker`）；收容台两段式（先开盖再收容）；拾取范围收紧 1.5m。
  - **CC0 素材库**：Poly Haven 16 模型 + 贴图/HDRI + 字体 + ambientCG（`Assets/Resources/Sourced/`），许可审计 `Docs/ASSET-SOURCING.md`（**新增素材先查此文件**，BBC/freepbr/Zapsplat 等来源排除）。
- ✅ 第五轮：结算反馈层 + 文档补账（2026-10-20，待玩家实测）：
  - **电费账单 `BillPanel`（Content）**：收容台入账时居中弹 ~6.5s——照明/环境/机动/受创四类耗电 + 合计（满电量百分比）+ 机器人味结语；数据经 `BillingLedger`（Core 静态桥，BatterySystem 记账上报，同 EmfStorm 模式解耦 Player→Content）；触发链 `Inventory.OnDeposit`。瘫倒不清零（账单=整局累计）；Capacity 未就绪时百分比防御为 0（勿显示 int.MinValue）。
  - **收容图鉴（收容数据库）**：`SpecimenPickup` 加档案字段（`specimenName`/`flavor`），`Inventory` 收容时记录档案列表；实验室形态下订单板 Tab 详情 = 收容档案区（#001 起逐条档案卡）；瘫倒散失同步丢档案（队尾）。Metro 三件：值班员的备份 / 末班车的行车记录 / **复读磁带**（新第三异常物，站厅轨道边，Poly Haven 磁带机模型——「同一句『你终于来了』，每播一次语气都不耐烦一点」）。
  - **值班播报 `DutyBriefing`（Content）**：进层域时左上打字机式播报当班规则（Metro=渐熄/撤离/黑暗加倍；工厂/水淹占位），每世界每次会话一次；依赖 `WorldPresence`（WorldStreamer 每帧写入，后台无帧时 Home 未初始化——仅测试环境现象）。
  - **Flooded_Grounds Demo 残留清理**：删 FpsController/FPSDisplay/`player (1)`（保留环境：地形 1、灯 16）。⚠️ **首版清理误删了 "scene" 根（环境容器）——已从 git 恢复**；教训：按"子树含 Demo 脚本删根"会误伤，只删脚本宿主本体。
  - 文档：AGENTS.md 全面同步（本条目即）；新增 `Docs/adr/0005-world-streamer.md`。
- ⏳ 待办（按优先级）：
  1. 玩家实测 Metro 闭环 + 三世界门动线（重点：E 键自然交互、90s 节奏体感、黑暗 ×1.5 的掉电压力、车厢奖励动线、账单/图鉴/播报观感）；
  2. **工厂昼夜玩法闭环**（下一个大项）——设计已定（2026-10-20 用户拍板）：**软轮换**（时间到才换：门上挂钟提示，玩家自主穿门切换；不切换会被锁在当前状态——白天机器可见但门锁/夜晚门全开但异常显形）+ **夜班不掉电**（与 Metro 差异化，压迫来自轮换混乱与异常显形）；谜题=值班室排班表按序启动 3 台机器电源→锁定昼夜；奖励=昼夜自选手动切换（冷却 60s）。素材：Factory 双场景同布局（Day x≈−4324 / Night x≈+10473，几何几乎相同可坐标映射）。
  3. 水淹区玩法闭环（提案：积水短路易 2 倍+抖屏、扳手关 3 个排水阀、水位下降露下层区+防水涂层；素材自带水/雾/混响/8 音效）；
  4. 收容图鉴扩展：异常物种类还很少（软盘×2+磁带），工厂/水淹各配 2 件 + 差异化档案；
  5. 仓库卫生：`Docs/tools/node_modules`（418 个文件已入库）、截图目录（PortalDumps / Assets/Screenshots）的跟踪与忽略策略（snippets/ 与本地 vendor 包已入库）。

## 9. 术语速查（完整见 GLOSSARY.md）

世界=场景；实验室=起始世界；异世界=其余世界；门=可交互实体（门框/门板/门洞）；传送门=门的运行时行为；门配对=WorldLink 配置；电量=生命值；瘫倒=电量归零后重启；交互物=可按 E 的物体；交互中枢=玩家身上唯一持有 E 键的组件；异常=层域中违反常理的事件；异常物=要回收的目标物。

写代码 / 注释 / 文档必须使用表内术语；用「任意门 / 传送点 / 地图 / 血条」这类外来词会被视为缺陷。
