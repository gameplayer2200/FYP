using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 扫描特效演示布置生成器：在当前场景里搭一片立方体 + 一个 MatrixScanDemo 控制器。
/// 只往当前场景加东西，不新建/不覆盖场景文件，撤销一次即可全部回退。
/// </summary>
public static class MatrixScanDemoBuilder
{
    private const string RootName = "__扫描特效演示";
    private const string MatDir = "Assets/Material/MatrixVFX-master/";

    [MenuItem("FYP/扫描特效/生成演示布置", false, 40)]
    public static void Build()
    {
        if (Object.FindObjectOfType<MatrixScanDemo>() != null)
        {
            EditorUtility.DisplayDialog("扫描特效演示",
                "当前场景里已经有 MatrixScanDemo 了。先删掉旧的演示布置再生成。", "好");
            return;
        }

        Shader shader = Shader.Find("FYP/ScanMatrix");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("扫描特效演示",
                "找不到着色器 FYP/ScanMatrix（Assets/Shaders/ScanMatrix.shader）。" +
                "依赖素材在 " + MatDir + "（许可受限，见 Docs/ASSET-SOURCING.md §7.5）。",
                "好");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<Texture>(MatDir + "Assets/Texture/fonts_textures.png");
        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            MatDir + "Assets/Shaders/Resources/GenerateWhiteNoise.compute");

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "生成扫描特效演示");

        var field = new GameObject("探针阵列");
        field.transform.SetParent(root.transform, false);
        Undo.RegisterCreatedObjectUndo(field, "生成扫描特效演示");

        int made = 0;
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                for (int z = 0; z < 4; z++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "探针_" + x + y + z;
                    go.transform.SetParent(field.transform, false);
                    go.transform.localPosition = new Vector3(x * 3f - 4.5f, y * 2.5f + 1f, z * 3f - 4.5f);
                    float s = Random.Range(0.6f, 1.8f);
                    go.transform.localScale = new Vector3(s, s * Random.Range(0.8f, 2.2f), s);
                    go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    Undo.RegisterCreatedObjectUndo(go, "生成扫描特效演示");
                    made++;
                }
            }
        }

        var ctrl = new GameObject("扫描特效控制器");
        ctrl.transform.SetParent(root.transform, false);
        var demo = ctrl.AddComponent<MatrixScanDemo>();
        Undo.RegisterCreatedObjectUndo(ctrl, "生成扫描特效演示");

        var so = new SerializedObject(demo);
        so.FindProperty("whiteNoiseGenerator").objectReferenceValue = compute;
        so.FindProperty("fontTexture").objectReferenceValue = font;
        so.FindProperty("targetRoot").objectReferenceValue = field.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = ctrl;

        Debug.Log("[MatrixScanDemoBuilder] 已生成 " + made + " 个探针 + 1 个控制器。进 Play 按 G 触发扫描。"
                  + (compute == null || font == null ? " 注意：字体图集或 compute 没找到，需在 Inspector 里手动补。" : ""));
    }

    [MenuItem("FYP/扫描特效/清除演示布置", false, 41)]
    public static void Clear()
    {
        var existing = GameObject.Find(RootName);
        if (existing == null)
        {
            EditorUtility.DisplayDialog("扫描特效演示", "当前场景里没有演示布置。", "好");
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[MatrixScanDemoBuilder] 已清除演示布置。");
    }
}
