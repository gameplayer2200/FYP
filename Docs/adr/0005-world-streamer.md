# ADR-0005：世界装卸收敛到 WorldStreamer

- 状态：已采纳（2026-10-18 落地，2026-10-20 补记）
- 关联：ADR-0003（世界常驻）的复查条件

## 背景

ADR-0003 决定双世界 Additive 常驻、不在穿门后卸载旧世界。当时留了复查条件：
世界数量增长到 4~5 个后，显存/内存随世界数线性增长、灯光与后期互相叠加，
必须把加载职责收敛到一个可替换的抽象。

2026-10-17~18 三世界传送门落地后场景数达到 6（实验室 + Metro + 工厂昼/夜 + 水淹区），
复查条件触发。同时门拓扑重构为链式（实验室↔Metro、实验室↔工厂昼↔工厂夜↔水淹区），
"每扇门各自 preloadRadius 加载"的散装逻辑既无法限总量、也无法卸载。

## 决策

新增 `WorldStreamer`（静态类，Assets/Scripts/World/）作为**场景装卸的唯一裁决者**，
PortalWindow 只保留取景与穿行，不再自行加载场景。三条规则：

1. **需求装载**：玩家进入某扇门的 `preloadRadius` → 该门的目标世界开始装载；
2. **远离卸载**：某世界关联的所有门都超出 `2×preloadRadius + ReleaseMargin(2m)` → 卸下
   （保活半径比取景渲染距离 2×preloadRadius 大，不会"还在取景却被卸掉"）；
3. **总量封顶**：同时存活世界不超过 `MaxLoadedWorlds=5`（含实验室）；满了先淘汰
   "需求最弱"（离玩家相对最远）的可卸世界，腾不出空位就暂缓本次装载。

**永不卸载**（护栏，缺一都会出事故）：
- 实验室（`homeWorld` = 玩家根对象所在场景，开局登记）；
- 玩家当前所在世界（穿门时 `NotifyCrossed` 登记——异世界里实验室侧的门远在天边，
  没有这条护栏会卸掉玩家脚下的世界）；
- 任何"门还开着"的世界（免得开着的门洞后面突然没了世界）。

配套：`WorldLightIsolation` 在世界装载后立即做光照隔离（每个世界改挂专属图层，
世界灯只照本世界，实验室灯排除全部世界层；玩家的灯跳过）。

## 试过并回退的方案（踩坑记录）

- **延迟激活（方案 A）**：装载后先 `allowSceneActivation=false` 压着，玩家走近再激活。
  坑一：`AsyncOperation.completed` 在 activation=false 时**永不触发**，完成回调链全断；
  坑二：激活空窗期间 WorldStreamer 以为没装载又发一次加载，同一场景装了两遍。
  两版实现（ActivationRadiusFactor 0.55 / preloadRadius=0 全常驻）均回退，
  终态 = 预载完成即激活（`ActivationRadiusFactor=1`），MaxLoadedWorlds=5 兜总量。
- 详见 `.workbuddy-ai/memory/2026-10-18.md`（队友工作日志）。

## 后果

- 正面：世界数增长不再线性堆内存；门配对（autoPair）与装卸解耦，各管各的；
  将来换流式分块/预算调度只改 WorldStreamer 一个类。
- 负面：装卸时序引入新时序问题——`PortalWindow.OnTargetWorldUnloaded` 收尾取景资源；
  编辑器后台测试时 Tick 不跑、`WorldPresence.Home` 不初始化（仅测试环境现象，见 AGENTS §6）。
- 补充：`WorldStreamer` 在 `TickInner`/`NotifyCrossed` 顺带写入 `WorldPresence`
  （Core 世界快照，HUD 按世界切换形态的数据源）。
