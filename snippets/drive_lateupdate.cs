var lu = typeof(PortalWindow).GetMethod("LateUpdate",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
foreach (var name in new[] { "anywhere-door-Metro-Lab", "anywhere-door-Metro" })
{
    var go = UnityEngine.GameObject.Find(name);
    if (go == null) continue;
    var win = go.GetComponentInChildren<PortalWindow>(true);
    if (win != null) lu.Invoke(win, null);
}
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var root = (UnityEngine.Transform)pr.GetProperty("Root").GetValue(null);
return "after LateUpdate tick, player=" + root.position.ToString("F2");
