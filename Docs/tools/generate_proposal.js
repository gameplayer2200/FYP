const {
  Document, Packer, Paragraph, TextRun, Header, Footer, Table, TableRow, TableCell,
  AlignmentType, HeadingLevel, PageNumber, WidthType, TableLayoutType, BorderStyle,
  ShadingType, VerticalAlign, TableOfContents, SectionType
} = require("docx");
const fs = require("fs");

// GO-1 palette (proposal)
const P = {
  bg: "1A2330", primary: "FFFFFF", accent: "D4875A",
  cover: { titleColor: "FFFFFF", subtitleColor: "B0B8C0", metaColor: "90989F", footerColor: "687078" },
  table: { headerBg: "D4875A", headerText: "FFFFFF", accentLine: "D4875A", innerLine: "DDD0C8", surface: "F8F0EB" },
  body: "182030", secondary: "506070",
};

const NB = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };
const noBorders = { top: NB, bottom: NB, left: NB, right: NB };
const allNoBorders = { top: NB, bottom: NB, left: NB, right: NB, insideHorizontal: NB, insideVertical: NB };
const emptyPara = () => new Paragraph({ children: [] });

function splitTitleLines(title, charsPerLine) {
  if (title.length <= charsPerLine) return [title];
  const breakAfter = new Set([":", ",", ";", "、", ")", "》", "」", "】", "的", "与", "和", "及", "之", "在", "于", "为", "-", "_", "—", "–", "·", "/", " ", "\t"]);
  const lines = []; let remaining = title;
  while (remaining.length > charsPerLine) {
    let breakAt = -1;
    for (let i = charsPerLine; i >= Math.floor(charsPerLine * 0.6); i--) {
      if (i < remaining.length && breakAfter.has(remaining[i - 1])) { breakAt = i; break; }
    }
    if (breakAt === -1) {
      const limit = Math.min(remaining.length, Math.ceil(charsPerLine * 1.3));
      for (let i = charsPerLine + 1; i < limit; i++) { if (breakAfter.has(remaining[i - 1])) { breakAt = i; break; } }
    }
    if (breakAt === -1) {
      breakAt = charsPerLine;
      const prevChar = remaining[breakAt - 1], nextChar = remaining[breakAt];
      if (prevChar && nextChar && !breakAfter.has(prevChar) && !breakAfter.has(nextChar) &&
          /[\u4e00-\u9fff]/.test(prevChar) && /[\u4e00-\u9fff]/.test(nextChar)) breakAt = Math.max(1, breakAt - 1);
    }
    lines.push(remaining.slice(0, breakAt).trim());
    remaining = remaining.slice(breakAt).trim();
  }
  if (remaining) lines.push(remaining);
  if (lines.length > 1 && lines[lines.length - 1].length === 1) {
    lines[lines.length - 2] += lines.pop();
  }
  return lines;
}
function calcTitleLayout(title, maxWidthTwips, preferredPt = 40, minPt = 24) {
  const charsPerLine = (pt) => Math.floor(maxWidthTwips / (pt * 20));
  let titlePt = preferredPt, lines;
  while (titlePt >= minPt) {
    const cpl = charsPerLine(titlePt);
    if (cpl < 2) { titlePt -= 2; continue; }
    lines = splitTitleLines(title, cpl);
    if (lines.length <= 3) break;
    titlePt -= 2;
  }
  if (!lines || lines.length > 3) { lines = splitTitleLines(title, charsPerLine(minPt)); titlePt = minPt; }
  return { titlePt, titleLines: lines };
}

function buildCoverR4(config) {
  const padL = 1200, padR = 800;
  const availableWidth = 11906 - padL - padR;
  const { titlePt, titleLines } = calcTitleLayout(config.title, availableWidth, 40, 26);
  const titleSize = titlePt * 2;
  const titleBlockHeight = titleLines.length * (titlePt * 23 + 200);
  const englishLabelH = config.englishLabel ? (9 * 23 + 500) : 0;
  const subtitleH = config.subtitle ? (12 * 23 + 200) : 0;
  const upperContentH = englishLabelH + titleBlockHeight + subtitleH;
  const UPPER_MIN = 7500;
  const UPPER_H = Math.max(UPPER_MIN, upperContentH + 1500 + 800);
  const DIVIDER_H = 60;
  const contentEstimate = englishLabelH + titleLines.length * (titlePt * 23 + 200) + subtitleH;
  const spacerIntrinsic = 280;
  const topSpacing = Math.max(UPPER_H - contentEstimate - spacerIntrinsic - 800, 400);

  const upperBlock = new Table({
    width: { size: 100, type: WidthType.PERCENTAGE }, layout: TableLayoutType.FIXED, borders: allNoBorders,
    rows: [new TableRow({
      height: { value: UPPER_H, rule: "exact" },
      children: [new TableCell({
        shading: { fill: P.bg, type: ShadingType.CLEAR }, borders: noBorders, verticalAlign: VerticalAlign.TOP,
        margins: { left: padL, right: padR },
        children: [
          new Paragraph({ spacing: { before: topSpacing } }),
          config.englishLabel ? new Paragraph({ spacing: { after: 500 },
            children: [new TextRun({ text: config.englishLabel.split("").join(" "), size: 18, color: P.accent, font: { ascii: "Calibri" } })] }) : null,
          ...titleLines.map((line, i) => new Paragraph({
            spacing: { after: i < titleLines.length - 1 ? 100 : 200 },
            children: [new TextRun({ text: line, size: titleSize, bold: true, color: P.cover.titleColor, font: { eastAsia: "SimHei", ascii: "Arial" } })],
          })),
          config.subtitle ? new Paragraph({ spacing: { after: 100 },
            children: [new TextRun({ text: config.subtitle, size: 24, color: P.cover.subtitleColor, font: { eastAsia: "Microsoft YaHei", ascii: "Arial" } })] }) : null,
        ].filter(Boolean),
      })],
    })],
  });
  const divider = new Table({
    width: { size: 100, type: WidthType.PERCENTAGE }, borders: allNoBorders,
    rows: [new TableRow({ height: { value: DIVIDER_H, rule: "exact" },
      children: [new TableCell({ borders: noBorders, shading: { fill: P.accent, type: ShadingType.CLEAR }, children: [emptyPara()] })] })],
  });
  const lowerContent = [
    new Paragraph({ spacing: { before: 800 } }),
    ...(config.metaLines || []).map(line => new Paragraph({
      indent: { left: padL }, spacing: { after: 100 },
      children: [new TextRun({ text: line, size: 28, color: P.cover.metaColor, font: { eastAsia: "Microsoft YaHei", ascii: "Arial" } })],
    })),
    new Paragraph({ spacing: { before: 2000 } }),
    new Paragraph({ indent: { left: padL }, children: [
      new TextRun({ text: config.footerLeft || "", size: 22, color: P.cover.footerColor }),
      new TextRun({ text: "          " }),
      new TextRun({ text: config.footerRight || "", size: 22, color: P.cover.footerColor }),
    ] }),
  ];
  return [new Table({
    width: { size: 100, type: WidthType.PERCENTAGE }, layout: TableLayoutType.FIXED, borders: allNoBorders,
    rows: [new TableRow({ height: { value: 16838, rule: "exact" },
      children: [new TableCell({ shading: { fill: "FFFFFF", type: ShadingType.CLEAR }, borders: noBorders, verticalAlign: VerticalAlign.TOP,
        children: [upperBlock, divider, ...lowerContent] })] })],
  })];
}

function h1(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_1, spacing: { before: 360, after: 140 }, children: [
    new TextRun({ text, bold: true, color: P.body, font: { ascii: "Arial", eastAsia: "SimHei" }, size: 32 })] });
}
function h2(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_2, spacing: { before: 260, after: 110 }, children: [
    new TextRun({ text, bold: true, color: P.body, font: { ascii: "Arial", eastAsia: "SimHei" }, size: 28 })] });
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
  return new Paragraph({ keepNext: true, spacing: { before: 160, after: 80 }, alignment: AlignmentType.CENTER,
    children: [new TextRun({ text, bold: true, size: 22, color: P.secondary })] });
}

const bodyChildren = [];

bodyChildren.push(h1("一、提案总览"));
bodyChildren.push(body("本文档汇总《门后领域》下一阶段的内容扩展创意，全部方案基于项目现有资产与系统设计（传送门多实例架构、电量系统、交互登记表、UAL1 动画库、已购场景与模型包），每一项附实现方式与工作量估算，供小组讨论取舍。"));
bodyChildren.push(tableTitle("表 1  提案速览"));
bodyChildren.push(buildTable(
  ["编号", "提案", "一句话概述", "工作量", "优先级"],
  [
    ["零", "核心循环", "收集异常样本—回实验室收容—解锁结局，串起所有玩法", "2 人日", "P0"],
    ["一", "三层视界系统", "肉眼 / 相机 / 频率三层现实，道具是切换视界的方式", "3 人日", "P0"],
    ["二", "六世界规则与解谜", "每个世界一条独特规则加一个谜题，通关反转成增益", "每世界 1~1.5 天", "P0 / P1"],
    ["三", "十二舰收容改装厂", "飞船异常体收容后拆解，改装机器人外观与能力", "首批 2 天", "P1"],
    ["四", "调查仪器套装", "八件现成模型的探测器，恐鬼症式调查体验", "框架 1 天", "P0"],
    ["五", "场景道具异常化", "RandomObjects 全包映射为剧情道具与小型异想体", "1 天", "P1"],
    ["六", "无战斗恐怖氛围", "灯光事件、音效层、黑影接近施压，零战斗零 AI", "1.5 天", "P1"],
    ["七", "审讯室与结局", "终局问答考本局见闻，答对解锁结局真相", "1 天", "P2"],
  ],
  [8, 20, 40, 16, 16]
));
bodyChildren.push(body("所有提案默认遵循三条已确认的设计约束：玩家是 AI 机器人（道具全部为机器逻辑，不出现饮食类行为）；无战斗功能（威胁来自资源压力与氛围）；传送门采用彩色挡板方案（放弃无缝渲染）。「」内为道具或规则名。", { after: 120 }));

bodyChildren.push(h1("二、核心循环（提案零）"));
bodyChildren.push(bodyLead("创意描述：", "调查员机器人穿过彩色门进入异常世界，在电量耗尽前定位并收集异常样本（记忆核心），带回实验室放入收容槽；收容数量推进全局进度，集齐后实验室出现结局门，穿过即进入终局审讯（见提案七）。"));
bodyChildren.push(bodyLead("已具备基础：", "双向传送、门开关同步、关门禁止传送、多实例多世界架构、InteractionRegistry 交互分发、PickUp 拾取动画——本提案不含任何新架构，全部是内容填充。"));
bodyChildren.push(bodyLead("实现方式：", "（1）样本物件 = 挂 IInteractable 接口的预制体，InteractionRegistry 自动发现，无需新增交互代码；（2）拾取演出 = PlayerAnimatorProxy 调 PickUp 动画（现成）；（3）收容槽 = 用 SciFi MegaKit 模块化墙件在实验室搭收容翼，4 个槽位各配一个触发交互点；（4）计数与结局 = Canvas 文本显示收集进度，集齐后激活隐藏的结局门。"));
bodyChildren.push(bodyLead("工作量：", "2 人日。风险：低——全部由已验证系统组合。"));

bodyChildren.push(h1("三、提案一：三层视界系统"));
bodyChildren.push(bodyLead("创意描述：", "世界存在三层现实。肉眼层：默认可见的普通世界。相机层：一部分隐藏的门、墙上的文字、不存在的人形，只在举起相机取景时可见，画面套用素材库自带的 GameBoy 像素滤镜，天然区分「现实」与「记录」。频率层：用灵异收音盒调频切换同一个世界的状态——Metro 在 88.0 频率是停电废弃的现在，调到 92.5 则是灯光全亮、广播正常、门都开着的过去。"));
bodyChildren.push(bodyLead("玩法效果：", "探索的主循环变成轮换三层视界寻找线索；每个世界的谜题绑定其中一层；恐怖感来自「切换视界的瞬间，画面里多出了刚才没有的东西」。"));
bodyChildren.push(bodyLead("实现方式：", "（1）相机层：RogueNoodle GBCamera 后处理 shader（素材库自带）挂到一台只渲染「相机层物体」的子相机，隐藏物统一放入专属 Layer；举相机动作复用 TorchIdle 举臂动画。（2）频率层：场景双状态 = 同一空间的两组物体集合加两套灯光参数，Spirit_Box 模型的发光屏（自带 Emissor 贴图）用脚本替换贴图显示当前频率刻度，调到阈值即切换两组物体的激活态。（3）频段与层内物体清单用 ScriptableObject 配置，做新关卡不改代码。"));
bodyChildren.push(bodyLead("工作量：", "3 人日。依赖：GBCamera shader（素材库 RogueNoodle 已含）、Spirit_Box 模型（已有）、双状态场景布置（随提案二各世界一并完成）。"));

bodyChildren.push(h1("四、提案二：六大世界规则与解谜"));
bodyChildren.push(body("每个世界一条独特规则构成压迫感，一个场景内谜题构成解法，通关后规则反转为可复用的增益。六个世界的素材全部来自 Material_Scence 素材库，无需外购。"));
bodyChildren.push(tableTitle("表 2  世界规则、谜题与奖励总表"));
bodyChildren.push(buildTable(
  ["世界", "世界规则（压迫）", "场景谜题（解法）", "通关奖励"],
  [
    ["乡村", "记忆循环：离开房屋再进，屋内恢复原样（拿走的东西复位），只有粉笔标记的位置例外", "屋内 3 座挂钟时间错误，线索藏在墓碑与旧照片，按线索拨对全部挂钟即打破循环", "「时间静止」：每局一次冻结世界 3 秒"],
    ["Metro", "渐渐熄灭：进站后灯光逐段熄灭（每 90 秒一段），黑暗中电量消耗 1.5 倍", "配电室用万用表测出 3 根线中带电的一根，合上电闸", "全站复明；解锁尾节车厢战利品区"],
    ["工厂", "昼夜轮班：每 2 分钟在 Day 与 Night 双场景间切换；白天机器可见但门锁，夜晚门全开但异常体显形", "值班室找到排班表，按表调整工厂时钟，把世界锁定在想要的状态", "「昼夜自选」：手动切换，冷却 60 秒"],
    ["水淹区", "短路：积水中电量消耗 2 倍并随机短路抖屏", "用扳手关 3 个排水阀（阀门藏在建筑物内）", "水位下降露出下层区；「防水涂层」"],
    ["审讯室", "终局审讯：神秘声音考问本局亲眼见闻", "问答全部来自玩家本局真实经历", "结局触发权限"],
    ["实验室", "无规则（安全屋）；用 SciFi MegaKit 扩建收容翼", "收容槽与结局门的实体搭建", "—"],
  ],
  [10, 30, 30, 30]
));
bodyChildren.push(bodyLead("实现方式：", "乡村循环 = 房间状态快照（进入时保存、离开时恢复，粉笔标记位置加入白名单）；Metro 熄灯 = 灯组分组脚本按时间轴依次关闭，配上电闸交互；工厂昼夜 = 场景内 Day 与 Night 两个根节点组切换激活（素材库自带双场景内容）；水淹 = 水体 Mesh 位移加触发体积改电量参数；审讯问答 = ScriptableObject 题库加全屏问答 UI。"));
bodyChildren.push(bodyLead("工作量：", "每世界 1~1.5 人日；建议首期只做乡村与 Metro（场景已接入传送门），工厂与水淹区随后（素材在库、需先搭建可玩区域）。"));

bodyChildren.push(h1("五、提案三：十二舰收容改装厂"));
bodyChildren.push(body("spaceships 模型包共 12 艘飞船，全部设定为异常体生物：它们会飞、会逃、有各自的「不对劲之处」。找到后收容，拆解部件装进机器人胸口的 4 个改装槽（肩、眼、胸、背），外观与能力同时可见成长。收容过程即小关卡：飞船会绕柱飞行、只在特定视界可见、或被追上时发出警报。"));
bodyChildren.push(tableTitle("表 3  十二舰异常体一览"));
bodyChildren.push(buildTable(
  ["飞船", "异常行为", "搬运惩罚", "收容增益", "槽位"],
  [
    ["迷彩星号", "僚机跟随，异常体靠近时挡在玩家身前（挡 3 次后坠机冷却）", "引擎声暴露位置", "派遣侦察：飞向指定方向 8 秒后返回，显示该方向轮廓", "肩"],
    ["微型侦察机", "主动飞向异常体悬停示警", "机身反光", "小地图异常红点显示 30 秒", "肩"],
    ["双子 striker", "两架永远编队；被分开超 5 米会哀鸣对撞，火花照亮全屋", "火花暴露位置", "F 键指挥双子冲刺撞碎脆弱障碍", "肩"],
    ["红外隐身者", "只在热成像视界可见", "无", "热成像视界常驻模块", "眼"],
    ["紫外入侵者", "只在相机层可见", "无", "相机层免举相机（视界常驻）", "眼"],
    ["流星切割者", "高速绕柱飞行，撞落物资", "撞击声大", "冲刺速度 +15%", "胸"],
    ["红色战机", "撞角开路：撞碎脆弱障碍、推动重物", "蓄力声大", "撞角冷却减半", "胸"],
    ["星际陆战队", "站岗姿态，对异常体做威吓动作", "体型大", "黑影接近警告提前 2 秒", "背"],
    ["战舰", "悬停充当掩体，遮挡黑影视线", "移动 -20%", "受击扣电减半", "背"],
    ["穿越星", "穿墙飞行，拾取墙对面物件", "穿墙时卡墙异响", "一次穿墙闪现（长冷却）", "胸"],
    ["银河竞速者", "绕圈竞速，跟上它会被带到样本旁", "跟丢即重置", "解除冲刺限制", "腿"],
    ["星际奔跑者", "永远在绕圈奔跑", "噪音", "步行速度 +10%", "腿"],
  ],
  [16, 30, 18, 26, 10]
));
bodyChildren.push(bodyLead("实现方式：", "跟随 = 每帧 Lerp 至玩家肩侧偏移锚点叠加正弦浮动；收容 = InteractionRegistry 注册的收容槽交互；改装 = PlayerUpgradeComponent 维护槽位字典，安装时在对应挂点实例化飞船模型（缩放 0.05）。首批建议只做迷彩星号、红外隐身者、双子 striker，形成「护航、视界、保命」三角。"));
bodyChildren.push(bodyLead("工作量：", "首批 3 艘 2 人日；后续每艘约 0.5 人日。"));

bodyChildren.push(h1("六、提案四：调查仪器套装"));
bodyChildren.push(body("Exploration objects 模型包自带 8 件探测器（含多级发光屏贴图），是恐鬼症式调查玩法的现成硬件。仪器作为机器人外挂传感器，同一时间手持一件，滚轮或数字键切换，手持姿态复用 TorchIdle 举臂动画。"));
bodyChildren.push(tableTitle("表 4  八件调查仪器"));
bodyChildren.push(buildTable(
  ["仪器（模型）", "游戏功能", "恐怖点"],
  [
    ["手电筒 Flashlight", "主照明，持续消耗电量", "开灯能看见，关灯能隐蔽，永恒两难"],
    ["口袋手电 Flashlight_Poquet", "电量耗尽后的保底弱光", "快没电时的绝望感"],
    ["EMF 探测器（5 级发光屏）", "靠近异常点时等级由 1 爬到 5，屏幕贴图真实切换", "滴滴声越来越快的压迫"],
    ["温度计（发光屏）", "异常点附近读数骤降", "数字开始变负的那一刻"],
    ["灵异收音盒（发光屏）", "频率层调谐器：扫频切换世界状态；杂音中偶有低语", "「电台里有人在说话」"],
    ["热成像相机", "热残留追踪：显示受害者最后经过的热迹", "冷蓝色人形残影"],
    ["拍照相机（加 GBCamera 滤镜）", "相机层入口；对异常拍照即证据；闪光后短暂致盲自己", "致盲一秒里，照片上多了什么"],
    ["录音笔", "放在异常点旁录制，回实验室播放诡异语音", "叙事碎片投放器"],
  ],
  [26, 40, 34]
));
bodyChildren.push(bodyLead("实现方式：", "装备框架 1 天（手持模型挂玩家相机子物体、切换逻辑、TorchIdle 动画驱动）；每件仪器是独立的 MonoBehaviour（EMF 按距离换屏、温度计按距离换读数、收音盒联动频率层状态、拍照相机截图套 GBCamera 滤镜）。P0 先做手电、EMF、拍照相机三件，构成「照明、定位、记录」三职能。"));

bodyChildren.push(h1("七、提案五：场景道具异常化"));
bodyChildren.push(body("RandomObjects 道具包全部映射为游戏道具，一件不浪费；另附精选静态异想体（无 AI，纯规则与氛围）。"));
bodyChildren.push(tableTitle("表 5  RandomObjects 道具映射"));
bodyChildren.push(buildTable(
  ["模型", "道具定位", "说明"],
  [
    ["floppydisk 软盘", "记忆核心", "读取后浮现一段实验室事故记录（收集品，接核心循环）"],
    ["multimeter 万用表", "电力检测仪", "对充电桩或电线使用，显示是否有电（Metro 电闸谜题道具）"],
    ["wrench 扳手", "扳手", "水淹区排水阀谜题道具；也可修理损坏物件"],
    ["phone 手机", "来电", "铃响接听，听筒里是机器人自己的声音给出一句提示（事件触发器）"],
    ["portableHeater 取暖器", "便携热源", "放下后周围变热：吸引趋热异常体，或制造安全区"],
    ["bat 球棒", "物理工具", "压住会动的盖板、捅远处的开关（明确不做挥击伤害）"],
    ["case 手提箱", "容器", "收纳采集样本的载体（外观演出用）"],
    ["padlock 与 key", "挂锁与钥匙（备选）", "本期不启用；保留作未来锁题玩法储备"],
  ],
  [24, 24, 52]
));
bodyChildren.push(tableTitle("表 6  静态异想体精选（无 AI，纯规则与氛围）"));
bodyChildren.push(buildTable(
  ["名称", "效果", "代价 / 不对劲之处"],
  [
    ["「温柔的椅子」", "坐下充电速度 5 倍", "越来越不想站起来：起身需连按，按键反馈逐次迟钝。它没有恶意，它只是太温柔了"],
    ["「任务清单」", "完成任务给电量与芯片奖励", "任务一条比一条不对劲，最后一条写着：谢谢你陪我到最后一格电"],
    ["「对面的灯」", "Metro 隧道对面轨道的灯，走近就灭、走远就亮，永远到不了", "无机制、无收益——玩家会花二十分钟试图到达它，这就是它的机制"],
    ["「174 号班机」", "Plane04 系列零件悬浮成永远处于坠毁前一刻的客机；靠近零件会缓慢追随", "须在散件中找到位置随机的黑匣子才能收容；收容后实验室里偶尔传出舱门提示音"],
    ["「门里门」", "20 厘米高的迷你门，打开可见亮灯小房间与踱步的迷你机器人；对它挥手它会回应", "收容后：可透过任意关着的门窥视门后；窥视时门后传来很轻的敲门声"],
    ["「敲击」", "无实体：关着的门后传来三下敲门声，开门后空无一物", "敲击声后 30 秒内穿门，传送落点偏移 2 米——它在门的另一侧推你"],
    ["「回门遥控」", "按一下立刻传回实验室门口（保命）", "冷却 3 分钟；使用后本次世界的收集物掉一半在原地"],
    ["「夜视模组」", "绿色夜视，黑暗全清楚", "见光全白，耗电速度 3 倍"],
    ["「重力螺丝」", "拧进任意门框，该门 60 秒内无视锁闭随便穿一次", "拧螺丝需蹲桩 5 秒，期间无防备"],
    ["「安全绳」", "系在实验室门口，按 F 顺绳拉回（保命）", "绳长 40 米限制探索半径；解开即放弃保命"],
  ],
  [18, 40, 42]
));
bodyChildren.push(bodyLead("实现方式：", "道具数据全部 ScriptableObject（名称、图标、类型、效果参数），加新道具即建资产不改代码；异想体效果多为触发器加 UI 层把戏（任务清单、光标）或简单区域逻辑（椅子、灯），无 AI 依赖。工作量：1 天（数据底座半天 + 首批道具半天）。"));

bodyChildren.push(h1("八、提案六：无战斗恐怖氛围系统"));
bodyChildren.push(bodyLead("设计立场：", "本作没有任何攻击与对抗功能。恐怖全部来自资源压力（电量）、信息压力（视界）与氛围（声光）。"));
bodyChildren.push(bodyLead("三个组成：", "（1）灯光事件：灯管闪烁、整段熄灭、突然亮起的追光——全部是 Light 加脚本时间轴，与 Metro 渐熄规则复用；（2）音效层：门嗡嗡声随距离渐强、穿门 whoosh、每世界一条环境音循环、靠近异常体的低语 stinger——CC0 素材库加触发体积即可；（3）黑影实体：MCO 包的 Ninja 移动动画贴纯黑材质，只接近不攻击——它靠近时屏幕边缘渐暗、电量流失加速，摆脱方式是拉开距离、遮挡视线或使用诱饵信标；被触碰即 Hit 动画加扣电并被弹回实验室门口。"));
bodyChildren.push(bodyLead("实现方式：", "事件系统 = 触发体积 + 灯光/音频/后处理参数动画；黑影 = NavMeshAgent 巡游点 + 与玩家距离驱动的屏幕渐晕强度。工作量：1.5 天。"));

bodyChildren.push(h1("九、待团队决策点"));
bodyChildren.push(body("以下问题建议小组会上逐条过，每条都附程序侧推荐项："));
bodyChildren.push(tableTitle("表 7  决策清单"));
bodyChildren.push(buildTable(
  ["#", "决策点", "选项", "程序侧推荐"],
  [
    ["1", "核心循环确认", "收集—收容—结局 / 逃出去型 / 其他", "推荐收集循环：与三视界、收容改装全部咬合"],
    ["2", "P0 世界范围", "乡村 + Metro 先行 / 加做工厂 / 加水淹区", "推荐乡村 + Metro：场景已接入传送门，其余需先搭可玩区"],
    ["3", "手电筒形态", "手持模型（沉浸） / 纯光源（快）", "推荐手持：TorchIdle 动画现成，仅多半天"],
    ["4", "GBCamera 滤镜范围", "仅照片 / 举相机时整个取景画面", "推荐仅照片：全屏滤镜易视觉疲劳，照片里出现更有冲击"],
    ["5", "黑影实体", "本期做 / 下期做 / 不做", "推荐下期：P0 先用灯光与音效事件压氛围，黑影作版本大招"],
    ["6", "审讯室问答形式", "固定题库 / 三选一分支", "推荐三选一分支：避免背板，顺便搭分支叙事框架"],
  ],
  [6, 22, 38, 34]
));

bodyChildren.push(h1("十、工作量汇总与排期建议"));
bodyChildren.push(tableTitle("表 8  分期排期（按 4 人团队估算）"));
bodyChildren.push(buildTable(
  ["阶段", "内容", "涉及成员方向", "预估"],
  [
    ["P0 · 第 1 周", "核心循环 + 仪器框架与手电/EMF/拍照三件 + 乡村与 Metro 谜题 + 挡板配色区分", "程序：循环与视界；关卡：谜题摆放；美术：收容翼与挡板配色；音效：事件表", "约 7 人日"],
    ["P1 · 第 2 周", "工厂与水淹区规则解谜 + 改装厂首批 3 艘 + 氛围系统 + 剩余仪器", "同上分工轮转", "约 7 人日"],
    ["P2 · 第 3 周", "审讯室问答 + 结局演出 + 剩余异常体填充 + 打磨与音效补全", "全员", "约 6 人日"],
  ],
  [14, 44, 26, 16]
));
bodyChildren.push(body("以上工作量均按「素材已在库、系统已验证」估算；若中途更换场景或新增系统，按实际重估。所有提案相互独立，砍掉任意一条不影响其余提案运行。", { after: 200 }));

const doc = new Document({
  styles: { default: { document: {
    run: { font: { ascii: "Arial", eastAsia: "Microsoft YaHei" }, size: 24, color: P.body },
    paragraph: { spacing: { line: 312 } },
  }}},
  features: { updateFields: true },
  sections: [
    { properties: { page: { margin: { top: 0, bottom: 0, left: 0, right: 0 } } },
      children: buildCoverR4({
        title: "《门后领域》内容扩展创意提案",
        englishLabel: "THE BEYOND",
        subtitle: "小组讨论稿 · 基于现有资产与模型的可实现方案",
        metaLines: ["提案人：丁晓铭（主程序）", "日期：2026 年 10 月 9 日", "用途：小组讨论与方案抉择"],
        footerLeft: "The Beyond · 门后领域",
        footerRight: "v1.0 讨论稿",
      }) },
    { properties: {
        type: SectionType.NEXT_PAGE,
        page: { margin: { top: 1440, bottom: 1440, left: 1701, right: 1417 },
                pageNumbers: { start: 1, formatType: "lowerRoman" } } },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
        children: [new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "808080" })] })] }) },
      children: [
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 240, after: 240 },
          children: [new TextRun({ text: "目  录", bold: true, size: 32, color: P.body, font: { eastAsia: "SimHei" } })] }),
        new TableOfContents("目录", { hyperlink: true, headingStyleRange: "1-2" }),
        new Paragraph({ spacing: { before: 120 }, children: [
          new TextRun({ text: "提示：在 Word 中右键目录并选择「更新域」以刷新页码。", italics: true, size: 20, color: "909090" })] }),
      ] },
    { properties: {
        type: SectionType.NEXT_PAGE,
        page: { margin: { top: 1440, bottom: 1440, left: 1701, right: 1417 },
                pageNumbers: { start: 1, formatType: "decimal" } } },
      headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT,
        children: [new TextRun({ text: "《门后领域》内容扩展创意提案 · 小组讨论稿", size: 16, color: "A0A0A0" })] })] }) },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
        children: [new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "808080" })] })] }) },
      children: bodyChildren },
  ],
});

Packer.toBuffer(doc).then(buf => {
  const out = "C:/Users/xiaoming/Downloads/FYP/Docs/门后领域_内容扩展创意提案_小组讨论版.docx";
  fs.writeFileSync(out, buf);
  console.log("written:", out, buf.length, "bytes");
});
