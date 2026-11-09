using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 前人残档库（Core 静态）：记录玩家读过哪些笔记。
///
/// 【为什么放在 Core 而不是 Content】
/// 笔记的**读取权**属于世界（层域里的笔记物体要知道自己有没有被读过），
/// 而**展示权**属于 Content（阅读窗、图鉴）。两边都依赖 Core，放这里不会成环。
/// 与 PlayerGear / BillingLedger / RealityAnchor 同一处理。
///
/// 【内容组织】
/// 一篇残档 = 标题 + 若干段正文。正文用字符串数组而不是富文本，
/// 因为 CRT 阅读窗要逐段控制行距与打字速度；富文本标签会跟打字机打架。
///
/// 【为什么不做成 ScriptableObject】
/// 残档条目少（每个世界 2~3 篇），做 SO 要为每篇建一个资产文件、
/// 还要处理引用丢失。走静态表 + 代码内联文本，改起来最快，也不占资产管线。
/// 等条目超过 30 篇再迁 SO 不迟。
/// </summary>
public static class ArchiveLog
{
    /// <summary>一篇残档。</summary>
    public class Entry
    {
        /// <summary>稳定 id（去重/存档用，不要改）。</summary>
        public string id;
        /// <summary>标题（阅读窗顶部）。</summary>
        public string title;
        /// <summary>落款（作者/时间，可空）。</summary>
        public string signer;
        /// <summary>正文段落。</summary>
        public string[] paragraphs;
        /// <summary>它属于哪个世界（用于图鉴分组）。</summary>
        public string world;

        public Entry(string id, string title, string signer, string world, params string[] paragraphs)
        {
            this.id = id;
            this.title = title;
            this.signer = signer;
            this.world = world;
            this.paragraphs = paragraphs;
        }
    }

    private static readonly Dictionary<string, Entry> table = new Dictionary<string, Entry>();
    private static readonly HashSet<string> read = new HashSet<string>();
    private static readonly List<Entry> readOrder = new List<Entry>();

    /// <summary>全部已读条目（按阅读顺序）。</summary>
    public static IReadOnlyList<Entry> ReadEntries { get { return readOrder; } }

    /// <summary>已读篇数。</summary>
    public static int ReadCount { get { return readOrder.Count; } }

    /// <summary>残档总数（图鉴显示"已读 N/总数"）。</summary>
    public static int TotalCount { get { return table.Count; } }

    /// <summary>读过一篇时广播（图鉴/HUD 订阅）。</summary>
    public static event System.Action<Entry> OnEntryRead;

    /// <summary>登记一篇残档（启动时由静态构造函数灌入）。</summary>
    public static void Register(Entry e)
    {
        if (e == null || string.IsNullOrEmpty(e.id)) return;
        table[e.id] = e;
    }

    /// <summary>取一篇（未登记返回 null）。</summary>
    public static Entry Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Entry e;
        return table.TryGetValue(id, out e) ? e : null;
    }

    /// <summary>是否已读。</summary>
    public static bool IsRead(string id)
    {
        return !string.IsNullOrEmpty(id) && read.Contains(id);
    }

    /// <summary>标记为已读（重复调用只算一次）。返回是否是新读的。</summary>
    public static bool MarkRead(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!read.Add(id)) return false;

        Entry e = Get(id);
        if (e != null) readOrder.Add(e);
        if (OnEntryRead != null && e != null) OnEntryRead(e);
        return true;
    }

    /// <summary>清空（重开存档/测试用）。</summary>
    public static void ResetRead()
    {
        read.Clear();
        readOrder.Clear();
    }

    static ArchiveLog()
    {
        Seed();
    }

    /// <summary>
    /// 内置残档内容。写法遵循项目调性：不解释超自然现象本身，
    /// 只写"当时那个人看到了什么、他以为自己在干什么"，恐怖来自错位而非怪物。
    /// </summary>
    private static void Seed()
    {
        // ── Metro 站 ──────────────────────────────────────────────
        Register(new Entry(
            "metro_b1",
            "值班记录 · 第四班",
            "值班员 陈",
            "Metro",
            "第 41 天。今天最后一班车又没来。",
            "调度室说线路一切正常，让我不要乱报。可我数过了，站台的灯从上到下，每天少亮一盏。",
            "今天是第四盏。等它全灭的时候，我大概就知道车为什么不来了。"));

        Register(new Entry(
            "metro_b2",
            "贴在配电室门上的纸条",
            "字迹潦草，墨迹晕开",
            "Metro",
            "三根线里只有一根是活的，别一次全接——会跳闸，跳了就回不去了。",
            "万用表在站台那截车厢上。我把它留在那儿了，因为我不想再测第二次。",
            "上一次测的时候，仪器读数一直往上走，指针撞到了极限还在走。我把表蒙起来了。"));

        Register(new Entry(
            "metro_b3",
            "末班车的行车记录（残页）",
            "车载登记系统自动记录",
            "Metro",
            "到站：无。发出：无。行驶时长：——",
            "乘客人数：1。",
            "备注：乘客拒绝下车。乘务员第二次询问时，座位上已经没有人了，但座位还是热的。"));

        // ── 工厂 ────────────────────────────────────────────────
        Register(new Entry(
            "factory_b1",
            "交接本 · 倒班留言",
            "夜班 未署名",
            "Factory",
            "白班的兄弟：表停在 17:59 不是坏了，是它不想走。别去修。",
            "我按排班表顺序开过一遍机器，中间错了一次，全厂的火花跟下雨一样。",
            "对了，如果你在泵房听见有人在数数，那是上一班的。他还没数完。"));

        Register(new Entry(
            "factory_b2",
            "考勤卡背面的字",
            "工号 0447",
            "Factory",
            "今天第 9 次打卡。系统说我今天已经下班了。",
            "可我还在厂里。灯一会儿亮一会儿灭，我分不清是白班还是夜班。",
            "如果这张卡被人捡到，说明我还没走出去。"));

        // ── 水淹区 ──────────────────────────────────────────────
        Register(new Entry(
            "flood_b1",
            "排水方案（未执行）",
            "工程组 · 草稿",
            "Flooded_Grounds",
            "三个阀门，先开最远的那个。顺序错了水会倒灌。",
            "水下去以后露出来的那层，我们从来没建过。图纸上没有。",
            "建议：别一个人下去。至少留一个人在上面看着水位。"));

        Register(new Entry(
            "flood_b2",
            "写给后来的人",
            "无名",
            "Flooded_Grounds",
            "我在这里待了三天，水一直没退。",
            "后来我发现，水不是淹上来的——是地面在慢慢变低。",
            "所以别量水位。量天花板。"));
    }
}
