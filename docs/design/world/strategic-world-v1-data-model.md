# 战略大世界 V1 数据模型

本文定义 V1 的数据结构。目标是先支持人口、经济、石材、兵营、矿场、防御塔、场域状态和大地图部队移动，同时保证后续能自然扩展更多资源、建筑、场域设施、中立势力、野外机会和敌方计划。

## 分层原则

V1 使用“定义 + 运行态”的双层模型：

```text
Definition 描述可复用内容。
State 描述本局实际变化。
Presentation 只读取 State 和 ViewModel，不保存权威状态。
```

禁止把 Godot 节点、场景实例或 UI 控件当作存档对象。长期保存只能保存结构化 id 和数值。

## 顶层定义

```text
StrategicWorldDefinition
  Id
  DisplayName
  StartingSiteId
  PlayerFactionId
  EnemyFactionIds[]
  ResourceDefinitions[]
  FacilityDefinitions[]
  SiteDefinitions[]
  ActionDefinitions[]
  ThreatRules[]
  InitialResources[]
```

第一版内容：

```text
Id: chapter_01_strategic_v1
StartingSiteId: player_camp
Sites:
  player_camp
  bonefield
  graveyard
Resources:
  population
  economy
  stone
Facilities:
  barracks
  mine
  defense_tower
```

## 资源定义

```text
ResourceDefinition
  Id
  DisplayName
  Description
  Category
  IsReservable
  MinValue
  MaxValue?
  IconKey
```

V1 资源：

| Id | 显示名 | Category | IsReservable | 说明 |
|---|---|---|---|---|
| `population` | 人口 | Workforce | true | 可被建筑占用，也可被训练成驻军 |
| `economy` | 经济 | Currency | false | 建造、训练和行动消耗 |
| `stone` | 石材 | Material | false | 矿场产出，防御塔和修复设施消耗 |

### 资源运行态

```text
ResourceStore
  Amounts: Dictionary<ResourceId, int>
  Reservations: ResourceReservation[]

ResourceReservation
  ResourceId
  Amount
  SourceId
  SourceKind
```

可用资源计算：

```text
available(resourceId) = amount(resourceId) - reserved(resourceId)
```

V1 规则：

- 人口可以被占用，例如矿场占用 1 人口。
- 人口也可以被消耗，例如训练 1 队民兵时人口转化为驻军。
- 经济和石材第一版只做消耗和增加，不做占用。
- 所有消耗必须经过统一交易逻辑，不能由 UI 直接改字典。

### 资源交易

```text
ResourceTransaction
  Id
  Reason
  Costs[]
  Gains[]
  ReservationsAdded[]
  ReservationsRemoved[]
```

失败规则：

- 任意 cost 不足时，整笔交易失败。
- 任意 reservation 目标不足时，整笔交易失败。
- 失败交易不能产生部分效果。

## 场域定义

```text
WorldSiteDefinition
  Id
  DisplayName
  SiteKind
  Description
  MapPosition
  InitialOwnerFactionId
  InitialControlState
  FacilitySlots[]
  InitialFacilities[]
  InitialGarrison[]
  BattleAnchors[]
  EntranceDefinitions[]
  Tags[]
```

### SiteKind

V1 使用：

| Kind | 用途 |
|---|---|
| `Base` | 玩家营地 |
| `ResourceSite` | 埋骨地 |
| `EnemySource` | 墓园 |

后续可以扩展：

```text
Village
Fort
Bridge
Forest
Ruins
NeutralCamp
BossLair
```

### 场域运行态

```text
WorldSiteState
  SiteId
  OwnerFactionId
  ControlState
  SiteMode
  DamageLevel
  LocalResources: ResourceStore
  Facilities[]
  Garrison[]
  ActiveTags[]
  LastVisitedTick
  LastModeChangedTick
  PendingThreatIds[]
```

V1 资源位置规则：

- 玩家可花费资源主要存在 `StrategicWorldState.PlayerResources`。
- 场域 `LocalResources` 预留给仓库、可抢资源、战斗撤离物资。
- V1 矿场产出可以先直接进入玩家资源库，避免第一版增加运输系统。

### 场域模式

```text
Peacetime
Alert
Wartime
Aftermath
```

模式含义：

| 模式 | 含义 | 第一版行为 |
|---|---|---|
| `Peacetime` | 非战时经营 | 允许建造、训练、驻军、等待 |
| `Alert` | 警戒 | 敌方计划已生成或正在接近，仍允许战前准备 |
| `Wartime` | 战时 | 已生成 `BattleStartRequest` 并进入 `WorldSiteRoot` |
| `Aftermath` | 战后 | 战斗或自动结算已回写，保留一段结算状态后回到非战时或警戒 |

模式切换必须走 `WorldSiteModeTransitionService`，并输出 `SiteModeChanged` 事件；不要由 UI 或战斗节点直接改 `WorldSiteState.SiteMode`。

### 控制状态

```text
Unknown
Neutral
Hostile
Contested
PlayerHeld
Damaged
Lost
```

状态含义：

| 状态 | 可建造 | 可驻军 | 可被 Raid | 说明 |
|---|---|---|---|---|
| `Unknown` | 否 | 否 | 否 | 未发现 |
| `Neutral` | 否 | 否 | 否 | 已知但无人控制或暂不争夺 |
| `Hostile` | 否 | 否 | 否 | 敌方或失控 |
| `Contested` | 否 | 可选 | 是 | 争夺中 |
| `PlayerHeld` | 是 | 是 | 是 | 玩家控制 |
| `Damaged` | 受限 | 是 | 是 | 玩家控制但受损 |
| `Lost` | 否 | 否 | 否 | 被敌方夺回 |

## 大地图地表

V1 不使用场域连线图。场域只是大地图上的可进入节点，节点之间的地表由 TileMap 制作表达。

场域视觉由 `SiteVisualLayer` 维护；`MapAnchors/Sites/<site_id>` 是场域逻辑锚点，锚点必须落在对应场域素材内部。运行时可以从锚点所在 cell 扫描 `SiteVisualLayer` 的 4 向连通区域，得到只用于表现和交互命中的视觉 footprint。不同场域素材不能在该层边相连。场域业务数据仍然来自 `WorldSiteDefinition` / `WorldSiteState`，不要从 tile 反推控制权、驻军、建筑或可攻占规则。

敌方移动由敌方 `WorldArmyState` 承载，使用 `StrategicNavigation` 根据可通行地表计算路线。`EnemyThreatPlan` 只描述敌方计划、目标和战斗入口，不再保存或依赖隐藏移动锚点。

## 大地图部队运行态

玩家小队、敌方 Raid 和后续 NPC 势力部队都应使用同类运行态：

```text
WorldArmyState
  ArmyId
  OwnerFactionId
  SourceSiteId
  TargetSiteId
  RelatedThreatId
  WorldX
  WorldY
  DestinationX
  DestinationY
  MoveSpeed
  Radius
  Status
  Intent
  GarrisonUnits[]
  CargoResources
  CreatedTick
  WorldPosition: Vector2 runtime convenience
  Destination: Vector2 runtime convenience
  NavigationPathPoints runtime cache
```

V1 状态控制在最小集合：

```text
Idle
Garrisoned
Moving
Attacking
Retreating
Defeated
```

规则：

- `WorldArmyState` 保存权威位置、目标、阵营和成员。
- 路径点是运行时缓存，目标改变、导航版本改变、进入战斗或到达后可以重算。
- 部队接触、到达场域或进入野外机会区时，只产生 `EncounterRequest` 或 `GameEvent`，不直接加载战斗场景。
- 野外短期内容优先建模为 `Opportunity` / `EncounterRequest`；只有长期可运营地点才建成 `WorldSiteState`。

## 建筑定义

```text
FacilityDefinition
  Id
  DisplayName
  Description
  FacilityType
  BuildCosts[]
  BuildTimeTicks
  RequiredSlotTags[]
  MaxLevel
  PassiveEffects[]
  Actions[]
  BattleModifiers[]
```

V1 建筑：

| Id | 类型 | 关键效果 |
|---|---|---|
| `barracks` | Military | 解锁训练民兵，增加驻军能力 |
| `mine` | Production | 占用人口，每 tick 产石材 |
| `defense_tower` | Defense | 提供防守值，防守战提供塔支援 |

### 建筑运行态

```text
FacilityInstance
  InstanceId
  FacilityId
  SiteId
  SlotId
  Level
  State
  AssignedPopulation
  ProgressTicks
  Cooldowns[]
  ActiveTags[]
```

建筑状态：

```text
Planned
Building
Active
Damaged
Disabled
Destroyed
```

V1 可以先把建造做成立即完成，但 `Building` 和 `ProgressTicks` 要保留，避免后续重构。

## 建筑槽位

```text
FacilitySlotDefinition
  SlotId
  DisplayName
  Tags[]
  AllowedFacilityIds[]
  InitialFacilityId?
  BattleAnchorId?
```

例子：

```text
bonefield
  mine_slot_01
	Tags: mine, production
	AllowedFacilityIds: mine

  tower_slot_01
	Tags: tower, defense
	AllowedFacilityIds: defense_tower
	BattleAnchorId: bonefield_north_tower
```

槽位规则：

- 建筑必须建在合法槽位。
- 一个槽位同一时间只能有一个建筑。
- 战斗中的表现位置通过 `BattleAnchorId` 映射，不直接保存节点路径。

## 驻军

```text
GarrisonState
  UnitTypeId
  Count
  SourceFacilityId?
  Morale
  DamageLevel
```

V1 只需要一种驻军：

```text
militia
```

驻军用途：

- 提高自动防守结算值。
- 防守战中生成友方单位。
- 失败时可能损失。

第一版不做：

- 兵种升级。
- 装备。
- 复杂士气。
- 多兵种克制。

## 威胁定义和运行态

```text
ThreatRuleDefinition
  Id
  SourceSiteId
  TargetSiteId
  ThreatType
  TriggerConditions[]
  InitialCountdownTicks
  EnemyGroupId
  ConsequenceEffects[]
```

```text
EnemyThreatPlan
  Id
  RuleId
  SourceSiteId
  TargetSiteId
  WorldArmyId
  ThreatType
  Stage
  CountdownTicks
  EnemyGroupId
  VisibleIntelLevel
  CreatedTick
```

V1 威胁：

```text
graveyard_raid_bonefield
  SourceSiteId: graveyard
  TargetSiteId: bonefield
  ThreatType: Raid
  Trigger: bonefield ControlState is PlayerHeld or Damaged
  CountdownTicks: 3
  EnemyGroupId: undead_raid_01
```

威胁阶段：

```text
Hidden
Rumor
Marching
Attacking
Resolved
```

V1 可以直接从 `Marching` 开始显示，后续再做情报等级和隐藏威胁。

## 顶层运行态

```text
StrategicWorldState
  RunId
  DefinitionId
  Seed
  WorldTick
  PlayerFactionId
  PlayerResources: ResourceStore
  SiteStates: Dictionary<SiteId, WorldSiteState>
  ArmyStates: Dictionary<ArmyId, WorldArmyState>
  ThreatPlans: Dictionary<ThreatId, EnemyThreatPlan>
  CompletedEventIds[]
  Flags[]
```

保存规则：

- 保存 `DefinitionId`，不复制整份 definition。
- 保存 state 中的 id、数字、枚举、tag。
- 不保存 Godot 节点路径。
- 不保存 UI 选择状态，除非是明确的玩家决策。

## 推荐初始状态

```text
PlayerResources
  population: 8
  economy: 10
  stone: 4

player_camp
  ControlState: PlayerHeld
  Facilities:
	barracks active
  Garrison:
	militia x1

bonefield
  ControlState: Hostile
  FacilitySlots:
	mine_slot_01
	tower_slot_01

graveyard
  ControlState: Hostile
  Tags:
	undead_source
```

## 数据不变量

- 所有 `WorldSiteState.SiteId` 必须能在 `StrategicWorldDefinition.SiteDefinitions` 中找到。
- 所有 `FacilityInstance.FacilityId` 必须能找到定义。
- 所有建筑必须占用合法槽位。
- 所有 `WorldArmyState.SourceSiteId` / `TargetSiteId` 如果存在，必须能找到对应 `WorldSiteDefinition`。
- `EnemyThreatPlan.WorldArmyId` 如果存在，必须能找到对应 `WorldArmyState`。
- 所有资源变动必须经过交易或 effect。
- `WorldTick` 只能单调递增。
- `Resolved` 威胁不能再次推进。
- Battle 只能通过 result applier 改世界状态。
- UI 不能直接修改 `StrategicWorldState`。
