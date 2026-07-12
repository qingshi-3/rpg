import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import sharp from "sharp";

const CHUNK_SIZE = 1024;
const TERRAIN_CELL_SIZE = 16;
const COLUMNS = 7;
const ROWS = 5;
const WORLD_WIDTH = COLUMNS * CHUNK_SIZE;
const WORLD_HEIGHT = ROWS * CHUNK_SIZE;
const GRID_WIDTH = WORLD_WIDTH / TERRAIN_CELL_SIZE;
const GRID_HEIGHT = WORLD_HEIGHT / TERRAIN_CELL_SIZE;

const terrainColors = new Map([
  [1, [136, 168, 93]],
  [2, [63, 110, 74]],
  [3, [93, 118, 109]],
  [4, [154, 130, 98]],
  [5, [209, 179, 109]],
  [6, [220, 232, 232]],
  [7, [25, 87, 134]],
]);

const snowPolygon = [[290, 75], [390, 52], [610, 65], [705, 125], [655, 250], [515, 290], [350, 245], [285, 180]];
const desertPolygon = [[240, 305], [390, 280], [545, 315], [640, 405], [710, 565], [660, 705], [500, 748], [350, 675], [240, 535]];
const wetlandPolygon = [[600, 180], [825, 185], [960, 255], [900, 350], [670, 335], [585, 260]];
const purpleRoute = [[515, 150], [650, 145], [790, 155], [900, 175], [1000, 195]];
const blueRoute = [[730, 585], [870, 595], [1030, 590], [1180, 570], [1320, 540]];
const redRoute = [[675, 505], [685, 455], [705, 400], [735, 350], [760, 330]];

function pointInPolygon(x, y, polygon) {
  let inside = false;
  for (let current = 0, previous = polygon.length - 1; current < polygon.length; previous = current++) {
    const [currentX, currentY] = polygon[current];
    const [previousX, previousY] = polygon[previous];
    if (((currentY > y) !== (previousY > y)) && x < ((previousX - currentX) * (y - currentY)) / (previousY - currentY) + currentX) inside = !inside;
  }
  return inside;
}

function rgbToHsv(r, g, b) {
  const red = r / 255;
  const green = g / 255;
  const blue = b / 255;
  const max = Math.max(red, green, blue);
  const min = Math.min(red, green, blue);
  const delta = max - min;
  let hue = 0;
  if (delta !== 0) {
    if (max === red) hue = 60 * (((green - blue) / delta) % 6);
    else if (max === green) hue = 60 * ((blue - red) / delta + 2);
    else hue = 60 * ((red - green) / delta + 4);
  }
  if (hue < 0) hue += 360;
  return { hue, saturation: max === 0 ? 0 : delta / max, value: max };
}

function distanceToSegment(x, y, [startX, startY], [endX, endY]) {
  const deltaX = endX - startX;
  const deltaY = endY - startY;
  const lengthSquared = deltaX * deltaX + deltaY * deltaY;
  const t = lengthSquared === 0 ? 0 : Math.max(0, Math.min(1, ((x - startX) * deltaX + (y - startY) * deltaY) / lengthSquared));
  return Math.hypot(x - (startX + t * deltaX), y - (startY + t * deltaY));
}

function distanceToPolyline(x, y, polyline) {
  let distance = Number.POSITIVE_INFINITY;
  for (let index = 1; index < polyline.length; index += 1) distance = Math.min(distance, distanceToSegment(x, y, polyline[index - 1], polyline[index]));
  return distance;
}

function classifyPixel(r, g, b, sourceX, sourceY) {
  const { hue, saturation, value } = rgbToHsv(r, g, b);
  const blueWater = b > 48 && b > g * 1.08 && b > r * 1.35 && b - Math.max(r, g) > 7 && saturation > 0.25;
  if (blueWater) return 7;
  if (pointInPolygon(sourceX, sourceY, snowPolygon)) return 6;
  if (pointInPolygon(sourceX, sourceY, desertPolygon)) return value < 0.36 || saturation < 0.18 ? 4 : 5;
  if (value < 0.28 && saturation < 0.35) return 4;
  if ((hue >= 65 && hue <= 155 && value < 0.46) || (g > r * 1.05 && value < 0.5)) return 2;
  if (hue >= 25 && hue < 65 && saturation > 0.25 && r > g * 1.08) return 4;
  return 1;
}

function repairRouteArtifact(grid, polyline, sourceRadius, sourceWidth, sourceHeight, renderedGridHeight, topCells) {
  const result = new Uint8Array(grid);
  for (let y = topCells; y < topCells + renderedGridHeight; y += 1) {
    for (let x = 0; x < GRID_WIDTH; x += 1) {
      const sourceX = x * sourceWidth / GRID_WIDTH;
      const sourceY = (y - topCells) * sourceHeight / renderedGridHeight;
      if (distanceToPolyline(sourceX, sourceY, polyline) > sourceRadius) continue;
      const counts = new Map();
      for (let offsetY = -7; offsetY <= 7; offsetY += 1) {
        for (let offsetX = -7; offsetX <= 7; offsetX += 1) {
          const neighborX = x + offsetX;
          const neighborY = y + offsetY;
          if (neighborX < 0 || neighborY < topCells || neighborX >= GRID_WIDTH || neighborY >= topCells + renderedGridHeight) continue;
          const neighborSourceX = neighborX * sourceWidth / GRID_WIDTH;
          const neighborSourceY = (neighborY - topCells) * sourceHeight / renderedGridHeight;
          if (distanceToPolyline(neighborSourceX, neighborSourceY, polyline) < sourceRadius * 1.35) continue;
          const terrainId = grid[neighborY * GRID_WIDTH + neighborX];
          counts.set(terrainId, (counts.get(terrainId) ?? 0) + 1);
        }
      }
      let selected = grid[y * GRID_WIDTH + x];
      let selectedCount = 0;
      for (const [terrainId, count] of counts) {
        if (count > selectedCount) {
          selected = terrainId;
          selectedCount = count;
        }
      }
      result[y * GRID_WIDTH + x] = selected;
    }
  }
  return result;
}

function smoothLand(grid) {
  const result = new Uint8Array(grid);
  for (let y = 1; y < GRID_HEIGHT - 1; y += 1) {
    for (let x = 1; x < GRID_WIDTH - 1; x += 1) {
      const index = y * GRID_WIDTH + x;
      if (grid[index] === 7) continue;
      const counts = new Map();
      for (let offsetY = -1; offsetY <= 1; offsetY += 1) {
        for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
          const terrainId = grid[(y + offsetY) * GRID_WIDTH + x + offsetX];
          if (terrainId === 7) continue;
          counts.set(terrainId, (counts.get(terrainId) ?? 0) + 1);
        }
      }
      let selected = grid[index];
      let selectedCount = 0;
      for (const [terrainId, count] of counts) {
        if (count > selectedCount) {
          selected = terrainId;
          selectedCount = count;
        }
      }
      result[index] = selected;
    }
  }
  return result;
}

function removeSmallComponents(grid, threshold) {
  const result = new Uint8Array(grid);
  const visited = new Uint8Array(grid.length);
  const neighbors = [[-1, 0], [1, 0], [0, -1], [0, 1]];
  for (let start = 0; start < grid.length; start += 1) {
    if (visited[start]) continue;
    const terrainId = grid[start];
    const queue = [start];
    const component = [];
    visited[start] = 1;
    for (let cursor = 0; cursor < queue.length; cursor += 1) {
      const index = queue[cursor];
      component.push(index);
      const x = index % GRID_WIDTH;
      const y = Math.floor(index / GRID_WIDTH);
      for (const [offsetX, offsetY] of neighbors) {
        const neighborX = x + offsetX;
        const neighborY = y + offsetY;
        if (neighborX < 0 || neighborY < 0 || neighborX >= GRID_WIDTH || neighborY >= GRID_HEIGHT) continue;
        const neighborIndex = neighborY * GRID_WIDTH + neighborX;
        if (!visited[neighborIndex] && grid[neighborIndex] === terrainId) {
          visited[neighborIndex] = 1;
          queue.push(neighborIndex);
        }
      }
    }
    if (component.length > threshold) continue;
    const borderCounts = new Map();
    for (const index of component) {
      const x = index % GRID_WIDTH;
      const y = Math.floor(index / GRID_WIDTH);
      for (const [offsetX, offsetY] of neighbors) {
        const neighborX = x + offsetX;
        const neighborY = y + offsetY;
        if (neighborX < 0 || neighborY < 0 || neighborX >= GRID_WIDTH || neighborY >= GRID_HEIGHT) continue;
        const neighborId = grid[neighborY * GRID_WIDTH + neighborX];
        if (neighborId !== terrainId) borderCounts.set(neighborId, (borderCounts.get(neighborId) ?? 0) + 1);
      }
    }
    let replacement = terrainId;
    let replacementCount = 0;
    for (const [neighborId, count] of borderCounts) {
      if (count > replacementCount) {
        replacement = neighborId;
        replacementCount = count;
      }
    }
    for (const index of component) result[index] = replacement;
  }
  return result;
}

function addWetlands(grid, sourceWidth, sourceHeight, topCells) {
  const result = new Uint8Array(grid);
  for (let y = 2; y < GRID_HEIGHT - 2; y += 1) {
    for (let x = 2; x < GRID_WIDTH - 2; x += 1) {
      const index = y * GRID_WIDTH + x;
      if (grid[index] !== 1 && grid[index] !== 2) continue;
      const sourceX = x * sourceWidth / GRID_WIDTH;
      const sourceY = (y - topCells) * sourceHeight / (GRID_HEIGHT - topCells * 2);
      if (!pointInPolygon(sourceX, sourceY, wetlandPolygon)) continue;
      let nearWater = false;
      for (let offsetY = -2; offsetY <= 2 && !nearWater; offsetY += 1) {
        for (let offsetX = -2; offsetX <= 2; offsetX += 1) {
          if (grid[(y + offsetY) * GRID_WIDTH + x + offsetX] === 7) {
            nearWater = true;
            break;
          }
        }
      }
      if (nearWater) result[index] = 3;
    }
  }
  return result;
}

function mapPoint([x, y], sourceWidth, sourceHeight) {
  return [Math.round(x * WORLD_WIDTH / sourceWidth), Math.round(y * WORLD_HEIGHT / sourceHeight)];
}

function lineFeature(featureId, featureType, name, coordinates, sourceWidth, sourceHeight, extra) {
  return {
    type: "Feature",
    geometry: { type: "LineString", coordinates: coordinates.map((point) => mapPoint(point, sourceWidth, sourceHeight)) },
    properties: { featureId, featureType, name, ...extra },
  };
}

function buildGeography(sourceWidth, sourceHeight) {
  const rivers = [
    ["river_northern_lakes", "北部湖河", [[1036, 80], [1045, 115], [1025, 145], [990, 165], [950, 195], [910, 225], [875, 250]], { widthClass: 4 }],
    ["river_central_west", "中央西河", [[650, 165], [657, 205], [680, 235], [725, 255], [775, 282], [835, 286], [875, 250]], { widthClass: 4 }],
    ["river_snowmelt", "雪原融水河", [[530, 215], [565, 230], [600, 245], [625, 275], [650, 285], [680, 270]], { widthClass: 2 }],
    ["river_central_branch", "中央支流", [[835, 214], [825, 250], [840, 282], [820, 315], [835, 350]], { widthClass: 2 }],
    ["river_western_spine", "西部纵河", [[475, 325], [510, 355], [535, 400], [570, 455], [610, 505], [650, 545], [690, 585]], { widthClass: 4 }],
    ["river_eastern_valley", "东部谷河", [[1315, 240], [1322, 285], [1330, 335], [1360, 385], [1348, 440], [1368, 500], [1360, 565]], { widthClass: 4 }],
    ["river_southern_forest", "南部林河", [[690, 585], [760, 610], [840, 630], [930, 640], [1030, 625], [1140, 600], [1260, 565]], { widthClass: 3 }],
  ].map(([id, name, coordinates, extra]) => lineFeature(id, "river", name, coordinates, sourceWidth, sourceHeight, extra));

  const mountains = [
    ["mountain_snow_crown", "雪冠山脉", [[320, 120], [390, 82], [470, 88], [550, 100], [625, 125], [685, 165]], 0.85],
    ["mountain_snow_south", "雪原南缘", [[355, 180], [420, 205], [480, 225], [535, 250], [590, 280], [630, 315]], 0.72],
    ["mountain_western_spine", "西部中轴山脉", [[500, 205], [525, 260], [548, 320], [570, 375], [600, 430], [635, 490], [675, 555], [710, 605]], 0.9],
    ["mountain_desert_wall", "荒漠东壁", [[430, 310], [480, 350], [520, 405], [555, 465], [595, 525], [640, 590], [680, 655]], 0.78],
    ["mountain_desert_ridges", "西南荒漠山群", [[275, 350], [340, 385], [410, 430], [480, 490], [545, 555], [610, 620]], 0.62],
    ["mountain_eastern_spine", "东部纵贯山脉", [[1115, 95], [1155, 150], [1190, 210], [1215, 275], [1235, 345], [1260, 410], [1290, 475], [1325, 545], [1370, 610]], 0.92],
    ["mountain_eastern_branch", "东北支脉", [[1210, 125], [1265, 155], [1320, 190], [1360, 235]], 0.7],
    ["mountain_northern_gate", "北境山口", [[940, 80], [1000, 78], [1060, 95], [1120, 125]], 0.68],
    ["mountain_southern_hills", "南部林地丘陵", [[700, 600], [790, 630], [885, 650], [980, 650], [1080, 630], [1170, 605], [1260, 570]], 0.42],
    ["mountain_west_island", "西海岛山", [[65, 125], [105, 145], [135, 185]], 0.58],
    ["mountain_southwest_island", "西南岛山", [[120, 590], [150, 630], [175, 690]], 0.62],
    ["mountain_east_island", "东海岛山", [[1585, 220], [1630, 245], [1675, 285]], 0.56],
    ["mountain_southeast_island", "东南岛山", [[1580, 620], [1630, 655], [1680, 700]], 0.66],
  ].map(([id, name, coordinates, density]) => lineFeature(id, "mountain", name, coordinates, sourceWidth, sourceHeight, { density }));

  return {
    version: 1,
    linearFeatures: { type: "FeatureCollection", features: [...rivers, ...mountains] },
    waterAnchors: { type: "FeatureCollection", features: [] },
    strategicLocations: { type: "FeatureCollection", features: [] },
    regions: { type: "FeatureCollection", features: [] },
  };
}

async function atomicWriteJson(filePath, value) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  const temporaryPath = `${filePath}.${process.pid}.tmp`;
  await fs.writeFile(temporaryPath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  await fs.rename(temporaryPath, filePath);
}

async function buildTerrain(sourcePath) {
  const metadata = await sharp(sourcePath).metadata();
  if (!metadata.width || !metadata.height) throw new Error("Reference image has no dimensions");
  const renderedGridHeight = GRID_HEIGHT;
  const topCells = 0;
  const resized = await sharp(sourcePath).resize(GRID_WIDTH, renderedGridHeight, { fit: "fill", kernel: "lanczos3" }).removeAlpha().raw().toBuffer();
  const terrain = new Uint8Array(GRID_WIDTH * GRID_HEIGHT).fill(7);
  for (let y = 0; y < renderedGridHeight; y += 1) {
    for (let x = 0; x < GRID_WIDTH; x += 1) {
      const sourceIndex = (y * GRID_WIDTH + x) * 3;
      const targetY = y + topCells;
      const sourceX = x * metadata.width / GRID_WIDTH;
      const sourceY = y * metadata.height / renderedGridHeight;
      terrain[targetY * GRID_WIDTH + x] = classifyPixel(resized[sourceIndex], resized[sourceIndex + 1], resized[sourceIndex + 2], sourceX, sourceY);
    }
  }
  const withoutPurpleRoute = repairRouteArtifact(terrain, purpleRoute, 20, metadata.width, metadata.height, renderedGridHeight, topCells);
  const withoutBlueRoute = repairRouteArtifact(withoutPurpleRoute, blueRoute, 22, metadata.width, metadata.height, renderedGridHeight, topCells);
  const withoutRoutes = repairRouteArtifact(withoutBlueRoute, redRoute, 18, metadata.width, metadata.height, renderedGridHeight, topCells);
  const smoothed = smoothLand(smoothLand(withoutRoutes));
  const wetlands = addWetlands(smoothed, metadata.width, metadata.height, topCells);
  let cleaned = wetlands;
  for (let pass = 0; pass < 6; pass += 1) cleaned = removeSmallComponents(cleaned, 6);
  return {
    metadata,
    terrain: cleaned,
    renderedGridHeight,
    topCells,
  };
}

async function writePreview(terrain, previewPath) {
  const rgb = Buffer.alloc(GRID_WIDTH * GRID_HEIGHT * 3);
  for (let index = 0; index < terrain.length; index += 1) {
    const color = terrainColors.get(terrain[index]) ?? [255, 0, 255];
    rgb[index * 3] = color[0];
    rgb[index * 3 + 1] = color[1];
    rgb[index * 3 + 2] = color[2];
  }
  await sharp(rgb, { raw: { width: GRID_WIDTH, height: GRID_HEIGHT, channels: 3 } })
    .resize(GRID_WIDTH * 3, GRID_HEIGHT * 3, { kernel: "nearest" })
    .png()
    .toFile(previewPath);
}

async function writeReferenceChunks(sourcePath, projectRoot) {
  const referenceBuffer = await sharp(sourcePath)
    .resize(WORLD_WIDTH, WORLD_HEIGHT, { fit: "fill", kernel: "lanczos3" })
    .png({ compressionLevel: 6 })
    .toBuffer();
  const referenceDirectory = path.join(projectRoot, "assets", "textures", "world", "reference", "sample-world-stretched");
  await fs.mkdir(referenceDirectory, { recursive: true });
  for (let y = 0; y < ROWS; y += 1) {
    for (let x = 0; x < COLUMNS; x += 1) {
      const id = `chunk_${x}_${y}`;
      await sharp(referenceBuffer)
        .extract({ left: x * CHUNK_SIZE, top: y * CHUNK_SIZE, width: CHUNK_SIZE, height: CHUNK_SIZE })
        .png({ compressionLevel: 8 })
        .toFile(path.join(referenceDirectory, `${id}.png`));
    }
  }
}

async function applyWorld(sourcePath, projectRoot, metadata, terrain) {
  const projectPath = path.join(projectRoot, "config", "world", "workbench.project.json");
  const geographyPath = path.join(projectRoot, "config", "world", "geography.json");
  const project = JSON.parse(await fs.readFile(projectPath, "utf8"));
  project.world = { width: WORLD_WIDTH, height: WORLD_HEIGHT };
  project.chunk = { ...project.chunk, width: CHUNK_SIZE, height: CHUNK_SIZE, terrainCellSize: TERRAIN_CELL_SIZE };
  const ocean = { id: 7, key: "ocean", label: "海洋与湖泊", color: "#195786" };
  project.terrainTypes = [...project.terrainTypes.filter((terrainType) => terrainType.id !== ocean.id), ocean];
  project.layers = project.layers.map((layer) => layer.id === "reference-map"
    ? { ...layer, visible: true, opacity: 0.92 }
    : layer.id === "terrain"
      ? { ...layer, visible: true, opacity: 0.42 }
      : layer);
  project.chunks = [];
  for (let y = 0; y < ROWS; y += 1) {
    for (let x = 0; x < COLUMNS; x += 1) {
      const id = `chunk_${x}_${y}`;
      project.chunks.push({
        id,
        coordinate: [x, y],
        worldOrigin: [x * CHUNK_SIZE, y * CHUNK_SIZE],
        referenceTexturePath: `reference/sample-world-stretched/${id}.png`,
        terrainMaskPath: `masks/terrain/${id}.png`,
        territoryMaskPath: `masks/territory/${id}.png`,
        navigationScenePath: `res://scenes/world/navigation/${id}.tscn`,
      });
    }
  }

  const terrainDirectory = path.join(projectRoot, "assets", "textures", "world", "masks", "terrain");
  await fs.mkdir(terrainDirectory, { recursive: true });
  await writeReferenceChunks(sourcePath, projectRoot);

  for (let y = 0; y < ROWS; y += 1) {
    for (let x = 0; x < COLUMNS; x += 1) {
      const id = `chunk_${x}_${y}`;
      const chunkCells = Buffer.alloc(CHUNK_SIZE / TERRAIN_CELL_SIZE * (CHUNK_SIZE / TERRAIN_CELL_SIZE));
      for (let localY = 0; localY < CHUNK_SIZE / TERRAIN_CELL_SIZE; localY += 1) {
        const sourceStart = (y * (CHUNK_SIZE / TERRAIN_CELL_SIZE) + localY) * GRID_WIDTH + x * (CHUNK_SIZE / TERRAIN_CELL_SIZE);
        terrain.subarray(sourceStart, sourceStart + CHUNK_SIZE / TERRAIN_CELL_SIZE).forEach((value, localX) => {
          chunkCells[localY * (CHUNK_SIZE / TERRAIN_CELL_SIZE) + localX] = value;
        });
      }
      await sharp(chunkCells, { raw: { width: CHUNK_SIZE / TERRAIN_CELL_SIZE, height: CHUNK_SIZE / TERRAIN_CELL_SIZE, channels: 1 } })
        .png({ compressionLevel: 9 })
        .toFile(path.join(terrainDirectory, `${id}.png`));
    }
  }

  await atomicWriteJson(projectPath, project);
  await atomicWriteJson(geographyPath, buildGeography(metadata.width, metadata.height));
}

const sourcePath = process.argv[2];
const projectRoot = process.argv[3];
const apply = process.argv.includes("--apply");
const referenceOnly = process.argv.includes("--reference-only");
if (!sourcePath || !projectRoot) throw new Error("Usage: node scripts/seed-sample-world.mjs <source-image> <project-root> [--apply]");

if (referenceOnly) {
  await writeReferenceChunks(sourcePath, projectRoot);
  console.log(JSON.stringify({ referenceOnly: true, sourcePath, chunks: [COLUMNS, ROWS] }, null, 2));
  process.exit(0);
}

const { metadata, terrain } = await buildTerrain(sourcePath);
const previewPath = path.join(os.tmpdir(), "rpg-sample-world-terrain-preview.png");
await writePreview(terrain, previewPath);
if (apply) await applyWorld(sourcePath, projectRoot, metadata, terrain);

const counts = new Map();
for (const terrainId of terrain) counts.set(terrainId, (counts.get(terrainId) ?? 0) + 1);
console.log(JSON.stringify({ previewPath, apply, world: [WORLD_WIDTH, WORLD_HEIGHT], chunks: [COLUMNS, ROWS], counts: Object.fromEntries(counts) }, null, 2));
