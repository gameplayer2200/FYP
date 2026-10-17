var sb = new System.Text.StringBuilder();
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
// ① 拾取两块软盘
foreach (var sp in UnityEngine.Object.FindObjectsOfType<SpecimenPickup>(true))
    if (sp.gameObject.activeSelf) sp.Interact(player.gameObject);
sb.AppendLine("拾取后 carrying=" + Inventory.Instance.SpecimenCount);
// ② 订单板内部进度行
var board = UnityEngine.Object.FindObjectsOfType<OrderBoard>(true);
if (board.Length > 0)
{
    var pl = typeof(OrderBoard).GetMethod("ProgressLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var hl = typeof(OrderBoard).GetMethod("HintLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    sb.AppendLine("进度: " + pl.Invoke(board[0], null));
    sb.AppendLine("提示: " + hl.Invoke(board[0], null));
}
// ③ 收容入账
foreach (var dep in UnityEngine.Object.FindObjectsOfType<SpecimenDeposit>(true))
    dep.Interact(player.gameObject);
sb.AppendLine("收容后 stored=" + Inventory.Instance.StoredSpecimenCount + " carrying=" + Inventory.Instance.SpecimenCount);
if (board.Length > 0)
{
    var hl = typeof(OrderBoard).GetMethod("HintLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var pl = typeof(OrderBoard).GetMethod("ProgressLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    sb.AppendLine("进度: " + pl.Invoke(board[0], null));
    sb.AppendLine("提示: " + hl.Invoke(board[0], null));
}
return sb.ToString();
