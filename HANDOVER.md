# 《门阈行动》交接文档

> 最后更新：2026-10-21 · 对应提交 `104f838`
> 配套阅读：**AGENTS.md**（对接手册，操作细节最全）→ **GLOSSARY.md**（术语强制）→ **Docs/adr/0001~0005**（设计决策）→ `FYP_Game_Proposal.md`（内容提案）→ `Docs/ASSET-SOURCING.md`（素材许可）

---

## 1. 项目是什么

**《门阈行动 (Operation: Threshold)》**——第一人称心理恐怖游戏（Unity 2022.3.62f3c1，Built-in 管线，旧版 Input）。

核心循环：**实验室接单 → 推门进入层域（异世界）→ 在规则压迫下回收异常物 → 归途门锁死前撤回 → 收容入账 + 电费结算 → 休整再出发**。

- 电量 = 生命值（无血条）；黑暗/技能/受创都在耗电，瘫倒会散失携带异常物的一半。
- 每个层域一条独特规则构成压迫、一个场景谜题作为解法、通关后规则反转为增益。
- 无武器、无 AI 怪物（用户明确否决黑影方案）——恐怖感来自规则、异常与氛围。

## 2. 当前完成度

| 系统 | 状态 | 说明 |
|---|---|---|
| 门/传送门（实时取景+穿行+同步） | ✅ 完整 | 8 扇门 4 对，链式拓扑，autoPair 自动配对 |
| 世界装卸 WorldStreamer | ✅ 完整 | 三规则 + 永不卸载护栏，上限 5 世界（ADR-0005） |
| 电量系统（双通道耗电/降级链/瘫倒起身） | ✅ 完整 | 含分类记账（照明/环境/机动/受创） |
| 交互与背包 | ✅ 完整 | 唯一 E 键中枢；底部道具栏 + 图标；两段式收容台（认领部件已改精确取名，见 §8.9） |
| **Metro 渐熄闭环** | ✅ 完整 | 渐熄/黑暗加倍/配电室谜题/复明级联/车厢奖励 |
| Metro 异常 ×4 | ✅ 完整 | 电磁风暴 / 幽灵进站 / 对面的灯 / 粉笔箭头 |
| 撤离窗口 + 瘫倒惩罚 | ✅ 完整 | 倒计时→锁门；瘫倒散失一半携带 |
| 探索道具 | ✅ 部分 | 手电筒、万用表已做；照相机/EMF 未做 |
| 结算反馈层 | ✅ 完整 | 电费账单 / 收容图鉴 / 值班播报（CRT 终端风） |
| **工厂昼夜玩法** | ❌ 纯美术壳 | 设计已拍板（见 §9） |
| **水淹区玩法** | ❌ 纯美术壳（已清理 Demo 残留） | 设计在提案与 AGENTS 待办 |
| 怪物 / 配额递进 / 商店 / 笔记叙事 | ❌ 未做 | 提案有完整设计 |

## 3. 十分钟跑通

1. Unity Hub 打开仓库根目录（Unity **2022.3.62f3c1**）。
2. 起始场景 `Assets/Scenes/SpecimenLab.unity`（Build Settings 第 0 项），直接 Play。
3. 主要键位：**WASD** 移动 / **Shift** 冲刺（双击翻滚）/ **Ctrl** 蹲 / **Space** 跳（含蓄力超级跳）/ 鼠标视角 / **E** 交互（唯一交互键）/ **F** 手电筒 / **Tab** 订单板详情 / **G** 调试受创（上线前移除）/ Esc 释放鼠标。
4. 推荐验证动线：实验室拿门边手电筒 → Tab 看订单板（整备形态）→ 穿粉色任意门进 Metro（左上值班播报、右上订单+撤离倒计时）→ 90 秒一段熄灯体验压迫 → 死端配电室测线合闸复明 → 车厢拿第二块软盘 → 站厅捡"复读磁带" → 回收容台两段式入账 → 看电费账单 → Tab 看收容图鉴。
5. 开发捷径：**Metro 场景内常驻 `Player(Metro开发)`**，直接从 Metro Play 也能玩（双玩家守卫保证只有一个活）。

## 4. 世界与门拓扑（链式）

```
SpecimenLab(实验室) ↔ Metro(站)
SpecimenLab(实验室) ↔ FactoryDay(工厂·昼) ↔ FactoryNight(工厂·夜) ↔ Flooded_Grounds(水淹区)
```

- 命名约定：**`anywhere-door-X-A` ↔ `anywhere-door-X`**（`-A` 在来路侧）；`PortalWindow.autoPair` 运行时按门名配对，WorldLink 资产只作参数兜底。
- 世界坐标彼此拉开：Metro 抬高 y+93.9；工厂昼 x≈−4324、工厂夜 x≈+10473；水淹区 y+2134（地面≈2147.8）。
- 门参数、坐标、不变量见 AGENTS.md §4（改门前必读）。

## 5. 玩法系统速查（职责 / 代码 / 关键点）

| 系统 | 核心代码（Assets/Scripts/） | 关键点 |
|---|---|---|
| 门/传送门 | World/AnywhereDoor · PortalWindow · WorldLink | 取景+穿行+开关同步；`SelfTest` 数值自检（AGENTS §5.1）；门关=不可穿 |
| 世界装卸 | World/WorldStreamer · WorldLightIsolation | 装卸唯一裁决者；永不卸：实验室/当前世界/门开着的世界 |
| 世界快照 | Core/WorldPresence | Home/Current/InHome——HUD 按世界切换形态的数据源 |
| 电量 | Player/BatterySystem | 环境通道 + 手电通道叠加；≤50 锁冲刺 ≤25 锁跳；0=瘫倒→起身→实验室重启；Core/BillingLedger 分类记账 |
| 交互 | Interaction/* | 交互物实现 IInteractable 自注册；InteractionLabel 自定义 [E] 文案；HintToast 全玩法反馈出口 |
| 背包/异常物 | Content/Inventory · SpecimenPickup · SpecimenDeposit | 拾取带档案（name/flavor）→ 随身 → 两段式收容入账 → 图鉴；瘫倒散失一半（含档案） |
| Metro 渐熄 | World/MetroBlackout · LightGroup · PowerCabinet 等 | 5 灯组每 90s 熄一组；黑暗区段 ×1.5（手电照明时免）；谜题=万用表测线合闸；复明广播 OnPowerRestored 级联（车厢/充电桩/车厢灯） |
| 撤离窗口 | Core/ExtractionClock · World/ExtractionTimer | 进站武装 600s；归零关门锁 E；回实验室复位解锁 |
| Metro 异常 | World/EmfStormAnomaly · GhostArrivalEvent · UnreachableLightAnomaly · ChalkTrail | 风暴=灯乱闪+HUD 乱码（Core/EmfStorm.Scramble）；幽灵进站=有征兆无列车；对面的灯=近灭远亮；粉笔箭头=顺行指路/逆行惊吓 |
| 探索道具 | Player/Flashlight · World/FlashlightPickup · MultimeterPickup | F 开关；开灯 +0.5 电/s 但免黑暗加倍；灯锥 ForcePixel（灯多场景必须） |
| HUD | Core/CrtTheme · CrtOverlay；Content/OrderBoard · BillPanel · DutyBriefing | CRT 磷光绿终端风；订单板双形态（整备/订单）+收容档案区；账单在入账时弹；播报进层域触发 |
| 玩家 | Prefab/player.prefab · PlayerMovement | 双玩家守卫（Awake 先到先得）；起身动画；蓄力跳 |

## 6. 代码分层（ADR-0004，单向依赖）

```
FYP.Core（零依赖：注册表/静态桥/UI主题）
  ↑            ↑            ↑
FYP.Player   FYP.Interaction   FYP.Content（→Core+Interaction）
  ↑            ↑
  └── FYP.World（→Core+Interaction+Player）
FYP.Editor → 全部
```

- 跨层通信走 **Core 静态桥**（PlayerGear / WorldPresence / ExtractionClock / EmfStorm / BillingLedger / CollapsePenalty）。
- 新脚本放对目录；搬迁用 `AssetDatabase.MoveAsset` 保 GUID。
- UI 一律 `CrtTheme`（等宽字/磷光绿/打字机），别自造风格。

## 7. 开发工具与自动化

- **Unity MCP**（编辑器自动化，端口 8080）：本地包 `Packages/com.coplaydev.unity-mcp`（`file:` 引用，**勿改回 git URL**）。用 MCP JSON-RPC（POST /mcp）调用；工具与片段用法见 **AGENTS.md §6**（含 curl 握手模板）。
- **测试驱动模式**：编辑器失焦 Play 不推进——计时类逻辑用 `snippets/` 里的反射驱动片段（`blackdrive`/`drive_lateupdate`/`repair_singletons` 等）；Play 中改脚本会触发域重载清空静态，修法也在 snippets。
- **分阶段搭建**：`Assets/Editor/MetroSceneDev.cs`（Stage0~6 幂等方法 + PT_* Play 驱动 + 勘察/截图/探缺工具）——新世界的搭建照这个模式写 Stage。
- **门配对工具**：`DoorLinkAutoRepair` / `DoorPairFixer`（Editor 菜单）。
- **素材规范**：新增外部素材先查 `Docs/ASSET-SOURCING.md`（CC0 白名单；BBC/freepbr/Zapsplat 排除）。CC0 素材在 `Assets/Resources/Sourced/`。
- **音效**：`generate_audio` 需在 Unity 的 MCP Asset Generation 页配 fal key；无 key 时可用 `MetroSceneDev.Stage4_MakeSfx` 的程序合成回退。

## 8. 已知问题与坑（必读）

1. **Play 中别动 Assets 文件**（含 manage_camera 截图落盘）——会触发导入/域重载，静态全灭、投影材质失效（重开会话即恢复）。截图先停 Play。
2. **编辑器失焦 Play 不推进**（runInBackground 也一样）——计时/协程测试用反射手动驱动 Update/LateUpdate。
3. 域重载后静态注册表清空（PlayerRef/BatterySystem.Instance/Inventory.Instance）——跑 `snippets/repair_singletons.cs`；组件缓存初始化放 OnEnable（勿放 Awake）。
4. SelfTest 玩家站门洞内会假 FAIL（挪开 2m+）；Metro 侧窗镜像判据 dot=−1 属已知表现（自检不补偿 mapYaw180，以实验室窗为准）。
5. 控制台基线 = 6 条历史 missing-script 警告（第三方资产）。
6. **场景清理别按"子树含目标脚本删根"**——Flooded 环境容器 "scene" 曾被整树误删（已从 git 恢复）；只删脚本宿主本体。
7. 控制台历史 6 条 missing-script 警告为第三方资产遗留，属正常基线；「0 报错」指除此之外无错误。
8. 仓库卫生欠账：`Docs/tools/node_modules`（418 文件已入库）、截图目录跟踪策略待定。
9. **收容台认领部件别用 `GetComponentsInChildren` 的遍历顺序**：装饰件 `casedeco_low` 可能排在盖子 `caseup_low` 之前，旧判据 `name.StartsWith("caseup")` 会认领到装饰件 → 盖子看着不动、玩家以为「按 E 没反应」。现改为 `transform.Find("caseup_low")` 精确取 + 兜底排除含 `deco` 的名字，`Awake` 先把缓存置 null 防旧域残留。同类「按名字认领子物体」的逻辑都该用 `Find` 而不是遍历。
10. **素材合规**：`Assets/Material_Scence/sci-fi-train/` 来自 Sketchfab（Free Standard 许可禁止裸文件再分发），与本仓库公开状态冲突；`Assets/Material/MatrixVFX-master/` 是未跟踪的完整外部 Unity 工程（含自带 ProjectSettings/Packages，会被当资源导入）+ non-commercial 网格。两者去留待定，新增素材前先查 `Docs/ASSET-SOURCING.md`。

## 9. 待办路线图（按优先级）

1. **玩家实测** Metro 闭环 + 三世界动线 + 本轮结算反馈层观感。
2. **工厂昼夜玩法闭环**（下一个大项，设计已拍板 2026-10-20）：
   - **软轮换**：不强制切换；时间到门上挂钟提示，玩家自主穿门换昼夜。不切换的代价=被锁在当前状态（**昼**：机器可见但通往深处的门锁死 / **夜**：门全开但异常显形）。
   - **夜班不掉电**（与 Metro 差异化——压迫来自轮换混乱与异常显形，不靠耗电）。
   - 谜题：值班室排班表 → 按序启动 3 台机器电源 → 锁定想要的昼夜；奖励：昼夜自选手动切换（冷却 60s）。
   - 素材就绪：FactoryDay/Night 同布局双场景（x 相差约 1.48 万，坐标可线性映射）。
3. **水淹区闭环**：积水短路易（耗电 ×2 + 抖屏）、扳手关 3 个排水阀、水位下降露下层区 + 防水涂层；场景自带水/雾/混响/8 个环境音效。
4. 图鉴扩展：工厂/水淹各配 2 件异常物 + 差异化档案（素材建议见 ASSET-SOURCING 的「背影 CRT」「多一人的合影」等）。
5. 更远：探索道具补全（照相机/EMF）、配额递进/结算升级、笔记叙事、提案其余异常池。

## 10. 文档地图

| 文档 | 内容 |
|---|---|
| `AGENTS.md` | 对接手册（最全）：环境/目录/门系统/数值/验收/工具链/踩坑/状态 |
| `GLOSSARY.md` | 术语表——写代码/注释/文档必须遵守 |
| `Docs/adr/0001~0005` | 决策记录：门实现收敛 / 跨场景引用 / 世界常驻 / 程序集分层 / WorldStreamer |
| `FYP_Game_Proposal.md` | 开题提案（怪物/经济/核心循环原文） |
| `Docs/tools/generate_proposal*.js` | 内容提案生成脚本（四支柱版设计的真正出处） |
| `Docs/ASSET-SOURCING.md` | 素材许可审计（CC0 白名单/黑名单） |
| `.workbuddy-ai/memory/*.md` | 队友工作日志（比 AGENTS 更细的过程记录） |
| 本文档 | 交接总览 |
