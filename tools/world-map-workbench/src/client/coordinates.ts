import type { FeatureCollection, Geometry } from "geojson";
import GeoJSON from "ol/format/GeoJSON.js";
import type Feature from "ol/Feature.js";

function flipGeometryY(geometry: Geometry): Geometry {
  // OpenLayers renders Y upward; game data remains Y-down and is flipped only at the display boundary.
  const clone = structuredClone(geometry);
  const visit = (coordinates: unknown): void => {
    if (!Array.isArray(coordinates)) return;
    if (coordinates.length >= 2 && typeof coordinates[0] === "number" && typeof coordinates[1] === "number") {
      coordinates[1] = -coordinates[1];
      return;
    }
    for (const child of coordinates) visit(child);
  };
  if ("coordinates" in clone) visit(clone.coordinates);
  return clone;
}

export function toMapCoordinate(coordinate: [number, number]): [number, number] {
  return [coordinate[0], -coordinate[1]];
}

export function toGameCoordinate(coordinate: number[]): [number, number] {
  return [coordinate[0] ?? 0, -(coordinate[1] ?? 0)];
}

export function readGameFeatures(collection: FeatureCollection): Feature[] {
  const transformed = structuredClone(collection);
  for (const feature of transformed.features) feature.geometry = flipGeometryY(feature.geometry);
  return new GeoJSON().readFeatures(transformed);
}

export function writeGameFeatures(features: Feature[]): FeatureCollection {
  const collection = new GeoJSON().writeFeaturesObject(features.map((feature) => feature.clone())) as FeatureCollection;
  for (const feature of collection.features) feature.geometry = flipGeometryY(feature.geometry);
  return collection;
}
