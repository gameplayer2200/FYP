// 第五轮验证：拾取三件（档案）→ 入账（OnDeposit→账单）→ 图鉴数据 → 播报状态
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
// 制造账单数据（手动充值各通道——后台无帧，Update 记账不推进）
BillingLedger.AddLighting(3.2f);
BillingLedger.AddAmbient(11.5f);
BillingLedger.AddDevice(1f);
BillingLedger.AddHit(20f);
// 拾取三件异常物
int picked = 0;
foreach (var sp in UnityEngine.Object.FindObjectsOfType<SpecimenPickup>(true))
    if (sp.gameObject.activeSelf && picked < 3) { sp.Interact(player.gameObject); picked++; }
var inv = Inventory.Instance;
sb.AppendLine("①拾取 " + picked + " 件 carrying=" + inv.SpecimenCount);
// 入账 → OnDeposit → 账单
foreach (var dep in UnityEngine.Object.FindObjectsOfType<SpecimenDeposit>(true))
    dep.Interact(player.gameObject);
sb.AppendLine("②入账 stored=" + inv.StoredSpecimenCount + " archive=" + inv.Archive.Count + "（应3/3）");
for (int i = 0; i < inv.Archive.Count; i++)
    sb.AppendLine("   #" + (i + 1) + " " + inv.Archive[i].name + " — " + inv.Archive[i].flavor);
// 账单显示状态（反射 showUntil）
var bills = UnityEngine.Object.FindObjectsOfType<BillPanel>(true);
foreach (var b in bills)
{
    var su = typeof(BillPanel).GetField("showUntil", bf);
    sb.AppendLine("③账单显示至 " + ((float)su.GetValue(b)).ToString("F1") + " now=" + UnityEngine.Time.unscaledTime.ToString("F1") + "（应 > now）");
}
// 播报：手动触发世界变化（穿门测试另行），先查组件在位
var briefs = UnityEngine.Object.FindObjectsOfType<DutyBriefing>(true);
sb.AppendLine("④Briefing 组件=" + briefs.Length);
return sb.ToString();
