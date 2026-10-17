var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
// 触发电磁风暴
var storms = UnityEngine.Object.FindObjectsOfType<EmfStormAnomaly>(true);
foreach (var s in storms)
{
    var f = typeof(EmfStormAnomaly).GetField("nextIn", bf);
    f.SetValue(s, -0.01f);
    var u = typeof(EmfStormAnomaly).GetMethod("Update", bf);
    u.Invoke(s, null);
}
sb.AppendLine("storming=" + EmfStorm.IsStorming + " ( storms=" + storms.Length + " )");
// 触发幽灵进站
var ghosts = UnityEngine.Object.FindObjectsOfType<GhostArrivalEvent>(true);
foreach (var g in ghosts)
{
    var f = typeof(GhostArrivalEvent).GetField("nextIn", bf);
    f.SetValue(g, -0.01f);
    var u = typeof(GhostArrivalEvent).GetMethod("Update", bf);
    u.Invoke(g, null);
}
var running = typeof(GhostArrivalEvent).GetField("running", bf);
sb.AppendLine("ghostRunning=" + (ghosts.Length > 0 && (bool)running.GetValue(ghosts[0])));
var roarT = ghosts[0].transform.Find("风压声");
var roar = roarT != null ? roarT.GetComponent<AudioSource>() : null;
sb.AppendLine("roarPlaying=" + (roar != null && roar.isPlaying) + " clip=" + (roar != null && roar.clip != null ? roar.clip.name : "null"));
// 灯组状态应已被事件扰动
int off = 0;
foreach (var lg in UnityEngine.Object.FindObjectsOfType<LightGroup>(true)) if (!lg.IsOn) off++;
sb.AppendLine("groupsOff=" + off + "/5");
return sb.ToString();
