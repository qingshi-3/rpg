# 战略场域格子探索技术合同

本文定义 `WorldSite` 内“格子式探索”的长期技术契约。玩家体验方向见 `../../20-game-design/strategic-map/strategic-world-site-grid-exploration.md`。

## 目标

场域探索用于解决“潜入、侦察、警报、伏击、强攻为什么发生”的代入感问题：玩家必须能在场域地图上看到小队位置、路线、风险点和触发点，而不是只在菜单里点击抽象选项。

第一版目标不是做完整 CRPG 或 RTS，而是在现有人工 `TileMapLayer` 场域和格子寻路能力上，建立一个可持久、可回写、可触发战斗的探索层。

## 子系统归属

场域格子探索属于：

```text
WorldSite Runtime State
+ WorldSite Presentation
+ World Action / Battle Handoff
```

它可以复用：

- `BattleMapView`：读取人工搭建的场域地图。
- `BattleGridMap`：使用已有格子、地表、高度、可走性和移动成本。
- `GridMapReader`：从 `TileMapLayer` 和 Tile custom data 读取 `Walkable / MoveCost / CanStandOn / IsObstacle / TerrainTag`。
- `MovementRangeFinder`：基于格子图寻找可达路径。
- `BattleStartRequest / BattleResult`：和战斗系统交接。

它不得拥有或修改：

- `TurnSystem`。
- 战斗 AP。
- 战斗单位行动规则。
- 战斗网格寻路规则本身。
- 大地图 RTS 部队移动权威。

## 运行模式

`WorldSiteRoot` 至少需要区分三种运行态：

```text
Exploration
Management
Battle
```

### Exploration

用于陌生、敌对、未完全控制的场域。

职责：

- 显示一个小队代表 marker。
- 处理点击格子移动意图。
- 按 `SiteExplorationTick` 推进玩家小队、巡逻单位和警戒检查。
- 处理探索点行动。
- 维护警戒度和探索记忆。
- 在行动失败、强攻或警戒触顶时请求战斗。

禁止：

- 启动战斗 HUD。
- 启动 `BattleTurnController`。
- 消耗战斗 AP。
- 让战斗单位实体承担探索移动权威。

### Management

用于已掌控或战后稳定的场域。

职责：

- 设施槽位、建造、修复、升级。
- 驻军查看、部署和经营任务。
- 场域内 `WorldTick` 推进。
- 产出、施工、威胁提示。

### Battle

用于战棋战斗。

职责：

- 战斗单位实例化。
- TurnSystem / AP。
- 技能、移动、敌人意图、战斗 HUD。
- 结束后通过 `BattleResult` 回写世界状态。

战斗回合不得推进探索时间、施工、产出、警戒倒计时或 `WorldTick`，除非 `BattleResult` 的世界回写明确要求一次世界结算。

## 持久状态

探索记忆是运行存档的一部分，放在 `WorldSiteState` 下，而不是场景节点缓存。

```text
WorldSiteState
  Exploration: WorldSiteExplorationState
  UnitPlacements[]: includes the visiting army row used as the exploration party
```

第一版状态：

```text
WorldSiteExplorationState
  CurrentCellX
  CurrentCellY
  CurrentCellHeight
  PartyActionPoints
  IsSimulationPaused
  AlertLevel
  RevealedCellKeys
  VisitedCellKeys
  RevealedPointIds
  ResolvedPointIds
  PatrolUnits[]
```

语义：

- `CurrentCell*`：小队代表当前位置，用场域格子坐标表示。
- 探索小队的单位身份来自 `WorldSiteState.UnitPlacements` 中进入场域的 `PlayerArmy / VisitingArmy` placement；`CurrentCell*` 必须与该 placement 同步，不能由场景字段或临时 marker 单独持有。
- `PartyActionPoints`：探索行动力蓄积值，只用于场域探索 tick，不等于战斗 AP。
- `IsSimulationPaused`：探索推进是否暂停；进入战斗、风险提示、到达目标或打开确认面板时为 true。
- `AlertLevel`：当前场域警戒度，第一版建议范围 `0..5`。
- `RevealedCellKeys`：已揭示格子，用于轻量场域迷雾或路径可读性。
- `VisitedCellKeys`：小队实际到达过的格子。
- `RevealedPointIds`：已显露的探索点。
- `ResolvedPointIds`：已结算且不应重复触发的一次性探索点。
- `PatrolUnits`：需要持久化的巡逻 actor 状态。mock 巡逻数据也必须落在这个长期形状上，不要只挂在场景节点。

巡逻状态：

```text
SiteExplorationPatrolState
  PatrolId
  CellX
  CellY
  CellHeight
  RouteIndex
  ActionPoints
  IsRemoved
```

格子 key 稳定格式：

```text
x:y:height
```

## 作者数据

探索点属于 `WorldSiteDefinition`，因为它描述该场域长期可交互结构，而不是临时 UI。

```text
WorldSiteDefinition
  ExplorationPoints[]
  ExplorationPatrols[]
```

探索点定义：

```text
SiteExplorationPointDefinition
  Id
  DisplayName
  Description
  CellX
  CellY
  CellHeight
  InteractionRange
  InitiallyRevealed
  Actions[]
```

探索行动定义：

```text
SiteExplorationActionDefinition
  Id
  DisplayName
  Description
  AlertDelta
  ConsumesWorldTick
  ResolvesPoint
  StartsBattle
  BattleEncounterId
  RevealsPointIds[]
```

巡逻定义：

```text
SiteExplorationPatrolDefinition
  Id
  DisplayName
  RouteCells[]
  AlertRadiusCells
  ActionPointRegenPerTick
  MoveCostPerCell
  InitiallyActive
```

第一版行动结果保持确定性。以后若加入潜入概率、人物能力检定或道具修正，应包在行动解析层，不改变定义和状态归属。

## 场景制作合同

人工场域继续使用现有 `BattleMapView` 场景结构：

```text
WorldSiteRoot
  MapRoot
    <SiteMap: BattleMapView>
      WaterFoundationLayer
      LowFoundationLayer
      HighFoundationLayer
      ObjectLayer...
      FacilitySlots
      SiteExplorationPoints   # 后续可加 authored point entity root
```

第一版不需要 `CollisionShape2D`。可走性来自格子数据：

```text
TileMapLayer + Tile custom data
-> GridMapReader
-> BattleGridMap.TopSurfacePositions
-> MovementRangeFinder
```

如果后续增加连续移动、实时卫兵视锥或物理遮挡，再单独引入碰撞层；不要让碰撞层反过来成为第一版探索权威。

## 离散实时推进

探索移动用一个小队代表 marker，不直接移动每个单位。场域探索采用可暂停的离散实时制，由 `SiteExplorationTick` 驱动。

每个 tick 的固定顺序：

```text
1. 读取玩家当前 MoveIntent / InteractIntent。
2. 为玩家小队增加探索行动力。
3. 为活动巡逻单位增加探索行动力。
4. 玩家小队若行动力足够，则沿已计算路径移动一格。
5. 巡逻单位若行动力足够，则沿固定路线移动一格。
6. 标记 visited / revealed。
7. 检查巡逻警戒半径。
8. 结算到达目标、兴趣点交互、战斗请求或暂停原因。
```

玩家移动意图：

```text
click target cell
-> TryGetMouseGridPosition
-> BattleGridMap.TryGetTopSurfacePosition
-> MovementRangeFinder.FindReachableCells(start, current exploration reach policy)
-> MovementRangeResult.TryBuildPathTo(target)
-> store MoveIntent path
-> SiteExplorationTick advances one cell when action points cover MoveCost
```

走路、巡逻和警戒检查不消耗 `WorldTick`。只有行动定义明确 `ConsumesWorldTick` 时，才推进世界时间。

探索移动不使用：

- 战斗 AP。
- 回合结束。
- 多单位选择。
- 碰撞体。
- 实时队形。

第一版可以用 mock 定义初始化一个玩家小队和一到两个巡逻单位，但运行时仍必须通过 `WorldSiteExplorationState` 保存位置、行动力和移除状态。

## 警戒半径

第一版敌方发现使用固定半径，不做视锥、遮挡、听觉或随机检定。

```text
distance(playerCell, patrolCell) <= AlertRadiusCells
-> pause exploration simulation
-> emit SiteExplorationAlertChanged or SiteExplorationBattleRequested
-> build BattleStartRequest when player confirms or rule requires combat
```

距离计算第一版应采用格子距离的稳定规则，例如 Manhattan distance。后续如果要改为寻路距离或视线距离，必须作为警戒规则扩展，而不是改写调用方语义。

## 兴趣点交互

探索点有两个层级：

```text
Point visibility: hidden / revealed / resolved
Action result: inspect / infiltrate / clear / loot / assault / withdraw
```

推荐第一版动作：

| 动作 | 用途 | 时间 | 可能结果 |
|---|---|---|---|
| 观察 | 揭示相邻点或风险 | 不耗 WorldTick | 增加情报 |
| 调查 | 获得资源、线索或解锁 | 可选 | 结算点位 |
| 潜入 | 尝试低成本进入关键点 | 不耗或耗时 | 提高警戒或触发战斗 |
| 清理 | 打通障碍或解锁槽位 | 耗 WorldTick | 解锁经营资产 |
| 强攻 | 主动进入战斗 | 不耗 WorldTick | 创建战斗请求 |
| 撤离 | 回到大地图或入口 | 不耗 WorldTick | 保留探索记忆 |

行动解析应该输出结构化结果，而不是 UI 直接改状态。

## 警戒度

`AlertLevel` 是场域探索的核心张力条。

建议语义：

```text
0: 未察觉
1-2: 轻微警觉，部分潜入风险上升
3-4: 高警戒，关键点更容易触发战斗
5: 警报，触发遭遇或强制撤离选择
```

第一版可以只支持确定性 `AlertDelta`。后续引入概率时，必须显示最终风险来源，例如：

```text
基础风险 40%
斥候 -15%
警戒度 +10%
夜色 -10%
最终风险 25%
```

## 战斗交接

探索只能请求战斗，不能运行战斗。

```text
Exploration action
-> Build BattleStartRequest
-> BattleSessionHandoff
-> WorldSiteRoot activates Battle runtime
-> BattleResult
-> WorldBattleResultApplier / WorldSite writeback
-> return to Exploration or Management
```

`BattleStartRequest` 至少应携带：

```text
TargetSiteId
EncounterId or ExplorationPointId
EntryCell x:y:height
AlertLevel
TriggerPatrolId, when detection caused combat
PlayerForces from the infiltrating WorldArmy
EnemyForces bound to current alive exploration patrol WorldSiteUnitPlacements
AttackDirection or entrance context
Advantage tags, when available
```

第一版可以复用现有 `BattleKind.AssaultSite` 或专门增加 `SiteExplorationEncounter`。如果增加新 `BattleKind`，必须同步 `WorldBattleResultApplier` 的回写分支。

探索遭遇战不能重新生成抽象敌人，也不能只凭触发者临时拼一个敌人。敌方参战集合来自当前场域内仍存活的探索巡逻 `SourcePlacementId`；每个 `SourcePlacementId` 都是敌方单位身份边界。战斗部署只绑定这些 resident placements，胜利回写根据 `BattleResult.ForceResults` 精确移除被击败的 placement / garrison count，未参战或未被击败的场域单位保留。玩家方身份来自进入场域的单单位 `WorldArmyState`；如果无法解析该军队或触发巡逻，不能启动战斗。

## 战斗回写

探索触发的战斗结束后，回写不应只给胜负文本。至少要能更新：

- 相关 `ResolvedPointIds`。
- 场域控制权。
- 解锁的设施槽位或资源点。
- 驻军 / 小队伤亡。
- 建筑或场景损坏。
- 警戒度清除、降低或保留。

推荐规则：

```text
局部遭遇胜利 -> 当前探索点 resolved，继续 Exploration
核心据点胜利 -> 场域变为 PlayerHeld，切 Management
失败撤退 -> 保留 visited/revealed，警戒度保持或升高
```

## 经营转换

探索结果应沉淀为经营资产，而不是一次性奖励后消失。

示例：

```text
塌方矿道 resolved -> 解锁 mine_slot_01
哨岗 resolved -> 解锁 tower_slot_01 或防守优势
仓库 resolved -> 获得资源，并在经营态显示为储备点
排水口 revealed -> 后续防守战可作为敌方潜入风险或我方撤离路线
```

同一张场域地图应从“探索图”自然变为“经营图”，让玩家相信自己经营的是刚刚探索过的地方。

## UI / Presentation 合同

探索态 UI 最小结构：

```text
顶部：场域名 / 探索态 / 世界第 N 日 / 警戒度
地图：小队 marker / 可走格 / 探索点 / 未揭示遮罩
右侧：当前格或探索点详情 / 可执行行动
底部：低噪事件反馈
```

要求：

- 小队 marker 是探索投影，不是战斗单位实体。
- 可达格和路径预览可以复用格子高亮层，但高亮语义必须和战斗移动区分。
- 右侧行动按钮只发探索 action request，不直接修改 `WorldSiteState`。
- 进入 Battle 后隐藏探索 UI。
- 返回 Exploration/Management 后根据 `WorldSiteState` 重建展示。

## 第一版切片建议

推荐首个场域切片：`Bonefield` 或独立 `AbandonedMineSite`。

最小内容：

```text
入口
破损矿车：调查获得少量资源
哨岗：观察降低后续伏击，强攻触发战斗
塌方矿道：清理 1 WorldTick，解锁矿场槽位
内侧营地：核心战斗点，胜利后场域转 Management
巡逻单位：沿固定路线移动，警戒半径 2 格
```

第一版明确不做：

- 多单位直接控制。
- 敌人追逐、搜索和复杂 AI。
- 物理碰撞移动。
- 完整潜行视锥。
- 大量可拾取物。
- 复杂随机检定。

## 日志与诊断

低噪日志事件建议：

```text
SiteExplorationEntered
SiteExplorationPartyMoved
SiteExplorationPointRevealed
SiteExplorationActionResolved
SiteExplorationAlertChanged
SiteExplorationBattleRequested
SiteExplorationConvertedToManagement
```

失败必须显式记录原因，例如：

```text
exploration_start_blocked
exploration_destination_missing
exploration_destination_unreachable
exploration_point_unrevealed
exploration_action_missing
```

不要静默 fallback 到直线移动、菜单结算或强制战斗。

## 验收标准

- 场域探索能在无碰撞体的人工 TileMap 上移动。
- 移动权威来自 `BattleGridMap` 可走 top surface。
- 探索态不显示战斗 HUD、AP、回合顺序。
- 玩家和巡逻单位由 `SiteExplorationTick` 推进。
- 探索行动力不复用战斗 AP。
- 玩家进入巡逻单位固定警戒半径后暂停探索推进并给出明确原因。
- 走路不推进 `WorldTick`。
- 明确耗时行动才推进 `WorldTick`。
- 潜入或强攻触发的战斗携带探索上下文。
- 战斗回合不推进施工、产出、探索计时或警戒倒计时。
- 战斗结果能回写探索点和场域经营状态。
