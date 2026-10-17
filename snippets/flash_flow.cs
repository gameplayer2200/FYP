// 用户流程复现：Lab 出发 → 拿手电 → 穿门 → Metro 死端开灯
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
sb.AppendLine("start: player=" + player.position.ToString("F1") + " 场景=" + player.gameObject.scene.name
    + " | WorldPresence: home=" + WorldPresence.Home + " current=" + WorldPresence.Current + " inHome=" + WorldPresence.InHome);
// ① 拿实验室手电
var pick = UnityEngine.GameObject.Find("道具_手电筒_Lab");
if (pick != null) { pick.GetComponent<FlashlightPickup>().Interact(player.gameObject); sb.AppendLine("①已拿实验室手电"); }
else sb.AppendLine("①Lab手电不存在");
// ② 开门+穿门
var door = UnityEngine.GameObject.Find("anywhere-door-Metro-Lab").GetComponentInChildren<AnywhereDoor>(true);
door.SetOpen(true, false);
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(3.78f, 1f, 4.1f), UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(0f, 0f, 1f)) });
var lu = typeof(PortalWindow).GetMethod("LateUpdate", bf);
lu.Invoke(UnityEngine.GameObject.Find("anywhere-door-Metro-Lab").GetComponentInChildren<PortalWindow>(true), null);
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(3.78f, 1f, 6f), UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(0f, 0f, 1f)) });
lu.Invoke(UnityEngine.GameObject.Find("anywhere-door-Metro").GetComponentInChildren<PortalWindow>(true), null);
sb.AppendLine("②穿门后 player=" + player.position.ToString("F1") + " current=" + WorldPresence.Current + " inHome=" + WorldPresence.InHome);
// ③ 传送到死端暗区并开手电
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(-46.5f, 95.6f, 34f), UnityEngine.Quaternion.Euler(0f, 180f, 0f) });
var fl = player.GetComponentInChildren<Flashlight>(true);
if (fl != null)
{
    typeof(Flashlight).GetMethod("Update", bf).Invoke(fl, null); // 建灯锥
    if (!PlayerGear.FlashlightOn) fl.Toggle();
    sb.AppendLine("③手电 FlashlightOn=" + PlayerGear.FlashlightOn + "（在 Metro 死端）");
}
else sb.AppendLine("③无 Flashlight 组件");
return sb.ToString();
