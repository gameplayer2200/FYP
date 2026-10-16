# 传送门只保留一个实现，配置改为数据

传送门曾有两代实现同时挂在场景里：`PortalPair`（纯色挡板 + 世界切换）与 `PortalWindow`（偏心视锥实时取景）。
`PortalPair` 的 `labFrame` 已被置空、初始化必然失败，但它的 `Awake` 仍在改写 Build Settings，且会生成纯色挡板，
逼得 `PortalWindow` 每 60 帧调用 `SuppressLegacyQuads()` 去压制它。两套系统靠互相打补丁共存。

我们决定**删除 `PortalPair`**，只保留 `PortalWindow` 作为唯一实现，并把"门 → 世界 → 对面门"的配置抽成
`WorldLink` 数据资产（门配对）。理由是：提案要求"多门多世界"，若每扇门都在代码/Inspector 里各持一份
场景名字符串，新增一扇门就要改代码；而两套实现并存会让"哪套在生效"变成一个只能靠运行时日志回答的问题。

## Considered Options

- **保留两套、用接口切换**：为论文做"实时取景 vs 纯色挡板"的性能对比实验。否决——A/B 用 git 分支做更干净，
  不值得为一次性实验让两套实现长期共存。
- **只删 `PortalPair`、不抽 `WorldLink`**：改动最小，但每扇门的 `sceneName`/`doorName` 仍是散落在场景里的魔法字符串。

## Consequences

`PortalWindow` 从此独占场景加载职责，"两个系统抢着 `LoadSceneAsync`、靠等 2 秒决定谁先加载"的竞态计时器可以删除。
