# 文件大小审计（自己写的代码）

生成时间：2026-11-11 · 阈值 20 KB（≈5,000 token）

> **为什么关心**：agent 每次改一个文件都要整份读进上下文。
> 单个文件 100 KB ≈ 25,000 token，读两个就把上下文吃掉一大半——这正是"模型变笨"的主因。

## 现状

- 自己写的 `.cs`：**115 个**，合计 **1.1 MB**
- 超过 20 KB：**11 个**（占 10%）

| 文件 | 大小 | 行数 | 约方法数 | tokens |
|---|---|---|---|---|
| `Assets/Scripts/World/PortalWindow.cs` | 96.9 KB | 1705 | ~62 | 24,818 |
| `Assets/Editor/MetroSceneDev.cs` | 92.4 KB | 1938 | ~66 | 23,652 |
| `Assets/Scripts/World/EnemyGhoul.cs` | 75.8 KB | 1764 | ~61 | 19,400 |
| `Assets/Editor/FactorySceneDev.cs` | 69.6 KB | 1355 | ~60 | 17,822 |
| `Assets/Scripts/World/WorldStreamer.cs` | 52.6 KB | 953 | ~44 | 13,461 |
| `Assets/Scripts/Player/PlayerMovement.cs` | 27.5 KB | 550 | ~12 | 7,048 |
| `Assets/Editor/DoorLinkAutoRepair.cs` | 24.2 KB | 470 | ~14 | 6,208 |
| `Assets/Scripts/Core/CrtTheme.cs` | 22 KB | 541 | ~32 | 5,628 |
| `Assets/Scripts/Content/InventoryUI.cs` | 21.3 KB | 477 | ~31 | 5,462 |
| `Assets/Scripts/Player/EmfDetector.cs` | 20.4 KB | 510 | ~28 | 5,210 |
| `Assets/Editor/PortalDiagnostics.cs` | 20 KB | 436 | ~14 | 5,123 |

## 建议拆分优先级

**第一优先（严重超标，建议尽快拆）**

| 文件 | 建议 |
|---|---|
| `PortalWindow.cs` (97 KB) | 取景渲染 / 穿行判定 / 配对逻辑 / SelfTest 四块可分离；至少把 SelfTest 拆到 `PortalWindow.SelfTest.cs`（partial） |
| `MetroSceneDev.cs` (92 KB) | 场景搭建工具，按 Stage 拆分（Stage0_Sfx / Stage1~4 / FixPass / PT_*） |
| `EnemyGhoul.cs` (76 KB) | 感知 / 状态机 / 导航烘焙 / 动画补偿四块分离 |

**第二优先**

| 文件 | 建议 |
|---|---|
| `FactorySceneDev.cs` (70 KB) | 同 MetroSceneDev，按 Stage 拆 |
| `WorldStreamer.cs` (53 KB) | 装卸裁决 / 光照隔离 / 诊断输出 |

**可暂缓（略超阈值，收益有限）**

`PlayerMovement.cs`(27KB) · `DoorLinkAutoRepair.cs`(24KB) · `CrtTheme.cs`(22KB) ·
`InventoryUI.cs`(21KB) · `EmfDetector.cs`(20KB) · `PortalDiagnostics.cs`(20KB)

## 已有防护

`.git/hooks/pre-commit` 已安装（Unity 适配版）：
- 只检查 `.cs`，忽略 `Packages/`、`Assets/Art/`、`.meta`、二进制资产
- 超过 20 KB 拒绝提交，并提示按职责拆分
- 备份在 `Docs/pre-commit-hook.sh`（可纳入版本管理）

⚠️ 钩子**只拦新提交**，存量 11 个文件不会被强制拆分——这是有意的，避免打断当前开发。

## 拆文件时的注意事项（Unity）

1. **不要动 `.meta`**——GUID 变了场景引用就断
2. 用 `partial class` 拆同一类的多份文件时，文件名可以是 `Xxx.SelfTest.cs`，Unity 正常识别
3. 拆完在 Unity 里重新编译，确认场景里的组件引用没丢（红线/None）
