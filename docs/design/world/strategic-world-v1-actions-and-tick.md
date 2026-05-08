# 战略大世界 V1 行动与 WorldTick

本文定义玩家行动、资源结算、建筑产出、敌方威胁推进和 WorldTick。目标是让第一版运营足够清晰，同时保留后续扩展空间。

## WorldProgression 命名

V1 统一使用：

```text
WorldProgression
  WorldClock
  WorldTick
  SiteProduction
  ThreatProgression
  WorldEventQueue
```

- `WorldClock`：非暂停状态下持续走的世界时钟。
- `WorldTick`：世界时钟或玩家等待触发的离散结算点。
- `SiteProduction`：场域收益结算，例如矿场产出石材。
- `ThreatProgression`：Raid 行军、敌方计划和危机倒计时。
- `WorldEventQueue`：后续按时间触发剧情、经营事件和危机。

第一版不做离线后台真实时间推进。威胁到达 `Attacking` 后必须暂停 `WorldClock`，等待玩家处理，不允许静默结算导致玩家丢失场域。

## 行动总原则

所有战略交互都走统一行动管线：

```text
WorldActionRequest
-> Validate
-> Preview
-> Apply
-> Emit GameEvent[]
-> optionally AdvanceWorldTick
```

UI 只能发 request，不能直接改资源、建筑、驻军或威胁。

## 行动请求

```text
WorldActionRequest
  ActionId
  ActorFactionId
  SourceSiteId?
  TargetSiteId?
  TargetFacilityInstanceId?
  TargetSlotId?
  Payload
```

V1 不需要复杂 payload。常见参数：

- 目标场域。
- 目标建筑槽位。
- 训练数量。
- 是否进入战斗。

## 行动定义

```text
WorldActionDefinition
  Id
  DisplayName
  Description
  Scope
  AdvancesWorldTick
  Conditions[]
  Costs[]
  Effects[]
  FailureReasonKey
```

Scope：

```text
Run
Site
Facility
Threat
BattleEntry
```

V1 可先把行动定义写成 Godot Resource 或 C# definition；关键是解释器通用。

## 条件

第一版需要的条件：

| 条件 | 参数 | 用途 |
|---|---|---|
| `SiteControlStateIs` | siteId, state | 埋骨地被玩家控制后才能建造 |
| `SiteOwnerIs` | siteId, factionId | 只允许玩家操作自有据点 |
| `HasResourceAtLeast` | resourceId, amount | 检查经济、石材、人口 |
| `HasAvailablePopulation` | amount | 检查未占用人口 |
| `HasFacility` | facilityId, state | 有兵营才能训练 |
| `HasEmptyFacilitySlot` | slotTag or facilityId | 有合法槽位才能建造 |
| `HasGarrisonAtLeast` | unitTypeId, count | 后续高级行动 |
| `ThreatStageIs` | threatId, stage | Raid 到达后才能防守 |
| `NoActiveThreatOfRule` | ruleId | 防止重复生成 Raid |

条件失败必须返回稳定 reason，不要只返回 false。

## 效果

第一版需要的效果：

| 效果 | 参数 | 用途 |
|---|---|---|
| `AddResource` | resourceId, amount | 产出经济或石材 |
| `SpendResource` | resourceId, amount | 行动消耗 |
| `ReserveResource` | resourceId, amount, sourceId | 矿场占用人口 |
| `ReleaseResourceReservation` | sourceId | 建筑停用释放人口 |
| `SetSiteControlState` | siteId, state | 攻占或丢失场域 |
| `SetSiteOwner` | siteId, factionId | 改变归属 |
| `AddFacility` | siteId, slotId, facilityId | 建造建筑 |
| `SetFacilityState` | instanceId, state | 损坏、修复、启用 |
| `AddGarrison` | siteId, unitTypeId, count | 训练守军 |
| `RemoveGarrison` | siteId, unitTypeId, count | 战斗或自动结算损失 |
| `CreateThreat` | ruleId | 生成敌方计划 |
| `SetThreatStage` | threatId, stage | 推进威胁 |
| `StartBattle` | battle context | 进入战斗 |
| `AddSiteTag` | siteId, tag | 记录持久变化 |
| `RemoveSiteTag` | siteId, tag | 清除持久变化 |

## V1 行动清单

### 出征编成

```text
Entry: player-owned WorldSite action panel
AdvancesWorldTick: false
Conditions:
  source site Owner is player
  source site ControlState is PlayerHeld or Damaged
  source site has selectable heroes or garrison units
Flow:
  select source WorldSite
  click 出征
  choose heroes and corps
  right-click target on strategic map
```

说明：

- 出征不是目标场域的行动按钮，不能从敌方目的地面板发起。
- 第一版只有民兵可选，英雄选择位保留但暂无英雄数据。
- 右键敌方埋骨地会创建 `AssaultSite` 小队，抵达后进入攻占战。
- 右键己方场域会创建 `ReinforceSite` 小队，抵达后加入目标场域驻军。
- 右键空地会创建 `MoveToPosition` 小队，到达后停留为空闲小队。
- 创建出征小队本身不推进 WorldTick；部队移动由 WorldClock 连续推进。
- 战斗结束后的 result applier 决定是否推进 tick。V1 建议战斗结束推进 1 tick。

### 启用矿场

```text
ActionId: build_mine
Scope: Site
AdvancesWorldTick: true
Conditions:
  bonefield Owner is player
  bonefield ControlState is PlayerHeld or Damaged
  empty slot supports mine
  available population >= 1
  economy >= 2
Costs:
  economy 2
Effects:
  AddFacility(mine)
  ReserveResource(population, 1, facilityInstanceId)
```

第一版叫“启用矿场”或“修复矿场”都可以，但底层仍是 AddFacility。

### 建造防御塔

```text
ActionId: build_defense_tower
Scope: Site
AdvancesWorldTick: true
Conditions:
  target site Owner is player
  empty slot supports defense_tower
  stone >= 4
  economy >= 2
Costs:
  stone 4
  economy 2
Effects:
  AddFacility(defense_tower)
```

### 训练民兵

```text
ActionId: train_militia
Scope: Site
AdvancesWorldTick: true
Conditions:
  target site Owner is player
  target site has active barracks
  available population >= 1
  economy >= 2
Costs:
  population 1
  economy 2
Effects:
  AddGarrison(militia, 1)
```

V1 中训练民兵会消耗人口。后续如果要做“人口转兵役，可解散返还”，再加 garrison lifecycle。

### 进驻己方场域

进驻由出征系统发起，不再是目标场域行动按钮：

```text
Entry: 出征 -> 选择英雄 / 小兵 -> 右键己方场域
AdvancesWorldTick: false
Command:
  WorldArmyIntent = ReinforceSite
Validation:
  target site Owner is player
  target DefaultGarrisonZone has capacity
Runtime:
  RemoveGarrison(source, selected units)
  Create WorldArmyState
  on arrival AddGarrison(target, selected units)
```

如果目标驻军区满员，大地图上悬停该场域显示禁止光标，右键指令拒绝执行。

### 防守 Raid

```text
ActionId: defend_raid
Scope: Threat
AdvancesWorldTick: false
Conditions:
  threat TargetSiteId is player controlled
  threat Stage is Attacking
Effects:
  StartBattle(kind=DefenseRaid, threatId)
```

战斗结束后由 result applier 清除或结算威胁。

### 自动结算 Raid

自动结算不是玩家行动，是 WorldTick 中的系统流程：

```text
if threat Stage == Attacking and player does not defend:
  ResolveRaidAutomatically(threat)
```

V1 可以在 UI 上给玩家一个“处理进攻 / 放弃处理”的明确选择，不要静默结算。

### 等待 / 整顿

```text
ActionId: wait_tick
Scope: Run
AdvancesWorldTick: true
Conditions: none
Effects: none
```

用途：

- 让玩家主动推进世界。
- 测试生产和威胁倒计时。

## WorldTick 流程

一次 WorldTick 按固定顺序执行：

```text
1. TickBegin
2. FacilityProgress
3. Production
4. ThreatGeneration
5. ThreatProgression
6. ThreatResolutionGate
7. TickEnd
```

### 1. TickBegin

记录日志：

```text
WorldTickStarted tick=N
```

不要逐建筑输出高频日志，只记录关键变化。

### 2. FacilityProgress

V1 如果建造立即完成，此阶段可以没有实际效果。

保留原因：

- 后续建造时间。
- 修复时间。
- 生产队列。

### 3. Production

遍历玩家控制场域中的 active facility。

V1 生产规则：

```text
active mine with AssignedPopulation >= 1
-> AddResource(player, stone, 2)
```

矿场受损：

```text
mine State == Damaged -> no production
mine State == Disabled -> no production
```

### 4. ThreatGeneration

规则：

```text
if bonefield is PlayerHeld or Damaged
and no active threat of rule graveyard_raid_bonefield
and graveyard is Hostile
then create threat countdown 3
```

为了避免玩家刚占领就立刻被打，可以在战斗胜利后下一次 tick 才生成威胁。

### 5. ThreatProgression

```text
Hidden/Rumor/Marching threat:
  CountdownTicks -= 1
  if CountdownTicks <= 0:
	Stage = Attacking
```

V1 可以直接使用：

```text
Marching -> Attacking
```

后续再引入情报系统时扩展 Hidden/Rumor。

### 6. ThreatResolutionGate

当 threat 进入 `Attacking`：

- UI 必须提示玩家。
- 玩家可以选择防守战。
- 玩家可以选择自动结算或放弃。
- 不要在同一个 tick 内无提示直接结算。

### 7. TickEnd

记录 tick 结束摘要：

```text
WorldTickEnded tick=N resources=... activeThreats=...
```

## Raid 自动结算

V1 使用确定性公式，不上复杂 AI。

```text
defenseScore =
  militiaCount * 2
  + activeDefenseTowerCount * 3
  + siteControlBonus
  - damagePenalty

attackScore = 5
```

建议：

```text
siteControlBonus:
  PlayerHeld +1
  Damaged 0

damagePenalty:
  DamageLevel 0 -> 0
  DamageLevel 1 -> 1
  DamageLevel 2 -> 2
```

结算：

| 结果 | 条件 | 效果 |
|---|---|---|
| 完全防住 | defenseScore >= attackScore + 2 | 清除威胁，无损失 |
| 勉强防住 | defenseScore >= attackScore | 清除威胁，损失 1 民兵或建筑受损 |
| 受损 | defenseScore < attackScore | 埋骨地变 Damaged，矿场 Damaged |
| 丢失 | defenseScore <= attackScore - 3 | 埋骨地变 Lost，清除玩家驻军 |

这套公式只服务 V1 验证，后续可以替换为更丰富的 `ThreatResolver`。

## 行动是否推进 WorldTick

| 行动 | 推进 tick |
|---|---|
| 建造矿场 | 是 |
| 建造防御塔 | 是 |
| 训练民兵 | 是 |
| 出征创建小队 | 否 |
| 小队抵达并进驻 | 否 |
| 等待 / 整顿 | 是 |
| 进入战斗 | 否 |
| 战斗结束回写 | 是 |
| 查看场域 / 选择 UI | 否 |

原则：

- 信息查看不推进。
- 有资源收益、建筑变化的经营行动推进。
- 大地图部队出征、移动、改目标和抵达不直接推进 WorldTick；它们由 WorldClock 连续推进。
- 战斗开始不推进，战斗结束推进。

## 事件输出

每次成功行动输出结构化事件：

```text
GameEvent
  Id
  Kind
  SourceSystem
  TargetIds[]
  Payload
  Tick
```

V1 事件例子：

```text
FacilityBuilt
ResourceChanged
GarrisonChanged
SiteControlChanged
ThreatCreated
ThreatStageChanged
BattleRequested
BattleResolved
WorldTickAdvanced
WorldOpportunitySpawned
WorldOpportunityCompleted
WorldOpportunityExpired
```

情感系统暂不消费这些事件，但事件结构要为后续保留。

## 失败反馈

行动失败必须能解释：

```text
资源不足
人口不足
没有合法建筑槽位
场域不属于玩家
建筑不存在或未启用
敌方威胁未到达
```

UI 文案后续应中文化，但逻辑层 reason 使用稳定 id：

```text
not_enough_resource
site_not_owned
missing_facility
no_valid_facility_slot
threat_not_attackable
```
