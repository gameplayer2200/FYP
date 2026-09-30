using System.Collections.Generic;

/// <summary>
/// 可交互物自注册登记表。
/// 交互中枢直接遍历登记表选目标，不依赖物理扫描——
/// 密集几何场景里 6m OverlapSphere 会被墙体/地板等静态碰撞体填满（实测 32 深的缓冲区都挤爆），
/// 交互物的触发体反而进不了结果集。交互物全项目就几个，登记表 O(N) 远比物理扫描可靠。
/// </summary>
public static class InteractionRegistry
{
    private static readonly List<IInteractable> items = new List<IInteractable>();

    public static void Register(IInteractable item)
    {
        if (item != null && !items.Contains(item)) items.Add(item);
    }

    public static void Unregister(IInteractable item)
    {
        items.Remove(item);
    }

    public static List<IInteractable> Items { get { return items; } }
}
