# 《门阈行动》美术素材清单（整理版）

> 生成时间 2026-10-23；同日执行清理（§7）并完成分类整理（§11），体积数据为整理后实测。数据口径：**场景 + 预制件可达性**——以工程里全部 `t:Scene` + `t:Prefab`（538 个根）为起点跑 `AssetDatabase.GetDependencies`，得到 1804 个可达资产；凡出现在该集合里的素材记为「已接入」。体积 = 文件字节数（含 `.meta` 的目录体积会略大）。
> 相关文档：素材来源与许可核验见 [`ASSET-SOURCING.md`](ASSET-SOURCING.md)，术语见 [`../GLOSSARY.md`](../GLOSSARY.md)，门系统数值见 [`../AGENTS.md`](../AGENTS.md)。

## 1. 一句话结论

工程美术素材约 **4.0 GB**（`Assets/` 合计 4153 MB / 7315 文件，排除 `.git` / `Library` / `Temp` / `obj`）。其中 **真正接入游戏的约 3.5 GB**，集中在四个世界场景；**剩余闲置约 0.5 GB**。

**2026-10-23 已执行两步**：① **清理**——把 **1243 MB / 11012 文件**闲置素材**移出** `Assets/`，备份在**仓库外**的 `C:\Users\xiaoming\Downloads\FYP-asset-backup\`（保留原相对路径，可原样搬回，见 §7）；② **分类整理**——所有第三方美术素材归入 **`Assets/Art/`** 统一分类树（见 §11），移动全部走 `AssetDatabase.MoveAsset`，GUID 不变、引用零破坏。用户决定**保留** `Spaceships` + `Aircraft`（178 MB）。

## 2. 总表（分类整理后的目录）

| 目录 | 体积 | 资产数 | 已接入 | 角色 | 许可 |
|---|---:|---:|---:|---|---|
| `Assets/Art/Worlds/` | 3544 MB | 5517 | **3381 MB / 1531** | 四个世界场景 + 样例场景的美术（原 `Material_Scence/`） | 混合（见 §3） |
| `Assets/Art/Props/` | 224 MB | 525 | 5 MB / 7 | 可交互道具、门模型、飞船/飞机（原 `items/`） | 混合（见 §5） |
| `Assets/Art/Characters/` | 58 MB | 153 | 29 MB / 30 | MCO 怪物包 v1（原 `Role/MCO_Demo_Pack/`） | CryEngine 来源，待留档 |
| `Assets/Art/Animations/UAL/` | 69 MB | 162 | 28 MB / 21 | 玩家动画 `UAL1` / `UAL2`（原 `Role/Player/Animations/`） | CC0（`License.txt` @Quaternius） |
| `Assets/Art/VFX/MatrixScan/` | 2 MB | 21 | 2 MB / 6 | 屏幕空间扫描/矩阵特效（原 `Material/MatrixVFX-master/`） | **MIT** |
| `Assets/Resources/Sourced/` | 133 MB | 299 | 3 MB / 5 + 字体 | CC0 素材库（贴图/模型/音效/字体） | CC0 / OFL / ambientCG / Poly Haven |
| `Assets/Screenshots/` | 64 MB | 212 文件 | — | 验收截图（部分入库，非素材） | 自产 |
| `Assets/Audio/` | 4 MB | 16 | 3 MB / 7 | 程序合成音效 + 场景自带音 | 自产 / 第三方 |
| `Assets/Material/` | 1 MB | 44（+ 已瘦身 VFX 移至 Art/VFX） | 0 MB / 28 | **FYP 自制材质**（`Metro*` / `Factory*` 的 `.mat`） | 自产 |
| `Assets/Scenes/` | 17 MB | 10 | — | 7 个场景文件 | — |

## 3. `Art/Worlds/` 分解（世界美术）

| 子目录 | 体积 | 已接入 | 服务于 |
|---|---:|---:|---|
| `FloodedGrounds/` | 2694 MB | 2600 MB / 915 | 水淹区世界（`Flooded_Grounds.unity`） |
| `Factory/` | 507 MB | 500 MB / 344 | 工厂·昼 / 工厂·夜 |
| `CountryHouse/` | 279 MB | 278 MB / 51 | Build Settings 里的第三方样例场景 |
| `SciFiMegaKit/`（原 `Modular SciFi MegaKit[Standard]`） | 60 MB | 未单独统计 | 实验室道具（原目录名含方括号，脚本枚举需 `-LiteralPath`；整理时已去掉方括号） |
| `Metro/`（原 `PixelDrift Metro Free`） | 9 MB | 7 MB / 148 | Metro 站 |
| `Interrogation/` | 3 MB | 2 MB / 4 | 实验室审讯室布景 |
| `TerrainLayers/`（原 `_TerrainAutoUpgrade`） | 1 MB | 0 | 导入器残留 |
| ~~`sci-fi-train/`~~ | ~~43 MB~~ 0 | **0** | ✅ 2026-10-23 已移出（Sketchfab 许可冲突，见 §7） |

## 4. `Art/Characters/` + `Art/Animations/`（角色与动画）

| 子目录 | 体积 | 已接入 | 说明 |
|---|---:|---:|---|
| `Characters/MCO_Demo_Pack/` | 58 MB | 29 MB / 30 | MCO 怪物包 v1（部分已用于异世界生物） |
| `Animations/UAL/` | 69 MB | 28 MB / 21 | 玩家动画（`Clips` + `UAL1` + `UAL2`，`License.txt` 为 CC0） |
| ~~`MCO_Demo_Pack_v2/`~~ | ~~136 MB~~ 0 | **0** | ✅ 2026-10-23 已移出（v1 已够用） |
| ~~`Stylized_Alien/`~~ | ~~13 MB~~ 0 | **0** | ✅ 2026-10-23 已移出（6 个静态 FBX，无骨骼无动画） |

## 5. `Art/Props/` 分解（道具）

| 子目录 | 体积 | 已接入 | 说明 |
|---|---:|---:|---|
| `Spaceships/`（原 `spaceships/`） | 174 MB | **0** | ✅ **保留**（用户决定，题材备用；曾判「与题材无关」） |
| `RandomObjects/` | 47 MB | 4 MB / 2 | 箱子、万用表、软盘、挂锁、手机、扳手等（已用：万用表、软盘） |
| `Aircraft/`（原 `plane/`） | 4 MB | **0** | 飞机模型 |
| `Exploration/`（原 `Exploration objects/`） | 2 MB | 2 件 | 手电筒、EMF、灵盒、热像仪等调查道具（手电筒已接入） |
| `Doors/`（原 `anywhere-door-pintu-kemana-saja/`） | 1 MB | 门模型 | 任意门模型来源 |

## 6. `Resources/Sourced/` 素材库（CC0 / OFL，已核验）

| 子目录 | 体积 | 已接入 | 备注 |
|---|---:|---:|---|
| `Textures/` | 107 MB | 3 MB / 4 | 统一保留 **1k**；4k / 8k 已于 2026-10-23 删除（48 文件 / 746 MB，删除前引用数 = 0） |
| `HDRIs/` | ~~19 MB~~ 0 | **0** | ✅ 2026-10-23 已移出（内置渲染管线，未使用 HDRI 环境光） |
| `Fonts/` | 16 MB | 0（静态） | **运行时使用**：`CrtTheme` 经 `Resources.Load` 加载 `Sourced/Fonts/NotoSansMonoCJKsc-Regular` 与 `VT323-Regular` |
| `Models/` | 5 MB | 1 件 | Poly Haven 道具（60 个 FBX）与 ambientCG 材质 |
| `Audio/` | 2 MB | — | Kenney / OGA 音效 |
| `Documentation/` | <1 MB | — | `CREDITS.md` 许可原文与署名 |

> ⚠️ **静态引用分析看不到 `Resources.Load`**。当前只有三处按字符串路径加载：`Assets/Scripts/Core/CrtTheme.cs:33,44`（字体）、`Assets/Scripts/Content/InventoryUI.cs`（`UI/Icons/` 图标）、`Assets/Scripts/Player/EmfDetector.cs:294,295`（`Scan/` 计算着色器与噪声贴图）。这几类素材必须保留在 `Assets/Resources/` 的原有相对路径下，其余素材请一律走序列化引用，以便引用体检有效。

## 7. 清理记录（2026-10-23 已执行）

处理方式 = **移出**（`Move-Item`），不是删除：所有文件以原相对路径保存在仓库外的 `C:\Users\xiaoming\Downloads\FYP-asset-backup\`，随时可搬回；同时清掉仓库内残留的空目录与它们的 `.meta`。下表路径为**移出前**的位置。

| 已移出 | 体积 | 文件数 | 依据 |
|---|---:|---:|---|
| `Assets/Resources/_asset_staging/` | 1030 MB | 10591 | 原始下载暂存，已 gitignore；缩短导入时间 |
| `Assets/Role/MCO_Demo_Pack_v2/` | 136 MB | 329 | 引用体检 **0 引用**，v1 已够用 |
| `Assets/Material_Scence/sci-fi-train/` | 44 MB | 34 | 0 引用 + Sketchfab 许可冲突 |
| `Assets/Resources/Sourced/HDRIs/` | 19 MB | 6 | 0 引用，内置管线不用 HDRI |
| `Assets/Role/Stylized_Alien/` | 14 MB | 33 | 0 引用 |
| **合计** | **≈1243 MB** | **11012（含 .meta）** | |

**保留（用户决定）**：`Art/Props/Spaceships/` + `Art/Props/Aircraft/`（178 MB，题材备用）；世界包内 177 MB 未引用贴图（世界包常含备用贴图与 LOD，不宜一刀切）；`Sourced/Textures` 1k 备用贴图池。

**MatrixVFX 瘦身**：只保留屏幕空间扫描/矩阵特效所需文件——`ScreenSpaceMatrix.cs`、`Shaders/Resources/ScreenSpaceMatrixEffect.shader`、`ScreenSpaceMatrixEffectColored.shader`、`GenerateWhiteNoise.compute`、`RandomLib.cginc`、`LabColorspace.cginc`、`Texture/fonts_textures.png`、`LICENSE.md`（现位于 `Assets/Art/VFX/MatrixScan/`）；已移出 `TriPlanarMatrix.shader`、`StanUnlitDissolve.shader`、`Transition.cginc`、`TriplanarManager.cs`、`MoveCamera.cs`、README 与仓库杂项。两侧 shader 均 `Shader.Find` 成功且 `isSupported=True`。许可 = **MIT**（Copyright 2020 Shahriar Shahrabi）；原 README 里的 Global Digital Heritage 网格是**非商用**素材，但它属于 82 MB 版仓库且**从未进入本工程**。顺带修了 `#include "LabColorspace.cginc"` 与文件名 `LabColorSpace.cginc` 的**大小写不匹配**（Windows 不报错、换平台会编译失败），已重命名为 `LabColorspace.cginc` 并保留 `.meta`。

恢复方法：把 `FYP-asset-backup\` 下对应目录搬回 `Assets\` 同名位置，再 `refresh_unity`；或对被 git 跟踪的目录直接 `git checkout -- <路径>`。

## 8. 合规待决

| 素材 | 问题 | 建议 |
|---|---|---|
| ~~`Assets/Material_Scence/sci-fi-train/`（43 MB，0 引用）~~ | ✅ **已解决 2026-10-23**：整目录移出 `Assets/`（备份见 §7），公开仓库不再分发该素材（原冲突：Sketchfab Free Standard 2.2(b) 禁止裸文件再分发） | — |
| ~~`Assets/Material/MatrixVFX-master/`（1 MB，0 引用）~~ | ✅ **已解决 2026-10-23**：非商用的 Global Digital Heritage 网格从未入库（属 82 MB 版仓库）；本工程只留该仓库的 **MIT** 代码与屏幕空间特效（现 `Art/VFX/MatrixScan/`），见 §7 | — |
| `Assets/Art/Worlds/CountryHouse/`（279 MB） | 第三方样例场景，来源许可未在本仓库留档 | 补一份许可说明，或确认其仅作 Build Settings 样例 |
| `Assets/Art/Characters/MCO_Demo_Pack/` | CryEngine 来源包，仓库内无许可文本 | 补留档 |

已核验可商用、可随游戏分发：`Sourced/` 全部（CC0 / OFL / ambientCG / Poly Haven，原文见 `Sourced/Documentation/CREDITS.md`）、`Art/Animations/UAL/UAL1|UAL2`（`License.txt` = CC0 1.0 @Quaternius）。

## 9. 目录规范

1. **新素材先归入 `Assets/Art/<类别>/`**（世界包 → `Worlds/`，道具 → `Props/`，角色 → `Characters/`，动画 → `Animations/`，特效 → `VFX/`）；**保持第三方包内部结构原样**，不要按文件类型拆散（包内贴图/材质/预制件的相对引用与 `Shader.Find` 路径都可能依赖它）。
2. **下载来的 .fbx/.glb 工具资产**（仅作图标烘焙、搭建脚本的输入）放 `Assets/Resources/Sourced/Models/`；许可文本放 `Sourced/Documentation/`，命名 `<来源>_<名称>`。
3. **贴图统一 1k**：4k / 8k 不入库（`.gitignore` 已含 `Sourced/Textures/**/*_4k.*`、`**/*_8k.*`）。曾有一个 8k PNG 达 125 MB，超 GitHub 单文件 100 MB 硬上限。
4. **原始下载包不入 `Assets/`**：`_asset_staging/` 只作临时落地，导入完成后移出。
5. **资源加载方式**：字体、UI 图标、`Scan/` 着色器可 `Resources.Load`；其余素材必须由场景 / 预制件序列化引用。
6. **模型导入**：Poly Haven 模型单位是毫米，需 `Scale Factor = 0.001`；`_arm` 打包图在内置管线下需拆通道。
7. **移动素材一律用 `AssetDatabase.MoveAsset`**（保 `.meta` GUID），不要用文件系统剪切——否则场景/预制件引用全变 Missing。

## 10. 维护方法（重跑引用体检）

在 Unity 里执行（MCP `execute_code`，`safety_checks:false`）：

```csharp
var refs = new System.Collections.Generic.HashSet<string>();
foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:Scene t:Prefab"))
    foreach (var p in UnityEditor.AssetDatabase.GetDependencies(UnityEditor.AssetDatabase.GUIDToAssetPath(g), true))
        refs.Add(p);
// 再对目标目录跑 FindAssets，逐资产判断 refs.Contains(path) 并累加体积
```

注意：以 `t:Scene t:Prefab` 为根才是「游戏真正用到」；把 `t:Material` 也当根会把全部材质连同其贴图算成已使用，得出虚高结论。

## 11. 分类整理记录（2026-10-23）

**目标**：把散落在 `Material_Scence/`、`items/`、`Role/`、`Material/` 的第三方素材统一归类，**不影响现有引用**。全部移动走 `AssetDatabase.MoveAsset`（GUID 不变）。

| 原路径 | 新路径 |
|---|---|
| `Assets/Material_Scence/PixelDrift Metro Free/` | `Assets/Art/Worlds/Metro/` |
| `Assets/Material_Scence/Factory/` | `Assets/Art/Worlds/Factory/` |
| `Assets/Material_Scence/Flooded_Grounds/` | `Assets/Art/Worlds/FloodedGrounds/` |
| `Assets/Material_Scence/country house/` | `Assets/Art/Worlds/CountryHouse/` |
| `Assets/Material_Scence/interrogation/` | `Assets/Art/Worlds/Interrogation/` |
| `Assets/Material_Scence/Modular SciFi MegaKit[Standard]/` | `Assets/Art/Worlds/SciFiMegaKit/` |
| `Assets/Material_Scence/_TerrainAutoUpgrade/` | `Assets/Art/Worlds/TerrainLayers/` |
| `Assets/items/anywhere-door-pintu-kemana-saja/` | `Assets/Art/Props/Doors/` |
| `Assets/items/Exploration objects/` | `Assets/Art/Props/Exploration/` |
| `Assets/items/RandomObjects/` | `Assets/Art/Props/RandomObjects/` |
| `Assets/items/spaceships/` | `Assets/Art/Props/Spaceships/` |
| `Assets/items/plane/` | `Assets/Art/Props/Aircraft/` |
| `Assets/Role/MCO_Demo_Pack/` | `Assets/Art/Characters/MCO_Demo_Pack/` |
| `Assets/Role/Player/Animations/` | `Assets/Art/Animations/UAL/` |
| `Assets/Material/MatrixVFX-master/` | `Assets/Art/VFX/MatrixScan/`（并把内层多余的 `Assets/{Scripts,Shaders,Texture}` 扁平化到 `MatrixScan/` 下） |

清掉的空目录：`Assets/Material_Scence/`、`Assets/items/`、`Assets/Role/`（移动后逐级校验为空再删）。

**同步修改的代码**（旧路径是硬编码字符串）：
- `Assets/Editor/MetroSceneDev.cs`：`multimeter.glb` / `case_low.glb` / `Flashlight.fbx` 的路径前缀改为 `Assets/Art/Props/...`。
- `Assets/Editor/MatrixScanDemoBuilder.cs:12`：`MatDir` 改为 `Assets/Art/VFX/MatrixScan/`，其下 `Texture/...`、`Shaders/Resources/...` 两处拼接同步修正。
- 注释同步：`Assets/Scripts/World/MatrixScanDemo.cs:16-17`、`Assets/Scripts/World/FlashlightPickup.cs:5`、`Assets/Scripts/World/EmfDetectorPickup.cs:5`、`Assets/Scripts/Content/SpecimenPickup.cs:6`。
- `ProjectSettings/EditorBuildSettings.asset:12` 由 Unity **自动改写**为 `Assets/Art/Worlds/CountryHouse/Scene/country house.unity`（场景 GUID `b204886fe6f75b442adaa629c881f5f6` 未变）。

**验收**：`grep 'Assets/items/|Material_Scence|Assets/Role/'` 扫 `Assets/**/*.cs` → **零命中**；`refresh_unity(scope:"all")` 成功；`read_console` 报错 **0 条**（warnings 仅历史基线的 13 条 missing-script + 27 条 BoxCollider negative scale）。`Assets/` 由移动前的 5.5 GB 级降至 **4153 MB / 7315 文件**。

**未动的目录及原因**：`Assets/Resources/`（内部相对路径被 `Resources.Load` 字符串依赖）、`Assets/Material/*.mat` 与 `Assets/Audio/`、`Assets/Prefab/`、`Assets/Scenes/`、`Assets/Shaders/`、`Assets/WorldLinks/`、`Assets/Editor/`、`Assets/Scripts/`、`Assets/Plugins/`（编辑器脚本按字符串路径引用，整体搬迁收益低、破坏面大）。
