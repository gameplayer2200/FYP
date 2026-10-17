// 撤离窗口完整验证（修正版：私有 setter 用 GetSetMethod(true)）
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
var timers = UnityEngine.Object.FindObjectsOfType<ExtractionTimer>(true);
var updT = typeof(ExtractionTimer).GetMethod("Update", bf);
updT.Invoke(timers[0], null);
sb.AppendLine("①armed=" + ExtractionClock.Armed + " remaining=" + ExtractionClock.Remaining.ToString("F0") + "（应 True/600）");
var doorGo = UnityEngine.GameObject.Find("anywhere-door-Metro");
var door = doorGo.GetComponentInChildren<AnywhereDoor>(true);
var rtk = typeof(AnywhereDoor).GetField("respondToKey", bf);
var setter = typeof(ExtractionClock).GetProperty("Remaining").GetSetMethod(true);
setter.Invoke(null, new object[] { 0.01f });
ExtractionClock.Tick(0.05f);
sb.AppendLine("②锁门 remaining=" + ExtractionClock.Remaining + " doorOpen=" + door.IsOpen + " respondToKey=" + rtk.GetValue(door) + "（应 0/False/False）");
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(5f, 1f, 3f), UnityEngine.Quaternion.identity });
updT.Invoke(timers[0], null);
sb.AppendLine("③离场 armed=" + ExtractionClock.Armed + " respondToKey=" + rtk.GetValue(door) + "（应 False/True=解锁）");
return sb.ToString();
