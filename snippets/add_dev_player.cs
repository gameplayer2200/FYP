// 把实验室玩家复制一份进 Metro 场景（常驻开发用；双玩家守卫保证运行时只活一个）
var lab = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SpecimenLab.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.GameObject src = null;
foreach (var m in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (m != null && m.GetType().Name == "PlayerMovement") { src = m.gameObject; break; }
}
if (src == null)
{
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(lab, true);
    return "Lab 玩家 NOT FOUND";
}
var metro = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var clone = UnityEngine.Object.Instantiate(src);
clone.name = "Player(Metro开发)";
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, metro);
clone.transform.position = new UnityEngine.Vector3(-46.5f, 94.2f, 137f);
clone.transform.rotation = UnityEngine.Quaternion.Euler(0f, 180f, 0f);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(metro);
UnityEditor.SceneManagement.EditorSceneManager.CloseScene(lab, true);
return "dev player 已存入 Metro 场景 @" + clone.transform.position.ToString("F1");
