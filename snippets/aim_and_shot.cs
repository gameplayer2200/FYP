// 平视死端方向（清 pitch）后截图由外部触发
System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
var cam = pr.GetProperty("Camera").GetValue(null) as UnityEngine.Camera;
cam.transform.localRotation = UnityEngine.Quaternion.identity;
return "aimed flat";
