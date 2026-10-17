var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
var timers = UnityEngine.Object.FindObjectsOfType<ExtractionTimer>(true);
sb.AppendLine("timers=" + timers.Length + " player=" + (player != null));
var updT = typeof(ExtractionTimer).GetMethod("Update", bf);
sb.AppendLine("updT=" + (updT != null));
try { updT.Invoke(timers[0], null); sb.AppendLine("armed=" + ExtractionClock.Armed + " rem=" + ExtractionClock.Remaining); }
catch (System.Exception e) { sb.AppendLine("arm FAIL: " + e.Message); return sb.ToString(); }
var doorGo = UnityEngine.GameObject.Find("anywhere-door-Metro");
sb.AppendLine("doorGo=" + (doorGo != null));
var door = doorGo.GetComponentInChildren<AnywhereDoor>(true);
var rtk = typeof(AnywhereDoor).GetField("respondToKey", bf);
sb.AppendLine("door=" + (door != null) + " rtk=" + (rtk != null));
var remProp = typeof(ExtractionClock).GetProperty("Remaining");
var setter = remProp.GetSetMethod(true);
sb.AppendLine("remSetter=" + (setter != null) + " static=" + (setter != null && setter.IsStatic));
setter.Invoke(null, new object[] { 0.01f });
ExtractionClock.Tick(0.05f);
sb.AppendLine("after tick rem=" + ExtractionClock.Remaining + " open=" + door.IsOpen + " rtk=" + rtk.GetValue(door));
return sb.ToString();
