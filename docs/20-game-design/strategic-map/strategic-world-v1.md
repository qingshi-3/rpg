# 战略大世界 V1 落地规划

本文是第一版世界骨架的基准规格。后续实现优先依照本文，而不是旧的战役地图、可行走大地图或自由探索场域原型。

V1 的定位是支撑战略地图支柱的底层闭环：大地图有状态和移动，场域可进入并保留记忆，战斗通过 request/result 回写世界。它不是最终完整的战略地图、人物社交和战棋整合形态。

## 阅读路线

本文负责 V1 总览和边界。实现时按下面顺序读：

1. `../../30-technical-design/world/strategic-world-v1-data-model.md`：定义、运行态、资源、建筑、场域、威胁的数据结构。
2. `strategic-world-v1-actions-and-tick.md`：世界行动、资源结算、建筑生产、WorldTick、敌方 Raid。
3. `../../30-technical-design/world/strategic-world-v1-battle-contract.md`：World 到 Battle 的 request/result 合同和回写规则。
4. `strategic-world-v1-ui-flow.md`：战略地图 UI、场域面板、行动预览和反馈。
5. `../../30-technical-design/world/strategic-world-v1-implementation.md`：分阶段实现计划、文件归属和验收门槛。
6. `../../60-qa/testcases/strategic-world-v1.md`：测试和手动验收用例。

## 核心目标

V1 先验证一件事：

```text
玩家能在大地图上看到可推进的世界状态。
玩家能进入 WorldSite 小地图并进行非战时经营或战时战斗。
大世界准备、场域设施和驻军会改变战斗入口与结算。
战斗结果能回写 WorldSite、WorldArmy、Threat 和资源状态。
```

这不是完整城建模拟，也不是完整开放世界探索。第一版应做成“世界生态 + 可进入场域 + 战斗回写”的最小闭环。

## 玩家体验闭环

```text
查看战略地图
-> 选择玩家控制的场域、小队或移动目标
-> 建造 / 驻军 / 出征 / 进入场域 / 拦截
-> 进入场域战、场域经营态或野外遭遇
-> 战斗结算
-> 场域资源、建筑、控制权、部队和敌方威胁变化
-> WorldTick 推进
-> 敌方计划推进或进攻玩家据点
```

玩家每轮要回答的问题：

- 先占领哪个场域。
- 人口分配到生产、驻守还是出征。
- 经济用于训练、建造还是维持行动。
- 石材用于矿场、防御塔还是修复场域设施。
- 是否在敌方进攻前加固据点。
- 是否主动拦截敌方部队，在场域内防守，还是撤回整备。

长期产品中的玩家还需要回答：

- 是否进入某个势力冲突、野外机会或战后残场。
- 以战斗、交涉、夺取、救援、护送还是撤离作为目标。
- 这次介入会让哪个 `WorldSite`、`Faction`、`WorldArmy` 或角色记住结果。

## V1 内容范围

第一版只做 3 个可运营场域：

```text
玩家营地、埋骨地、墓园
```

### 玩家营地

玩家初始据点。承担基础资源、建造、训练和出征入口。

初始能力：

- 持有人口、经济、石材库存。
- 可建造或管理兵营。
- 可派出部队进入埋骨地。
- 可承接战斗失败后的撤退结果。

### 埋骨地

第一座可争夺资源场域。承担资源产出和持久场域记忆验证。

初始状态：

- 敌方或中立失控。
- 有可占领矿场。
- 可在占领后产出石材。
- 可建造防御塔。
- 会成为墓园 Raid 的目标。

场域记忆第一版至少保存：

- 控制权。
- 矿场是否激活。
- 防御塔是否存在。
- 驻军数量。
- 损毁状态。

### 墓园

第一版敌方源头。承担敌方威胁计划生成。

初始能力：

- 定期向埋骨地生成 Raid。
- Raid 到达后触发防守战或自动结算。
- 后续可扩展为 Boss 巢穴和亡灵祭坛。

### 大地图地表

场域之间不是连线图。玩家营地、埋骨地和墓园只是大地图上的节点，其余区域由 TileMapLayer 制作成可行走地表。

大地图部队移动和后续 RTS 式连续空间寻路的规划见 `../../30-technical-design/world/strategic-world-rts-navigation-and-armies.md`。该规划明确大地图不使用战斗格子 A*，玩家部队和敌方 Raid 都应通过 `WorldArmyState + StrategicNavigation` 在地表上连续移动。

最低能力：

- 表达道路、荒地、河流、山口等地貌，而不是流程线。
- 敌方 Raid marker 能在地表上移动靠近目标场域。
- 支持后续扩展为玩家部队移动、野外遭遇战或拦截战。

## 资源

第一版只定义 3 个资源：

| 资源 | 稳定 Id | 第一版用途 |
|---|---|---|
| 人口 | `population` | 建筑工作、训练守军、驻守据点 |
| 经济 | `economy` | 建造、训练、行动消耗 |
| 石材 | `stone` | 建造矿场、防御塔、修复场域设施 |

资源必须用通用资源容器保存：

```text
ResourceStore
  amounts: Dictionary<ResourceId, int>
```

禁止在长期状态里写死：

```text
Population
Economy
Stone
```

原因是后续会自然扩展木材、粮食、魔晶、情报、信仰、污染等资源或指标。

### 人口规则

V1 先只做“可用人口”。

不做：

- 总人口 / 劳动力 / 兵役人口细分。
- 民心、治安、出生死亡。
- 复杂人口增长。

需要支持：

- 建筑行动消耗人口或占用人口。
- 训练守军消耗人口。
- 场域被袭击时可能损失人口。

### 经济规则

经济是第一版的通用钱粮，不拆钱、粮、税。

需要支持：

- 建筑成本。
- 训练守军成本。
- 战略行动成本。
- 战斗失败或据点受损时损失。

### 石材规则

石材用于让“矿洞”有明确战略价值。

需要支持：

- 矿场产出石材。
- 防御塔消耗石材。
- 后续修桥、路障、设施修复也消耗石材。

## 建筑

第一版只做 3 类建筑：

| 建筑 | 稳定 Id | 第一版定位 |
|---|---|---|
| 兵营 | `barracks` | 训练和驻军能力 |
| 矿场 | `mine` | 资源产出 |
| 防御塔 | `defense_tower` | 据点防守与防守战支援 |

建筑必须定义化，不能写死成三个专属流程。

```text
FacilityDefinition
  Id
  DisplayName
  FacilityType
  BuildCost[]
  BuildTimeTicks
  AllowedSlotTags[]
  Effects[]
  Actions[]
  BattleModifiers[]

FacilityInstance
  InstanceId
  FacilityId
  SiteId
  SlotId
  Level
  State
  AssignedPopulation
  ProgressTicks
```

### 兵营

战略作用：

- 训练守军。
- 提高场域可驻军上限。
- 后续扩展为兵团恢复、兵种解锁、英雄整备。

战斗作用：

- 防守战可根据驻军生成基础友军。

第一版动作：

```text
训练守军
条件：场域有 active barracks
消耗：population 1, economy 2
效果：AddGarrison("militia", 1)
```

### 矿场

战略作用：

- 每个 WorldTick 产出石材。
- 让埋骨地成为长期争夺目标。

战斗作用：

- 防守战中可作为敌方破坏目标。
- 战斗失败时可能进入 Damaged 状态。

第一版动作：

```text
启用矿场
条件：埋骨地已被玩家控制，场域有 mine_slot
消耗：population 1, economy 2
效果：AddFacility("mine")
```

每 WorldTick：

```text
active mine -> AddResource("stone", 2)
```

### 防御塔

战略作用：

- 提高据点防守值。
- Raid 自动结算时降低损失。

战斗作用：

- 防守战生成一次或多次塔防支援。
- 后续可扩展为视野、远程压制或入口封锁。

第一版动作：

```text
建造防御塔
条件：场域有 tower_slot，玩家控制该场域
消耗：stone 4, economy 2
效果：AddFacility("defense_tower")
```

防守战修正：

```text
BattleModifier
  Type: tower_support
  SourceFacilityInstanceId
  Uses: 1
```

## 场域状态

场域不是一次性副本。每个可运营场域都要有运行态：

```text
WorldSiteState
  SiteId
  OwnerFaction
  ControlState
  Resources: ResourceStore
  FacilityInstances[]
  FacilitySlots[]
  Garrison[]
  ActiveTags[]
  DamageLevel
  LastVisitedTick
  PendingThreatIds[]
```

### 控制状态

V1 使用轻量状态：

| 状态 | 含义 |
|---|---|
| `Unknown` | 未发现 |
| `Neutral` | 已知但无人控制或暂不争夺 |
| `Hostile` | 敌方控制 |
| `Contested` | 争夺中 |
| `PlayerHeld` | 玩家控制 |
| `Damaged` | 玩家控制但受损 |
| `Lost` | 曾被玩家控制，后被夺回 |

### 设施槽位

不要允许所有场域随便造所有建筑。场域定义提供槽位：

```text
FacilitySlotDefinition
  SlotId
  Tags[]
  AllowedFacilityIds[]
  InitialFacilityId?
```

例子：

```text
埋骨地
  mine_slot_01: allow mine
  tower_slot_01: allow defense_tower
```

这样后续扩展村庄、码头、传送锚点、祭坛、商铺时，不需要改运行时结构。

## 场域行动

所有建筑、资源、驻军相关交互都走通用行动定义。

```text
WorldActionDefinition
  Id
  DisplayName
  Scope
  TargetRules[]
  Conditions[]
  Costs[]
  Effects[]
  DurationTicks
```

示例：

```text
Action: build_defense_tower
Scope: Site
Conditions:
  SiteOwner == player
  HasEmptyFacilitySlot(tag=tower)
Costs:
  stone 4
  economy 2
Effects:
  AddFacility(defense_tower)
```

```text
Action: train_militia
Scope: Site
Conditions:
  HasFacility(barracks, active)
Costs:
  population 1
  economy 2
Effects:
  AddGarrison(militia, 1)
```

第一版可以先同步完成行动，`DurationTicks` 预留给后续建造时间。

## 敌方威胁

V1 不做完整 4X AI，只做威胁计划。

```text
EnemyThreatPlan
  Id
  SourceSiteId
  TargetSiteId
  ThreatType
  Stage
  CountdownTicks
  EnemyGroupId
  VisibleIntelLevel
  Consequence
```

`VisibleIntelLevel` is governed by the strategic fog-of-war rules in `strategic-world-fog-of-war.md`. It should be treated as player knowledge, not as threat ownership or behavior authority.

第一版威胁：

```text
墓园 -> 埋骨地 Raid
```

推进规则：

```text
每次玩家完成一次战略行动或战斗
-> WorldTick +1
-> Threat Countdown -1
-> Countdown 到 0 时进入 Attacking
```

玩家应对：

- 在埋骨地防守。
- 提前建防御塔。
- 派驻守军。
- 后续扩展为地表拦截野战。

不做：

- 完整敌方经济。
- 多势力外交 AI。
- 每秒实时推进。

## 战斗衔接

战斗不直接读取或修改大世界状态。它只消费请求，返回结果。

```text
WorldEncounterContext
-> BattleStartRequest
-> WorldSiteRoot
-> BattleResult
-> WorldResultApplier
-> WorldSiteState
```

### BattleStartRequest V1

```text
BattleStartRequest
  ContextId
  EncounterId
  BattleKind
  SourceSiteId
  TargetSiteId
  AttackerFaction
  DefenderFaction
  AvailableEntrances[]
  PlayerGarrison[]
  EnemyGroupId
  BattleModifiers[]
  ObjectiveIds[]
  ReturnScenePath
```

第一版可以先让 `BattleSessionHandoff` 承载最小 request，再逐步替换当前 `contextId + encounterId`。

### BattleResult V1

```text
BattleResult
  ContextId
  Outcome
  ObjectiveResults[]
  Casualties[]
  ResourceChanges[]
  SiteStateChanges[]
  FacilityStateChanges[]
  TagsAdded[]
  TagsRemoved[]
```

当前 `BattleSessionResult` 只有胜负，不够。V1 需要至少支持：

- 攻占埋骨地。
- 防守埋骨地。
- 防守失败导致埋骨地 Damaged 或 Lost。
- 防御塔在战斗中消耗支援次数或被破坏。

### 第一版结算表

| 情况 | 世界结果 |
|---|---|
| 玩家攻占埋骨地胜利 | 埋骨地变为 `PlayerHeld`，解锁矿场槽位 |
| 玩家攻占埋骨地失败但撤退 | 埋骨地保持 `Hostile`，玩家损失经济或人口 |
| 墓园 Raid 防守胜利 | 埋骨地保持 `PlayerHeld`，威胁清除 |
| 墓园 Raid 防守失败 | 埋骨地变为 `Damaged` 或 `Lost` |
| 防御塔存在 | Raid 自动结算损失降低，防守战获得塔支援 |
| 驻军存在 | 防守战生成民兵，自动结算防守值提高 |

## 运行时分层

推荐代码归属：

```text
src/Definitions/World/
  ResourceDefinition.cs
  FacilityDefinition.cs
  WorldSiteDefinition.cs
  WorldActionDefinition.cs
  StrategicWorldDefinition.cs

src/Domain/World/
  ResourceStore.cs
  WorldSiteState.cs
  FacilityInstance.cs
  GarrisonState.cs
  EnemyThreatPlan.cs
  StrategicWorldState.cs

src/Application/World/
  StrategicWorldService.cs
  WorldActionResolver.cs
  WorldTickService.cs
  WorldBattleRequestBuilder.cs
  WorldBattleResultApplier.cs

src/Presentation/World/
  StrategicWorldRoot.cs
  Sites/WorldSiteRoot.cs
  SiteInteractions/
```

当前场景目录约定：

```text
scenes/world/StrategicWorldRoot.tscn
scenes/world/sites/WorldSiteRoot.tscn
scenes/world/sites/
scenes/world/site_interactions/
```

当前代码迁移判断：

| 当前实现 | V1 处理 |
|---|---|
| `BattleSessionHandoff` | 短期扩展，长期替换为 request/result handoff |
| `WorldSiteRoot` | 迁入 `sites`，作为通用场域运行壳，不重写战斗流程 |
| `sites/` | 存放具体场域详细地图，不再使用 `maps` 子目录 |
| `site_interactions/` | 存放场域详细交互点的场景、资源或脚本占位 |
| 旧世界原型入口 | 已从当前主流程清理，不再作为 V1 实现参考入口 |

## 实现阶段

### 阶段 1：世界状态和定义

目标：

- 建立通用资源、建筑、场域、行动、威胁定义。
- 建立 `StrategicWorldState`。
- 能在纯逻辑层执行建造、训练、WorldTick。

验收：

- 玩家营地有初始人口、经济、石材。
- 埋骨地可从 `Hostile` 变为 `PlayerHeld`。
- 可建造矿场、防御塔。
- 矿场每 tick 产出石材。

### 阶段 2：战略地图 UI

目标：

- 新建 V1 战略地图入口场景。
- 显示玩家营地、埋骨地、墓园和可行走地表。
- 选中场域后显示资源、建筑、驻军、威胁和可执行行动。

验收：

- 玩家能理解每个场域归属、资源、建筑。
- 所有行动来自 `WorldActionDefinition` 或等价通用定义，不写死按钮逻辑。

### 阶段 3：战斗 request/result

目标：

- 扩展 `BattleSessionHandoff` 或新增 handoff 对象。
- 让攻占埋骨地战斗能返回场域结果。
- 战斗胜利后埋骨地进入 `PlayerHeld`。

验收：

- 从战略地图进入战斗。
- 战斗结束回到战略地图。
- 世界状态发生可见变化。

### 阶段 4：建筑影响战斗

目标：

- 防御塔和驻军能影响防守战初始化。
- 战斗读取 `BattleModifier`，不直接读取 World 内部对象。

验收：

- 有防御塔时防守战出现塔支援或等价效果。
- 有驻军时防守战出现民兵或防守数值加成。

### 阶段 5：敌方 Raid

目标：

- 墓园生成对埋骨地的 Raid。
- WorldTick 推进倒计时。
- 到期后触发防守战或自动结算。

验收：

- 玩家能在 Raid 到达前建塔或驻军。
- 未处理 Raid 会让埋骨地受损或丢失。
- 防守胜利会清除该威胁。

### 阶段 6：持久化

目标：

- 保存并读取 `StrategicWorldState`。
- 保证场域状态、资源、建筑、威胁可以恢复。

验收：

- 退出重进后，埋骨地控制权、矿场、防御塔、资源库存、威胁倒计时仍然存在。

## 非目标

V1 不做：

- 完整开放世界。
- 完整城建科技树。
- 大量资源链。
- 多势力外交 AI。
- 情感系统深度接入。
- 装备系统。
- 完整兵种克制。
- 完整随机大陆。
- 真实时间后台推进。
- 完整统一天下终局和全量三群复刻。
- 把场域经营做成只有全屏菜单的 UI。

情感系统只保留后续接入点。等 V1 主链路稳定后，再让剧情、招募、驻军士气、战斗支援消费情感查询。

## 设计红线

- 不把资源写成固定字段，必须走 `ResourceId -> amount`。
- 不把建筑写成硬编码分支，必须走 definition + instance。
- 不让 World 直接操作 Battle 节点。
- 不让 Battle 直接保存 WorldSiteState。
- 不修改 Battle flow、AP、TurnSystem 来实现大世界功能。
- 不为了一个建筑写一套专属 UI 和专属服务。
- 不把失败做成 Game Over，失败应回写成场域受损、资源损失、威胁增强或据点丢失。

## 第一版成功标准

V1 成立的标准不是内容多，而是玩家能明确感到：

```text
我在大世界做的运营选择，改变了下一场战斗。
我在战斗里的结果，改变了大世界状态。
敌人会推动世界局势，但不会把游戏变成实时压力。
场域不是一次性副本，而是会保留建筑、驻军、资源和损毁记忆。
大地图不是场域列表，而是可以承载部队移动、拦截和后续野外机会的世界地表。
```
