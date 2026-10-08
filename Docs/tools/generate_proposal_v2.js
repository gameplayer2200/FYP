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
function body(text, opts) {
  const o = opts || {};
  return new Paragraph({
    alignment: AlignmentType.JUSTIFIED, indent: { firstLine: o.noIndent ? 0 : 420 },
    spacing: { line: 312, after: o.after || 60 },
    children: [new TextRun({ text, size: 24, color: P.body })],
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

// ── 一、核心循环 ──
C.push(h1("一、核心循环（提案零 · 骨架）"));
C.push(body("调查员机器人穿过彩色门进入异常世界，在电量耗尽前定位并收集异常样本（记忆核心），带回实验室放入收容槽；收容数量推进全局进度，集齐后实验室出现结局门，穿过进入终局审讯。"));
C.push(body("现状：双向传送、门开关同步、关门禁止传送、多世界多实例架构、E 键交互、拾取动画全部已就绪，本提案是纯内容填充。所有其他提案都挂在这个骨架上。"));
C.push(body("工作量：2 天。"));

// ── 二、三层视界 ──
C.push(h1("二、三层视界系统（提案一）"));
C.push(body("世界有三层现实，玩家可切换视界探索："));
C.push(tableTitle("表 1  三层视界"));
C.push(buildTable(
  ["视界", "进入方式", "能看到什么"],
  [
    ["肉眼层", "默认", "普通世界；大部分异常不可见"],
    ["相机层", "举起拍照相机（画面变 GameBoy 像素滤镜）", "隐藏的门、墙上的字、肉眼看不见的人形——只在取景框里存在"],
    ["频率层", "灵异收音盒调频（如 88.0 / 92.5）", "同一世界的不同状态：Metro 88.0 是停电废弃的现在，92.5 是灯亮广播正常的过去"],
  ],
  [12, 34, 54]
));
C.push(body("恐怖点：切换视界的瞬间，画面里多出了刚才没有的东西。解谜语言统一：每个世界的谜题都绑定某一层视界。"));

// ── 三、世界规则 ──
C.push(h1("三、世界规则与解谜（提案二）"));
C.push(body("每个世界一条独特规则构成压迫，一个场景内谜题作为解法，通关后规则反转成增益。素材全部在库："));
C.push(tableTitle("表 2  世界规则总表"));
C.push(buildTable(
  ["世界", "规则（压迫）", "谜题（解法）", "奖励（反转）"],
  [
    ["乡村", "记忆循环：离开房屋再进，屋内恢复原样，拿走的东西复位", "3 座挂钟时间错误，线索在墓碑与旧照片，拨对全部挂钟打破循环", "「时间静止」：每局一次冻结世界 3 秒"],
    ["Metro", "渐渐熄灭：灯光逐段熄灭，黑暗中耗电 1.5 倍", "万用表在配电室找出带电的线，合上电闸", "全站复明；解锁尾节车厢战利品区"],
    ["工厂", "昼夜轮班：每 2 分钟在 Day / Night 双场景切换；白天门锁，夜晚异常体显形", "找排班表调整工厂时钟，锁定想要的昼夜", "「昼夜自选」手动切换（冷却 60 秒）"],
    ["水淹区", "短路：积水中耗电 2 倍加随机抖屏", "扳手关 3 个排水阀（阀门藏在建筑内）", "水位下降露下层区；「防水涂层」"],
    ["审讯室", "终局：神秘声音考本局见闻（钟拨了什么时间、电闸哪根线）", "答对解锁实验室事故真相；答错扣 20 电", "结局触发权限"],
  ],
  [10, 30, 30, 30]
));

// ── 四、十二舰 ──
C.push(h1("四、十二舰收容改装厂（提案三）"));
C.push(body("spaceships 包 12 艘飞船全部设定为异常体生物：会飞、会逃、各有一处不对劲。收容后拆解装进机器人 4 个改装槽（肩 / 眼 / 胸 / 背），外观与能力同步成长。收容过程即小关卡。"));
C.push(tableTitle("表 3  十二舰异常体"));
C.push(buildTable(
  ["飞船", "异常行为", "收容增益", "槽位"],
  [
    ["迷彩星号", "僚机跟随，异常体靠近时挡在身前（3 次后坠机）", "派遣侦察：飞行 8 秒返回后显示该方向轮廓", "肩"],
    ["微型侦察机", "主动飞向异常体悬停示警", "小地图异常红点 30 秒", "肩"],
    ["双子 striker", "双机编队；分开超 5 米哀鸣对撞，火花照亮全屋", "指挥双子冲刺撞碎脆弱障碍", "肩"],
    ["红外隐身者", "只在热成像视界可见", "热成像视界常驻", "眼"],
    ["紫外入侵者", "只在相机层可见", "相机层视界免举相机", "眼"],
    ["流星切割者", "高速绕柱飞行撞落物资", "冲刺速度 +15%", "胸"],
    ["红色战机", "撞角开路：碎障碍、推重物", "撞角冷却减半", "胸"],
    ["星际陆战队", "站岗威吓异常体", "黑影接近警告提前 2 秒", "背"],
    ["战舰", "悬停掩体遮挡黑影视线", "受击扣电减半", "背"],
    ["穿越星", "穿墙飞行拾取墙对面物件", "一次穿墙闪现", "胸"],
    ["银河竞速者", "绕圈竞速，跟上它被带到样本旁", "解除冲刺限制", "腿"],
    ["星际奔跑者", "永远绕圈奔跑", "步行速度 +10%", "腿"],
  ],
  [18, 40, 32, 10]
));

// ── 五、调查仪器 ──
C.push(h1("五、调查仪器套装（提案四）"));
C.push(body("Exploration objects 包 8 件模型（含多级发光屏贴图），作为机器人外挂传感器，一次手持一件，滚轮切换，持握用 TorchIdle 举臂动画。"));
C.push(tableTitle("表 4  八件仪器"));
C.push(buildTable(
  ["仪器", "功能", "恐怖点"],
  [
    ["手电筒", "主照明，持续耗电", "开灯能看见，关灯能隐蔽"],
    ["口袋手电", "电量耗尽后的保底弱光", "快没电时的绝望感"],
    ["EMF 探测器", "靠近异常点等级 1 爬到 5（屏幕发光贴图真实切换）", "滴滴声越来越快"],
    ["温度计", "异常点附近读数骤降", "数字变负的那一刻"],
    ["灵异收音盒", "频率层调谐器；杂音中偶有低语", "「电台里有人在说话」"],
    ["热成像相机", "热残留追踪：受害者最后经过的热迹", "冷蓝色人形残影"],
    ["拍照相机", "相机层入口；拍异常即证据；闪光后短暂致盲自己", "致盲一秒里照片上多了什么"],
    ["录音笔", "放在异常点旁录制，回实验室播放", "叙事碎片投放器"],
  ],
  [24, 42, 34]
));

// ── 六、道具异常化 ──
C.push(h1("六、场景道具异常化（提案五）"));
C.push(tableTitle("表 5  RandomObjects 道具映射"));
C.push(buildTable(
  ["模型", "定位", "说明"],
  [
    ["floppydisk", "记忆核心", "读取后浮现一段实验室事故记录（收集品）"],
    ["multimeter", "电力检测仪", "测充电桩 / 电线是否有电（Metro 谜题道具）"],
    ["wrench", "扳手", "水淹区排水阀谜题道具；可修理物件"],
    ["phone", "来电", "接听是机器人自己的声音给出提示（事件触发器）"],
    ["portableHeater", "便携热源", "放下后吸引趋热异常体，或制造安全区"],
    ["bat", "物理工具", "压盖板、捅远处开关（不做挥击伤害）"],
    ["case", "样本箱", "收纳采集样本（演出用）"],
  ],
  [22, 24, 54]
));
C.push(tableTitle("表 6  静态异想体精选"));
C.push(buildTable(
  ["名称", "效果", "代价 / 不对劲之处"],
  [
    ["「温柔的椅子」", "坐下充电速度 5 倍", "越来越不想站起来：起身需连按且逐次迟钝。它没有恶意"],
    ["「任务清单」", "完成任务给电量奖励", "任务一条比一条不对劲，最后一条：谢谢你陪我到最后一格电"],
    ["「对面的灯」", "Metro 隧道对面的灯，走近就灭走远就亮", "永远到不了。无机制无收益——试图到达它的二十分钟就是它的机制"],
    ["「174 号班机」", "Plane04 零件悬浮成坠毁前一刻的客机，零件会缓慢追随", "须找到位置随机的黑匣子才能收容"],
    ["「门里门」", "迷你门内有亮灯小房间和踱步的迷你机器人，挥手会回应", "收容后可透视任意关着的门，但门后传来轻敲门声"],
    ["「敲击」", "关着的门后三下敲门声，开门空无一物", "敲击后 30 秒内穿门，落点偏移 2 米——它在另一侧推你"],
    ["「回门遥控」", "立刻传回实验室门口", "冷却 3 分钟；本次世界收集物掉一半"],
    ["「夜视模组」", "黑暗全清楚", "见光全白，耗电 3 倍"],
    ["「重力螺丝」", "拧进任意门框，60 秒内无视锁闭穿一次", "拧螺丝 5 秒蹲桩无防备"],
    ["「安全绳」", "系实验室门口，F 顺绳拉回", "绳长 40 米限制探索；解开即放弃保命"],
  ],
  [18, 40, 42]
));

// ── 七、规则型异想 ──
C.push(h1("七、世界规则型异想（提案六）"));
C.push(body("不改道具，改玩家本身的规则：每局开始随机 1~2 条生效（实验室公告屏显示规则文本），也可由世界谜题解锁指定规则。"));
C.push(tableTitle("表 7  规则池"));
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
    ["匀速世界", "免疫一切加减速效果（陷阱、冰面、硬直）", "冲刺键消失，永远匀速"],
    ["万物有灵", "所有门与容器不锁", "每开一个，别处随机关一个"],
    ["偷窥许可证", "穿墙透视轮廓常驻", "无法得知异常体何时在看你（提示音消失）"],
  ],
  [18, 40, 42]
));

// ── 八、氛围 ──
C.push(h1("八、无战斗恐怖氛围（提案七）"));
C.push(body("设计立场：无攻击无对抗，恐怖全部来自资源压力（电量）、信息压力（视界）与声光氛围。三个组成："));
C.push(tableTitle("表 8  氛围组成"));
C.push(buildTable(
  ["组成", "内容", "实现"],
  [
    ["灯光事件", "灯管闪烁、整段熄灭、突然亮起的追光", "Light + 脚本时间轴（与 Metro 渐熄规则复用）"],
    ["音效层", "门嗡嗡声随距离渐强、穿门 whoosh、每世界环境音循环、异常体低语 stinger", "触发体积 + AudioSource，CC0 素材库"],
    ["黑影实体", "MCO 包 Ninja 动画贴纯黑材质，只接近不攻击：靠近时屏幕边缘渐暗、电量加速流失；摆脱靠拉距离 / 遮视线 / 诱饵信标；被触碰即 Hit 扣电弹回实验室门口", "NavMeshAgent 巡游 + 距离驱动屏幕渐晕"],
  ],
  [14, 46, 40]
));

// ── 九、门与机器人叙事彩蛋 ──
C.push(h1("九、叙事彩蛋（提案八 · 低成本高记忆点）"));
C.push(tableTitle("表 9  叙事彩蛋"));
C.push(buildTable(
  ["彩蛋", "内容"],
  [
    ["死亡即重装", "机器人损毁后实验室重新激活一台，屏幕花屏 + 一行字「记忆已从云端恢复」——天然解释无限重试，零剧情成本"],
    ["「备份的我」芯片", "当场复活一次，但复活的你没有最后 10 分钟记忆，它会问：刚才发生了什么"],
    ["「来电」", "接听是自己的声音说：别去 92.5（频率层联动）"],
    ["「门里门」", "20 厘米迷你门内有亮灯小房间与踱步的迷你机器人，对它挥手它会挥手回应"],
    ["「对面的灯」", "Metro 隧道对面的灯，走近就灭走远就亮，永远到不了——试图到达它的二十分钟就是它的机制"],
  ],
  [22, 78]
));

// ── 十、实现方式 ──
C.push(h1("十、实现方式汇总"));
C.push(tableTitle("表 10  关键实现对照"));
C.push(buildTable(
  ["提案", "实现要点", "依赖素材", "工作量"],
  [
    ["核心循环", "样本挂 IInteractable（自动被发现）；拾取调 PickUp 动画；收容槽 = MegaKit 模块件 + 触发交互点；计数 UI + 隐藏结局门", "SciFi MegaKit（库内）", "2 天"],
    ["三层视界", "相机层 = RogueNoodle GBCamera shader 挂子相机 + 专属 Layer；频率层 = 场景双状态根节点组切换 + Spirit_Box 发光屏换贴图显示频率；配置用 ScriptableObject", "GBCamera shader、Spirit_Box（均已有）", "3 天"],
    ["世界规则", "乡村循环 = 房间状态快照 / 恢复（粉笔位白名单）；Metro 熄灯 = 灯组按时间轴关闭 + 电闸交互；工厂昼夜 = Day/Night 两个根节点组切换；水淹 = 水体位移 + 触发体积改电量参数", "Factory 双场景、Flooded_Grounds（库内）", "每世界 1~1.5 天"],
    ["十二舰改装厂", "跟随 = Lerp 至肩侧锚点 + 正弦浮动；收容 = 收容槽交互点；改装 = 槽位字典 + 挂点实例化飞船模型（缩放 0.05）", "spaceships 12 艘（已有）", "首批 2 天，后续每艘 0.5 天"],
    ["调查仪器", "手持模型挂玩家相机子物体；每件一个 MonoBehaviour（EMF 距离换屏、温度计换读数、收音盒联动频率层、拍照截图套 GBCamera 滤镜）", "Exploration objects 8 件（已有）", "框架 1 天 + 每件 0.5 天"],
    ["道具异常化", "全部 ScriptableObject 数据驱动；异想体 = 触发器 + UI 把戏或区域逻辑，无 AI", "RandomObjects（已有）", "1 天"],
    ["氛围系统", "灯光/音频参数动画 + 触发体积；黑影 = NavMeshAgent 巡游 + 距离驱动屏幕渐晕", "MCO 动画（已有）", "1.5 天"],
    ["审讯室结局", "ScriptableObject 题库（题目引用本局真实事件状态）+ 全屏问答 UI + 结局文字演出", "interrogation 模型（已有）", "1 天"],
  ],
  [14, 40, 26, 20]
));

// ── 十一、待决策 ──
C.push(h1("十一、待小组决策"));
C.push(tableTitle("表 11  决策清单"));
C.push(buildTable(
  ["#", "决策点", "选项", "推荐"],
  [
    ["1", "核心循环确认", "收集—收容—结局 / 逃出去型", "收集循环：与视界、改装全部咬合"],
    ["2", "P0 世界", "乡村 + Metro / 加工厂 / 加水淹区", "乡村 + Metro：场景已接入传送门"],
    ["3", "手电筒形态", "手持模型 / 纯光源", "手持：TorchIdle 动画现成，多半天"],
    ["4", "GBCamera 滤镜", "仅照片 / 整个取景画面", "仅照片：全屏易疲劳，照片里出现更有冲击"],
    ["5", "黑影实体", "本期 / 下期 / 不做", "下期：P0 先用声光事件压氛围"],
    ["6", "审讯室问答", "固定题库 / 三选一分支", "三选一分支：防背板，顺便搭叙事框架"],
  ],
  [6, 22, 38, 34]
));
C.push(tableTitle("表 12  排期建议（4 人团队）"));
C.push(buildTable(
  ["阶段", "内容", "预估"],
  [
    ["P0 · 第 1 周", "核心循环 + 仪器三件套（手电/EMF/拍照）+ 乡村与 Metro 谜题 + 挡板配色", "约 7 人日"],
    ["P1 · 第 2 周", "工厂与水淹区 + 改装厂首批 3 艘 + 氛围系统 + 剩余仪器", "约 7 人日"],
    ["P2 · 第 3 周", "审讯室 + 结局 + 剩余异常体 + 打磨音效", "约 6 人日"],
  ],
  [18, 62, 20]
));
C.push(body("以上工作量按「素材已在库、系统已验证」估算。所有提案相互独立，砍掉任意一条不影响其余提案运行。", { after: 200 }));

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
