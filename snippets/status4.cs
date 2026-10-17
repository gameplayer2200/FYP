var sb = new System.Text.StringBuilder();
sb.Append("isPlaying=" + UnityEngine.Application.isPlaying);
sb.Append(" scenes=" + UnityEngine.SceneManagement.SceneManager.sceneCount);
sb.Append(" active=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
int pm = 0, activePm = 0, runners = 0;
foreach (var m in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (m == null) continue;
    var tn = m.GetType().Name;
    if (tn == "PlayerMovement") { pm++; if (m.gameObject.activeInHierarchy) activePm++; }
    if (tn == "InteractionRunner" && m.gameObject.activeInHierarchy) runners++;
}
sb.Append(" players=" + pm + " active=" + activePm + " runners=" + runners);
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var root = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
sb.Append(" playerRef=" + (root != null ? root.position.ToString("F1") : "null"));
var mb = UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true);
if (mb.Length > 0) sb.Append(" | inZone=" + mb[0].PlayerInZone + " killed=" + mb[0].KilledGroups);
return sb.ToString();
