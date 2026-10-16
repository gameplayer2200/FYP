using UnityEngine;

/// <summary>
/// 门配对：一条"本门 → 通往哪个世界 → 对面是哪扇门"的配置。
/// 新增一扇门 = 新建一条门配对资产，不改代码。
///
/// 为什么对面那扇门只能按名字指认：门配对天然跨场景（编辑时两个世界不会同时加载），
/// 而 Unity 无法序列化指向未加载场景中对象的引用。所以它是**本资产的字段**——
/// 配置数据，而不是代码里的魔法字符串。判据见 docs/adr/0002-cross-scene-refs-are-data.md。
/// </summary>
[CreateAssetMenu(fileName = "WorldLink", menuName = "门阈行动/门配对")]
public class WorldLink : ScriptableObject
{
    [Header("目标世界")]
    [Tooltip("对面的世界（Unity 场景名，须在 Build Settings 中启用）")]
    [SerializeField] private string worldSceneName = "Metro";
    [Tooltip("对面世界里那扇门的对象名（包含匹配，可匹配 anywhere-door-Metro 等变体）")]
    [SerializeField] private string worldDoorName = "anywhere-door-Metro";

    [Header("门几何")]
    [Tooltip("门洞平面相对门根的路径（门资产的属性，两侧一致）")]
    [SerializeField] private string openingPath = "Cube.001/Plane";

    [Header("取景")]
    [Tooltip("取景 RT 高度，宽度按门洞宽高比推导")]
    [SerializeField] private int rtHeight = 720;
    [SerializeField] private float farClip = 200f;
    [SerializeField] private float nearClip = 0.05f;
    [Tooltip("玩家离本门多近才开始加载并渲染对面世界（米）")]
    [SerializeField] private float preloadRadius = 15f;

    [Header("约定")]
    [Tooltip("两扇门之间的朝向差。\n" +
             "false = 朝向一致 → 门→门是纯平移（local 坐标原样搬过去）。\n" +
             "true  = 朝向相差 180° → 绕 Y 转 180°。\n" +
             "注意：门→门映射必须是刚体变换，不能用镜像——镜像会让相机落到对面门背面、\n" +
             "门洞里看到的不是那扇门的正面。")]
    [SerializeField] private bool mapYaw180 = false;
    [Tooltip("取景画面左右镜像时勾上")]
    [SerializeField] private bool mirrorU = false;

    public string WorldSceneName { get { return worldSceneName; } }
    public string WorldDoorName { get { return worldDoorName; } }
    public string OpeningPath { get { return openingPath; } }
    public int RtHeight { get { return rtHeight; } }
    public float FarClip { get { return farClip; } }
    public float NearClip { get { return nearClip; } }
    public float PreloadRadius { get { return preloadRadius; } }
    public bool MapYaw180 { get { return mapYaw180; } }
    public bool MirrorU { get { return mirrorU; } }
}
