import type { MapCatalog } from "../shared/types.js";

export const LAST_OPENED_MAP_KEY = "rpg.world-map-workbench.last-opened-map-id";

export function selectInitialMapId(catalog: MapCatalog, requestedMapId?: string, lastOpenedMapId?: string): string | undefined {
  const available = new Set(catalog.maps.map((entry) => entry.mapId));
  if (requestedMapId && available.has(requestedMapId)) return requestedMapId;
  if (lastOpenedMapId && available.has(lastOpenedMapId)) return lastOpenedMapId;
  if (catalog.defaultMapId && available.has(catalog.defaultMapId)) return catalog.defaultMapId;
  return undefined;
}
