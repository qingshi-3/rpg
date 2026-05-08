import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, "..");
const unitRoot = path.join(projectRoot, "assets", "battle", "units");
const resUnitRoot = "res://assets/battle/units";
const defaultPlistRoot = "C:\\Users\\qs\\Desktop\\游戏素材\\duelyst-main (1)\\duelyst-main\\app\\resources\\units";
const plistRoot = process.env.PLIST_ROOT || defaultPlistRoot;

const standardAnimationSpecs = [
  { name: "attack", sourceCues: ["attack"], loop: false, speed: 10 },
  { name: "defeated", sourceCues: ["death", "death2", "die", "explode"], loop: false, speed: 6 },
  { name: "hit", sourceCues: ["hit", "hurt", "damage"], loop: false, speed: 10 },
  { name: "idle", sourceCues: ["idle", "breathing", "breath", "breathe"], loop: true, speed: 5 },
  { name: "move", sourceCues: ["run", "move", "movement", "crawl"], loop: true, speed: 8 },
];

const knownSourceCues = [
  ...new Set(standardAnimationSpecs.flatMap((spec) => spec.sourceCues).concat([
    "breathing",
    "breath",
    "breathe",
    "cast",
    "castLoop",
    "castStart",
    "castend",
    "castendsample",
    "casting",
    "castloop",
    "caststart",
    "open",
    "projectile",
  ])),
].sort((a, b) => b.length - a.length);

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

function xmlDecode(value) {
  return value
    .replace(/&quot;/g, "\"")
    .replace(/&apos;/g, "'")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&amp;/g, "&");
}

function parseFrameRect(value) {
  const match = value.match(/\{\{\s*(-?\d+)\s*,\s*(-?\d+)\s*\}\s*,\s*\{\s*(\d+)\s*,\s*(\d+)\s*\}\s*\}/);
  if (!match) {
    return null;
  }

  return {
    x: Number(match[1]),
    y: Number(match[2]),
    width: Number(match[3]),
    height: Number(match[4]),
  };
}

function parsePoint(value) {
  const match = value.match(/\{\s*(-?\d+)\s*,\s*(-?\d+)\s*\}/);
  if (!match) {
    return null;
  }

  return {
    x: Number(match[1]),
    y: Number(match[2]),
  };
}

function valueForKey(dictText, key) {
  const escapedKey = key.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const stringMatch = dictText.match(new RegExp(`<key>${escapedKey}</key>\\s*<string>([^<]*)</string>`));
  if (stringMatch) {
    return xmlDecode(stringMatch[1]);
  }

  if (dictText.match(new RegExp(`<key>${escapedKey}</key>\\s*<true\\s*/>`))) {
    return true;
  }

  if (dictText.match(new RegExp(`<key>${escapedKey}</key>\\s*<false\\s*/>`))) {
    return false;
  }

  return null;
}

function inferCue(frameName, plistId, unitId) {
  const baseName = frameName.replace(/\.png$/i, "");
  const match = baseName.match(/^(.*)_(\d+)$/);
  if (!match) {
    return {
      cue: "idle",
      frameIndex: 0,
      inferredWithoutCue: true,
      unknownCue: null,
    };
  }

  const prefix = match[1];
  const frameIndex = Number(match[2]);
  const sanitizedPrefix = sanitizeId(prefix);
  const idWithoutCopy = unitId.endsWith("_copy")
    ? unitId.slice(0, -"_copy".length)
    : unitId;

  if (sanitizedPrefix === plistId ||
      sanitizedPrefix === unitId ||
      sanitizedPrefix === idWithoutCopy) {
    return {
      cue: "idle",
      frameIndex,
      inferredWithoutCue: true,
      unknownCue: null,
    };
  }

  for (const cue of knownSourceCues) {
    if (prefix.endsWith(`_${cue}`)) {
      return {
        cue,
        frameIndex,
        inferredWithoutCue: false,
        unknownCue: null,
      };
    }
  }

  const cueMatch = prefix.match(/_([A-Za-z][A-Za-z0-9]*)$/);
  return {
    cue: cueMatch ? cueMatch[1] : "idle",
    frameIndex,
    inferredWithoutCue: !cueMatch,
    unknownCue: cueMatch ? cueMatch[1] : null,
  };
}

function parsePlistFrames(plistPath, plistId, unitId) {
  const text = fs.readFileSync(plistPath, "utf8");
  const frames = [];
  const unsupportedRotatedFrames = [];
  const nonZeroOffsets = [];
  const malformedFrames = [];
  const unknownCues = new Set();
  const noCueFrames = [];
  const entryPattern = /<key>([^<]+\.png)<\/key>\s*<dict>([\s\S]*?)<\/dict>/g;

  for (const match of text.matchAll(entryPattern)) {
    const frameName = xmlDecode(match[1]);
    const dictText = match[2];
    const frameRect = parseFrameRect(valueForKey(dictText, "frame") || "");
    const rotated = valueForKey(dictText, "rotated") === true;
    const offset = parsePoint(valueForKey(dictText, "offset") || "{0,0}");
    const cue = inferCue(frameName, plistId, unitId);

    if (rotated) {
      unsupportedRotatedFrames.push(frameName);
    }

    if (offset && (offset.x !== 0 || offset.y !== 0)) {
      nonZeroOffsets.push({ frameName, offset });
    }

    if (cue.unknownCue) {
      unknownCues.add(cue.unknownCue);
    }

    if (cue.inferredWithoutCue) {
      noCueFrames.push(frameName);
    }

    if (!frameRect) {
      malformedFrames.push(frameName);
      continue;
    }

    frames.push({
      id: `AtlasTexture_${String(frames.length).padStart(3, "0")}`,
      frameName,
      cue: cue.cue,
      frameIndex: cue.frameIndex,
      ...frameRect,
    });
  }

  if (unsupportedRotatedFrames.length > 0) {
    throw new Error(`Unsupported rotated frames in ${plistPath}: ${unsupportedRotatedFrames.slice(0, 5).join(", ")}`);
  }

  return {
    frames,
    unsupportedRotatedFrames,
    nonZeroOffsets,
    malformedFrames,
    unknownCues: [...unknownCues].sort(),
    noCueFrames,
  };
}

function groupFramesByCue(frames) {
  const groups = new Map();
  const cueOrder = [];

  for (const frame of frames) {
    if (!groups.has(frame.cue)) {
      groups.set(frame.cue, []);
      cueOrder.push(frame.cue);
    }

    groups.get(frame.cue).push(frame);
  }

  for (const group of groups.values()) {
    group.sort((a, b) => a.frameIndex - b.frameIndex || a.frameName.localeCompare(b.frameName, "en"));
  }

  return { groups, cueOrder };
}

function extraSpecForCue(cue, frames) {
  const lower = cue.toLowerCase();
  const loop = lower === "idle" ||
    lower === "breathing" ||
    lower === "breath" ||
    lower === "breathe" ||
    lower === "run" ||
    lower === "move" ||
    lower === "movement" ||
    lower === "crawl" ||
    lower === "castloop" ||
    lower === "casting";
  let speed = 10;

  if (lower === "idle" || lower === "breathing" || lower === "breath" || lower === "breathe") {
    speed = 5;
  } else if (lower === "run" || lower === "move" || lower === "movement" || lower === "crawl") {
    speed = 8;
  } else if (lower === "death" || lower === "death2" || lower === "die") {
    speed = 6;
  }

  return {
    name: cue,
    frames,
    loop,
    speed,
    sourceCue: cue,
    isStandardAlias: false,
  };
}

function buildAnimationSpecs(groups, cueOrder) {
  const animations = [];
  const usedAnimationNames = new Set();

  for (const standardSpec of standardAnimationSpecs) {
    const sourceCue = standardSpec.sourceCues.find((candidate) => groups.has(candidate));
    if (!sourceCue) {
      continue;
    }

    animations.push({
      name: standardSpec.name,
      frames: groups.get(sourceCue),
      loop: standardSpec.loop,
      speed: standardSpec.speed,
      sourceCue,
      isStandardAlias: sourceCue !== standardSpec.name,
    });
    usedAnimationNames.add(standardSpec.name);
  }

  for (const cue of cueOrder) {
    if (usedAnimationNames.has(cue)) {
      continue;
    }

    const extraSpec = extraSpecForCue(cue, groups.get(cue));
    animations.push(extraSpec);
    usedAnimationNames.add(cue);
  }

  return animations;
}

function framesToTres(texturePath, frames, animations) {
  const subResources = frames.map((frame) => {
    return `[sub_resource type="AtlasTexture" id="${frame.id}"]\natlas = ExtResource("1_texture")\nregion = Rect2(${frame.x}, ${frame.y}, ${frame.width}, ${frame.height})\n`;
  }).join("\n");

  const animationBlocks = animations.map((animation) => {
    const frameBlocks = animation.frames.map((frame) => {
      return `{\n"duration": 1.0,\n"texture": SubResource("${frame.id}")\n}`;
    });

    return `{\n"frames": [${frameBlocks.join(", ")}],\n"loop": ${animation.loop ? "true" : "false"},\n"name": &"${escapeTres(animation.name)}",\n"speed": ${animation.speed.toFixed(1)}\n}`;
  });

  return `[gd_resource type="SpriteFrames" load_steps=${frames.length + 2} format=3]\n\n` +
    `[ext_resource type="Texture2D" path="${escapeTres(texturePath)}" id="1_texture"]\n\n` +
    `${subResources}\n` +
    `[resource]\nanimations = [${animationBlocks.join(", ")}]\n`;
}

function readUnitId(unitPath) {
  const text = fs.readFileSync(unitPath, "utf8");
  const match = text.match(/^Id = "([^"]+)"/m);
  return match ? match[1] : null;
}

function findPackagePng(packagePath, unitId) {
  const pngFiles = fs.readdirSync(packagePath)
    .filter((fileName) => fileName.toLowerCase().endsWith(".png"))
    .sort((a, b) => a.localeCompare(b, "en"));
  const expected = `${unitId}.png`;
  return pngFiles.includes(expected) ? expected : pngFiles[0];
}

function buildPlistMap() {
  if (!fs.existsSync(plistRoot)) {
    throw new Error(`PLIST_ROOT does not exist: ${plistRoot}`);
  }

  const map = new Map();
  for (const fileName of fs.readdirSync(plistRoot)) {
    if (!fileName.toLowerCase().endsWith(".plist")) {
      continue;
    }

    const plistId = sanitizeId(path.basename(fileName, ".plist"));
    map.set(plistId, {
      id: plistId,
      fileName,
      path: path.join(plistRoot, fileName),
    });
  }

  return map;
}

function resolvePlistForUnit(unitId, plistMap) {
  if (plistMap.has(unitId)) {
    return {
      plist: plistMap.get(unitId),
      matchType: "exact",
    };
  }

  if (unitId.endsWith("_copy")) {
    const baseId = unitId.slice(0, -"_copy".length);
    if (plistMap.has(baseId)) {
      return {
        plist: plistMap.get(baseId),
        matchType: "copy-base",
      };
    }
  }

  return {
    plist: null,
    matchType: "missing",
  };
}

function countBy(items, selector) {
  const result = {};
  for (const item of items) {
    const key = selector(item);
    result[key] = (result[key] || 0) + 1;
  }

  return result;
}

const plistMap = buildPlistMap();
const packageDirs = fs.readdirSync(unitRoot, { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)
  .sort((a, b) => a.localeCompare(b, "zh-Hans-CN"));

const report = {
  generatedAt: new Date().toISOString(),
  plistRoot,
  unitRoot: "res://assets/battle/units",
  packageCount: packageDirs.length,
  plistCount: plistMap.size,
  generatedCount: 0,
  missingUnitId: [],
  missingPng: [],
  missingPlist: [],
  copyBaseMatches: [],
  malformedFrames: [],
  nonZeroOffsets: [],
  unknownCues: [],
  unusedPlists: [],
  missingStandardAnimations: [],
  cueCounts: {},
  packages: [],
};
const usedPlistIds = new Set();

for (const folderName of packageDirs) {
  const packagePath = path.join(unitRoot, folderName);
  const unitPath = path.join(packagePath, "unit.tres");
  if (!fs.existsSync(unitPath)) {
    report.missingUnitId.push({ folderName, reason: "unit.tres missing" });
    continue;
  }

  const unitId = readUnitId(unitPath);
  if (!unitId) {
    report.missingUnitId.push({ folderName, reason: "Id missing" });
    continue;
  }

  const pngFileName = findPackagePng(packagePath, unitId);
  if (!pngFileName) {
    report.missingPng.push({ folderName, unitId });
    continue;
  }

  const { plist, matchType } = resolvePlistForUnit(unitId, plistMap);
  if (!plist) {
    report.missingPlist.push({ folderName, unitId });
    continue;
  }

  const parsed = parsePlistFrames(plist.path, plist.id, unitId);
  if (parsed.malformedFrames.length > 0) {
    report.malformedFrames.push({ folderName, unitId, plist: plist.fileName, frames: parsed.malformedFrames });
  }

  if (parsed.nonZeroOffsets.length > 0) {
    report.nonZeroOffsets.push({ folderName, unitId, plist: plist.fileName, frames: parsed.nonZeroOffsets });
  }

  if (parsed.unknownCues.length > 0) {
    report.unknownCues.push({ folderName, unitId, plist: plist.fileName, cues: parsed.unknownCues });
  }

  if (matchType === "copy-base") {
    report.copyBaseMatches.push({ folderName, unitId, plist: plist.fileName });
  }
  usedPlistIds.add(plist.id);

  const { groups, cueOrder } = groupFramesByCue(parsed.frames);
  const animations = buildAnimationSpecs(groups, cueOrder);
  const texturePath = `${resUnitRoot}/${folderName}/${pngFileName}`;
  const framesTresPath = path.join(packagePath, "frames.tres");
  fs.writeFileSync(framesTresPath, framesToTres(texturePath, parsed.frames, animations), "utf8");

  const standardNames = new Set(animations
    .filter((animation) => standardAnimationSpecs.some((spec) => spec.name === animation.name))
    .map((animation) => animation.name));
  const missingStandard = standardAnimationSpecs
    .map((spec) => spec.name)
    .filter((name) => !standardNames.has(name));
  if (missingStandard.length > 0) {
    report.missingStandardAnimations.push({ folderName, unitId, plist: plist.fileName, missing: missingStandard });
  }

  for (const frame of parsed.frames) {
    report.cueCounts[frame.cue] = (report.cueCounts[frame.cue] || 0) + 1;
  }

  report.packages.push({
    folderName,
    unitId,
    texture: texturePath,
    plist: plist.fileName,
    matchType,
    frameCount: parsed.frames.length,
    sourceCues: cueOrder,
    animations: animations.map((animation) => ({
      name: animation.name,
      sourceCue: animation.sourceCue,
      frameCount: animation.frames.length,
      loop: animation.loop,
      speed: animation.speed,
    })),
    noCueFrameCount: parsed.noCueFrames.length,
  });

  report.generatedCount += 1;
}

report.cueCounts = Object.fromEntries(Object.entries(report.cueCounts).sort((a, b) => a[0].localeCompare(b[0], "en")));
report.unusedPlists = [...plistMap.values()]
  .filter((plist) => !usedPlistIds.has(plist.id))
  .map((plist) => plist.fileName)
  .sort((a, b) => a.localeCompare(b, "en"));
report.summary = {
  generatedCount: report.generatedCount,
  missingPlistCount: report.missingPlist.length,
  missingPngCount: report.missingPng.length,
  unusedPlistCount: report.unusedPlists.length,
  copyBaseMatchCount: report.copyBaseMatches.length,
  unknownCuePackageCount: report.unknownCues.length,
  nonZeroOffsetPackageCount: report.nonZeroOffsets.length,
  missingStandardAnimationPackageCount: report.missingStandardAnimations.length,
  totalFrameCount: report.packages.reduce((sum, item) => sum + item.frameCount, 0),
};

fs.writeFileSync(
  path.join(unitRoot, "_plist_frame_generation_report.json"),
  `${JSON.stringify(report, null, 2)}\n`,
  "utf8",
);

console.log(JSON.stringify(report.summary, null, 2));
