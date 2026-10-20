// 第五轮验证②：两段式收容（反射跳过开盖动画）→ OnDeposit → 账单 → 图鉴
var sb = new System.Text.StringBuilder();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
BillingLedger.AddLighting(3.2f);
BillingLedger.AddAmbient(11.5f);
BillingLedger.AddDevice(1f);
BillingLedger.AddHit(20f);
int picked = 0;
foreach (var sp in UnityEngine.Object.FindObjectsOfType<SpecimenPickup>(true))
    if (sp.gameObject.activeSelf && picked < 3) { sp.Interact(player.gameObject); picked++; }
var inv = Inventory.Instance;
sb.AppendLine("①拾取 " + picked + " carrying=" + inv.SpecimenCount + "（应3/3）");
foreach (var dep in UnityEngine.Object.FindObjectsOfType<SpecimenDeposit>(true))
{
    typeof(SpecimenDeposit).GetField("lidOpen", bf).SetValue(dep, true); // 模拟已开盖
    dep.Interact(player.gameObject);                                     // 存放 → StoreAllSpecimens → OnDeposit
}
sb.AppendLine("②入账 stored=" + inv.StoredSpecimenCount + " archive=" + inv.Archive.Count + "（应3/3）");
for (int i = 0; i < inv.Archive.Count; i++)
    sb.AppendLine("   #" + (i + 1) + " " + inv.Archive[i].name);
sb.AppendLine("③账本 照明=" + BillingLedger.Lighting.ToString("F1") + " 环境=" + BillingLedger.Ambient.ToString("F1")
    + " 机动=" + BillingLedger.Device.ToString("F1") + " 受创=" + BillingLedger.Hit.ToString("F1")
    + " 合计=" + BillingLedger.Total.ToString("F1") + "/" + BillingLedger.Capacity);
foreach (var b in UnityEngine.Object.FindObjectsOfType<BillPanel>(true))
{
    var su = typeof(BillPanel).GetField("showUntil", bf);
    float until = (float)su.GetValue(b);
    sb.AppendLine("④账单显示 " + (until > UnityEngine.Time.unscaledTime ? "✓（至 " + until.ToString("F1") + "）" : "✗"));
}
return sb.ToString();
