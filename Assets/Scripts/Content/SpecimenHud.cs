using UnityEngine;

/// <summary>
/// 异常物随身计数 HUD：屏幕右上角显示 携带/已收容。
/// 挂实验室常驻对象（与 Inventory 同级的 WorldManager）即可，OnGUI 直绘，
/// 与电量条的画法保持一致，不接 UGUI 层级。
/// </summary>
public class SpecimenHud : MonoBehaviour
{
    private int carrying;
    private int stored;

    private void Start()
    {
        if (Inventory.Instance != null)
        {
            carrying = Inventory.Instance.SpecimenCount;
            stored = Inventory.Instance.StoredSpecimenCount;
        }
        Inventory.OnSpecimenChanged += Refresh;
    }

    private void OnDestroy()
    {
        Inventory.OnSpecimenChanged -= Refresh;
    }

    private void Refresh(int carryingCount, int storedCount)
    {
        carrying = carryingCount;
        stored = storedCount;
    }

    private void OnGUI()
    {
        GUI.color = new Color(0.22f, 0.86f, 0.95f);
        GUI.Label(new Rect(Screen.width - 280f, 66f, 260f, 22f),
            "异常物 " + carrying + "（已收容 " + stored + "）");
        GUI.color = Color.white;
    }
}
