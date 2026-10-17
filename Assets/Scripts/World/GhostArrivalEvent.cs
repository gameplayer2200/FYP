using System.Collections;
using UnityEngine;

/// <summary>
/// 幽灵进站（Metro 专属异常）：有列车进站的全部征兆——轨道风压轰鸣渐强、
/// 灯管异常波动、已熄的死灯自己亮起来闪——但车永远不来。
/// 轰鸣最响时一切骤停，死寂两秒，灯态复原。「应该有车」而车不出现，这就是异常。
/// </summary>
public class GhostArrivalEvent : MonoBehaviour
{
    [SerializeField] private MetroBlackout blackout;
    [SerializeField] private LightGroup[] groups;
    [Tooltip("风压声源（循环 Audio，音量由本组件渐强渐弱控制）")]
    [SerializeField] private AudioSource roar;
    [Tooltip("首次事件距玩家进站的最短秒数")]
    [SerializeField] private float firstDelay = 60f;
    [SerializeField] private float minInterval = 180f;
    [SerializeField] private float maxInterval = 300f;
    [Tooltip("轰鸣渐强秒数")]
    [SerializeField] private float rampTime = 9f;
    [Tooltip("最强轰鸣持续秒数")]
    [SerializeField] private float holdTime = 5f;
    [Tooltip("骤停后的死寂秒数")]
    [SerializeField] private float silenceTime = 2.2f;
    [SerializeField] private float roarPeakVolume = 0.9f;
    [SerializeField] private string arrivalHint = "轨道在震——有车要进站？";
    [SerializeField] private string endHint = "……没有车。";

    private float nextIn;
    private bool running;

    private void Update()
    {
        if (running) return;
        if (blackout == null || !blackout.PlayerInZone) return;

        if (nextIn <= 0f) nextIn = firstDelay + Random.Range(0f, 90f);
        nextIn -= Time.deltaTime;
        if (nextIn <= 0f)
        {
            nextIn = Random.Range(minInterval, maxInterval);
            StartCoroutine(ArrivalRoutine());
        }
    }

    private IEnumerator ArrivalRoutine()
    {
        running = true;
        bool[] wasOn = new bool[groups != null ? groups.Length : 0];
        for (int i = 0; i < wasOn.Length; i++) wasOn[i] = groups[i].IsOn;

        HintToast.Show(arrivalHint);
        if (roar != null)
        {
            roar.volume = 0f;
            roar.Play();
        }

        // 阶段一：征兆渐强——灯管越来越不安分，死灯开始自己亮
        float t = 0f;
        while (t < rampTime)
        {
            float k = t / rampTime;
            if (roar != null) roar.volume = Mathf.Lerp(0f, roarPeakVolume, k);
            for (int i = 0; i < wasOn.Length; i++)
                groups[i].SetOn(Random.value < 0.2f + 0.45f * k, false);
            yield return new WaitForSeconds(0.25f);
            t += 0.25f;
        }

        // 阶段二：最响——高频乱闪
        t = 0f;
        while (t < holdTime)
        {
            for (int i = 0; i < wasOn.Length; i++)
                groups[i].SetOn(Random.value < 0.65f, false);
            yield return new WaitForSeconds(0.08f);
            t += 0.08f;
        }

        // 阶段三：骤停——声音瞬间消失，灯全灭，死寂
        if (roar != null) roar.Stop();
        for (int i = 0; i < wasOn.Length; i++) groups[i].SetOn(false, false);
        yield return new WaitForSeconds(silenceTime);

        // 恢复进站前的灯态
        for (int i = 0; i < wasOn.Length; i++) groups[i].SetOn(wasOn[i], false);
        HintToast.Show(endHint);
        running = false;
    }
}
