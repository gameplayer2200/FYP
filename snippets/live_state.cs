var sb = new System.Text.StringBuilder();
sb.Append("scenes=" + UnityEngine.SceneManagement.SceneManager.sceneCount);
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    sb.Append(" [" + s.name + (s.isLoaded ? "" : "(卸载中)") + "]");
}
sb.Append(" active=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
int pm = 0, act = 0; UnityEngine.GameObject sample = null;
foreach (var m in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (m != null && m.GetType().Name == "PlayerMovement") { pm++; if (m.gameObject.activeInHierarchy) { act++; sample = m.gameObject; } }
}
sb.Append(" players=" + pm + " active=" + act);
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
sb.Append(" root=" + (pr.GetProperty("Root").GetValue(null) != null));
if (sample != null) sb.Append(" | 玩家自报 activeSelf=" + sample.activeSelf + " activeInHierarchy=" + sample.activeInHierarchy + " pos=" + sample.transform.position.ToString("F1"));
return sb.ToString();
