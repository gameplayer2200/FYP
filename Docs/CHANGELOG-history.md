# 开发历史与状态记录（从 AGENTS.md 迁出）

> 本文件承接原 `AGENTS.md` §8「当前状态 / 待办 / 仓库卫生」的完整内容。
> AGENTS.md 只保留**当前有效**的约束与指针；历史演进、逐轮验收证据、清理记录都在这里。
> 需要追溯某次改动时查本文件，日常对接不需要读它。

---

## 8. 当前状态 / 待办 / 仓库卫生

- ✅ 已修并实测（2026-10-16，`31185c2`）：判定提前 `crossPlaneLead=0.25`（≈面板前 0.2m，只认朝门里走 + 落点补偿）；两世界门开关同步（双向 1~2 帧）；穿门出向/落点（dot=+1.000、raw=−0.020）；双窗 `SelfTest` 全 PASS；控制台 0 报错。证据截图在 `PortalDumps/`（如 `portal_metro_lookback4.png`、`portal_lab_open3.png`）。
- ✅ Metro「渐渐熄灭」完整闭环（2026-10-17，待玩家实测）：
  - **场景整备**：雾 15~170 青绿减淡、隧道/上层 36 盏灯提亮、门区常亮灯塔；展示假人/展示相机/无效 Volume 全部禁用；封壳（顶盖+四周围墙，`MetroGameplay/Sealing`）+ 两块补地板（L1 6.9 / 大厅 14.9）封掉所有露背景缺口与地板空洞（空洞清零）。
  - **渐熄规则**：5 个灯组按「上层远厅→上层中段→对面隧道→隧道A远段→门区与楼梯」每 90s 熄一组（`MetroBlackout`，灯组 GO 摆在各组灯泡质心）；停电期间玩家 1 电/秒、所在区段已熄 ×1.5（`BatterySystem.SetAmbientDrain`，实验室恒 0）；世界判定为盒形（z≥29.5，避开实验室 z≤28.4）。
  - **配电室谜题**（-Z 死端）：检修平台+配电箱+红黄蓝三线（每局随机 1 根带电）+电闸；持万用表 E 线=测线+接入；合闸接对→全站复明并广播 `MetroBlackout.OnPowerRestored`，接错→火花弹回可重试。
  - **奖励级联**：尾节车厢端门解锁（`PowerLockedDoor`）+ 车厢灯点亮（`PoweredLight`）+ 门区充电桩恢复供电（`RequiresPower` 门控，停电期间提示断电）。
  - **异常物回收**：软盘 ×2（门区箱上 + 车厢内，程序化拼装——`Art/Props/RandomObjects/floppydisk` 的 fbx/glb 资产损坏无法导入）+ 实验室收容台（case 模型，`SpecimenDeposit`）+ 右上角 HUD（`SpecimenHud`）；拾取/收容经 `Inventory` 扩展（knob 逻辑不动）。
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
- ✅ 第六轮：收容台根因修复 + 素材入库（2026-10-21，`104f838`）：
  - **收容台「按 E 打不开」根因**：`SpecimenDeposit.AcquireParts()` 用 `GetComponentsInChildren<Transform>(true)` 遍历，装饰件 `casedeco_low` 可能排在盖子 `caseup_low` 之前，而旧判据只有 `name.StartsWith("caseup")` → 认领到装饰件，盖子其实在动但玩家看不见，表现为「按 E 没反应」。修法：先 `transform.Find("caseup_low")` 精确取，兜底遍历时排除含 `deco` 的名字；`Awake` 里先把 `lid`/`clasps` 置 null，避免旧域/序列化残留干扰认领（`clasps` 同理由 `GetChild` 直取 `caseop*`）。
  - **素材入库**：`Assets/Material_Scence/sci-fi-train/`（Sketchfab「Sci-Fi Train」`Untitled.glb` + 贴图，约 44MB）。⚠️ 来源是 Sketchfab，**与 `Docs/ASSET-SOURCING.md` §1「Sketchfab 全站」排除项冲突**（其许可 2.2(b) 禁止把素材作为裸文件再分发，而本仓库是公开 GitHub）→ ✅ **2026-10-23 已整目录移出 `Assets/`**（移至仓库外 `..\FYP-asset-backup\`），公开仓库不再分发。
  - `Assets/Material/MatrixVFX-master/`：原为约 82MB 的完整外部 Unity 工程（自带 `ProjectSettings/`、`Packages/`、3 个 `.cs`；网格来自 Global Digital Heritage，README 明写 non-commercial）→ ✅ **2026-10-23 已瘦身**：那份非商用网格从未进入本工程，只保留**屏幕空间扫描/矩阵特效**必需文件（`ScreenSpaceMatrix.cs`、`ScreenSpaceMatrixEffect.shader`、`ScreenSpaceMatrixEffectColored.shader`、`GenerateWhiteNoise.compute`、`RandomLib.cginc`、`LabColorspace.cginc`、`Texture/fonts_textures.png`、`LICENSE.md`），许可 = **MIT**（Copyright 2020 Shahriar Shahrabi，两个 shader 均 `Shader.Find` 成功且 `isSupported=True`）；顺带修掉 `#include "LabColorspace.cginc"` 与文件名 `LabColorSpace.cginc` 的**大小写不匹配**（Windows 不报错、换平台会编译失败）。
- ✅ 第六轮：工厂昼夜玩法闭环（2026-10-22，待玩家实测）——设计 2026-10-20 拍板 + 本轮三问定稿（值班/沉睡模型 + 配撤离窗口 + 异常选自开生产线/巡检灯束）：
  - **值班/沉睡模型**：Core 静态排班钟 `FactoryShift`（每班 240s 昼⇄夜轮换，玩家离厂即暂停；谜题完成后冻结）。在班的世界醒着（机器可启动 / 夜侧深区门开放 / 异常显形），歇班的世界死寂（机器停摆、门锁死、E 只给「机器沉睡」提示）。**夜班不掉电**：工厂代码从不调用 `SetAmbientDrain`（与 Metro 差异化；手电通道照常）。
  - **谜题**：值班室排班表（主厅北墙）每局随机顺序，按序启动 3 台机器——昼侧主厅 2 台风机（z=9.3 东西墙，槽位刻意避开 Animated 叠加位）+ 夜侧深区送料机（**2026-11-10 回归：Poly Haven `old_drill_press` CC0 模型，`Assets/Art/Props/Machines/OldDrillPress/`，21,842 三角面，实尺 527×610×1050 mm**，贴 `Factory2Wall04` 西墙 x≈−6.44、落地点 = 地板 −12001.94；开关控制箱 @ (−6.02, −12001.69, −21.42)，`machineId=2`，带 `深区送料机_补光` 点光 intensity 2.6 / range 6.5——夜侧离屏渲染偏暗，异常物与关键道具一律带局部补光，同 Metro 软盘先例），**必然跨昼夜往返**；错序 = 火花 + 全厂复位（排班表是唯一线索）。完成 → 排班表变锁定器：E 锁定当前班次（被锁世界永久在班且缺陷消除：锁昼=昼侧深区门也开 / 锁夜=异常平息），之后每 **60s** 可改锁（奖励=轮换控制权交给玩家）。
  - **深区**：annex 东墙工业门（z=−70.1）通往程序化泵房（昼/夜镜像，**Night = Day + (14797, 0, 0)**）；昼侧谜题前恒锁 / 夜侧夜班开放 / 谜题后双侧常开（`ShiftGate`，条件满足自动开闸演出）。泵房在穿行动线之外——锁门不阻断轮换。
  - **异常 ×2（夜班显形，锁夜平息）**：`GhostProduction` 自开的生产线（机器自转渐强→骤停死寂，幽灵进站三段协程复刻，不动谜题状态）；`PatrolBeam` 巡检灯束（annex 走廊扫光，扫中玩家 → HUD 乱码一瞬=EmfStorm 短脉冲 + 刺点，无伤害）。
  - **撤离窗口**：`FactoryExtractionTimer`（只挂昼场景一份；昼/夜双 zone **运行时按类型查找**——跨场景引用不可序列化，ADR-0002）；归零锁死 `anywhere-door-FactoryDay`，回实验室复位解锁。⚠️ **Metro 的 `ExtractionTimer` 同步加了两道守卫**（复位需 `WorldPresence.InHome` + 锁门需玩家在 Metro 站）——ExtractionClock 是全局唯一一口钟，多层域共存必需；对 Metro 单层域时代行为零变化。
  - **异常物 ×2**（订单目标 3→5，Inventory 容量 3→5，全部订单板实例同步）：昼「停在 17:59 的考勤卡」（值班角柜顶）/ 夜「对不上班的交接本」（泵房木箱，带补光+内页自发光）。
  - **HUD**：OrderBoard 工厂形态新增**排班行**（「换班 X:XX → 夜班（歇班的世界死寂）」/ 谜题后「排班钟已停——锁定X」）+ 提示行与详情按世界区分；DutyBriefing 工厂真文案（昼/夜各一条）。
  - **工具 `FactorySceneDev`**（MetroSceneDev 同构）：`Report/Boxes/SurveyShots/Shot` 勘察 + `Stage0_Sfx`（程序合成 5 音效：换班钟/机器嗡鸣/金属敲击/扫光刺点/闸门滚动 → `Assets/Audio/Factory/`）+ `Stage1_Core(bool night)/Stage2_Anomalies/Stage3_Player/Stage4_Orders` + `FixPass1~3`（截图评审视觉修正：贴墙定位用**射线探测**真实墙面，勿目测）+ `PT_Setup/PT_Drive/PT_Status/PT_SolvePuzzle/PT_LockFlow/PT_Extraction`。工厂常驻开发玩家 `Player(工厂开发)` @ FactoryDay (−4320, 1.1, 3)。
  - **复验（2026-10-22）**：PT 解谜全链（错序拒绝→按序三台→锁定→冻结→双侧门开）/ 锁流转（冷却拦截→改锁→异常平息）/ 撤离（武装→归零锁门→回家复位）全过；实验室开局穿门落点 (−4324.00, 0.06, −0.02) 精确门槽、presence/InHome 正确、HUD 订单视图含排班行（截图 `Assets/Screenshots/screenshot-20261022-231134.png`）；Metro 进站武装/回家复位回归通过；控制台 0 报错。
  - 已知测试环境伪象：从工厂直接 Play 时 `WorldPresence.Home` 未初始化（InHome 恒真、播报不触发、撤离钟会被误复位）——实验室正常开局不受影响，验证工厂动线请从 SpecimenLab 起 Play。
  - 夜侧照明教训：离屏渲染比实机暗很多，异常物一律带局部补光 + 自发光（Metro 软盘同款先例）；挂钟时针/分针静止重合在 12 点属正常（游玩时随班次分开）。
- ✅ 敌人 AI（食尸鬼 Ghoul）落地（2026-10-30/31，逐轮按玩家实测反馈迭代；完整说明见 **`Docs/ENEMY-AI.md`**）：
  - **文件**：`Assets/Scripts/World/EnemyGhoul.cs`（FYP.World，约 1345 行，全部中文注释 + Tooltip）、`Assets/Scripts/Core/NoiseBus.cs`（FYP.Core 静态听觉总线，解耦 Player→World）、`Assets/Prefab/Enemy/GhoulEnemy.prefab`（**拖进任意场景即可用**）。素材是用户导入的商店包 `Assets/Prefab/Enemy/Ghoul.prefab`（legacy `Animation`，无 Animator/无 Collider，走 in-place 动画，位移全由 NavMeshAgent 驱动）。
  - **体型/速度按用户要求**：可见体型 = 玩家的 **1.5 倍**（3.0 m，`Ghoul` 子物体 scale 0.423 / localY 0.645，CapsuleCollider 3.0×0.6）；`patrolSpeed 1.6` / `chaseSpeed 3.2` = 玩家走 2 / 跑 4 的 **0.8 倍**。
  - **导航**：无可用的编辑器烘焙 NavMesh（7 个场景的 `NavMeshSettings` 全指向悬空 guid `d2fc871308fdb5747b1bc6b5cefdaf00`）→ **运行时按场景烘焙**（`NavMeshBuilder`，静态 `s_bakedData` 每场景一份、多只敌人共享；150×150 约 30 ms、300×300 约 175 ms）。烘焙用**独立小号代理** `bakeAgentRadius 0.35 / height 2.0 / climb 0.6`，与 3 m 体型解耦。
  - **踩过的根因（别回退）**：① `agentClimb 0.4 < 门口 0.55 m 台阶` → 导航面在门口断成两岛，「进不了门也出不了门」（`climb=0.6` 即 `PathComplete`，另有 `directApproachRange=12` 的贴脸直冲兜底）；② `SamplePosition(10 m)` 会把敌人吸到 y≈9.97 的**屋面** → 改为 `GroundPointBelow`（向下 `RaycastAll` 取最低点）+ 1.5 m 吸附 + 推迟一帧；③ 蒙皮包围盒不是每帧刷新，**逐帧按 bounds 锁地会来回抖** → 改为按剪辑固定补偿（Idle 0.03 / Walk −0.15 / Run 0.01 / Attack1 −0.19，按 `localScale.y / 0.27` 换算）；④ `PathPartial` 也触发补烘 → 每次追击连烘 3 次卡顿，改成只在目标超范围时补烘 + 2 s 节流。
  - **感知**：视觉扇形 ±35°（`viewAngle 70`）/ 视距 35 m + 贴身 5 m 内不看角度 + 单次射线遮挡；**发现节奏**（`detectTimeNear .6` → `detectTimeFar 2.2`，蹲下 ×1.7）累积到 1 才锁定，不再 0 帧发现；**听觉**（`NoiseBus`，走路 0.55 / 奔跑 1.0 / 蹲行 0.2，`hearRange 18 m × 响度`）→ 只进入 `Search` 去查看，不直接锁定；蹲下把视距压到 0.5、贴身半径压到 0.45（判定用 `controller.height ≤ 1.5`，不读私有 `crouching` 字段）。
  - **状态机** `Patrol / Search / Chase`：丢视线 3 s → 走到**最后可见点**搜索 7 s（到点原地左右扫视 ±55°、90°/s）→ 超时回巡逻；攻击前摇 0.45 s 结束时**复核距离（≤1.3×attackRange）+ 视线**，跑开或躲掩体后不掉电。
  - **实测验过**：20 m 正视约 1.9 s 锁定 → 追到 6.9 m；丢视线 3 s 转 `Search`、到位环视、7 s 后回 `Patrol`；`Emit(0.6)` 9.4 m 外触发 `Search`（目标点等于噪音点），0.2 响度与 30 m 外忽略；前摇中瞬移 6 m 外**未掉电**；双向穿门（屋内外）均 `PathComplete`；控制台 0 error。
- ⏳ 待办（按优先级）：
  1. 玩家实测 Metro + 工厂昼夜闭环 + 三世界门动线（Metro 重点：E 键自然交互、90s 节奏体感、黑暗 ×1.5 的掉电压力、车厢奖励动线、账单/图鉴/播报观感；工厂重点：换班节奏体感、歇班死寂的引导是否够、错序复位的挫败感、深区泵房探索动线、异常显形的惊吓度、锁定奖励的意义感）；
  2. **水淹区玩法闭环**（下一个大项）——提案：积水短路易 2 倍+抖屏、扳手关 3 个排水阀、水位下降露下层区+防水涂层；素材自带水/雾/混响/8 音效；
  3. 收容图鉴扩展：Metro 3 + 工厂 2 已就位，水淹区配 2 件 + 差异化档案（素材建议见 ASSET-SOURCING）；
  4. 仓库卫生：`Docs/tools/node_modules`（418 个文件已入库）、截图目录（PortalDumps / Assets/Screenshots）的跟踪与忽略策略（snippets/ 与本地 vendor 包已入库）；**素材合规已处理（2026-10-23）**——`sci-fi-train/` 与 `MatrixVFX-master/` 已按上条处置。**素材盘点、清理记录与恢复方法见 `Docs/ART-ASSETS.md`**（2026-10-23：已把 1243 MB / 11012 文件闲置素材**移出** `Assets/` 至仓库外 `..\FYP-asset-backup\`——`_asset_staging/` 1030 MB、`MCO_Demo_Pack_v2` 136 MB、`sci-fi-train` 44 MB、`HDRIs` 19 MB、`Stylized_Alien` 14 MB；`Assets/` 由 ≈5.5 GB 降至 4.14 GB。**保留**：`Art/Props/Spaceships`+`Art/Props/Aircraft` 178 MB、世界包内 177 MB 冗余贴图、`Sourced/Textures` 1k 备用池；**分类整理**：第三方素材已统一到 `Assets/Art/`（`Worlds/` `Props/` `Characters/` `Animations/UAL/` `VFX/MatrixScan/`）——保持包内结构原样，`Assets/Resources/` 内部路径被 `Resources.Load` 字符串依赖、勿动）。**2026-10-27 第二轮清理**：再移出 **1087 MB**（Sci-Fi Bio Laboratory 633 MB、Excelsior 170 MB、Citypark 预制体 112 MB、旧截图 72 MB 等零引用资产），`Assets/` 现 **8769 MB**；明细与可继续瘦身的项见 `Docs/ART-ASSETS.md` §12。
