using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MetroUpgradeInfo : MonoBehaviour
{
    // Ta wartość może być nadpisana przez Unity w Inspektorze
    public string assetUrl = "https://assetstore.unity.com/packages/3d/environments/urban/pixeldrift-metro-3d-pixel-art-subway-for-boomer-fps-vr-129896";

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying) return;

        EditorApplication.delayCall += () =>
        {
            if (this != null && gameObject != null)
            {
                Selection.activeGameObject = gameObject;
            }
        };
    }
#endif
}