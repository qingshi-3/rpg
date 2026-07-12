# 当前系统代码审查报告

- 日期：2026-07-12
- 审查对象：启动时的当前工作树，不是干净检出，也不是单独的 `HEAD`
- 主场景：`scenes/world/StrategicWorldRoot.tscn`
- 结论状态：审查与报告撰写完成，等待独立验证

## 一、架构与业务结论

当前系统已经具备“战略出征 -> 战前部署 -> 实时战斗 -> 结果回写”的大部分纵向骨架，战斗固定时序、多人选择目的地信标的原子校验、战术暂停、显式交接失败和资源路径完整性也有可靠的正面证据。但它还不能被判定为一个安全闭环：战略战斗主线同时依赖 Strategic Management、Bridge Active Context、旧 `BattleStartRequest`、旧 `WorldSiteState` 和旧世界军队载体，导致参与者、快照、地图控制权、命令权限和结果落盘分别出现多重权威。

最严重的业务后果集中在九项 P1：合法预备队会使结果回写被拒；一个“英雄 + 一个军团”会被放大为四英雄四军团；最终快照仍由旧请求重建；胜利后战略状态和地图显示会分裂；桥接失败不能把军团恢复到合法城市；结果先改内存再直接覆盖存档且无法安全重试；战斗编组指挥状态被镜像到每个 actor；VS-10 重整/撤退生产入口未完成；生产 UI 绕过应用层命令授权且现有回归接受敌军技能命令。

因此，当前版本不宜以“先删旧代码”作为第一步。应先阻止状态损坏并收敛单一权威，再补齐产品契约和验证门，最后进行精简。直接机械拆分大 partial、删除桥接/测试缝接口或清理仍在生产路径上的旧载体，会扩大回归面而不会消除双权威。

## 二、基线与漂移

| 项目 | 记录 |
| --- | --- |
| 分支 | `main` |
| HEAD | `212c69161fabe375d524adf72c13cfe9cf290d79` |
| 启动 tracked diff hash（PowerShell text-pipeline） | `ded0c7c04acbbda9c0a3962373d49206f61b64a3` |
| 启动状态 | 671 项：41 modified、619 deleted、11 个未跟踪顶层条目 |
| 首次预验证状态 | 分支仍为 `main`，HEAD 仍为 `212c69161fabe375d524adf72c13cfe9cf290d79`；PowerShell text-pipeline tracked diff hash 为 `73825094cace873df7d70197181ef492ee1b99c2`；普通/顶层视图为 43 modified、619 deleted、12 个 untracked status entries |
| 14:38 复核状态 | 分支仍为 `main`，HEAD 仍为 `212c69161fabe375d524adf72c13cfe9cf290d79`；43 modified、619 deleted、12 untracked。PowerShell text-pipeline hash 为 `64700e83be62c91a82c429f81d5ca76b5bae6613`；同一时点 native byte-preserving cmd pipeline hash 为 `2d2f338af0e2f6c3cfd528abb9ec7a1b1e284124`。不同管线的 hash 不互相比对。 |
| 审查对象 | 上述脏工作树的启动快照；不清理、不还原、不以 `HEAD` 替换 |

启动后发生两类漂移。其一是已记录的未跟踪 Strategic Region Preview：`StrategicRegionPreviewRegression` 在测试时先有 2 项通过，随后因 `StrategicRegionOverlayChunk.tscn` 缺失而未处理失败；03:51:42 又创建了四个 preview 文件，03:59:56 `StrategicRegionOverlayChunk.cs` 与 shader 再次变化。该事实只证明测试时失败和随后目标移动，不能证明当前稳定实现的结论；本报告继续将该移动目标排除在当前系统结论之外，未继续检查或编辑它。

其二是持续发生的 tracked drift：启动状态列表中尚未修改的 `tools/world-map-workbench/src/server/artifacts.ts` 与 `tools/world-map-workbench/tests/artifacts.test.ts` 后来被修改，内容增加自然领地边缘采样及其测试；第 12 个未跟踪状态条目是本任务授权生成的本报告。此前记录的 npm test 与 typecheck 早于这两个文件，也不验证之后重新生成的派生领地产物。

首次独立验证于 14:33 后失败并把任务退回 `In Progress`。在先前报告纠正后，下列三个 tracked 派生领地产物于 14:33:29 改变，并于 14:38:16 再次改变：

- `assets/textures/world/masks/territory/region_lookup.json`
- `assets/textures/world/masks/territory/region_outlines.json`
- `assets/textures/world/masks/territory/territory_mask.png`

启动 hash `ded0c7c04acbbda9c0a3962373d49206f61b64a3` 与首次预验证 hash `73825094cace873df7d70197181ef492ee1b99c2` 均由既有 PowerShell text-pipeline 方法生成。14:38 同一时点，该方法得到 `64700e83be62c91a82c429f81d5ca76b5bae6613`；native byte-preserving cmd pipeline 得到 `2d2f338af0e2f6c3cfd528abb9ec7a1b1e284124`。不同方法的 hash 不可相互比较。

这三个派生输出是已知并发移动集合，其精确当前字节排除在语义审查结论之外。后续 hash 变化若仅限于这三个文件，不推翻已审查发现、P2-10、资源路径存在性扫描或验证记录，但精确生成产物正确性仍未验证；任何超出该命名集合的漂移仍必须重新审查。该处置不改变 P2-10：当前源码的 `compileRegionArtifacts` 在 `artifacts.ts:130-134` 仍以 `Promise.all` 调用三次相互独立的 `atomicWriteFile`，所以复合产物仍缺少整体原子提交。Strategic Region Preview 仍是另一项单独排除的移动目标。

## 三、方法、范围与证据语言

### 3.1 方法与范围

审查以现行玩法权威、系统权威和 VS-01 至 VS-16 为准，静态追踪四条端到端流程：战略定向与出征、战前准备与编译、实时命令与模拟、结果结算与继续。覆盖状态所有权、事务/失败语义、Battle Runtime、Godot 生命周期与资源、性能/可观测性、测试工具链和代码精简候选。

使用的 GodotPrompter 技能为 `using-godot-prompter`、`godot-code-review`、`save-load`。技能提供审查清单；项目 Godot 4.7 权威、玩法权威和系统权威优先。

### 3.2 排除项

- 未进入 `history/`。
- 未修复代码、资源、测试、配置或权威文档。
- 未运行 Godot headless/import/场景 smoke、Profiler 或实际 I/O 故障注入。
- Strategic Region Preview 因并发未跟踪漂移排除。
- 性能项均为静态风险，不声称已测得毫秒开销。

### 3.3 假设与限制

- 评审把启动脏工作树视为一个整体；大量删除可能使旧的源形状测试失真。
- 八个 C# 测试工程均为 `OutputType Exe`，不是 `dotnet test` 工程；其中七个不在 solution。
- 现有 C# 测试没有 `PackedScene.Instantiate`，大量断言依赖 `File.ReadAllText`、源码字符串和旧文件路径，无法替代 Godot 场景/生命周期验证。
- 存档 I/O 失败的控制流后果可由源码确定，但磁盘实际形态必须通过故障注入确认。

### 3.4 证据状态定义

| 标签 | 含义 |
| --- | --- |
| 静态确定 | 当前源码与配置直接证明控制流、数据形状或缺失入口。 |
| 已观察运行证据 | 已有日志或已完成命令记录直接观察到结果。 |
| 推理结论 | 由两处以上静态事实必然或高度确定地组合得到，组合步骤明确列出。 |
| 假设 | 需要定向复现、故障注入或 Profiler 才能确认；不得作为已发生缺陷。 |

## 四、P0-P3 总览

| 级别 | 数量/结论 | 发布含义 |
| --- | --- | --- |
| P0 | 0 | 未发现已证实的灾难性破坏或无法继续审查的问题。 |
| P1 | 9 | 战斗主线存在关键权威、状态回写、actor 基数和命令授权失败；应先处理。 |
| P2 | 15 组 | 启动合法性、结果解释、幂等/存档、性能、地图内容、生命周期、工具事务和测试门存在实质风险。 |
| P3 | 5 组 | 资源缺口、日志策略、资源化、空值分析和低优先级结构债务。 |

## 五、确认发现

### P1-01 合法预备队使战略结果回写必然被拒

- 证据状态：静态确定；最终后果为推理结论。
- 权威：VS-07；`strategic-battle-bridge-architecture.md` 的预备队、参与者映射和结果摘要契约；`battle-result-settlement-architecture.md`。
- 精确证据：`src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationHud.cs:496-543` 只从兼容请求移除未部署组；`src/Application/StrategicBattleBridge/StrategicBattleBridgeModels.cs:103-132` 的 Session 参与者不含部署/预备状态；`src/Application/StrategicBattleBridge/StrategicBattleBridgeService.cs:291-317,398-405` 要求每个 Session 参与者都有 Runtime corps 结果；`src/Application/StrategicManagement/StrategicManagementCommandService.BattleResults.cs:112-119` 又要求每个 expedition 参与者有摘要；`src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs:220-252,824-834` 在拒绝后仍进入展示/返回链。
- 触发：出征携带 2-3 个合法战斗编组，只部署其中一部分并把其余留作预备队。
- 影响：Bridge 拒绝摘要或 Strategic Management 拒绝回写；预备队本应不参战，却阻塞整场结算，并留下 context/载体残留。
- 测试漏检：测试以完整参与者或手工构造结果为主，没有“携带但未部署”的行为级闭环。
- 修复方向：Bridge Draft 明确记录 deployed/reserve；Snapshot 和结果只要求 deployed 参与者，战略命令对 reserve 执行无伤亡解锁；失败路径必须清理或保留可重试上下文，不能软化状态失败。
- 验证：1/2/3 个携带组的全部部署子集矩阵；确认预备队零伤亡、结果可写回、所有锁和 context 被正确消费。

### P1-02 最终快照由旧请求重建并覆盖 Active Snapshot

- 证据状态：静态确定。
- 权威：`strategic-battle-bridge-architecture.md` 明确 Bridge Draft/Active Context 是当前交接，最终摘要不得从旧请求重建；`hero-led-light-rts-system-architecture.md` 的单一权威。
- 精确证据：`src/Presentation/World/StrategicWorldRoot.BattleEntry.cs:19-27,82-94` 先生成旧请求再建 Active Context；`src/Application/StrategicBattleBridge/StrategicBattleBridgeService.cs:172-210` 同时保存 Snapshot 与 CompatibilityRequest；`src/Application/StrategicBattleBridge/StrategicBattleLaunchSnapshotSyncService.cs:44-142` 新建 Snapshot 并从 CompatibilityRequest 的 forces/plan/navigation 拷贝；`src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs:142-179` 用该结果覆盖 `activeContext.Snapshot`；同服务 `298-315,684-705` 结果分母仍读旧请求。
- 触发：任何 Strategic Management 战斗启动，尤其 Draft 与旧请求出现差异时。
- 影响：Bridge Draft 不是启动权威；参与者、部署、导航、数量和结算分母可被兼容载体反向覆盖。
- 测试漏检：源码形状测试确认“同步服务存在”，但未断言 Draft 到 Runtime 的数据血缘唯一性。
- 修复方向：由 Bridge Draft 直接编译最终不可变 Snapshot；旧请求仅作为命名清晰、只读且可移除的边界投影，不能回写 Active Snapshot。
- 验证：人为让兼容投影与 Draft 不同，断言 Runtime 只消费 Draft 编译结果；增加禁止反向覆盖的契约测试。

### P1-03 一个战斗编组被放大为四英雄四军团，伤亡分母含英雄行

- 证据状态：静态确定。
- 权威：玩法 `battle group = 1 hero + 1 main corps`；Runtime actor 数量不能碎裂 commander；可见士兵不是独立长期单位。
- 精确证据：`config/strategic_management/military/corps_common.json:7,24,41` 当前军团数量为 3；`src/Application/World/StrategicExpeditionWorldArmyAdapter.cs:76-96` 输出 1 条英雄行和 1 条 count=3 军团行；`src/Application/World/WorldBattleRequestBuilder.cs:424-449` 两行进入 request；`src/Application/StrategicBattleBridge/StrategicBattleLaunchSnapshotSyncService.cs:89-110,180-229` 按每条 force 的 Count 建 group；`src/Runtime/Battle/BattleRuntimeSession.cs:235-315` 每个 group 都建一个 Hero 和一个 Corps；`src/Application/StrategicBattleBridge/StrategicBattleBridgeService.cs:684-705` 初始分母汇总同一参与者的所有 force 数量。
- 触发：当前任一默认英雄编组进入战斗。
- 影响：1+3 行变成 4 个 BattleGroupSnapshot，再变成 4 Hero + 4 Corps；命令、战斗力、伤亡和报告均被放大，且英雄 force 行进入军团伤亡分母。
- 测试漏检：fixture 多按 Snapshot 直接构造，未从 Strategic Management 配置贯穿到 actor 数量。
- 修复方向：每个战略参与者只编译一个 BattleGroupSnapshot；英雄和主军团分别成为该 group 下 actor/presentation facts；`battleUnitCount` 只控制可见军团表现或明确的 corps actor 展开，不生成额外英雄/指挥者；伤亡基于军团强度契约。
- 验证：三个默认编组逐一和组合启动，断言每个参与者恰好 1 Hero、1 commander、正确的 corps 表现和 0-100 强度换算。

### P1-04 胜利只更新 Strategic Management，主地图仍读取旧 WorldSiteState

- 证据状态：静态确定；VS-12 失败为推理结论。
- 权威：`strategic-management-system-architecture.md` 单一战略权威；VS-12 要求胜利改变战略世界。
- 精确证据：`src/Application/StrategicManagement/StrategicManagementCommandService.BattleResults.cs:21-35` 更新 `StrategicLocationState`；`src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs:910-965` 只清理旧展示残留；`src/Presentation/World/StrategicWorldRoot.MapDrawing.cs:209-250` 颜色读取 `WorldSiteState`；`src/Presentation/World/StrategicWorldRoot.Expedition.cs:114-148` 目标分类也读旧 site owner；`src/Application/StrategicManagement/StrategicManagementRules.cs:847-873` 新规则读 Strategic Location。
- 触发：赢得 Bonefield 后返回主地图或再次选择目标。
- 影响：规则层认为已占领，地图仍可能显示敌对并按旧状态分类；玩家无法可信理解胜利结果。
- 测试漏检：战略命令和旧世界展示分别测试，没有 VS-12 返回地图行为测试。
- 修复方向：主地图颜色、目标分类和交互统一读取 Strategic Management view model；旧 WorldSiteState 仅保留明确展示适配且不得拥有控制权。
- 验证：胜利/失败/重载后三种路径检查同一 location ID 的颜色、文案、可攻击性和后续命令一致。

### P1-05 Bridge 创建失败无法把军团恢复到合法城市

- 证据状态：静态确定。
- 权威：Strategic Management 命令不得部分应用；场景/桥接失败必须回滚到一致状态。
- 精确证据：`src/Application/StrategicManagement/StrategicManagementCommandService.Expeditions.cs:70-79` 创建出征时清空 `HomeCityId`；`97-119,190-228` Cancel 以空 station 解锁，非零军团不会恢复城市；`src/Presentation/World/StrategicWorldRoot.BattleEntry.cs:57-69,95-107` Bridge 失败后 reset 旧军队再 Cancel；`src/Application/StrategicManagement/StrategicManagementRules.cs:781-830` 新出征要求 `HomeCityId == sourceLocation`；`src/Application/World/WorldArmyCommandService.cs:162-183` reset 不清除 `StrategicExpeditionId`。
- 触发：出征已创建并抵达，但 Bridge session 或 Active Context 创建失败。
- 影响：英雄解锁但军团 `HomeCityId` 为空，不能再次合法出征/由城市管理；旧载体继续保留 expedition ID。
- 测试漏检：成功路径和单服务 cancel 测试没有模拟“创建后、桥接前失败”的跨系统回滚。
- 修复方向：出征保存明确 rollback station；Cancel/bridge-failure 用该站点原子恢复英雄、军团和载体别名；若无法恢复则进入显式可诊断失败态。
- 验证：对 session 创建、Active Context 创建、场景切换三个故障点注入失败，逐项核对城市、军团、英雄、expedition 和旧载体。

### P1-06 结果先改内存再直接覆盖存档，I/O 失败后不可安全重试

- 证据状态：控制流静态确定；实际磁盘形态是假设，需故障注入。
- 权威：`battle-result-settlement-architecture.md` 完整结果后才写长期状态；`save-load` 的版本迁移和显式 I/O 失败原则；Strategic Management 命令不得部分应用。
- 精确证据：`src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs:821-863` 先 Apply、再 `SaveCurrentState`、最后清理 context；`src/Application/StrategicManagement/StrategicManagementSaveService.cs:29-65` 使用 `File.WriteAllText` 覆盖 live save，无 temp+replace、回滚或旧版本迁移，`State:null` 变成空状态；`src/Application/StrategicManagement/StrategicManagementCommandService.BattleResults.cs:101-104` duplicate guard 在内存中阻止重试。
- 触发：保存期间权限、磁盘空间、进程终止或写入异常；或加载旧版本/`State:null` 文件。
- 影响：可形成“内存已应用、磁盘未知、context 未消费”；同一进程重试被 duplicate guard 拒绝。不能声称具体磁盘损坏形态，直到故障注入。
- 测试漏检：无 I/O fault injection、崩溃边界、旧版本迁移和 null-state 拒绝测试。
- 修复方向：建立 apply-plan/commit 边界，temp 写入+flush+原子替换/备份；以 session/snapshot/result key 做可重放幂等；持久化成功后才消费 context；旧版本逐级迁移，`State:null` 显式失败。
- 验证：在序列化、temp 写、replace、context consume 各点故障注入并重启，证明只出现旧完整状态或新完整状态，且同结果可安全重试。

### P1-07 战斗编组指挥状态被镜像并按 actor 推进

- 证据状态：静态确定。
- 权威：`battle-runtime-architecture.md`、`battle-command-architecture.md` 和 `battle-group-tactical-region-architecture.md` 要求每个 BattleGroup 一个 commander state。
- 精确证据：`src/Runtime/Battle/BattleRuntimeActor.cs:123,127,136-137` 每 actor 保存 command/beacon/PlanState/objective；`BattleRuntimeDestinationBeaconCommandResolver.cs:65-72` 对组内每 actor 写命令；`BattlePlanStateEmitter.cs:27,36,63` 以 actor 状态发 group 事件；`BattleRuntimeDecisionPhaseCoordinator.cs:49-58` 按 corps actor 决策；`BattleGroupSessionProbeService.cs:124,135,196` 和 `BattleRuntimeSession.cs:237,280,315` 显示兼容展开与 actor 创建；而 `src/Runtime/Battle/Tactics/BattleGroupTacticalState.cs:13-22` 已存在组状态。
- 触发：一个 commander 下有多个 actor，尤其 P1-03 的放大后。
- 影响：同一组可出现多份命令/信标/计划状态与重复事件，状态推进次数随 actor 数量变化。
- 测试漏检：多数 fixture 一个组一个 corps actor，未断言 commander 状态唯一且 actor 仅消费派生 intent。
- 修复方向：命令、信标、计划、战术区、重整/撤退统一存入组状态；actor 只保留执行阶段和必要的 revision cache。
- 验证：同一组 1/2/N actor 下，命令接受、状态迁移和报告事件数量完全一致。

### P1-08 VS-10 重整未实现，撤退/中断无生产入口

- 证据状态：静态确定。
- 权威：VS-10；玩法要求 combined regroup/retreat；命令架构要求明确接受/拒绝/完成。
- 精确证据：`src/Application/Battle/Commands/CommandKind.cs:3-11` 声明 Regroup；`src/Runtime/Battle/BattleRuntimeSessionController.cs:126-139` 仅分派 DestinationBeacon，其余全部交给 skill resolver；后者 `BattleRuntimeHeroSkillCommandResolver.cs:151-153` 只接受 Hero+CastSkill；Controller `182,188,223,229` 仅内部形成终止；`src/Runtime/Battle/BattleTerminationReason.cs:8-9` 虽有 PlayerRetreat/Interrupted 但无生产命令入口。
- 触发：玩家发送 Regroup、Retreat、Move、Attack 或 Hold。
- 影响：除目的地信标和技能外的命令被当作不支持；VS-10 不可完成，失败也缺玩家可理解入口。
- 测试漏检：枚举/源码存在性被当成能力，缺 UI -> Application -> Runtime -> 报告的行为测试。
- 修复方向：为 regroup/retreat 建独立应用校验、Runtime resolver、组状态迁移、事件和 UI disabled reason；不把普通命令送给 skill resolver。
- 验证：实时与暂停下接受/拒绝/效果/报告矩阵，包含无效选择、敌方组和动作锁。

### P1-09 生产 UI 绕过应用层授权，敌军技能命令被现有回归接受

- 证据状态：静态确定，且有测试代码明确期待敌军命令被接受。
- 权威：`battle-command-architecture.md` 要求 Application 验证战斗、所有权和频道；Presentation 只提交请求。
- 精确证据：`src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs:401` 与 `WorldSiteRoot.BattleRuntimeDestinationBeacon.cs:212` 直接调用 Runtime Controller；目的地 resolver `BattleRuntimeDestinationBeaconCommandResolver.cs:46` 自行检查玩家阵营，而 skill resolver `BattleRuntimeHeroSkillCommandResolver.cs:151,369-386` 不验证玩家阵营，也未对 authored skill channel 做一致性比较；`tests/TargetBattleArchitectureRegression/TargetBattleEffectCommitBufferRegressionCases.cs:485-498` 明确断言 enemy-group skill accepted。
- 触发：构造敌方 battle group/source actor 的 Hero CastSkill 请求，或频道与技能定义不一致的请求。
- 影响：生产入口没有统一授权边界；敌方单位可被玩家命令，配置频道成为假契约。
- 测试漏检：该回归把越权接受当成正向预期；`BattleCommandApplicationValidator` 未接入生产。
- 修复方向：UI 统一经过 `BattleCommandApplicationValidator`，Runtime 再做上下文复验；验证 player ownership、deployed group、source actor、command channel 与 authored snapshot channel。该 Validator 不是删除候选。
- 验证：玩家/敌人、Hero/Corps/Combined、合法/伪造 actor、定义频道不匹配的拒绝矩阵。

### P2-01 初始目的地启动合法性不足

- 证据状态：静态确定。
- 权威：VS-07、命令/导航权威要求可达且 footprint-aware。
- 证据：`src/Runtime/Battle/BattleRuntimeSession.cs:332` 只看 `HasInitialDestinationBeacon`；`src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeDestinationBeacon.cs:408` 的目标检查只 `TryGetCell`；`src/Application/StrategicBattleBridge/StrategicBattleLaunchSnapshotSyncService.cs:417-442` 复制布尔/坐标。未形成 walkability、完整 footprint 和 reachability 的统一 launch gate。
- 触发/影响：选中存在但不可站立或不可达的格子仍可能被视为准备完成，进入 Runtime 后才失败。
- 测试漏检：验证布尔字段和节点存在，未用断开拓扑/大 footprint 做启动测试。
- 修复与验证：Bridge launch 前按最终 topology/profile 验证；覆盖水面、断路、边界、大 footprint、不同高度。

### P2-02 真实结果摘要未消费结算/报告后果事实

- 证据状态：静态确定；玩家解释质量是推理结论。
- 权威：结果/结算权威要求 Result、Settlement、Report 同源并解释奖励、损失、命令和技能。
- 证据：`StrategicBattleBridgeService.cs:291-317` 的 participant summary 只按存活计强度；`WorldSiteRoot.BattleRuntime.cs:839-855` 读取反馈后回退固定文本。Bridge Active Context 中虽有 settlement/report carrier，主摘要链未证明完整消费 objective/reward/consequence facts。
- 触发/影响：普通胜负回写后，战略反馈退化为静态奖励或固定句，VS-12 至 VS-16 的解释不可信。
- 测试漏检：报告结构和结算命令分开验证，缺同一事件源的字段级闭环。
- 修复与验证：定义完整 summary mapping 表；用含 skill、beacon、损失、奖励、装备贡献的事件 fixture 逐字段核对 settlement/report/feedback。

### P2-03 Session/context 发布与结果消费缺少 CAS 幂等

- 证据状态：静态确定。
- 权威：Bridge session/snapshot 必须匹配且结果只消费一次。
- 证据：`src/Application/StrategicBattleBridge/StrategicBattleActiveContextStore.cs:5-30` 是进程级 begin/clear store；`src/Infrastructure/Scenes/SceneTransitionRouter.cs:117-131` 写入/失败清理；`WorldSiteRoot.BattleRuntime.cs:860-862` 以布尔和全局 clear 消费。结果命令只用 expedition duplicate guard，未校验期望 session/snapshot revision。
- 触发/影响：重复发布、旧回调或重入可覆盖/清除非预期 context，结果缺与期望 snapshot 的 compare-and-set。
- 测试漏检：单线程顺序 fixture，没有旧 context/重复回调交错。
- 修复与验证：按 context ID + session ID + snapshot ID + revision 做 CAS；交错测试旧回调、重复返回、场景失败和重试。

### P2-04 Runtime 启动未证明 actor 初始 footprint 互不重叠

- 证据状态：静态确定的缺失防线。
- 权威：导航 topology 的初始 placement/occupancy 必须完整合法。
- 证据：`BattleRuntimeSession.cs:235-315` 逐 group 在同一 placement 建 hero/corps；启动代码归一化 footprint，但未在该边界做 actor 间全局 overlap 拒绝；部署辅助虽在 `WorldSiteDeploymentService.cs:226`、`WorldSiteBattleDeploymentPreparer.cs:426` 预留覆盖格，却不能替代 Runtime 最终校验。
- 触发/影响：适配器、测试或旧请求提供重叠 placement 时，Runtime 从非法 occupancy 起步。
- 测试漏检：主要测试合法 fixture，未直接构造跨组和 hero/corps footprint 重叠。
- 修复与验证：Runtime 启动建立 covered-cell 集合并显式拒绝；覆盖同格、边缘、不同尺寸、不同高度。

### P2-05 Typed skill snapshot 是“暴露但多数未执行”的假配置契约

- 证据状态：静态确定的未来内容风险；不是已证明当前玩家缺陷。
- 权威：`battle-content-progression-architecture.md` 要求定义、Snapshot、Runtime、UI 同源。
- 证据：当前 snapshot 暴露 channel/range/area/cost/cooldown/charge/refund/mark/channel-follow 等字段；`BattleRuntimeHeroSkillCommandResolver.cs:151,369-386` 仅校验很小子集，P1-09 还证明 channel 未对比。当前 authored 值多与硬编码行为一致，所以不能宣称现有内容已经错误。
- 触发/影响：未来内容修改这些字段时，编辑器显示可配而 Runtime 仍执行硬编码。
- 测试漏检：fixture 使用与默认硬编码一致的值。
- 修复与验证：先建立 supported-field gate，未支持字段在编译时失败；再逐项参数化和做变异测试。

### P2-06 战术观察存在静态性能与所有权风险

- 证据状态：静态确定风险；无 Profiler 毫秒证据。
- 权威：战术事实应事件驱动 dirty/lazy rebuild，不得每 tick 全局重建。
- 证据：`src/Runtime/Battle/BattleTacticalObservationUpdater.cs:40,196,231,277,461` 多次读取全量 `TacticalStates`；`src/Runtime/Battle/Tactics/BattleCombatZoneBuilder.cs:29-30` 分配两个 `bool[n,n]` 并执行成对扫描；`src/Runtime/Battle/BattleRuntimeState.cs:30` 的 getter 调用 `CaptureSnapshots()` 深拷贝。
- 触发/影响：actor/group 增长时产生 O(n²) 扫描、矩阵分配和重复深拷贝，且观察器易变成隐性全局协调器。
- 测试漏检：无 Profiler、无规模基准；逻辑回归只看结果。
- 修复与验证：先加计数器与 Profiler 基线，再事件 dirty、版本化只读 snapshot、局部重建；用 3/10/30 组规模对比。

### P2-07 `demo_site` 的无效地图创作仅警告并继续

- 证据状态：静态确定；已有运行日志观察。
- 权威：Site Map Layout 要求无效高度连接和重复 marker ID 失败验证。
- 证据：`src/Presentation/Battle/GridMapReader.cs:154-217` 丢弃无效 link；`src/Application/Maps/SemanticMapMarkerExtractor.cs:41-50` 对 deployment duplicate ID 不形成硬失败；`src/Presentation/World/Sites/WorldSiteRoot.SiteMapPresentation.cs:241-251` 只告警。2026-07-11 日志反复记录“楼梯落在缺失高度 surface”和重复 deployment marker ID 后继续。
- 触发/影响：加载当前 `demo_site`；作者错误被保留为运行时降级，导航/部署事实与场景意图不一致。
- 测试漏检：测试把“有 warning 且继续”视为可接受，没有消费方硬门。
- 修复与验证：导入/场景启动前聚合验证并阻断需要该事实的消费者；修正内容后 headless import/scene smoke。

### P2-08 异步动画与技能 FX 存在节点离树后续执行风险

- 证据状态：静态生命周期风险，未观察崩溃。
- 权威：Godot 生命周期要求 await 后复验实例与 tree；Presentation 不得在释放节点上继续操作。
- 证据：`UnitAnimationComponent` 未在 tree exit 失效 one-shot/defeat generation，部分 await 路径缺 `IsInstanceValid/IsInsideTree`；`BattleSkillImpactFx` await 后直接 `QueueFree`，而同级 FX 已采用 generation + 实例/tree guard。
- 触发/影响：场景切换、战斗清理或节点在动画等待中被释放，可能产生 stale continuation/对象已释放错误。
- 测试漏检：C# runner 不实例化 PackedScene，也不覆盖 scene exit。
- 修复与验证：统一 generation cancellation 与 await 后 guard；Godot 场景 smoke 中在动画/FX 中途切场景和清理。

### P2-09 多个静态热路径需要 Profiler 决定优先级

- 证据状态：静态风险，不是已确认性能缺陷。
- 权威：UI dirty refresh 与 Runtime 热路径应避免每帧全量重建。
- 证据：`BattleUnitRoot.Movement.cs:45-63,326-350` 活跃帧 `ToArray`；`WorldSiteRoot.cs:420-435` 每帧 `UpdateSiteMapEntities`；`SiteMapPresentation.cs:282-315` 重同步 placements/occupants；`StrategicWorldRoot.Fog.cs:35-58,98-125` 重建 list、排序和 signature。
- 触发/影响：单位、地点、雾对象增长时形成分配/排序压力。
- 测试漏检：无 Profiler 和规模场景。
- 修复与验证：先记录调用数、分配和 frame time；只有热点成立才做 dirty/version cache。

### P2-10 Web workbench 复合保存不是整体原子事务

- 证据状态：静态确定。
- 权威：Strategic World Map Authoring 要求跨 chunk/派生产物不能部分提交。
- 证据：后续 tracked drift 增加了自然领地边缘采样及测试，但当前 `tools/world-map-workbench/src/server/artifacts.ts:130-134` 的 `compileRegionArtifacts` 仍通过 `Promise.all` 调用三次相互独立的 `atomicWriteFile` 写 mask/lookup/outlines；`src/client/WorkbenchApp.ts:823` 三端点并发 saveAll；`src/server/projectRepository.ts:60,105` 每文件 temp+rename 与 terrain batch rollback 是正面证据，但没有覆盖跨三类产物整体 commit。
- 触发/影响：三次并发中的部分成功、部分失败会留下混合版本 artifact set。
- 测试漏检：测试覆盖单文件原子写和 terrain batch rollback，未覆盖复合提交中途故障。
- 修复与验证：生成到 staging generation，校验齐套后一次切换 manifest/generation pointer；对每一步故障注入。

### P2-11 测试门不能代表真实 Godot 行为

- 证据状态：已观察验证结果 + 静态项目形状。
- 权威：工作项要求行为级四流程、Godot 场景/资源和失败语义验证。
- 证据：八个 C# 工程均 `OutputType Exe`，solution 仅含主项目与 `TargetBattleArchitectureRegression`；七个 runner 不在 solution；测试无 `PackedScene.Instantiate`，大量 `File.ReadAllText`/源码路径断言。已出现删除文档、移动工具和 partial 拆分导致的假失败。
- 触发/影响：重构文件位置会红，真实场景/生命周期/资源错误可能绿。
- 测试漏检原因即发现本身。
- 修复与验证：统一 runner/solution，保留少量架构 guard，但主线转为 DTO 行为、四流程集成和 Godot headless import/scene smoke。

### P2-12 日志写入同步且无保留上限

- 证据状态：静态确定；日志体积已观察。
- 权威：低噪诊断不能反过来形成运行时 I/O 压力。
- 证据：`src/Infrastructure/Logging/GameLog.cs:162` 每行 `File.AppendAllText`；无 retention/size cap；日文件已达约 8-9 MB。Trace suppression 是正面证据。
- 触发/影响：长时间运行或高频 warning 时同步小写入累积；磁盘持续增长。
- 测试漏检：无日志吞吐/轮转测试。
- 修复与验证：有界队列批写、每日/大小轮转与保留天数；压测前后比较日志丢失、主线程耗时和磁盘上限。

### P2-13 VS-01 启动没有当前目标

- 证据状态：静态确定。
- 权威：VS-01 要求启动即明确“派战斗编组进攻 Bonefield”。
- 证据：`src/Application/World/StrategicWorldRuntime.cs:13` 的 `LastNotice` 初始为空；`StrategicWorldRoot.UiBootstrap.cs:135` 绑定 NoticeLabel；`StrategicWorldRoot.DetailHud.cs:27` 直接显示该值；启动链没有设置 Bonefield 当前目标，只有 reset `StrategicWorldRuntime.cs:32` 的泛化文案。
- 触发/影响：新玩家首次进入主场景；目标提示为空或泛化，不能完成 VS-01。
- 测试漏检：无从 main scene 启动的 UI 行为测试。
- 修复与验证：由 slice/campaign objective view model 提供当前目标；main scene smoke 读取实际 Label 文本。

### P2-14 结果状态重复镜像掩盖一致性错误

- 证据状态：静态确定。
- 权威：Bridge Active Context 是单一 carrier，不得有平行结果权威。
- 证据：ActiveContext 同时有直接结果字段和 `FlowResult` 镜像；`WorldSiteBattleGroupRuntimeAdapter.cs:172-176` 重建 FlowResult；消费端存在 direct-or-FlowResult fallback。该 fallback 能隐藏两者不一致。
- 触发/影响：任一路径只更新一侧；消费者静默选择另一侧，错误延迟到结算。
- 测试漏检：fixtures 通常只填一种形状，fallback 使其通过。
- 修复与验证：一个不可变结果 envelope；迁移期对双值不等直接失败并记录 ID。

### P2-15 Nullable 与编辑器防线不足

- 证据状态：已观察构建警告 + 静态配置。
- 权威：Godot C# 节点/资源边界应显式处理可空与生命周期。
- 证据：`rpg.csproj` 未启用 Nullable，`.editorconfig` 仅定义 charset；已完成 build 产生 23 个 nullable warnings。
- 触发/影响：可空契约靠约定，节点/反序列化/桥接路径的空值风险不易在编译期收敛。
- 测试漏检：构建允许 warning。
- 修复与验证：分批启用 Nullable、建立 warning baseline，先覆盖新代码和边界 DTO，不一次性噪声淹没。

### P3-01 意图图标目录缺失，全部降级问号

- 证据状态：静态确定；C# literal 扫描唯一缺失项。
- 权威：资源 taxonomy 要求 stale path 显式失败，Presentation 应有可读意图。
- 证据：`src/Presentation/Battle/Intents/BattleIntentIcons.cs:19-35` 指向从未存在的目录；`BattleIntentMarker.cs:48-52` 加载失败统一退化问号。
- 触发/复现：任一当前 `BattleIntentMarker` 应用已知 intent icon key 时，都会尝试缺失的基目录并回退为问号。
- 影响：所有 intent icon 丧失区分；不影响战斗真值。
- 修复方向：迁移到已创作 `resource/ui/icons`，或明确删掉未实现图标契约。
- 验证：静态资源扫描并在场景 smoke 中逐一显示当前 intent key，确认不再走问号 fallback。

### P3-02 永久 StrategicWorld overlay 在代码中创建且 guard 漏检

- 证据状态：静态确定，低优先级。
- 权威：Presentation UI 与 Godot 资源化原则要求永久 UI 使用 authored scene/resource。
- 证据：StrategicWorld dynamic/site-name overlay Controls 由代码创建，现有 resource-authoring guard 未覆盖 `StrategicWorldRoot`。
- 触发/复现：`StrategicWorldRoot` 启动时，authored scene 缺少永久 overlay child，Root 会在代码中创建该 `Control`；当前 resource-authoring regression 不扫描此 Root。
- 影响：样式、布局、生命周期和回归 guard 分散。
- 修复方向：先创作 overlay 场景，再扩展 guard；不能只加 guard 而无替代资源。
- 验证：实例化 authored scene，确认永久 child 已存在且启动不再创建替代 `Control`；guard 必须覆盖 `StrategicWorldRoot`。

### P3-03 Root owner 过大但不应先机械拆 partial

- 证据状态：静态确定。
- 权威：scene/UI 单职责与单一运行时责任。
- 证据：`WorldSiteRoot` 21 partial/8375 行；`StrategicWorldRoot` 17 partial/6098 行；oversized gate 新发现 `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs` 1634 行和 `StrategicManagementRules.cs` 1088 行。
- 触发/复现：任何跨领域 feature/fix 落入任一 Root，或落入 oversized rules/test owner 时，都需要同时触碰混合责任；partial 数量本身不是缺陷。
- 影响：责任边界难审计，但 partial 数本身不是缺陷。
- 修复方向：先移除兼容和不可达责任，再按 owner/data flow 提取服务，不做机械 partial 切分。
- 验证：以跨领域变更的触点和 owner/data-flow 边界为准，用行为测试证明提取前后职责与行为稳定。

### P3-04 战略内容仍大量编码在 C# factory

- 证据状态：静态确定，迁移前置较高。
- 权威：内容应通过 definition/config 创作。
- 证据：`FirstStrategicManagementDefinitions` 与 StateFactory 编码 locations、rewards、equipment、heroes、initial resources/control。
- 触发/复现：新增或修改 strategic locations、rewards、equipment、heroes、initial resources、control 或 save schema 时，必须修改 C# factory。
- 影响：内容变体需改代码，stable ID 与存档迁移难治理。
- 修复方向：先定义 schema、稳定 ID 校验和 save migration，再迁移；不能直接删除 factory。
- 验证：用 definition/config 完成上述各类内容变更而无需 C# factory 修改，并覆盖稳定 ID 与旧存档迁移。

### P3-05 测试基础设施结构重复

- 证据状态：静态确定。
- 权威/契约：本活动审查契约要求 practical reproducibility、统一 behavior-level coverage，并显式检查重复 test runners/helpers；项目单一权威路径和可维护性规则适用。
- 证据：8 个 Program runner、至少 62 个重复 test helpers；大量源码字符串/文件路径断言。
- 触发/复现：重构文件/路径，或在另一 runner 增加同一 assertion/fixture pattern 时，会产生重复维护或结构性假失败。
- 影响：测试维护成本和假失败高。
- 修复方向：统一 runner 与共享行为 fixture；保留必要的隔离测试缝接口。
- 验证：同一行为断言/fixture 只维护一份；文件/路径重构不再触发无行为变化的失败，统一 runner 仍覆盖原有行为面。

## 六、只保留为假设的事项

| 假设 | 静态线索 | 不能升级为确认项的原因 | 定向验证 |
| --- | --- | --- | --- |
| 大 footprint covered-cell 移动可能跨越缺失的 covered edge | placement 检查 covered nodes，但边迁移的每个 covered-cell edge 是否完整需逐路径证明 | 未构造反例，也未观察非法穿越 | 构造 anchor 可走但某 covered edge 缺失的 topology，逐步提交移动 |
| `AdvanceNextTick` 或大 delta 可能跳过 channel cadence | 该路径可能忽略 `NextTickAtSeconds` | 未观察漏 tick，具体 scheduler 语义需运行验证 | 以小 delta/大 delta/advance-next 三组比较 channel tick 时间序列 |

## 七、正面证据

- Runtime 同 tick effect/damage 使用稳定 commit buffer 和确定顺序；对立双方同 tick 结果不受遍历顺序影响。
- 目的地信标多选执行原子验证，且 flow-field cache key 包含 beacon/topology/height/profile；失败不部分修改已选组。
- tactical pause 不推进移动、攻击、cooldown、perception 或模拟时间。
- 无效 handoff 显式失败，不伪造胜负。
- 结果/结算已有 session、snapshot、event boundary 检查及 duplicate guard；问题在于边界仍不完整，而不是完全没有防线。
- Strategic request 已不再进入 `WorldBattleResultApplier`，表明旧结果写回的一条主路径已切断。
- Web workbench 有 Zod 校验、路径策略、每文件原子写、terrain batch rollback 和 TypeScript strict；P2-10 是跨产物事务缺口，不否定这些防线。
- 静态资源扫描 1970 个直接 `res://` 引用为 0 missing；C# literal 仅 intent-icons 基目录缺失。
- 战斗真值代码未发现 `Random`、`DateTime` 或 `Guid`，有利于确定性。
- 日志已有 Trace suppression，避免一部分高频噪声。

## 八、权威覆盖矩阵

| 权威 | 实现覆盖 | 正面证据 | 缺口/冲突 | 相关发现 |
| --- | --- | --- | --- | --- |
| 长期内容系统设计 | 部分 | 英雄领军、实时 Runtime、战略资源/出征骨架 | 编组基数、报告后果、命令集不完整 | P1-03、P1-08、P2-02 |
| First Playable VS-01..16 | 部分 | VS-02..09、VS-11 的组件多数存在 | VS-01、07 预备队、10、12 回写显示、13-16 解释链 | P1-01、04、08、P2-02、13 |
| Hero-led RTS 总架构 | 冲突 | Snapshot/Runtime/Event 分层存在 | 旧 request 反向成为权威；actor 碎裂 commander | P1-02、03、07 |
| Battle Runtime | 部分 | 固定 tick、commit ordering、pause | group state 镜像、初始 overlap、观察热路径 | P1-07、P2-04、06 |
| Navigation Topology | 部分 | beacon cache、footprint 邻接和显式 topology | 初始目的地 launch gate 不完整；一个 covered-edge 假设待证 | P2-01、假设 1 |
| Battle Command | 冲突 | beacon Runtime 校验强 | Application validator 未接入、普通命令误送 skill resolver、越权技能 | P1-08、09 |
| AI/Tactical Intent/Region | 部分 | group tactical store、combat/action zone 概念存在 | per-actor state 镜像与全局重建风险 | P1-07、P2-06 |
| Result & Settlement | 冲突 | 结果完整性/重复 guard 存在 | 预备队阻塞、结果摘要贫化、保存原子性缺失 | P1-01、06、P2-02 |
| Content & Progression | 部分 | typed Resource/Snapshot 链已搭建 | 多数字段未执行，形成假配置契约 | P2-05、P3-04 |
| Strategic Management | 冲突 | command-only mutation 主体存在 | 地图仍读旧状态；rollback station 丢失 | P1-04、05 |
| Strategic Battle Bridge | 冲突 | session/context/snapshot/result 类型齐全 | 兼容请求重建最终 Snapshot，CAS/参与者状态不足 | P1-01、02、P2-03、14 |
| Scene Transition Router | 基本覆盖 | typed route、失败 clear、单 root switch | context store 无 revision CAS；真实场景 smoke 缺失 | P2-03、11 |
| Presentation UI | 部分 | HUD suppression、authored HUD 方向明确 | UI 直连 Runtime；永久 overlay 代码创建 | P1-09、P3-02 |
| Site Map/Markers | 冲突 | pure-data extractor 与 topology 分层存在 | 当前无效 stairs/重复 marker 只 warning | P2-07 |
| Resource Taxonomy | 大体覆盖 | 1809 scene/resource/project 扫描 0 missing direct ref | intent icon C# 基目录缺失 | P3-01 |
| Strategic World Authoring | 部分 | Zod、路径策略、单文件原子、terrain rollback | 三产物/saveAll 非整体事务 | P2-10 |
| Emotion | 架构存在，产品接入弱 | 独立状态与显式结果 | 约 61 文件/3081 行、消费者很少；需产品决定 | Lean 矩阵 |

## 九、四条端到端流程矩阵

| 流程 | 当前权威/转换 | 成功证据 | 失败证据与事务边界 | 可观测性/测试缺口 | 结论 |
| --- | --- | --- | --- | --- | --- |
| 1. 战略定向与出征 | Strategic Management 创建 expedition；旧 WorldArmy 负责地图载体；到达后 Bridge session | 命名英雄/军团、地图行军、确认入口均有实现 | 创建时清 `HomeCityId`；Bridge 失败 Cancel 无 station，旧载体留 ID；地图分类仍读旧 site | 有低噪日志；缺创建后各故障点的跨系统 rollback 测试 | 不安全，P1-04/05 |
| 2. 战前准备与编译 | Bridge Session + Draft 应编译 Snapshot；现由 CompatibilityRequest 参与最终同步 | 部署 UI、formation、初始 beacon、至少一组启动骨架存在 | reserve 只从 request 删除；Session 仍含全部参与者；旧 request 重建 Snapshot；actor 基数放大；初始目的地只查存在/布尔 | 缺携带/部署子集、Draft 血缘、拓扑不可达、大 footprint 行为测试 | 不可验收，P1-01/02/03、P2-01/04 |
| 3. 实时命令与模拟 | Runtime fixed tick；beacon resolver；skill resolver；tactical store | same-tick commit 稳定、多选原子、pause 不推进、无随机源 | UI 绕过 Application；enemy skill 可接受；regroup/retreat 无入口；group 状态每 actor 镜像 | 回归偏 DTO/源码；无 Godot input/scene；无规模 Profiler | 核心可运行但授权与产品命令不完整 |
| 4. 结果、结算与继续 | Runtime Result/Event -> Bridge Summary -> Strategic command -> save -> context consume | session/snapshot/event/duplicate guard；不再走旧 ResultApplier | reserve 摘要缺失；胜利显示读旧状态；apply 先于非原子 save；summary 未完整消费 consequence facts | 缺 fault injection、返回地图、报告归因、重启恢复测试 | 状态耐久性和 VS-12..16 不安全 |

## 十、VS-01 至 VS-16 矩阵

| VS | 状态 | 证据 | 缺口/阻塞 |
| --- | --- | --- | --- |
| VS-01 | 缺失 | main scene 与 NoticeLabel 存在 | 启动 `LastNotice` 为空，无 Bonefield 当前目标；P2-13 |
| VS-02 | 部分实现 | Stronghold/资源/英雄/军团/出征 UI 路径存在 | 未做 PackedScene 行为验证；旧/新战略状态仍并存 |
| VS-03 | 部分实现 | 到达、聚焦/确认和 typed transition 路径存在 | Bridge 失败回滚破坏军团状态；P1-05 |
| VS-04 | 部分实现 | 1-3 个命名英雄及默认 corps 配置存在 | Runtime 编译把一个组放大；P1-03 |
| VS-05 | 部分实现 | WorldArmy 可视载体和行军存在 | 载体/expedition alias 双状态；失败 residue |
| VS-06 | 部分实现 | session 含双方、目标 location/objective 字段 | 未用真实场景确认玩家文案完整性 |
| VS-07 | 失败 | 部署、formation、initial beacon UI 存在 | 合法 reserve 阻塞结算；initial beacon readiness 不查完整可达性；P1-01/P2-01 |
| VS-08 | 部分实现，核心正面 | 多选共享 beacon 原子、pause command 不推进 | 生产绕过 Application validator；initial/live 校验边界不统一 |
| VS-09 | 部分实现 | 固定 tick、移动/伤害/败退事件和表现组件存在 | 无 Godot 场景 smoke；stale await 风险；性能未 profile |
| VS-10 | 缺失 | enum 有 Regroup | Controller 没有生产 resolver/UI 闭环；P1-08 |
| VS-11 | 部分实现 | 三个技能与 skill resolver/FX/事件骨架 | 敌军越权、channel 假契约、生命周期风险；P1-09/P2-05/08 |
| VS-12 | 失败 | Strategic command 能更新 location | 主地图颜色/目标分类读旧 WorldSiteState；P1-04 |
| VS-13 | 部分/未证 | termination 与 failure candidates 类型存在 | defeat/heavy-loss 的可行动解释、恢复/重启未做行为验证 |
| VS-14 | 部分/未证 | feedback record/hero reaction 数据入口存在 | 主摘要退化固定文本，未证明按参战英雄展示 |
| VS-15 | 部分/未证 | ownership/reward/readiness 字段存在 | consequence facts 未完整进入真实 summary/feedback |
| VS-16 | 部分/未证 | 三类 equipment sample 在内容/factory 中存在 | report-visible contributor 与 Bonefield reward 的闭环未证明 |

## 十一、代码精简与架构熵矩阵

| 类别 | 证据/规模 | 当前判断 | 安全移除或合并前置条件 | 权威替代方向 |
| --- | --- | --- | --- | --- |
| 死代码/资源 | AutoBattle 16 C#/899 行 + `WorldSiteAutoBattleAdapter` 70 行，无生产消费者且 runner 永久 disabled；`ResolveAutonomousCombat` 无调用；`WorldCorpsInstanceRow` scene/script 未用；五对未引用且 byte-identical StyleBox；legacy result builder 仅测试调用 | 独立候选最明确 | 再做动态实例化/resource path 审计；确认无 editor tooling/反射；先删完全独立者 | Runtime/Bridge 当前路径、共享 StyleBox 资源 |
| 精确重复 | 五对 byte-identical StyleBox | 可合并 | 更新全部 `.tscn/.tres` 引用并保留语义命名入口或统一主题 | `resource/ui/themes` 单一资源 |
| 结构重复 | skill snapshot deep copy 3 处；8 个 Program runner；至少 62 个重复 test helper | 应合并 | 先固定单一 Snapshot compiler 和统一 runner；行为覆盖不降 | Bridge compiler；共享 test support |
| 语义重复 | 两套战略 runtime；ActiveContext direct result + FlowResult；旧 request 重建 snapshot | 不能直接删，先切权威 | 完成 Strategic Management map read、Draft->Snapshot、单结果 envelope 后删除旧镜像 | Strategic Management + Bridge Active Context |
| 无意义中间层 | `BattleGroupSessionProbeService` 784 行及 legacy adapter 基本 test-only，正式 Root 留 probe null | 移到 test support 候选 | 动态调用审计；测试改用正式 Snapshot/Runtime；保留必要 test seam interface | 正式 Bridge/Runtime builder |
| 不可删中间层 | `BattleCommandApplicationValidator` | 明确不是删除候选 | 先接入生产 UI，再决定 API 简化 | Application 授权边界 |
| 过度抽象 | Emotion 约 61 文件/3081 行，消费者很少但仍是 Accepted；C# behavior tree 与未接线 LimboAI 并存 | 需产品/集成决定 | Emotion 必须先确认产品近期路线；AI 必须动态实例化/资源引用审计并选定一个执行边界 | Emotion authority；AI driver-first boundary |
| 兼容残留 | legacy World/Request/Result/Handoff 仍在生产主线；objective-planning UI 似不可达 | 当前不能直接删 | 完成 Bridge/Strategic cutover；确认 objective UI 无 scene/input 入口 | Bridge Draft/Snapshot/Result；destination beacon |
| 重复状态/缓存 | Expedition aliases vs Participants；actor group state；ActiveContext mirrors；`TacticalStates` getter 深拷贝 | 高收益但依赖权威收敛 | 先建立 canonical ID/revision 和只读 view；加不一致硬失败 | Strategic expedition、group tactical state、single result envelope |
| 臃肿 owner | WorldSiteRoot 21 partial/8375 行；StrategicWorldRoot 17 partial/6098 行；oversized gate 两个新违规文件 | 不做机械 partial 切分 | 先删兼容/不可达责任，再按 owner、input/output 提取 | Presentation binder、Bridge、Strategic services |
| 内容在代码 | `FirstStrategicManagementDefinitions`、StateFactory 编码 location/reward/equipment/hero/初始资源与控制 | 需要迁移，不可直接删 | schema、稳定 ID 校验、加载失败策略、save migration | `config/` + typed resources + definitions loader |
| 重复测试 | 8 runner、62+ helpers、source-string/file-path assertion | 应优先治理验证门 | 统一 solution/runner，迁移到行为 fixture；保留少量架构 guard | 行为级四流程 + Godot smoke |
| 无效防御 | direct-or-FlowResult fallback 隐藏不一致；无效地图 authoring 只 warning 并保留状态 | 应改为显式失败 | 先提供单一结果 envelope 和可执行内容修复路径 | Bridge invariant；layout validation gate |
| 明确拒绝的假删除候选 | test-seam interfaces；当前 live `WorldBattleRequestBuilder`/WorldArmy/legacy site cache；已发现/preview 的 resource/battle/units | 现在不能删 | 只有生产 cutover、动态/资源引用审计和行为验证完成后再判断 | 当前主线或明确适配边界 |

精简顺序必须服从风险：先独立死代码/完全相同资源；再 Snapshot、Result 和测试基础设施合并；再旧战略/双 Runtime/root responsibility；最后才是 Emotion、LimboAI 与内容迁移的产品决策。删除行数不是成功指标，单一权威和可独立验证才是。

## 十二、精确验证记录（仅记录，未重跑）

| 命令/检查 | 已完成结果 | 解释 |
| --- | --- | --- |
| `npm test -- --maxWorkers=2` | 12 files、32 tests passed，约 3.16s | Web workbench 单元测试在后续 drift 前通过；不验证后来的 `artifacts.ts` / `artifacts.test.ts` 修改，也不验证随后重新生成的三个派生领地产物 |
| `npm run typecheck` | passed | TypeScript strict/typecheck 在后续 drift 前通过；不验证后来的 `artifacts.ts` 修改或随后重新生成的三个派生领地产物 |
| `dotnet build rpg.sln -maxcpucount:2 -v:minimal` | success，0 errors、23 warnings，约 2.58s | solution 仅含主项目与 TargetBattleArchitectureRegression |
| TargetBattleArchitectureRegression | exit 1 | oversized gate：`WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs` 1634 行；`StrategicManagementRules.cs` 1088 行 |
| WorldSiteDeploymentCacheRegression | 279 pass、1 fail | 仍读取已删除 `design-proposals/active/README.md`；属于过时路径断言 |
| StrategicManagementRegression | 84 pass | 通过 |
| WorldArmyMovementRegression | 8 pass | 通过 |
| BattleHitFeedbackRegression | 110 pass、2 fail | UnitPreviewWorkbench 已移到 `tools/battle`，测试仍找 `scenes/tools/battle`；spotlight 已移到 `WorldSiteRoot.BattleRuntimeDestinationBeacon.cs`，测试断言旧文件结构 |
| GameCursorAnimationRegression | 1 pass | 通过 |
| AutoBattleRuntimeRegression | 14 pass | 通过，但生产 runner 永久 disabled，不证明生产消费 |
| StrategicRegionPreviewRegression | 2 pass 后未处理 missing-file failure | 测试时 `StrategicRegionOverlayChunk.tscn` 缺失；随后并发漂移，已排除 |
| 静态资源扫描 | 1809 scene/resource/project files；1970 direct `res://`；0 missing | 直接资源引用完整 |
| C# literal 扫描 | 743 files；68 literal `res://`；仅 intent-icons 基目录缺失 | P3-01 |

明确未执行：Godot headless、import、scene smoke、Profiler。八个 C# 测试项目均为可执行 runner 而非 `dotnet test`；七个不在 solution；无 `PackedScene.Instantiate`，且大量源码形状断言。因此，上表不能证明真实 Godot 场景启动、节点生命周期、输入路由、资源导入或性能。另因 npm test 与 typecheck 完成后 `artifacts.ts` 和 `artifacts.test.ts` 才发生 tracked drift，且三个派生领地产物之后又在 14:33:29 与 14:38:16 两次变化，上表的通过结果既不能证明自然领地边缘采样及其测试的当前版本正确，也不能证明精确生成产物正确；本次记录修正按约束未重跑命令。

## 十三、按依赖和风险排序的修复批次

### A. 先停止状态损坏

1. 修复 reserve settlement 的参与者集合和解锁语义。
2. 修复 battle-group -> actor 基数与伤亡分母。
3. 将 `BattleCommandApplicationValidator` 接入生产，封堵敌军/频道越权。
4. 建立 save durability、可重试 result idempotency 和 fault-injection 设计。
5. 修复 Bridge failure rollback station 与旧载体 residue。
6. 将主地图控制权/目标分类切到 Strategic Management。

### B. 建立一个权威

1. Bridge Draft 直接编译最终 Snapshot，禁止 legacy request 反向覆盖。
2. command/beacon/PlanState/objective/region 收敛为每 BattleGroup 一个 commander state。
3. 清除 ActiveContext direct/FlowResult 双结果和 expedition/participant 别名的不受控镜像。

### C. 完成产品与内容契约

1. 实现 regroup/retreat 的 Application -> Runtime -> event/report 闭环。
2. 对 preparation destination 做 walkability/footprint/reachability launch gate。
3. 让 result summary 消费 settlement/report consequence facts。
4. 建立 typed-skill supported-field gate，再逐项消除硬编码。
5. 补 VS-01 当前目标、intent icons、site-map authoring 硬验证。

### D. 建立可信验证门

1. 统一 runner/solution，减少源码字符串和路径断言。
2. 增加四流程行为级集成测试，尤其携带/部署子集和返回地图。
3. 增加 Godot headless import/scene smoke 与 PackedScene 实例化。
4. 增加 save/bridge/scene-transition 故障注入。
5. 用 Profiler 决定 tactical observation、每帧同步、fog、日志的优化优先级。

### E. 最后精简

1. 先删独立 AutoBattle、未用场景/脚本、完全相同 StyleBox 和 test-only legacy result builder。
2. 合并 Snapshot deep copy、Result carrier、runner/helper。
3. 完成 legacy World/Request/Result/Handoff 和双战略 runtime 切除，再减少 root owner。
4. 最后对 Emotion、LimboAI/C# behavior tree、内容迁移作产品/架构决定。

## 十四、剩余风险与独立验证入口

剩余最高风险是：报告仍基于静态审查和已提供的验证记录，没有 Godot 场景 smoke、I/O fault injection、并发 context 交错验证或 Profiler；Strategic Region Preview 仍是单独排除的移动目标；后续 `artifacts.ts` / `artifacts.test.ts` tracked drift 未被先前 npm test 与 typecheck 覆盖。三个命名派生领地产物的精确字节作为已知并发移动集合排除，后续仅限该集合的 hash 变化不推翻发现、P2-10、资源路径存在性扫描或既有验证记录，但其精确生成正确性仍未验证；集合外漂移仍须审查。P2-10 的当前源码证据仍成立，但自然领地边缘采样、其测试与派生产物正确性均无当前验证证据。新的独立验证者应核对方法标签、漂移处置、P2-07 引用和每项 P3 的完整字段，再确认授权写入边界；本报告本身不得被执行者标记为已独立验证。
