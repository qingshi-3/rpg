export type ToolId =
  | "select"
  | "terrain-brush"
  | "terrain-erase"
  | "terrain-fill"
  | "terrain-lasso"
  | "terrain-polygon"
  | "river"
  | "water-anchor"
  | "road"
  | "mountain"
  | "location"
  | "region";

export type WorkspaceId = "terrain" | "networks" | "locations" | "regions" | "review";

export interface ToolUiDefinition {
  id: ToolId;
  label: string;
  shortLabel: string;
  instruction: string;
  nextStep: string;
}

export interface WorkspaceUiDefinition {
  id: WorkspaceId;
  index: string;
  glyph: string;
  label: string;
  shortLabel: string;
  description: string;
  defaultTool: ToolId;
  tools: ToolId[];
  steps: [string, string, string];
}

export const toolUiDefinitions: Record<ToolId, ToolUiDefinition> = {
  select: {
    id: "select",
    label: "选择与修改",
    shortLabel: "选择 / 修改",
    instruction: "点击对象进行选择；拖动对象或控制点即可修改。按 Esc 可从绘制状态返回这里。",
    nextStep: "选中对象后会自动打开属性面板。",
  },
  "terrain-brush": {
    id: "terrain-brush",
    label: "地貌笔刷",
    shortLabel: "笔刷",
    instruction: "在地图上按住并拖动，可跨 Chunk 连续绘制当前地貌。",
    nextStep: "切换地貌类型或半径后可继续覆盖绘制。",
  },
  "terrain-erase": {
    id: "terrain-erase",
    label: "地貌擦除",
    shortLabel: "擦除",
    instruction: "在地图上按住并拖动，把经过的地貌恢复为未分类。",
    nextStep: "擦除后的空白会出现在检查结果中。",
  },
  "terrain-fill": {
    id: "terrain-fill",
    label: "连片填充",
    shortLabel: "连片填充",
    instruction: "点击一片相连的地貌，一次替换整片区域。",
    nextStep: "适合快速铺设大面积基础地貌。",
  },
  "terrain-lasso": {
    id: "terrain-lasso",
    label: "套索填充",
    shortLabel: "套索",
    instruction: "按住鼠标自由圈出范围，松开后用当前地貌填充。",
    nextStep: "适合勾勒自然、不规则的地貌边缘。",
  },
  "terrain-polygon": {
    id: "terrain-polygon",
    label: "多边形填充",
    shortLabel: "多边形",
    instruction: "逐点单击绘制边界，双击最后一个点完成填充；Esc 取消。",
    nextStep: "适合精确控制城郊、海岸等边界。",
  },
  river: {
    id: "river",
    label: "绘制河流",
    shortLabel: "河流",
    instruction: "逐点单击确定河道走向，双击完成；端点会自动尝试吸附。",
    nextStep: "完成后会自动选中河流，可填写名称并检查汇入关系。",
  },
  "water-anchor": {
    id: "water-anchor",
    label: "放置水系锚点",
    shortLabel: "水系锚点",
    instruction: "先选择源头、湖泊或海岸，再点击地图放置锚点。",
    nextStep: "新画河流的端点可吸附到附近锚点。",
  },
  road: {
    id: "road",
    label: "绘制道路",
    shortLabel: "道路",
    instruction: "逐点单击确定道路走向，双击完成；Esc 取消。",
    nextStep: "完成后会自动选中道路，可填写名称和道路等级。",
  },
  mountain: {
    id: "mountain",
    label: "绘制山脉与高地",
    shortLabel: "山脉 / 高地",
    instruction: "逐点单击勾勒山脉轴线，双击完成；Esc 取消。",
    nextStep: "完成后会自动选中山脉，可调整名称和密度。",
  },
  location: {
    id: "location",
    label: "放置战略地点",
    shortLabel: "放置地点",
    instruction: "先选择地点类型，再点击地图放置一个战略地点。",
    nextStep: "放置后会自动打开属性，请补充稳定 LocationId、ProvinceId 和主城/辅城类型。",
  },
  region: {
    id: "region",
    label: "绘制城市区域",
    shortLabel: "城市区域",
    instruction: "先指定已有主城或辅城的 LocationId，再逐点单击围合其唯一视觉区域，双击完成；Esc 取消。",
    nextStep: "完成后请核对 ProvinceId、LocationId 与方向。",
  },
};

export const workspaceUiDefinitions: WorkspaceUiDefinition[] = [
  {
    id: "terrain",
    index: "01",
    glyph: "地",
    label: "地貌绘制",
    shortLabel: "地貌",
    description: "铺设草原、森林、沼泽等基础地貌，并处理边缘与空白。",
    defaultTool: "select",
    tools: ["terrain-brush", "terrain-erase", "terrain-fill", "terrain-lasso", "terrain-polygon"],
    steps: ["选择要绘制的地貌类型", "选择笔刷、填充或范围工具", "在地图上绘制并检查 Chunk 接缝"],
  },
  {
    id: "networks",
    index: "02",
    glyph: "线",
    label: "水系与交通",
    shortLabel: "水系交通",
    description: "建立河流、道路和山脉的连续全局线，并配置水系锚点。",
    defaultTool: "select",
    tools: ["river", "water-anchor", "road", "mountain"],
    steps: ["选择要新建的线状对象", "单击添加节点，双击完成", "检查属性、吸附关系与跨块连续性"],
  },
  {
    id: "locations",
    index: "03",
    glyph: "点",
    label: "省份与战略地点",
    shortLabel: "地点",
    description: "维护省份成员，并放置主城、辅城、关隘、港口、遗迹和资源点。",
    defaultTool: "select",
    tools: ["location"],
    steps: ["确认 ProvinceId 已存在", "选择地点类型并点击地图放置", "填写名称、LocationId 与 ProvinceId"],
  },
  {
    id: "regions",
    index: "04",
    glyph: "域",
    label: "省份与城市区域",
    shortLabel: "城市区域",
    description: "为每个主城或辅城维护唯一视觉几何，并检查省份归属与拓扑。",
    defaultTool: "select",
    tools: ["region"],
    steps: ["先指定城市 LocationId", "绘制该城市的唯一视觉边界", "核对 ProvinceId、方向与重叠问题"],
  },
  {
    id: "review",
    index: "05",
    glyph: "检",
    label: "检查与生成",
    shortLabel: "检查",
    description: "集中处理数据问题，确认无阻断后生成城市区域 Mask 与 LocationId 查询数据。",
    defaultTool: "select",
    tools: [],
    steps: ["运行地图校验并展开问题清单", "点击问题定位并修正对象", "生成区域数据，最后保存全部修改"],
  },
];

export function getWorkspaceDefinition(id: WorkspaceId): WorkspaceUiDefinition {
  const workspace = workspaceUiDefinitions.find((candidate) => candidate.id === id);
  if (!workspace) throw new Error(`Unknown workspace: ${id}`);
  return workspace;
}

export function workspaceForTool(tool: ToolId): WorkspaceId | undefined {
  return workspaceUiDefinitions.find((workspace) => workspace.tools.includes(tool))?.id;
}
