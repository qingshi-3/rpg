# 战略大地图 RTS 寻路与部队规划

本文定义战略大地图从“场域节点 + 威胁 marker”升级为“三群式大地图部队移动”的规划。它是 `strategic-world-v1.md` 的后续专题，不替代 V1 资源、场域、行动、战斗回写等基础规格。

该专题服务于当前产品方向：玩家默认以角色 / 小队身份在大世界游历，三群式部队移动和势力攻防是世界生态，不是要求玩家从开局就扮演势力君主。

## 目标

战略大地图应表达为可移动的世界地表：

```text
大地图地表
-> 场域节点
-> 玩家和敌方部队在地表连续移动
-> 到达、接触、拦截或攻打场域
-> 进入 WorldSite / Battle
-> 结果回写世界
-> 世界继续推进
```

场域仍然是 `WorldSite`，是可运营、可争夺、可进入的节点。场域之间不使用可见连线，也不恢复节点图路网。

## 硬性边界

- 大地图寻路采用 RTS 式连续空间寻路。
- 大地图部队使用 `Vector2` 世界坐标、移动速度和路径折线，不使用战棋格子坐标。
- 战斗地图仍可使用 `BattleGridMap`、`GridPosition` 和战棋移动规则。
- 大地图移动系统不得复用战斗格子 A*，不得让部队一格一格移动。
- `WorldTick` 是战略结算点；部队移动按帧或世界时钟连续推进。
- 敌方 Raid 也必须由 `WorldArmyState + StrategicNavigation` 驱动，不再使用手工 waypoint 伪装行军路径。
- World 与 Battle 继续通过 `BattleStartRequest` / `BattleResult` 交互，不能直接互改内部状态。

## 核心概念

| 概念 | 责任 |
|---|---|
| `WorldSite` | 大地图上的持久场域节点，保存控制权、建筑、驻军、威胁、入口。 |
| `WorldArmy` / `Expedition` | 大地图上可移动的部队实体。 |
| `ThreatPlan` | 敌方计划和意图，不等同于地图实体。 |
| `StrategicNavigation` | 战略地图连续空间寻路与到达判断。 |
| `Encounter` | 部队接触、拦截、野外遭遇或攻打场域时产生的战斗入口。 |
| `Opportunity` | 短期野外机会，例如救援、追杀、伏击、夺取、护送、残场或资源成熟。 |

`ThreatPlan` 可以创建敌方 `WorldArmy`，但二者职责不同：计划描述“为什么来”，部队描述“在哪里、怎么走、能不能被拦截”。

`Opportunity` 不应默认升级为 `WorldSite`。只有需要长期保存控制权、设施、驻军、损毁或持续入口的地点，才承担 `WorldSite` 职责。

## 数据模型方向

第一版部队运行态建议：

```text
WorldArmyState
  ArmyId
  OwnerFactionId
  SourceSiteId
  TargetSiteId
  WorldPosition: Vector2
  Destination: Vector2
  MoveSpeed
  Radius
  State
  Intent
  Members[]
  GarrisonUnits[]
  CargoResources[]
  CreatedTick
  RelatedThreatId?
```

状态先控制在最小集合：

```text
Idle
Garrisoned
Moving
Attacking
Retreating
Defeated
```

路径点可以作为运行时缓存：

```text
WorldArmyRuntime
  PathPoints: Vector2[]
  CurrentPathIndex
  RepathCooldown
```

存档优先保存 `WorldPosition`、`Destination`、`State`、成员和目标；读档后由导航系统重算路径。除非后续证明需要精确恢复行军轨迹，否则不把完整 path 当作权威存档。

## 地图制作合同

战略地图场景保留当前入口，并逐步扩展：

```text
StrategicWorldRoot
  WorldCamera                   # 大地图相机；复用通用 MapCameraController
  WorldMapRoot
    StrategicMapLayer              # TileMapLayer，陆地和基础地貌视觉表达，不参与寻路判定
    SiteVisualLayer                # TileMapLayer，场域大地图素材层，不参与通行判定
    StrategicBridgeLayer           # TileMapLayer，桥面视觉表达；可以和陆地 cell 重叠
    StrategicNavigationTileLayer   # TileMapLayer，专用导航绘制层，战略行军唯一权威可走面
    MapAnchors
      Sites                        # WorldSite 节点锚点
        player_camp
        bonefield
        graveyard
      ArmySpawnPoints              # 部队出生点，后续添加
      EncounterZones               # 野外遭遇、山口、伏击区，后续添加
```

地图视觉制作优先表达：

- 地表视觉。
- 不可通行区域，例如水体、断崖、山脉。
- 关键瓶颈，例如桥、山口、窄路。
- 场域入口附近的部队集结点。
- 后续可选地形代价，例如道路快、荒地慢、沼泽慢。

正式寻路优先考虑 Godot 2D 导航能力：

```text
TileSet navigation polygon
NavigationServer2D
```

TileMapLayer 可以继续承担视觉地表；是否直接从 TileMap 导出导航区域，取决于 Godot 工程配置和地图制作效率。

当前接入方式：

- `StrategicNavigationContext` 请求 Godot `NavigationServer2D`，路径点在 `WorldMapRoot` 本地坐标和 Godot 全局坐标之间转换。
- `StrategicNavigationTileLayer` 是大地图唯一权威导航绘制层；它通过 TileSet navigation polygon 注册到 Godot 导航系统。
- 如果 Godot 导航没有配置、尚未同步或返回空路径，系统必须报错并停止该部队，不允许回退到旧 TileMap A* 或直线路径。
- 部队路径缓存在 `WorldArmyState` 的运行态字段中；只有目标改变、导航版本变化、战斗/到达/失败等状态切换时才清空或重算。
- 碰撞配置不直接等同于寻路数据。碰撞负责物理阻挡、点击命中或烘焙来源；大地图行军路径以 Godot 2D navigation 和 `StrategicNavigationTileLayer` 为准。

当前导航层规则：

- `StrategicNavigationTileLayer` 上有导航 tile 的 cell 表示可走；无 tile 表示不可走。
- 导航 tile 的 TileSet 必须配置 navigation layer 和 full-cell navigation polygon。
- `StrategicMapLayer`、`StrategicBridgeLayer` 和 `SiteVisualLayer` 只负责视觉，不再作为寻路权威。
- 桥、陆地、场域视觉可以重叠；实际可走范围只由 `StrategicNavigationTileLayer` 决定。
- `MapAnchors/Sites/<site_id>` 是场域在大地图上的权威锚点，运行时会同步到 `WorldSiteDefinition.MapPosition` 供军队生成使用。
- `SiteVisualLayer` 只维护场域大地图素材；`MapAnchors/Sites/<site_id>` 必须落在对应素材内部。
- 运行时会从锚点所在 cell 对 `SiteVisualLayer` 做 4 向连通扫描，构建该场域的视觉 footprint。
- 场域按钮热区、选中描边和标签位置使用 footprint；没有素材或锚点未落入素材时才回退到旧圆形图标。
- 右键点击场域 footprint 时，行军目标不是场域素材内部。运行时会从部队当前位置到场域锚点作一条接近线，取这条线进入 footprint 的边界点，在边界外侧寻找最近的 `StrategicNavigationTileLayer` 可行军点作为导航终点。
- 对场域进攻 / 进驻命令，部队到达导航终点后可以应用一个小的场域接近偏移，让 marker 视觉上贴近建筑边缘；普通右键空地移动必须清除该接近偏移。
- 不再使用抽象蓝黄点表达设施或驻军。英雄、兵团等地图角标需要等正式可用图标资源接入后再做，不从临时 UI 包里截取素材。
- 不同场域素材不能在 `SiteVisualLayer` 上边相连；需要保持至少 1 个空 cell 或视觉层断开，否则会被识别成同一片素材。

## 当前已落地交互

- 玩家小队在大地图上以 `WorldArmyState` 保存位置、目标、速度、状态和意图。
- 出征从己方 `WorldSite` 发起：选中己方场域，点击出征，选择英雄和小兵，再在大地图右键选择目的地。
- V1 当前以 `骑士` 作为英雄、`民兵` 作为小兵。
- 左键点击可选择单支玩家小队；左键拖框可圈选多支玩家小队；按住 Shift 可追加选择。
- 右键点击地表会把已选小队设为 `MoveToPosition`，按 RTS 连续路径移动。
- 右键点击敌方场域会尝试下达 `AssaultSite`；当前只配置了埋骨地攻占战，墓园仍是敌方源头，不是 V1 可攻占目标。
- 右键点击己方场域会尝试下达 `ReinforceSite`；到达后小队转为驻军，并在该场域默认驻军区生成持久化部署位置。
- 己方场域驻军区满员时不能继续进驻；有已选小队悬停在不可执行的场域指令上时，鼠标显示禁止光标。
- 如果旧存档或运行中状态已经把玩家小队推进到未配置攻占战的敌方场域，世界层会恢复为空闲小队，不进入卡死的 `Attacking` 状态。
- 敌方 Raid 创建后会生成敌方 `WorldArmyState`，并和玩家部队共用 `StrategicNavigation` 行军、到达和拦截规则。

## 世界推进关系

`WorldClock` 和 `WorldTick` 不承担同一职责：

```text
WorldClock
  持续推进时间
  驱动部队 delta 移动
  检查到达、接触、拦截

WorldTick
  离散结算
  资源产出
  建筑进度
  威胁生成
  事件刷新
```

部队移动不应写成“每个 WorldTick 跳一格”。当部队到达目标、接触敌军或进入遭遇区时，系统产生 `GameEvent` 或 `EncounterRequest`，再由世界层决定是否暂停、弹出选择或进入战斗。

## 分阶段计划

### 阶段 0：概念锁定

目标：

- 文档和代码术语区分 `WorldSite`、`WorldArmy`、`ThreatPlan`。
- 明确大地图是 RTS 连续空间，战斗才是战棋格子。
- 敌军行军不使用手工 waypoint，统一通过 `WorldArmyState + StrategicNavigation` 表现。

验收：

- 文档中不把大地图移动描述成格子 A*。
- 新增大地图功能时先判断是否属于部队、场域、威胁、遭遇或结算。

### 阶段 1：地图制作合同

目标：

- 完成 `StrategicMapLayer` / `StrategicBridgeLayer` 的正式视觉地表配置，并完成 `StrategicNavigationTileLayer` 的战略行军导航配置。
- 摆放 `MapAnchors/Sites/<site_id>`。
- 配置 `ArmySpawnPoints`、`EncounterZones` 的节点位置。

验收：

- 大地图第一屏像世界地表，不像流程图。
- 场域只是地表上的节点。
- 非场域地表具备后续移动和遭遇扩展空间。
- 行军可走范围只由 `StrategicNavigationTileLayer` 和 Godot 2D navigation 决定。

### 阶段 2：新增 WorldArmy 运行态

目标：

- 新增 `WorldArmyState`。
- `StrategicWorldState` 保存玩家和敌方部队。
- 部队有位置、目标、速度、半径、成员、状态。

验收：

- 存档可以恢复部队位置、目标和状态。
- UI 可以在地图上绘制部队 marker，而不是只绘制 threat marker。

### 阶段 3：接入 StrategicNavigation

目标：

- 建立连续空间路径请求。
- 部队从当前位置移动到目标世界坐标。
- 到达目标时产生事件。

第一版可以只做单体部队，不做复杂群体避让。

验收：

- 部队沿连续路径移动。
- 部队移动速度和到达半径可调。
- 无路径时返回清晰失败原因，不静默失败。

### 阶段 4：敌方 Raid 升级为敌军部队

目标：

```text
ThreatRuleDefinition
-> EnemyThreatPlan
-> Enemy WorldArmyState
-> WorldArmyState 移动
-> 到达目标后 ThreatStage = Attacking
```

验收：

- 墓园 Raid 在地图上是一支可见敌军。
- 敌军能被选中查看来源、目标、倒计时或预计到达。
- 敌军到达埋骨地后暂停世界时钟并暴露处理入口。

### 阶段 5：玩家出征升级为玩家部队

目标：

```text
选择来源场域
-> 选择目标场域
-> 选择民兵 / 角色 / 资源
-> 创建 Player Expedition
-> 地图上移动
-> 到达后驻守、攻打或触发遭遇
```

第一版只需要支持玩家营地到埋骨地。

验收：

- 出征进驻不再瞬移。
- 玩家可以在大地图上看到自己的队伍移动。
- 到达后能转为驻军或触发攻占战。

### 阶段 6：部队接触与野外遭遇

目标：

- 玩家部队和敌方部队接近时触发拦截判断。
- 部队进入 `EncounterZones` 时可触发野外遭遇。
- 产生统一 `EncounterRequest` / `BattleStartRequest`。
- 支持后续把两方会战、追杀、救援、夺取、护送和撤离目标表达为 `Opportunity`。

第一版规则：

```text
distance(armyA, armyB) <= armyA.Radius + armyB.Radius + interceptThreshold
=> create encounter
```

验收：

- 玩家不只能等敌军打到场域，还能主动拦截。
- 野外遭遇战结果能影响双方部队和 Raid 是否继续。

### 阶段 7：深化战斗 request/result

目标：

`BattleStartRequest` 应逐步包含：

```text
SourceArmyId
TargetArmyId
SourceSiteId
TargetSiteId
PlayerForces
EnemyForces
MapDefinitionId
VictoryConditions
RetreatRules
FacilityModifiers
```

`BattleResult` 应逐步包含：

```text
SurvivingUnits
LostUnits
InjuredCharacters
ResourceDelta
SiteStateDelta
ArmyStateDelta
ThreatDelta
EventHooks
```

验收：

- 战斗结果不只返回胜负。
- 战斗能改变部队、场域、威胁和资源。

### 阶段 8：降低硬编码

目标：

- `WorldActionResolver` 不再按具体场域和 action id 写死显示与目标选择。
- 战斗入口由通用 `BattleKind + Source + Target + Army/Site` 构建。
- `WorldSiteRoot` 不固定加载 `BonefieldSite`，由 request 或 site definition 决定。

验收：

- 新增第二个可争夺场域时，不需要复制埋骨地专属分支。
- 新增敌方 Raid 规则时，不需要新增专属 UI 逻辑。

### 阶段 9：存档与调试

目标：

- 保存 `WorldArmyState`。
- 调试工具可以生成敌军、推进时间、传送部队、触发遭遇。
- 调试工具必须走正式服务接口，不直接改 UI 节点。

验收：

- 读档后部队位置和目标保留。
- 调试操作产生和正式操作同类的事件和日志。

## 非目标

第一轮不做：

- 完整多势力外交 AI。
- 大规模群体避让。
- 编队阵型。
- 补给线。
- 复杂地形代价调参。
- 离线真实时间后台行军。
- 把大地图改成战棋格子玩法。

## 当前导航策略

敌方 Raid 已经收敛到 `WorldArmyState + StrategicNavigation`。`EnemyThreatPlan` 只描述敌方计划、目标和战斗入口，地图上的位置、行军、到达、接触和拦截由敌方 `WorldArmyState` 决定。

如果旧运行态中仍存在没有 `WorldArmyId` 的威胁，表现层只允许用 `StrategicNavigation` 计算一次临时路径；不再配置或读取手工移动锚点。

大地图当前只使用 Godot Navigation。`StrategicNavigationTileLayer` 是正式导航数据入口；如果导航层缺失、为空、TileSet navigation polygon 未生效，或者目标点不在导航区域内，系统必须报错并停止对应部队。

## 完成定义

该专题第一轮完成时，应满足：

- 大地图部队使用 RTS 连续空间移动。
- 玩家和敌方至少各有一种可移动部队。
- 敌方 Raid 由可见敌军承载。
- 玩家可以出征并在地图上看到队伍移动。
- 双方接触能产生野外遭遇或拦截。
- 战斗结果能回写部队和威胁状态。
- 所有关键运行态可保存和读取。
