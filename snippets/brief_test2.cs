var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
foreach (var b in UnityEngine.Object.FindObjectsOfType<DutyBriefing>(true))
{
    typeof(DutyBriefing).GetMethod("Start", bf).Invoke(b, null); // 后台无帧补跑订阅
    break;
}
WorldPresence.SetCurrent("SpecimenLab"); // 先回home再进Metro（若已played去重）
WorldPresence.SetCurrent("Metro");
foreach (var b in UnityEngine.Object.FindObjectsOfType<DutyBriefing>(true))
{
    var f = typeof(DutyBriefing).GetField("lines", bf);
    var sa = typeof(DutyBriefing).GetField("shownAt", bf);
    var arr = (string[])f.GetValue(b);
    sb.AppendLine("lines=" + (arr != null ? arr.Length : 0) + " shown=" + (((float)sa.GetValue(b)) > 0f));
    if (arr != null) foreach (var l in arr) sb.AppendLine("  | " + l);
    break;
}
// 重触发账单（反射 Show）供截图
foreach (var p in UnityEngine.Object.FindObjectsOfType<BillPanel>(true))
{
    typeof(BillPanel).GetMethod("Show", bf).Invoke(p, new object[] { 3 });
    break;
}
return sb.ToString();
