var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
// 模拟 WorldStreamer 的每帧写入（后台无帧时测试用；真实游戏自动发生）
WorldPresence.SetHome("SpecimenLab");
WorldPresence.SetCurrent("SpecimenLab");
foreach (var b in UnityEngine.Object.FindObjectsOfType<DutyBriefing>(true))
    typeof(DutyBriefing).GetMethod("Start", bf).Invoke(b, null);
WorldPresence.SetCurrent("Metro"); // 穿门
foreach (var b in UnityEngine.Object.FindObjectsOfType<DutyBriefing>(true))
{
    var arr = (string[])typeof(DutyBriefing).GetField("lines", bf).GetValue(b);
    sb.AppendLine("实例 lines=" + (arr != null ? arr.Length : 0));
    if (arr != null && arr.Length > 0) { foreach (var l in arr) sb.AppendLine("  | " + l); break; }
}
return sb.ToString();
