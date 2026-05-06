# 战略大世界 V1 战斗衔接合同

本文定义大世界如何进入战斗、战斗如何回写大世界。目标是让 Battle 保持独立，同时让场域运营真正影响战斗。

## 边界

World 负责：

- 判断哪里发生战斗。
- 选择参战阵营、入口、驻军、敌方计划和场域状态。
- 根据建筑和驻军生成战斗修正。
- 消费战斗结果并修改世界状态。

Battle 负责：

- 执行战棋规则。
- 处理回合、AP、移动、能力、敌方意图、目标结算。
- 返回结构化战斗结果。

Battle 不负责：

- 修改 `StrategicWorldState`。
- 决定敌方大地图策略。
- 保存场域持久状态。

World 不负责：

- 直接操作 Battle 节点。
- 修改 AP、TurnSystem 或 Battle flow。
- 在 UI 回调里手动改战斗单位。

## 进入战斗流程

```text
WorldActionRequest
-> WorldActionResolver
-> WorldBattleRequestBuilder
-> BattleStartRequest
-> BattleSessionHandoff
-> WorldSiteRoot
```

当前项目已有 `BattleSessionHandoff`，但它只有：

```text
contextId
encounterId
returnScenePath
```

V1 应逐步扩展为完整 request。短期可以兼容旧字段，但新实现不得继续只依赖胜负。

## BattleStartRequest

```text
BattleStartRequest
  RequestId
  ContextId
  BattleKind
  EncounterId
  SourceSiteId?
  TargetSiteId?
  ThreatId?
  AttackerFactionId
  DefenderFactionId
  MapDefinitionId
  ObjectiveIds[]
  AvailableEntrances[]
  PlayerForces[]
  EnemyForces[]
  BattleModifiers[]
  SiteStateSnapshot
  ReturnScenePath
```

### BattleKind

V1 使用：

| Kind | 用途 |
|---|---|
| `AssaultSite` | 玩家攻占埋骨地 |
| `DefenseRaid` | 玩家防守埋骨地 Raid |

预留：

```text
FieldIntercept
SearchAndExtract
Rescue
Sabotage
BossAssault
```

### AvailableEntrances

```text
BattleEntranceRequest
  EntranceId
  DisplayName
  FactionId
  Capacity
  BattleAnchorId
  Source
```

Source：

```text
Default
Facility
Garrison
MapSurface
HeroSkill
```

V1：

- 攻占埋骨地：玩家从默认入口进入。
- 防守埋骨地：如果有防御塔或驻军，可额外提供防守部署点。

### PlayerForces

```text
BattleForceRequest
  ForceId
  SourceKind
  SourceId
  UnitDefinitionId
  Count
  FactionId
  PreferredEntranceId?
```

V1：

- 英雄和主力单位可以先沿用现有场域内单位。
- 驻军需要转成 `militia` 单位或等价支援。
- 后续再把所有单位出生点彻底数据化。

### BattleModifiers

```text
BattleModifier
  Id
  Type
  SourceKind
  SourceId
  BattleAnchorId?
  Uses
  Values
  Tags[]
```

V1 modifier：

```text
tower_support
  SourceKind: Facility
  SourceId: defense_tower instance id
  BattleAnchorId: bonefield_north_tower
  Uses: 1
```

可能表现：

- 战斗内提供一次远程支援。
- 或在第一版先转换成敌方初始伤害 / 延迟增援。
- 关键是由 Battle 消费 modifier，不由 World 操作战斗节点。

### SiteStateSnapshot

```text
SiteStateSnapshot
  SiteId
  ControlState
  DamageLevel
  ActiveFacilityIds[]
  DamagedFacilityIds[]
  GarrisonSummary[]
  ActiveTags[]
```

用途：

- Battle 初始化地图覆盖层。
- 决定哪些对象存在或已损坏。
- 决定防守目标。

V1 不保存完整 Battle 节点状态。

## 战斗目标

V1 至少支持两类目标：

### 攻占埋骨地

```text
ObjectiveId: occupy_bonefield
Success:
  alive enemies defeated or command point captured
Failure:
  player defeated or withdraws without objective
```

第一版可以先沿用“击败敌人”胜利，但 result 必须表达这是 `occupy_bonefield` 成功。

### 防守埋骨地

```text
ObjectiveId: defend_bonefield
Success:
  defeat raid leader or survive required rounds
Failure:
  player defeated, mine core destroyed, or withdraws
```

第一版如果没有防守目标系统，可以先使用胜负映射，但合同要保留 objective result。

## BattleResult

```text
BattleResult
  RequestId
  ContextId
  BattleKind
  Outcome
  ObjectiveResults[]
  UnitResults[]
  ResourceChanges[]
  SiteStateChanges[]
  FacilityStateChanges[]
  ThreatStateChanges[]
  TagsAdded[]
  TagsRemoved[]
```

### Outcome

V1 建议扩展为：

```text
Victory
Withdraw
Defeat
Disaster
```

如果当前代码短期只有 `Victory / Defeat`，实现时先做兼容映射：

```text
Victory -> Victory
Defeat -> Defeat
```

但 result 数据结构不要再只存一个 enum。

### ObjectiveResult

```text
BattleObjectiveResult
  ObjectiveId
  State
  Score?
  Tags[]
```

State：

```text
Succeeded
Failed
Partial
Skipped
```

### SiteStateChange

```text
SiteStateChange
  SiteId
  OwnerFactionId?
  ControlState?
  DamageLevelDelta?
  TagsAdded[]
  TagsRemoved[]
```

### FacilityStateChange

```text
FacilityStateChange
  SiteId
  FacilityInstanceId?
  FacilityId?
  State?
  DamageDelta?
```

如果战斗中摧毁的是地图对象，但还没有具体 facility instance，可以先通过 `FacilityId + SiteId` 匹配。

## Result Applier

```text
WorldBattleResultApplier.Apply(state, request, result)
```

职责：

- 校验 result 是否匹配当前 active request。
- 根据 BattleKind 和 ObjectiveResults 应用世界变化。
- 推进 WorldTick。
- 清除或更新 ThreatPlan。
- 记录 GameEvent。

不负责：

- 播放 UI。
- 发奖励弹窗。
- 改 Battle 节点。

## V1 结算规则

### 攻占埋骨地

| 战斗结果 | 世界状态 |
|---|---|
| Victory + `occupy_bonefield` succeeded | bonefield owner=player, state=PlayerHeld |
| Victory 但目标 partial | bonefield state=Contested，给少量 stone 或情报 |
| Withdraw | bonefield 保持 Hostile，玩家经济 -1 |
| Defeat | bonefield 保持 Hostile，玩家人口 -1 或经济 -2 |
| Disaster | bonefield Hostile，墓园 Raid 倒计时缩短或敌方增强 tag |

V1 最小实现：

```text
Victory -> PlayerHeld
Defeat -> Hostile, player economy -1
```

但数据结构要能承载上表扩展。

### 埋骨地防守 Raid

| 战斗结果 | 世界状态 |
|---|---|
| Victory | threat Resolved，bonefield 保持 PlayerHeld |
| Withdraw | bonefield Damaged，mine facility Damaged |
| Defeat | bonefield Damaged 或 Lost，驻军损失 |
| Disaster | bonefield Lost，清除防御塔或矿场 |

防御塔处理：

```text
如果 tower_support uses 在战斗中用尽：
  V1 可以不改建筑状态，只记录本场已用。
如果敌方 objective 摧毁防御塔：
  SetFacilityState(defense_tower, Damaged)
```

## Request Builder 规则

### 攻占埋骨地 Request

输入：

```text
bonefield site state
player resources
default player party
bonefield encounter definition
```

输出：

```text
BattleKind: AssaultSite
TargetSiteId: bonefield
AttackerFactionId: player
DefenderFactionId: undead
ObjectiveIds:
  occupy_bonefield
AvailableEntrances:
  main_entrance
BattleModifiers:
  none in V1
```

### 防守埋骨地 Request

输入：

```text
bonefield site state
graveyard raid threat
facility instances
garrison
```

输出：

```text
BattleKind: DefenseRaid
TargetSiteId: bonefield
ThreatId: active raid id
AttackerFactionId: undead
DefenderFactionId: player
ObjectiveIds:
  defend_bonefield
PlayerForces:
  militia from garrison
BattleModifiers:
  tower_support if active defense_tower exists
```

## 兼容当前 Battle

当前 Battle 仍从 Godot 场域运行壳中手工摆放的 units 和 `SiteMapScene` 起步。`SiteMapScene` 是场域详细地图；战斗只是该场域的一种运行模式。V1 可以分两步：

### 第一步

- Handoff 增加 request/result 对象。
- WorldSiteRoot 仍加载现有 `WorldSiteRoot.tscn`。
- 战斗结束仍由胜负触发。
- ResultApplier 根据 `BattleKind + Outcome` 回写世界。

### 第二步

- EncounterDefinition 增加 map、objectives、spawns、modifiers。
- WorldSiteRoot 根据 request 初始化单位和 modifier。
- Objective 系统替代单纯全灭胜负。

## 日志要求

低噪声记录：

```text
BattleRequested kind=... target=... threat=...
BattleResultReceived outcome=... objectives=...
WorldBattleResultApplied site=... changes=...
```

不要记录每个单位每帧状态。

## 验收

- 从战略地图进入攻占埋骨地战斗。
- 胜利后埋骨地变为玩家控制。
- 墓园 Raid 到达后能进入防守战。
- 防御塔和驻军能进入 request。
- 防守胜利清除 Raid。
- 防守失败让埋骨地受损或丢失。
- Battle 不直接引用 `StrategicWorldState`。
