using System.Collections;
using UnityEngine;

/// <summary>
/// 电磁风暴（Metro 专属异常）：停电进行中随机触发——灯组乱闪、HUD 乱码抖动。
/// 风暴中不信任仪器，只能靠肉眼与记忆。结束时恢复风暴前的灯态，不打断渐熄进度。
/// </summary>
public class EmfStormAnomaly : MonoBehaviour
{
    [SerializeField] private MetroBlackout blackout;
    [SerializeField] private LightGroup[] groups;
    [Tooltip("电流干扰声（可选，无则纯视觉）")]
    [SerializeField] private AudioSource sfx;
    [Tooltip("首次风暴距停电开始的最短秒数")]
    [SerializeField] private float firstDelay = 45f;
    [SerializeField] private float minInterval = 120f;
    [SerializeField] private float maxInterval = 240f;
    [SerializeField] private float duration = 10f;
    [SerializeField] private string stormHint = "EMF 风暴——仪器全在乱跳！";
    [SerializeField] private string calmHint = "风暴过去了……";

    private float nextIn;
    private bool storming;

    private void Update()
    {
        if (storming) return;
        if (blackout == null || !blackout.PlayerInZone) return;
        if (blackout.KilledGroups <= 0) return; // 只在停电开始后出现

        if (nextIn <= 0f) nextIn = firstDelay + Random.Range(0f, 60f); // 首次排程
        nextIn -= Time.deltaTime;
        if (nextIn <= 0f)
        {
            nextIn = Random.Range(minInterval, maxInterval);
            StartCoroutine(StormRoutine());
        }
    }

    private IEnumerator StormRoutine()
    {
        storming = true;
        bool[] wasOn = new bool[groups != null ? groups.Length : 0];
        for (int i = 0; i < wasOn.Length; i++) wasOn[i] = groups[i].IsOn;

        EmfStorm.Set(true);
        HintToast.Show(stormHint);
        if (sfx != null) sfx.Play();

        float t = 0f;
        while (t < duration)
        {
            for (int i = 0; i < wasOn.Length; i++)
                if (Random.value < 0.35f) groups[i].SetOn(Random.value < 0.5f, false);
            yield return new WaitForSeconds(0.12f);
            t += 0.12f;
        }

        for (int i = 0; i < wasOn.Length; i++) groups[i].SetOn(wasOn[i], false);
        if (sfx != null) sfx.Stop();
        EmfStorm.Set(false);
        HintToast.Show(calmHint);
        storming = false;
    }
}
