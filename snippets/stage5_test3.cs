// 第三轮验证 ③：瘫倒减携带 + 粉笔箭头
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
// 拾两块软盘再瘫倒
foreach (var sp in UnityEngine.Object.FindObjectsOfType<SpecimenPickup>(true))
    if (sp.gameObject.activeSelf) sp.Interact(player.gameObject);
var inv = UnityEngine.Object.FindObjectsOfType<Inventory>(true);
sb.AppendLine("①携带=" + inv[0].SpecimenCount + "（应2）");
CollapsePenalty.Notify();
sb.AppendLine("②瘫倒后携带=" + inv[0].SpecimenCount + " 已收容=" + inv[0].StoredSpecimenCount + "（应1/0）");
// 粉笔箭头
var chalk = UnityEngine.Object.FindObjectsOfType<ChalkTrail>(true);
if (chalk.Length == 0) return sb.ToString() + " | 粉笔箭头 NOT FOUND";
typeof(ChalkTrail).GetMethod("Start", bf).Invoke(chalk[0], null);
sb.AppendLine("③箭头数=" + chalk[0].transform.childCount + "（应6）");
pr.GetMethod("Teleport").Invoke(null, new object[] { new UnityEngine.Vector3(-41.5f, 94.5f, 175f), UnityEngine.Quaternion.identity });
typeof(ChalkTrail).GetMethod("Update", bf).Invoke(chalk[0], null);
var scaredF = typeof(ChalkTrail).GetField("scared", bf);
sb.AppendLine("④进惊吓区 scared=" + scaredF.GetValue(chalk[0]) + "（应True，协程惊吓已启动）");
return sb.ToString();
