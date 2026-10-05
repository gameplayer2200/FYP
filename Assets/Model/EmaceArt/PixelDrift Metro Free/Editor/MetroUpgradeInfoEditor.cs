using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MetroUpgradeInfo))]
public class MetroUpgradeInfoEditor : Editor
{
    private Texture2D coverImage;
    private GUIStyle headlineStyle;
    private GUIStyle bodyTextStyle;
    
    // Stały link ignorujący błędy zapisu w scenie
    private const string TargetUrl = "https://assetstore.unity.com/packages/3d/environments/urban/pixeldrift-metro-3d-pixel-art-subway-for-boomer-fps-vr-129896";

    private void OnEnable()
    {
        string pathPNG = "Assets/EmaceArt/PixelDrift Metro Free/Scripts/MetroCover.png";
        string pathJPG = "Assets/EmaceArt/PixelDrift Metro Free/Scripts/MetroCover.jpg";

        coverImage = AssetDatabase.LoadAssetAtPath<Texture2D>(pathPNG);
        if (coverImage == null) coverImage = AssetDatabase.LoadAssetAtPath<Texture2D>(pathJPG);
    }

    public override void OnInspectorGUI()
    {
        if (headlineStyle == null)
        {
            headlineStyle = new GUIStyle(GUI.skin.label) 
            { 
                fontSize = 14, 
                fontStyle = FontStyle.Bold, 
                wordWrap = true, 
                richText = true,
                margin = new RectOffset(5, 5, 2, 2)
            };
        }
        if (bodyTextStyle == null)
        {
            bodyTextStyle = new GUIStyle(GUI.skin.label) 
            { 
                fontSize = 12, 
                wordWrap = true, 
                richText = true,
                margin = new RectOffset(5, 5, 2, 2)
            };
        }

        EditorGUILayout.Space(5);

        if (coverImage != null)
        {
            float imageAspect = (float)coverImage.width / coverImage.height;
            float maxWidth = Mathf.Min(EditorGUIUtility.currentViewWidth - 45f, coverImage.width);
            float calculatedHeight = maxWidth / imageAspect;

            Rect imageRect = GUILayoutUtility.GetRect(maxWidth, calculatedHeight);
            GUI.DrawTexture(imageRect, coverImage, ScaleMode.ScaleToFit);

            if (GUI.Button(imageRect, GUIContent.none, GUIStyle.none))
            {
                Application.OpenURL(TargetUrl);
            }
            EditorGUILayout.Space(10);
        }

        // 1. Nagłówek
        GUILayout.Label("<b>PixelDrift: Metro PRO – Full Urban Potential</b>", headlineStyle);
        EditorGUILayout.Space(4);

        // 2. Treść promocyjna
        GUILayout.Label("You are currently using Metro Free, which offers 76 basic prefabs and a playable demo scene. It is a solid foundation, but only a fraction of the PixelDrift system’s true potential.", bodyTextStyle);
        EditorGUILayout.Space(6);

        GUILayout.Label("<b>Metro PRO</b> features 400+ models across 7 categories: architecture, infrastructure, visual information, tech rooms, and more. Every model comes in three texture variants: <b>clean, dirty, and post-apocalyptic</b>.", bodyTextStyle);
        EditorGUILayout.Space(6);

        GUILayout.Label("Gain full creative freedom with 5x more assets while maintaining the same optimal, VR-tested performance.", bodyTextStyle);
        EditorGUILayout.Space(15);

        // 3. Przycisk akcji (bez tekstu o cenie)
        GUIStyle mainButtonStyle = new GUIStyle(GUI.skin.button) 
        { 
            fontStyle = FontStyle.Bold, 
            fontSize = 12,
            wordWrap = true,
            padding = new RectOffset(10, 10, 10, 10)
        };

        if (GUILayout.Button("Get PixelDrift: Metro PRO - 3D Pixel Art Subway", mainButtonStyle, GUILayout.Height(45)))
        {
            Application.OpenURL(TargetUrl);
        }

        EditorGUILayout.Space(10);
    }
}