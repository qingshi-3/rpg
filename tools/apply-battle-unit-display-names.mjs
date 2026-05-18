import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, "..");
const unitRoot = path.join(projectRoot, "assets", "battle", "units");
const defaultDuelystRoot = path.join(
  process.env.USERPROFILE ?? "",
  "Desktop",
  "游戏素材",
  "duelyst-main (1)",
  "duelyst-main",
);
const duelystRoot = process.env.DUELYST_ROOT || defaultDuelystRoot;

const sourceKeyOverrides = new Map(Object.entries({
  f1_shieldforger: "盾牌铸造者",
  f1_scintilla: "闪烁术士",
  f1_3rdgeneral: "圣辉三阶将军",
  f1_altgeneraltier2: "圣辉二阶替补将军",
  f1_bromemk2: "圣辉布罗姆二型",
  f1_buildcommon: "圣辉普通建筑",
  f1_general_skinroguelegacy: "圣辉将军潜行遗装",
  f1_kingsguard: "圣辉王庭守卫",
  f1_sinergyunit: "圣辉协同单位",
  f1_sunbreakguardian: "圣辉破日守护者",
  f1_tier2general: "圣辉二阶将军",
  f1_xylestormblade: "圣辉赛尔风暴刃",
  f2_3rdgeneral: "疾刃三阶将军",
  f2_altgeneraltier2: "疾刃二阶替补将军",
  f2_buildcommon: "疾刃普通建筑",
  f2_buildlegendary: "疾刃传奇建筑",
  f2_eclipseasura: "疾刃蚀影阿修罗",
  f2_general_skindogehai: "疾刃将军道袍遗装",
  f2_godpalmblacktiger: "疾刃神掌黑虎",
  f2_grandmasterzendo: "疾刃宗师禅道",
  f2_hammonnladeseeker: "疾刃求道武僧",
  f2_masteroftaikwai: "疾刃太魁大师",
  f2_ogremonk02: "疾刃鬼僧二型",
  f2_onnabugeisha: "疾刃女武者",
  f2_pandabear02: "疾刃战斗熊猫二型",
  f2_seenoevil: "疾刃无目者",
  f2_sekitori: "疾刃关取",
  f2_sepukku: "疾刃切腹武士",
  f2_shidaimk2: "疾刃四代二型",
  f2_synergyunit: "疾刃协同单位",
  f2_tier2general: "疾刃二阶将军",
  f3_3rdgeneral: "沙域三阶将军",
  f3_altgeneraltier2: "沙域二阶替补将军",
  f3_anubis: "沙域阿努比斯",
  f3_badlandsscout: "沙域荒原斥候",
  f3_buildcommon: "沙域普通建筑",
  f3_ciphyronmk2: "沙域赛弗隆二型",
  f3_deathobelysk: "沙域死亡方尖碑",
  f3_endlessobelysk: "沙域无尽方尖碑",
  f3_eurus: "沙域欧洛斯",
  f3_glassmirage: "沙域琉璃幻景",
  f3_grandmasternoshrak: "沙域宗师诺什拉克",
  f3_ishtarhunter: "沙域伊什塔猎手",
  f3_keeperofages: "沙域纪元守望者",
  f3_lavastormobelysk: "沙域熔风方尖碑",
  f3_obelyskduskwind: "沙域暮风方尖碑",
  f3_obelyskgoldenflame: "沙域金焰方尖碑",
  f3_obelyskredsand: "沙域赤沙方尖碑",
  f3_palacetrinketeer: "沙域宫廷工匠",
  f3_rehorah: "沙域雷霍拉",
  f3_scarabtera: "沙域圣甲翼虫",
  f3_sinergyunit: "沙域协同单位",
  f3_starfirescarab: "沙域星火圣甲",
  f3_tier2general: "沙域二阶将军",
  f3_trifectaperfecta: "沙域三相完形",
  f3_windshriek: "沙域风啸者",
  f3_windslicer: "沙域风切者",
  f3_zirixfestive: "沙域节庆齐瑞克斯",
  f3_zodiac: "沙域黄道守卫",
  f3_zodiac02: "沙域黄道守卫二型",
  f4_3rdgeneral: "深渊三阶将军",
  f4_altgeneraltier2: "深渊二阶替补将军",
  f4_buildcommon: "深渊普通建筑",
  f4_buildlegendary: "深渊传奇建筑",
  f4_creepmangler: "深渊暗域撕裂者",
  f4_creepydemon: "深渊骇人恶魔",
  f4_daemonvoid: "深渊虚空恶魔",
  f4_demonofeternity: "深渊永恒恶魔",
  f4_maehvmk2: "深渊梅芙二型",
  f4_nocturn: "深渊夜幕者",
  f4_pitstrider: "深渊坑道行者",
  f4_plaguedr: "深渊瘟疫医师",
  f4_remora: "深渊雷莫拉",
  f4_sinergyunit: "深渊协同单位",
  f4_tier2general: "深渊二阶将军",
  f5_3rdgeneral: "原兽三阶将军",
  f5_altgeneraltier2: "原兽二阶替补将军",
  f5_brundlbeast: "原兽布伦德野兽",
  f5_buildcommon: "原兽普通建筑",
  f5_catalystquillbeast: "原兽催化棘兽",
  f5_egg: "原兽龙卵",
  f5_gibblegup: "原兽吉布幼兽",
  f5_lavalasher: "原兽熔岩鞭击者",
  f5_mankatorwarbeast: "原兽曼卡托战兽",
  f5_mythronquest: "原兽神话任务体",
  f5_orphanoftheaspect: "原兽始祖孤裔",
  f5_oxalaia: "原兽奥沙莱亚",
  f5_ragnoramk2: "原兽拉格诺拉二型",
  f5_silitharelder: "原兽老年希利萨",
  f5_silitharveteran: "原兽希利萨老兵",
  f5_silitharyoung: "原兽幼年希利萨",
  f5_sinergyunit: "原兽协同单位",
  f5_spiritharvester: "原兽灵魂收割者",
  f5_tier2general: "原兽二阶将军",
  f5_unstableflump: "原兽不稳定软兽",
  f5_upgradizon: "原兽进化体",
  f5_valknu: "原兽瓦尔努",
  f6_3rdgeneral: "霜晶三阶将军",
  f6_altgeneraltier2: "霜晶二阶替补将军",
  f6_bloodsurge: "霜晶血涌体",
  f6_buildcommon: "霜晶普通建筑",
  f6_buildlegendary: "霜晶传奇建筑",
  f6_eldersaberspine: "霜晶刃脊长老",
  f6_elklodon: "霜晶角兽",
  f6_evilcrystalwisp: "霜晶邪水晶精灵",
  f6_faiefestive: "霜晶节庆法伊",
  f6_fenrirwerewolf: "霜晶芬里尔狼人",
  f6_frostbitehawker: "霜晶冻咬贩子",
  f6_grandmasterembla: "霜晶宗师恩布拉",
  f6_ilenamk2: "霜晶伊莲娜二型",
  f6_sinergyunit: "霜晶协同单位",
  f6_snowchasermk2: "霜晶逐雪者二型",
  f6_snowman: "霜晶雪人",
  f6_spiritwolf: "霜晶灵狼",
  f6_tier2general: "霜晶二阶将军",
  f6_tundraguardian: "霜晶苔原守护者",
  neutral_shadow1: "中立暗影一型",
  neutral_shadow2: "中立暗影二型",
  neutral_shadow3: "中立暗影三型",
  neutral_shadowranged: "中立暗影射手",
  neutral_tribalmelee1: "中立部族近战一型",
  neutral_tribalmelee4: "中立部族近战四型",
  neutral_tribalranged2: "中立部族射手二型",
  neutral_mechaz0rwing: "中立机甲佐尔之翼",
  neutral_merctwinbladewarmonger: "中立佣兵双刃战争贩子",
  prop_cranewisp: "机关鹤形精灵",
}));

const phraseNames = new Map(Object.entries({
  "calibero 2 0": "卡利贝罗二型",
  "d3c": "欺诈机体",
  "d3cepticle": "欺诈机体",
  "deceptib0t": "欺诈机兵",
  "emp": "电磁脉冲体",
  "mechaz0r": "机甲佐尔",
  "mechaz0r!": "机甲佐尔",
  "s i l v e r": "白银机甲",
  "q orrhlma a": "科尔玛",
  "e xun": "伊克森",
  "z0r": "佐尔",
  "zir an": "齐尔安",
  "z ir": "齐尔",
  "yggdra": "伊格德拉",
  "yggdra s": "伊格德拉",
}));

const wordNames = new Map(Object.entries({
  abjudicator: "裁断者",
  abomination: "憎恶体",
  abyssal: "深渊",
  ace: "王牌",
  adjudicator: "裁决者",
  adept: "学徒",
  aer: "艾尔",
  aethermaster: "以太大师",
  alabaster: "雪石",
  alarmist: "警戒者",
  alchemist: "炼金术士",
  allomancer: "炼金术士",
  alter: "异变",
  ancient: "远古",
  andromeda: "安德洛墨达",
  angered: "愤怒",
  araki: "阿拉基",
  arcane: "奥术",
  archmagus: "大法师",
  archonis: "奥术体",
  argeon: "阿吉昂",
  aratha: "阿拉萨",
  aratha: "阿拉萨",
  arbiter: "仲裁者",
  arcanedevourer: "奥术吞噬者",
  armada: "舰队",
  arrow: "箭",
  artificer: "工匠",
  artist: "艺师",
  ash: "灰烬",
  ashmephyt: "灰烬梅菲特",
  assassin: "刺客",
  astral: "星界",
  attack: "攻击",
  auroara: "奥罗拉",
  automaton: "自动机",
  avatar: "化身",
  ayamara: "艾玛拉",
  aymara: "艾玛拉",
  azure: "蔚蓝",
  bakezori: "化履",
  bandit: "盗贼",
  baronette: "女爵",
  barrier: "屏障",
  basilysk: "寒蜥",
  bastion: "堡垒",
  battle: "战斗",
  beak: "喙",
  bear: "熊",
  beast: "野兽",
  beastbound: "驭兽",
  beat: "搏击",
  beatrix: "贝娅特丽克丝",
  beatrix: "贝娅特丽克丝",
  behold: "凝视",
  beholder: "凝视者",
  binky: "宾奇",
  blade: "刃",
  blaze: "烈焰",
  blazing: "炽燃",
  blightchaser: "疫影追猎者",
  blightspawned: "疫生者",
  blinding: "致盲",
  blistering: "灼烈",
  blood: "血",
  bloodfire: "血火",
  bloodshard: "血晶",
  bloodsworn: "血誓",
  bloodtear: "血泪",
  bloodtide: "血潮",
  bloodwing: "血翼",
  blue: "蓝",
  bobble: "摇摆",
  body: "躯体",
  bone: "骨",
  bonechill: "寒骨",
  bonereaper: "骨镰",
  boreal: "北境",
  borean: "北地",
  bound: "束缚",
  brawler: "搏击者",
  breaker: "破坏者",
  brightmoss: "亮苔",
  broken: "破碎",
  brute: "蛮兵",
  bur: "伯尔",
  burster: "爆裂者",
  calculator: "计算者",
  calligrapher: "书法师",
  caller: "呼唤者",
  cannon: "炮体",
  cannoneer: "炮手",
  canopic: "圣瓮",
  captive: "俘虏",
  carcynus: "甲壳蟹",
  cassyva: "卡西瓦",
  celestial: "天穹",
  celebrant: "庆典者",
  champion: "冠军",
  chaos: "混沌",
  charge: "冲能体",
  charger: "冲锋者",
  chassis: "底盘",
  chakkram: "轮刃",
  chakri: "轮刃",
  chirpuka: "奇普卡",
  cindera: "辛德拉",
  circulus: "环使",
  cleric: "教士",
  cloaker: "隐覆者",
  cloudcaller: "云唤者",
  coil: "盘卷",
  collective: "集合体",
  conjurer: "咒唤师",
  corsair: "海盗",
  corporeal: "实体",
  core: "核心",
  crab: "蟹",
  crag: "岩脊",
  crescent: "新月",
  crimson: "绯红",
  crusader: "圣战士",
  cryoblade: "冻刃",
  cryptographer: "秘文师",
  crystal: "水晶",
  crystalline: "水晶",
  cutter: "切割者",
  dagona: "达贡娜",
  dark: "黑暗",
  darkspear: "暗矛",
  day: "日间",
  decimus: "德西姆斯",
  decoy: "诱饵",
  deepfire: "深火",
  defender: "守卫",
  deliverant: "传令体",
  desolator: "毁灭者",
  desolater: "荒芜者",
  devourer: "吞噬者",
  dex: "德克斯",
  diamond: "钻石",
  disciple: "信徒",
  displacer: "移位者",
  dissonance: "失谐者",
  dispirited: "颓丧",
  draugar: "尸鬼",
  dragoon: "龙骑兵",
  dragon: "龙",
  dragonbone: "龙骨",
  dragonhawk: "龙鹰",
  drake: "龙兽",
  dreamgazer: "窥梦者",
  dreamshaper: "塑梦者",
  drinker: "饮者",
  dryad: "树灵",
  drybone: "枯骨",
  dusk: "暮色",
  dust: "尘沙",
  dustdrinker: "饮尘者",
  dustwailer: "尘哀者",
  dweller: "居者",
  earth: "大地",
  ebon: "乌木",
  echo: "回声",
  eclipse: "蚀影",
  elder: "长老",
  elemental: "元素体",
  elveiti: "艾薇提",
  emberwyrm: "余烬龙裔",
  enchanted: "附魔",
  entangler: "缠结者",
  envy: "妒意",
  envybaer: "妒意贝尔",
  eternity: "永恒",
  eventide: "暮潮",
  exile: "流放者",
  exorcist: "驱魔者",
  falcius: "法尔修斯",
  fate: "命运",
  feather: "羽",
  feralu: "菲拉鲁",
  fifth: "第五",
  fiend: "魔徒",
  fire: "火",
  fist: "拳",
  flame: "焰",
  flamewreath: "焰环",
  fledgling: "雏卫",
  fog: "雾",
  force: "力场",
  forge: "铸炉",
  forger: "铸造者",
  fortuneshaper: "塑运者",
  four: "四",
  fox: "狐",
  freeblade: "自由刃",
  frost: "霜",
  frostfire: "霜火",
  frosthorn: "霜角",
  frostiva: "霜蒂瓦",
  frostiva: "霜蒂瓦",
  furiosa: "芙莉奥莎",
  gambler: "赌徒",
  gauntlet: "护手",
  gauj: "高吉",
  gazer: "凝视者",
  geargrinder: "齿轮研磨者",
  general: "将军",
  geomancer: "地脉术士",
  ghost: "幽影",
  ghoulie: "食尸鬼",
  giago: "贾戈",
  gibbet: "绞架灵",
  giant: "巨型",
  glub: "格鲁布",
  gloomchaser: "幽郁追猎者",
  golem: "魔像",
  golden: "黄金",
  gore: "血角",
  grand: "大",
  grandmaster: "宗师",
  grailmaster: "圣杯大师",
  gravity: "重力",
  great: "大",
  grimrock: "格里姆洛克",
  grimes: "格莱姆斯",
  grincher: "格林彻",
  gro: "格罗",
  grove: "林地",
  gryphon: "狮鹫",
  guard: "守卫",
  guardian: "守护者",
  hand: "之手",
  haunt: "魂影",
  healer: "医者",
  healing: "疗愈",
  heart: "心",
  hearth: "炉心",
  herald: "使者",
  heretic: "异端者",
  hexclaw: "咒爪",
  hideatsu: "秀笃",
  high: "高阶",
  highhand: "强手",
  hollow: "空心",
  horn: "角",
  horror: "恐惧体",
  hound: "猎犬",
  howler: "嚎叫者",
  hsuku: "赫苏库",
  huldra: "胡尔德拉",
  hunter: "猎手",
  hydrax: "海德拉克斯",
  ice: "冰",
  iceblade: "冰刃",
  icy: "冰灵",
  idol: "神像",
  immortal: "不朽者",
  impervious: "坚不可摧",
  imp: "小魔",
  incinera: "焚化者",
  indominus: "不屈者",
  inquisitor: "审判官",
  invader: "入侵者",
  invigoration: "活力",
  iron: "铁",
  ironclad: "铁甲",
  ironcliffe: "铁崖",
  jax: "贾克斯",
  jaxi: "贾克西",
  joseki: "定式",
  juggernaut: "重装巨像",
  judicator: "裁判者",
  kaero: "凯罗",
  kage: "影",
  kahlmar: "卡尔玛",
  kaido: "海斗",
  kaleos: "卡莱奥斯",
  katara: "卡塔拉",
  katastrophosaurus: "灾厄龙兽",
  keeper: "守护者",
  kelaino: "刻莱诺",
  keshrai: "凯什莱",
  khymera: "奇美拉",
  ki: "气",
  kin: "族裔",
  kindling: "引火者",
  kindred: "同族",
  knight: "骑士",
  koan: "公案",
  koi: "锦鲤",
  kolossus: "巨像",
  komodo: "科莫多",
  kron: "克朗",
  kujata: "库加塔",
  lady: "女士",
  lancer: "枪兵",
  lantern: "灯影",
  lasher: "鞭击者",
  lava: "熔岩",
  leviathan: "巨兽",
  lifegiver: "赋命者",
  light: "光",
  lightchaser: "逐光者",
  lightning: "闪电",
  lilithe: "莉莉丝",
  lion: "狮",
  locke: "洛克",
  lord: "领主",
  loreweaver: "织识者",
  lost: "失落",
  luminous: "辉光",
  lux: "卢克斯",
  lynthian: "灵西亚",
  lysian: "利希安",
  magi: "法师",
  magma: "岩浆",
  man: "人",
  mandrake: "曼德拉草",
  marauder: "劫掠者",
  massacre: "屠戮",
  master: "大师",
  material: "物质",
  matron: "女族长",
  maw: "巨口",
  maerid: "梅瑞德",
  maia: "玛亚",
  megafiend: "巨魔徒",
  megapenti: "巨蛇裔",
  mech: "机甲",
  meltdowm: "熔毁",
  meltdown: "熔毁",
  metallurgist: "冶金师",
  metaltooth: "铁齿",
  mephyt: "梅菲特",
  mirrorrim: "镜界者",
  mnemovore: "噬忆者",
  moebius: "莫比乌斯",
  mogwai: "魔怪",
  moloki: "莫洛基",
  monolith: "巨碑",
  moon: "月",
  moonlit: "月光",
  moonrider: "月骑士",
  mortar: "迫击",
  moss: "苔",
  mystic: "秘术师",
  nahlgol: "纳尔戈尔",
  nature: "自然",
  necroseer: "亡灵视者",
  nemesis: "复仇者",
  nemeton: "圣林",
  nekomata: "猫又",
  night: "夜",
  nightmare: "梦魇",
  nightsorrow: "夜哀",
  nimbus: "云灵",
  nocturne: "夜曲",
  north: "北地",
  oak: "橡木",
  oakenheart: "橡心",
  obliterate: "湮灭",
  oculus: "眼魔",
  of: "之",
  okkadok: "奥卡多克",
  omniseer: "全知者",
  oni: "鬼武者",
  ooze: "软泥",
  ooz: "软泥",
  operant: "作战体",
  orb: "法球",
  orbrider: "驭球者",
  orias: "奥里亚斯",
  orizuru: "折鹤",
  oropsisaur: "甲背龙兽",
  painter: "画师",
  palm: "掌",
  panda: "熊猫",
  pandamonium: "熊猫乱舞",
  panddo: "潘多",
  pandora: "潘多拉",
  pantheran: "黑豹人",
  paragon: "典范",
  paradise: "乐园",
  pax: "帕克斯",
  peacekeeper: "维和者",
  phalanxar: "方阵兽",
  phantom: "幻影",
  phantasm: "幻像",
  priestess: "女祭司",
  pridebeak: "傲羽",
  prime: "主宰",
  primordial: "原初",
  primus: "元初",
  prisoner: "囚徒",
  prophet: "先知",
  protector: "保护者",
  prowler: "潜猎者",
  punch: "重拳",
  pureblade: "纯刃",
  purgatos: "普加托斯",
  quahog: "夸霍格",
  quartermaster: "军需官",
  rage: "怒意",
  ragebinder: "缚怒者",
  rae: "蕾",
  rancour: "怨怒",
  rawr: "拉尔",
  razorback: "锋背",
  razorcrag: "锐岩",
  reader: "读者",
  realmkeeper: "界域守护者",
  reaper: "收割者",
  reaver: "掠夺者",
  recombobulus: "重组体",
  reliquarian: "圣物守护者",
  repulsor: "斥退者",
  rescue: "救援",
  reticle: "瞄准核心",
  rexx: "雷克斯",
  rifter: "裂隙行者",
  rin: "琳",
  rigger: "装配师",
  rizen: "瑞岑",
  rock: "岩",
  rok: "洛克",
  rokadoptera: "洛卡翼虫",
  rogue: "游荡",
  rook: "战车",
  ruby: "红玉",
  saberspine: "刃脊",
  sai: "赛",
  sandswirl: "沙旋",
  sand: "沙",
  sarlac: "萨拉克",
  savage: "蛮者",
  scarabyte: "小圣甲",
  scarlet: "猩红",
  scarzig: "斯卡齐格",
  scientist: "科学家",
  scion: "后裔",
  scioness: "女后裔",
  scintilla: "闪烁术士",
  scroll: "卷轴",
  seeker: "追寻者",
  seer: "视者",
  seismoid: "震岩体",
  sentinel: "哨卫",
  serenity: "静谧",
  serpenti: "蛇裔",
  shadow: "暗影",
  shadowdancer: "影舞者",
  shadowsworn: "影誓者",
  shaper: "塑形者",
  shinkage: "影景",
  shiro: "白",
  shivers: "寒颤者",
  shield: "盾牌",
  shieldmaster: "盾卫大师",
  silhouette: "剪影",
  silica: "硅晶",
  silver: "白银",
  silverbeak: "银喙",
  silverguard: "白银卫",
  silvertongue: "银舌",
  sister: "修女",
  sky: "天空",
  skyfall: "天坠",
  skyrock: "天岩",
  skywing: "天翼",
  skullprophet: "颅骨先知",
  slo: "斯洛",
  snow: "雪",
  sol: "索尔",
  solarius: "索拉里斯",
  solfist: "日拳",
  solpiercer: "穿阳者",
  songhai: "武道",
  soul: "灵魂",
  soulreaper: "魂镰",
  soulstealer: "夺魂者",
  spear: "矛",
  spell: "法术",
  spelleater: "噬法者",
  spelljammer: "扰法者",
  spellspark: "法术火花",
  spine: "脊",
  spines: "尖脊",
  spirit: "灵魂",
  spitter: "喷吐者",
  spriggin: "树苗灵",
  squire: "侍从",
  star: "星",
  starhorn: "星角",
  starstrider: "星行者",
  steel: "钢",
  sterope: "斯忒洛佩",
  stone: "石",
  storm: "风暴",
  stormblade: "风暴刃",
  stormmetal: "风暴金属",
  strategos: "战略官",
  strider: "行者",
  striker: "打击者",
  sun: "太阳",
  sunbreaker: "破日者",
  sunforge: "日铸",
  sunforger: "日铸者",
  sunrise: "日升",
  sunriser: "日升者",
  sunset: "日暮",
  sunsteel: "日钢",
  sunstone: "日石",
  suntide: "日潮",
  surgeforger: "涌能铸造者",
  swamp: "沼泽",
  sworn: "誓约",
  sword: "剑",
  syvrel: "希芙蕾尔",
  taura: "陶拉",
  taygete: "塔宇革忒",
  templar: "圣殿武士",
  tempest: "风暴",
  terradon: "泰拉顿",
  terrible: "恐怖者",
  tethermancer: "系链术士",
  the: "",
  theobule: "西奥布尔",
  thunderhorn: "雷角",
  tide: "潮",
  timekeeper: "时计师",
  titan: "泰坦",
  totem: "图腾",
  tormentor: "折磨者",
  tower: "塔",
  tracer: "追迹者",
  treant: "树人",
  trinketeer: "饰品匠",
  truesight: "真视",
  twilight: "暮光",
  twin: "双刃",
  ubo: "乌波",
  umb: "暗影",
  umbra: "暗影",
  underworld: "冥界",
  unstable: "不稳定",
  ursaplomb: "熊铠",
  vaath: "瓦斯",
  vale: "谷地",
  valiant: "英勇者",
  vanquisher: "征服者",
  variax: "瓦里亚克斯",
  vengeful: "复仇者",
  veteran: "老兵",
  viper: "毒蛇",
  visionar: "幻视者",
  void: "虚空",
  vol: "沃尔",
  voracity: "贪食",
  vorpal: "裂空",
  vox: "沃克斯",
  wailer: "哀嚎者",
  war: "战",
  warden: "看守者",
  warlock: "术士",
  warmaster: "战争大师",
  watcher: "守望者",
  watchful: "警戒",
  wave: "波",
  weaver: "织者",
  white: "白",
  whyte: "怀特",
  widow: "寡妇",
  wild: "狂野",
  wind: "风",
  windblade: "风刃",
  winter: "冬",
  winterblade: "冬刃",
  wolfraven: "狼鸦",
  wolfpunch: "狼拳",
  worldcore: "世界核心",
  wrath: "怒火",
  xaan: "赞",
  xel: "泽尔",
  xenkai: "玄凯",
  xerroloth: "泽罗洛斯",
  xho: "佐",
  xyle: "赛尔",
  yun: "云",
  zane: "赞恩",
  zendo: "禅道",
  zenrui: "禅瑞",
  zephyr: "西风",
  zirix: "齐瑞克斯",
  zukong: "祖空",
  zurael: "祖瑞尔",
  zyx: "吉克斯",
}));

for (const [key, value] of Object.entries({
  akrane: "阿克兰",
  alkyone: "阿尔刻俄涅",
  amu: "阿穆",
  an: "安",
  archon: "执政官",
  archonspellbinder: "执政官缚法者",
  arclyte: "弧光",
  arctic: "极寒",
  artifact: "圣物",
  artifacthunter: "圣物猎手",
  avenger: "复仇者",
  avanger: "复仇者",
  azurite: "天蓝石",
  bandainamco: "联动版",
  beetle: "甲虫",
  black: "黑",
  binder: "束缚者",
  blades: "刃",
  blisteringscorn: "灼烈斯科恩",
  bonecrusher: "碎骨者",
  boar: "野猪",
  breach: "破城者",
  building: "建筑",
  burrower: "掘地者",
  cage: "牢笼",
  candy: "糖果",
  capricious: "反复无常",
  captainhankheart: "汉克哈特船长",
  cade: "凯德",
  cacophynos: "卡科菲诺斯",
  chaser: "追逐者",
  chloroara: "叶绿奥拉",
  contentiousbrute: "争斗蛮兵",
  crawler: "爬行者",
  critterc: "小生物丙",
  critterd: "小生物丁",
  crossbones: "交叉骨",
  darkspine: "暗脊",
  dasher: "疾行者",
  dancing: "舞刃",
  dancingblades: "舞刃者",
  dagger: "匕首",
  daggerkiri: "雾刃匕首",
  death: "死亡",
  deathblighter: "死疫者",
  dilotas: "迪洛塔斯",
  dogdragon: "犬龙",
  dowager: "太夫人",
  dreadnought: "无畏重装",
  dervish: "苦行者",
  elf: "精灵",
  drudging: "劳役",
  drogon: "卓贡",
  dunecaster: "沙丘术士",
  duskweaver: "暮织者",
  eater: "吞噬者",
  edcrawler: "爬行者",
  elucidator: "启明者",
  elyx: "艾利克斯",
  excelsious: "艾克塞尔修斯",
  enforcer: "执行者",
  eshaper: "重塑者",
  eternal: "永恒",
  faie: "法伊",
  fanblade: "扇刃",
  fear: "恐惧",
  fenrir: "芬里尔",
  fish: "鱼形体",
  firestarter: "引火者",
  fiz: "菲兹",
  gambitgirl: "奇策少女",
  gnasher: "啃咬者",
  grablackhole: "黑洞抓取体",
  grovekeeper: "林地守护者",
  gor: "戈尔",
  grym: "格里姆",
  hailstone: "冰雹石",
  harmony: "和谐体",
  harbinger: "先兆者",
  headhunter: "猎头者",
  heartseeker: "追心者",
  helm: "头盔",
  highmayne: "高鬃",
  huntress: "女猎手",
  husk: "空壳",
  ignis: "伊格尼斯",
  inceptor: "引导者",
  ion: "离子",
  ir: "伊尔",
  jaguar: "捷豹",
  jin: "金",
  justicar: "裁决官",
  kara: "卡拉",
  kian: "基安",
  kiri: "雾刃",
  klaxon: "警钟",
  knell: "丧钟",
  kraigon: "克莱贡",
  legion: "军团",
  letigress: "蕾虎",
  letigresscub: "蕾虎幼体",
  limiter: "限界器",
  locust: "蝗群",
  lynx: "猞猁",
  malyk: "马利克",
  maiden: "侍女",
  mantella: "曼特拉",
  mind: "心智",
  mindwarper: "扭心者",
  mini: "小型",
  monger: "贩战者",
  mirkwooddevourer: "幽林吞噬者",
  mk2: "二型",
  moons: "诸月",
  monument: "纪念碑",
  mrgoldmclover: "金苜蓿先生",
  myriad: "万象",
  naga: "娜迦",
  needler: "针刺者",
  nine: "九",
  onyx: "黑曜",
  ox: "牛灵",
  owlbearmage: "枭熊法师",
  owlshadescholar: "枭影学者",
  paddo: "帕多",
  pennyarcade01: "便士街机一型",
  pennyarcade02: "便士街机二型",
  pennyarcade03: "便士街机三型",
  pennyarcade04: "便士街机四型",
  phasehound: "相位猎犬",
  pontiff: "教宗",
  prismaticillusionist: "棱光幻术师",
  prismaticillusionistminion: "棱光幻象",
  prongbok: "叉角羚",
  putridmindflayer: "腐化剥心者",
  pyromancer: "炎术士",
  radiant: "辉光",
  ravager: "掠夺者",
  rehorah: "雷霍拉",
  rejuvenator: "复苏者",
  replicator: "复制者",
  reva: "蕾娃",
  reshaper: "重塑者",
  rex: "雷克斯",
  rhyno: "犀角兽",
  rippler: "涟漪者",
  rower: "掘地者",
  runner: "奔行者",
  rui: "瑞",
  saon: "萨翁",
  sajj: "萨姬",
  sammer: "震法者",
  santaur: "圣诞使者",
  sapphire: "蓝宝石",
  second: "第二",
  seismic: "地震",
  seraphim: "炽天使",
  servant: "仆从",
  seven: "七贤",
  shreddingmantis: "撕裂螳螂",
  sightlessfarseer: "盲眼远视者",
  sirocco: "热风术士",
  singletonmythron: "唯一神话体",
  sinister: "阴险",
  siren: "塞壬",
  skurge: "灾刃",
  sleet: "冻雨",
  slicer: "切风者",
  soboro: "索博罗",
  solus: "索勒斯",
  songweaver: "织歌者",
  sorcerer: "术士",
  sparrowhawk: "雀鹰",
  swarm: "虫群",
  taskmaster: "监工",
  synja: "辛加",
  tahr: "塔尔羊",
  talon: "战爪",
  thecollective: "集合体",
  thegreatprotector: "大保护者",
  theseven: "七贤",
  thevalue: "谷地",
  thraex: "瑟雷克斯",
  thrusters: "推进翼",
  tiger: "猛虎",
  tombstone: "墓碑",
  trickster: "诈术师",
  trinity: "三相",
  tusk: "獠牙",
  twitch: "直播版",
  tyrant: "暴君",
  unhallowed: "亵渎者",
  unseven: "逆七贤",
  vespyr: "霜灵",
  vindicator: "辩护者",
  voice: "声灵",
  walker: "行者",
  warblade: "战刃",
  warmonger: "战争贩子",
  warpup: "跃迁幼体",
  well: "井",
  whistler: "啸箭者",
  whistlingblade: "啸鸣刃",
  widowmaker: "寡妇制造者",
  windcliffe: "风崖",
  winds: "诸风",
  wing: "翼",
  wings: "翼",
  wisp: "精灵",
  wu: "吴",
  wasteland: "荒原",
  wraith: "怨灵",
  z: "兹",
  zen: "禅",
  zir: "齐尔",
})) {
  wordNames.set(key, value);
}

const compactTokenNames = new Map(Object.entries({
  abjudicator: "裁断者",
  abomination: "憎恶体",
  abyssian: "深渊",
  adept: "学徒",
  alt: "替补",
  ancient: "远古",
  antiswarm: "反虫群",
  arcanedevourer: "奥术吞噬者",
  archer: "弓手",
  archdeacon: "大执事",
  artificer: "工匠",
  asura: "阿修罗",
  attack: "攻击",
  avatar: "化身",
  backline: "后排",
  beast: "野兽",
  black: "黑",
  blade: "刃",
  blighter: "枯疫者",
  blood: "血",
  bloodletter: "放血者",
  bloodshard: "血晶",
  bone: "骨",
  boulder: "巨岩",
  build: "建筑",
  caster: "施法者",
  chaos: "混沌",
  chassis: "底盘",
  chill: "寒意",
  common: "普通",
  copy: "复制体",
  crawler: "爬行者",
  crystal: "水晶",
  dark: "黑暗",
  death: "死亡",
  demon: "恶魔",
  desert: "沙漠",
  dervish: "苦行者",
  dragon: "龙",
  dream: "梦境",
  elder: "长老",
  endless: "无尽",
  epic: "史诗",
  evil: "邪",
  exploding: "爆裂",
  faction: "阵营",
  fan: "扇",
  fire: "火",
  flame: "焰",
  frost: "霜",
  general: "将军",
  golden: "黄金",
  golem: "魔像",
  guard: "守卫",
  guardian: "守护者",
  helm: "头盔",
  hunter: "猎手",
  ice: "冰",
  iron: "铁",
  jammer: "干扰者",
  king: "王",
  kings: "王庭",
  lava: "熔岩",
  legendary: "传奇",
  lightning: "闪电",
  mage: "法师",
  master: "大师",
  mech: "机甲",
  melee: "近战",
  merc: "佣兵",
  minion: "召唤物",
  monster: "异兽",
  moon: "月",
  nature: "自然",
  neutral: "中立",
  obelysk: "方尖碑",
  onyx: "黑曜",
  oracle: "神谕者",
  paragon: "典范",
  phantom: "幻影",
  prime: "主宰",
  protector: "保护者",
  ranged: "远程",
  red: "赤",
  rift: "裂隙",
  rifter: "裂隙行者",
  runner: "奔行者",
  rune: "符文",
  sand: "沙",
  scarab: "圣甲",
  scorpion: "蝎",
  seer: "视者",
  sentinel: "哨卫",
  shadow: "暗影",
  shield: "盾牌",
  skin: "皮肤",
  snow: "雪",
  soul: "灵魂",
  spark: "火花",
  spell: "法术",
  spirit: "灵魂",
  starter: "火种",
  storm: "风暴",
  sun: "太阳",
  sword: "剑",
  synergy: "协同",
  tank: "重装",
  tier: "阶",
  tribal: "部族",
  twilight: "暮光",
  veteran: "老兵",
  void: "虚空",
  war: "战争",
  white: "白",
  wind: "风",
  wings: "翼",
  wolf: "狼",
  wyrm: "龙裔",
}));

for (const [key, value] of wordNames) {
  if (value) {
    compactTokenNames.set(key, value);
  }
}

const factionPrefixNames = new Map([
  ["f1", "圣辉"],
  ["f2", "疾刃"],
  ["f3", "沙域"],
  ["f4", "深渊"],
  ["f5", "原兽"],
  ["f6", "霜晶"],
]);

const animationSuffixes = [
  "Breathing",
  "Idle",
  "Run",
  "Walk",
  "Attack",
  "Hit",
  "Damage",
  "Death",
  "Active",
  "Inactive",
];
const compactTokenKeys = [...compactTokenNames.keys()].sort((a, b) => b.length - a.length);

function readText(filePath) {
  return fs.readFileSync(filePath, "utf8");
}

function writeText(filePath, text) {
  fs.writeFileSync(filePath, text, "utf8");
}

function stripExtension(fileName) {
  return fileName.replace(/\.[^.]+$/, "");
}

function normalizeKey(value) {
  return value
    .toLowerCase()
    .replace(/[\s-]+/g, "_")
    .replace(/[^a-z0-9_]+/g, "")
    .replace(/_+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function normalizeLookup(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "");
}

function normalizePhrase(value) {
  return value
    .toLowerCase()
    .replace(/['’]/g, " ")
    .replace(/[^a-z0-9]+/g, " ")
    .trim()
    .replace(/\s+/g, " ");
}

function stripAnimationSuffix(alias) {
  return animationSuffixes.reduce(
    (current, suffix) => current.endsWith(suffix) ? current.slice(0, -suffix.length) : current,
    alias,
  );
}

function resolveDuelystRoot() {
  return fs.existsSync(duelystRoot) ? duelystRoot : "";
}

function loadLocaleTable(root) {
  const table = {};
  for (const localePath of [
    "app/localization/locales/en/cards.json",
    "app/localization/locales/en/boss_battles.json",
    "app/localization/locales/en/tutorial.json",
    "app/localization/locales/en/modifiers.json",
  ]) {
    const fullPath = path.join(root, localePath);
    if (fs.existsSync(fullPath)) {
      Object.assign(table, JSON.parse(readText(fullPath)));
    }
  }

  return table;
}

function walkFiles(directory, results = []) {
  if (!fs.existsSync(directory)) {
    return results;
  }

  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      walkFiles(fullPath, results);
    } else {
      results.push(fullPath);
    }
  }

  return results;
}

function readI18nName(rawKey, localeTable) {
  const key = rawKey
    .replace(/^cards\./, "")
    .replace(/^boss_battles\./, "")
    .replace(/^tutorial\./, "")
    .replace(/^modifiers\./, "");
  return localeTable[key] ?? "";
}

function loadDuelystSourceNameMap() {
  const root = resolveDuelystRoot();
  if (!root) {
    return { root: "", names: new Map() };
  }

  const localeTable = loadLocaleTable(root);
  const names = new Map();
  const factoryRoot = path.join(root, "app", "sdk", "cards", "factory");
  const files = walkFiles(factoryRoot).filter((filePath) => filePath.endsWith(".coffee"));

  for (const filePath of files) {
    const lines = readText(filePath).split(/\r?\n/);
    let currentName = "";
    for (const line of lines) {
      let match = line.match(/card\.name\s*=\s*i18next\.t\("([^"]+)"\)/);
      if (match) {
        currentName = readI18nName(match[1], localeTable);
      }

      match = line.match(/card\.name\s*=\s*"([^"]+)"/);
      if (match) {
        currentName = match[1];
      }

      if (!currentName) {
        continue;
      }

      const resourceRegex = /RSX\.([A-Za-z0-9_]+)\.name/g;
      while ((match = resourceRegex.exec(line)) !== null) {
        const sourceKey = normalizeLookup(stripAnimationSuffix(match[1]));
        if (sourceKey && !names.has(sourceKey)) {
          names.set(sourceKey, currentName);
        }
      }
    }
  }

  return { root, names };
}

function resolveSourceKey(packageDir, unitId) {
  const framesPath = path.join(packageDir, "frames.tres");
  if (fs.existsSync(framesPath)) {
    const frameText = readText(framesPath);
    const textureMatch = frameText.match(/path="res:\/\/assets\/battle\/units\/[^"]+\/([^"\/]+\.png)"/);
    if (textureMatch) {
      return {
        sourceKey: normalizeKey(stripExtension(textureMatch[1])),
        sourceKind: "frames-texture",
      };
    }
  }

  const png = fs.readdirSync(packageDir)
    .filter((fileName) => fileName.toLowerCase().endsWith(".png"))
    .sort((a, b) => a.localeCompare(b, "en"))[0];
  if (png) {
    return {
      sourceKey: normalizeKey(stripExtension(png)),
      sourceKind: "package-png",
    };
  }

  return {
    sourceKey: normalizeKey(unitId),
    sourceKind: "unit-id",
  };
}

function sourceParts(sourceKey) {
  const parts = sourceKey.split("_").filter(Boolean);
  const sourcePrefix = parts.length > 1 && /^(f[1-6]|neutral|boss)$/.test(parts[0])
    ? parts[0]
    : "";
  return {
    sourcePrefix,
    core: sourcePrefix ? parts.slice(1).join("_") : parts.join("_"),
  };
}

function transliterateToken(token) {
  const normalized = normalizePhrase(token).replace(/\s+/g, "");
  if (!normalized) {
    return "";
  }

  if (phraseNames.has(normalized)) {
    return phraseNames.get(normalized);
  }

  const chunks = [
    ["tion", "申"],
    ["shi", "希"],
    ["kai", "凯"],
    ["xan", "赞"],
    ["zor", "佐尔"],
    ["zen", "禅"],
    ["dra", "德拉"],
    ["ryn", "林"],
    ["rom", "罗姆"],
    ["med", "梅德"],
    ["and", "安德"],
    ["ar", "阿尔"],
    ["or", "奥尔"],
    ["ur", "乌尔"],
    ["ae", "艾"],
    ["ai", "艾"],
    ["au", "奥"],
    ["ea", "伊"],
    ["ee", "伊"],
    ["ei", "艾"],
    ["ia", "娅"],
    ["io", "伊奥"],
    ["oo", "乌"],
    ["ou", "奥"],
    ["ph", "夫"],
    ["th", "瑟"],
    ["ch", "奇"],
    ["sh", "希"],
    ["ck", "克"],
    ["qu", "夸"],
    ["x", "克斯"],
    ["z", "兹"],
    ["a", "阿"],
    ["b", "布"],
    ["c", "克"],
    ["d", "德"],
    ["e", "艾"],
    ["f", "夫"],
    ["g", "格"],
    ["h", "哈"],
    ["i", "伊"],
    ["j", "杰"],
    ["k", "卡"],
    ["l", "勒"],
    ["m", "姆"],
    ["n", "恩"],
    ["o", "欧"],
    ["p", "普"],
    ["q", "库"],
    ["r", "尔"],
    ["s", "斯"],
    ["t", "特"],
    ["u", "乌"],
    ["v", "维"],
    ["w", "沃"],
    ["y", "伊"],
  ];

  let remaining = normalized;
  let result = "";
  while (remaining.length > 0) {
    const numberMatch = remaining.match(/^\d+/);
    if (numberMatch) {
      result += numberMatch[0];
      remaining = remaining.slice(numberMatch[0].length);
      continue;
    }

    const chunk = chunks.find(([candidate]) => remaining.startsWith(candidate));
    if (!chunk) {
      remaining = remaining.slice(1);
      continue;
    }

    result += chunk[1];
    remaining = remaining.slice(chunk[0].length);
  }

  return result || token;
}

function translateEnglishToken(token) {
  const normalized = normalizePhrase(token).replace(/\s+/g, "");
  if (!normalized || normalized === "the" || normalized === "in" || normalized === "s" || normalized === "l") {
    return { text: "", transliterated: false };
  }

  if (/^\d+$/.test(normalized)) {
    return { text: normalized, transliterated: false };
  }

  if (phraseNames.has(normalized)) {
    return { text: phraseNames.get(normalized), transliterated: false };
  }

  if (wordNames.has(normalized)) {
    return { text: wordNames.get(normalized), transliterated: false };
  }

  return { text: transliterateToken(token), transliterated: true };
}

function translateEnglishSequence(value) {
  const tokens = normalizePhrase(value).split(" ").filter(Boolean);
  const translated = [];
  const transliterated = [];
  for (const token of tokens) {
    if (token === "of" || token === "and" || token === "the" || token === "in" || token === "s" || token === "l") {
      continue;
    }

    const result = translateEnglishToken(token);
    if (result.text) {
      translated.push(result.text);
    }
    if (result.transliterated) {
      transliterated.push(token);
    }
  }

  return {
    text: translated.join(""),
    transliterated,
  };
}

function translateEnglishName(englishName) {
  const phraseKey = normalizePhrase(englishName);
  if (phraseNames.has(phraseKey)) {
    return {
      displayName: phraseNames.get(phraseKey),
      transliterated: [],
    };
  }

  const commaParts = englishName.split(",").map((part) => part.trim()).filter(Boolean);
  if (commaParts.length > 1) {
    const translatedParts = commaParts.map(translateEnglishSequence);
    return {
      displayName: translatedParts.map((part) => part.text).join(""),
      transliterated: translatedParts.flatMap((part) => part.transliterated),
    };
  }

  const ofMatch = englishName.match(/^(.+?)\s+of\s+(?:the\s+)?(.+)$/i);
  if (ofMatch) {
    const left = translateEnglishSequence(ofMatch[1]);
    const right = translateEnglishSequence(ofMatch[2]);
    return {
      displayName: `${right.text}之${left.text}`,
      transliterated: [...left.transliterated, ...right.transliterated],
    };
  }

  const translated = translateEnglishSequence(englishName);
  return {
    displayName: translated.text,
    transliterated: translated.transliterated,
  };
}

function translateCompactToken(value) {
  let remaining = value;
  const translated = [];
  const transliterated = [];

  while (remaining.length > 0) {
    const numberMatch = remaining.match(/^\d+/);
    if (numberMatch) {
      translated.push(numberMatch[0].padStart(2, "0"));
      remaining = remaining.slice(numberMatch[0].length);
      continue;
    }

    const key = compactTokenKeys.find((candidate) => remaining.startsWith(candidate));
    if (key) {
      translated.push(compactTokenNames.get(key));
      remaining = remaining.slice(key.length);
      continue;
    }

    const tail = remaining.match(/^[a-z0-9]+/)?.[0] ?? remaining[0];
    translated.push(transliterateToken(tail));
    transliterated.push(tail);
    remaining = remaining.slice(tail.length);
  }

  return { translated, transliterated };
}

function reorderCompoundName(core, translated) {
  if (core.startsWith("golem") && translated.length > 1) {
    return `${translated.slice(1).join("")}魔像`;
  }

  if (core.startsWith("beast") && translated.length > 1) {
    return `${translated.slice(1).join("")}野兽`;
  }

  return translated.join("");
}

function translateSourceKeyFallback(sourceKey) {
  const { sourcePrefix, core } = sourceParts(sourceKey);
  const prefix = sourcePrefix === "boss"
    ? "首领"
    : sourcePrefix === "neutral"
      ? "中立"
      : factionPrefixNames.get(sourcePrefix) ?? "";

  const coreSegments = core.split("_").filter(Boolean);
  const translated = [];
  const transliterated = [];
  for (const segment of coreSegments) {
    if (compactTokenNames.has(segment)) {
      translated.push(compactTokenNames.get(segment));
      continue;
    }

    const compact = translateCompactToken(segment);
    translated.push(...compact.translated);
    transliterated.push(...compact.transliterated);
  }

  const coreName = translated.length > 0
    ? reorderCompoundName(core.replace(/_/g, ""), translated)
    : transliterateToken(core);
  return {
    displayName: `${prefix}${coreName}`,
    confidence: transliterated.length === 0 ? "source-key-rule" : "source-key-transliterated",
    transliterated,
  };
}

function translateSourceKey(sourceKey, duelystNames) {
  if (sourceKeyOverrides.has(sourceKey)) {
    return {
      displayName: sourceKeyOverrides.get(sourceKey),
      confidence: "exact-source-key",
      transliterated: [],
      duelystName: "",
    };
  }

  const duelystName = duelystNames.get(normalizeLookup(sourceKey));
  if (duelystName) {
    const translated = translateEnglishName(duelystName);
    return {
      displayName: translated.displayName,
      confidence: translated.transliterated.length === 0
        ? "duelyst-source-name"
        : "duelyst-source-name-transliterated",
      transliterated: translated.transliterated,
      duelystName,
    };
  }

  return {
    ...translateSourceKeyFallback(sourceKey),
    duelystName: "",
  };
}

function readUnitId(unitText) {
  const match = unitText.match(/^Id = "([^"]+)"/m);
  return match ? match[1] : "";
}

function replaceDisplayName(unitText, displayName) {
  const escaped = displayName.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
  if (/^DisplayName = ".*"$/m.test(unitText)) {
    return unitText.replace(/^DisplayName = ".*"$/m, `DisplayName = "${escaped}"`);
  }

  return unitText.replace(/^(Id = "[^"]+")$/m, `$1\nDisplayName = "${escaped}"`);
}

function enumerateUnitResources() {
  const resources = [];

  for (const fileName of fs.readdirSync(unitRoot)) {
    if (fileName.endsWith(".tres")) {
      resources.push({
        unitPath: path.join(unitRoot, fileName),
        packageDir: unitRoot,
        legacyFlat: true,
      });
    }
  }

  for (const entry of fs.readdirSync(unitRoot, { withFileTypes: true })) {
    if (!entry.isDirectory()) {
      continue;
    }

    const unitPath = path.join(unitRoot, entry.name, "unit.tres");
    if (fs.existsSync(unitPath)) {
      resources.push({
        unitPath,
        packageDir: path.join(unitRoot, entry.name),
        legacyFlat: false,
      });
    }
  }

  return resources.sort((a, b) => a.unitPath.localeCompare(b.unitPath, "zh-Hans-CN"));
}

const duelystSource = loadDuelystSourceNameMap();
const report = {
  generatedAt: new Date().toISOString(),
  unitRoot: "res://assets/battle/units",
  duelystRoot: duelystSource.root,
  changed: [],
  unchanged: [],
  lowConfidence: [],
  transliterated: [],
};

for (const resource of enumerateUnitResources()) {
  const unitText = readText(resource.unitPath);
  const unitId = readUnitId(unitText);
  if (!unitId) {
    continue;
  }

  const source = resolveSourceKey(resource.packageDir, unitId);
  const translation = translateSourceKey(source.sourceKey, duelystSource.names);
  const nextText = replaceDisplayName(unitText, translation.displayName);
  const relativePath = path.relative(projectRoot, resource.unitPath).replace(/\\/g, "/");
  const item = {
    path: relativePath,
    unitId,
    sourceKey: source.sourceKey,
    sourceKind: source.sourceKind,
    duelystName: translation.duelystName,
    displayName: translation.displayName,
    confidence: translation.confidence,
  };

  if (!translation.displayName || /[A-Za-z]/.test(translation.displayName)) {
    report.lowConfidence.push({
      ...item,
      reason: "missing-or-latin-display-name",
    });
  }

  if (translation.transliterated.length > 0) {
    report.transliterated.push({
      ...item,
      transliterated: [...new Set(translation.transliterated)].join(","),
    });
  }

  if (nextText === unitText) {
    report.unchanged.push(item);
    continue;
  }

  writeText(resource.unitPath, nextText);
  report.changed.push(item);
}

report.summary = {
  totalUnitCount: report.changed.length + report.unchanged.length,
  changedCount: report.changed.length,
  unchangedCount: report.unchanged.length,
  lowConfidenceCount: report.lowConfidence.length,
  transliteratedCount: report.transliterated.length,
  duelystSourceNameCount: [...report.changed, ...report.unchanged]
    .filter((item) => item.confidence.startsWith("duelyst-source-name")).length,
};

writeText(
  path.join(unitRoot, "_display_name_translation_report.json"),
  `${JSON.stringify(report, null, 2)}\n`,
);

console.log(JSON.stringify(report.summary, null, 2));
