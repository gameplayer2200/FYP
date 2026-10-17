var sb = new System.Text.StringBuilder();
sb.Append("scenes=" + UnityEngine.SceneManagement.SceneManager.sceneCount);
int pm = 0, activePm = 0;
foreach (var m in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (m != null && m.GetType().Name == "PlayerMovement") { pm++; if (m.gameObject.activeInHierarchy) activePm++; }
}
sb.Append(" players=" + pm + " active=" + activePm + "（应 1/1——实验室玩家被守卫禁用）");
var inv = UnityEngine.Object.FindObjectsOfType<Inventory>(true);
sb.Append(" inventory=" + inv.Length + " instance=" + (inv.Length > 0 && UnityEngine.Object.FindObjectsOfType<Inventory>(true)[0] == Inventory.Instance));
var dep = UnityEngine.Object.FindObjectsOfType<SpecimenDeposit>(true);
sb.Append(" deposit=" + dep.Length);
return sb.ToString();
