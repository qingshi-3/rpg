# Strategic World Site Grid Exploration Checks

## Grid Movement

前置：进入一个陌生或敌对 `WorldSite`，该场域使用人工 `TileMapLayer` 和可读 `BattleGridMap`。

操作：

- 点击一个可达可走格。

期望：

- 一个小队 marker 沿格子路径移动到目标格。
- `WorldSiteState.Exploration.CurrentCell*` 更新到目标格。
- 目标格加入 `VisitedCellKeys` 和 `RevealedCellKeys`。
- 不显示战斗 HUD、AP、回合顺序或敌人行动阶段。
- 不推进 `WorldTick`。

## Blocked Movement

操作：

- 点击不可达格、无 top surface 的格，或被不可走 tile 阻隔的格。

期望：

- 小队 marker 不移动。
- UI 显示低噪失败原因。
- 日志记录 `exploration_destination_missing` 或 `exploration_destination_unreachable`。
- 不 fallback 成直线移动。

## Interest Point Reveal

操作：

- 移动到一个已揭示探索点附近。
- 选择“观察”或“调查”。

期望：

- 右侧面板显示点位名称、描述、行动、风险和后果。
- 行动结果写入 `RevealedPointIds` 或 `ResolvedPointIds`。
- 一次性点位不能重复领取奖励。

## Alert Flow

操作：

- 执行一个带 `AlertDelta` 的潜入或强行动作。

期望：

- `AlertLevel` 按定义变化，并限制在 `0..5`。
- UI 反馈说明警戒变化来源。
- 警戒到达上限时，触发遭遇或给出明确撤离/战斗选择。

## WorldTick Boundary

操作：

- 只移动和观察。
- 再执行一个明确 `ConsumesWorldTick` 的清理行动。

期望：

- 移动和观察不推进 `WorldTick`。
- 清理行动推进一次 `WorldTick` 或进入明确的耗时任务。
- 推进世界步时同步处理威胁、施工和产出提示。

## Battle Trigger

操作：

- 执行“强攻”或潜入失败触发战斗。

期望：

- 创建 `BattleStartRequest`。
- 请求携带 `TargetSiteId`、探索点 id、入口格 `x:y:height`、`AlertLevel`。
- 战斗部署或修正能读取探索上下文。
- 探索 UI 隐藏，战斗 HUD 显示。

## Battle Turn Boundary

操作：

- 在探索触发的战斗中推进多个战斗回合。

期望：

- 战斗回合不推进 `WorldTick`。
- 战斗回合不推进施工、生产、探索耗时行动或警戒倒计时。
- 战斗内短周期资源或命令节奏不得影响探索移动；不要把旧战斗 AP 作为未来战斗架构要求。

## Battle Result Writeback

操作：

- 完成探索触发的战斗。

期望：

- 局部胜利：相关探索点变为 resolved，可继续探索。
- 核心胜利：场域控制权或模式转为可经营。
- 失败撤退：保留已访问/已揭示记忆，警戒度按规则保留或上升。
- 战后返回探索或经营态时，UI 从 `WorldSiteState` 重建，不依赖旧场景缓存。
