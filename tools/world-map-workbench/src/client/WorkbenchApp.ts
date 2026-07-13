import { centroid, featureCollection, point, union } from "@turf/turf";
import type { Feature as GeoFeature, FeatureCollection, LineString, MultiPolygon, Point, Polygon } from "geojson";
import Feature from "ol/Feature.js";
import OlMap from "ol/Map.js";
import View from "ol/View.js";
import Draw from "ol/interaction/Draw.js";
import Modify from "ol/interaction/Modify.js";
import PointerInteraction from "ol/interaction/Pointer.js";
import Select from "ol/interaction/Select.js";
import Snap from "ol/interaction/Snap.js";
import type Interaction from "ol/interaction/Interaction.js";
import ImageStatic from "ol/source/ImageStatic.js";
import VectorSource from "ol/source/Vector.js";
import Projection from "ol/proj/Projection.js";
import { clipLinearFeaturesToChunks, snapRiverEndpoints } from "../shared/geo.js";
import { FIXED_NEW_MAP_CHUNK_SIZE, getProjectGridDimensions, MAX_GRID_AXIS, MAX_GRID_CHUNKS, validateMapGridDimensions } from "../shared/mapGrid.js";
import type {
  GeographyDocument,
  LayerId,
  LinearFeature,
  LinearFeatureProperties,
  LocationGeometryFeature,
  ProvinceDefinition,
  PublishProfile,
  StrategicLocationFeature,
  StrategicLocationProperties,
  StrategicLocationType,
  TerrainChunkPayload,
  ValidationItem,
  WaterAnchorFeature,
  WaterAnchorType,
  WorldProject,
} from "../shared/types.js";
import type { ProjectBundle } from "./api.js";
import { workbenchApi } from "./api.js";
import { readGameFeatures, toGameCoordinate, toMapCoordinate, writeGameFeatures } from "./coordinates.js";
import { resolveCityWorkspaceSelection } from "./cityWorkspaceSelection.js";
import { createWorkbenchLayers, type HoverRegionState, type WorkbenchLayers } from "./mapLayers.js";
import { AuthoringIdentityFactory } from "./model/AuthoringIdentityFactory.js";
import { History } from "./model/History.js";
import { TerrainStore } from "./model/TerrainStore.js";
import { diagnosticPoint, validateWorkbench } from "./validation.js";
import {
  getWorkspaceDefinition,
  toolUiDefinitions,
  workspaceForTool,
  workspaceUiDefinitions,
  type ToolId,
  type WorkspaceId,
} from "./workspaceUi.js";

interface WorkbenchSnapshot {
  geography: GeographyDocument;
  terrainChunks: TerrainChunkPayload[];
  selectedProvinceId: string;
  selectedGeometryLocationId: string;
}

interface PendingCityRegionCreation {
  before: WorkbenchSnapshot;
  location: StrategicLocationProperties;
  provinceWasCreated: boolean;
  previousProvinceId: string;
  previousGeometryLocationId: string;
  previousDirty: boolean;
  previousMutationRevision: number;
  previousLayerVisibility: Partial<Record<LayerId, boolean>>;
}

const toolLayer: Partial<Record<ToolId, LayerId>> = {
  "terrain-brush": "terrain",
  "terrain-erase": "terrain",
  "terrain-fill": "terrain",
  "terrain-lasso": "terrain",
  "terrain-polygon": "terrain",
  river: "water",
  "water-anchor": "water",
  road: "roads",
  mountain: "mountains",
  location: "strategic-locations",
  region: "territories",
};

const layerWorkspace: Partial<Record<LayerId, WorkspaceId>> = {
  terrain: "terrain",
  water: "networks",
  roads: "networks",
  mountains: "networks",
  "strategic-locations": "locations",
  territories: "regions",
  "region-masks": "regions",
  validation: "review",
};

function decodeBase64(value: string): Uint8Array {
  const binary = atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
  return bytes;
}

function escapeHtml(value: unknown): string {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function shortId(prefix: string): string {
  return `${prefix}_${crypto.randomUUID().replaceAll("-", "").slice(0, 10)}`;
}

export class WorkbenchApp {
  private readonly project: WorldProject;
  private readonly terrain: TerrainStore;
  private readonly layers: WorkbenchLayers;
  private readonly map: OlMap;
  private readonly history = new History<WorkbenchSnapshot>(30);
  private readonly select: Select;
  private readonly modify: Modify;
  private activeInteraction?: Interaction;
  private activeTool: ToolId = "terrain-brush";
  private activeWorkspace: WorkspaceId = "terrain";
  private openDrawer?: "layers" | "inspector";
  private validationExpanded = false;
  private dirty = false;
  private saving = false;
  private mutationRevision = 0;
  private readonly expandedLayerIds = new Set<LayerId>();
  private selectedTerrainId = 1;
  private brushRadius = 48;
  private provinces: ProvinceDefinition[] = [];
  private selectedLocationType: StrategicLocationType = "gate";
  private selectedWaterAnchorType: WaterAnchorType = "source";
  private selectedRiverWidthClass = 2;
  private selectedRoadClass = 1;
  private selectedMountainDensity = 0.5;
  private selectedLocationProvinceId = "";
  private selectedGeometryLocationId = "";
  private selectedRegionDirection = "center";
  private hoverRegion: HoverRegionState = {};
  private diagnostics: ValidationItem[] = [];
  private initialized: boolean;
  private mutationBefore?: WorkbenchSnapshot;
  private pendingCityRegion?: PendingCityRegionCreation;
  private resizeObserver?: ResizeObserver;

  public constructor(private readonly root: HTMLElement, bundle: ProjectBundle) {
    this.project = bundle.project;
    this.initialized = bundle.initialized;
    this.terrain = new TerrainStore(this.project);
    for (const chunk of bundle.terrainChunks) this.terrain.loadChunk(chunk.chunkId, decodeBase64(chunk.cellsBase64));
    this.provinces = structuredClone(bundle.geography.provinces);
    this.selectedLocationProvinceId = this.provinces[0]?.provinceId ?? "";
    this.selectedGeometryLocationId = bundle.geography.strategicLocations.features.find((feature) =>
      feature.properties.locationType === "main-city" || feature.properties.locationType === "auxiliary-city")?.properties.locationId ?? "";

    this.renderShell();
    this.layers = createWorkbenchLayers(this.project, this.terrain, () => this.hoverRegion);
    this.loadGeography(bundle.geography);

    const projection = new Projection({
      code: "RPG-WORLD",
      units: "pixels",
      extent: [0, -this.project.world.height, this.project.world.width, 0],
    });
    this.map = new OlMap({
      target: this.requireElement("map"),
      layers: this.project.layers.map((definition) => this.layers.byId.get(definition.id)).filter((layer) => layer !== undefined),
      view: new View({
        projection,
        center: [this.project.world.width / 2, -this.project.world.height / 2],
        extent: [0, -this.project.world.height, this.project.world.width, 0],
        showFullExtent: true,
        zoom: 0,
        minZoom: -3,
        maxZoom: 8,
      }),
    });
    this.map.getView().fit([0, -this.project.world.height, this.project.world.width, 0], { padding: [60, 60, 60, 60] });
    window.addEventListener("resize", () => this.scheduleMapResize());
    if (typeof ResizeObserver !== "undefined") {
      this.resizeObserver = new ResizeObserver(() => this.scheduleMapResize());
      this.resizeObserver.observe(this.requireElement("map-stage"));
    }

    this.select = new Select({
      layers: this.layers.editableVectorLayers,
      filter: (feature) => {
        const id = this.featureLayerId(feature);
        return id !== undefined && !this.getLayerDefinition(id).locked;
      },
    });
    this.modify = new Modify({ features: this.select.getFeatures() });
    this.map.addInteraction(this.select);
    this.map.addInteraction(this.modify);
    this.select.on("select", () => this.handleMapSelectionChanged());
    this.modify.on("modifystart", () => { this.mutationBefore = this.captureSnapshot(); });
    this.modify.on("modifyend", () => {
      for (const feature of this.select.getFeatures().getArray()) {
        if (feature.get("featureType") === "river") this.snapRiverFeature(feature);
      }
      this.finishMutation();
    });
    for (const source of [this.layers.waterSource, this.layers.waterAnchorSource, this.layers.roadSource, this.layers.mountainSource, this.layers.locationSource, this.layers.regionSource]) {
      this.map.addInteraction(new Snap({ source }));
    }

    this.bindUi();
    this.renderWorkspaceNavigation();
    this.updateLayerOrder();
    this.refreshCityOutlines();
    this.refreshChunkClips();
    this.runValidation();
    this.activateWorkspace("terrain");
    this.renderLayers();
    this.renderProperties();
    this.setDirty(false);
    this.updateHistoryButtons();
    void this.initializeMapControls();
    this.setStatus(this.initialized ? "项目已加载" : "尚未初始化；首次保存时创建 config/world", "info");
  }

  private renderShell(): void {
    const grid = getProjectGridDimensions(this.project);
    this.root.innerHTML = `
      <div class="workbench-shell" id="workbench-shell">
        <header class="toolbar">
          <div class="brand">
            <span class="brand-mark">界</span>
            <div><strong>大世界地理工作台</strong><small>${escapeHtml(this.project.displayName)}</small></div>
          </div>
          <div class="toolbar-context">
            <span>当前工作流</span>
            <strong id="toolbar-workspace-title">01 · 地貌绘制</strong>
          </div>
          <div class="toolbar-actions">
            <select id="map-selector" class="compact-select" title="MapId"><option value="${escapeHtml(this.project.mapId)}">${escapeHtml(this.project.mapId)}</option></select>
            <button class="quiet" id="new-map">新建地图</button>
            <button class="quiet" id="duplicate-map">复制地图</button>
            <button class="quiet" id="map-settings">地图设置</button>
            <select id="publish-profile" class="compact-select" title="发布能力"><option value="region-interactive">区域交互包</option><option value="strategic-runtime">战略运行包</option></select>
            <button class="quiet" id="publish-map">发布</button>
            <span class="save-state" id="save-state"><i></i><span>已保存</span></span>
            <button class="quiet" id="undo" title="撤销 Ctrl+Z">撤销</button>
            <button class="quiet" id="redo" title="重做 Ctrl+Y">重做</button>
            <button class="quiet" id="open-layers" aria-expanded="false">图层</button>
            <button class="primary" id="save" title="保存 Ctrl+S">保存修改</button>
          </div>
        </header>
        <aside class="authoring-sidebar panel">
          <nav class="workspace-rail" aria-label="制作工作流">
            <div class="workspace-rail-title">制作</div>
            <div id="workspace-list" class="workspace-list"></div>
          </nav>
          <section class="context-panel" id="context-panel" aria-label="当前工作流工具">
            <div id="context-panel-body" class="context-panel-body"></div>
          </section>
        </aside>
        <main class="map-stage" id="map-stage">
          <div id="map" class="map"></div>
          <div id="operation-status" class="operation-status info">正在载入</div>
        </main>
        <aside class="side-drawer layer-drawer" id="layer-drawer" aria-hidden="true">
          <div class="drawer-heading">
            <div><span>显示设置</span><strong>图层</strong><small>越靠上，绘制层级越高</small></div>
            <button class="icon-button" id="close-layers" aria-label="关闭图层">×</button>
          </div>
          <div id="layer-list" class="layer-list"></div>
        </aside>
        <aside class="side-drawer inspector-drawer" id="inspector-drawer" aria-hidden="true">
          <div class="drawer-heading">
            <div><span>当前选择</span><strong>对象属性</strong><small>修改会立即进入撤销历史</small></div>
            <div class="drawer-heading-actions"><button class="quiet" id="clear-selection">清除选择</button><button class="icon-button" id="close-inspector" aria-label="关闭属性">×</button></div>
          </div>
          <div id="property-panel" class="property-panel"></div>
        </aside>
        <footer class="bottom-panel panel" id="validation-panel">
          <div class="validation-header">
            <div class="current-tool-status"><span>当前工具</span><strong id="current-tool-status">地貌笔刷</strong></div>
            <div class="map-readout" aria-label="地图指针信息"><span id="coordinate-status">X 0 · Y 0</span><span id="terrain-status">地貌 —</span><span id="region-status">Region —</span></div>
            <div class="validation-actions">
              <span class="validation-summary" id="validation-summary">0 错误 · 0 警告</span>
              <button class="quiet" id="validate">重新检查</button>
              <button id="validation-toggle" aria-expanded="false"><span id="validation-toggle-label">查看问题</span><span class="count-badge" id="validation-count">0</span></button>
            </div>
          </div>
          <div id="validation-list" class="validation-list"></div>
        </footer>
      </div>
      <dialog class="workbench-dialog" id="new-map-dialog" aria-labelledby="new-map-title">
        <form id="new-map-form">
          <header><span>地图文档</span><strong id="new-map-title">新建地图</strong><p>Chunk 结构由地图级网格统一生成，创建后 MapId 不可修改。</p></header>
          <div class="dialog-fields">
            <label><span>MapId <em>不可变</em></span><input id="new-map-id" name="mapId" required minlength="2" maxlength="64" pattern="[a-z0-9][a-z0-9_-]{1,63}" value="new_map"><small>小写字母、数字、下划线或连字符</small></label>
            <label><span>显示名称</span><input id="new-map-name" name="displayName" required value="新地图"></label>
            <div class="dialog-grid-fields">
              <label><span>Chunk 列数</span><input id="new-map-columns" name="columns" type="number" min="1" max="${MAX_GRID_AXIS}" step="1" required value="4"></label>
              <label><span>Chunk 行数</span><input id="new-map-rows" name="rows" type="number" min="1" max="${MAX_GRID_AXIS}" step="1" required value="2"></label>
            </div>
          </div>
          <div class="grid-preview"><span>固定 Chunk</span><strong>${FIXED_NEW_MAP_CHUNK_SIZE} × ${FIXED_NEW_MAP_CHUNK_SIZE}</strong><span>世界总尺寸</span><strong id="new-map-world-size">4096 × 2048</strong><small>最多 ${MAX_GRID_CHUNKS} 个 Chunk；提交前会完整校验，失败不会创建目录。</small></div>
          <div class="dialog-error" id="new-map-error" role="alert"></div>
          <footer><button type="button" class="quiet" id="cancel-new-map">取消</button><button type="submit" class="primary">创建地图</button></footer>
        </form>
      </dialog>
      <dialog class="workbench-dialog" id="map-settings-dialog" aria-labelledby="map-settings-title">
        <form id="map-settings-form">
          <header><span>地图结构</span><strong id="map-settings-title">Chunk 网格扩展</strong><p>只向右增加列、向下增加行；既有身份、原点和世界坐标保持不变。</p></header>
          <div class="grid-preview current-grid"><span>当前网格</span><strong>${grid.columns} × ${grid.rows}</strong><span>当前世界</span><strong>${this.project.world.width} × ${this.project.world.height}</strong></div>
          <div class="dialog-grid-fields dialog-fields">
            <label><span>目标列数</span><input id="map-settings-columns" type="number" min="${grid.columns}" max="${MAX_GRID_AXIS}" step="1" required value="${grid.columns}"></label>
            <label><span>目标行数</span><input id="map-settings-rows" type="number" min="${grid.rows}" max="${MAX_GRID_AXIS}" step="1" required value="${grid.rows}"></label>
          </div>
          <div class="grid-preview"><span>扩展后世界</span><strong id="map-settings-world-size">${this.project.world.width} × ${this.project.world.height}</strong><small>缩小、左/上迁移和单独修改 Chunk 尺寸均不受支持。</small></div>
          <div class="dialog-error" id="map-settings-error" role="alert"></div>
          <footer><button type="button" class="quiet" id="cancel-map-settings">取消</button><button type="submit" class="primary">安全扩展</button></footer>
        </form>
      </dialog>`;
  }

  private bindUi(): void {
    this.requireElement("workspace-list").addEventListener("click", (event) => {
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-workspace]");
      if (button) this.activateWorkspace(button.dataset.workspace as WorkspaceId);
    });
    this.requireElement("context-panel").addEventListener("click", (event) => {
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-tool]");
      if (button) {
        this.activateTool(button.dataset.tool as ToolId);
        return;
      }
      const cityLocationId = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-city-location-id]")?.dataset.cityLocationId;
      if (cityLocationId) {
        this.selectCityLocation(cityLocationId);
        return;
      }
      const provinceAction = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-province-action]")?.dataset.provinceAction;
      if (provinceAction === "create") this.createProvince();
      if (provinceAction === "add-auxiliary") this.addAuxiliaryCityRegion();
      if (provinceAction === "cancel-pending") {
        this.cancelPendingCityRegion("已取消本次城市区域创建");
        this.activateTool("select");
      }
      if (provinceAction === "delete") this.deleteProvince();
      const action = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-review-action]")?.dataset.reviewAction;
      if (action === "validate") {
        this.runValidation();
        this.setValidationExpanded(true);
      }
      if (action === "show-issues") this.setValidationExpanded(true);
      if (action === "compile-location-geometries") void this.compileLocationGeometries();
    });
    this.requireElement("context-panel").addEventListener("input", (event) => this.handleContextSettingChange(event));
    this.requireElement("context-panel").addEventListener("change", (event) => this.handleContextSettingChange(event));
    this.requireElement("save").addEventListener("click", () => void this.saveAll());
    this.requireElement<HTMLSelectElement>("map-selector").addEventListener("change", (event) => this.switchMap((event.target as HTMLSelectElement).value));
    this.requireElement("new-map").addEventListener("click", () => this.openNewMapDialog());
    this.requireElement("duplicate-map").addEventListener("click", () => void this.duplicateMap());
    this.requireElement("map-settings").addEventListener("click", () => this.openMapSettingsDialog());
    this.requireElement<HTMLFormElement>("new-map-form").addEventListener("submit", (event) => void this.createMap(event));
    this.requireElement<HTMLFormElement>("map-settings-form").addEventListener("submit", (event) => void this.expandMapGrid(event));
    this.requireElement("cancel-new-map").addEventListener("click", () => this.requireElement<HTMLDialogElement>("new-map-dialog").close());
    this.requireElement("cancel-map-settings").addEventListener("click", () => this.requireElement<HTMLDialogElement>("map-settings-dialog").close());
    for (const id of ["new-map-columns", "new-map-rows", "map-settings-columns", "map-settings-rows"]) {
      this.requireElement(id).addEventListener("input", () => this.updateGridPreviews());
    }
    this.requireElement("publish-map").addEventListener("click", () => void this.publishMap());
    this.requireElement("validate").addEventListener("click", () => this.runValidation());
    this.requireElement("undo").addEventListener("click", () => this.undo());
    this.requireElement("redo").addEventListener("click", () => this.redo());
    this.requireElement("open-layers").addEventListener("click", () => this.setDrawer(this.openDrawer === "layers" ? undefined : "layers"));
    this.requireElement("close-layers").addEventListener("click", () => this.setDrawer(undefined));
    this.requireElement("close-inspector").addEventListener("click", () => this.setDrawer(undefined));
    this.requireElement("validation-toggle").addEventListener("click", () => this.setValidationExpanded(!this.validationExpanded));
    this.requireElement("clear-selection").addEventListener("click", () => {
      this.select.getFeatures().clear();
      this.setDrawer(undefined);
      this.renderProperties();
    });
    this.requireElement("layer-list").addEventListener("change", (event) => this.handleLayerChange(event));
    this.requireElement("layer-list").addEventListener("click", (event) => this.handleLayerClick(event));
    this.requireElement("property-panel").addEventListener("change", (event) => this.handlePropertyChange(event));
    this.requireElement("property-panel").addEventListener("click", (event) => {
      const action = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-action]")?.dataset.action;
      if (action === "delete-feature") this.deleteSelectedFeature();
    });
    this.requireElement("validation-list").addEventListener("click", (event) => {
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-index]");
      if (!button) return;
      const item = this.diagnostics[Number(button.dataset.index)];
      if (item) this.locateDiagnostic(item);
    });
    window.addEventListener("keydown", (event) => this.handleGlobalKeyDown(event));
    window.addEventListener("beforeunload", (event) => {
      if (!this.dirty && !this.pendingCityRegion) return;
      event.preventDefault();
      event.returnValue = "";
    });
    this.map.on("pointermove", (event) => this.handlePointerMove(event.coordinate, event.pixel));
  }

  private async initializeMapControls(): Promise<void> {
    try {
      const catalog = await workbenchApi.listMaps();
      const selector = this.requireElement<HTMLSelectElement>("map-selector");
      const groups = (["authoring", "fixture"] as const).map((kind) => {
        const entries = catalog.maps.filter((entry) => entry.kind === kind);
        if (entries.length === 0) return "";
        const label = kind === "authoring" ? "创作地图" : "验证 Fixture";
        const prefix = kind === "authoring" ? "创作" : "Fixture";
        return `<optgroup label="${label}">${entries.map((entry) => `<option value="${escapeHtml(entry.mapId)}" ${entry.mapId === this.project.mapId ? "selected" : ""}>[${prefix}] ${escapeHtml(entry.displayName)} · ${escapeHtml(entry.mapId)}${entry.mapId === catalog.defaultMapId ? " · 默认" : ""}</option>`).join("")}</optgroup>`;
      }).join("");
      selector.innerHTML = groups;
      if (catalog.maps.length === 0) selector.innerHTML = `<option value="${escapeHtml(this.project.mapId)}">${escapeHtml(this.project.displayName)} · ${escapeHtml(this.project.mapId)}</option>`;
    } catch (error) {
      this.setStatus(`地图目录加载失败：${error instanceof Error ? error.message : String(error)}`, "error");
    }
  }

  private switchMap(mapId: string): void {
    if (!mapId || mapId === this.project.mapId) return;
    if ((this.dirty || this.pendingCityRegion) && !window.confirm("当前地图有未保存修改。放弃修改并切换地图吗？")) {
      this.requireElement<HTMLSelectElement>("map-selector").value = this.project.mapId;
      return;
    }
    window.location.assign(`?mapId=${encodeURIComponent(mapId)}`);
  }

  private openNewMapDialog(): void {
    const dialog = this.requireElement<HTMLDialogElement>("new-map-dialog");
    this.requireElement("new-map-error").textContent = "";
    this.updateGridPreviews();
    dialog.showModal();
  }

  private async createMap(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    if ((this.dirty || this.pendingCityRegion) && !window.confirm("当前地图有未保存修改。放弃修改并新建地图吗？")) return;
    const mapId = this.requireElement<HTMLInputElement>("new-map-id").value.trim();
    const displayName = this.requireElement<HTMLInputElement>("new-map-name").value.trim();
    const columns = Number(this.requireElement<HTMLInputElement>("new-map-columns").value);
    const rows = Number(this.requireElement<HTMLInputElement>("new-map-rows").value);
    const error = this.requireElement("new-map-error");
    try {
      if (!/^[a-z0-9][a-z0-9_-]{1,63}$/.test(mapId)) throw new RangeError("MapId 必须由小写字母、数字、下划线或连字符组成，长度 2–64");
      if (!displayName) throw new RangeError("显示名称不能为空");
      validateMapGridDimensions(columns, rows);
      error.textContent = "正在创建并初始化 Chunk…";
      await workbenchApi.createMap(mapId, displayName, columns, rows);
      window.location.assign(`?mapId=${encodeURIComponent(mapId)}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.requireElement("new-map-error").textContent = message;
      this.setStatus(`新建地图失败：${message}`, "error");
    }
  }

  private openMapSettingsDialog(): void {
    const dialog = this.requireElement<HTMLDialogElement>("map-settings-dialog");
    this.requireElement("map-settings-error").textContent = "";
    this.updateGridPreviews();
    dialog.showModal();
  }

  private async expandMapGrid(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    const error = this.requireElement("map-settings-error");
    if (this.dirty || this.pendingCityRegion) {
      error.textContent = "请先保存当前地图，再扩展网格。";
      return;
    }
    const columns = Number(this.requireElement<HTMLInputElement>("map-settings-columns").value);
    const rows = Number(this.requireElement<HTMLInputElement>("map-settings-rows").value);
    try {
      validateMapGridDimensions(columns, rows);
      error.textContent = "正在安全扩展并初始化新增 Chunk…";
      await workbenchApi.expandGrid(this.project.mapId, columns, rows);
      window.location.reload();
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : String(caught);
      error.textContent = message;
      this.setStatus(`网格扩展失败：${message}`, "error");
    }
  }

  private updateGridPreviews(): void {
    const newColumns = Number(this.requireElement<HTMLInputElement>("new-map-columns").value);
    const newRows = Number(this.requireElement<HTMLInputElement>("new-map-rows").value);
    this.requireElement("new-map-world-size").textContent = Number.isFinite(newColumns) && Number.isFinite(newRows)
      ? `${newColumns * FIXED_NEW_MAP_CHUNK_SIZE} × ${newRows * FIXED_NEW_MAP_CHUNK_SIZE}`
      : "—";
    const columns = Number(this.requireElement<HTMLInputElement>("map-settings-columns").value);
    const rows = Number(this.requireElement<HTMLInputElement>("map-settings-rows").value);
    this.requireElement("map-settings-world-size").textContent = Number.isFinite(columns) && Number.isFinite(rows)
      ? `${columns * this.project.chunk.width} × ${rows * this.project.chunk.height}`
      : "—";
  }

  private async duplicateMap(): Promise<void> {
    if ((this.dirty || this.pendingCityRegion) && !window.confirm("当前地图有未保存修改。放弃修改并复制已保存版本吗？")) return;
    const mapId = window.prompt("输入副本的不可变 MapId", `${this.project.mapId}_copy`)?.trim();
    if (!mapId) return;
    const displayName = window.prompt("输入副本显示名称", `${this.project.displayName} 副本`)?.trim() || mapId;
    try {
      await workbenchApi.duplicateMap(this.project.mapId, mapId, displayName);
      window.location.assign(`?mapId=${encodeURIComponent(mapId)}`);
    } catch (error) {
      this.setStatus(`复制地图失败：${error instanceof Error ? error.message : String(error)}`, "error");
    }
  }

  private createProvince(): void {
    if (this.saving || this.pendingCityRegion || this.mutationBefore) return;
    try {
      const identity = new AuthoringIdentityFactory(this.getGeography()).createProvinceCityIdentity();
      const province = { provinceId: identity.provinceId, name: identity.provinceId, layoutId: identity.layoutId };
      this.beginCityRegionCreation({
        province,
        location: {
          locationId: identity.locationId,
          name: identity.locationId,
          locationType: "main-city",
          provinceId: identity.provinceId,
        },
      });
    } catch (error) {
      this.setStatus(`无法生成省份身份：${error instanceof Error ? error.message : String(error)}`, "error");
    }
  }

  private addAuxiliaryCityRegion(): void {
    if (this.saving || this.pendingCityRegion || this.mutationBefore) return;
    const province = this.provinces.find((candidate) => candidate.provinceId === this.selectedLocationProvinceId);
    if (!province) {
      this.setStatus("请先选择一个省份", "warning");
      return;
    }
    try {
      const locationId = new AuthoringIdentityFactory(this.getGeography()).createLocationId();
      this.beginCityRegionCreation({
        location: {
          locationId,
          name: locationId,
          locationType: "auxiliary-city",
          provinceId: province.provinceId,
        },
      });
    } catch (error) {
      this.setStatus(`无法生成辅城身份：${error instanceof Error ? error.message : String(error)}`, "error");
    }
  }

  private beginCityRegionCreation(input: { province?: ProvinceDefinition; location: StrategicLocationProperties }): void {
    const requiredLayers: LayerId[] = ["strategic-locations", "territories"];
    const locked = requiredLayers.map((id) => this.getLayerDefinition(id)).find((layer) => layer.locked);
    if (locked) {
      this.setStatus(`${locked.label} 已锁定，无法创建城市区域`, "warning");
      return;
    }

    const before = this.captureSnapshot();
    const previousLayerVisibility: Partial<Record<LayerId, boolean>> = {};
    for (const id of requiredLayers) {
      const definition = this.getLayerDefinition(id);
      previousLayerVisibility[id] = definition.visible;
      definition.visible = true;
      this.layers.byId.get(id)?.setVisible(true);
    }
    this.pendingCityRegion = {
      before,
      location: input.location,
      provinceWasCreated: input.province !== undefined,
      previousProvinceId: this.selectedLocationProvinceId,
      previousGeometryLocationId: this.selectedGeometryLocationId,
      previousDirty: this.dirty,
      previousMutationRevision: this.mutationRevision,
      previousLayerVisibility,
    };
    if (input.province) this.provinces.push(input.province);
    this.selectedLocationProvinceId = input.location.provinceId ?? "";
    this.selectedGeometryLocationId = input.location.locationId;
    this.select.getFeatures().clear();
    if (this.openDrawer === "inspector") this.setDrawer(undefined);
    this.renderLayers();
    this.activateTool("region");
    const role = input.location.locationType === "main-city" ? "主城" : "辅城";
    this.setStatus(`${role}身份已生成，请立即绘制区域；Esc 或“取消本次创建”会完整回滚`, "pending");
  }

  private cancelPendingCityRegion(message: string): void {
    const pending = this.pendingCityRegion;
    if (!pending) return;
    this.pendingCityRegion = undefined;
    this.mutationBefore = undefined;
    this.restoreSnapshot(pending.before);
    this.selectedLocationProvinceId = pending.previousProvinceId;
    this.selectedGeometryLocationId = pending.previousGeometryLocationId;
    for (const [id, visible] of Object.entries(pending.previousLayerVisibility) as Array<[LayerId, boolean]>) {
      const definition = this.getLayerDefinition(id);
      definition.visible = visible;
      this.layers.byId.get(id)?.setVisible(visible);
    }
    this.restoreDirtyState(pending.previousDirty, pending.previousMutationRevision);
    this.renderLayers();
    this.renderWorkspaceContext();
    this.setStatus(message, "info");
  }

  private selectCityLocation(locationId: string): void {
    const feature = this.layers.locationSource.getFeatures().find((candidate) => candidate.get("locationId") === locationId);
    if (!feature) return;
    if (!this.synchronizeCityWorkspaceFromFeature(feature)) return;
    this.select.getFeatures().clear();
    this.select.getFeatures().push(feature);
    this.renderProperties();
  }

  private handleMapSelectionChanged(): void {
    const feature = this.select.getFeatures().item(0);
    if (feature) this.synchronizeCityWorkspaceFromFeature(feature);
    // Empty and non-city selections intentionally leave the last city workspace context intact.
    this.renderProperties();
  }

  private synchronizeCityWorkspaceFromFeature(feature: Feature): boolean {
    const locations = this.layers.locationSource.getFeatures().map((candidate) => candidate.getProperties() as StrategicLocationProperties);
    const selection = resolveCityWorkspaceSelection(feature.getProperties(), this.provinces, locations);
    if (!selection) return false;

    this.selectedLocationProvinceId = selection.provinceId;
    this.selectedGeometryLocationId = selection.locationId;
    // Preserve the OpenLayers selection so the inspector continues to own the exact clicked marker or geometry.
    this.activateWorkspace("regions", true);
    return true;
  }

  private deleteProvince(): void {
    if (this.saving || this.provinces.length === 0) return;
    const provinceId = this.selectedLocationProvinceId || this.provinces[0]?.provinceId;
    if (!provinceId) return;
    const index = this.provinces.findIndex((province) => province.provinceId === provinceId);
    if (index < 0) {
      this.setStatus(`未知 ProvinceId：${provinceId}`, "error");
      return;
    }
    const hasMembers = this.layers.locationSource.getFeatures().some((feature) => feature.get("provinceId") === provinceId) ||
      this.layers.regionSource.getFeatures().some((feature) => feature.get("provinceId") === provinceId);
    if (hasMembers) {
      this.setStatus(`省份仍有地点或区域成员，不能删除：${provinceId}`, "error");
      return;
    }
    if (!window.confirm(`确认删除空省份 ${provinceId}？`)) return;
    const before = this.captureSnapshot();
    this.provinces.splice(index, 1);
    this.selectedLocationProvinceId = this.provinces[0]?.provinceId ?? "";
    this.history.commit(before);
    this.setDirty(true);
    this.refreshAfterMutation();
    this.renderWorkspaceContext();
  }

  private async publishMap(): Promise<void> {
    if (this.saving) return;
    if (this.dirty || this.pendingCityRegion) {
      this.setStatus("发布前请先保存当前地图；草稿保存不代表满足发布条件", "warning");
      return;
    }
    const profile = this.requireElement<HTMLSelectElement>("publish-profile").value as PublishProfile;
    this.setSaving(true);
    try {
      const result = await workbenchApi.publish(this.project.mapId, profile);
      this.setStatus(`发布成功：${result.package.revision} · ${profile}`, "success");
    } catch (error) {
      this.setValidationExpanded(true);
      this.setStatus(`发布被阻止：${error instanceof Error ? error.message : String(error)}`, "error");
    } finally {
      this.setSaving(false);
    }
  }

  private handleGlobalKeyDown(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    const isEditingText = target?.matches("input, textarea, select, [contenteditable='true']") ?? false;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
      event.preventDefault();
      // Native change fires on blur, so the visible field value reaches the model before the save snapshot is captured.
      if (isEditingText) target?.blur();
      void this.saveAll();
      return;
    }
    if (this.saving || isEditingText) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") { event.preventDefault(); this.undo(); }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "y") { event.preventDefault(); this.redo(); }
    if (event.key === "Delete") this.deleteSelectedFeature();
    if (event.key.toLowerCase() === "v") this.activateTool("select");
    if (event.key === "Escape" && this.activeTool !== "select") this.activateTool("select");
  }

  private renderWorkspaceNavigation(): void {
    this.requireElement("workspace-list").innerHTML = workspaceUiDefinitions.map((workspace) => `
      <button class="workspace-button ${workspace.id === this.activeWorkspace ? "active" : ""}" data-workspace="${workspace.id}" title="${escapeHtml(workspace.label)}" aria-current="${workspace.id === this.activeWorkspace ? "step" : "false"}">
        <span class="workspace-glyph">${workspace.glyph}</span>
        <span>${escapeHtml(workspace.shortLabel)}</span>
      </button>`).join("");
    const workspace = getWorkspaceDefinition(this.activeWorkspace);
    this.requireElement("toolbar-workspace-title").textContent = `${workspace.index} · ${workspace.label}`;
  }

  private activateWorkspace(workspaceId: WorkspaceId, preserveMapSelection = false): void {
    if (this.saving) return;
    this.activeWorkspace = workspaceId;
    if (!preserveMapSelection) {
      this.select.getFeatures().clear();
      if (this.openDrawer === "inspector") this.setDrawer(undefined);
    }
    this.renderWorkspaceNavigation();
    this.activateTool(getWorkspaceDefinition(workspaceId).defaultTool);
  }

  private renderWorkspaceContext(): void {
    const workspace = getWorkspaceDefinition(this.activeWorkspace);
    const tools = workspace.id === "review" ? ["select" as ToolId] : ["select" as ToolId, ...workspace.tools];
    const tool = toolUiDefinitions[this.activeTool];
    const provinceManagement = workspace.id === "regions" ? this.renderProvinceManagementHtml() : "";
    const reviewActions = workspace.id === "review" ? `
      <section class="context-section">
        <div class="section-label">检查与输出</div>
        <div class="review-actions">
          <button data-review-action="validate"><strong>运行地图检查</strong><span>重新扫描地貌、线路、地点与区域问题</span></button>
          <button data-review-action="show-issues"><strong>查看问题清单</strong><span>展开底部列表，点击问题可定位对象</span></button>
          <button data-review-action="compile-location-geometries"><strong>生成城市区域数据</strong><span>输出 territory mask、LocationId 查询表与省份/城市轮廓</span></button>
        </div>
      </section>` : "";

    this.requireElement("context-panel-body").innerHTML = `
      <header class="context-heading">
        <span>${workspace.index} · 工作流</span>
        <strong>${escapeHtml(workspace.label)}</strong>
        <p>${escapeHtml(workspace.description)}</p>
      </header>
      ${provinceManagement}
      <section class="context-section">
        <div class="section-label">${workspace.id === "review" ? "定位工具" : "选择操作"}</div>
        <div class="tool-choice-grid">
          ${tools.map((toolId) => {
            const definition = toolUiDefinitions[toolId];
            return `<button data-tool="${toolId}" class="tool-choice ${toolId === this.activeTool ? "active" : ""}" aria-pressed="${toolId === this.activeTool}"><span>${escapeHtml(definition.shortLabel)}</span></button>`;
          }).join("")}
        </div>
      </section>
      ${reviewActions}
      <section class="active-tool-guide">
        <span>正在使用</span>
        <strong>${escapeHtml(tool.label)}</strong>
        <p>${escapeHtml(tool.instruction)}</p>
        <small>${escapeHtml(tool.nextStep)}</small>
      </section>
      ${this.renderToolSettingsHtml()}
      <section class="workflow-steps">
        <div class="section-label">推荐步骤</div>
        <ol>${workspace.steps.map((step) => `<li>${escapeHtml(step)}</li>`).join("")}</ol>
      </section>`;
  }

  private renderProvinceManagementHtml(): string {
    const selected = this.provinces.find((province) => province.provinceId === this.selectedLocationProvinceId) ?? this.provinces[0];
    if (selected && selected.provinceId !== this.selectedLocationProvinceId) this.selectedLocationProvinceId = selected.provinceId;
    const locations = this.getGeography().strategicLocations.features.filter((feature) => feature.properties.provinceId === selected?.provinceId);
    const pending = this.pendingCityRegion;
    const pendingLocation = pending && pending.location.provinceId === selected?.provinceId ? pending.location : undefined;
    const mainCity = locations.find((feature) => feature.properties.locationType === "main-city")?.properties ?? (pendingLocation?.locationType === "main-city" ? pendingLocation : undefined);
    const auxiliaries = locations.filter((feature) => feature.properties.locationType === "auxiliary-city").map((feature) => feature.properties);
    if (pendingLocation?.locationType === "auxiliary-city") auxiliaries.push(pendingLocation);
    const options = this.provinces.map((province) => `<option value="${escapeHtml(province.provinceId)}" ${province.provinceId === selected?.provinceId ? "selected" : ""}>${escapeHtml(province.name)}</option>`).join("");
    const cityCard = (location: StrategicLocationProperties, role: "主城" | "辅城") => {
      const variant = role === "主城" ? "main" : "auxiliary";
      const isSelected = location.locationId === this.selectedGeometryLocationId;
      const content = `<span class="city-card-icon" aria-hidden="true">城</span><span class="city-card-copy"><span class="city-role-badge">${role}</span><strong>${escapeHtml(location.name)}</strong></span>`;
      return location === pendingLocation
        ? `<div class="city-card city-card-${variant} pending selected" aria-label="${role} ${escapeHtml(location.name)}，正在绘制区域">${content}<small>正在绘制区域…</small></div>`
        : `<button class="city-card city-card-${variant} ${isSelected ? "selected" : ""}" data-city-location-id="${escapeHtml(location.locationId)}" aria-pressed="${isSelected}" aria-label="选择${role} ${escapeHtml(location.name)}">${content}</button>`;
    };
    return `<section class="context-section province-management" data-ui-scope="city-regions-provinces">
      <div class="section-label">当前省份</div>
      <label class="setting-field"><span>省份选择</span><select data-setting="province-selection" ${this.provinces.length === 0 || this.pendingCityRegion ? "disabled" : ""}>${options || `<option>尚无省份</option>`}</select></label>
      ${selected ? `<label class="setting-field"><span>省份名称</span><input data-setting="province-name" value="${escapeHtml(selected.name)}" ${this.pendingCityRegion ? "disabled" : ""}><small>可稍后命名；改名不会改变稳定身份</small></label>
      <div class="city-member-list">
        <div class="city-member-group city-main-group"><span>省份核心</span>${mainCity ? cityCard(mainCity, "主城") : `<div class="inline-warning">缺少主城（发布会阻止）</div>`}</div>
        <div class="city-member-group city-auxiliary-group"><span>辅城成员</span>${auxiliaries.map((location) => cityCard(location, "辅城")).join("")}${!pendingLocation ? `<button class="city-add-card" data-province-action="add-auxiliary" aria-label="添加辅城区域"><span aria-hidden="true">＋</span><strong>添加辅城区域</strong></button>` : ""}</div>
      </div>
      <details class="identity-details"><summary>高级身份信息（只读）</summary><dl class="province-facts">
        <div><dt>ProvinceId</dt><dd>${escapeHtml(selected.provinceId)}</dd></div>
        <div><dt>省份拥有的 LayoutId</dt><dd>${escapeHtml(selected.layoutId)}</dd></div>
        ${mainCity ? `<div><dt>主城 LocationId</dt><dd>${escapeHtml(mainCity.locationId)}</dd></div>` : ""}
        ${auxiliaries.map((location) => `<div><dt>辅城 LocationId</dt><dd>${escapeHtml(location.locationId)}</dd></div>`).join("")}
      </dl></details>` : `<div class="inline-warning">新建省份后将立即绘制其主城区域。</div>`}
      <div class="province-actions">
        ${this.pendingCityRegion
          ? `<button data-province-action="cancel-pending" class="danger">取消本次创建</button>`
          : `<button data-province-action="create">新建省份</button><button data-province-action="delete" ${selected ? "" : "disabled"}>删除空省份</button>`}
      </div>
    </section>`;
  }

  private renderToolSettingsHtml(): string {
    const terrainChoices = this.project.terrainTypes.map((terrain) => `
      <label class="terrain-choice" title="地貌 ID ${terrain.id}">
        <input type="radio" name="terrain-type" data-setting="terrain-id" value="${terrain.id}" ${terrain.id === this.selectedTerrainId ? "checked" : ""}>
        <span class="terrain-swatch" style="--terrain-color:${escapeHtml(terrain.color)}"></span>
        <span>${escapeHtml(terrain.label)}</span>
      </label>`).join("");
    const radius = `
      <label class="setting-field range-field"><span>笔刷半径 <output data-value-for="brush-radius">${this.brushRadius}</output></span><input data-setting="brush-radius" type="range" min="8" max="192" step="8" value="${this.brushRadius}"></label>`;

    if (["terrain-brush", "terrain-erase", "terrain-fill", "terrain-lasso", "terrain-polygon"].includes(this.activeTool)) {
      return `<section class="context-section tool-settings"><div class="section-label">地貌设置</div><div class="terrain-choice-grid">${terrainChoices}</div>${["terrain-brush", "terrain-erase"].includes(this.activeTool) ? radius : ""}</section>`;
    }
    if (this.activeTool === "river") {
      return `<section class="context-section tool-settings"><div class="section-label">河流设置</div><label class="setting-field"><span>宽度等级</span><input data-setting="river-width" type="number" min="1" max="5" step="1" value="${this.selectedRiverWidthClass}"><small>1 为支流，5 为主干河流</small></label></section>`;
    }
    if (this.activeTool === "water-anchor") {
      return `<section class="context-section tool-settings"><div class="section-label">锚点类型</div><div class="option-card-grid">
        ${this.renderRadioOption("water-anchor-type", "source", "源头", this.selectedWaterAnchorType)}
        ${this.renderRadioOption("water-anchor-type", "lake", "湖泊", this.selectedWaterAnchorType)}
        ${this.renderRadioOption("water-anchor-type", "coast", "海岸", this.selectedWaterAnchorType)}
      </div></section>`;
    }
    if (this.activeTool === "road") {
      return `<section class="context-section tool-settings"><div class="section-label">道路设置</div><label class="setting-field"><span>道路等级</span><input data-setting="road-class" type="number" min="1" max="5" step="1" value="${this.selectedRoadClass}"><small>用于区分小路、官道与主干道</small></label></section>`;
    }
    if (this.activeTool === "mountain") {
      return `<section class="context-section tool-settings"><div class="section-label">山脉设置</div><label class="setting-field range-field"><span>山脉密度 <output data-value-for="mountain-density">${this.selectedMountainDensity.toFixed(2)}</output></span><input data-setting="mountain-density" type="range" min="0" max="1" step="0.05" value="${this.selectedMountainDensity}"></label></section>`;
    }
    if (this.activeTool === "location") {
      const options: Array<[StrategicLocationType, string]> = [["gate", "关隘"], ["bridge", "桥梁"], ["ferry", "渡口"], ["port", "港口"], ["ruin", "遗迹"], ["resource-site", "资源点"]];
      return `<section class="context-section tool-settings"><div class="section-label">非城市地点类型</div><div class="option-card-grid location-options">${options.map(([value, label]) => this.renderRadioOption("location-type", value, label, this.selectedLocationType)).join("")}</div><small>主城与辅城请在“城市区域”中统一创建并绘制。</small></section>`;
    }
    if (this.activeTool === "region") {
      const cities = this.getGeography().strategicLocations.features
        .filter((feature) => feature.properties.locationType === "main-city" || feature.properties.locationType === "auxiliary-city")
        .map((feature) => feature.properties);
      if (this.pendingCityRegion) cities.push(this.pendingCityRegion.location);
      return `<section class="context-section tool-settings">
        <div class="section-label">城市区域归属</div>
        <label class="setting-field"><span>城市</span><select data-setting="geometry-location-id" ${cities.length === 0 || this.pendingCityRegion ? "disabled" : ""}>${cities.map((city) => `<option value="${escapeHtml(city.locationId)}" ${city.locationId === this.selectedGeometryLocationId ? "selected" : ""}>${city.locationType === "main-city" ? "主城" : "辅城"} · ${escapeHtml(city.name)}</option>`).join("")}</select><small>${cities.length > 0 ? "每个主城或辅城只能有一份视觉几何" : "尚无城市，请通过上方统一流程创建"}</small></label>
        <label class="setting-field"><span>方向</span><select data-setting="region-direction">${this.renderDirectionOptions()}</select></label>
        ${this.selectedGeometryLocationId.trim() === "" ? `<div class="inline-warning">先选择一个城市后才能绘制。</div>` : ""}
      </section>`;
    }
    return "";
  }

  private renderRadioOption(setting: string, value: string, label: string, selected: string): string {
    return `<label class="option-card"><input type="radio" name="${setting}" data-setting="${setting}" value="${value}" ${value === selected ? "checked" : ""}><span>${escapeHtml(label)}</span></label>`;
  }

  private renderDirectionOptions(): string {
    const options = [["center", "中央"], ["north", "北"], ["northeast", "东北"], ["east", "东"], ["southeast", "东南"], ["south", "南"], ["southwest", "西南"], ["west", "西"], ["northwest", "西北"]];
    return options.map(([value, label]) => `<option value="${value}" ${value === this.selectedRegionDirection ? "selected" : ""}>${label}</option>`).join("");
  }

  private handleContextSettingChange(event: Event): void {
    const input = (event.target as HTMLElement).closest<HTMLInputElement | HTMLSelectElement>("[data-setting]");
    if (!input) return;
    const value = input.value;
    if (input.dataset.setting === "province-name") {
      if (event.type !== "change" || this.pendingCityRegion) return;
      const province = this.provinces.find((candidate) => candidate.provinceId === this.selectedLocationProvinceId);
      if (!province) return;
      const name = value.trim() || province.provinceId;
      if (name === province.name) return;
      const before = this.captureSnapshot();
      province.name = name;
      this.history.commit(before);
      this.setDirty(true);
      this.refreshAfterMutation();
      this.renderWorkspaceContext();
      this.setStatus("省份名称已更新；稳定身份保持不变", "success");
      return;
    }
    switch (input.dataset.setting) {
      case "terrain-id": this.selectedTerrainId = Number(value); break;
      case "brush-radius": this.brushRadius = Number(value); break;
      case "river-width": this.selectedRiverWidthClass = Number(value); break;
      case "water-anchor-type": this.selectedWaterAnchorType = value as WaterAnchorType; break;
      case "road-class": this.selectedRoadClass = Number(value); break;
      case "mountain-density": this.selectedMountainDensity = Number(value); break;
      case "location-type": this.selectedLocationType = value as StrategicLocationType; break;
      case "province-selection": {
        this.selectedLocationProvinceId = value.trim();
        const main = this.layers.locationSource.getFeatures().find((feature) => feature.get("provinceId") === value && feature.get("locationType") === "main-city");
        this.selectedGeometryLocationId = String(main?.get("locationId") ?? "");
        break;
      }
      case "geometry-location-id": this.selectedGeometryLocationId = value.trim(); break;
      case "region-direction": this.selectedRegionDirection = value; break;
    }
    const output = this.root.querySelector<HTMLOutputElement>(`[data-value-for="${input.dataset.setting}"]`);
    if (output) output.textContent = input.dataset.setting === "mountain-density" ? Number(value).toFixed(2) : value;
    if (event.type === "change" && input.dataset.setting === "geometry-location-id" && this.activeTool === "region") this.activateTool(this.activeTool);
    if (event.type === "change" && input.dataset.setting === "province-selection") this.renderWorkspaceContext();
  }

  private setDrawer(drawer: "layers" | "inspector" | undefined): void {
    this.openDrawer = drawer;
    const layerDrawer = this.requireElement("layer-drawer");
    const inspectorDrawer = this.requireElement("inspector-drawer");
    layerDrawer.classList.toggle("open", drawer === "layers");
    inspectorDrawer.classList.toggle("open", drawer === "inspector");
    layerDrawer.setAttribute("aria-hidden", String(drawer !== "layers"));
    inspectorDrawer.setAttribute("aria-hidden", String(drawer !== "inspector"));
    this.requireElement("open-layers").setAttribute("aria-expanded", String(drawer === "layers"));
  }

  private setValidationExpanded(expanded: boolean): void {
    this.validationExpanded = expanded;
    this.requireElement("workbench-shell").classList.toggle("validation-expanded", expanded);
    this.requireElement("validation-toggle").setAttribute("aria-expanded", String(expanded));
    this.requireElement("validation-toggle-label").textContent = expanded ? "收起问题" : "查看问题";
    this.scheduleMapResize();
  }

  private scheduleMapResize(): void {
    requestAnimationFrame(() => this.map.updateSize());
  }

  private setDirty(dirty: boolean): void {
    if (dirty) this.mutationRevision += 1;
    this.dirty = dirty;
    this.renderDirtyState();
  }

  private restoreDirtyState(dirty: boolean, mutationRevision: number): void {
    this.dirty = dirty;
    this.mutationRevision = mutationRevision;
    this.renderDirtyState();
  }

  private renderDirtyState(): void {
    const state = this.requireElement("save-state");
    state.classList.toggle("dirty", this.dirty);
    state.querySelector("span")!.textContent = this.dirty ? "有未保存修改" : "已保存";
  }

  private setSaving(saving: boolean): void {
    this.saving = saving;
    this.requireElement("workbench-shell").classList.toggle("saving", saving);
    this.requireElement<HTMLButtonElement>("save").disabled = saving;
    if (this.activeInteraction) this.activeInteraction.setActive(!saving);
    this.select.setActive(!saving && this.activeTool === "select");
    this.modify.setActive(!saving && this.activeTool === "select");
    this.updateHistoryButtons();
  }

  private activateTool(tool: ToolId): void {
    if (this.saving) return;
    if (this.pendingCityRegion && tool !== "region") this.cancelPendingCityRegion("已取消未完成的城市区域创建");
    const targetWorkspace = workspaceForTool(tool);
    if (targetWorkspace && targetWorkspace !== this.activeWorkspace) this.activeWorkspace = targetWorkspace;
    const requiredLayer = toolLayer[tool];
    let blockedLayerLabel: string | undefined;
    let revealedLayerLabel: string | undefined;
    if (requiredLayer) {
      const definition = this.getLayerDefinition(requiredLayer);
      if (definition.locked) {
        blockedLayerLabel = definition.label;
        tool = "select";
      } else if (!definition.visible) {
        // Starting an authoring tool makes its output visible instead of allowing invisible edits.
        definition.visible = true;
        this.layers.byId.get(requiredLayer)?.setVisible(true);
        this.renderLayers();
        this.setDirty(true);
        revealedLayerLabel = definition.label;
      }
    }
    if (this.activeInteraction) this.map.removeInteraction(this.activeInteraction);
    this.activeInteraction = undefined;
    this.activeTool = tool;
    this.select.setActive(tool === "select");
    this.modify.setActive(tool === "select");

    if (["terrain-brush", "terrain-erase", "terrain-fill"].includes(tool)) this.activateTerrainPointer(tool);
    else if (tool === "terrain-lasso" || tool === "terrain-polygon") this.activateTerrainPolygon(tool === "terrain-lasso");
    else if (tool === "river" || tool === "road" || tool === "mountain") this.activateLineDraw(tool);
    else if (tool === "water-anchor") this.activateWaterAnchorDraw();
    else if (tool === "location") this.activateLocationDraw();
    else if (tool === "region" && this.selectedGeometryLocationId.trim() !== "") this.activateLocationGeometryDraw();

    if (this.activeInteraction) this.map.addInteraction(this.activeInteraction);
    this.renderWorkspaceNavigation();
    this.renderWorkspaceContext();
    this.requireElement("current-tool-status").textContent = toolUiDefinitions[tool].shortLabel;
    if (blockedLayerLabel) this.setStatus(`${blockedLayerLabel} 已锁定，已切换为选择 / 修改`, "warning");
    else if (tool === "region" && this.selectedGeometryLocationId.trim() === "") this.setStatus("请先选择一个城市，再开始绘制", "warning");
    else if (revealedLayerLabel) this.setStatus(`${revealedLayerLabel} 已自动显示 · 工具：${toolUiDefinitions[tool].label}`, "info");
    else this.setStatus(`工具：${toolUiDefinitions[tool].label}`, "info");
  }

  private activateTerrainPointer(tool: ToolId): void {
    let last: [number, number] | undefined;
    this.activeInteraction = new PointerInteraction({
      handleDownEvent: (event) => {
        const coordinate = toGameCoordinate(event.coordinate);
        this.mutationBefore = this.captureSnapshot();
        if (tool === "terrain-fill") {
          this.terrain.floodFill(coordinate, this.selectedTerrainId);
          this.finishMutation();
          return false;
        }
        last = coordinate;
        this.terrain.paintCircle(coordinate, this.brushRadius, tool === "terrain-erase" ? 0 : this.selectedTerrainId);
        this.layers.terrainSource.changed();
        return true;
      },
      handleDragEvent: (event) => {
        const coordinate = toGameCoordinate(event.coordinate);
        if (last) this.terrain.paintStroke(last, coordinate, this.brushRadius, tool === "terrain-erase" ? 0 : this.selectedTerrainId);
        last = coordinate;
        this.layers.terrainSource.changed();
      },
      handleUpEvent: () => {
        last = undefined;
        this.finishMutation();
        return false;
      },
    });
  }

  private activateTerrainPolygon(freehand: boolean): void {
    const interaction = new Draw({ type: "Polygon", freehand });
    this.bindDrawMutation(interaction);
    interaction.on("drawend", (event) => {
      const geometry = writeGameFeatures([event.feature]).features[0]?.geometry;
      if (geometry?.type === "Polygon") this.terrain.fillPolygon(geometry.coordinates[0] as [number, number][], this.selectedTerrainId);
      this.finishMutation();
    });
    this.activeInteraction = interaction;
  }

  private activateLineDraw(featureType: "river" | "road" | "mountain"): void {
    const source = featureType === "river" ? this.layers.waterSource : featureType === "road" ? this.layers.roadSource : this.layers.mountainSource;
    const interaction = new Draw({ source, type: "LineString" });
    this.bindDrawMutation(interaction);
    interaction.on("drawend", (event) => {
      const id = shortId(featureType);
      event.feature.setProperties({
        featureId: id,
        featureType,
        name: "",
        widthClass: featureType === "river" ? this.selectedRiverWidthClass : undefined,
        roadClass: featureType === "road" ? this.selectedRoadClass : undefined,
        density: featureType === "mountain" ? this.selectedMountainDensity : undefined,
      });
      event.feature.setId(id);
      if (featureType === "river") this.snapRiverFeature(event.feature);
      this.finishNewFeature(event.feature, featureType === "river" ? "河流" : featureType === "road" ? "道路" : "山脉");
    });
    this.activeInteraction = interaction;
  }

  private snapRiverFeature(feature: Feature): void {
    const candidate = writeGameFeatures([feature]).features[0] as LinearFeature | undefined;
    if (!candidate) return;
    const rivers = writeGameFeatures(this.layers.waterSource.getFeatures().filter((other) => other !== feature)).features as LinearFeature[];
    const anchors = writeGameFeatures(this.layers.waterAnchorSource.getFeatures()).features as WaterAnchorFeature[];
    const snapped = snapRiverEndpoints(candidate, rivers, anchors, 32);
    const mapped = readGameFeatures(featureCollection([snapped]))[0];
    if (!mapped) return;
    feature.setGeometry(mapped.getGeometry());
    feature.setProperties(mapped.getProperties());
  }

  private activateLocationDraw(): void {
    const interaction = new Draw({ source: this.layers.locationSource, type: "Point" });
    this.bindDrawMutation(interaction);
    interaction.on("drawend", (event) => {
      const id = new AuthoringIdentityFactory(this.getGeography()).createLocationId();
      event.feature.setProperties({ locationId: id, name: id, locationType: this.selectedLocationType });
      event.feature.setId(id);
      this.finishNewFeature(event.feature, "战略地点");
    });
    this.activeInteraction = interaction;
  }

  private activateWaterAnchorDraw(): void {
    const interaction = new Draw({ source: this.layers.waterAnchorSource, type: "Point" });
    this.bindDrawMutation(interaction);
    interaction.on("drawend", (event) => {
      const id = shortId(this.selectedWaterAnchorType);
      event.feature.setProperties({ anchorId: id, name: id, anchorType: this.selectedWaterAnchorType });
      event.feature.setId(id);
      this.finishNewFeature(event.feature, "水系锚点");
    });
    this.activeInteraction = interaction;
  }

  private activateLocationGeometryDraw(): void {
    const interaction = new Draw({ source: this.layers.regionSource, type: "Polygon" });
    this.bindDrawMutation(interaction);
    interaction.on("drawend", (event) => {
      const pending = this.pendingCityRegion;
      const location = pending?.location ?? this.getGeography().strategicLocations.features.find((feature) => feature.properties.locationId === this.selectedGeometryLocationId)?.properties;
      event.feature.setProperties({
        locationId: this.selectedGeometryLocationId,
        provinceId: location?.provinceId ?? "",
        direction: this.selectedRegionDirection,
      });
      event.feature.setId(this.selectedGeometryLocationId);
      if (pending) {
        const authoredGeometry = writeGameFeatures([event.feature]).features[0] as GeoFeature<Polygon | MultiPolygon> | undefined;
        if (!authoredGeometry) {
          this.cancelPendingCityRegion("城市区域几何无效，本次创建已回滚");
          return;
        }
        const markerCoordinate = centroid(authoredGeometry).geometry.coordinates;
        const marker = readGameFeatures(featureCollection([
          point(markerCoordinate, pending.location) as StrategicLocationFeature,
        ]))[0];
        if (!marker) {
          this.cancelPendingCityRegion("无法计算城市标记位置，本次创建已回滚");
          return;
        }
        marker.setId(pending.location.locationId);
        this.layers.locationSource.addFeature(marker);
        this.mutationBefore = pending.before;
        this.pendingCityRegion = undefined;
        // OpenLayers defers single-click selection after draw completion; select the marker after that window.
        setTimeout(() => {
          this.finishMutation();
          this.activateTool("select");
          this.select.getFeatures().clear();
          this.select.getFeatures().push(marker);
          this.renderProperties();
          this.renderWorkspaceContext();
          this.setStatus("城市区域与质心标记已创建；可稍后修改显示名称", "success");
        }, 350);
        return;
      }
      this.finishNewFeature(event.feature, "城市区域");
    });
    this.activeInteraction = interaction;
  }

  private bindDrawMutation(interaction: Draw): void {
    interaction.on("drawstart", () => { this.mutationBefore = this.pendingCityRegion?.before ?? this.captureSnapshot(); });
    // Esc, workspace changes, and layer locks all abort the sketch; none may leave saving permanently blocked.
    interaction.on("drawabort", () => {
      this.mutationBefore = undefined;
      if (!this.pendingCityRegion) return;
      this.cancelPendingCityRegion("已取消本次城市区域创建");
      queueMicrotask(() => this.activateTool("select"));
    });
  }

  private finishNewFeature(feature: Feature, label: string): void {
    // OpenLayers inserts a drawn feature into its VectorSource after drawend returns; derived data must refresh after that insertion.
    queueMicrotask(() => {
      this.finishMutation();
      this.activateTool("select");
      this.select.getFeatures().clear();
      this.select.getFeatures().push(feature);
      this.renderProperties();
      this.setStatus(`${label}已创建，请完善对象属性`, "success");
    });
  }

  private captureSnapshot(): WorkbenchSnapshot {
    return {
      geography: structuredClone(this.getGeography()),
      terrainChunks: this.terrain.exportAllChunks(),
      selectedProvinceId: this.selectedLocationProvinceId,
      selectedGeometryLocationId: this.selectedGeometryLocationId,
    };
  }

  private restoreSnapshot(snapshot: WorkbenchSnapshot): void {
    this.selectedLocationProvinceId = snapshot.selectedProvinceId;
    this.selectedGeometryLocationId = snapshot.selectedGeometryLocationId;
    this.loadGeography(snapshot.geography);
    for (const chunk of snapshot.terrainChunks) this.terrain.loadChunk(chunk.chunkId, decodeBase64(chunk.cellsBase64), true);
    this.layers.terrainSource.changed();
    this.select.getFeatures().clear();
    this.refreshAfterMutation();
    this.renderWorkspaceContext();
  }

  private finishMutation(): void {
    if (this.mutationBefore) this.history.commit(this.mutationBefore);
    this.mutationBefore = undefined;
    this.setDirty(true);
    this.refreshAfterMutation();
  }

  private refreshAfterMutation(): void {
    this.layers.terrainSource.changed();
    this.refreshCityOutlines();
    this.refreshChunkClips();
    this.runValidation();
    this.renderProperties();
    this.updateHistoryButtons();
  }

  private undo(): void {
    if (this.saving) return;
    if (this.pendingCityRegion) {
      this.cancelPendingCityRegion("已取消本次城市区域创建；撤销历史未改变");
      this.activateTool("select");
      return;
    }
    const snapshot = this.history.undo(this.captureSnapshot());
    if (!snapshot) return;
    this.restoreSnapshot(snapshot);
    this.setDirty(true);
    this.setStatus("已撤销", "info");
  }

  private redo(): void {
    if (this.saving) return;
    if (this.pendingCityRegion) {
      this.cancelPendingCityRegion("已取消本次城市区域创建；重做历史未改变");
      this.activateTool("select");
      return;
    }
    const snapshot = this.history.redo(this.captureSnapshot());
    if (!snapshot) return;
    this.restoreSnapshot(snapshot);
    this.setDirty(true);
    this.setStatus("已重做", "info");
  }

  private loadGeography(geography: GeographyDocument): void {
    this.layers?.waterSource.clear();
    this.layers?.waterAnchorSource.clear();
    this.layers?.roadSource.clear();
    this.layers?.mountainSource.clear();
    this.layers?.locationSource.clear();
    this.layers?.regionSource.clear();
    if (!this.layers) return;
    for (const feature of readGameFeatures(geography.linearFeatures)) {
      const type = feature.get("featureType");
      const source = type === "river" ? this.layers.waterSource : type === "road" ? this.layers.roadSource : this.layers.mountainSource;
      feature.setId(feature.get("featureId"));
      source.addFeature(feature);
    }
    for (const feature of readGameFeatures(geography.waterAnchors)) {
      feature.setId(feature.get("anchorId"));
      this.layers.waterAnchorSource.addFeature(feature);
    }
    for (const feature of readGameFeatures(geography.strategicLocations)) {
      feature.setId(feature.get("locationId"));
      this.layers.locationSource.addFeature(feature);
    }
    this.provinces = structuredClone(geography.provinces);
    for (const feature of readGameFeatures(geography.locationGeometries)) {
      feature.setId(feature.get("locationId"));
      this.layers.regionSource.addFeature(feature);
    }
    if (!this.provinces.some((province) => province.provinceId === this.selectedLocationProvinceId)) {
      this.selectedLocationProvinceId = this.provinces[0]?.provinceId ?? "";
    }
    const cityLocations = geography.strategicLocations.features.filter((feature) => feature.properties.locationType === "main-city" || feature.properties.locationType === "auxiliary-city");
    if (!cityLocations.some((feature) => feature.properties.locationId === this.selectedGeometryLocationId)) {
      this.selectedGeometryLocationId = cityLocations.find((feature) => feature.properties.provinceId === this.selectedLocationProvinceId)?.properties.locationId
        ?? cityLocations[0]?.properties.locationId
        ?? "";
    }
  }

  private getGeography(): GeographyDocument {
    // Only canonical editable sources are serialized; outlines, clips, masks, and diagnostics are rebuilt derivatives.
    const lineFeatures = [...this.layers.waterSource.getFeatures(), ...this.layers.roadSource.getFeatures(), ...this.layers.mountainSource.getFeatures()];
    return {
      version: 3,
      provinces: structuredClone(this.provinces),
      linearFeatures: writeGameFeatures(lineFeatures) as GeographyDocument["linearFeatures"],
      waterAnchors: writeGameFeatures(this.layers.waterAnchorSource.getFeatures()) as GeographyDocument["waterAnchors"],
      strategicLocations: writeGameFeatures(this.layers.locationSource.getFeatures()) as GeographyDocument["strategicLocations"],
      locationGeometries: writeGameFeatures(this.layers.regionSource.getFeatures()) as GeographyDocument["locationGeometries"],
    };
  }

  private refreshCityOutlines(): void {
    if (!this.layers) return;
    this.layers.cityOutlineSource.clear();
    const geometries = this.getGeography().locationGeometries;
    const groups = new Map<string, LocationGeometryFeature[]>();
    for (const geometry of geometries.features) {
      const list = groups.get(geometry.properties.provinceId) ?? [];
      list.push(geometry);
      groups.set(geometry.properties.provinceId, list);
    }
    for (const [provinceId, provinceGeometries] of groups) {
      try {
        const outline = provinceGeometries.length === 1 ? provinceGeometries[0] : union(featureCollection(provinceGeometries));
        if (!outline) continue;
        outline.properties = { ...outline.properties, provinceId };
        const mapped = readGameFeatures(featureCollection([outline]))[0];
        if (mapped) this.layers.cityOutlineSource.addFeature(mapped);
      } catch {
        // Invalid topology is reported by validation; the authoring surface stays usable.
      }
    }
  }

  private refreshChunkClips(): void {
    if (!this.layers) return;
    const clips = clipLinearFeaturesToChunks(this.project, this.getGeography().linearFeatures);
    this.layers.clipPreviewSource.clear();
    this.layers.clipPreviewSource.addFeatures(readGameFeatures(clips));
  }

  private runValidation(): void {
    if (!this.layers) return;
    this.diagnostics = validateWorkbench(this.project, this.terrain, this.getGeography());
    const points = this.diagnostics.map(diagnosticPoint).filter((feature) => feature !== undefined);
    this.layers.validationSource.clear();
    this.layers.validationSource.addFeatures(readGameFeatures(featureCollection(points)));
    const errors = this.diagnostics.filter((item) => item.severity === "error").length;
    const warnings = this.diagnostics.filter((item) => item.severity === "warning").length;
    this.requireElement("validation-summary").textContent = `${errors} 错误 · ${warnings} 警告`;
    this.requireElement("validation-count").textContent = String(this.diagnostics.length);
    this.requireElement("validation-list").innerHTML = this.diagnostics.length === 0
      ? `<div class="empty-state validation-empty"><strong>当前没有校验问题</strong><span>可以生成区域数据或继续编辑。</span></div>`
      : this.diagnostics.map((item, index) => `<button class="validation-item ${item.severity}" data-index="${index}"><span class="severity-dot"></span><span class="validation-copy"><strong>${escapeHtml(item.message)}</strong><small>${escapeHtml(item.code)}${item.coordinate ? ` · ${Math.round(item.coordinate[0])}, ${Math.round(item.coordinate[1])}` : ""}</small></span><span class="locate-label">定位</span></button>`).join("");
  }

  private async saveAll(): Promise<void> {
    if (this.saving) return;
    if (!this.initialized) {
      this.setStatus("请使用“新建地图”填写 MapId、名称和 Chunk 网格后再保存", "warning");
      return;
    }
    if (this.mutationBefore || this.pendingCityRegion) {
      this.setStatus("请先完成或取消当前绘制，再保存", "warning");
      return;
    }
    const revisionAtStart = this.mutationRevision;
    this.setSaving(true);
    try {
      this.setStatus("正在保存…", "pending");
      const geography = this.getGeography();
      const chunks = this.terrain.exportDirtyChunks();
      await Promise.all([
        workbenchApi.saveProject(this.project),
        workbenchApi.saveGeography(this.project.mapId, geography),
        chunks.length > 0 ? workbenchApi.saveTerrainMasks(this.project.mapId, chunks) : Promise.resolve({ ok: true, savedChunkIds: [] }),
      ]);
      if (this.mutationRevision === revisionAtStart) {
        this.terrain.markSaved(chunks.map((chunk) => chunk.chunkId));
        this.setDirty(false);
        this.setStatus(`已保存 · ${chunks.length} 个地貌 Chunk`, "success");
      } else {
        // UI editing is suspended while saving; this guard also protects future programmatic mutations.
        this.setStatus("保存完成，但期间出现了新修改，请再次保存", "warning");
      }
    } catch (error) {
      this.setStatus(`保存失败：${error instanceof Error ? error.message : String(error)}`, "error");
    } finally {
      this.setSaving(false);
    }
  }

  private async compileLocationGeometries(): Promise<void> {
    if (this.saving) return;
    if (!this.initialized) {
      this.setStatus("请先通过结构化表单新建地图", "warning");
      return;
    }
    this.runValidation();
    const errorCount = this.diagnostics.filter((item) => item.severity === "error").length;
    if (errorCount > 0) {
      this.setValidationExpanded(true);
      this.setStatus(`存在 ${errorCount} 个错误，修正后才能生成区域数据`, "error");
      return;
    }
    this.setSaving(true);
    try {
      this.setStatus("正在生成 territory mask、查询表和轮廓…", "pending");
      const geography = this.getGeography();
      await workbenchApi.saveGeography(this.project.mapId, geography);
      await workbenchApi.compileLocationGeometries(this.project.mapId, geography);
      this.layers.regionMaskLayer.setSource(new ImageStatic({
        url: `/project-assets/maps/${encodeURIComponent(this.project.mapId)}/draft/regions/territory_mask.png?v=${Date.now()}`,
        imageExtent: [0, -this.project.world.height, this.project.world.width, 0],
      }));
      const maskLayer = this.getLayerDefinition("region-masks");
      const maskVisibilityChanged = !maskLayer.visible;
      maskLayer.visible = true;
      this.layers.regionMaskLayer.setVisible(true);
      if (maskVisibilityChanged) this.setDirty(true);
      this.renderLayers();
      this.setStatus("区域产物已生成", "success");
    } catch (error) {
      this.setStatus(`生成失败：${error instanceof Error ? error.message : String(error)}`, "error");
    } finally {
      this.setSaving(false);
    }
  }

  private renderLayers(): void {
    const list = this.requireElement("layer-list");
    list.innerHTML = [...this.project.layers].reverse().map((definition) => {
      const index = this.project.layers.findIndex((layer) => layer.id === definition.id);
      const expanded = this.expandedLayerIds.has(definition.id);
      return `
        <div class="layer-row ${definition.locked ? "locked" : ""} ${definition.visible ? "" : "hidden-layer"}" data-layer-id="${definition.id}">
          <div class="layer-primary">
            <label class="layer-visible" title="显示或隐藏"><input type="checkbox" data-action="visible" ${definition.visible ? "checked" : ""}><span></span></label>
            <div class="layer-name"><strong>${escapeHtml(definition.label)}</strong><small>${this.layerKindLabel(definition.kind)}${definition.locked ? " · 已锁定" : ""}</small></div>
            <button class="icon-button layer-expand" data-action="expand" aria-expanded="${expanded}" title="${expanded ? "收起高级设置" : "展开高级设置"}">•••</button>
          </div>
          ${expanded ? `<div class="layer-advanced">
            <label class="layer-opacity"><span>透明度 <output>${Math.round(definition.opacity * 100)}%</output></span><input type="range" min="0" max="1" step="0.05" data-action="opacity" value="${definition.opacity}"></label>
            <div class="layer-actions">
              <button data-action="lock">${definition.locked ? "解锁编辑" : "锁定图层"}</button>
              <span class="layer-order-label">层级</span>
              <button data-action="raise" title="上移一层" ${index === this.project.layers.length - 1 ? "disabled" : ""}>↑</button>
              <button data-action="lower" title="下移一层" ${index === 0 ? "disabled" : ""}>↓</button>
            </div>
          </div>` : ""}
        </div>`;
    }).join("");
  }

  private handleLayerChange(event: Event): void {
    if (this.saving) return;
    const input = event.target as HTMLInputElement;
    const row = input.closest<HTMLElement>("[data-layer-id]");
    if (!row) return;
    const definition = this.getLayerDefinition(row.dataset.layerId as LayerId);
    const layer = this.layers.byId.get(definition.id);
    if (input.dataset.action === "visible") {
      definition.visible = input.checked;
      layer?.setVisible(input.checked);
      let stoppedEditing = false;
      let clearedSelection = false;
      if (!input.checked) {
        if (toolLayer[this.activeTool] === definition.id) {
          this.activateTool("select");
          stoppedEditing = true;
        }
        clearedSelection = this.clearSelectionForLayer(definition.id);
      }
      this.renderLayers();
      if (stoppedEditing || clearedSelection) this.setStatus(`${definition.label} 已隐藏，已退出相关编辑`, "warning");
    }
    if (input.dataset.action === "opacity") {
      definition.opacity = Number(input.value);
      layer?.setOpacity(definition.opacity);
      const output = row.querySelector("output");
      if (output) output.textContent = `${Math.round(definition.opacity * 100)}%`;
    }
    this.setDirty(true);
  }

  private handleLayerClick(event: Event): void {
    if (this.saving) return;
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-action]");
    const row = button?.closest<HTMLElement>("[data-layer-id]");
    if (!button || !row) return;
    const id = row.dataset.layerId as LayerId;
    const index = this.project.layers.findIndex((layer) => layer.id === id);
    if (button.dataset.action === "expand") {
      if (this.expandedLayerIds.has(id)) this.expandedLayerIds.delete(id);
      else this.expandedLayerIds.add(id);
      this.renderLayers();
      return;
    }
    if (button.dataset.action === "lock") {
      const definition = this.getLayerDefinition(id);
      definition.locked = !definition.locked;
      if (definition.locked) {
        if (toolLayer[this.activeTool] === id) this.activateTool("select");
        this.clearSelectionForLayer(id);
        this.setStatus(`${definition.label} 已锁定`, "warning");
      } else {
        this.setStatus(`${definition.label} 已解锁`, "info");
      }
      this.setDirty(true);
      this.renderLayers();
      return;
    }
    const delta = button.dataset.action === "raise" ? 1 : button.dataset.action === "lower" ? -1 : 0;
    const target = index + delta;
    if (delta !== 0 && target >= 0 && target < this.project.layers.length) {
      const [definition] = this.project.layers.splice(index, 1);
      if (definition) this.project.layers.splice(target, 0, definition);
      this.updateLayerOrder();
      this.setDirty(true);
      this.renderLayers();
    }
  }

  private layerKindLabel(kind: string): string {
    return ({ reference: "参考对照", canonical: "可编辑内容", derived: "检查结果" } as Record<string, string>)[kind] ?? kind;
  }

  private updateLayerOrder(): void {
    this.project.layers.forEach((definition, index) => this.layers.byId.get(definition.id)?.setZIndex(index));
  }

  private renderProperties(): void {
    const panel = this.requireElement("property-panel");
    const feature = this.select?.getFeatures().item(0);
    if (!feature) {
      panel.innerHTML = `<div class="empty-state"><strong>未选择对象</strong><span>完成新建或使用“选择 / 修改”点击对象后，这里会显示属性。</span></div>`;
      if (this.openDrawer === "inspector") this.setDrawer(undefined);
      return;
    }
    this.setDrawer("inspector");
    const properties = feature.getProperties();
    const geometry = feature.getGeometry();
    const mapCoordinate = geometry?.getType() === "Point" ? (geometry as import("ol/geom/Point.js").default).getCoordinates() : geometry?.getExtent().slice(0, 2);
    const coordinate = mapCoordinate ? toGameCoordinate(mapCoordinate) : undefined;
    const fields: Array<{ key: string; label: string; type?: "number"; value: unknown; help?: string; min?: number; max?: number; step?: number; options?: Array<{ value: string; label: string }>; readonly?: boolean; advanced?: boolean }> = [];
    if (properties.featureType) {
      fields.push({ key: "name", label: "名称", value: properties.name ?? "", help: "用于制作时识别，可稍后补充" }, { key: "featureId", label: "对象 ID (FeatureId)", value: properties.featureId, help: "稳定 ID；被其他数据引用后不要随意修改" });
      if (properties.featureType === "river") fields.push({ key: "widthClass", label: "河流宽度等级", type: "number", min: 1, max: 5, step: 1, value: properties.widthClass ?? 2 }, { key: "receiverId", label: "汇入河流 ID", value: properties.receiverId ?? "", help: "端点吸附时自动建立，也可手动修正" });
      if (properties.featureType === "road") fields.push({ key: "roadClass", label: "道路等级", type: "number", min: 1, max: 5, step: 1, value: properties.roadClass ?? 1 });
      if (properties.featureType === "mountain") fields.push({ key: "density", label: "山脉密度", type: "number", min: 0, max: 1, step: 0.05, value: properties.density ?? 0.5 });
    } else if (properties.locationType) {
      const cityLocation = properties.locationType === "main-city" || properties.locationType === "auxiliary-city";
      fields.push({ key: "name", label: "名称", value: properties.name ?? "", help: cityLocation ? "可稍后命名；改名不会改变稳定身份或区域绑定" : undefined });
      if (!cityLocation) fields.push({ key: "locationType", label: "类型", value: properties.locationType, options: [
          { value: "gate", label: "关隘" }, { value: "bridge", label: "桥梁" },
          { value: "ferry", label: "渡口" }, { value: "port", label: "港口" }, { value: "ruin", label: "遗迹" }, { value: "resource-site", label: "资源点" },
        ] });
      fields.push({ key: "locationId", label: "LocationId", value: properties.locationId, help: "自动生成的稳定身份", readonly: true, advanced: true });
      if (cityLocation) fields.push(
        { key: "locationType", label: "城市角色", value: properties.locationType === "main-city" ? "主城" : "辅城", readonly: true, advanced: true },
        { key: "provinceId", label: "ProvinceId", value: properties.provinceId ?? "", help: "省份归属由统一创建事务绑定", readonly: true, advanced: true },
      );
    } else if (properties.anchorType) {
      fields.push(
        { key: "name", label: "名称", value: properties.name ?? "" },
        { key: "anchorType", label: "锚点类型", value: properties.anchorType, options: [
          { value: "source", label: "源头" }, { value: "lake", label: "湖泊" }, { value: "coast", label: "海岸" },
        ] },
        { key: "anchorId", label: "锚点 ID (AnchorId)", value: properties.anchorId, help: "河流端点吸附后会引用此 ID" },
      );
    } else if (properties.direction && properties.locationId && properties.provinceId) {
      fields.push(
        { key: "direction", label: "方向", value: properties.direction, options: [
          { value: "center", label: "中央" }, { value: "north", label: "北" }, { value: "northeast", label: "东北" },
          { value: "east", label: "东" }, { value: "southeast", label: "东南" }, { value: "south", label: "南" },
          { value: "southwest", label: "西南" }, { value: "west", label: "西" }, { value: "northwest", label: "西北" },
        ] },
        { key: "provinceId", label: "ProvinceId", value: properties.provinceId, help: "必须与城市定义的省份一致", readonly: true, advanced: true },
        { key: "locationId", label: "LocationId", value: properties.locationId, help: "直接绑定主城或辅城；不创建独立区域身份", readonly: true, advanced: true },
      );
    }
    const objectId = String(properties.featureId ?? properties.anchorId ?? properties.locationId ?? "");
    const relatedDiagnostics = this.diagnostics.filter((item) => item.objectId === objectId);
    const renderField = (field: typeof fields[number]) => `<label><span>${escapeHtml(field.label)}</span>${field.options
      ? `<select data-property="${field.key}">${field.options.map((option) => `<option value="${option.value}" ${option.value === field.value ? "selected" : ""}>${escapeHtml(option.label)}</option>`).join("")}</select>`
      : `<input ${field.readonly ? "readonly" : `data-property="${field.key}"`} type="${field.type ?? "text"}" ${field.min !== undefined ? `min="${field.min}"` : ""} ${field.max !== undefined ? `max="${field.max}"` : ""} step="${field.step ?? 1}" value="${escapeHtml(field.value)}">`}${field.help ? `<small>${escapeHtml(field.help)}</small>` : ""}</label>`;
    const ordinaryFields = fields.filter((field) => !field.advanced);
    const advancedFields = fields.filter((field) => field.advanced);
    panel.innerHTML = `
      <div class="selection-summary"><div><span>${escapeHtml(this.objectTypeLabel(properties))}</span><strong>${escapeHtml(properties.name || objectId)}</strong></div>${coordinate ? `<small>X ${Math.round(coordinate[0])} · Y ${Math.round(coordinate[1])}</small>` : ""}</div>
      ${relatedDiagnostics.length > 0 ? `<div class="object-diagnostics"><strong>${relatedDiagnostics.length} 个相关问题</strong>${relatedDiagnostics.map((item) => `<span>${escapeHtml(item.message)}</span>`).join("")}</div>` : ""}
      <div class="property-fields">${ordinaryFields.map(renderField).join("")}</div>
      ${advancedFields.length > 0 ? `<details class="identity-details property-identities"><summary>高级身份信息（只读）</summary><div class="property-fields">${advancedFields.map(renderField).join("")}</div></details>` : ""}
      <button class="danger" data-action="delete-feature">删除对象</button>`;
  }

  private objectTypeLabel(properties: Record<string, unknown>): string {
    if (properties.direction && properties.locationId && properties.provinceId && !properties.locationType) return "城市区域";
    const type = String(properties.featureType ?? properties.anchorType ?? properties.locationType ?? "object");
    return ({ river: "河流", road: "道路", mountain: "山脉 / 高地", source: "源头锚点", lake: "湖泊锚点", coast: "海岸锚点", "main-city": "主城", "auxiliary-city": "辅城", gate: "关隘", bridge: "桥梁", ferry: "渡口", port: "港口", ruin: "遗迹", "resource-site": "资源点", object: "对象" } as Record<string, string>)[type] ?? type;
  }

  private handlePropertyChange(event: Event): void {
    const input = (event.target as HTMLElement).closest<HTMLInputElement | HTMLSelectElement>("[data-property]");
    const feature = this.select.getFeatures().item(0);
    if (!input || !feature) return;
    const layerId = this.featureLayerId(feature);
    if (this.saving || (layerId && this.getLayerDefinition(layerId).locked)) {
      this.renderProperties();
      if (layerId) this.setStatus(`${this.getLayerDefinition(layerId).label} 已锁定，无法修改对象`, "warning");
      return;
    }
    const before = this.captureSnapshot();
    const propertyKey = input.dataset.property!;
    const stableIdKeys = new Set(["featureId", "anchorId", "locationId", "provinceId"]);
    let value: string | number = input instanceof HTMLInputElement && input.type === "number" ? Number(input.value) : input.value;
    if (stableIdKeys.has(propertyKey)) value = String(value).trim();
    if (stableIdKeys.has(propertyKey) && value === "") {
      this.renderProperties();
      this.setStatus("稳定 ID 不能为空", "warning");
      return;
    }
    feature.set(propertyKey, value);
    if (stableIdKeys.has(propertyKey)) feature.setId(String(value));
    this.history.commit(before);
    this.setDirty(true);
    this.refreshAfterMutation();
    if (propertyKey === "name" && feature.get("locationType")) this.renderWorkspaceContext();
  }

  private deleteSelectedFeature(): void {
    const feature = this.select.getFeatures().item(0);
    if (!feature) return;
    const layerId = this.featureLayerId(feature);
    if (this.saving || (layerId && this.getLayerDefinition(layerId).locked)) {
      if (layerId) this.setStatus(`${this.getLayerDefinition(layerId).label} 已锁定，无法删除对象`, "warning");
      return;
    }
    const before = this.captureSnapshot();
    for (const source of [this.layers.waterSource, this.layers.waterAnchorSource, this.layers.roadSource, this.layers.mountainSource, this.layers.locationSource, this.layers.regionSource]) source.removeFeature(feature);
    this.select.getFeatures().clear();
    this.history.commit(before);
    this.setDirty(true);
    this.refreshAfterMutation();
    this.setDrawer(undefined);
  }

  private locateDiagnostic(item: ValidationItem): void {
    if (this.saving) return;
    let workspaceContextSynced = false;
    let lockedLayerLabel: string | undefined;
    if (item.layerId) {
      const definition = this.getLayerDefinition(item.layerId);
      if (!definition.visible) {
        definition.visible = true;
        this.layers.byId.get(item.layerId)?.setVisible(true);
        this.setDirty(true);
      }
      const workspace = layerWorkspace[item.layerId];
      this.renderLayers();
      if (workspace) {
        // Problem navigation exits authoring mode so the destination context is visible without leaving a stale draw interaction active.
        this.activeWorkspace = workspace;
        this.activateTool("select");
        workspaceContextSynced = true;
      }
    }
    if (item.coordinate) this.map.getView().animate({ center: toMapCoordinate(item.coordinate), zoom: Math.max(this.map.getView().getZoom() ?? 0, 3), duration: 250 });
    if (item.objectId) {
      const sources = [this.layers.waterSource, this.layers.waterAnchorSource, this.layers.roadSource, this.layers.mountainSource, this.layers.locationSource, this.layers.regionSource];
      const feature = sources.flatMap((source) => source.getFeatures()).find((candidate) => String(candidate.getId() ?? candidate.get("featureId") ?? candidate.get("anchorId") ?? candidate.get("locationId")) === item.objectId);
      if (feature) {
        if (!workspaceContextSynced) this.activateTool("select");
        const featureLayerId = this.featureLayerId(feature);
        lockedLayerLabel = featureLayerId && this.getLayerDefinition(featureLayerId).locked ? this.getLayerDefinition(featureLayerId).label : undefined;
        this.select.getFeatures().clear();
        if (!lockedLayerLabel) this.select.getFeatures().push(feature);
        this.renderProperties();
        if (lockedLayerLabel && this.openDrawer === "inspector") this.setDrawer(undefined);
      } else if (!workspaceContextSynced) {
        this.renderWorkspaceNavigation();
        this.renderWorkspaceContext();
      }
    }
    this.setStatus(`已定位：${item.message}${lockedLayerLabel ? ` · ${lockedLayerLabel} 已锁定` : ""}`, item.severity === "error" ? "error" : "warning");
  }

  private handlePointerMove(mapCoordinate: number[], pixel: number[]): void {
    const coordinate = toGameCoordinate(mapCoordinate);
    const terrainId = this.terrain.getTerrainIdAtWorld(coordinate[0], coordinate[1]);
    const terrain = this.project.terrainTypes.find((type) => type.id === terrainId);
    this.requireElement("coordinate-status").textContent = `X ${Math.round(coordinate[0])} · Y ${Math.round(coordinate[1])}`;
    this.requireElement("terrain-status").textContent = `地貌 ${terrain ? `${terrain.label} · ${terrain.id}` : terrainId === 0 ? "未分类 · 0" : "—"}`;

    let locationId: string | undefined;
    let provinceId: string | undefined;
    this.map.forEachFeatureAtPixel(pixel, (feature) => {
      if (!locationId && feature.get("locationId")) {
        locationId = String(feature.get("locationId"));
        provinceId = String(feature.get("provinceId") ?? "");
      }
    }, { layerFilter: (layer) => layer === this.layers.regionLayer });
    if (this.hoverRegion.locationId !== locationId || this.hoverRegion.provinceId !== provinceId) {
      this.hoverRegion = { locationId, provinceId };
      this.layers.regionLayer.changed();
    }
    this.requireElement("region-status").textContent = `Location ${locationId ?? "—"}`;
  }

  private getLayerDefinition(id: LayerId) {
    const definition = this.project.layers.find((layer) => layer.id === id);
    if (!definition) throw new Error(`Missing layer definition ${id}`);
    return definition;
  }

  private featureLayerId(feature: Feature): LayerId | undefined {
    const featureType = feature.get("featureType");
    return feature.get("direction") !== undefined && feature.get("provinceId") !== undefined && feature.get("locationType") === undefined
      ? "territories"
      : feature.get("anchorType") !== undefined
        ? "water"
        : feature.get("locationType") !== undefined
          ? "strategic-locations"
          : featureType === "river"
            ? "water"
            : featureType === "road"
              ? "roads"
              : featureType === "mountain"
                ? "mountains"
                : undefined;
  }

  private clearSelectionForLayer(layerId: LayerId): boolean {
    const selected = this.select.getFeatures().getArray();
    if (!selected.some((feature) => this.featureLayerId(feature) === layerId)) return false;
    // Clearing the whole collection avoids leaving Shift-multiselected handles active on a newly hidden or locked layer.
    this.select.getFeatures().clear();
    this.renderProperties();
    if (this.openDrawer === "inspector") this.setDrawer(undefined);
    return true;
  }

  private updateHistoryButtons(): void {
    this.requireElement<HTMLButtonElement>("undo").disabled = this.saving || !this.history.canUndo;
    this.requireElement<HTMLButtonElement>("redo").disabled = this.saving || !this.history.canRedo;
  }

  private setStatus(message: string, kind: "info" | "pending" | "success" | "warning" | "error"): void {
    const status = this.requireElement("operation-status");
    status.textContent = message;
    status.className = `operation-status ${kind}`;
  }

  private toolLabel(tool: ToolId): string {
    return ({
      select: "选择与修改",
      "terrain-brush": "地貌笔刷",
      "terrain-erase": "地貌擦除",
      "terrain-fill": "地貌填充",
      "terrain-lasso": "地貌套索",
      "terrain-polygon": "地貌多边形填充",
      river: "绘制河流",
      "water-anchor": "放置源头、湖泊或海岸锚点",
      road: "绘制道路",
      mountain: "绘制山脉",
      location: "放置战略地点",
      region: "绘制城市区域",
    } satisfies Record<ToolId, string>)[tool];
  }

  private requireElement<T extends HTMLElement = HTMLElement>(id: string): T {
    const element = this.root.querySelector<T>(`#${id}`);
    if (!element) throw new Error(`Workbench UI element missing: ${id}`);
    return element;
  }
}
