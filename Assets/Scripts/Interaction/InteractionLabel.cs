using UnityEngine;

/// <summary>
/// 交互提示文案：挂在交互物上，交互中枢的 [E] 提示改用它；
/// 不挂则统一显示"交互"。给新交互物加提示不用改交互中枢。
/// </summary>
public class InteractionLabel : MonoBehaviour
{
    [SerializeField] private string text = "交互";

    public string Text { get { return text; } }
}
