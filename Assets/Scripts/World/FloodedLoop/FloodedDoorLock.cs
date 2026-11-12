using System.Reflection;
using UnityEngine;

/// <summary>
/// 把某扇门（AnywhereDoor）锁死，并给它一句自定义提示。
///
/// 为什么用反射：`AnywhereDoor.locked` / `lockedHint` 是 `[SerializeField] private`，
/// 没有公开 setter，而本轮约定「不改共用脚本」——反射只读不写文件，不会造成合并冲突。
/// 万一将来字段被改名，这里会退化成「安静的上锁」（SetRespondToKey(false)）并打一条警告，不会崩。
/// </summary>
[DisallowMultipleComponent]
public class FloodedDoorLock : MonoBehaviour
{
    [Tooltip("要锁的门（拖场景里的门板对象，脚本会自动找同物体/父物体上的 AnywhereDoor）")]
    [SerializeField] private AnywhereDoor door;

    [Tooltip("锁死时按 E 的提示文案")]
    [SerializeField] private string lockedHint = "门在身后锁死了——得先找到钥匙";

    [Tooltip("进入场景（启用本组件）时自动上锁")]
    [SerializeField] private bool lockOnStart = true;

    private static FieldInfo s_lockedField;
    private static FieldInfo s_lockedHintField;
    private static bool s_reflectionReady;
    private static bool s_reflectionOk;

    public bool IsLocked { get; private set; }

    private void Awake()
    {
        if (door == null) door = GetComponentInParent<AnywhereDoor>();
        if (door == null) door = GetComponentInChildren<AnywhereDoor>();
        PrepareReflection();
    }

    private void OnEnable()
    {
        if (lockOnStart) SetLocked(true);
    }

    /// <summary>上锁 / 解锁。上锁后按 E 只弹提示，门打不开。</summary>
    public void SetLocked(bool value)
    {
        IsLocked = value;
        if (door == null)
        {
            Debug.LogWarning("[FloodedDoorLock] 没有指定 AnywhereDoor，锁门无效");
            return;
        }

        if (s_reflectionOk)
        {
            s_lockedField.SetValue(door, value);
            if (value && s_lockedHintField != null) s_lockedHintField.SetValue(door, lockedHint);
        }
        else
        {
            // 退化路径：不能让玩家白按 E 却没反应，至少把交互关掉
            door.SetRespondToKey(!value);
        }
    }

    public void Unlock()
    {
        SetLocked(false);
        // 解锁后把门恢复成可交互（退化路径下必须显式打开）
        door.SetRespondToKey(true);
    }

    private static void PrepareReflection()
    {
        if (s_reflectionReady) return;
        s_reflectionReady = true;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        s_lockedField = typeof(AnywhereDoor).GetField("locked", flags);
        s_lockedHintField = typeof(AnywhereDoor).GetField("lockedHint", flags);
        s_reflectionOk = s_lockedField != null && s_lockedField.FieldType == typeof(bool);

        if (!s_reflectionOk)
        {
            Debug.LogWarning("[FloodedDoorLock] AnywhereDoor.locked 字段找不到或类型变了，改用 SetRespondToKey 兜底");
        }
    }
}
