# 引入程序集分层

此前全部运行时脚本都在 `Assembly-CSharp` 里，与 `Assets/Material_Scence/` 下数千个第三方资产（PostProcessing、
GBCamera、PixelDrift、Flooded_Grounds、MegaKit…）同处一个程序集。任何一行改动都会触发全量重编译，
且代码里没有任何机制阻止"电量系统去调用 UI"或"交互层去依赖门"。

我们决定按子系统拆分目录与程序集：`Core` / `Player` / `Interaction` / `World` / `Content`，`Editor` 单独一个
程序集并显式引用运行时程序集。依赖方向单向：`World` 与 `Player` 可以依赖 `Core` 与 `Interaction`，反向不允许。

## Consequences

搬迁脚本必须走 `AssetDatabase.MoveAsset` 以保留 `.meta` 里的 GUID，否则场景里所有组件引用会变成 `Missing`。
`Assets/Editor/` 下的工具引用了运行时类型（如 `AnywhereDoor`），拆分层后必须补上程序集引用，否则 Editor 程序集编译失败。
