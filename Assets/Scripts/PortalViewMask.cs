using UnityEngine;

/// <summary>
/// 传送门取景相机的渲染遮挡器：挂在本相机上，渲染期间临时隐藏指定物体。
/// 视点同步后虚拟相机站在对面门背后往门洞里看——不藏掉对面门的话，
/// 拍到的是对面门自己的门框/门板/门洞屏（屏上还嵌套显示着本侧画面，无限套娃）。
/// OnPreRender 隐藏、OnPostRender 按缓存的原状态恢复，只影响本相机这一帧渲染。
/// </summary>
public class PortalViewMask : MonoBehaviour
{
    private Renderer[] hide;
    private bool[] origEnabled;
    private Material skyOverride;      // 项目无天空盒材质时给传送门相机专用的程序化蓝天
    private Material prevSky;
    private bool swappedSky;

    public void SetSkybox(Material sky) => skyOverride = sky;

    public void SetHidden(GameObject root)
    {
        if (root == null) return;
        hide = root.GetComponentsInChildren<Renderer>(true);
        origEnabled = new bool[hide.Length];
        for (int i = 0; i < hide.Length; i++) origEnabled[i] = hide[i] != null && hide[i].enabled;
    }

    private void OnPreRender()
    {
        if (skyOverride != null && RenderSettings.skybox == null)
        {
            prevSky = RenderSettings.skybox;
            RenderSettings.skybox = skyOverride;
            swappedSky = true;
        }
        if (hide == null) return;
        for (int i = 0; i < hide.Length; i++)
            if (hide[i] != null && hide[i].enabled) hide[i].enabled = false;
    }

    private void OnPostRender()
    {
        if (swappedSky)
        {
            RenderSettings.skybox = prevSky;
            swappedSky = false;
        }
        if (hide == null) return;
        for (int i = 0; i < hide.Length; i++)
            if (hide[i] != null) hide[i].enabled = origEnabled[i];
    }
}
