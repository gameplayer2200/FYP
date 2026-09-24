using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class FixHingeTool
{
    [MenuItem("Tools/FixDemoDoorHinge")]
    public static void Run()
    {
        Scene demo = default;
        Scene lab = default;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name == "DemoScene") demo = s;
            if (s.name == "SpecimenLab") lab = s;
        }
        if (!demo.isLoaded)
        {
            string[] guids = AssetDatabase.FindAssets("DemoScene t:Scene");
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            demo = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }
        // 清理 SpecimenLab 里的残留空 pivot
        if (lab.isLoaded)
        {
            foreach (var r in lab.GetRootGameObjects())
                if (r.name == "DemoDoorLeafPivot") { Object.DestroyImmediate(r); Debug.Log("[FixHinge] 删除实验室空pivot"); }
        }
        // 场景限定查找：只找 DemoScene 自己的门框
        Transform frame = null;
        foreach (var r in demo.GetRootGameObjects())
        {
            if (r.name == "DemoScene_Root") { frame = r.transform.Find("anywhere-door"); break; }
        }
        if (frame == null) { Debug.LogError("[FixHinge] DemoScene 门框未找到"); return; }
        Transform leaf = frame.Find("Cube");
        if (leaf == null) { Debug.LogError("[FixHinge] 门板 Cube 未找到"); return; }

        // 清理旧 pivot（可能在任何位置）
        var oldPivot = GameObject.Find("DemoDoorLeafPivot");
        if (oldPivot != null) Object.DestroyImmediate(oldPivot);

        var rend = leaf.GetComponent<Renderer>();
        var b = rend.bounds;
        SceneManager.SetActiveScene(demo); // 确保新对象落在 DemoScene
        var pivot = new GameObject("DemoDoorLeafPivot");
        pivot.transform.SetParent(frame, false);
        pivot.transform.position = new Vector3(b.center.x + b.size.x * 0.5f, 0f, b.center.z);
        leaf.SetParent(pivot.transform, true);
        pivot.transform.rotation = Quaternion.Euler(0, -90, 0);
        var door = leaf.GetComponent<AnywhereDoor>();
        if (door != null) Object.DestroyImmediate(door);
        EditorSceneManager.MarkSceneDirty(demo);
        bool saved = EditorSceneManager.SaveScene(demo);
        EditorSceneManager.CloseScene(demo, true);
        if (lab.isLoaded) SceneManager.SetActiveScene(lab);
        Debug.Log($"[FixHinge] 完成 saved={saved} leafPos={leaf.position:F2} leafYaw={leaf.eulerAngles.y:F0} parent={leaf.parent.name}");
    }
}
