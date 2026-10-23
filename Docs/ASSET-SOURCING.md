# 素材调研与审核清单（免费 / 不侵权 / 可随游戏分发）

> 状态：**待审核**。本文所有许可证结论均来自**实际抓取许可页原文**（引文 + URL 见各节），未凭印象判断。
> 本轮**未下载任何文件、未改动仓库**。体积极小、来源明确的项可直接下；体积大或有风险的项已单独标注。
> 适用范围：《门阈行动》Unity **2022.3.62f3c1 + 内置渲染管线（Built-in RP）**。

---

## 0. 一页结论：建议批准的 5 组

| 序 | 组 | 解决什么 | 许可证 | 体积 | 风险 |
|---|---|---|---|---|---|
| **1** | **Kenney 音效四包**（Interface / UI Audio / Impact / Sci-Fi） | 项目**目前完全没有交互反馈音**——按 E、拾取、报错、收容成功全是静音 | CC0 | 单包 KB~MB 级（未标） | 无 |
| **2** | **OGA 门音效 + 咔哒声**（Door Set 3.3 MB / 87 Clickety Clips 3.8 MB） | 门是本作核心机制，但 `AnywhereDoor` **开合完全无声** | CC0 | 7.1 MB | 无 |
| **3** | **Poly Haven 单件道具**（挂钟 / CRT / 橡皮鸭 / 夹板 / 喇叭等） | 异常物与环境叙事的实物载体；**带真实三角面数与毫米尺寸**，可精确筛选 | CC0 | 按件（1 件几 MB） | 无（但**单位要设 0.001**） |
| **4** | **ambientCG Surface Imperfections + 材质 + Poly Haven 材质** | 污渍 / 水渍 / 潮湿地毯 / 剥落墙漆 / 矿棉天花板；**内置 RP 没有 Decal Projector，这批自带 opacity 正好补** | CC0 | 每套 2~10 MB（1K-JPG） | 无 |
| **5** | **VT323 + Share Tech Mono + Noto Sans SC（子集化）** | CRT 终端风 HUD + **中文不方块**（现在中文 HUD 没字体） | OFL 1.1 | 153 KB + 43 KB + 17 MB（须子集化） | 无（注意保留字体名条款） |

**明确建议不引入的**：BBC Sound Effects（非商业限定）、freepbr（禁商用）、Zapsplat / Freesound / Sketchfab（需登录）、任何 CC-BY-NC 或 CC-BY-SA（传染性）。

---

## 1. 许可证红线（先看这个，其余都可商量）

### ✅ 可以放心用（全站统一，免登录，免署名，可随游戏分发）

| 站点 | 精确许可证 | 关键原文 | 核验链接 |
|---|---|---|---|
| **Poly Haven** | CC0 1.0 | "You can use our assets for any purpose, including commercial work. You do not need to give credit… **You can redistribute them**" | https://polyhaven.com/license |
| **ambientCG** | CC0 1.0 | "All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License" / "**You can include the raw files in your project, for example a video game**" | https://docs.ambientcg.com/license/ |
| **Kenney** | CC0 | "all game assets on the asset pages are public domain licensed (CC0)… **Attribution is not required**" | https://kenney.nl/support |

### ⚠️ 有条件可用

| 来源 | 条件 |
|---|---|
| **OpenGameArt** | **无站点级许可，必须逐件看**。只取页面标 `License(s): CC0` 或 `CC-BY` 的条目。**避开 CC-BY-SA / GPL / Sampling+** |
| **google/fonts** | OFL 1.1 / Apache-2.0。可嵌入分发，但**必须随包保留许可证文件** |
| **Sonniss #GameAudioGDC** | 允许商用、免署名、可随游戏分发，**但禁止"以音效库形式再分发"** → 见 §5 仓库风险 |
| **Poly Pizza** | 逐模型（多为 CC-BY 3.0，**要署名**）。⚠️ **下载是否强制登录未证实** → 未证实前不进主清单 |
| **Quaternius** | ⚠️ **站点许可 2026-08-28 起已从 CC0 改为 QAL v1.0**（禁止再分发素材本身）。旧包页面仍挂 CC0 徽章 → **必须逐包看徽章**，不能引用站点级声明 |

### ❌ 已排除（附证据，避免以后重复踩）

| 来源 | 原因 |
|---|---|
| **BBC Sound Effects** | 许可原文限"**non-commercial, personal or research**"；商用需另付授权，且 §4h 禁止再上传分发 |
| **freepbr.com** | "**As long as you don't use these commercially**"——个人使用免费，商用要付费 |
| **textures.com** | 订阅制 + 需登录 |
| **Sketchfab 全站** | 下载强制登录 |
| **Zapsplat 免费档** | 需注册 + **必须署名** + 仅 MP3 |
| **Freesound** | 需注册；库内大量 CC-BY-NC / Sampling+，误取即侵权 |
| **CC-BY-SA / GPL 音频与模型** | **传染性许可**，会波及整个游戏 |
| **ambientCG 的 Door / Pipe 类目** | 官方类型表标明是 **Decal（贴花）**，不是 3D 模型——当模型用会踩坑 |

---

## 2. 3D 模型

### 2.1 怪物（缺口最大）

| 素材 | 来源 | 许可 | 说明 |
|---|---|---|---|
| **Quaternius · Animated Monster Pack** | https://quaternius.com/packs/animatedmonster.html | **页面标 CC0**（逐包核实） | 4 只，**带动画、无贴图** → 直接给近黑 `Unlit/Color` 就是剪影，完全贴合低保真基调。FBX/OBJ/Blend。**OGA 有免登录镜像**（1.4 MB） |
| Quaternius · Easy Enemy Pack | https://quaternius.com/packs/easyenemy.html | 同上 | 5 种，无贴图 |
| Quaternius · Animated Zombie Pack | https://quaternius.com/packs/animatedzombie.html | 同上 | 2 只，**有 atlas 贴图** |
| **Quaternius · Universal Base Characters** | https://quaternius.com/packs/universalbasecharacters.html | 同上 | 26 件（6 体型 + 20 发型），**标准 Humanoid 骨架** → 可复用已有的 UAL1/UAL2 重定向 |

**导入要点**：`Rig → Animation Type = Generic`（Quaternius 不是标准 Humanoid，硬选 Humanoid 会扭）；动画内嵌在 FBX 里，勾 `Import Animation` 后按名切 clip；模型无碰撞体，要自己加 `CapsuleCollider` + `NavMeshAgent`。
⚠️ **别用 Quaternius 的 "Source" 版**（那是 URP 工程，材质进内置管线会变粉），只拿免费 FBX。

### 2.2 道具（异常物 / 调查 / 环境叙事）

> Poly Haven 的 API（`https://api.polyhaven.com/assets?type=models`）免登录、返回真实 **polycount 与毫米尺寸**，下表数据即来自该 API。

| 道具 | 世界 | 故事钩子 | asset id | 三角面 | 尺寸 (mm) |
|---|---|---|---|---|---|
| **逆走挂钟** | Metro | 钟从 1987 年倒着走，但报时是准的——玩家会开始不敢相信撤离倒计时 | `wall_clock` | **3658** | 320.5 × 47.2 × 320.5 |
| **背影 CRT** | 工厂·昼 | 断电的监视器，屏幕里永远有人背对镜头坐着；你绕到侧面，他也跟着转 | `television_02` | **2310** | 399.8 × 350.2 × 411.4 |
| **多一人的合影** | 水淹区 | 相框里 7 个人，值班表上这个班组只有 6 人；回头再看会变 | `standing_picture_frame_02` | **2986** | 216.9 × 102.3 × 266.4 |
| **复读磁带** | 工厂·夜 | 同一句"……你终于来了"，每播一次语气更不耐烦 | `cassette_player` | **4830** | 129.5 × 48.9 × 236.8 |
| **值班表 / 排班板** | Metro / 工厂 | 最后一栏签名全是同一笔迹，日期却跨了三年 | `clipboard` + `office_notepads` | **6176** / **666** | 228.6 × 336.6 × 42.0 / 1102.8 × 659.8 × 22.0 |
| **广播喇叭** | Metro | 电池早没了，但每次停电它都会"咔"一声 | `Megaphone_01` | **15814** | 370.7 × 228.5 × 300.1 |
| **橡皮鸭** | 水淹区 | 水是死的，它却一直在原地打转——无害的东西比怪物更让人不安 | `rubber_duck_toy` | **4288** | 213.5 × 300.0 × 285.5 |
| **复古相机** | 全层域 | 取景框能看到异常物的"真名"，但拍下来的照片是空白的 | `Camera_01` | 26987（**偏高，建议降到 ~6k**） | 213.9 × 262.8 × 77.9 |
| **便携录音机** | 工厂·夜 | 录下的环境音回放时多出一个人声 | `boombox` | 10168 | 723.8 × 186.5 × 470.1 |
| 防毒面具（可选） | 水淹区 | 环境叙事 | `old_gas_mask` | 18900 | 210.5 × 308.7 × 1125.7 |

**必须自建的 6 件**（找不到现成合规模型，但都是 Quad+贴图 或 Box+表盘级别）：
`错号门牌`、`烧不坏的员工证`、`声级计`、`盖革计数器`、`内窥镜`、`旧报纸`。
> 项目里**已经用程序化拼装做过软盘**（原 fbx/glb 损坏），这条路径是已验证的，不是理论方案。

**Poly Haven 导入三个必改项**：
1. `Model → Scale Factor = 0.001`（单位是**毫米**，不改会放大 1000 倍）
2. 贴图从 4K/8K 降到 **512~1024**（否则 Metro 66 盏灯的场景显存爆炸）
3. 材质是 PBR → 用 `Standard` 手动接 albedo / normal / roughness / metallic

---

## 3. 音效

> **当前状态：`Assets/Audio/Metro/` 只有 2 个程序合成的 wav（`emf_storm` / `arrival_pressure`）。UI、门、脚步、拾取全是静音。**

| 素材 | 来源 | 许可 | 体积 | 用在哪 |
|---|---|---|---|---|
| **Kenney Interface Sounds**（100） | https://kenney.nl/assets/interface-sounds | CC0 | 未标 | `InteractionRunner` 的 `[E]` 提示、`HintToast` 弹出、订单完成、瘫倒 |
| **Kenney UI Audio**（50） | https://kenney.nl/assets/ui-audio | CC0 | 未标 | `OrderBoard` Tab 展开、背包切换 |
| **Kenney Impact Sounds**（130） | https://kenney.nl/assets/impact-sounds | CC0 | 未标 | 关门闷响、金属撞击、脚步底层 |
| **Kenney Sci-Fi Sounds**（70） | https://kenney.nl/assets/sci-fi-sounds | CC0 | 未标 | 万用表蜂鸣、闸刀、线缆插拔 |
| **OGA · Door Open, Door Close Set** | https://opengameart.org/content/door-open-door-close-set | CC0 | **3.3 MB** | `AnywhereDoor.SetOpen()` 开/关（对齐 0.8s 动画）、`ExtractionTimer` 锁门 |
| **OGA · 87 Clickety Clips** | https://opengameart.org/content/87-clickety-clips | CC0 | **3.8 MB** | 配电室闸刀、手电筒 F 开关、旋钮 |
| **OGA · Atmospheric Interaction Pack**（43） | https://opengameart.org/content/atmospheric-interaction-sound-pack | CC0 | **8.7 MB** | 报错/确认/警告语义音 |
| **OGA · Different Steps**（8） | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 | **77.7 KB** | 脚步基础层（石/木/砾石） |
| OGA · 30 CC0 SFX loops | https://opengameart.org/content/30-cc0-sfx-loops | CC0 | 3.5 MB | 机房底噪、空站台环境 loop |
| OGA · Loopable Dungeon Ambience | https://opengameart.org/content/loopable-dungeon-ambience | CC0 | 1.6 MB | 隧道底噪（低频风 + 滴水） |
| OGA · Ghost Moans / 80 creature SFX | OGA 站内 | CC0 | 4 MB / 1.9 MB | 耳语、非人声低吟（后者是口技合成，只取 monster/roar/grunt） |

**导入要点**：短音 `Decompress On Load` + `PCM`；世界内 3D 音 `Force To Mono` + `Spatial Blend = 1`；UI 音保持 stereo。**loop 必须勾 `Loop` 并把首尾裁到零交叉**，否则每圈咔哒。

> ⚠️ **项目缺 AudioMixer**：停电要压低环境音、EMF 风暴要叠电流、幽灵进站要做"渐强→骤停"，这些都需要 `Master / SFX / Ambience / UI` 分组总线。建议一并加。

---

## 4. 视觉（贴图 / HDRI / 字体 / UI）

### 4.1 贴花与表面细节 —— 内置 RP 最大的坑，但有解

**内置 RP 没有 Decal Projector**。三条落地路径（推荐第 1 条）：
1. **透明 Quad**：Plane/Quad + `Standard` → Rendering Mode = Transparent，Albedo 的 A 通道接 opacity 贴图，离墙 1~5 mm 防 z-fighting。静态贴花全用它。
2. `Projector` 组件（内置 RP 仍保留）+ 自写 ~30 行 projector shader，可做墙角多面投影。
3. 烘焙进墙面第二套 UV —— 最稳但不可复用，不推荐。

| 素材 | 来源 | 许可 | 说明 |
|---|---|---|---|
| **ambientCG · Surface Imperfections**（20 套） | https://ambientcg.com/ | CC0 | **自带 opacity 图** → 正好补上面第 1 条。污渍 / 水渍 / 指纹 / 灰尘 / 咖啡圈 |
| ambientCG · Decals（Leaking 39 / RoadLines 69 / ManholeCover 11） | 同上 | CC0 | ⚠️ **不是每个都带 opacity**，选之前确认 `maps` 里有 `"opacity"` |
| Poly Haven · `dirty_carpet` / `worn_tile_floor` / `rough_plaster_broken` / `metal_plate` | https://polyhaven.com/ | CC0 | **潮湿地毯（全站仅此 1 套）**、旧瓷砖、剥落墙漆、格栅钢板 |
| ambientCG · `OfficeCeiling001`（含 emission） | 同上 | CC0 | **矿棉天花板 + 亮着的灯盘**，一格解决两件事 |
| Kenney · Splat Pack（30） | https://kenney.nl/assets/splat-pack | CC0 | 血迹/污渍（CC0 可任意改色，比 CC-BY 的血迹包省心） |

⚠️ **真的没有现成合规资源的三样**：涂鸦、霉斑、粉笔箭头。
- 粉笔箭头**工程里已经是运行时程序化绘制**（无贴图资产），不需要买。
- 霉斑用 Poly Haven `castle_brick_01`（tags moss/sewer/damp）或 `SurfaceImperfections017` 染绿。
- 涂鸦建议自绘 3~5 张，或用 game-icons.net 的 SVG 转 alpha（CC-BY 3.0，要署名）。

**导入要点**：`_NormalGL` → Texture Type = **Normal map**（sRGB 关，否则法线发绿）；`_Opacity` → sRGB 关、Wrap = Clamp；Poly Haven 的 `_arm` 是 **R=AO / G=Rough / B=Metal 打包图**，内置 Standard 没有 mask 槽 → 必须拆通道（Rough 取 G 反相填 Smoothness）。

### 4.2 HDRI

| 素材 | 许可 | 用途 |
|---|---|---|
| Poly Haven `dresden_station_night`（2K ≈ 6.7 MB） | CC0 | 荧光灯照亮的空站台——**与 Metro 几乎同构** |
| Poly Haven `kloppenheim_04`（雾夜，2K ≈ 6.7 MB） | CC0 | 雾天层域 |
| Poly Haven `creepy_bathroom`（2K ≈ 6.5 MB） | CC0 | 废弃湿冷室内 |

**用 2K 就够**（内置 RP 反射探针看不出 8K 差别）。导入：Texture Shape = **Cube**（或 2D + `Skybox/Panoramic`），Compression = **None**（HDR 压缩毁暗部）。
> ⚠️ 实验室是纯黑天空盒，**保持现状**；HDRI 只给异世界用。

### 4.3 字体 —— 有一个坑必须避开

现有 HUD 是 **IMGUI**（`OrderBoard.cs` / `SpecimenHud.cs` 都是 `OnGUI()`），中文**目前没有字体**。

| 字体 | 许可 | 体积 | 用途 |
|---|---|---|---|
| **VT323** | **OFL 1.1** | 153 KB | CRT/终端标题、数字 |
| **Share Tech Mono** | **OFL 1.1**（⚠️ **Reserved Font Name "Share"**——改了字体文件就必须改名） | **43 KB** | 终端正文，最省 |
| **Noto Sans SC** | OFL 1.1 | **17 MB**（可变字重） | **中文 HUD 必须**，否则全是方块 |
| Courier Prime | OFL 1.1 | 71 KB | 打字机/笔记 |
| Caveat | OFL 1.1 | 404 KB | 手写笔记碎片 |
| Bebas Neue | OFL 1.1 | 61 KB | 警示标签 |

**⚠️ 许可证纠正**：`Special Elite` **不是 OFL**，它在 `apache/` 目录下、许可是 **Apache-2.0**（要求保留 LICENSE/NOTICE + 改动声明）。可以用，但不能当 OFL 用。

**中文那 17 MB 怎么办**：IMGUI 的 dynamic font 会**整个打进构建**、无法子集化 → 中文 HUD 建议走 **TMP Font Asset Creator 只烘实际用到的 ~100 个字**（Atlas 512²，产物几十 KB）。`com.unity.textmeshpro: 3.0.7` 已在 manifest 里，不用装包。

### 4.4 UI / 粒子 / 灯光遮片（全部 CC0）

| 素材 | 来源 | 文件数 | 用在哪 |
|---|---|---|---|
| **Kenney UI Pack Sci-Fi** | https://kenney.nl/assets/ui-pack-sci-fi | 130 | **CRT 终端面板边框** |
| **Kenney Game Icons** | https://kenney.nl/assets/game-icons | 105 | 电量/背包/手电筒图标（单色可 tint） |
| **Kenney Particle Pack** | https://kenney.nl/assets/particle-pack | 80（512×512） | 尘埃、雾噪、配电室火花 |
| **Kenney Light Masks** | https://kenney.nl/assets/light-masks | 150 | **直接当聚光灯 Cookie** 做百叶/窗棂投影，比程序化扫描线省事 |

> 胶片颗粒 / 扫描线后期：**没有 CC0 现成包**。建议自写 `OnRenderImage` 图像效果（约 80 行 shader），颗粒贴图用 Kenney Particle Pack，扫描线程序生成。

---

## 5. ⚠️ 仓库风险：公开仓库 vs 私有仓库

这一条会影响能装哪些素材，**需要你确认**：

- **Poly Haven / ambientCG / Kenney 的 CC0 素材**：明文允许 "include the raw files in your project" / "redistribute them" → **公开仓库也没问题**。
- **Sonniss #GameAudioGDC（23.5 GB 专业音效库）**：许可允许随游戏分发、免署名，但**禁止"以音效库形式再分发"**。若毕设仓库是**公开 GitHub**，把原始 WAV 提交进 `Assets/` 字面上接近违规。
  → 稳妥做法：音效放 `Assets/Audio/` 并**加入 `.gitignore`**（或 LFS + 私有仓库），只在构建产物里包含。
- **Pixabay / Mixkit**：同样有 "Standalone 转售" 禁令 → **游戏成品可以，素材裸文件公开不行**。

**建议**：如果仓库是公开的，本轮**先只装 CC0 四组（§0 的 1/2/3/4/5）**，Sonniss 这类专业库先不引入，等确认仓库可见性再定。

---

## 6. 建议的落地顺序

1. **Kenney 音效四包 + OGA 门音效** → 5 分钟装完，立刻能听到"门有声了"。这是当前**体验空洞最大**的地方。
2. **VT323 + Share Tech Mono + Noto Sans SC 子集** → 改 `OrderBoard` 两行 `Resources.Load<Font>()`，终端风立刻成立。
3. **Poly Haven 五件零系统成本道具**（挂钟 / 值班表 / CRT / 橡皮鸭 / 标本罐）→ 验证"CC0 直链 → 内置管线材质"这条链。
4. **ambientCG Surface Imperfections + Poly Haven 四套材质** → 补细节层。
5. **自建 6 件**（门牌 / 员工证 / 声级计 / 盖革计数器 / 内窥镜 / 旧报纸）→ 走已验证的程序化拼装路径。

> **风格压力测试**：第 3 步做完后，把 1 只 Quaternius 剪影怪物 + 1 台 Poly Haven 写实摄像头放在同一盏荧光灯下截图。**如果写实与剪影打架，缺口 ③ 整体转向 Kenney/KayKit 低模路线**，而不是继续攒 Poly Haven。

---

## 7. 未核实项（诚实标注，别当结论用）

- Kenney 各包 zip 的**具体体积与采样率**（站点不标，本轮未下 HEAD 请求）
- Poly Pizza **下载是否强制登录**（`/m/<id>/download` 与 `/download/<id>` 均 404，页面常驻 Login → **未证实**，故未进主清单）
- Poly Pizza 的**三角面数与尺寸**（站点不提供）
- Quaternius **2024+ 新包**是否已改 QAL（只逐包核实了 2020 前后的旧包仍为 CC0）
- OGA 上除本文引用条目外的逐件许可（**OGA 无站点级许可，必须逐件看**）
- Sonniss GDC 2026（`gdc.sonniss.com`）的许可条款（站点被 Cloudflare 拦截）
- game-icons.net 具体图标名是否存在（`lorc/flashlight` 实测 404，需人工在站点搜索确认）
- Poly Haven `prepaid: true` 资产在免费档的具体分辨率上限

---

## 7.5 入库后发现的问题素材（2026-10-21 审计补记）

以下两件**已经在仓库里**（`104f838` 与未跟踪目录），但来源与本文 §1 的排除清单冲突，需决定去留。

| 素材 | 来源 | 许可判定 | 建议 |
|---|---|---|---|
| `Assets/Material_Scence/sci-fi-train/`（`Untitled.glb` 约 20MB + 贴图，合计约 44MB） | Sketchfab「Sci-Fi Train」，来源 URL 记在 `Docs/asset.txt`（`sketchfab.com/3d-models/sci-fi-train-40ab7e95...`） | Sketchfab **Free Standard**。§1 已把「Sketchfab 全站」列入排除项（下载强制登录）；其许可条款 2.2(b) 明文禁止「把素材作为裸文件分发」——本仓库是**公开 GitHub**，提交 glb 即等于裸文件再分发 | **不入库（加 .gitignore）或换 CC0 替代** |
| `Assets/Material/MatrixVFX-master/`（**未跟踪**，约 82MB） | GitHub `ShahriarShahrabi/MatrixVFX` | 代码 MIT；**网格来自 Global Digital Heritage，README 明写 non commercial use** → non-commercial = §1 红线（CC-BY-NC 类）。且该目录是完整外部 Unity 工程（含自带 `ProjectSettings/`、`Packages/`、3 个 `.cs`），放进 `Assets/` 会被 Unity 当资源导入 | **移出 `Assets/` 或删除** |

> 判定依据：Sketchfab 许可页原文抓取（条款 2.2(b)）+ MatrixVFX 仓库 README 原文（原文："The Mesh is from Global Digital Heritage and is licnesed under non commercial use"）。
> **引用扫描（2026-10-21 实测）**：`sci-fi-train` 的 18 个 GUID、`MatrixVFX-master` 的 63 个 GUID 逐一比对全部 `.unity`/`.prefab`/`.mat`——**零外部引用**（MatrixVFX 的 14 个纹理 GUID 仅被它自己目录内的 `.mat` 引用）。所以两件都还没被任何场景用到，移出或删除不会破坏现有内容。
> ✅ **处置结果（2026-10-23）**：`sci-fi-train/` 已整目录移出 `Assets/`（备份在仓库外 `..\FYP-asset-backup\`）；`MatrixVFX-master/` 已瘦身——非商用网格从未入库，只保留 **MIT** 的屏幕空间扫描特效，现位于 `Assets/Art/VFX/MatrixScan/`。两条记录保留为历史依据，详见 [`ART-ASSETS.md`](ART-ASSETS.md) §7/§8/§11。

---

## 8. 需要你拍板的三件事

1. **仓库是公开还是私有？** → 决定 Sonniss / Pixabay 这类"可随游戏分发但禁止裸文件再分发"的素材能不能进 `Assets/`。
2. **§0 的 5 组是否批准？** → 批准后我按 §6 顺序逐组下载、导入、配好导入设置，并把来源写进 `CREDITS.md`。
3. **6 件自建道具现在做，还是等素材装完再说？** → 我建议先装完素材，用真实资产在场景里对一次剪影，再决定自建件的造型。
