# 大世界地理工作台

本目录是独立的本地 Web 地理数据编辑与校验工具。它不参与 Godot 运行时，也不生成正式 Chunk 美术或导航数据。

## 运行

```powershell
cd tools\world-map-workbench
npm install
npm run dev
```

浏览器入口：`http://127.0.0.1:4173/`

生产构建与本地服务：

```powershell
npm run build
npm start
```

生产入口：`http://127.0.0.1:4174/`

服务默认从当前目录向上寻找 `project.godot`，也可以显式设置：

```powershell
$env:WORLD_MAP_PROJECT_ROOT = 'D:\godot\rpg'
npm start
```

## 工作方式

界面按制作任务拆成五个工作区，不要求一次理解全部功能。顶部只保留保存、撤销、重做等全局操作；左侧选择当前任务后，工具、参数、操作步骤和完成方式会随工作区切换。进入工作区时默认处于安全的“选择 / 修改”状态，只有主动选择具体绘制或放置工具后才会创建内容。地图始终保留在中央，图层和对象属性需要时再从右侧打开，底部校验默认只显示摘要。

| 工作区 | 典型操作入口 |
| --- | --- |
| 地貌绘制 | 先选地貌类型，再选笔刷、擦除、填充、套索或多边形，直接在地图上绘制并检查 Chunk 接缝。 |
| 水系与交通 | 选择河流、水系锚点、道路或山脉；折线逐点单击、双击完成，完成后在对象属性中补充等级和关系。 |
| 战略地点 | 先选城池、关隘、桥梁等地点类型，再点击地图放置；放置后在对象属性中填写稳定 ID、名称和详细地图 ID。 |
| 城域与区域 | **必须先填写归属 `CityId`**，再选择城域或小区域并绘制边界；未填写时不会开始绘制。 |
| 检查与生成 | 运行校验、展开问题清单并点击问题定位；修正阻断问题后生成区域 Mask、查询表和轮廓数据。 |

常用的跨工作区入口：

- 按 `V` 或选择“选择 / 修改”，再点击对象；对象属性会在右侧打开。
- 点击顶部“图层”打开图层抽屉，集中调整显示、锁定、透明度和顺序，关闭后继续当前任务。
- 隐藏或锁定正在编辑的图层会退出对应工具并清除相关选择；重新选择绘制工具时会自动显示目标图层。
- 点击底部校验状态条展开或收起问题清单；定位问题会切换到对应任务。
- 折线和多边形通过双击完成，按 `Esc` 取消当前绘制并返回选择状态。

## 已实现功能

- 十层显示栈：参考地图、地貌、水系、山脉、道路、战略地点、城域/小区域、正式 Chunk 美术、区域 Mask、校验信息。
- 每层显示、隐藏、锁定、透明度和顺序控制。
- 地貌笔刷、擦除、填充、套索和多边形填充；跨 Chunk 操作写入全部受影响 Mask。
- 地貌 ID 指示、未分类、未知 ID 和孤立区域检查。
- 河流、道路和山脉全局折线绘制、选择、控制点修改、属性编辑及逐 Chunk 裁切预览。
- 源头、湖泊和海岸水系锚点；河流端点吸附、支流 `receiverId` 及端点 AnchorId 自动建立。
- 城池、关隘、桥梁、渡口、港口、遗迹和资源点放置及属性编辑。
- 稳定 ID、参考偏差、水系/山脉冲突和 Chunk 边界终止检查。
- 城域与小区域绘制、`RegionId` Hover、城市整体高亮、拓扑校验和城市外轮廓预览。
- `territory_mask.png`、`region_lookup.json` 和 `region_outlines.json` 编译。
- 最多 30 步的编辑撤销/重做、错误定位和显式保存状态。

## 数据边界

工具代码、测试和构建配置全部位于本目录。首次保存会按权威目录写入：

```text
config/world/workbench.project.json
config/world/geography.json
assets/textures/world/masks/terrain/*.png
assets/textures/world/masks/territory/territory_mask.png
assets/textures/world/masks/territory/region_lookup.json
assets/textures/world/masks/territory/region_outlines.json
```

`workbench.project.json` 是 Chunk、坐标、图层和地貌类型契约；`geography.json` 是河流、道路、山脉、战略地点和区域几何的权威文本数据。Godot 不应另建一份可编辑副本。

浏览器显示层内部使用负 Y 映射适配 OpenLayers 画布方向，但保存和界面显示始终使用 Godot 的原始世界坐标，保存数据不包含额外缩放或偏移。

本地服务只允许访问 `config/world/` 与 `assets/textures/world/`。绝对路径、目录穿越和其他项目路径会被拒绝。工具不会读取或修改 `C:\Users\qs\asset` 外部素材库。

## 快捷操作

- `V`：选择与修改
- `Ctrl+S`：保存
- `Ctrl+Z`：撤销
- `Ctrl+Y`：重做
- `Delete`：删除选中对象

地貌笔刷、擦除和填充直接作用于地图。河流、道路、山脉和多边形工具通过单击增加控制点，双击完成。选择工具可拖动控制点或对象，并在右侧修改稳定 ID 和业务属性。输入框获得焦点时，`V` 和 `Delete` 不会触发全局快捷操作；按 `Ctrl+S` 会先提交当前输入值再保存。

## 验证

```powershell
npm test
npm run typecheck
npm run build
```

所有服务与浏览器资源都绑定到 `127.0.0.1`，不会监听外网地址。
