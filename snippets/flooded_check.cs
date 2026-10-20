var sb = new System.Text.StringBuilder();
var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
sb.Append("active=" + s.name);
sb.Append(" 根对象=" + s.GetRootGameObjects().Length);
int lights = 0;
foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>(true)) lights++;
sb.Append(" 灯=" + lights);
var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true);
sb.Append(" 地形=" + terrains.Length);
int demoLeft = 0;
foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (mb == null) continue;
    var tn = mb.GetType().Name;
    if (tn == "FPSDisplay" || tn == "CharController_Motor" || tn == "FpsController") demoLeft++;
}
sb.Append(" Demo残留=" + demoLeft + "（应0）");
foreach (var r in s.GetRootGameObjects()) sb.Append(" | " + r.name);
return sb.ToString();
