import "ol/ol.css";
import "./styles.css";
import { workbenchApi } from "./api.js";
import { LAST_OPENED_MAP_KEY, selectInitialMapId } from "./mapSelection.js";
import { WorkbenchApp } from "./WorkbenchApp.js";

async function start(): Promise<void> {
  const root = document.querySelector<HTMLElement>("#app");
  if (!root) throw new Error("Workbench root element is missing");
  try {
    const requestedMapId = new URLSearchParams(window.location.search).get("mapId") ?? undefined;
    const catalog = await workbenchApi.listMaps();
    const mapId = selectInitialMapId(catalog, requestedMapId, window.localStorage.getItem(LAST_OPENED_MAP_KEY) ?? undefined);
    const bundle = await workbenchApi.loadProject(mapId);
    if (bundle.initialized) window.localStorage.setItem(LAST_OPENED_MAP_KEY, bundle.project.mapId);
    new WorkbenchApp(root, bundle);
  } catch (error) {
    root.innerHTML = `<div class="startup-error"><strong>工作台启动失败</strong><p>${error instanceof Error ? error.message : String(error)}</p><p>请确认本地服务正在 127.0.0.1:4174 运行。</p></div>`;
  }
}

void start();
