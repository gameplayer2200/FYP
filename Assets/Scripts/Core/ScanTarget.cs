using UnityEngine;

/// <summary>
/// 扫描目标标记：挂在「只有电磁探测器扫到才会显形」的物体上（异常本体 / 异常物）。
///
/// 放在 Core 而不是 World：World（异常）与 Content（异常物）都会挂它，两者都已依赖 Core，
/// 而显形逻辑在 Player 层的 EmfDetector——Core 不能反向依赖它们，所以这里只放标记。
///
/// 显形由 EmfDetector 统一负责：把这些物体下的 Renderer 换成 FYP/ScanMatrix 材质。
/// 那个着色器是「显现」型——波没扫到就完全透明，所以挂了本组件的物体平时是看不见的，
/// 必须带着探测器去扫。标记本身不带参数：强度/颜色都由扫描波统一给。
/// </summary>
[DisallowMultipleComponent]
public class ScanTarget : MonoBehaviour
{
}
