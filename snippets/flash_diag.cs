// 手电筒 Metro 失效复现诊断
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
var cam = pr.GetProperty("Camera").GetValue(null) as UnityEngine.Camera;
sb.AppendLine("player=" + player.position.ToString("F1") + " 场景=" + player.gameObject.scene.name + " cam=" + (cam != null) + " camGO层级=" + (cam != null ? LayerMask.LayerToName(cam.gameObject.layer) : "-"));
// 拾取
var pick = UnityEngine.GameObject.Find("道具_手电筒_Metro");
if (pick != null) { pick.GetComponent<FlashlightPickup>().Interact(player.gameObject); sb.AppendLine("已拾取"); }
else sb.AppendLine("拾取物不存在（已拿过？）");
sb.AppendLine("HasFlashlight=" + PlayerGear.HasFlashlight + " 组件=" + (player.GetComponentInChildren<Flashlight>(true) != null));
var fl = player.GetComponentInChildren<Flashlight>(true);
if (fl == null) return sb.ToString() + " | 无组件——这就是问题";
// 驱动 Update 建灯锥 + 开灯
typeof(Flashlight).GetMethod("Update", bf).Invoke(fl, null);
fl.Toggle();
var spot = fl.GetComponentInChildren<Light>(true);
if (spot == null) return sb.ToString() + " | 灯锥未创建";
sb.AppendLine("灯锥 enabled=" + spot.enabled + " mask=" + spot.cullingMask.ToString("X") + " inten=" + spot.intensity + " 父链active=" + spot.gameObject.activeInHierarchy + " 灯锥所在场景=" + spot.gameObject.scene.name);
sb.AppendLine("FlashlightOn=" + PlayerGear.FlashlightOn);
// 玩家朝向近处墙（隧道A东侧）便于截图验证亮度
player.rotation = UnityEngine.Quaternion.Euler(0f, 90f, 0f); // 朝 +X 墙
if (cam != null) cam.transform.localRotation = UnityEngine.Quaternion.identity;
return sb.ToString();
