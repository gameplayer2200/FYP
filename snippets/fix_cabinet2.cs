var cab = UnityEngine.Object.FindObjectsOfType<PowerCabinet>(true);
var fl = typeof(PowerCabinet).GetField("blackout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var mb = UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true);
fl.SetValue(cab[0], mb.Length > 0 ? mb[0] : null);

var breaker = UnityEngine.Object.FindObjectsOfType<BreakerSwitch>(true);
var closedField = typeof(BreakerSwitch).GetField("closed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
closedField.SetValue(breaker[0], false);
breaker[0].enabled = true; // SetClosed(true) 时会 enabled=false 让位

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
sb.AppendLine("carriageDoor enabled=" + (carDoor.Length > 0 && carDoor[0].enabled) + " (False=已开锁让位)");
var rs = UnityEngine.Object.FindObjectsOfType<RequiresPower>(true);
sb.AppendLine("chargeGate enabled=" + (rs.Length > 0 && rs[0].enabled) + " (False=已供电让位)");
var station = breaker[0].GetComponent<ChargeStation>();
var pl = UnityEngine.Object.FindObjectsOfType<PoweredLight>(true);
foreach (var p in pl)
{
    var lamp = p.GetComponentsInChildren<Light>(true);
    sb.AppendLine("carriageLight on=" + (lamp.Length > 0 && lamp[0].enabled));
}
foreach (var lg in UnityEngine.Object.FindObjectsOfType<LightGroup>(true))
    sb.AppendLine(lg.name + " on=" + lg.IsOn);
var bs = UnityEngine.Object.FindObjectsOfType<BatterySystem>(true);
var drain = typeof(BatterySystem).GetField("ambientDrain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
sb.AppendLine("ambientDrain=" + drain.GetValue(bs[0]) + " (应0)");
return sb.ToString();
