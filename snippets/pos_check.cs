var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var rf = typeof(PortalWindow).GetField("ready", bf);
var a = UnityEngine.GameObject.Find("anywhere-door-Metro-Lab");
var b = UnityEngine.GameObject.Find("anywhere-door-Metro");
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var root = (UnityEngine.Transform)pr.GetProperty("Root").GetValue(null);
return "player=" + root.position.ToString("F2")
    + " labReady=" + (bool)rf.GetValue(a.GetComponentInChildren<PortalWindow>(true))
    + " metroReady=" + (b != null && (bool)rf.GetValue(b.GetComponentInChildren<PortalWindow>(true)));
