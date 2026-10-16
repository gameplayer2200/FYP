// 把每个灯组 GO 摆到其灯泡的质心（运行时验证用；场景侧后续用 MetroSceneDev 落盘）
var sb = new System.Text.StringBuilder();
foreach (var lg in UnityEngine.Object.FindObjectsOfType<LightGroup>(true))
{
    var lightsField = typeof(LightGroup).GetField("lights",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var lights = (Light[])lightsField.GetValue(lg);
    if (lights == null || lights.Length == 0) { sb.AppendLine(lg.name + " no lights"); continue; }
    Vector3 c = Vector3.zero; int n = 0;
    foreach (var l in lights) { if (l == null) continue; c += l.transform.position; n++; }
    if (n > 0) { c /= n; lg.transform.position = c; }
    sb.AppendLine(lg.name + " -> " + lg.transform.position.ToString("F1") + " (" + n + "灯)");
}
return sb.ToString();
