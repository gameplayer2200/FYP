var sb = new System.Text.StringBuilder();
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
// 站到 Metro 门 7m 内（预载半径 15）
pr.GetMethod("Teleport").Invoke(null, new object[]
{ new UnityEngine.Vector3(-47.9f, 94.2f, 140f), UnityEngine.Quaternion.identity });
// 手动泵 Metro 侧 PortalWindow 的 Update（触发 SpecimenLab 预载）
var upd = typeof(PortalWindow).GetMethod("Update",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var win = UnityEngine.GameObject.Find("anywhere-door-Metro").GetComponentInChildren<PortalWindow>(true);
for (int i = 0; i < 3; i++) upd.Invoke(win, null);
return "teleported + pumped, scenes now pump 完成后由下一段查询";
