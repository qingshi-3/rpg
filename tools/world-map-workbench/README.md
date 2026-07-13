# 大世界地图工作台

本目录是独立的本地 Web 地图编辑与发布工具。它不参与 Godot 运行时；可编辑源是唯一创作权威，Godot 只读取发布后的不可变地图包。

## 运行

```powershell
cd tools\world-map-workbench
npm install
npm run dev
```

开发入口为 `http://127.0.0.1:4173/`。生产构建与本地服务：

```powershell
npm run build
npm start
```

服务默认向上查找 `project.godot`，也可通过 `WORLD_MAP_PROJECT_ROOT` 指定项目根目录。

## 多地图与保存边界

- 顶栏只承载地图/文档操作：创建、打开、复制和切换地图，以及校验、发布、撤销、重做、图层与保存；`MapId` 创建后不可修改。
- 地图目录明确区分普通创作地图和验证 fixture。裸地址会优先恢复仍存在的上次地图，否则打开目录声明的默认创作地图；fixture 只在用户显式选择时打开。
- 新建地图使用单一表单收集 `MapId`、显示名、Chunk 列数和行数，并在提交前显示世界总尺寸。首版 Chunk 固定为 `1024x1024`，身份、坐标、原点和草稿路径均由地图级矩形网格确定性生成。
- 地图设置只支持向右增加列、向下增加行；扩展保留全部既有 Chunk 身份、原点与创作坐标，只初始化新增 Chunk。缩小、左/上迁移和单独修改 Chunk 结构会在写入前被拒绝。
- 切换、新建、复制及关闭页面都会保护未保存修改。
- “城市区域”工作区使用统一的省份/城市区域创作流程：新建省份会自动生成防碰撞的 `ProvinceId`、`LayoutId` 与主城 `LocationId`，原子创建省份和主城后立即进入多边形绘制；在所选省份下“添加辅城区域”会自动生成辅城身份并立即绘制。完成首个多边形时绑定唯一城市几何并把地点标记初始化到几何质心；取消首次绘制会完整回滚本次创建，撤销/重做也把它视为一次操作。
- 地图画布与“城市区域”工作区共享一条基于 `ProvinceId + LocationId` 的选择同步路径：点击主城/辅城区域或城市标记会切换到其所属省份和准确城市，同时保留右侧属性面板对被点击对象的检查；点击地图空白或选择非城市对象只清除/替换地图对象选择，不丢失最近的省份与城市工作上下文。
- 城市成员按角色呈现：主城使用一张带城市图标、主城徽标和明确选中态的重点卡片；辅城使用紧凑可选卡片；“添加辅城区域”固定为辅城列表末尾的虚线添加卡片。稳定技术 ID 仍只在只读高级信息中显示。
- 初始省份名和城市名允许等于自动生成的稳定 ID，之后可独立改名且不会改变身份、归属、布局或几何绑定。普通流程不要求填写技术 ID；`ProvinceId`、`LocationId` 和 `LayoutId` 只在高级信息中只读显示。非城市战略地点继续使用既有地点工具。
- 草稿保存只要求结构可读，允许未完成内容反复保存和打开。
- 发布按能力档位执行严格校验；校验失败不会改变当前发布指针。

## 数据路由

```text
config/world/maps/catalog.json
config/world/maps/<MapId>/source/workbench.project.json
config/world/maps/<MapId>/source/geography.json
assets/textures/world/maps/<MapId>/draft/**

config/world/published/<MapId>/<Revision>/package.json
assets/textures/world/maps/<MapId>/<Revision>/**
config/world/published/<MapId>/current.json
```

源目录可编辑；发布目录由发布器生成且只读。视觉块、分类 Mask 与其他派生产物均按 MapId 和 Revision 隔离。分类 Mask 使用无 mipmap 的无损导入，视觉块使用无损并启用 mipmap。

区域发布使用 `rgb24-location-code-v1`：0 表示无区域，其余代码只用于查表并解析到稳定 `LocationId`，不作为玩法或存档身份。几何按创作顶点精确栅格化，重叠、拓扑合并失败和跨产物不一致会阻止发布。

## 验证

```powershell
npm run typecheck
npm test
npm run build
```

服务只监听本机地址，所有读写限制在项目的地图配置与纹理子树内；不会修改 `C:\Users\qs\asset`。
