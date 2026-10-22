using System.Collections;
using UnityEngine;

/// <summary>
/// 「自开的生产线」（工厂夜班异常，幽灵进站的三段协程模式复刻）：夜班显形期随机触发——
/// 全厂机器无人自转：嗡鸣渐起、转子加速、远处金属敲击，全速运转十余秒后骤停死寂。
/// 「应该有人值班」而没有人，这就是异常。不动 MachinePowerSwitch 的启动状态：
/// 转子由本组件临时驱动，结束后停转，谜题进度不受影响。锁夜（或谜题完成）后平息。
/// </summary>
public class GhostProduction : MonoBehaviour
{
    [SerializeField] private FactoryShiftController controller; // 夜场景
    [Tooltip("事件期间自转的转子（风机叶片等；谜题机器的转子可共用——只转不改变状态）")]
    [SerializeField] private Transform[] rotors;
    [SerializeField] private Vector3 rotorAxis = Vector3.forward;
    [Tooltip("轰鸣（循环 Audio，音量由本组件渐强渐弱控制）")]
    [SerializeField] private AudioSource roar;
    [Tooltip("远处金属敲击（随机单发声源池）")]
    [SerializeField] private AudioSource[] clanks;
    [Tooltip("首次事件距玩家进厂的最短秒数")]
    [SerializeField] private float firstDelay = 45f;
    [SerializeField] private float minInterval = 120f;
    [SerializeField] private float maxInterval = 240f;
    [Tooltip("征兆渐强秒数")]
    [SerializeField] private float rampTime = 6f;
    [Tooltip("全速运转秒数")]
    [SerializeField] private float runTime = 9f;
    [Tooltip("骤停后死寂秒数")]
    [SerializeField] private float silenceTime = 2.5f;
    [SerializeField] private float peakVolume = 0.8f;
    [SerializeField] private float rotorSpeed = 420f;
    [SerializeField] private string startHint = "机器自己转了起来——没人合闸";
    [SerializeField] private string endHint = "……停了。像一直在等这个班次。";

    private float nextIn;
    private bool running;

    private void Update()
    {
        if (running) return;
        if (controller == null || !controller.PlayerInZone) return;
        if (!FactoryShift.AnomaliesActive) return;

        if (nextIn <= 0f) nextIn = firstDelay + Random.Range(0f, 60f);
        nextIn -= Time.deltaTime;
        if (nextIn <= 0f)
        {
            nextIn = Random.Range(minInterval, maxInterval);
            StartCoroutine(RunRoutine());
        }
    }

    private IEnumerator RunRoutine()
    {
        running = true;
        HintToast.Show(startHint);
        if (roar != null)
        {
            roar.volume = 0f;
            roar.Play();
        }

        // 阶段一：征兆渐强——嗡鸣爬升、转子越转越快、偶尔一声敲击
        float t = 0f;
        while (t < rampTime)
        {
            float k = t / rampTime;
            if (roar != null) roar.volume = Mathf.Lerp(0f, peakVolume, k);
            SpinRotors(rotorSpeed * k * Time.deltaTime);
            if (Random.value < 0.02f) PlayClank(0.3f + 0.5f * k);
            yield return null;
            t += Time.deltaTime;
        }

        // 阶段二：全速——无人车间全力运转
        t = 0f;
        while (t < runTime)
        {
            SpinRotors(rotorSpeed * Time.deltaTime);
            if (Random.value < 0.03f) PlayClank(1f);
            yield return null;
            t += Time.deltaTime;
        }

        // 阶段三：骤停——一切归零，死寂
        if (roar != null) roar.Stop();
        yield return new WaitForSeconds(silenceTime);
        HintToast.Show(endHint);
        running = false;
    }

    private void SpinRotors(float delta)
    {
        if (rotors == null) return;
        for (int i = 0; i < rotors.Length; i++)
            if (rotors[i] != null) rotors[i].Rotate(rotorAxis, delta, Space.Self);
    }

    private void PlayClank(float vol)
    {
        if (clanks == null || clanks.Length == 0) return;
        var c = clanks[Random.Range(0, clanks.Length)];
        if (c != null)
        {
            c.volume = vol;
            c.Play();
        }
    }
}
