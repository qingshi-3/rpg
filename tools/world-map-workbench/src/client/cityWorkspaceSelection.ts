import type { ProvinceDefinition, StrategicLocationProperties } from "../shared/types.js";

export interface CityWorkspaceSelection {
  provinceId: string;
  locationId: string;
  locationType: "main-city" | "auxiliary-city";
}

function propertyString(properties: Record<string, unknown>, key: string): string {
  const value = properties[key];
  return typeof value === "string" ? value.trim() : "";
}

export function resolveCityWorkspaceSelection(
  properties: Record<string, unknown>,
  provinces: readonly ProvinceDefinition[],
  locations: readonly StrategicLocationProperties[],
): CityWorkspaceSelection | undefined {
  const provinceId = propertyString(properties, "provinceId");
  const locationId = propertyString(properties, "locationId");
  if (!provinceId || !locationId || !provinces.some((province) => province.provinceId === provinceId)) return undefined;

  const location = locations.find((candidate) => candidate.provinceId === provinceId && candidate.locationId === locationId);
  if (!location || (location.locationType !== "main-city" && location.locationType !== "auxiliary-city")) return undefined;

  const selectedType = propertyString(properties, "locationType");
  const isCityMarker = selectedType === "main-city" || selectedType === "auxiliary-city";
  const isCityGeometry = selectedType === "" && propertyString(properties, "direction") !== "";
  if (!isCityMarker && !isCityGeometry) return undefined;

  return { provinceId, locationId, locationType: location.locationType };
}
