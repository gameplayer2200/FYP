using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 取景 RenderTexture 池。
///
/// ── 为什么要池化（对照 Project Doors 的 PortalRTPool） ─────────────────
/// 改造前每扇门各自 `new RenderTexture(...)`，世界被卸下时 `Destroy(rt)`，玩家来回穿门
/// 就是反复「申请显存 → GPU 分配 → 提交 → 释放」。RenderTexture 的创建/销毁在驱动层
/// 不是免费的：1080p×24bit 一张约 8MB 显存，创建要走一遍资源分配与格式验证，
/// 释放要等 GPU 把在用帧提交完。玩家在门前反复进出时，这些开销会叠在**穿门那一帧**上，
/// 和世界装载撞在一起就更像"卡死"。
///
/// 池化：RT 不 Destroy，只 Release 后放回队列；下次要同尺寸的再从队列拿。
/// 同一尺寸的 RT 一生只创建一次，反复穿门是零分配。
///
/// ── 为什么按尺寸分桶 ─────────────────────────────────────────────────
/// 取景分辨率是自适应的（远看 720、贴脸 1440），每扇门的门洞宽高比还不同。
/// 池子按「宽 × 高」精确匹配，命中不了就新造一张——尺寸种类有限
///（720/864/1080/1440 × 几种宽高比），跑一会儿就收敛，不会无限膨胀。
/// 每档最多囤 <see cref="MaxPerBucket"/> 张，多的直接销毁。
///
/// ── 为什么不需要 DontDestroyOnLoad ───────────────────────────────────
/// RenderTexture 不是场景对象，`new` 出来的实例不参与场景卸载，
/// 换场景不会把它连带销毁。所以池子只持有静态字典就够，
/// 不必像 GameObject 那样挂一个常驻空物体（那反而会多一个可疑的层级残留）。
///
/// ── 生命周期 ─────────────────────────────────────────────────────────
/// 静态字段在域重载时被重置，但**已经创建出来的 RT 对象**不一定跟着释放，
/// 所以用 SubsystemRegistration 钩子显式清场，避免上一局的显存泄漏到下一局。
/// </summary>
public static class PortalRtPool
{
    private const int MaxPerBucket = 2;      // 同尺寸最多留 2 张，再多就是泄漏

    private static readonly Dictionary<int, Stack<RenderTexture>> buckets =
        new Dictionary<int, Stack<RenderTexture>>();

    private static int createdCount;
    private static int reusedCount;

    /// <summary>池子里当前空闲的 RT 总数（诊断用）。</summary>
    public static int IdleCount
    {
        get
        {
            int n = 0;
            foreach (var b in buckets.Values) n += b.Count;
            return n;
        }
    }

    /// <summary>累计新建张数（诊断用）。</summary>
    public static int CreatedCount { get { return createdCount; } }

    /// <summary>累计复用次数（诊断用）。</summary>
    public static int ReusedCount { get { return reusedCount; } }

    /// <summary>
    /// 取一张指定宽高、24 位深度的 RT。
    /// 命中同尺寸空闲 RT 就复用（并确保它已 Create），否则新造一张。
    /// </summary>
    public static RenderTexture Get(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        int key = KeyOf(width, height);

        Stack<RenderTexture> bucket;
        if (buckets.TryGetValue(key, out bucket))
        {
            while (bucket.Count > 0)
            {
                var pooled = bucket.Pop();
                if (pooled == null) continue;                    // 被外部销毁过
                if (!pooled.IsCreated()) pooled.Create();        // 归还时 Release 过，要重开
                if (pooled.IsCreated())
                {
                    reusedCount++;
                    pooled.filterMode = FilterMode.Bilinear;
                    return pooled;
                }
                Object.Destroy(pooled);                          // 重开失败，弃掉换新的
            }
        }

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.Default);
        rt.filterMode = FilterMode.Bilinear;
        rt.name = "PortalRT_" + width + "x" + height;
        rt.Create();
        createdCount++;
        return rt;
    }

    /// <summary>
    /// 归还一张 RT。传 null 安全。
    /// 归还后**不要**再使用这个引用——它可能立刻被下一扇门借走。
    /// </summary>
    public static void Release(RenderTexture rt)
    {
        if (rt == null) return;

        int key = KeyOf(rt.width, rt.height);
        Stack<RenderTexture> bucket;
        if (!buckets.TryGetValue(key, out bucket))
        {
            bucket = new Stack<RenderTexture>();
            buckets[key] = bucket;
        }

        if (bucket.Count >= MaxPerBucket)
        {
            // 这一档已经囤够了，多的直接销毁，避免池子无上限长大
            rt.Release();
            Object.Destroy(rt);
            return;
        }

        // 相机可能还把它当 targetTexture，先解除引用再 Release，
        // 否则"释放正在使用的 RT"会在部分图形 API 上告警/花屏。
        rt.Release();
        bucket.Push(rt);
    }

    /// <summary>清空整个池（退出 Play / 彻底重置时用）。</summary>
    public static void Clear()
    {
        foreach (var bucket in buckets.Values)
        {
            while (bucket.Count > 0)
            {
                var rt = bucket.Pop();
                if (rt != null)
                {
                    rt.Release();
                    Object.Destroy(rt);
                }
            }
        }
        buckets.Clear();
        createdCount = 0;
        reusedCount = 0;
    }

    private static int KeyOf(int w, int h)
    {
        // 宽高各留 13 位（最大 8191），4K 门洞也够；冲突概率为零
        return (h << 13) | (w & 0x1FFF);
    }

    /// <summary>
    /// 每次进 Play 前把池子清空。
    /// 域重载会重置静态字段，但已经创建出来的 RT 对象不一定跟着释放——
    /// 不清就会出现"上一局的 RT 泄漏到这一局"，显存越玩越大。
    /// SubsystemRegistration 早于 AfterSceneLoad，正是清场的时机。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clear();
    }
}
