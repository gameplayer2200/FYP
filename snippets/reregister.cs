System.Type pr = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
}
UnityEngine.GameObject playerRoot = null;
foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (mb != null && mb.GetType().Name == "PlayerMovement")
    {
        playerRoot = mb.gameObject;
        break;
    }
}
if (playerRoot == null) return "player GO not found";
var cam = playerRoot.GetComponentInChildren<UnityEngine.Camera>(true);
var cc = playerRoot.GetComponentInChildren<UnityEngine.CharacterController>(true);
pr.GetMethod("Register").Invoke(null, new object[] { playerRoot.transform, cam, cc });
var root = (UnityEngine.Transform)pr.GetProperty("Root").GetValue(null);
return "registered, root=" + (root != null ? root.position.ToString("F2") : "null");
