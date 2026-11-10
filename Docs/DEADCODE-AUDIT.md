# 死代码审计报告（Unity 适配版）

生成时间：2026-11-11
扫描范围：`Assets/Scripts/` + `Assets/Editor/` 共 115 个 .cs（排除第三方 `Packages/` 与 `Assets/Art/`）

> ⚠️ **Unity 特有的判断难点**：Unity 靠 `.meta` 里的 **GUID** 引用资源，grep 找不到类名 ≠ 没使用。
> 本报告的做法：① 场景/预制体里搜脚本 GUID；② 代码里搜 `AddComponent<T>` / `typeof(T)` 动态挂载；
> ③ 识别 `[MenuItem]` 编辑器工具（菜单调用，不产生场景引用）。

---

## 结论：**没有可以安全删除的死代码**

初筛出 28 个"其他文件零引用"的类，逐一核查后全部有正当用途：

### A. 编辑器菜单工具（16 个）— 绝不能删

通过 Unity 菜单调用，不出现在场景里，所以 GUID 扫描永远查不到：

`CrtCharsetGenerator` · `DoorPairFixer` · `FixHingeTool` · `GlbPrefabUnpacker` ·
`HitchAttribution` · `HitchBench` · `HitchBench2~5` · `MatrixScanDemoBuilder` ·
`PortalDiagnostics` · `SceneOptimizer` · `TextureDownsizer` · `UnbatchStaticTool` · `WorldSlotAudit`

其中 `WorldSlotAudit`（世界槽位审计）、`DoorPairFixer`（门配对修复）在 AGENTS.md §4 里被明确引用为工作流的一部分。

### B. 代码动态挂载的 MonoBehaviour（3 个）— 不能删

| 类 | 挂载方式 | 用途 |
|---|---|---|
| `SoundOcclusion` | `AddComponent<SoundOcclusion>` | 音效遮挡（隔墙声音变闷），恐怖空间感基础 |
| `StartupDeployment` | `AddComponent<StartupDeployment>` | 开局部署，用加载屏遮住世界实例化的卡顿 |
| `Crosshair` | `AddComponent<Crosshair>` | 屏幕中心准心，自动生成无需摆放 |

### C. 正在开发中（1 个）— 不要动

| 类 | 状态 |
|---|---|
| `ArchivePickup` + `ArchiveReader` | **2026-11-09 23:47/23:53 刚修改，尚未提交**（git status = M）。残档拾取 + CRT 阅读窗功能开发中。 |

### D. 场景中真实挂载（6 个）— 已确认在用

`EnemyGhoul` · `FactoryAtmosphere` · `FactoryLighting` · `GearChest` · `ShiftWatchPickup` · `HeldItem`

---

## 真正该处理的其他卫生问题

1. **`.agents/worktrees/dsh-e206fa3f-bd14/`** — 一份完整的仓库副本（未跟踪），包含自己的 `Assets/`、`Packages/`、`.git`。
   扫描时导致每个文件出现两次。**建议删除**（它是 agent 工作树残留，非项目内容）。
2. **`Docs/tools/node_modules`（418 文件已入库）** — 应该 gitignore + `git rm --cached`。
3. **截图目录**：`PortalDumps/` 与 `Assets/Screenshots/` 的跟踪策略未定。
4. **11 个超过 20KB 的自己代码文件** — 见 `Docs/FILE-SIZE-AUDIT.md`，钩子已装，这些是存量，不强制拆。

---

## 复核方法

想再确认某条，用：

```powershell
# 查某个脚本的 GUID
Select-String -Path "Assets/Scripts/Content/ArchiveReader.cs.meta" -Pattern "guid:"
# 再在场景里搜
Select-String -Path "Assets/Scenes/*.unity" -Pattern "<上面拿到的 guid>"
```
