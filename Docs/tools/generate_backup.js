const {
  Document, Packer, Paragraph, TextRun, Header, Footer, Table, TableRow, TableCell,
  AlignmentType, HeadingLevel, PageNumber, WidthType, TableLayoutType, BorderStyle,
  ShadingType, VerticalAlign
} = require("docx");
const fs = require("fs");

const P = {
  table: { headerBg: "8A8F98", headerText: "FFFFFF", accentLine: "8A8F98", innerLine: "D5D8DC", surface: "F2F3F5" },
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
    children: [new TextRun({ text: lead, bold: true, size: 24, color: "5A6470" }),
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

C.push(h1("备案说明"));
C.push(body("本文档收录《门后领域》内容扩展提案中**未纳入当前主提案范围**的全部备选方案：被砍的视界系统、被否决的黑影与多结局、暂缓的支线世界与系统类提案、以及全部叙事彩蛋。每项保留完整的实现方式与工作量，供后续版本（更新补丁 / 毕设答辩后的完整版）随时取用复活。当前主提案范围见《门后领域_内容扩展提案.docx》。"));

// ── 一、三层视界 ──
C.push(h1("一、三层视界系统（视界扩展）"));
C.push(body("世界存在三层现实：肉眼层（默认）、相机层（举起拍照相机，画面套 GameBoy 像素滤镜，隐藏的门 / 墙上文字 / 不存在的人形只在取景框里存在）、频率层（灵异收音盒调频切换同一世界的状态：Metro 88.0 是停电废弃的现在，92.5 是灯亮广播正常的过去）。"));
C.push(body("被移出主提案的原因：异常体已定为全部肉眼可见，三层视界与该约束冲突；但作为「二周目 / 深度探索」机制仍然优秀——通关后解锁相机层，重访各世界寻找肉眼看不到的秘密，是现成的续命内容。"));
C.push(body("实现：相机层 = RogueNoodle GBCamera shader 挂子相机 + 专属 Layer；频率层 = 场景双状态根节点组切换 + Spirit_Box 发光屏换贴图显示频率；配置用 ScriptableObject。工作量：3 天。"));

// ── 二、水淹区 ──
C.push(h1("二、水淹区（备选第四世界）"));
C.push(body("规则「短路」：积水中电量消耗 2 倍并随机短路抖屏。谜题：用扳手关 3 个排水阀（阀门藏在建筑物内），水位下降露出下层区。奖励：「防水涂层」规则（水中消耗正常化）+ 下层区物资。素材：Flooded_Grounds 场景包已在库（含建筑、管线、水体示例）。"));
C.push(body("被移出主流程的原因：控制主流程长度（30~45 分钟，三世界各 2 份样本）。作为更新补丁的第一个新世界最合适——谜题道具（扳手）已在道具池。"));
C.push(body("实现：水体 Mesh 位移 + 触发体积改电量参数；阀门交互复用电闸交互代码。工作量：1~1.5 天。"));

// ── 三、场域事件 ──
C.push(h1("三、异常天气与场域事件"));
C.push(tableTitle("表 1  场域事件表"));
C.push(buildTable(
  ["事件", "世界", "效果", "玩家应对"],
  [
    ["电磁风暴", "Metro", "全仪器屏幕乱码 10 秒，EMF 自动跳 5 级（假信号）", "风暴中不信任仪器，靠肉眼与记忆"],
    ["酸雨", "乡村", "露天放置的道具缓慢被腐蚀（超时消失），机器人淋雨持续耗电", "尽快进屋或收好道具"],
    ["浓雾涨潮", "水淹区", "定时涨雾：视野缩至 5 米，异常体声音反而增强（雾传声）", "雾中靠声音判断，或暂退高处"],
    ["共振时刻", "任意", "所有收容完成的异常体同时轻微震动哼鸣（收集越多越响）", "既是进度提示也是恐怖时刻"],
  ],
  [12, 12, 44, 32]
));
C.push(body("实现：WeatherDirector 单例按世界配置随机调度；粒子模板（雨 / 雾）+ 灯光音频参数曲线。工作量：1 天。"));

// ── 四、电网经济 ──
C.push(h1("四、电网经济系统"));
C.push(body("把电量从「个人血条」升级为「世界资源」：实验室与各世界供电设施组成电网，电力成为可调配的稀缺品。"));
C.push(tableTitle("表 2  电网机制"));
C.push(buildTable(
  ["机制", "效果", "策略点"],
  [
    ["分闸供电", "每个世界的供电可单独拉闸：拉闸世界灯全灭但全局电量上限 +10%", "黑暗探索换续航，做不做？"],
    ["设备耗电注册", "手电、仪器、加热器等全部耗电，同时开启会叠加", "带三件仪器出门=续航减半，带哪三件？"],
    ["过载跳闸", "同时开启的设备总功率超限=跳闸全灭 10 秒", "用仪器前先想想还开着什么"],
    ["偷电现象", "收容某异常体后，夜间有微小电量被「谁」偷走", "世界观钩子：电去哪了？"],
  ],
  [16, 48, 36]
));
C.push(body("实现：GridManager 单例维护电力池，设备注册耗电功率，阈值触发跳闸事件广播。工作量：1 天。"));

// ── 五、拍照存档 ──
C.push(h1("五、拍照证据与存档系统"));
C.push(body("把拍照相机升级为「存档机制」：每次拍照自动存档，照片即存档缩略图；实验室软木板按时间线钉上全部照片（回顾探索历程）；拍到特定瞬间（异常体正脸 / 穿门瞬间 / 收容完成）解锁抓拍成就条目。"));
C.push(body("当前主提案已保留「照片即存档缩略图」的最小功能；本备案为完整版（照片墙 + 成就）。实现：截帧 + 滤镜渲染到纹理 + JSON 存档 + 软木板 Prefab 实例化。工作量：完整版 2 天。"));

// ── 六、环境叙事 ──
C.push(h1("六、环境叙事系统"));
C.push(tableTitle("表 3  环境叙事载体"));
C.push(buildTable(
  ["载体", "内容与机制"],
  [
    ["白板磁贴", "实验室白板可拖动磁贴（时间线拼图）：按正确顺序排列，解锁一段隐藏记录"],
    ["散落文件", "各世界散落纸质文件（拾取后全屏阅读），页码连续——集齐同一编号才能读懂全文"],
    ["终端电脑", "实验室电脑可开关机，屏幕显示「收容数据库」：异常体档案随收容进度解锁"],
    ["前人痕迹", "Metro 站台的粉笔箭头与警告（前一个调查员留下）——跟着走有惊喜，反着走有惊吓"],
  ],
  [18, 82]
));
C.push(body("实现：文件与磁贴为 UI 覆盖层，终端为交互点换屏贴图。工作量：1 天（不含文案撰写；文案可 AI 辅助起草后人工定稿）。"));

// ── 七、残骸与交换机 ──
C.push(h1("七、前任机器人残骸与「任意门交换机」"));
C.push(body("前任机器人残骸：各世界散落同型号机器人残骸（UAL1 模型贴纯黑 + 倒地姿态），E 搜刮随机获得电池 / 零件 / 记忆核心碎片。恐怖点：第三具残骸的头会缓慢转向你；每次进世界残骸姿势都不一样——没见过它动，但位置确实变了。实现：UAL1 实例化 + 黑材质 + 3 组姿态随机 + IInteractable 搜刮表。工作量：1 天。"));
C.push(body("「任意门交换机」：pintu kemana saja 门模型缩小挂实验室墙上，把一件道具塞进门缝，「等价交换」吐出另一件（别的道具 / 一把电池 / 一张字条「它不喜欢这个」）；每次使用后门「消化」5 分钟。实现：交互点 + 道具池随机表 + 门缝发光演出。工作量：1 天。"));

// ── 八、监控网络 ──
C.push(h1("八、监控摄像头网络"));
C.push(body("Thermal_Camera 与 Photo_Camera 模型装到各世界关键走廊当监控头，实验室收容翼监控台可切换查看每一路实时画面——不出实验室就能确认哪个世界的门开着。恐怖点：某路画面里偶尔有黑影走过；切换画面的一瞬间，偶尔看到摄像头正被「什么」贴脸看着。"));
C.push(body("实现：每摄像头一个 RenderTexture，监控台 UI 切换显示。工作量：1.5 天。"));

// ── 九、交互小游戏 ──
C.push(h1("九、换电池交互小游戏"));
C.push(body("换电池、修门锁、插保险丝、装芯片共用一套节奏 QTE（指针旋转对刻度，连按 3 次）：落在完美区间额外多回 5 电，失败也算成功只是无奖励——奖励手感不惩罚。工作量：0.5 天。"));

// ── 十、图鉴与门铃 ──
C.push(h1("十、收容图鉴与门铃补给"));
C.push(body("收容图鉴：实验室终端的「收容数据库」即图鉴 UI，每个已收容异常体一张档案卡（编号、照片、行为记录、收容日期），未收容显示「???」；图鉴 100% 联动审讯室隐藏问题。照片从拍照存档自动填充。实现：图鉴 UI + ScriptableObject 档案。工作量：1 天。"));
C.push(body("门铃补给事件：实验室门铃每 5~8 分钟随机响，开门可能收到总部空投包裹（电池 / 零件），也可能门外一片漆黑只有一句低语。实现：定时器 + 门交互 + 二选一随机表。工作量：0.5 天。"));

// ── 十一、裂缝空间 ──
C.push(h1("十一、裂缝碎片空间"));
C.push(body("各世界随机出现发光裂缝（发光 Plane + 噪点材质）：20 秒限时可钻入，里面是 SciFi MegaKit 搭的「碎片空间」黑盒小房间——放着一件高价值收集品，倒计时归零被强制弹出。裂缝出现位置用现有异常点配置表随机。实现：碎片房间 Prefab 放场景远端 + 进出传送 + 倒计时 UI。工作量：1 天。"));

// ── 十二、语音日志 ──
C.push(h1("十二、机器人语音日志"));
C.push(body("每局开始机器人自动录一段值班语音日志（TTS 或组内配音）：播报今日规则、目标、加一句机械幽默——规则讲解不再是公告屏文字，而是机器人自己的「值班录音」，教程与叙事融合。通关后播放结尾录音呼应开场（首尾闭环）。实现：音频列表按局播放 + 字幕。工作量：0.5 天（不含配音）。"));

// ── 十三、黑影 ──
C.push(h1("十三、黑影实体与好奇心 AI（已被否决，备案）"));
C.push(body("MCO 包 Ninja 动画贴纯黑材质的黑影，不做攻击者，做「模仿者」——它对机器人的兴趣大于恶意。行为随收容数分四阶段：好奇（0~3 件：只出现在视野边缘、门框、相机框里，你举手它也举手）、学习（4~6 件：模仿你的移动延迟 2 秒，站在你下一扇要开的门前）、共鸣（7~9 件：用收集的记忆核心语音对你说话，电量低时蹲在你旁边陪你）、告别（集齐后：结局门前背对你站着让路，挥手）。"));
C.push(body("被否决原因：用户明确「不要黑影」。备案备注：若未来想加威胁实体，此设计的状态机（游走 / 观察 / 模仿 / 堵门 / 告别）与阶段驱动逻辑可复用；「模仿」= 延迟队列回放玩家输入。实现：状态机 + NavMeshAgent 巡游 + 距离驱动屏幕渐晕。工作量：2 天。"));

// ── 十四、多结局 ──
C.push(h1("十四、多结局系统（已被否决，备案）"));
C.push(tableTitle("表 4  结局表"));
C.push(buildTable(
  ["结局", "触发条件", "演出"],
  [
    ["A「回归」", "集齐样本，未收容禁忌异常", "机器人返回总部，交接完毕关机。平淡、干净、像什么都没发生"],
    ["B「同化」", "集齐样本但收容了禁忌异常体", "结局门后是另一个世界。机器人回头看了眼实验室，选择走进去留下"],
    ["C「格式化」", "审讯室错误过多", "实验室执行格式化，游戏重开——新档里有一个陌生机器人的记忆核心等着被拾取"],
    ["D「共处」", "与黑影好感事件全触发", "黑影与机器人并肩坐在结局门前。不进门，也不告别"],
  ],
  [14, 30, 56]
));
C.push(body("被否决原因：用户明确「不做多结局」（当前为审讯室单结局）。备案备注：结局 B「同化」与结局 D「共处」的演出文本可复用为「二周目彩蛋」或「隐藏档案文本」，不需要多结局系统本身。实现：结局标志位组合判断 + 黑屏文字演出。工作量：1 天。"));

// ── 十五、meta 扩展 ──
C.push(h1("十五、元世界异想体扩展件（已纳入两件，扩展件备案）"));
C.push(body("主提案已纳入「注视者」（窗口最小化）与「审问投影」（假权限弹窗）。以下为同机制的扩展件，判定代码同源："));
C.push(tableTitle("表 5  meta 扩展件"));
C.push(buildTable(
  ["异想体", "肉眼可见的表现", "解法（屏幕外真实操作）", "移植的判定"],
  [
    ["「光标吞噬者」", "一团游走的像素噪点球，靠近时你的鼠标光标会消失（它吃了光标）", "用键盘操作把它引到墙角，光标吐出后可点选收容", "光标锁定 / 显示 API"],
    ["「伪加载」", "突然整屏黑掉，中央出现假「加载中…」转圈——其实游戏还在走：黑暗里那个东西正在挪位置", "按 Esc 立刻退出假加载画面（它被强光暴露回原位）", "全屏遮罩 + Esc 事件"],
    ["「静音挑战」", "房间里出现持续尖叫的异常体（三不猴幕二机制）", "把系统音量拖到零并撑过 2 秒（世界安静，它就哑了）", "AudioListener.volume 检测"],
  ],
  [14, 32, 30, 24]
));
C.push(body("实现：全部复用《三不猴》判定代码（焦点事件 + 真实时钟、AudioListener.volume、假弹窗 UI，均在 Mobile 项目跑通）。工作量：每件 0.5 天。"));

// ── 十六、快赢 ──
C.push(h1("十六、快赢提案（已纳入主提案，此处留档）"));
C.push(tableTitle("表 6  快赢清单"));
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
C.push(body("以上各项均为演出层改动（各 0.5 天），已纳入主提案氛围章节。"));

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
        children: [new TextRun({ text: "《门后领域》备选提案备案", size: 16, color: "A0A0A0" })] })] }) },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
        children: [new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "808080" })] })] }) },
      children: C },
  ],
});

Packer.toBuffer(doc).then(buf => {
  const out = "C:/Users/xiaoming/Downloads/FYP/Docs/门后领域_备选提案备案.docx";
  fs.writeFileSync(out, buf);
  console.log("written:", out, buf.length, "bytes");
});
