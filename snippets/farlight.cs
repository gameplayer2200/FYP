// 对面的灯：两个站位手动驱动 Update，验证近灭远亮
var sb = new System.Text.StringBuilder();
var anom = UnityEngine.Object.FindObjectsOfType<UnreachableLightAnomaly>(true);
if (anom.Length == 0) return "anomaly NOT FOUND";
var lamp = anom[0].GetComponentInChildren<Light>(true);
var upd = typeof(UnreachableLightAnomaly).GetMethod("Update",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
// 站到 8m 内（offDistance=14 → 应灭）
pr.GetMethod("Teleport").Invoke(null, new object[]
{ new UnityEngine.Vector3(-52f, 1f, 42f), UnityEngine.Quaternion.identity });
upd.Invoke(anom[0], null);
sb.AppendLine("near8m lampOn=" + lamp.enabled + " (应False)");
// 退到 25m 外（onDistance=22 → 应亮）
pr.GetMethod("Teleport").Invoke(null, new object[]
{ new UnityEngine.Vector3(-44f, 1f, 120f), UnityEngine.Quaternion.identity });
upd.Invoke(anom[0], null);
sb.AppendLine("far25m lampOn=" + lamp.enabled + " (应True)");
return sb.ToString();
