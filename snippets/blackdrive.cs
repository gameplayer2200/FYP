var mbList = UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true);
if (mbList.Length == 0) return "no blackout";
var mb = mbList[0];
var t = typeof(MetroBlackout);
var fl = t.GetField("timer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var upd = t.GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var so = new UnityEditor.SerializedObject(mb);
float interval = so.FindProperty("segmentInterval").floatValue;
var sb = new System.Text.StringBuilder();
for (int i = 0; i < 5; i++)
{
    fl.SetValue(mb, interval + 0.1f); // 推过阈值
    upd.Invoke(mb, null);             // 手动步进：熄一组 + 重算耗电
}
foreach (var lg in UnityEngine.Object.FindObjectsOfType<LightGroup>(true))
    sb.AppendLine(lg.name + " on=" + lg.IsOn);
var bs = UnityEngine.Object.FindObjectsOfType<BatterySystem>(true);
var drain = typeof(BatterySystem).GetField("ambientDrain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
sb.AppendLine("ambientDrain=" + drain.GetValue(bs[0]) + " battery=" + bs[0].Battery.ToString("F1"));
return sb.ToString();
