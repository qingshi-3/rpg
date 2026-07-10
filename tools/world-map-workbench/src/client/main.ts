import "ol/ol.css";
import "./styles.css";
import { workbenchApi } from "./api.js";
import { WorkbenchApp } from "./WorkbenchApp.js";

async function start(): Promise<void> {
  const root = document.querySelector<HTMLElement>("#app");
  if (!root) throw new Error("Workbench root element is missing");
  try {
    const bundle = await workbenchApi.loadProject();
    new WorkbenchApp(root, bundle);
  } catch (error) {
    root.innerHTML = `<div class="startup-error"><strong>工作台启动失败</strong><p>${error instanceof Error ? error.message : String(error)}</p><p>请确认本地服务正在 127.0.0.1:4174 运行。</p></div>`;
  }
}

void start();
