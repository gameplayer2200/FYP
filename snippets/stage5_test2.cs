// 第三轮验证 ②：手电筒（拾取→开关→耗电通道→黑暗联动）
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
// 回 Metro 门区
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(-47f, 94.2f, 136f), UnityEngine.Quaternion.identity });
var pick = UnityEngine.GameObject.Find("道具_手电筒_Metro");
if (pick == null) return "手电筒拾取物 NOT FOUND";
pick.GetComponent<FlashlightPickup>().Interact(player.gameObject);
sb.AppendLine("①hasFlashlight=" + PlayerGear.HasFlashlight + " 组件=" + (player.GetComponentInChildren<Flashlight>(true) != null));
var fl = player.GetComponentInChildren<Flashlight>(true);
// 驱动一帧 Update（建灯锥）
typeof(Flashlight).GetMethod("Update", bf).Invoke(fl, null);
var spot = fl.GetComponentInChildren<Light>(true);
sb.AppendLine("②灯锥=" + (spot != null) + " enabled=" + (spot != null && spot.enabled) + "（拾取后应 False）");
fl.Toggle();
var drainF = typeof(BatterySystem).GetField("flashlightDrain", bf);
var bs = UnityEngine.Object.FindObjectsOfType<BatterySystem>(true);
sb.AppendLine("③开灯 FlashlightOn=" + PlayerGear.FlashlightOn + " 灯锥亮=" + (spot != null && spot.enabled) + " 手电耗电=" + drainF.GetValue(bs[0]) + "（应 True/True/0.5）");
// 黑暗联动：熄灭全部组 + 玩家站死组旁
var mb = UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true);
var updMb = typeof(MetroBlackout).GetMethod("Update", bf);
var timerF = typeof(MetroBlackout).GetField("timer", bf);
var so = new UnityEditor.SerializedObject(mb[0]);
float interval = so.FindProperty("segmentInterval").floatValue;
for (int i = 0; i < 5; i++) { timerF.SetValue(mb[0], interval + 0.1f); updMb.Invoke(mb[0], null); }
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(-52f, 94.5f, 110f), UnityEngine.Quaternion.identity });
var drainA = typeof(BatterySystem).GetField("ambientDrain", bf);
updMb.Invoke(mb[0], null);
sb.AppendLine("④站死组+开灯 ambient=" + drainA.GetValue(bs[0]) + "（应1.0 不吃1.5）");
fl.Toggle();
updMb.Invoke(mb[0], null);
sb.AppendLine("⑤关灯 ambient=" + drainA.GetValue(bs[0]) + "（应1.5）");
fl.Toggle(); // 复原关灯
return sb.ToString();
