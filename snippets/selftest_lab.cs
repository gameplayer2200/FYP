var win = UnityEngine.GameObject.Find("anywhere-door-Metro-Lab").GetComponentInChildren<PortalWindow>(true);
if (win == null) return "lab window NOT FOUND（SpecimenLab 未加载？）";
var m = typeof(PortalWindow).GetMethod("SelfTest");
var args = new object[] { null };
bool ok = (bool)m.Invoke(win, args);
string r = (string)args[0];
int idx = r.LastIndexOf("结果");
return "PASS=" + ok + " [" + (idx >= 0 ? r.Substring(idx) : r) + "]";
