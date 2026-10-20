var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var all = UnityEngine.Object.FindObjectsOfType<DutyBriefing>(true);
foreach (var b in all) typeof(DutyBriefing).GetMethod("Start", bf).Invoke(b, null);
WorldPresence.SetCurrent("FactoryDay"); // 换个没播过的世界触发
System.Threading.Thread.Sleep(50);
foreach (var b in all)
{
    var f = typeof(DutyBriefing).GetField("lines", bf);
    var arr = (string[])f.GetValue(b);
    sb.AppendLine("实例 lines=" + (arr != null ? arr.Length : 0));
    if (arr != null && arr.Length > 0) { foreach (var l in arr) sb.AppendLine("  | " + l); break; }
}
return sb.ToString();
