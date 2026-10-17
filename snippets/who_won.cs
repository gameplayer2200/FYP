var sb = new System.Text.StringBuilder();
sb.Append("scenes=" + UnityEngine.SceneManagement.SceneManager.sceneCount);
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    sb.Append(" [" + i + ":" + s.name + (s.isLoaded ? "" : "(卸)") + "]");
}
sb.Append(" active=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
foreach (var m in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
{
    if (m != null && m.GetType().Name == "PlayerMovement")
        sb.AppendLine(" | Player「" + m.gameObject.name + "」activeSelf=" + m.gameObject.activeSelf
            + " 场景=" + m.gameObject.scene.name + " pos=" + m.transform.position.ToString("F1));
}
sb.AppendLine("WorldStreamer: " + WorldStreamer.StatusText());
return sb.ToString();
