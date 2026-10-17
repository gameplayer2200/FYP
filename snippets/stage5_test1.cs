// 第三轮验证 ①：撤离窗口（武装→锁门→离场解锁）
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
var timers = UnityEngine.Object.FindObjectsOfType<ExtractionTimer>(true);
if (timers.Length == 0) return "ExtractionTimer NOT FOUND";
var updT = typeof(ExtractionTimer).GetMethod("Update", bf);
updT.Invoke(timers[0], null); // 武装
sb.AppendLine("①armed=" + ExtractionClock.Armed + " remaining=" + ExtractionClock.Remaining.ToString("F0") + "（应600）");
// 快进到归零
var armM = typeof(ExtractionClock).GetMethod("Arm",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
ExtractionClock.Reset(); updT.Invoke(timers[0], null);            // 重新武装（600）
var remProp = typeof(ExtractionClock).GetProperty("Remaining",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
remProp.SetValue(null, 0.01f, null);                                // 剩 0.01s
ExtractionClock.Tick(0.05f);                                        // 归零 → OnLocked
var doorGo = UnityEngine.GameObject.Find("anywhere-door-Metro");
var door = doorGo.GetComponentInChildren<AnywhereDoor>(true);
var rtk = typeof(AnywhereDoor).GetField("respondToKey", bf);
sb.AppendLine("②locked后 remaining=" + ExtractionClock.Remaining + " doorOpen=" + door.IsOpen + " respondToKey=" + rtk.GetValue(door) + "（应 0/False/False）");
// 离场解锁
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(5f, 1f, 3f), UnityEngine.Quaternion.identity });
updT.Invoke(timers[0], null);
sb.AppendLine("③离场 armed=" + ExtractionClock.Armed + " respondToKey=" + rtk.GetValue(door) + "（应 False/True）");
return sb.ToString();
