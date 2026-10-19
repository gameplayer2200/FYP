using UnityEngine;

/// <summary>
/// 交互提示文案：挂在交互物上，交互中枢的 [E] 提示改用它；
/// 不挂则统一显示"交互"。给新交互物加提示不用改交互中枢。
/// Text 可运行时改——交互物根据自身状态切换提示（如收容台"打开/收容"两段）。
/// </summary>
public class InteractionLabel : MonoBehaviour
{
    [SerializeField] private string text = "交互";

    public string Text { get { return text; } set { text = value; } }
}
