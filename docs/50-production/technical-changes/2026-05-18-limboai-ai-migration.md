# LimboAI AI 迁移文档

> 状态：迁移方案草案。本文用于盘点当前项目中的 AI 决策逻辑，并给出迁移到 LimboAI 维护的实施路径；在实际改运行时架构前，仍需走 `AGENTS.md` 中的 Design Proposal Gate。

## 目标

把当前分散在战斗表现层、战斗运行时、旧自动战斗、世界威胁推进和场景探索巡逻中的 AI 决策整理为可维护的行为树体系。LimboAI 只承担“决策编排”和“可视化调试”职责，不接管战斗真相、世界持久化、结算、寻路图、占位和 UI 表现的权威。

当前项目方向是英雄带队的轻 RTS / 低频空间实时战斗。迁移目标不是恢复旧手动战棋，也不是让纯自动战斗成为未来身份，而是把现有自动决策层抽成可配置、可复用、可调试的 AI 行为层。

## 当前 LimboAI 环境

| 项 | 当前状态 | 迁移含义 |
| --- | --- | --- |
| Godot / C# | `rpg.csproj` 使用 `Godot.NET.Sdk/4.5.1`，`project.godot` 标记 `4.5` + `C#` | 与本地 LimboAI 支持范围匹配。 |
| LimboAI 版本 | `addons/limboai/version.txt` 为 `v1.6.0` | 按 LimboAI 1.6 的资源、`BTPlayer`、`BehaviorTree`、Blackboard 工作流规划。 |
| 插件形态 | `addons/limboai/bin/limboai.gdextension`，`.godot/extension_list.cfg` 已登记扩展 | 这是 GDExtension 形态，不是 Godot 模块编译进引擎。 |
| 示例目录 | 根目录存在 `demo/`，包含 `BTPlayer`、`BehaviorTree` 示例 | 只作为学习参考，不应成为本项目运行时或内容权威。 |
| C# 集成风险 | LimboAI 官方 C# 文档对 GDExtension + C# 直接使用有明确限制提示 | 第一阶段不要假设能直接写 C# 自定义 BT Task；优先采用 GDScript 任务调用窄 C# Facade。 |

## 迁移原则

1. 运行时权威仍在 C# 服务和状态模型中：`BattleRuntimeState`、`BattleRuntimeActor`、`StrategicWorldState`、`WorldSiteState`、战斗结算和世界写回不交给行为树直接修改。
2. LimboAI 行为树输出“意图、命令、动作请求”，由现有 C# 权威路径校验、寻路、占位、执行和记录事件。
3. 自定义 BT Task 初期使用 GDScript 实现，Task 不直接改 Domain 对象，只调用 C# 暴露的 `Node` Facade 或提交 DTO。
4. AI Blackboard 只保存当帧决策上下文和引用键，例如 actor id、target id、command、last failure reason；持久状态继续保存在 Domain / Runtime state。
5. 寻路、占位、伤害、胜负、部队迁移、世界 Tick、资源生产不迁入 BT；BT 只能调用这些服务的只读查询或命令入口。
6. 旧 AutoBattle 后端只作为历史兼容和测试风险源处理，不作为新 LimboAI 迁移的目标架构。

## 当前 AI 逻辑盘点

| 系统 | 文件 | 当前 AI 职责 | 迁移目标 | 优先级 / 风险 |
| --- | --- | --- | --- | --- |
| 战斗表现层敌方意图 | `src/Presentation/Battle/AI/IEnemyIntentPlanner.cs`、`GreedyEnemyIntentPlanner.cs`、`BattleAiContext.cs` | 按最近敌人、技能射程、技能是否可用选择 `BattleIntent` | 用行为树复刻现有贪心逻辑，短期仍输出 `BattleIntent`，保证 UI 预演和动作执行不变 | 高 / 低风险，适合作为第一条行为树落地点 |
| 战斗表现层友方自动意图 | `src/Presentation/Battle/AI/GreedyAlliedIntentPlanner.cs`、`src/Presentation/Battle/Flow/BattleCorpsCommand.cs` | 根据 `Assault`、`FocusFire`、`HoldLine` 选择攻击、集火或保持 | 把军团命令写入 Blackboard，由行为树选择目标和姿态 | 高 / 中风险，牵涉玩家命令可读性 |
| 战斗意图解析 | `src/Presentation/Battle/Intents/BattleIntentResolver.cs`、`BattleIntentTemplate.cs` | 把意图转为 `BattleActionRequest`，处理目标、技能有效性、靠近目标、不可达反馈 | 不整体迁入 BT；作为 BT 输出后的 C# 校验和解析 Facade 保留 | 高 / 中风险，不能让 BT 绕开校验 |
| 战斗威胁投影 | `src/Presentation/Battle/Threats/BattleThreatProjectionBuilder.cs` | 用 AI 上下文投影移动和攻击威胁格，服务 UI 读盘 | 保持为表现层查询；可读取 BT/Planner 输出用于调试，但不成为决策源 | 中 / 低风险 |
| 目标战斗运行时自主战斗 | `src/Runtime/Battle/BattleRuntimeSession.cs`、`BattleRuntimeActor.cs` | `ResolveAutonomousCombat` 中硬编码最近敌军、靠近、固定伤害、胜负循环 | 第二阶段迁成每 actor tick 一次 BT，BT 产生移动/攻击请求，C# 运行时执行并写 `BattleEventStream` | 最高 / 高风险，涉及战斗结果权威 |
| 战斗导航和占位 | `src/Runtime/Battle/Navigation/*` | 网格、动态占位、footprint、reservation、pathfinder | 不迁入 BT；封装成只读查询和命令校验服务供 Task 调用 | 最高 / 高风险，必须保持权威 |
| 战斗流程入口 | `src/Application/Battle/BattleGroupBattleFlowService.cs`、`src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs` | 从 snapshot 跑目标运行时，再结算和报告 | 保持入口；在运行时内部切换 AI 执行器 | 高 / 中风险 |
| 世界站点运行时播放 | `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`、`WorldSiteRoot.BattleRuntimePlayback.cs` | 激活目标战斗运行时，播放移动/伤害事件，应用结果 | 不迁入 BT；只消费 runtime 事件 | 中 / 低风险 |
| 旧自动战斗后端 | `src/Application/Battle/Auto/*`、`src/Application/World/WorldSiteAutoBattleAdapter.cs` | 旧 `AutoBattleSimulation` 自行生成战斗事件、移动、攻击、报告 | 不作为 LimboAI 主迁移目标；目标运行时稳定后再删除或只保留历史测试 | 中 / 中风险，测试仍覆盖 |
| 世界威胁生成 | `src/Application/World/WorldTickService.cs`、`src/Definitions/World/ThreatRuleDefinition.cs`、`src/Domain/World/EnemyThreatPlan.cs` | 定义驱动敌军威胁生成、倒计时、创建 Raid 军队 | 暂不迁移；这是世界规则/内容生成，不是即时 actor 行为 | 低 / 高风险，不宜早迁 |
| 世界军队移动 | `src/Application/World/WorldArmyMovementService.cs`、`StrategicCommandNavigationService.cs` | 战略地图路径推进、到达、拦截、状态切换 | 保持 C# 服务；未来可在“敌方战略 AI”层只决定目标和 intent | 低 / 高风险 |
| 威胁自动结算 | `src/Application/World/WorldThreatService.cs` | 抽象防御分与攻击分结算 Raid | 不迁入 BT；属于结算规则 | 低 / 中风险 |
| 场景探索巡逻 | `src/Application/World/WorldSiteExplorationService.cs`、`SiteExplorationPatrolDefinition.cs`、`SiteExplorationPatrolState.cs` | 固定巡逻路线、AP、警戒半径、触发探索战斗 | 第三阶段迁成探索巡逻 BT：路线巡逻、等待 AP、侦测、警戒、触发遭遇 | 中 / 中风险，依赖战斗迁移经验 |
| 探索表现层 | `src/Presentation/World/Sites/WorldSiteRoot.SiteExplorationFlow.cs`、`WorldSiteRoot.SiteExplorationPresentation.cs` | 输入推进探索 tick，表现队伍和巡逻单位 | 不迁入 BT；只展示服务输出和警戒状态 | 低 / 低风险 |

## 推荐架构

### 1. 行为树宿主层

新增一个窄宿主层，而不是让业务代码散落调用 LimboAI：

| 建议路径 | 职责 |
| --- | --- |
| `scenes/ai/battle/BattleAiAgentHost.tscn` | Godot 资源化的 BT 宿主，包含 `BTPlayer`，绑定行为树资源和一个 C# Facade 节点。 |
| `assets/ai/battle/*.tres` | `BehaviorTree` 资源，例如 `battle_corps_assault.tres`、`battle_corps_hold_line.tres`、`battle_enemy_basic.tres`。 |
| `scripts/ai/limbo_tasks/battle/*.gd` | LimboAI 自定义条件和动作 Task，读取 Blackboard，调用 Facade。 |
| `src/Runtime/Battle/AI/BattleAiDecisionFacade.cs` | C# Facade：暴露目标查询、技能/攻击查询、移动请求、攻击请求；不允许 Task 直接改 runtime state。 |
| `src/Runtime/Battle/AI/BattleAiDecisionResult.cs` | 纯 DTO：记录本 tick 的 actor id、target id、动作类型、失败原因。 |

如果先迁表现层 `BattleIntent`，Facade 可以先落在 `src/Presentation/Battle/AI/`，但最终目标应迁回 runtime-facing 边界，避免表现层继续承担 AI 权威。

### 2. Blackboard 约定

| 变量 | 类型 | 生命周期 | 说明 |
| --- | --- | --- | --- |
| `actor_id` | `StringName` / string | 每个 agent 固定 | 当前行动单位。 |
| `team_id` / `faction_id` | string | 每 tick 刷新 | 判定敌友，不由 BT 改写。 |
| `command` | string | 玩家命令变化时刷新 | `Assault`、`FocusFire`、`HoldLine` 等命令输入。 |
| `target_id` | string | 行为树本 tick 写入 | 目标选择结果。 |
| `ability_id` | string | 行为树本 tick 写入 | 候选技能或普通攻击标识。 |
| `desired_action` | string | 行为树本 tick 写入 | `move`、`attack`、`cast`、`hold`、`retreat`。 |
| `last_failure_reason` | string | 每 tick 可覆盖 | 供低噪日志和调试面板使用。 |

Blackboard 不保存生命值、坐标、胜负、战斗结果、世界状态等权威数据；这些数据每 tick 由 Facade 从 C# runtime 读取。

### 3. Task 类型

第一批 Task 应尽量小且无副作用：

| 类别 | Task | 职责 |
| --- | --- | --- |
| 条件 | `HasBattleContext` | 确认 Facade、actor、runtime context 可用。 |
| 条件 | `IsActorAlive` | 当前 actor 仍可行动。 |
| 条件 | `HasCommand` | 检查 Blackboard 中是否存在指定军团命令。 |
| 条件 | `HasHostileTarget` | 当前是否已选中有效敌方目标。 |
| 条件 | `TargetInAttackRange` | 只读查询攻击/技能射程。 |
| 条件 | `PathToTargetAvailable` | 只读查询是否存在合法靠近路径。 |
| 动作 | `SelectNearestHostile` | 写入最近敌方 `target_id`。 |
| 动作 | `SelectLowestHpHostile` | 写入低血量集火目标。 |
| 动作 | `ResolvePrimaryAbility` | 写入本 tick 候选技能/普通攻击。 |
| 动作 | `RequestMoveTowardAttackRange` | 提交移动请求给 Facade，由 C# 校验和执行。 |
| 动作 | `RequestUseAbility` | 提交攻击/技能请求给 Facade，由 C# 校验和执行。 |
| 动作 | `EmitHold` | 提交保持/无动作结果，并记录原因。 |
| 探索条件 | `IsPartyInAlertRadius` | 查询探索队伍是否进入巡逻警戒范围。 |
| 探索动作 | `FollowPatrolRoute` | 通过 Facade 请求巡逻单位前往下一 route cell。 |
| 探索动作 | `TriggerExplorationEncounter` | 请求探索服务进入暂停/警戒/战斗入口。 |

### 4. 行为树草图

`battle_enemy_basic.tres`：

```text
Selector
  Sequence
    IsActorAlive
    SelectNearestHostile
    ResolvePrimaryAbility
    TargetInAttackRange
    RequestUseAbility
  Sequence
    IsActorAlive
    SelectNearestHostile
    PathToTargetAvailable
    RequestMoveTowardAttackRange
  EmitHold
```

`battle_corps_commanded.tres`：

```text
Selector
  Sequence
    HasCommand("HoldLine")
    SelectNearestHostile
    TargetInAttackRange
    RequestUseAbility
  Sequence
    HasCommand("FocusFire")
    SelectLowestHpHostile
    ResolvePrimaryAbility
    RequestUseAbilityOrMove
  Sequence
    HasCommand("Assault")
    SelectNearestHostile
    ResolvePrimaryAbility
    RequestUseAbilityOrMove
  EmitHold
```

`site_exploration_patrol.tres`：

```text
Selector
  Sequence
    IsPartyInAlertRadius
    TriggerExplorationEncounter
  Sequence
    HasEnoughPatrolActionPoints
    FollowPatrolRoute
  EmitHold
```

## 分阶段迁移计划

### Phase 0：插件接入稳定化

1. 保留 `addons/limboai/` 作为插件目录，但确认 `demo/` 不被项目 runtime 或资源索引当成正式内容。
2. 新增一个最小 LimboAI 验证场景或测试脚本，只验证 `BTPlayer` 能加载空树/简单树，不启动完整游戏流程。
3. 确认 Godot 编辑器不再因为 GDExtension 残留临时 dll 或 demo 根目录冲突崩溃。
4. 在正式架构变更前创建 active proposal，明确 AI 层职责、BT 资源目录、Facade 边界和测试入口。

### Phase 1：迁移表现层 Greedy Planner

目标：在不改变战斗结果的前提下，把 `GreedyEnemyIntentPlanner` / `GreedyAlliedIntentPlanner` 的分支复刻成行为树。

1. 新增 `IBattleAiDecisionRunner`，输入 `BattleAiContext`、actor、command，输出现有 `BattleIntent`。
2. 新增 LimboAI runner 实现，但保留 greedy runner 作为测试对照，不作为长期 fallback。
3. 用相同场景断言 BT runner 与 greedy runner 输出一致：最近目标、远程压制、直接攻击、集火、保持阵线。
4. `BattleIntentResolver` 继续作为唯一 `BattleActionRequest` 解析和校验路径。
5. 通过后再决定是否删除 greedy planner 或降级为测试 oracle。

### Phase 2：迁移目标战斗运行时自主战斗

目标：替换 `BattleRuntimeSession.ResolveAutonomousCombat` 内部硬编码决策，但保持 `BattleEventStream`、结果和 settlement 入口兼容。

1. 抽出 `BattleRuntimeAiExecutor`，每 tick 遍历可行动 actor。
2. 每个 actor 构建 Blackboard 快照：actor id、faction、command/stance、可见敌人、当前 tick。
3. `BTPlayer` tick 后只得到 `BattleAiDecisionResult`。
4. C# runtime 根据结果调用 `BattlePathfinder`、`BattleDynamicOccupancy`、`BattleMovementReservationMap`、伤害和事件写入。
5. 增加 regression：同一 snapshot 下，最小战斗仍能胜负结算、不会重叠 footprint、不可达时记录 hold/failure reason。
6. 删除 `ResolveAutonomousCombat` 中最近敌人/移动/攻击的硬编码分支，保留循环、终止和 outcome 判定权威。

### Phase 3：迁移场景探索巡逻

目标：让探索巡逻的“巡逻、等待 AP、警戒、触发遭遇”由行为树维护，同时保留 `WorldSiteExplorationService` 的状态权威。

1. 把 `WorldSiteExplorationService.AdvanceTick` 中的巡逻决策抽成 `IExplorationPatrolAiRunner`。
2. 行为树输出下一巡逻动作：hold、move next route cell、trigger alert。
3. `WorldSiteExplorationService` 继续校验 route cell、AP、阻挡、pause reason、`ActiveAlertPatrolId`。
4. 保持 `SiteExplorationPatrolDefinition` 作为内容配置；BT 只增加行为策略引用，不替代路线数据。
5. 回归测试覆盖：AP 巡逻移动、警戒半径暂停、触发 patrol battle、胜利后移除触发 patrol。

### Phase 4：整理旧 AutoBattle 后端

目标：目标战斗运行时已经由 LimboAI 决策后，再处理旧自动战斗实现。

1. 检查 `src/Application/Battle/Auto/*` 是否仍有正式入口引用。
2. 若无正式入口，把相关测试改为历史兼容或删除计划的一部分。
3. `WorldSiteAutoBattleAdapter` 不应重新接回 `WorldSiteRoot` 主流程。
4. 删除前必须保留目标运行时的报告、事件播放、结算覆盖。

### Phase 5：战略 AI 后置

世界威胁生成、军队移动、自动 Raid 结算暂不迁移。未来如果需要敌方战略 AI，应新增一层“战略意图规划器”，只负责决定目标、意图和优先级，仍由 `WorldTickService`、`WorldArmyMovementService` 和 `WorldThreatService` 执行规则。

## 不应迁移到 LimboAI 的职责

| 职责 | 保留位置 | 原因 |
| --- | --- | --- |
| 战斗寻路、footprint、占位、reservation | `src/Runtime/Battle/Navigation/*` | 这是空间规则权威，BT 直接改会破坏一致性。 |
| 伤害、死亡、胜负、事件流 | `BattleRuntimeSession` 及后续 runtime 服务 | 影响结算和回放，必须可测试、可复现。 |
| 战斗结算和世界写回 | `BattleGroupBattleFlowService`、`WorldBattleResultApplier` | 关系到持久化状态和奖励/损失。 |
| 世界 Tick 资源生产和威胁生成 | `WorldTickService` | 内容规则，不是 actor 行为。 |
| 战略路径推进和到达状态 | `WorldArmyMovementService` | 地图导航和状态机权威。 |
| UI 文字和表现播放 | Presentation 层 | 消费 AI 结果，不参与决策。 |

## 测试与验收标准

| 阶段 | 必跑验证 |
| --- | --- |
| 插件稳定化 | 最小 Godot 场景能加载 `BTPlayer`；编辑器打开项目不崩溃；`dotnet build rpg.csproj -maxcpucount:2 -v:minimal` 通过。 |
| 表现层 planner 迁移 | 新增 planner 对照测试；现有 `BattleIntentResolver` 相关 regression 不变。 |
| 目标战斗运行时迁移 | `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj`；footprint、胜负、非法 handoff 覆盖不退化。 |
| 探索巡逻迁移 | `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj` 中探索 tick 和 patrol battle case 通过。 |
| 世界层未迁移保护 | `dotnet run --project tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj`；战略移动和 Raid 状态不受 BT 影响。 |
| 旧 AutoBattle 整理 | 删除或降级前跑 `tests/AutoBattleRuntimeRegression`，并确认正式入口不再依赖旧后端。 |

验收时要看行为树输出日志，而不是只看胜负：每次 actor 行动应能追踪到 tree id、actor id、target id、decision、failure reason。日志必须低噪，不能每帧刷屏。

## 主要风险

1. LimboAI GDExtension + C# 直接交互存在官方限制提示，直接写 C# 自定义 Task 风险高；第一版应走 GDScript Task + C# Facade。
2. BT 资源会成为新的内容面，必须有缺失树、缺失 Task、Blackboard 变量缺失的验证和明确失败日志。
3. 如果 BT 直接修改 `StrategicWorldState` 或 `BattleRuntimeState`，会绕开现有结算和测试，必须禁止。
4. 目标运行时现在仍是最小自主战斗，迁移时容易把“让 AI 复杂”误当作“重做战斗系统”；第一目标是行为等价和可调试。
5. 根目录 `demo/` 是 LimboAI 示例项目，若被 Godot 当成本项目资源引用，可能污染导入和构建；需要单独清理策略，但不在本文迁移阶段直接删除。

## 建议的第一张票

创建 active proposal：`design-proposals/active/<date>-limboai-ai-runtime-boundary/`。

提案 expected 文档至少要确认：

1. LimboAI 决策层的目录和资源命名。
2. GDScript Task 调用 C# Facade 的唯一边界。
3. `BattleIntentResolver` / `BattleRuntimeSession` 哪个阶段仍是权威。
4. 旧 Greedy Planner 和旧 AutoBattle 的退场标准。
5. 每阶段 regression 入口和失败日志格式。

提案通过后，先做 Phase 0 + Phase 1。不要直接从 `BattleRuntimeSession.ResolveAutonomousCombat` 开始改，因为那是战斗结果权威，风险比表现层 planner 高。
