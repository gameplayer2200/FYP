using UnityEngine;

/// <summary>
/// 异常物随身计数 HUD：屏幕右上角显示 携带/已收容。
/// 挂实验室常驻对象（与 Inventory 同级的 WorldManager）即可，OnGUI 直绘，
/// 与电量条的画法保持一致，不接 UGUI 层级。
/// 外观：CRT 终端条（CrtTheme）——等宽字形、磷光绿、扫描线。
/// 注：OrderBoard 已吸收计数职能；本组件保留给需要独立计数条的场景。
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
        var r = new Rect(Screen.width - 268f, 92f, 248f, 28f);
        CrtTheme.Panel(r);
        GUI.Label(new Rect(r.x + 10f, r.y + 7f, r.width - 20f, 16f),
            EmfStorm.Scramble("> 异常物 " + carrying + " / 已收容 " + stored),
            CrtTheme.Style(12, CrtTheme.Ink, false));
    }
}
