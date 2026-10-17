using System.Collections;
using UnityEngine;

/// <summary>
/// 站台粉笔箭头（Metro 装饰性引导，backup 稿「前人痕迹」）：沿设定点位在地面生成
/// 程序绘制的粉笔箭头（无外部贴图资产）；顺着走 = 指向配电室的方向提示；
/// 逆着走进惊吓区（车厢方向）触发一次「身后的灯灭了」——箭头是给出去的人看的。
/// </summary>
public class ChalkTrail : MonoBehaviour
{
    [Header("箭头点位（本地坐标，相对本物体）")]
    [SerializeField] private Vector3[] localPoints;
    [SerializeField] private float arrowSize = 0.55f;

    [Header("逆行惊吓（本地中心+尺寸的盒）")]
    [SerializeField] private Vector3 scareCenter;
    [SerializeField] private Vector3 scareSize = new Vector3(8f, 4f, 10f);
    [SerializeField] private MetroBlackout blackout;
    [SerializeField] private LightGroup[] groups;
    [SerializeField] private string scareHint = "箭头是给出去的人看的……";

    private Texture2D arrowTex;
    private bool scared;

    private void Start()
    {
        // 幂等：编辑器失焦等场景下 Start 可能被外部重驱，已有箭头就不重复生成
        if (transform.childCount > 0) return;
        arrowTex = BuildArrowTexture();
        foreach (var p in localPoints)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "粉笔箭头";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = p + new Vector3(0f, 0.02f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 平贴地面，箭头朝 -Z
            quad.transform.localScale = Vector3.one * arrowSize;
            Object.Destroy(quad.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Unlit/Transparent"));
            mat.mainTexture = arrowTex;
            mat.color = new Color(0.85f, 0.87f, 0.8f, 0.85f);
            quad.GetComponent<Renderer>().material = mat;
        }
    }

    private void Update()
    {
        if (scared || blackout == null || !blackout.PlayerInZone) return;
        var player = PlayerRef.Root;
        if (player == null) return;
        var local = transform.InverseTransformPoint(player.position);
        var d = local - scareCenter;
        if (Mathf.Abs(d.x) <= scareSize.x * 0.5f && Mathf.Abs(d.y) <= scareSize.y * 0.5f
            && Mathf.Abs(d.z) <= scareSize.z * 0.5f)
        {
            scared = true;
            StartCoroutine(ScareRoutine());
        }
    }

    private IEnumerator ScareRoutine()
    {
        HintToast.Show(scareHint, 3.5f);
        bool[] wasOn = new bool[groups != null ? groups.Length : 0];
        for (int i = 0; i < wasOn.Length; i++) wasOn[i] = groups[i].IsOn;
        // 两下骤闪，然后全灭片刻，再复原
        for (int round = 0; round < 2; round++)
        {
            for (int i = 0; i < wasOn.Length; i++) groups[i].SetOn(false, false);
            yield return new WaitForSeconds(0.12f);
            for (int i = 0; i < wasOn.Length; i++) groups[i].SetOn(true, false);
            yield return new WaitForSeconds(0.18f);
        }
        for (int i = 0; i < wasOn.Length; i++) groups[i].SetOn(false, false);
        yield return new WaitForSeconds(1.6f);
        for (int i = 0; i < wasOn.Length; i++) groups[i].SetOn(wasOn[i], false);
    }

    /// <summary>程序画一个粉笔箭头贴图（白灰抖动线条）。尖画在贴图底部：Quad 以 (90,0,0)
    /// 平贴地面后贴图顶部朝 +Z，尖端即指向 -Z（站台→死端方向）。</summary>
    private static Texture2D BuildArrowTexture()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var rng = new System.Random(7);
        var clear = new Color(0f, 0f, 0f, 0f);
        var chalk = new Color(1f, 1f, 1f, 1f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                tex.SetPixel(x, y, clear);
        // 箭头尖：贴图下方 1/7~3/8，收拢成三角
        for (int y = n / 7; y < n * 3 / 8; y++)
        {
            float k = 1f - (y - n / 7f) / (n * 3 / 8f - n / 7f); // 越往下越宽 → 尖在底
            int half = Mathf.RoundToInt(Mathf.Lerp(1, n / 3, k));
            int jitter = (int)((rng.NextDouble() - 0.5) * 2.0);
            for (int x = n / 2 - half; x <= n / 2 + half + jitter / 2; x++)
                if (rng.NextDouble() > 0.15) tex.SetPixel(x, y, chalk);
        }
        // 箭杆：3/8 到 5/6，宽 4px，带抖动
        for (int y = n * 3 / 8; y <= n * 5 / 6; y++)
        {
            int jitter = (int)((rng.NextDouble() - 0.5) * 2.2);
            for (int x = n / 2 - 2; x <= n / 2 + 1 + jitter / 2; x++)
                if (rng.NextDouble() > 0.12) tex.SetPixel(x + jitter / 2, y, chalk);
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
