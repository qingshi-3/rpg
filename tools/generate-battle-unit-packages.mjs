import fs from "node:fs";
import path from "node:path";

const projectRoot = path.resolve("D:/godot/rpg");
const textureRoot = path.join(projectRoot, "assets/textures/units");
const unitRoot = path.join(projectRoot, "assets/battle/units");
const resUnitRoot = "res://assets/battle/units";
const resTextureRoot = "res://assets/textures/units";
const tileSize = 101;

const animationSetPath = "res://assets/battle/unit_visuals/default_battle_unit_animation_set.tres";
const unitScriptPath = "res://src/Definitions/Battle/BattleUnitDefinition.cs";
const visualScriptPath = "res://src/Definitions/Battle/BattleUnitVisualDefinition.cs";

const exactNames = new Map(Object.entries({
  andromeda: "安德洛墨达",
  antiswarm: "反虫群",
  borealjuggernaut: "北境重装",
  candypanda: "糖果熊猫",
  chaosknight: "混沌骑士",
  christmas: "圣诞使者",
  cindera: "辛德拉",
  city: "城域守卫",
  crystal: "水晶体",
  decepticle: "欺诈机体",
  decepticlechassis: "欺诈机体底盘",
  decepticlehelm: "欺诈机体头盔",
  decepticleprime: "欺诈机体主宰",
  decepticlesword: "欺诈机体剑刃",
  decepticlewings: "欺诈机体翼",
  dissonance: "失谐者",
  emp: "电磁脉冲体",
  gol: "戈尔",
  grym: "格里姆",
  harmony: "和谐体",
  invader: "入侵者",
  kane: "凯恩",
  kron: "克隆",
  legion: "军团",
  malyk: "马利克",
  manaman: "法力人",
  orias: "奥里亚斯",
  oriasidol: "奥里亚斯神像",
  paragon: "典范者",
  protector: "保护者",
  sandpanther: "沙地豹",
  serpenti: "蛇裔",
  shadowlord: "暗影领主",
  shinkagezendo: "新影禅道",
  skurge: "灾刃",
  skyfalltyrant: "天坠暴君",
  solfist: "太阳之拳",
  soulstealer: "夺魂者",
  spelleater: "噬法者",
  taskmaster: "监工",
  treatdemon: "糖宴恶魔",
  treatdrake: "糖宴幼龙",
  treatoni: "糖宴鬼人",
  umbra: "影曜",
  unhallowed: "亵渎者",
  valiant: "英勇者",
  vampire: "吸血者",
  wolfpunch: "狼拳",
  wraith: "怨魂",
  wujin: "无尽武者",
  "3rdgeneral": "三号将军",
  altgeneral: "替补将军",
  altgeneraltier2: "二阶替补将军",
  tier2general: "二阶将军",
  buildcommon: "通用建筑",
  buildminion: "召唤物建筑",
  buildlegendary: "传奇建筑",
  buildepic: "史诗建筑",
  general: "将军",
  sister: "修女",
  caster: "施法者",
  melee: "近战兵",
  ranged: "远程兵",
  support: "支援者",
  tank: "重装兵",
  special: "特殊单位",
  mech: "机械兵",
  critter: "小生物",
  scintilla: "闪烁",
  shieldforger: "盾牌铸造者",
}));

const tokenNames = [
  ["grandmaster", "宗师"], ["master", "大师"], ["commander", "指挥官"],
  ["champion", "冠军"], ["gladiator", "角斗士"], ["guardian", "守护者"],
  ["defender", "防御者"], ["protector", "保护者"], ["keeper", "守望者"],
  ["archer", "弓手"], ["ranger", "游侠"], ["caster", "施法者"],
  ["warrior", "战士"], ["soldier", "士兵"], ["squire", "侍从"],
  ["oracle", "神谕者"], ["seer", "先知"], ["alchemist", "炼金术士"],
  ["geomancer", "地脉术士"], ["tethermancer", "束缚术士"],
  ["spellblade", "法刃"], ["spelleater", "噬法者"], ["spell", "法术"],
  ["stormblade", "风暴刃"], ["windblade", "风刃"], ["warblade", "战刃"],
  ["blade", "刃"], ["sword", "剑"], ["dagger", "匕首"], ["hammer", "战锤"],
  ["lancer", "枪兵"], ["shield", "盾牌"], ["forger", "铸造者"],
  ["dragon", "龙"], ["dragoon", "龙骑兵"], ["drake", "幼龙"],
  ["lioness", "雌狮"], ["lion", "狮"], ["panther", "豹"], ["panda", "熊猫"],
  ["bear", "熊"], ["wolf", "狼"], ["hawk", "鹰"], ["fox", "狐"],
  ["wing", "翼"], ["beast", "野兽"], ["monster", "怪物"], ["demon", "恶魔"],
  ["golem", "魔像"], ["mech", "机械"], ["treant", "树人"], ["wisp", "精灵"],
  ["elemental", "元素"], ["sunsteel", "日钢"], ["sunstone", "日石"],
  ["sunforge", "日铸"], ["sunbreak", "破日"], ["sunrise", "日升"],
  ["sunset", "日落"], ["sun", "太阳"], ["moon", "月"], ["bloodstone", "血石"],
  ["ruby", "红宝石"], ["sapphire", "蓝宝石"], ["azurite", "天蓝石"],
  ["crystal", "水晶"], ["diamond", "钻石"], ["silver", "白银"],
  ["golden", "黄金"], ["ironcliffe", "铁崖"], ["iron", "铁"], ["steel", "钢"],
  ["stone", "石"], ["boulder", "巨石"], ["sand", "沙"], ["magma", "熔岩"],
  ["water", "水"], ["fire", "火"], ["flame", "火焰"], ["ice", "冰"],
  ["frost", "霜"], ["thunder", "雷霆"], ["storm", "风暴"], ["wind", "风"],
  ["sky", "天空"], ["void", "虚空"], ["shadow", "暗影"], ["dark", "黑暗"],
  ["night", "夜"], ["soul", "灵魂"], ["spirit", "灵魂"], ["death", "死亡"],
  ["chaos", "混沌"], ["harmony", "和谐"], ["mercsworn", "誓约佣兵"],
  ["merc", "佣兵"], ["tribal", "部族"], ["veteran", "老兵"],
  ["common", "普通"], ["epic", "史诗"], ["legendary", "传奇"],
  ["alt", "替补"], ["tier", "阶"], ["copy", "副本"], ["festive", "节庆"],
  ["mk2", "二型"], ["super", "超型"], ["prime", "主宰"], ["chassis", "底盘"],
  ["helm", "头盔"], ["wings", "翼"], ["idol", "神像"], ["tyrant", "暴君"],
  ["lord", "领主"], ["guard", "守卫"], ["breaker", "破坏者"],
  ["peacekeeper", "维和者"], ["backline", "后排"], ["radiant", "辉光"],
  ["aurora", "极光"], ["auroral", "极光"], ["boreal", "北境"],
].sort((a, b) => b[0].length - a[0].length);

function pngSize(filePath) {
  const buffer = fs.readFileSync(filePath);
  if (buffer.toString("ascii", 1, 4) !== "PNG") {
    throw new Error(`Not a PNG file: ${filePath}`);
  }

  return {
    width: buffer.readUInt32BE(16),
    height: buffer.readUInt32BE(20),
  };
}

function sanitizeId(fileBase) {
  return fileBase.toLowerCase()
    .replace(/\s+copy$/i, "_copy")
    .replace(/\s+/g, "_")
    .replace(/[^a-z0-9_-]+/g, "_")
    .replace(/_+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function escapeTres(value) {
  return String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function titleUnknown(token) {
  return /^\d+$/.test(token) ? token : token.charAt(0).toUpperCase() + token.slice(1);
}

function translateToken(token) {
  if (!token) {
    return "";
  }

  if (exactNames.has(token)) {
    return exactNames.get(token);
  }

  if (/^\d+$/.test(token)) {
    return token;
  }

  let rest = token;
  let result = "";
  while (rest.length > 0) {
    const match = tokenNames.find(([word]) => rest.startsWith(word));
    if (match) {
      result += match[1];
      rest = rest.slice(match[0].length);
      continue;
    }

    const digit = rest.match(/^\d+/);
    if (digit) {
      result += digit[0];
      rest = rest.slice(digit[0].length);
      continue;
    }

    const nextKnown = tokenNames
      .map(([word]) => rest.indexOf(word))
      .filter((index) => index > 0)
      .sort((a, b) => a - b)[0];
    if (nextKnown !== undefined) {
      result += titleUnknown(rest.slice(0, nextKnown));
      rest = rest.slice(nextKnown);
      continue;
    }

    result += titleUnknown(rest);
    break;
  }

  return result;
}

function displayNameFromId(id) {
  const parts = id.split(/[_-]+/).filter(Boolean);
  let prefix = "";
  let startIndex = 0;
  if (parts[0] === "boss") {
    prefix = "首领";
    startIndex = 1;
  } else if (parts[0] === "neutral") {
    prefix = "中立";
    startIndex = 1;
  } else if (/^f\d+$/.test(parts[0])) {
    prefix = `阵营${parts[0].slice(1)}`;
    startIndex = 1;
  } else if (parts[0] === "critter") {
    prefix = "小生物";
    startIndex = 1;
  } else if (parts[0] === "prop") {
    prefix = "场景物件";
    startIndex = 1;
  }

  return `${prefix}${parts.slice(startIndex).map(translateToken).join("") || "单位"}`;
}

function stripPrefix(value, prefix) {
  return value.startsWith(prefix) ? value.slice(prefix.length) : value;
}

function hasChinese(value) {
  return /[\u4e00-\u9fff]/.test(value);
}

function safeFolderName(value) {
  return value
    .replace(/[<>:"/\\|?*]/g, "")
    .replace(/\s+/g, "_")
    .replace(/_+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function packageFolderName(id, displayName) {
  const faction = id.match(/^(f[1-6])_/);
  let prefix = "单位";
  let semantic = displayName;
  if (id.startsWith("boss_")) {
    prefix = "首领";
    semantic = stripPrefix(displayName, "首领");
  } else if (id.startsWith("neutral_")) {
    prefix = "中立";
    semantic = stripPrefix(displayName, "中立");
  } else if (faction) {
    prefix = faction[1];
    semantic = stripPrefix(displayName, `阵营${faction[1].slice(1)}`);
  } else if (id.startsWith("critter_")) {
    prefix = "中立";
    semantic = displayName || "小生物";
  } else if (id.startsWith("prop_")) {
    prefix = "中立";
    semantic = stripPrefix(displayName, "场景物件") || displayName;
  }

  if (semantic && !hasChinese(semantic)) {
    semantic = `异界${semantic}`;
  }

  return safeFolderName(`${prefix}_${semantic || id}`);
}

function pushColumn(col, startRow, endRow, width, height) {
  const frames = [];
  const step = startRow >= endRow ? -1 : 1;
  for (let row = startRow; step < 0 ? row >= endRow : row <= endRow; row += step) {
    const x = col * tileSize;
    const y = row * tileSize;
    if (x + tileSize <= width && y + tileSize <= height) {
      frames.push([x, y]);
    }
  }
  return frames;
}

function fallbackFrames(width, height, maxCount = 8) {
  const frames = [];
  for (let y = 0; y + tileSize <= height && frames.length < maxCount; y += tileSize) {
    for (let x = 0; x + tileSize <= width && frames.length < maxCount; x += tileSize) {
      frames.push([x, y]);
    }
  }
  return frames.length > 0 ? frames : [[0, 0]];
}

function animationCoords(size) {
  const cols = Math.max(1, Math.floor(size.width / tileSize));
  const rows = Math.max(1, Math.floor(size.height / tileSize));
  const lastCol = cols - 1;
  const lastRow = rows - 1;
  const fallback = fallbackFrames(size.width, size.height);
  const attack = cols >= 8
    ? [
      ...pushColumn(7, Math.min(2, lastRow), 0, size.width, size.height),
      ...pushColumn(6, lastRow, 0, size.width, size.height),
      ...pushColumn(5, lastRow, 0, size.width, size.height),
      ...pushColumn(4, lastRow, Math.min(1, lastRow), size.width, size.height),
    ]
    : pushColumn(lastCol, lastRow, 0, size.width, size.height);

  return {
    attack: attack.length > 0 ? attack : fallback,
    defeated: [
      ...pushColumn(Math.min(2, lastCol), lastRow, Math.min(2, lastRow), size.width, size.height),
      ...pushColumn(Math.min(3, lastCol), Math.min(1, lastRow), 0, size.width, size.height),
    ],
    hit: pushColumn(Math.min(3, lastCol), lastRow, 0, size.width, size.height),
    idle: [
      ...pushColumn(Math.min(1, lastCol), lastRow, 0, size.width, size.height),
      [0, lastRow * tileSize],
    ].filter(([x, y]) => x + tileSize <= size.width && y + tileSize <= size.height),
    move: pushColumn(0, Math.max(0, lastRow - 1), 0, size.width, size.height),
  };
}

function framesTres(texturePath, size) {
  const animations = animationCoords(size);
  const frameRefs = [];
  const specs = [
    ["attack", false, 10],
    ["defeated", false, 6],
    ["hit", false, 10],
    ["idle", true, 5],
    ["move", true, 8],
  ];

  const animationBlocks = specs.map(([name, loop, speed]) => {
    const frames = (animations[name].length > 0 ? animations[name] : fallbackFrames(size.width, size.height))
      .map(([x, y]) => {
        const id = `AtlasTexture_${String(frameRefs.length).padStart(3, "0")}`;
        frameRefs.push({ id, x, y });
        return `{\n"duration": 1.0,\n"texture": SubResource("${id}")\n}`;
      });
    return `{\n"frames": [${frames.join(", ")}],\n"loop": ${loop ? "true" : "false"},\n"name": &"${name}",\n"speed": ${speed.toFixed(1)}\n}`;
  });

  const subResources = frameRefs.map((frame) => `[sub_resource type="AtlasTexture" id="${frame.id}"]\natlas = ExtResource("1_texture")\nregion = Rect2(${frame.x}, ${frame.y}, ${tileSize}, ${tileSize})\n`).join("\n");
  return `[gd_resource type="SpriteFrames" load_steps=${frameRefs.length + 2} format=3]\n\n[ext_resource type="Texture2D" path="${escapeTres(texturePath)}" id="1_texture"]\n\n${subResources}\n[resource]\nanimations = [${animationBlocks.join(", ")}]\n`;
}

function formatNumber(value) {
  return Number(value.toFixed(3)).toString();
}

function visualTres(folderPath, id) {
  const targetMaxSize = 40;
  const scale = Math.max(0.12, Math.min(0.45, targetMaxSize / tileSize));
  const scaledHeight = tileSize * scale;
  const offsetY = -Math.max(0, scaledHeight / 2 - 5);
  return `[gd_resource type="Resource" script_class="BattleUnitVisualDefinition" load_steps=4 format=3]\n\n[ext_resource type="Resource" path="${animationSetPath}" id="1_animation_set"]\n[ext_resource type="SpriteFrames" path="${escapeTres(folderPath)}/frames.tres" id="2_frames"]\n[ext_resource type="Script" path="${visualScriptPath}" id="3_definition"]\n\n[resource]\nscript = ExtResource("3_definition")\nSpriteFrames = ExtResource("2_frames")\nAnimationSet = ExtResource("1_animation_set")\nAutoLayoutFromSpriteFrames = true\nTargetMaxSpriteSizePixels = ${formatNumber(targetMaxSize)}\nGroundAnchorOffsetPixels = 5.0\nOffset = Vector2(0, ${formatNumber(offsetY)})\nScale = Vector2(${formatNumber(scale)}, ${formatNumber(scale)})\nModulate = Color(1, 1, 1, 1)\n`;
}

function unitStats(id) {
  if (id.startsWith("boss_")) {
    return { hp: 36, ap: 3, move: 3, damage: 8, range: 1, color: "Color(0.9, 0.28, 0.22, 0.95)" };
  }
  if (id.startsWith("critter_") || id.startsWith("prop_")) {
    return { hp: 6, ap: 1, move: 2, damage: 2, range: 1, color: "Color(0.58, 0.72, 0.56, 0.95)" };
  }
  if (/ranged|archer|caster|seer|oracle/.test(id)) {
    return { hp: 10, ap: 2, move: 3, damage: 4, range: 3, color: "Color(0.48, 0.72, 1, 0.95)" };
  }
  if (/tank|guardian|defender|protector|golem/.test(id)) {
    return { hp: 18, ap: 2, move: 3, damage: 4, range: 1, color: "Color(0.72, 0.72, 0.78, 0.95)" };
  }
  return { hp: 12, ap: 2, move: 3, damage: 4, range: 1, color: "Color(0.55, 0.8, 0.95, 0.95)" };
}

function unitTres(id, displayName, folderPath) {
  const stats = unitStats(id);
  return `[gd_resource type="Resource" script_class="BattleUnitDefinition" load_steps=3 format=3]\n\n[ext_resource type="Script" path="${unitScriptPath}" id="1_definition"]\n[ext_resource type="Resource" path="${escapeTres(folderPath)}/visual.tres" id="2_visual"]\n\n[resource]\nscript = ExtResource("1_definition")\nId = "${escapeTres(id)}"\nDisplayName = "${escapeTres(displayName)}"\nVisual = ExtResource("2_visual")\nDebugMarkerColor = ${stats.color}\nMaxHp = ${stats.hp}\nMaxActionPoints = ${stats.ap}\nMoveRange = ${stats.move}\nMoveActionPointCost = 1\nMaxMoveUsesPerTurn = 1\nAttackDamage = ${stats.damage}\nAttackRange = ${stats.range}\nAttackActionPointCost = 1\nBlocksMovement = true\nIsTargetable = true\n`;
}

function updateImportFile(importPath, newSourcePath) {
  if (!fs.existsSync(importPath)) {
    return;
  }

  const text = fs.readFileSync(importPath, "utf8");
  const next = text.replace(/source_file="[^"]+"/g, `source_file="${newSourcePath}"`);
  fs.writeFileSync(importPath, next, "utf8");
}

fs.mkdirSync(unitRoot, { recursive: true });
const sourceFiles = fs.readdirSync(textureRoot)
  .filter((fileName) => fileName.toLowerCase().endsWith(".png"))
  .sort((a, b) => a.localeCompare(b, "en"));

const usedIds = new Set();
const usedFolders = new Set();
const generated = [];

for (const sourceFileName of sourceFiles) {
  const sourceBaseName = path.basename(sourceFileName, ".png");
  const baseId = sanitizeId(sourceBaseName) || `unit_${generated.length + 1}`;
  let id = baseId;
  for (let index = 2; usedIds.has(id); index += 1) {
    id = `${baseId}_${index}`;
  }
  usedIds.add(id);

  const displayName = displayNameFromId(id);
  const baseFolderName = packageFolderName(id, displayName);
  let folderName = baseFolderName;
  for (let index = 2; usedFolders.has(folderName); index += 1) {
    folderName = `${baseFolderName}_${index}`;
  }
  usedFolders.add(folderName);

  const sourcePngPath = path.join(textureRoot, sourceFileName);
  const sourceImportPath = `${sourcePngPath}.import`;
  const size = pngSize(sourcePngPath);
  const folderFsPath = path.join(unitRoot, folderName);
  const folderResPath = `${resUnitRoot}/${folderName}`;
  const targetPngName = `${id}.png`;
  const targetPngPath = path.join(folderFsPath, targetPngName);
  const targetImportPath = `${targetPngPath}.import`;
  const targetTextureResPath = `${folderResPath}/${targetPngName}`;

  fs.mkdirSync(folderFsPath, { recursive: true });
  fs.renameSync(sourcePngPath, targetPngPath);
  if (fs.existsSync(sourceImportPath)) {
    fs.renameSync(sourceImportPath, targetImportPath);
    updateImportFile(targetImportPath, targetTextureResPath);
  }

  fs.writeFileSync(path.join(folderFsPath, "frames.tres"), framesTres(targetTextureResPath, size), "utf8");
  fs.writeFileSync(path.join(folderFsPath, "visual.tres"), visualTres(folderResPath, id), "utf8");
  fs.writeFileSync(path.join(folderFsPath, "unit.tres"), unitTres(id, displayName, folderResPath), "utf8");
  generated.push({ id, displayName, folderName, sourceFileName, targetPngName, width: size.width, height: size.height });
}

fs.writeFileSync(path.join(unitRoot, "_generated_unit_packages.json"), `${JSON.stringify({
  source: resTextureRoot,
  output: resUnitRoot,
  generatedUnitPackages: generated.length,
  tileSize,
  generated,
}, null, 2)}\n`, "utf8");

console.log(JSON.stringify({
  generatedUnitPackages: generated.length,
  first: generated.slice(0, 3),
  last: generated.slice(-3),
}, null, 2));
