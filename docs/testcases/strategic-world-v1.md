# 战略大世界 V1 测试用例

本文记录战略大世界 V1 的逻辑和手动验收用例。

## 逻辑测试

### 初始状态

前置：

- 创建 V1 `StrategicWorldState`。

期望：

- 玩家资源包含 `population`、`economy`、`stone`。
- 玩家营地为 `PlayerHeld`。
- 埋骨地为 `Hostile`。
- 墓园为 `Hostile`。
- 玩家营地有兵营。

### 建造矿场

前置：

- 埋骨地为 `PlayerHeld`。
- 玩家有 `population >= 1`、`economy >= 2`。

操作：

- 执行 `build_mine`。

期望：

- 经济减少 2。
- 人口被矿场占用 1。
- 埋骨地出现 active mine。
- 行动推进 1 WorldTick。

### 矿场产出

前置：

- 埋骨地为 `PlayerHeld`。
- 埋骨地有 active mine。

操作：

- 推进 1 WorldTick。

期望：

- 玩家石材 +2。
- 如果 mine 为 Damaged，则不产出。

### 建造防御塔

前置：

- 埋骨地为 `PlayerHeld`。
- 玩家有 `stone >= 4`、`economy >= 2`。

操作：

- 执行 `build_defense_tower`。

期望：

- 石材减少 4。
- 经济减少 2。
- 埋骨地出现 active defense_tower。
- 埋骨地防守值提高。

### 训练民兵

前置：

- 场域有 active barracks。
- 玩家有 `population >= 1`、`economy >= 2`。

操作：

- 执行 `train_militia`。

期望：

- 人口减少 1。
- 经济减少 2。
- 目标场域民兵 +1。

### Raid 生成

前置：

- 埋骨地为 `PlayerHeld`。
- 墓园为 `Hostile`。
- 没有 active graveyard raid。

操作：

- 推进 WorldTick。

期望：

- 创建一个 `graveyard_raid_bonefield` threat。
- 目标为埋骨地。
- threat 关联一支敌方 `WorldArmyState`，`EnemyThreatPlan.WorldArmyId` 指向该部队。
- 敌方部队从墓园导航点出发，目标为埋骨地导航点，意图为 `Raid`。
- Linked Raid 不再依赖旧 countdown marker 作为主要地图表现。

### Raid 推进

前置：

- 存在 linked Graveyard Raid 敌方 `WorldArmyState`，且 `StrategicNavigationTileLayer` 可用。

操作：

- 取消暂停或推进世界时钟，让敌方 Raid 部队沿导航路径移动并抵达埋骨地。

期望：

- 敌方部队抵达后 Raid 阶段变为 `Attacking`。
- UI 或服务层返回需要玩家处理的威胁。
- 不应在无确认情况下直接自动结算。
- 如果导航路径不可用，日志出现 `WorldArmyPathFailed`，不应静默回退为倒计时跳转攻击。

### WorldClock 自动推进

前置：

- 进入 `StrategicWorldRoot`。
- 没有 `Attacking` 状态的威胁。
- 世界时钟未暂停。

操作：

- 等待一个 `WorldTickIntervalSeconds` 周期。

期望：

- `WorldTick` 自动 +1。
- 矿场等 `SiteProduction` 正常结算。
- Raid 等 `ThreatProgression` 正常推进。
- 点击暂停后不再自动推进。

### 大地图威胁可视化

前置：

- 存在 Marching 状态的 Graveyard Raid。
- `WorldMapRoot/MapAnchors/Sites` 已配置墓园和埋骨地锚点。
- `WorldMapRoot/StrategicNavigationTileLayer` 已绘制从墓园到埋骨地的可导航区域，且对应 TileSet navigation polygon 生效。
- `StrategicMapLayer`、`StrategicBridgeLayer` 和 `SiteVisualLayer` 只作为视觉层，不作为战略行军寻路权威。

操作：

- 观察战略地图并推进世界步。

期望：

- 地图上能看到敌方军队 marker。
- marker 沿 Godot 2D navigation 返回的路径连续移动靠近埋骨地；地图上不绘制场域连线。
- marker 不会因为视觉陆地或桥面 tile 存在就穿过未绘制 `StrategicNavigationTileLayer` 的区域。
- Raid 到达后 marker 停在目标附近，世界时钟暂停，并自动触发战斗提示弹窗。

### 大地图 Godot 导航层

前置：

- `StrategicNavigationTileLayer` 是唯一战略行军导航绘制层。
- `StrategicNavigationTileLayer` 的导航 tile 配有可用 TileSet navigation polygon。
- `StrategicMapLayer` 只放大地图地貌视觉，`StrategicBridgeLayer` 只放桥面视觉；两者不产生战略行军路径。
- 玩家营地、埋骨地、墓园锚点都放在导航区域上，或在配置的搜索半径内能解析到最近导航点。

操作：

- 从墓园生成向埋骨地移动的敌军。
- 从玩家营地点击出征，选择英雄或小兵后右键埋骨地或己方场域。
- 选中玩家小队后右键点击 `StrategicNavigationTileLayer` 外的空白区域。
- 临时断开或清空导航层后再次下达移动 / 出征指令。

期望：

- 可达指令成功时日志出现 `WorldArmyPathBuilt`，且 `provider=godot_navigation`。
- 军队不会穿过未绘制 `StrategicNavigationTileLayer` 或 navigation polygon 未生效的区域。
- 视觉层桥面和陆地可以重叠，但是否可走只取决于 `StrategicNavigationTileLayer`。
- 右键空白区域时指令被拒绝，日志出现 `WorldArmyCommandNavigationRejected`，已选小队保持原状态。
- 导航层缺失、为空、断开或目标不可达时日志出现 `WorldArmyPathFailed`，系统不创建直线 fallback 路径。
- 军队出发点和目标点优先使用 `MapAnchors/ArmySpawnPoints/<site_id>`；未配置时使用 `MapAnchors/Sites/<site_id>` 附近最近导航点。
- 如果场域中心不在导航层上，日志出现 `SiteNavigationPointResolved`，军队从解析后的导航点出发或抵达。

### 战斗触发提示和战前情报

前置：

- 存在可触发战斗的部队移动或 `Attacking` 威胁。

操作：

- 让玩家攻城部队抵达目标场域，或让玩家部队与敌军在野外接触，或让敌军 Raid 抵达目标场域。

期望：

- 大地图立即把触发点对焦到可视地图区域中心。
- 先弹出一个提示框，文案为发生了战斗，且只有 `确定` 按钮。
- 点击 `确定` 后进入战前情报界面。
- 战前情报展示我方和敌方部队信息。
- 如果是攻城或守城，战前情报还展示目标场域的控制状态、归属、受损、建筑和驻军信息。
- 再确认后才进入 `WorldSiteRoot` 战斗场景。
- 敌军 Raid 抵达时不需要再点击右侧威胁处理按钮，抵达事件本身就是守城战入口。

### 大地图小队选择和右键指令

前置：

- 大地图上存在至少 1 支玩家小队。
- `StrategicNavigationTileLayer` 已绘制可走表面并配置 TileSet navigation polygon。

操作：

- 左键点击玩家小队。
- 左键拖框圈选玩家小队。
- 右键点击导航层内的可走地表。
- 右键点击导航层外的空白地表。

期望：

- 点击可选中单支小队，拖框可选中多支小队。
- 被选中小队有选中环。
- 右键地表后小队进入 `Moving`，意图为 `MoveToPosition`。
- 小队按 RTS 连续路径移动，不按战棋格逐格移动。
- 小队移动 marker 绘制在场域节点之上，不会到达节点后被场域图标盖住。
- 右键空白地表时小队不改变原状态，并给出路径拒绝反馈或日志。

### 己方场域出征发起

前置：

- 玩家营地为 `PlayerHeld`。
- 玩家营地有可出征单位；V1 默认至少有 `骑士 x1` 和 `民兵 x1`。

操作：

- 选中玩家营地。
- 点击右侧行动里的 `出征`。
- 调整英雄和小兵数量。
- 点击 `选择目的地`。
- 右键点击大地图空地。

期望：

- 出征入口只出现在己方场域，不出现在敌方埋骨地或墓园的目的地面板。
- 编成面板显示英雄行和小兵行；V1 英雄行使用 `骑士`。
- 英雄和小兵数量可以分别调整为 0，且不能超过来源场域可用数量。
- 右键空地后创建玩家小队，来源为玩家营地，意图为 `MoveToPosition`。
- 玩家营地扣除所选英雄 / 小兵，小队在大地图上可见并被选中。

### 大地图进攻和未配置目标

前置：

- 玩家营地有至少 1 个可出征英雄或小兵。
- 埋骨地为敌方控制。
- 墓园为敌方控制，且未配置攻占战。

操作：

- 从玩家营地点击出征，选择英雄或小兵后右键点击埋骨地。
- 再次从玩家营地点击出征，选择英雄或小兵后右键点击墓园。

期望：

- 右键埋骨地后创建进攻小队并向埋骨地移动，抵达后触发攻占战提示和战前情报。
- 右键墓园时立即提示墓园暂未配置攻占战，不能进攻。
- 墓园拒绝后小队不移动、不进入 `Attacking`，也不会在墓园节点下消失。
- 出征选择目的地时悬停墓园显示禁止光标。

### 场域 footprint 边缘导航

前置：

- 大地图配置了 `SiteVisualLayer` 场域素材。
- 场域素材内部不绘制 `StrategicNavigationTileLayer`，但边缘外侧有可行军导航 tile。
- 玩家小队已选中，且当前所在点在可行军区域。

操作：

- 右键点击场域建筑 / 场域 footprint 内部。
- 再右键点击普通可行军地表。

期望：

- 点击场域 footprint 时不会因为建筑内部不可导航而直接拒绝命令。
- 日志出现 `SiteApproachNavigationPointResolved`，导航终点落在从小队方向接近场域 footprint 的可行军边缘。
- 小队抵达边缘导航终点后，marker 视觉上略微贴近场域建筑边缘。
- 之后右键普通地表移动时，场域接近偏移被清空，小队按普通 `MoveToPosition` 目标行军。

### 大地图进驻和驻军容量

前置：

- 玩家营地有至少 1 个可出征英雄或小兵。
- 目标己方场域配置了 `DefaultGarrisonZone`。

操作：

- 从玩家营地点击出征，选择英雄或小兵后右键点击己方场域。
- 重复进驻直到驻军区容量满。

期望：

- 有空位时小队向目标场域移动，抵达后转为 `Garrisoned`，并从大地图移动 marker 中隐藏。
- 场域驻军数量增加，`WorldSiteUnitPlacement` 生成在默认驻军区空 cell。
- 容量满时进驻指令被拒绝，提示驻军区已满。
- 有已选小队悬停满员己方场域时鼠标显示禁止光标。

### Site 驻军拖动持久化

前置：

- 大地图上选中一个玩家控制的场域。
- 该场域有驻军和可用地图格。

操作：

- 点击右侧行动里的 `进入场域`。
- 点击驻军 marker。
- 拖动到另一个有效格后松开。
- 返回大地图，再重新进入该场域。

期望：

- 非战斗经营态隐藏战斗 HUD 和战斗单位层。
- 场域标题和经营数据对应刚才在大地图选择的目标场域。
- 拖动后提示驻军位置已更新。
- `WorldSiteState.UnitPlacements` 中对应 placement 的 cell 更新。
- 重新进入场域后，驻军 marker 出现在上次拖动后的 cell。

### 大地图相机

前置：

- 进入 `StrategicWorldRoot`。
- `WorldCamera` 存在并使用通用 `MapCameraController`。

操作：

- 使用 WASD 平移大地图。
- 使用鼠标滚轮缩放大地图。
- 使用鼠标中键拖拽大地图。
- 触发攻城、守城或野外遭遇。

期望：

- 大地图地表、场域节点、军队 marker 和可点击区域一起随相机移动或缩放。
- 顶部资源栏和右侧场域面板保持屏幕固定，不随大地图相机移动。
- 相机不会移动到 authored map bounds 之外。
- 战斗触发时相机对焦到触发点，而不是移动 `WorldMapRoot` 本身来伪装镜头。
- `WorldSiteRoot` 内的 site/battle 相机仍保留相同的移动、缩放和拖拽体验。

### Raid 自动结算

前置：

- Raid 为 `Attacking`。
- 埋骨地有 1 民兵和 1 防御塔。

操作：

- 执行自动结算。

期望：

- 使用防御塔和驻军计算防守值。
- 结果比没有防御塔更好。
- 结算后 threat 变 `Resolved`。

## 战斗衔接测试

### 攻占埋骨地胜利

前置：

- 埋骨地为 `Hostile`。

操作：

- 从战略地图进入攻占埋骨地战斗。
- 战斗返回 Victory。

期望：

- 埋骨地变为 `PlayerHeld`。
- 战斗结束推进 1 WorldTick。
- UI 留在当前 `WorldSiteRoot`，切换为非战时经营态。
- 非战时 UI 不遮住整张地图，地图仍可查看和作为经营交互面。
- UI 显示埋骨地可建造矿场和防御塔。

### 攻占埋骨地失败

前置：

- 埋骨地为 `Hostile`。

操作：

- 从战略地图进入攻占埋骨地战斗。
- 战斗返回 Defeat。

期望：

- 埋骨地仍为 `Hostile`。
- 玩家损失经济或人口。
- 游戏不 Game Over。

### 防守 Raid 胜利

前置：

- 埋骨地为 `PlayerHeld`。
- Raid 为 `Attacking`。
- 埋骨地驻军民兵已经在非战斗场域详细地图中拖动到非默认位置。

操作：

- 进入防守战。
- 战斗返回 Victory。

期望：

- 防守战中生成的民兵站位使用 `WorldSiteState.UnitPlacements` 中保存的位置，而不是默认出生点。
- Raid 变 `Resolved`。
- 埋骨地保持 `PlayerHeld`。
- 防御塔和矿场状态保持。

### 防守 Raid 失败

前置：

- 埋骨地为 `PlayerHeld`。
- Raid 为 `Attacking`。

操作：

- 进入防守战。
- 战斗返回 Defeat。

期望：

- 埋骨地变 `Damaged` 或 `Lost`。
- 驻军损失。
- 矿场或防御塔可能 Damaged。
- 游戏继续。

## UI 手动验收

### 资源栏

期望：

- 顶部显示人口、经济、石材、WorldTick。
- 人口能显示可用 / 总量。
- 资源变化后 UI 立即刷新。

### 场域面板

期望：

- 选中玩家营地能看到兵营和驻军。
- 选中埋骨地能看到控制权、建筑、驻军和威胁。
- 选中墓园能看到敌方源头说明。

### Site 非战时经营态

前置：

- 从 site 战斗中获得胜利或失败并触发战斗结束。

期望：

- 游戏停留在当前 `WorldSiteRoot`。
- 战斗 HUD 隐藏。
- 战斗单位层隐藏，避免继续表现为战斗态。
- 顶部显示人口、经济、石材、WorldTick 和返回大地图按钮。
- 地图上不生成建筑点 Button UI。
- 右侧经营面板显示建筑槽位状态；后续建筑地图交互应由 `site_interactions` 场景实体承担。
- 可用行动来自通用 action view model。
- 执行行动后资源、建筑、驻军、威胁和反馈立即刷新。

### 行动按钮

期望：

- 行动来自通用 action view model。
- 资源不足时按钮 disabled 并显示原因。
- 建造防御塔能显示成本和效果。
- 训练民兵能显示人口和经济成本。

### 威胁反馈

期望：

- 移动中的 Raid 可见来源、目标和预计到达信息；到达后的 `Attacking` 状态可见。
- Raid 敌军从墓园出现后会持续沿 `StrategicNavigationTileLayer` 移动，不会停在墓园出口；日志可见 `WorldArmyPathBuilt`，旧异常状态恢复时可见 `ThreatArmyRecovered`。
- Raid 到达时有明确处理入口。
- 玩家不会在无提示情况下丢失埋骨地。

### 野外小场域

前置：

- 战略大世界处于 V1 初始状态或任意可推进状态。
- `WorldClock` 可推进，或可通过“等待 / 整顿”手动推进 `WorldTick`。

操作：

- 推进多个 `WorldTick`，直到 `wilderness_v1` 规则触发。
- 在大地图点击出现的野外小场域 marker。
- 在右侧行动面板点击“处理机会”。

期望：

- 野外小场域按规则随机出现，活跃数量不超过规则上限。
- marker 位置落在配置的生成点半径和抖动范围内。
- 详情面板显示名称、描述、生成点、剩余世界步和奖励，不误显示为 `WorldSite`。
- 处理成功后 marker 立即消失，资源奖励正确写回。
- 未处理且超过 `ExpiresTick` 后，机会自动变为过期并从地图消失。
- 存档/读档后，活跃机会和规则冷却保持一致，不重复刷出同一时序机会。
- 日志或事件中能看到 `WorldOpportunitySpawned`、`WorldOpportunityCompleted` 或 `WorldOpportunityExpired`。

## 存档验收

前置：

- 埋骨地已占领。
- 已建矿场和防御塔。
- 有一支移动中的玩家小队或墓园 Raid 敌军，包含 `WorldPosition`、`Destination`、`Status`、`Intent`、来源场域、目标场域和成员。
- 如果是墓园 Raid 敌军，相关 `EnemyThreatPlan.WorldArmyId` 已指向该敌军。
- `StrategicNavigationTileLayer` 在保存前后保持可用。

操作：

- 保存。
- 退出或重载世界状态。
- 读档后取消暂停或下达新移动指令，让移动中的部队继续行军。

期望：

- 玩家资源恢复正确。
- 埋骨地仍为玩家控制。
- 矿场和防御塔仍存在。
- WorldTick 不重置。
- `WorldArmyState` 恢复部队位置、目标、状态、意图、来源、目标和成员。
- 墓园 Raid 的 threat 与敌军关联仍然存在，读档后不会退回旧 countdown marker 表现。
- 读档不会保存或依赖旧的运行时缓存路径；导航系统在继续移动时按当前 `StrategicNavigationTileLayer` 重建路径。
- 路径重建成功时日志出现 `WorldArmyPathBuilt provider=godot_navigation`。
- 如果读档后导航层缺失、为空或断开，日志出现 `WorldArmyPathFailed`，部队不会静默走直线 fallback。
