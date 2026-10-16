using UnityEngine;

/// <summary>
/// 玩家引用注册表。与 InteractionRegistry 同构的自注册模式：玩家自己登记自己，
/// 消费方按需取用，取代全项目散落的 GameObject.Find("player")。
///
/// 消费方必须容忍空值：世界是 Additive 加载的，玩家被销毁重建的那一帧这里就是空的。
/// 判空一律用 IsReady，不要缓存 Root 的引用。
/// </summary>
public static class PlayerRef
{
    public static Transform Root { get; private set; }
    public static Camera Camera { get; private set; }
    public static CharacterController Controller { get; private set; }

    public static bool IsReady { get { return Root != null; } }

    /// <summary>由 PlayerMovement 在 Awake 中调用。</summary>
    public static void Register(Transform root, Camera camera, CharacterController controller)
    {
        Root = root;
        Camera = camera;
        Controller = controller;
        Debug.Log("[PlayerRef] 已登记 " + root.name + " camera=" + (camera != null) + " controller=" + (controller != null));
    }

    /// <summary>由 PlayerMovement 在 OnDestroy 中调用；只有当前登记者能注销自己。</summary>
    public static void Unregister(Transform root)
    {
        if (Root != root) return;
        Root = null;
        Camera = null;
        Controller = null;
        Debug.Log("[PlayerRef] 已注销 " + root.name);
    }

    /// <summary>
    /// 安全瞬移。CharacterController 会覆盖直接写入的 position，必须先禁用再设置再启用。
    /// 刻意不动速度——穿门要保留动量，这是手感的一部分（要清零速度请用 PlayerMovement.TeleportTo）。
    /// </summary>
    public static void Teleport(Vector3 position, Quaternion rotation)
    {
        if (Root == null) return;
        bool hadController = Controller != null && Controller.enabled;
        if (hadController) Controller.enabled = false;
        Root.SetPositionAndRotation(position, rotation);
        if (hadController) Controller.enabled = true;
    }
}
