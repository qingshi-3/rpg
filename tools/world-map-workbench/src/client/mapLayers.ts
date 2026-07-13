import Feature, { type FeatureLike } from "ol/Feature.js";
import type BaseLayer from "ol/layer/Base.js";
import LayerGroup from "ol/layer/Group.js";
import ImageLayer from "ol/layer/Image.js";
import VectorLayer from "ol/layer/Vector.js";
import Polygon from "ol/geom/Polygon.js";
import ImageCanvasSource from "ol/source/ImageCanvas.js";
import ImageStatic from "ol/source/ImageStatic.js";
import VectorSource from "ol/source/Vector.js";
import CircleStyle from "ol/style/Circle.js";
import Fill from "ol/style/Fill.js";
import RegularShape from "ol/style/RegularShape.js";
import Stroke from "ol/style/Stroke.js";
import Style from "ol/style/Style.js";
import Text from "ol/style/Text.js";
import type { LayerId, WorldProject } from "../shared/types.js";
import type { TerrainStore } from "./model/TerrainStore.js";

export interface HoverRegionState {
  locationId?: string;
  provinceId?: string;
}

export interface WorkbenchLayers {
  byId: Map<LayerId, BaseLayer>;
  layerToId: Map<BaseLayer, LayerId>;
  terrainSource: ImageCanvasSource;
  waterSource: VectorSource;
  waterAnchorSource: VectorSource;
  mountainSource: VectorSource;
  roadSource: VectorSource;
  locationSource: VectorSource;
  regionSource: VectorSource;
  regionLayer: VectorLayer;
  cityOutlineSource: VectorSource;
  validationSource: VectorSource;
  clipPreviewSource: VectorSource;
  regionMaskLayer: ImageLayer<ImageStatic>;
  editableVectorLayers: VectorLayer[];
}

function assetUrl(project: WorldProject, relativePath: string): string {
  if (relativePath.startsWith("res://assets/textures/world/")) {
    relativePath = relativePath.slice("res://assets/textures/world/".length);
  } else if (!relativePath.startsWith(`maps/${project.mapId}/`)) {
    relativePath = `maps/${project.mapId}/draft/${relativePath}`;
  }
  return `/project-assets/${relativePath.split("/").map(encodeURIComponent).join("/")}`;
}

function chunkImages(project: WorldProject, pathSelector: (chunk: WorldProject["chunks"][number]) => string | undefined): ImageLayer<ImageStatic>[] {
  return project.chunks.flatMap((chunk) => {
    const imagePath = pathSelector(chunk);
    if (!imagePath) return [];
    const [originX, originY] = chunk.worldOrigin;
    return [new ImageLayer({
      source: new ImageStatic({
        url: assetUrl(project, imagePath),
        imageExtent: [originX, -(originY + project.chunk.height), originX + project.chunk.width, -originY],
      }),
    })];
  });
}

function createTerrainSource(project: WorldProject, terrain: TerrainStore): ImageCanvasSource {
  const colors = new Map(project.terrainTypes.map((type) => [type.id, type.color]));
  return new ImageCanvasSource({
    ratio: 1,
    canvasFunction: (extent, resolution, pixelRatio, size) => {
      const canvas = document.createElement("canvas");
      const [canvasWidth = 1, canvasHeight = 1] = size;
      const [minX = 0, minMapY = 0, maxX = 0, maxMapY = 0] = extent;
      canvas.width = canvasWidth;
      canvas.height = canvasHeight;
      const context = canvas.getContext("2d");
      if (!context) return canvas;
      context.imageSmoothingEnabled = false;
      const gameExtent: [number, number, number, number] = [minX, -maxMapY, maxX, -minMapY];
      const cellSize = project.chunk.terrainCellSize;
      terrain.forEachVisibleCell(gameExtent, (worldX, worldY, terrainId) => {
        if (terrainId === 0) return;
        context.fillStyle = colors.get(terrainId) ?? "#ff2f6d";
        const x = Math.floor(((worldX - gameExtent[0]) / resolution) * pixelRatio);
        const y = Math.floor(((worldY - gameExtent[1]) / resolution) * pixelRatio);
        const cellPixels = Math.max(1, Math.ceil((cellSize / resolution) * pixelRatio));
        context.fillRect(x, y, cellPixels, cellPixels);
      });
      return canvas;
    },
  });
}

function lineStyle(color: string, width: number, dash?: number[]): Style {
  return new Style({ stroke: new Stroke({ color, width, lineDash: dash }) });
}

function locationStyle(feature: FeatureLike): Style {
  const type = String(feature.get("locationType") ?? "main-city");
  const colors: Record<string, string> = {
    "main-city": "#ffd166",
    "auxiliary-city": "#9fd4ff",
    gate: "#f4a261",
    bridge: "#78c6d0",
    ferry: "#54a7b5",
    port: "#2a9d8f",
    ruin: "#a78bca",
    "resource-site": "#7cc576",
  };
  const isCity = type === "main-city" || type === "auxiliary-city";
  return new Style({
    image: isCity
      ? new RegularShape({ points: 4, radius: 10, angle: Math.PI / 4, fill: new Fill({ color: colors[type] }), stroke: new Stroke({ color: "#1c2431", width: 2 }) })
      : new CircleStyle({ radius: 7, fill: new Fill({ color: colors[type] ?? "#ffffff" }), stroke: new Stroke({ color: "#1c2431", width: 2 }) }),
    text: new Text({
      text: String(feature.get("name") ?? feature.get("locationId") ?? ""),
      offsetY: -17,
      font: "600 12px Inter, sans-serif",
      fill: new Fill({ color: "#f5f8ff" }),
      stroke: new Stroke({ color: "#111722", width: 3 }),
    }),
  });
}

function validationStyle(feature: FeatureLike): Style {
  const severity = String(feature.get("severity") ?? "warning");
  const color = severity === "error" ? "#ff5576" : severity === "warning" ? "#ffbd59" : "#5fc7ff";
  return new Style({
    image: new RegularShape({ points: 3, radius: 9, rotation: 0, fill: new Fill({ color }), stroke: new Stroke({ color: "#1b2230", width: 2 }) }),
  });
}

export function createWorkbenchLayers(project: WorldProject, terrain: TerrainStore, hover: () => HoverRegionState): WorkbenchLayers {
  const byId = new Map<LayerId, BaseLayer>();
  const layerToId = new Map<BaseLayer, LayerId>();
  const register = <T extends BaseLayer>(id: LayerId, layer: T): T => {
    byId.set(id, layer);
    layerToId.set(layer, id);
    layer.set("workbenchLayerId", id);
    return layer;
  };

  const referenceGroup = register("reference-map", new LayerGroup({ layers: chunkImages(project, (chunk) => chunk.referenceTexturePath) }));
  const terrainSource = createTerrainSource(project, terrain);
  const terrainLayer = register("terrain", new ImageLayer({ source: terrainSource }));

  const waterSource = new VectorSource();
  const waterLineLayer = new VectorLayer({
    source: waterSource,
    style: (feature) => lineStyle("#4bb6d6", Math.max(2, Number(feature.get("widthClass") ?? 2) * 1.25)),
  });
  waterLineLayer.set("workbenchLayerId", "water");
  const waterAnchorSource = new VectorSource();
  const waterAnchorLayer = new VectorLayer({
    source: waterAnchorSource,
    style: (feature) => {
      const anchorType = String(feature.get("anchorType") ?? "source");
      const color = anchorType === "source" ? "#9ce6ff" : anchorType === "lake" ? "#4bb6d6" : "#66d9c7";
      return new Style({
        image: new RegularShape({ points: anchorType === "source" ? 3 : 6, radius: 7, fill: new Fill({ color }), stroke: new Stroke({ color: "#102331", width: 2 }) }),
        text: new Text({ text: String(feature.get("name") ?? feature.get("anchorId") ?? ""), offsetY: -14, fill: new Fill({ color: "#c9efff" }), stroke: new Stroke({ color: "#0c131c", width: 3 }), font: "10px Inter, sans-serif" }),
      });
    },
  });
  waterAnchorLayer.set("workbenchLayerId", "water");
  const waterGroup = register("water", new LayerGroup({ layers: [waterLineLayer, waterAnchorLayer] }));
  const mountainSource = new VectorSource();
  const mountainLayer = register("mountains", new VectorLayer({
    source: mountainSource,
    style: (feature) => lineStyle("#9b7b5b", 3 + Number(feature.get("density") ?? 0.5) * 5, [10, 5]),
  }));
  const roadSource = new VectorSource();
  const roadLayer = register("roads", new VectorLayer({
    source: roadSource,
    style: (feature) => lineStyle("#e0b36f", 1.5 + Number(feature.get("roadClass") ?? 1), [8, 5]),
  }));
  const locationSource = new VectorSource();
  const locationLayer = register("strategic-locations", new VectorLayer({ source: locationSource, style: locationStyle }));

  const cityOutlineSource = new VectorSource();
  const cityOutlineLayer = new VectorLayer({ source: cityOutlineSource, style: lineStyle("rgba(255,215,112,.95)", 3) });
  const regionSource = new VectorSource();
  const regionLayer = new VectorLayer({
    source: regionSource,
    style: (feature) => {
      const current = hover();
      const locationId = String(feature.get("locationId") ?? "");
      const provinceId = String(feature.get("provinceId") ?? "");
      const exact = locationId !== "" && current.locationId === locationId;
      const sameProvince = provinceId !== "" && current.provinceId === provinceId;
      return new Style({
        fill: new Fill({ color: exact ? "rgba(255,206,92,.35)" : sameProvince ? "rgba(103,177,255,.16)" : "rgba(63,112,168,.035)" }),
        stroke: new Stroke({ color: exact ? "#ffd166" : sameProvince ? "#67b1ff" : "rgba(116,162,214,.28)", width: exact ? 3 : 1.5 }),
      });
    },
  });
  regionLayer.set("workbenchLayerId", "territories");
  const territoryGroup = register("territories", new LayerGroup({ layers: [regionLayer, cityOutlineLayer] }));

  const finalArtGroup = register("final-chunk-art", new LayerGroup({ layers: chunkImages(project, (chunk) => chunk.visualTexturePath) }));
  const regionMaskLayer = register("region-masks", new ImageLayer({
    source: new ImageStatic({
      url: `${assetUrl(project, "regions/territory_mask.png")}?v=${Date.now()}`,
      imageExtent: [0, -project.world.height, project.world.width, 0],
    }),
  }));

  const validationSource = new VectorSource();
  const diagnosticLayer = new VectorLayer({ source: validationSource, style: validationStyle });
  const clipPreviewSource = new VectorSource();
  const clipPreviewLayer = new VectorLayer({
    source: clipPreviewSource,
    style: (feature) => lineStyle(feature.get("featureType") === "river" ? "rgba(119,220,255,.5)" : feature.get("featureType") === "road" ? "rgba(255,211,143,.45)" : "rgba(210,175,140,.45)", 1, [3, 4]),
  });
  const chunkGridSource = new VectorSource();
  for (const chunk of project.chunks) {
    const [x, y] = chunk.worldOrigin;
    const geometry = new Polygon([[
      [x, -y],
      [x + project.chunk.width, -y],
      [x + project.chunk.width, -(y + project.chunk.height)],
      [x, -(y + project.chunk.height)],
      [x, -y],
    ]]);
    const feature = new Feature({ geometry });
    feature.set("chunkId", chunk.id);
    chunkGridSource.addFeature(feature);
  }
  const chunkGridLayer = new VectorLayer({
    source: chunkGridSource,
    style: (feature) => new Style({
      stroke: new Stroke({ color: "rgba(145,183,220,.35)", width: 1, lineDash: [6, 5] }),
      text: new Text({
        text: String(feature.get("chunkId") ?? ""),
        font: "11px ui-monospace, monospace",
        fill: new Fill({ color: "rgba(203,224,247,.7)" }),
        stroke: new Stroke({ color: "rgba(13,20,31,.9)", width: 3 }),
      }),
    }),
  });
  const validationGroup = register("validation", new LayerGroup({ layers: [chunkGridLayer, clipPreviewLayer, diagnosticLayer] }));

  for (const definition of project.layers) {
    const layer = byId.get(definition.id);
    layer?.setVisible(definition.visible);
    layer?.setOpacity(definition.opacity);
  }

  return {
    byId,
    layerToId,
    terrainSource,
    waterSource,
    waterAnchorSource,
    mountainSource,
    roadSource,
    locationSource,
    regionSource,
    regionLayer,
    cityOutlineSource,
    validationSource,
    clipPreviewSource,
    regionMaskLayer,
    editableVectorLayers: [waterLineLayer, waterAnchorLayer, mountainLayer, roadLayer, locationLayer, regionLayer],
  };
}
