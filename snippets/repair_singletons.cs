var sb = new System.Text.StringBuilder();
// 1) PlayerRef（玩家三件套）
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
UnityEngine.GameObject playerRoot = null;
foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (mb != null && mb.GetType().Name == "PlayerMovement") { playerRoot = mb.gameObject; break; }
}
if (playerRoot != null)
{
    var cam = playerRoot.GetComponentInChildren<UnityEngine.Camera>(true);
    var cc = playerRoot.GetComponentInChildren<UnityEngine.CharacterController>(true);
    pr.GetMethod("Register").Invoke(null, new object[] { playerRoot.transform, cam, cc });
    sb.AppendLine("PlayerRef ok");
}
// 2) BatterySystem.Instance
var bs = UnityEngine.Object.FindObjectsOfType<BatterySystem>(true);
if (bs.Length > 0)
{
    typeof(BatterySystem).GetProperty("Instance",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .SetValue(null, bs[0], null);
    sb.AppendLine("Battery.Instance ok");
}
// 3) Inventory.Instance
var inv = UnityEngine.Object.FindObjectsOfType<Inventory>(true);
if (inv.Length > 0)
{
    typeof(Inventory).GetProperty("Instance",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .SetValue(null, inv[0], null);
    sb.AppendLine("Inventory.Instance ok");
}
return sb.ToString();
