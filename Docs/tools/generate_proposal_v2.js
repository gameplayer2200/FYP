const {
  Document, Packer, Paragraph, TextRun, Header, Footer, Table, TableRow, TableCell,
  AlignmentType, HeadingLevel, PageNumber, WidthType, TableLayoutType, BorderStyle,
  ShadingType, VerticalAlign
} = require("docx");
const fs = require("fs");

const P = {
  table: { headerBg: "D4875A", headerText: "FFFFFF", accentLine: "D4875A", innerLine: "DDD0C8", surface: "F8F0EB" },
  body: "182030", secondary: "506070",
};

const NB = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };

function h1(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_1, spacing: { before: 320, after: 120 }, children: [
    new TextRun({ text, bold: true, color: P.body, font: { ascii: "Arial", eastAsia: "SimHei" }, size: 30 })] });
}
function h2(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_2, spacing: { before: 240, after: 100 }, children: [
    new TextRun({ text, bold: true, color: P.body, font: { ascii: "Arial", eastAsia: "SimHei" }, size: 27 })] });
}
function body(text, opts) {
  const o = opts || {};
  return new Paragraph({
    alignment: AlignmentType.JUSTIFIED, indent: { firstLine: o.noIndent ? 0 : 420 },
    spacing: { line: 312, after: o.after || 60 },
    children: [new TextRun({ text, size: 24, color: P.body })],
  });
}
function bodyLead(lead, text) {
  return new Paragraph({
    alignment: AlignmentType.JUSTIFIED, indent: { firstLine: 420 },
    spacing: { line: 312, after: 60 },
    children: [new TextRun({ text: lead, bold: true, size: 24, color: "8A5A2E" }),
               new TextRun({ text, size: 24, color: P.body })],
  });
}
function cellPara(text, opts) {
  const o = opts || {};
  return new Paragraph({
    alignment: o.center ? AlignmentType.CENTER : AlignmentType.LEFT,
    spacing: { line: 276 },
    children: [new TextRun({ text, size: o.size || 21, bold: !!o.bold, color: o.color || P.body })],
  });
}
function buildTable(headers, rows, widths) {
  const headerRow = new TableRow({
    tableHeader: true, cantSplit: true,
    children: headers.map((h, i) => new TableCell({
      shading: { fill: P.table.headerBg, type: ShadingType.CLEAR },
      verticalAlign: VerticalAlign.CENTER,
      margins: { top: 80, bottom: 80, left: 120, right: 120 },
      width: widths ? { size: widths[i], type: WidthType.PERCENTAGE } : undefined,
      children: [cellPara(h, { bold: true, color: P.table.headerText, center: true })],
    })),
  });
  const bodyRows = rows.map((r, ri) => new TableRow({
    cantSplit: true,
    children: r.map((cell, i) => new TableCell({
      shading: { fill: ri % 2 === 0 ? P.table.surface : "FFFFFF", type: ShadingType.CLEAR },
      verticalAlign: VerticalAlign.CENTER,
      margins: { top: 70, bottom: 70, left: 120, right: 120 },
      width: widths ? { size: widths[i], type: WidthType.PERCENTAGE } : undefined,
      children: [cellPara(cell)],
    })),
  }));
  return new Table({
    width: { size: 100, type: WidthType.PERCENTAGE }, layout: TableLayoutType.FIXED,
    borders: {
      top: { style: BorderStyle.SINGLE, size: 6, color: P.table.accentLine },
      bottom: { style: BorderStyle.SINGLE, size: 6, color: P.table.accentLine },
      left: NB, right: NB,
      insideHorizontal: { style: BorderStyle.SINGLE, size: 4, color: P.table.innerLine },
      insideVertical: { style: BorderStyle.SINGLE, size: 4, color: P.table.innerLine },
    },
    rows: [headerRow, ...bodyRows],
  });
}
function tableTitle(text) {
  return new Paragraph({ keepNext: true, spacing: { before: 140, after: 70 }, alignment: AlignmentType.CENTER,
    children: [new TextRun({ text, bold: true, size: 21, color: P.secondary })] });
}

const C = [];

// ═══ 总览 ═══
C.push(h1("零、提案总览与流程长度"));
C.push(body("提案按四大支柱组织：主世界（实验室安全屋与收容）、三大异常世界（乡村 / Metro / 工厂，各带独特规则与解谜）、探索道具（8 件调查仪器 + 场景道具）、异常体道具与规则改变（收容增益 / 规则池 / 元世界异想体）。所有提案只使用已在库的模型与场景素材。"));
C.push(bodyLead("流程长度原则：", "主流程目标通关时长 30~45 分钟——主世界 + 三大世界各收集 2 份记忆核心（共 6 份）即触发结局审讯。所有扩展内容不影响通关，砍掉任意提案也不影响其余提案。"));
C.push(tableTitle("表 0  四支柱 × 提案定位"));
C.push(buildTable(
  ["支柱", "包含提案", "定位"],
  [
    ["主世界（实验室）", "收容翼扩建 / 实验室异变副作用 / 审讯室终局", "主线"],
    ["三大异常世界", "乡村记忆循环 / Metro 渐渐熄灭 / 工厂昼夜轮班（各含谜题与奖励）", "主线"],
    ["探索道具", "调查仪器 8 件 / RandomObjects 道具映射", "主线（手电、EMF、拍照三件）+ 扩展"],
    ["异常体道具与规则改变", "规则池 11 条 / 元世界异想体（三不猴机制）/ 十二舰收容改装 / 静态异想体精选", "扩展（不影响通关）"],
    ["氛围与演出", "灯光事件 / 音效层 / 快赢提案合集", "主线氛围 + 扩展演出"],
  ],
  [22, 58, 20]
));

// ═══ 支柱一：主世界 ═══
C.push(h1("一、支柱一：主世界——实验室（安全屋与收容）"));

C.push(h2("1.1 核心循环"));
C.push(body("实验室是安全屋：传送门的出发点、收容槽所在地、唯一没有异常的空间。循环 = 从实验室出发 → 穿门进异常世界收集记忆核心 → 回实验室收容 → 集齐 6 份触发结局审讯。"));
C.push(body("实现：样本挂 IInteractable 接口（InteractionRegistry 自动发现）；拾取调 PickUp 动画；收容槽用 SciFi MegaKit 模块件搭建；进度用 Canvas 计数。工作量：2 天。"));

C.push(h2("1.2 收容翼扩建"));
C.push(body("用 Modular SciFi MegaKit 的模块化墙件在实验室一角搭出收容翼：一排收容舱（放样本与异常体）、监控台、审讯室门。全部是现有模块件拼装，零建模。"));

C.push(h2("1.3 实验室异变：收容副作用（进度与安全的矛盾）"));
C.push(body("心理恐怖核心设计：收容越多异常体，实验室越「不对劲」。家在一点点变成异常体的一部分，而这是变强的唯一途径。"));
C.push(tableTitle("表 1  异变阶段"));
C.push(buildTable(
  ["收容数", "实验室变化"],
  [
    ["1 件", "某盏灯开始闪烁"],
    ["3 件", "走廊尽头传来敲击声"],
    ["5 件", "收容舱里的东西换了个姿势；墙上照片的拍摄角度变了"],
    ["7 件", "黑雾浓度上升，环境音多出低频嗡鸣"],
    ["6 份集齐", "结局门出现，实验室灯光全灭——只剩收容舱的光。家已经不再是家"],
  ],
  [16, 84]
));
C.push(body("实现：ContainmentDirector 按 collectCount 驱动事件表（灯光参数 / 音频 / 物件状态预设）。工作量：1 天。"));

C.push(h2("1.4 终局：审讯室（单结局）"));
C.push(body("集齐 6 份记忆核心后，审讯室门解锁。神秘声音考问本局见闻（电话接听顺序、电闸哪根线、黑匣子位置）——全部是玩家本局真实经历。答对解锁实验室事故真相并触发结局演出；答错扣 20 电量可重答。实现：ScriptableObject 题库（题目引用本局事件状态）+ 全屏问答 UI + 结局文字演出。工作量：1 天。"));

// ═══ 支柱二：三大世界 ═══
C.push(h1("二、支柱二：三大异常世界（规则与解谜）"));
C.push(body("每个世界一条独特规则构成压迫，一个场景谜题作为解法，通关后规则反转为增益（异常体道具或规则改变，详见支柱四）。三世界素材全部在库。"));
C.push(tableTitle("表 2  三世界总表"));
C.push(buildTable(
  ["世界", "规则（压迫）", "谜题（解法）", "奖励（规则改变 / 道具）"],
  [
    ["乡村", "记忆循环：离开房屋再进，屋内恢复原样，拿走的东西复位", "3 部电话在不同房间响铃，接听顺序的线索藏在旧照片里，按正确顺序接听打破循环", "「时间静止」规则：每局一次冻结世界 3 秒"],
    ["Metro", "渐渐熄灭：灯光逐段熄灭，黑暗中耗电 1.5 倍", "万用表在配电室找出带电的线，合上电闸", "全站复明；解锁尾节车厢（战利品 + 飞船异常体「星际陆战队」出没）"],
    ["工厂", "昼夜轮班：每 2 分钟在 Day / Night 双场景切换；白天机器可见但门锁，夜晚门全开但异常体显形", "值班室找到排班表，按表顺序启动 3 台机器电源开关，锁定想要的昼夜", "「昼夜自选」规则：手动切换（冷却 60 秒）"],
  ],
  [10, 30, 30, 30]
));
C.push(body("备选第四世界：水淹区（Flooded_Grounds 素材在库）——规则为积水中耗电 2 倍，解法为扳手关排水阀。建议作为 P1 扩展，不占主流程。"));
C.push(body("实现：乡村循环 = 房间状态快照 / 恢复（粉笔标记白名单）；Metro 熄灯 = 灯组按时间轴关闭 + 电闸交互；工厂昼夜 = Day/Night 两个根节点组切换（素材库自带双场景）。工作量：每世界 1~1.5 天。"));

// ═══ 支柱三：探索道具 ═══
C.push(h1("三、支柱三：探索道具"));
C.push(h2("3.1 调查仪器 8 件（Exploration objects 模型包，含发光屏贴图）"));
C.push(body("仪器作为机器人外挂传感器，一次手持一件，滚轮切换，持握用 TorchIdle 举臂动画。全部模型现成。"));
C.push(tableTitle("表 3  八件仪器"));
C.push(buildTable(
  ["仪器", "功能", "说明"],
  [
    ["手电筒", "主照明，持续耗电", "开灯能看见，关灯能隐蔽"],
    ["口袋手电", "电量耗尽后的保底弱光", "快没电时的绝望感"],
    ["EMF 探测器", "靠近异常点等级 1 爬到 5（发光屏真实切换）", "滴滴声越来越快的压迫感"],
    ["温度计", "异常点附近读数骤降", "数字变负的那一刻"],
    ["灵异收音盒", "频率层调谐器；杂音中偶有低语", "「电台里有人在说话」"],
    ["热成像相机", "热残留追踪：受害者最后经过的热迹", "冷蓝色人形残影"],
    ["拍照相机", "对异常拍照即证据；闪光后短暂致盲自己；照片即存档缩略图", "致盲一秒里照片上多了什么"],
    ["录音笔", "放在异常点旁录制，回实验室播放", "叙事碎片投放器"],
  ],
  [26, 42, 32]
));
C.push(h2("3.2 场景道具（RandomObjects 模型包）"));
C.push(tableTitle("表 4  场景道具映射"));
C.push(buildTable(
  ["模型", "定位", "说明"],
  [
    ["floppydisk", "记忆核心", "收集品本体；读取后浮现一段实验室事故记录"],
    ["multimeter", "电力检测仪", "Metro 电闸谜题道具"],
    ["wrench", "扳手", "水淹区（备选世界）谜题道具；可修理物件"],
    ["phone", "来电", "接听是机器人自己的声音给出提示（事件触发器）"],
    ["portableHeater", "便携热源", "吸引趋热异常体，或制造安全区"],
    ["bat", "物理工具", "压盖板、捅远处开关（不做挥击伤害）"],
    ["case", "样本箱", "收纳记忆核心（演出用）"],
  ],
  [22, 24, 54]
));
C.push(body("实现：装备框架 = 手持模型挂玩家相机子物体 + 滚轮切换 + TorchIdle 驱动（框架 1 天）；每件仪器独立 MonoBehaviour（各 0.5 天）；道具数据全部 ScriptableObject。"));

// ═══ 支柱四：异常体道具与规则改变 ═══
C.push(h1("四、支柱四：异常体道具与规则改变"));

C.push(h2("4.1 规则池（世界规则型异想，11 条）"));
C.push(body("每局开始随机 1~2 条规则生效（实验室公告屏显示规则文本），也可由世界谜题解锁指定规则。全部肉眼可感知、直接改变玩家操作。"));
C.push(tableTitle("表 5  规则池"));
C.push(buildTable(
  ["规则", "增益", "代价"],
  [
    ["低重力协议", "跳跃高度 2 倍、滞空翻倍", "落地声 2 倍（噪音暴露）；移速 -15%"],
    ["摩擦失效", "完全无声移动", "溜冰惯性：停下要滑 2 米"],
    ["三点支撑", "永不倒地，45 度坡如履平地", "移速 -40%"],
    ["声呐模式", "一切声源显示可见回波（含不该有脚步的东西）", "视野缩到 2 米；自己每步也发波"],
    ["时间稀释", "世界速度 0.7 倍（子弹时间）", "电量消耗 2 倍"],
    ["视觉缓存", "视野 2 倍、黑暗全视、异常体红轮廓", "画面是 2 秒前的世界（延迟视觉）"],
    ["过载保留", "电量归零不瘫倒，进入 60 秒残血爬行", "爬行期间异常体对你兴趣倍增"],
    ["漏电体质", "走近门门自开、走近灯灯自亮", "电量流失 2 倍；噪音与光亮暴露位置"],
    ["匀速世界", "免疫一切加减速效果", "冲刺键消失，永远匀速"],
    ["万物有灵", "所有门与容器不锁", "每开一个，别处随机关一个"],
    ["偷窥许可证", "穿墙透视轮廓常驻", "无法得知异常体何时在看你（提示音消失）"],
  ],
  [18, 40, 42]
));

C.push(h2("4.2 元世界异想体（复用组内《三不猴》已验证的 meta 机制）"));
C.push(body("异常体肉眼可见地出现在游戏世界里，但制服它的解法在屏幕之外——玩家要在现实中做出真实操作。三套判定代码已在《三不猴》项目跑通，可直接移植。"));
C.push(tableTitle("表 6  元世界异想体"));
C.push(buildTable(
  ["异想体", "肉眼可见的表现", "解法（屏幕外真实操作）", "移植的判定"],
  [
    ["「注视者」", "一台悬浮的 EMF 探测器（现有模型），屏幕对着你；你看它时它缓慢逼近，逼到脸上即扣电", "把游戏窗口最小化 2 秒（它失去目标自己退下）", "焦点事件 + 真实时钟结算"],
    ["「审问投影」", "坏掉的机器人投影出：「你看到了什么？告诉我！」随后弹出与系统权限窗一模一样的弹窗：「门后领域」想访问你的摄像头", "点「拒绝」（点「允许」=招供，扣电 30 并被弹回实验室门口）", "假权限弹窗 UI + 选项判定"],
    ["「光标吞噬者」", "一团游走的像素噪点球，靠近时你的鼠标光标会消失（它吃了光标）", "用键盘操作把它引到墙角，光标吐出后可点选收容", "光标锁定 / 显示 API"],
    ["「伪加载」", "突然整屏黑掉，中央出现假「加载中…」转圈——其实游戏还在走：黑暗里那个东西正在挪位置", "按 Esc 立刻退出假加载画面（它被强光暴露回原位）", "全屏遮罩 + Esc 事件"],
  ],
  [14, 32, 30, 24]
));

C.push(h2("4.3 十二舰收容改装厂"));
C.push(body("12 艘飞船异常体全部肉眼可见（会飞、会逃、各有不对劲之处），收容后拆解装进机器人 4 个改装槽（肩 / 眼 / 胸 / 背），外观与能力同步成长。"));
C.push(tableTitle("表 7  十二舰异常体"));
C.push(buildTable(
  ["飞船", "异常行为", "收容增益", "槽位"],
  [
    ["迷彩星号", "僚机跟随，异常体靠近时挡在身前（3 次后坠机）", "派遣侦察：飞行 8 秒返回后显示该方向轮廓", "肩"],
    ["微型侦察机", "主动飞向异常体悬停示警", "小地图异常红点 30 秒", "肩"],
    ["双子 striker", "双机编队；分开超 5 米哀鸣对撞，火花照亮全屋", "指挥双子冲刺撞碎脆弱障碍", "肩"],
    ["红外隐身者", "贴地飞行，掠过处留下肉眼可见的焦痕路径（可循迹找巢）", "焦痕路径高亮", "眼"],
    ["紫外入侵者", "出现与离开瞬间化作紫色流光（肉眼可追踪去向）", "流光路径显示附近异常体方向", "眼"],
    ["流星切割者", "高速绕柱飞行撞落物资", "冲刺速度 +15%", "胸"],
    ["红色战机", "撞角开路：碎障碍、推重物", "撞角冷却减半", "胸"],
    ["星际陆战队", "站岗威吓异常体", "异常体靠近警告提前 2 秒", "背"],
    ["战舰", "悬停掩体遮挡威胁", "受击扣电减半", "背"],
    ["穿越星", "穿墙飞行拾取墙对面物件", "一次穿墙闪现", "胸"],
    ["银河竞速者", "绕圈竞速，跟上它被带到样本旁", "解除冲刺限制", "腿"],
    ["星际奔跑者", "永远绕圈奔跑", "步行速度 +10%", "腿"],
  ],
  [16, 40, 28, 16]
));

C.push(h2("4.4 静态异想体精选（全部肉眼可见）"));
C.push(tableTitle("表 8  静态异想体"));
C.push(buildTable(
  ["名称", "效果", "代价 / 不对劲之处"],
  [
    ["「温柔的椅子」", "坐下充电速度 5 倍", "越来越不想站起来：起身需连按且逐次迟钝。它没有恶意"],
    ["「任务清单」", "完成任务给电量奖励", "任务一条比一条不对劲，最后一条：谢谢你陪我到最后一格电"],
    ["「对面的灯」", "Metro 隧道对面的灯，走近就灭走远就亮", "永远到不了。无机制无收益——试图到达它的二十分钟就是它的机制"],
    ["「174 号班机」", "Plane04 零件悬浮成坠毁前一刻的客机，零件缓慢追随", "须找到位置随机的黑匣子才能收容"],
    ["「门里门」", "迷你门内有亮灯小房间与踱步的迷你机器人，挥手会回应", "收容后可透视任意关着的门，但门后传来轻敲门声"],
    ["「敲击」", "关着的门后三下敲门声，开门空无一物", "敲击后 30 秒内穿门，落点偏移 2 米——它在另一侧推你"],
    ["「回门遥控」", "立刻传回实验室门口", "冷却 3 分钟；本次世界收集物掉一半"],
    ["「夜视模组」", "黑暗全清楚", "见光全白，耗电 3 倍"],
    ["「重力螺丝」", "拧进任意门框，60 秒内无视锁闭穿一次", "拧螺丝 5 秒蹲桩无防备"],
    ["「安全绳」", "系实验室门口，F 顺绳拉回", "绳长 40 米限制探索；解开即放弃保命"],
  ],
  [18, 40, 42]
));

// ═══ 氛围与快赢 ═══
C.push(h1("五、氛围与快赢演出"));
C.push(body("灯光事件（闪烁 / 熄灭 / 追光）与音效层（门嗡嗡声、穿门 whoosh、每世界环境音循环）贯穿所有世界，实现为触发体积 + 参数动画。工作量：1 天。"));
C.push(tableTitle("表 9  快赢提案合集（每件半天内，不拉长流程）"));
C.push(buildTable(
  ["提案", "效果"],
  [
    ["穿门残留痕迹", "穿门后机器人带上那个世界的痕迹（乡村=草屑、Metro=机油渍），下次穿门才更换"],
    ["手电筒电量可视化", "手电亮度随电量衰减：满电刺眼、低电昏黄——不看 UI 也能直觉感到电量"],
    ["收容舱呼吸灯", "每收容一件，对应收容舱亮起一盏呼吸灯——进度可视化，实验室越到后期越「活」"],
    ["门色图例海报", "实验室墙上贴门色图例海报（青=乡村、橙=Metro）——装饰兼新手引导"],
    ["电量账单结算", "通关显示「本局电费账单」：照明、仪器、短路各用了多少——机器人味十足的结算画面"],
    ["声音涟漪 UI", "无声类规则生效时，所有声源以涟漪波纹显示（规则型异想的配套补偿）"],
    ["结尾值班录音", "通关后播放机器人结尾值班录音，呼应开场语音日志（首尾闭环）"],
  ],
  [24, 76]
));

// ═══ 实现方式 ═══
C.push(h1("六、实现方式汇总"));
C.push(tableTitle("表 10  关键实现对照"));
C.push(buildTable(
  ["提案", "实现要点", "依赖素材", "工作量"],
  [
    ["核心循环", "样本挂 IInteractable；拾取调 PickUp 动画；收容槽 = MegaKit 模块件 + 触发交互点；进度 Canvas 计数", "SciFi MegaKit（库内）", "2 天"],
    ["世界规则", "乡村循环 = 房间状态快照 / 恢复（粉笔位白名单）；Metro 熄灯 = 灯组按时间轴关闭 + 电闸交互；工厂昼夜 = Day/Night 根节点组切换", "Factory 双场景、country house（库内）", "每世界 1~1.5 天"],
    ["十二舰改装厂", "跟随 = Lerp 至肩侧锚点 + 正弦浮动；收容 = 收容槽交互点；改装 = 槽位字典 + 挂点实例化飞船模型（缩放 0.05）", "spaceships 12 艘（已有）", "首批 2 天，后续每艘 0.5 天"],
    ["调查仪器", "手持模型挂玩家相机子物体；每件一个 MonoBehaviour（EMF 距离换发光屏、温度计换读数、收音盒联动频率层）", "Exploration objects 8 件（已有）", "框架 1 天 + 每件 0.5 天"],
    ["元世界异想体", "移植《三不猴》判定：焦点事件+真实时钟（最小化）、假权限弹窗 UI+选项判定；光标吞噬者=Cursor API；伪加载=全屏遮罩+Esc 事件", "Mobile 项目三不猴判定代码（可直接移植）", "移植两件 1 天，新设计各 0.5 天"],
    ["道具异常化", "全部 ScriptableObject 数据驱动；异想体 = 触发器 + UI 把戏或区域逻辑，无 AI", "RandomObjects（已有）", "1 天"],
    ["氛围系统", "灯光/音频参数动画 + 触发体积", "CC0 音效库", "1 天"],
    ["拍照存档", "截帧 + 滤镜渲到纹理；照片文件即存档缩略图；软木板照片墙实例化", "—", "2 天"],
    ["实验室异变", "ContainmentDirector 按收集数驱动事件表（灯光 / 音频 / 物件状态预设）", "—", "1 天"],
    ["审讯室结局", "ScriptableObject 题库（题目引用本局真实事件状态）+ 全屏问答 UI + 结局文字演出", "interrogation 模型（已有）", "1 天"],
  ],
  [14, 40, 24, 22]
));

// ═══ 待决策 ═══
C.push(h1("七、待小组决策"));
C.push(tableTitle("表 11  决策清单"));
C.push(buildTable(
  ["#", "决策点", "选项", "推荐"],
  [
    ["1", "第三世界选择", "工厂（昼夜轮班）/ 水淹区（短路）/ 两者都要", "工厂：Day/Night 双场景素材现成"],
    ["2", "手电筒形态", "手持模型 / 纯光源", "手持：TorchIdle 动画现成，多半天"],
    ["3", "元世界异想体范围", "四件全做 / 只做「注视者」试水 / 不做", "先做「注视者」试水：判定已在三不猴跑通"],
    ["4", "规则池生效方式", "每局随机 1~2 条 / 谜题解锁指定规则 / 两者结合", "两者结合：随机保证重复可玩性，谜题解锁给目标感"],
    ["5", "拍照存档", "本期做 / 下期做", "本期做：照片即存档兼具功能与叙事"],
    ["6", "样本数量", "每世界 2 份（共 6）/ 每世界 3 份（共 9）", "每世界 2 份：控制流程长度"],
  ],
  [6, 22, 40, 32]
));
C.push(body("所有提案相互独立，砍掉任意一条不影响其余提案运行。工作量为程序侧人日估算（素材已在库前提），文案类工作另行分工。", { after: 200 }));

const doc = new Document({
  styles: { default: { document: {
    run: { font: { ascii: "Arial", eastAsia: "Microsoft YaHei" }, size: 24, color: P.body },
    paragraph: { spacing: { line: 312 } },
  }}},
  features: { updateFields: true },
  sections: [
    { properties: {
        page: { margin: { top: 1440, bottom: 1440, left: 1701, right: 1417 },
                pageNumbers: { start: 1, formatType: "decimal" } } },
      headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT,
        children: [new TextRun({ text: "《门后领域》内容扩展提案", size: 16, color: "A0A0A0" })] })] }) },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
        children: [new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "808080" })] })] }) },
      children: C },
  ],
});

Packer.toBuffer(doc).then(buf => {
  const out = "C:/Users/xiaoming/Downloads/FYP/Docs/门后领域_内容扩展提案.docx";
  fs.writeFileSync(out, buf);
  console.log("written:", out, buf.length, "bytes");
});
