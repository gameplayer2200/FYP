WorldPresence.SetCurrent("Metro");
var sb = new System.Text.StringBuilder();
foreach (var b in UnityEngine.Object.FindObjectsOfType<DutyBriefing>(true))
{
    var f = typeof(DutyBriefing).GetField("lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var sa = typeof(DutyBriefing).GetField("shownAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var arr = (string[])f.GetValue(b);
    sb.AppendLine("播报触发 lines=" + (arr != null ? arr.Length : 0) + " shownAt=" + ((float)sa.GetValue(b)).ToString("F1") + " now=" + UnityEngine.Time.unscaledTime.ToString("F1"));
    if (arr != null) foreach (var l in arr) sb.AppendLine("  | " + l);
    break;
}
return sb.ToString();
