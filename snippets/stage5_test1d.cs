// ③补测：驱动 MetroBlackout.Update 刷新 PlayerInZone，再驱动计时器解锁
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
sb.AppendLine("玩家现在 " + player.position.ToString("F1"));
var mb = UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true);
typeof(MetroBlackout).GetMethod("Update", bf).Invoke(mb[0], null);
sb.AppendLine("PlayerInZone=" + mb[0].PlayerInZone + "（应 False）");
var timers = UnityEngine.Object.FindObjectsOfType<ExtractionTimer>(true);
typeof(ExtractionTimer).GetMethod("Update", bf).Invoke(timers[0], null);
var door = UnityEngine.GameObject.Find("anywhere-door-Metro").GetComponentInChildren<AnywhereDoor>(true);
var rtk = typeof(AnywhereDoor).GetField("respondToKey", bf);
sb.AppendLine("armed=" + ExtractionClock.Armed + " respondToKey=" + rtk.GetValue(door) + "（应 False/True=解锁）");
return sb.ToString();
