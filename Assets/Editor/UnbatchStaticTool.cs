// 静态合批解包工具
//
// 背景：本工程的场景/预制件是从已打包的游戏里还原出来的。原版游戏在烘焙时把
// 大量物体的网格合并成了 "Combined Mesh (root: scene) N" 资源，每个物体只保留
// 了 m_StaticBatchInfo（firstSubMesh / subMeshCount）指向合并网格里的一段。
// 这种物体的顶点已经是世界坐标、渲染时变换矩阵被强制为单位矩阵，所以在编辑器里
// 拖动 Transform 时只有碰撞体（BoxCollider）会跟着动，画面上的模型纹丝不动。
//
// 本工具的做法：把该物体用到的那一段三角面从合并网格里"抠"出来，生成一个独立的
// Mesh 资源，并把顶点反向变换回物体本地空间。之后它就是一个普通物体，可以自由
// 移动/旋转/缩放。
//
// 用法：
//   Tools/静态合批/解包选中物体      —— 只处理 Hierarchy 里选中的物体（含子物体）
//   Tools/静态合批/解包整个场景      —— 处理当前场景全部，会生成大量 Mesh 资源，慎用
//
// 注意：解包会给每个物体生成一个 .asset（放在 Assets/Art/PakoneSoft/Mesh/Extracted），
//      物体越多仓库越大；建议按需对要移动的物件使用。

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class UnbatchStaticTool
{
    const string ExtractedFolder = "Assets/Art/PakoneSoft/Mesh/Extracted";

    [MenuItem("Tools/静态合批/解包选中物体 %u", false, 10)]
    static void UnbatchSelection()
    {
        var roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            EditorUtility.DisplayDialog("静态合批", "请先在 Hierarchy 里选中要解包的物体。", "好");
            return;
        }

        int done = 0, skipped = 0;
        try
        {
            for (int i = 0; i < roots.Length; i++)
            {
                var gos = roots[i].GetComponentsInChildren<MeshRenderer>(true);
                for (int j = 0; j < gos.Length; j++)
                {
                    float p = (i + (j / (float)Mathf.Max(1, gos.Length))) / roots.Length;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "解包静态合批", roots[i].name + " / " + gos[j].name, p))
                        return;
                    if (Extract(gos[j])) done++; else skipped++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("静态合批",
            "解包完成。\n成功：" + done + " 个\n跳过（未合批）：" + skipped + " 个\n\n" +
            "资源目录：" + ExtractedFolder, "好");
    }

    [MenuItem("Tools/静态合批/解包整个场景", false, 11)]
    static void UnbatchWholeScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("解包整个场景",
                "将把场景 “" + scene.name + "” 里所有合并网格物体解包成独立 Mesh。\n\n" +
                "这会生成上千个 .asset 文件（仓库会明显变大），耗时可能几分钟。\n\n确定继续？",
                "继续", "取消"))
            return;

        var renderers = Object.FindObjectsOfType<MeshRenderer>();
        int done = 0, skipped = 0;
        try
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                        "解包整个场景", renderers[i].name, i / (float)renderers.Length))
                    return;
                if (Extract(renderers[i])) done++; else skipped++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("静态合批",
            "解包完成。\n成功：" + done + " 个\n跳过：" + skipped + " 个", "好");
    }

    /// <summary>把单个渲染器从合并网格里解包出来。返回 true 表示确实做了处理。</summary>
    public static bool Extract(MeshRenderer mr)
    {
        if (mr == null) return false;
        var mf = mr.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;

        var so = new SerializedObject(mr);
        var info = so.FindProperty("m_StaticBatchInfo");
        if (info == null) return false;
        int first = info.FindPropertyRelative("firstSubMesh").intValue;
        int count = info.FindPropertyRelative("subMeshCount").intValue;
        if (count <= 0) return false;                  // 本来就没合批，跳过

        var src = mf.sharedMesh;
        if (first < 0 || first + count > src.subMeshCount) return false;

        var sv = src.vertices;
        var sn = src.normals;
        var st = src.tangents;
        var suv = src.uv;
        var suv2 = src.uv2;
        var sc = src.colors;
        bool hasNorm = sn.Length == sv.Length;
        bool hasTan = st.Length == sv.Length;
        bool hasUV = suv.Length == sv.Length;
        bool hasUV2 = suv2.Length == sv.Length;
        bool hasCol = sc.Length == sv.Length;

        // 收集这一段子网格的所有三角面
        var tris = new List<int>();
        for (int s = first; s < first + count; s++)
            tris.AddRange(src.GetTriangles(s));
        if (tris.Count == 0) return false;

        // 顶点重映射：只保留被用到的顶点
        var map = new Dictionary<int, int>();
        var srcOfNew = new List<int>();
        var outTris = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            int o = tris[i], ni;
            if (!map.TryGetValue(o, out ni))
            {
                ni = map.Count;
                map[o] = ni;
                srcOfNew.Add(o);
            }
            outTris[i] = ni;
        }

        int n = srcOfNew.Count;
        var w2l = mr.transform.worldToLocalMatrix;      // 合并网格顶点≈世界坐标，转回本地
        var invRot = Quaternion.Inverse(mr.transform.rotation);
        var nv = new Vector3[n];
        var nn = new Vector3[n];
        var nt = new Vector4[n];
        var nuv = new Vector2[n];
        var nuv2 = new Vector2[n];
        var nc = new Color[n];
        for (int k = 0; k < n; k++)
        {
            int o = srcOfNew[k];
            nv[k] = w2l.MultiplyPoint3x4(sv[o]);
            if (hasNorm) nn[k] = invRot * sn[o];
            if (hasTan)
            {
                var t = invRot * new Vector3(st[o].x, st[o].y, st[o].z);
                nt[k] = new Vector4(t.x, t.y, t.z, st[o].w);
            }
            if (hasUV) nuv[k] = suv[o];
            if (hasUV2) nuv2[k] = suv2[o];
            if (hasCol) nc[k] = sc[o];
        }

        var nm = new Mesh();
        nm.name = mr.gameObject.name;
        nm.indexFormat = IndexFormat.UInt32;
        nm.vertices = nv;
        if (hasNorm) nm.normals = nn;
        if (hasTan) nm.tangents = nt;
        if (hasUV) nm.uv = nuv;
        if (hasUV2) nm.uv2 = nuv2;
        if (hasCol) nm.colors = nc;
        nm.triangles = outTris;
        nm.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder(ExtractedFolder))
            AssetDatabase.CreateFolder("Assets/Art/PakoneSoft/Mesh", "Extracted");

        string safe = mr.gameObject.name.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
        string path = AssetDatabase.GenerateUniqueAssetPath(ExtractedFolder + "/" + safe + ".asset");
        AssetDatabase.CreateAsset(nm, path);

        // 换回独立网格 + 清掉合批信息与静态标记
        mf.sharedMesh = nm;
        info.FindPropertyRelative("firstSubMesh").intValue = 0;
        info.FindPropertyRelative("subMeshCount").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        var flags = GameObjectUtility.GetStaticEditorFlags(mr.gameObject);
        GameObjectUtility.SetStaticEditorFlags(
            mr.gameObject, flags & ~StaticEditorFlags.BatchingStatic);

        return true;
    }
}
