using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界连接器：Additive 加载异世界场景，找到其中的任意门，
/// 把整个世界根节点对齐到实验室锚点——两扇门背靠背拼成一条门廊，
/// 门后是真实几何：开门即见、走过去即达、回头看得到出发世界。
/// </summary>
public class WorldSceneLink : MonoBehaviour
{
    [SerializeField] private string worldSceneName = "DemoScene"; // 世界场景名
    [SerializeField] private Transform labAnchor;                 // 实验室侧锚点（门front面外侧，朝向=门朝向）
    [SerializeField] private bool autoOpenWorldDoor = true;       // 加载后自动打开远端门（常开）

#if UNITY_EDITOR
    private void Awake()
    {
        // 自动把世界场景注册进 Build Settings，避免"couldn't be loaded"
        bool found = false;
        foreach (var s in UnityEditor.EditorBuildSettings.scenes)
        {
            if (s.path.Replace('\\', '/').EndsWith(worldSceneName + ".unity") && s.enabled) { found = true; break; }
        }
        if (!found)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(worldSceneName + " t:Scene");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var list = new System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
                list.Add(new UnityEditor.EditorBuildSettingsScene(path, true));
                UnityEditor.EditorBuildSettings.scenes = list.ToArray();
                Debug.Log("[WorldSceneLink] 已自动注册场景到 Build Settings: " + path);
            }
        }
    }
#endif

    private void Start()
    {
        Scene worldScene = SceneManager.GetSceneByName(worldSceneName);
        if (worldScene.isLoaded)
        {
            AlignWorld(worldScene);
        }
        else
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError("[WorldSceneLink] LoadSceneAsync 返回 null——场景 " + worldSceneName + " 不在 Build Settings 或正在加载中");
                return;
            }
            op.completed += OnWorldLoaded;
        }
    }

    private void OnWorldLoaded(AsyncOperation op)
    {
        AlignWorld(SceneManager.GetSceneByName(worldSceneName));
    }

    private void AlignWorld(Scene worldScene)
    {
        // 只在异世界场景内找它的任意门（实验室自己的门不在这个场景里，不会误配）
        // 优先找门整体根物体（名为 anywhere-door），找不到再退回脚本所在的门板
        GameObject worldRoot = null;
        Transform worldDoor = null;
        foreach (GameObject rootObj in worldScene.GetRootGameObjects())
        {
            if (worldRoot == null) worldRoot = rootObj;
            Transform named = rootObj.transform.Find("anywhere-door");
            if (named != null && worldDoor == null) { worldDoor = named; continue; }
            if (worldDoor == null)
            {
                AnywhereDoor[] doors = rootObj.GetComponentsInChildren<AnywhereDoor>(true);
                if (doors.Length > 0) worldDoor = doors[0].transform;
            }
        }
        if (worldRoot == null || worldDoor == null)
        {
            Debug.LogError("[WorldSceneLink] 在场景 " + worldSceneName + " 中没找到任意门或根物体");
            return;
        }

        // 把异世界门从"它在世界场景里的本地姿态"搬到"实验室锚点姿态"
        // rootRot * doorLocalRot = anchorRot  →  rootRot = anchorRot * Inverse(doorLocalRot)
        // rootRot * doorLocalPos + rootPos = anchorPos → rootPos = anchorPos - rootRot * doorLocalPos
        Transform rootT = worldRoot.transform;
        Quaternion doorLocalRot = worldDoor.localRotation;
        Vector3 doorLocalPos = worldDoor.localPosition;
        rootT.rotation = labAnchor.rotation * Quaternion.Inverse(doorLocalRot);
        rootT.position = labAnchor.position - rootT.rotation * doorLocalPos;

        if (autoOpenWorldDoor)
        {
            AnywhereDoor door = worldDoor.GetComponentInChildren<AnywhereDoor>(true);
            if (door != null && !door.IsOpen) door.SetOpen(true);
        }

        // 禁用异世界自带的相机，避免和玩家的第一人称相机抢渲染
        foreach (var cam in worldRoot.GetComponentsInChildren<Camera>(true))
        {
            cam.gameObject.SetActive(false);
        }
    }
}
