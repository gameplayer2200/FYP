var sb = new System.Text.StringBuilder();
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    sb.Append(s.name).Append(" ");
}
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
string pos = "n/a"; bool isReady = false;
if (pr != null)
{
    isReady = (bool)pr.GetProperty("IsReady").GetValue(null);
    var root = (UnityEngine.Transform)pr.GetProperty("Root").GetValue(null);
    if (root != null) pos = root.position.ToString("F2");
}
sb.Append("| playerReady=").Append(isReady).Append(" pos=").Append(pos);
sb.Append("| ").Append(WorldStreamer.StatusText());
return sb.ToString();
