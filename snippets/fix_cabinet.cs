var cab = UnityEngine.Object.FindObjectsOfType<PowerCabinet>(true);
if (cab.Length == 0) return "no cabinet";
var mb = UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true);
var fl = typeof(PowerCabinet).GetField("blackout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
fl.SetValue(cab[0], mb.Length > 0 ? mb[0] : null);
// 重新合闸
var breaker = UnityEngine.Object.FindObjectsOfType<BreakerSwitch>(true);
var root = UnityEngine.GameObject.Find("PlayerRoot");
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
breaker[0].Interact(player != null ? player.gameObject : null);
var sb = new System.Text.StringBuilder();
sb.AppendLine("restored=" + MetroBlackout.PowerRestored);
var carDoor = UnityEngine.Object.FindObjectsOfType<PowerLockedDoor>(true);
sb.AppendLine("carriageDoor enabled=" + (carDoor.Length > 0 && carDoor[0].enabled) + " (False=已开)");
var rs = UnityEngine.Object.FindObjectsOfType<RequiresPower>(true);
sb.AppendLine("chargeGate enabled=" + (rs.Length > 0 && rs[0].enabled) + " (False=已供电让位)");
var pl = UnityEngine.Object.FindObjectsOfType<PoweredLight>(true);
foreach (var p in pl)
{
    var lamp = p.GetComponentsInChildren<Light>(true);
    sb.AppendLine("poweredLight on=" + (lamp.Length > 0 && lamp[0].enabled));
}
foreach (var lg in UnityEngine.Object.FindObjectsOfType<LightGroup>(true))
    sb.AppendLine(lg.name + " on=" + lg.IsOn);
return sb.ToString();
