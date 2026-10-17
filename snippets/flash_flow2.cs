var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
var lu = typeof(PortalWindow).GetMethod("LateUpdate", bf);
var labWin = UnityEngine.GameObject.Find("anywhere-door-Metro-Lab").GetComponentInChildren<PortalWindow>(true);
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(3.78f, 1f, 4.1f), UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(0f, 0f, 1f)) });
lu.Invoke(labWin, null);
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(3.78f, 1f, 6f), UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(0f, 0f, 1f)) });
lu.Invoke(labWin, null);
var metroGo = UnityEngine.GameObject.Find("anywhere-door-Metro");
if (metroGo != null) lu.Invoke(metroGo.GetComponentInChildren<PortalWindow>(true), null);
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(-46.5f, 95.6f, 34.5f), UnityEngine.Quaternion.Euler(0f, 180f, 0f) });
return "player=" + player.position.ToString("F2") + " current=" + WorldPresence.Current + " inHome=" + WorldPresence.InHome + " flashOn=" + PlayerGear.FlashlightOn;
