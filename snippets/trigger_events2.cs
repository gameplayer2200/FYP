var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
// 直接启动风暴协程（绕过倒计时门）
var storms = UnityEngine.Object.FindObjectsOfType<EmfStormAnomaly>(true);
foreach (var s in storms)
{
    var m = typeof(EmfStormAnomaly).GetMethod("StormRoutine", bf);
    var routine = (System.Collections.IEnumerator)m.Invoke(s, null);
    typeof(MonoBehaviour).GetMethod("StartCoroutine", new[] { typeof(System.Collections.IEnumerator) })
        .Invoke(s, new object[] { routine });
}
sb.AppendLine("storming=" + EmfStorm.IsStorming);
// 直接启动进站协程
var ghosts = UnityEngine.Object.FindObjectsOfType<GhostArrivalEvent>(true);
foreach (var g in ghosts)
{
    var m = typeof(GhostArrivalEvent).GetMethod("ArrivalRoutine", bf);
    var routine = (System.Collections.IEnumerator)m.Invoke(g, null);
    typeof(MonoBehaviour).GetMethod("StartCoroutine", new[] { typeof(System.Collections.IEnumerator) })
        .Invoke(g, new object[] { routine });
}
var running = typeof(GhostArrivalEvent).GetField("running", bf);
sb.AppendLine("ghostRunning=" + (ghosts.Length > 0 && (bool)running.GetValue(ghosts[0])));
var roarT = ghosts[0].transform.Find("风压声");
var roar = roarT != null ? roarT.GetComponent<AudioSource>() : null;
sb.AppendLine("roarPlaying=" + (roar != null && roar.isPlaying) + " clip=" + (roar != null && roar.clip != null ? roar.clip.name : "null"));
int off = 0, on = 0;
foreach (var lg in UnityEngine.Object.FindObjectsOfType<LightGroup>(true)) { if (!lg.IsOn) off++; else on++; }
sb.AppendLine("groups on=" + on + " off=" + off + "（事件扰动中应出现 on>0——死灯闪亮）");
return sb.ToString();
