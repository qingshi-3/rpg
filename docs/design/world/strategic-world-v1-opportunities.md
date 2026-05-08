# 战略大世界 V1 野外小场域

本文定义 V1 大地图上的短生命周期野外小场域。它服务于“大地图持续变化、玩家临时发现并处理机会”的体验，但不引入新的持久 `WorldSite`。

## 定位

- `WorldOpportunity` 是短生命周期机会，不是持久可经营场域。
- 只有长期存在、可被占领、经营、驻军或作为战斗回写目标的地点才是 `WorldSite`。
- 野外小场域由世界推进或地图算法生成，玩家在大地图 marker 与详情面板中处理。
- V1 交互先采用即时处理按钮；后续如需要进入独立小地图，也应由 `EncounterRequest` 或专用交互场景承接，不能把机会伪装成 `WorldSite`。

## 数据结构

```text
WorldOpportunityDefinition
  Id
  DisplayName
  Description
  PoolId
  Weight
  DurationTicks
  CompletionText
  CompletionRewards[]
  Tags[]

OpportunitySpawnPointDefinition
  Id
  DisplayName
  MapPosition
  Radius

OpportunitySpawnRuleDefinition
  Id
  PoolId
  MinWorldTick
  CheckIntervalTicks
  SpawnChancePermille
  CooldownTicks
  MaxActiveCount
  PositionJitterRadius
  SpawnPointIds[]

WorldOpportunityState
  OpportunityId
  DefinitionId
  SpawnRuleId
  SpawnPointId
  WorldPosition
  CreatedTick
  ExpiresTick
  Status
  Tags[]
```

设计意图：内容作者配置“有哪些小场域”“在哪些小区域可能出现”“按什么节奏抽池子”；运行态只保存当前实例和冷却。

## 生成规则

- `WorldTick` 推进时检查 `OpportunitySpawnRuleDefinition`。
- 规则满足最小 tick、检查间隔、冷却和活跃上限后才进行概率判定。
- 候选机会按 `Weight` 权重从同一个 `PoolId` 中抽取。
- 候选生成点从规则允许的 `SpawnPointIds` 中抽取。
- 最终位置为生成点 `MapPosition` 加半径内随机偏移，偏移上限受 `PositionJitterRadius` 和生成点 `Radius` 限制。
- 随机种子必须稳定，可由 `Seed`、`RunId`、`WorldTick`、规则 id 和当前活跃数派生，保证同一存档时序可复现。

## 生命周期

```text
Spawned -> Active -> Completed
                 -> Expired
```

- `Active`：大地图显示 marker，可选中并处理。
- `Completed`：玩家处理成功，发放奖励或写回状态，marker 消失。
- `Expired`：超过 `ExpiresTick` 未处理，marker 消失。
- 已完成或过期实例可短期保留在 state 里作为历史；后续若数量增长，再增加清理策略。

## UI 交互

- marker 绘制在战略地图表面，不创建新的 `WorldSite` 按钮。
- 左键点击 marker 后，右侧详情面板显示名称、描述、生成点、剩余世界步和奖励。
- 行动面板提供“处理机会”入口，完成后立即刷新资源、详情和 marker。
- 机会选择态不能破坏场域、威胁、出征和小队选择的既有边界。

## 存档与日志

- 存档保存 `OpportunityStates` 和 `OpportunityRuleCooldowns`。
- 读档不保存 UI 选择态；读档后由当前 state 继续推进。
- 关键事件输出低噪日志和 `GameEvent`：`WorldOpportunitySpawned`、`WorldOpportunityCompleted`、`WorldOpportunityExpired`。

## V1 内容池

第一版 `wilderness_v1` 池包含：

- `灵草丛`：采集后获得经济。
- `迷路商队`：护送后获得经济。
- `裸露石脉`：开采后获得石材。

第一版生成点包括西侧荒路、埋骨地外缘和南侧荒脊。后续应优先把生成点迁移到地图锚点资源或内容配置，而不是在业务逻辑里写一次性坐标。