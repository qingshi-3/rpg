# 战略大世界 V1 实施计划

本文把 V1 拆成可实现阶段。每个阶段都应能独立验证，不要等所有系统完成后才第一次跑通。

## 总体顺序

```text
Domain state
-> Definitions
-> Application services
-> Strategic UI
-> Battle handoff
-> Battle result writeback
-> Facility battle modifiers
-> Enemy Raid
-> Persistence
```

不要先做美术地图，也不要先做完整建筑 UI。先让状态和服务可测。

## 当前实现状态（2026-05-02）

第一版主链路已经跑通：

- `StrategicWorldState`、资源、场域状态、建筑实例、驻军、威胁、世界步已实现。
- V1 definitions 已覆盖三资源、三建筑、四场域、基础行动和墓园 Raid。
- `WorldActionResolver`、`WorldTickService`、`WorldThreatService`、`WorldBattleResultApplier` 已实现第一版。
- `StrategicWorldRoot` 已能展示战略地图、资源、场域详情、行动和威胁。
- `BattleStartRequest` / `BattleResult` 已接入战斗启动和结果回写。
- `WorldSiteRoot` 已替代旧 `BattleRoot` 作为场域运行壳，战斗子系统仍保留 `Battle*` 命名。
- 战斗结束后不再强制退出场域，而是切换到 `WorldSiteRoot` 的非战时经营 UI。
- 非战时 UI 第一版包含资源栏、右侧经营面板、地图上的可点击建筑点、驻军/威胁/行动列表。
- `WorldClock` 第一版已接入战略地图：未暂停时自动推进 `WorldTick`，敌方到达 `Attacking` 时自动暂停。
- `StrategicWorldRoot` 已提供 `WorldMapRoot`、场域锚点、场域视觉层和战略导航根；可在其下配置正式 TileMap。
- `StrategicWorldRoot` 已能从 `SiteVisualLayer` 和 `MapAnchors/Sites/<site_id>` 构建场域视觉 footprint，用于大地图点击热区、选中描边和标签位置。
- 大地图抽象蓝黄角标已移除；英雄数量和兵团数量角标等待正式 UI / 图标资源后再接入。
- 墓园 Raid 会生成敌方 `WorldArmyState`，并通过 `StrategicNavigation` 沿可通行地表移动，抵达后在目标场域显示进攻状态。

当前仍未完成：

- 正式战略大地图 TileMap 需要制作和配置。
- 后续新增或调整场域时，`MapAnchors/Sites` 仍需要落在 `SiteVisualLayer` 对应场域素材内部，并保持不同场域素材在该层断开。
- 真实 site 地图的建筑 slot anchor 需要手工制作和配置。
- 建造、拆除、升级、修复还需要从按钮列表升级为地图 tile 上的明确交互流程。
- 战斗地图中的 NPC 死亡、物件破坏等细粒度持久化还未建模。
- 敌方 AI 仍是 V1 tick-based Raid，不是完整势力 AI。
- 需要补一轮手动验收和可能的自动测试。

## 阶段 1：Domain 状态

建议新增：

```text
src/Domain/World/ResourceStore.cs
src/Domain/World/ResourceReservation.cs
src/Domain/World/StrategicWorldState.cs
src/Domain/World/WorldSiteState.cs
src/Domain/World/MapSurfaceState.cs
src/Domain/World/FacilityInstance.cs
src/Domain/World/GarrisonState.cs
src/Domain/World/EnemyThreatPlan.cs
```

最低实现：

- `ResourceStore` 支持 add、can spend、spend、reserve、release。
- `StrategicWorldState` 保存 tick、玩家资源、场域状态、威胁状态、威胁。
- `WorldSiteState` 保存控制权、建筑、驻军、tag。

验收：

- 可以在无 Godot 场景的情况下创建初始世界状态。
- 可以读写人口、经济、石材。
- 可以添加建筑实例。
- 可以添加驻军。

## 阶段 2：Definitions

建议新增：

```text
src/Definitions/World/ResourceDefinition.cs
src/Definitions/World/FacilityDefinition.cs
src/Definitions/World/FacilitySlotDefinition.cs
src/Definitions/World/WorldSiteDefinition.cs
src/Definitions/World/MapSurfaceDefinition.cs
src/Definitions/World/WorldActionDefinition.cs
src/Definitions/World/StrategicWorldDefinition.cs
src/Definitions/World/ThreatRuleDefinition.cs
```

最低实现：

- 用 Resource 或普通 C# definition 都可，优先贴合项目现有 Godot Resource 模式。
- 定义三资源、三建筑、四场域、基础行动。
- Definition 只描述数据，不执行逻辑。

验收：

- 可以从一个 definition 创建初始 `StrategicWorldState`。
- 新增资源或建筑不需要改 `StrategicWorldState` 字段。

## 阶段 3：Application 服务

建议新增：

```text
src/Application/World/StrategicWorldService.cs
src/Application/World/WorldActionResolver.cs
src/Application/World/WorldActionResult.cs
src/Application/World/WorldTickService.cs
src/Application/World/WorldThreatService.cs
```

最低职责：

- 初始化 V1 世界。
- 查询场域可用行动。
- 校验行动条件。
- 应用行动成本和效果。
- 推进 WorldTick。
- 生成和推进 Raid。

验收：

- 执行 `build_mine` 后经济减少、人口被占用、埋骨地出现矿场。
- 执行 tick 后矿场产出石材。
- 执行 `build_defense_tower` 后埋骨地出现防御塔。
- 执行 `train_militia` 后人口和经济减少，驻军增加。

## 阶段 4：战略地图 UI

建议新增：

```text
scenes/world/StrategicWorldRoot.tscn
src/Presentation/World/StrategicWorldRoot.cs
src/Presentation/World/StrategicSiteButton.cs
src/Presentation/World/StrategicSitePanel.cs
src/Presentation/World/StrategicActionPanel.cs
src/Presentation/World/StrategicThreatPanel.cs
```

最低能力：

- 显示玩家营地、埋骨地、墓园和可行走地表。
- 顶部显示人口、经济、石材、WorldTick。
- 选中场域显示状态、建筑、驻军、威胁。
- 行动按钮由 action view model 生成。

验收：

- 不进入战斗也能完成建矿场、建防御塔、训练民兵、等待 tick。
- UI 能显示行动不可用原因。

当前状态：

- 已完成第一版 `StrategicWorldRoot`。
- 场域内经营已开始落到 `WorldSiteRoot`，不再只依赖战略地图右侧面板。
- `WorldSiteRoot` 非战时 UI 已接入 `assets/textures/ui/basic-ui/2/` 的统一皮肤层，用于顶部栏、右侧经营面板和按钮。

## 阶段 5：BattleStartRequest

建议新增或扩展：

```text
src/Application/Battle/BattleStartRequest.cs
src/Application/Battle/BattleEntranceRequest.cs
src/Application/Battle/BattleForceRequest.cs
src/Application/Battle/BattleModifier.cs
src/Application/World/WorldBattleRequestBuilder.cs
```

最低实现：

- 从己方场域出征创建的 `AssaultSite` 小队抵达后生成 `BattleStartRequest`。
- 通过 handoff 进入现有 WorldSiteRoot。
- 保持兼容现有 `contextId + encounterId + returnScenePath`。

验收：

- 从 StrategicWorldRoot 选中己方场域，点击出征并右键埋骨地后，部队抵达能进入战斗。
- 战时启动主要依赖 `BattleStartRequest`；非战时经营态可以读取 `StrategicWorldRuntime` 来展示和执行 site 行动。

## 阶段 6：BattleResult 回写

建议新增或扩展：

```text
src/Application/Battle/BattleResult.cs
src/Application/Battle/BattleObjectiveResult.cs
src/Application/World/WorldBattleResultApplier.cs
```

最低实现：

- 当前战斗胜利映射为 `occupy_bonefield` 成功。
- 当前战斗失败映射为攻占失败。
- 战斗结束留在当前 site，并切换为非战时经营态；玩家可手动返回战略地图。

验收：

- 胜利后埋骨地变 `PlayerHeld`。
- 失败后埋骨地仍为 `Hostile`，玩家经济损失。
- 战斗结束推进 1 WorldTick。

当前状态：

- 已完成第一版。
- 非战时经营 UI 通过 `GameUiSkin` 统一使用 `basic-ui/2` 面板和按钮素材，代码不直接在业务逻辑里散落具体 PNG 处理。
- 战斗结束后的默认表现已从“返回战略地图”改为“留在当前 site，进入非战时经营态”。

## 阶段 7：建筑影响防守战

最低实现：

- 埋骨地有防御塔时，防守 request 包含 `tower_support`。
- 埋骨地有民兵时，防守 request 包含 militia force。

如果 Battle 暂时不能完全消费 modifier：

- 可先在 result applier 或自动结算中体现防御塔价值。
- 但 request/result 字段必须存在。

验收：

- 防御塔存在时，防守战 request 可见 modifier。
- 民兵存在时，防守战 request 可见 force。

## 阶段 8：墓园 Raid

最低实现：

- 埋骨地被玩家占领后，下一次 tick 生成 Raid。
- Raid countdown 从 3 递减。
- 到达后显示 Attacking 状态。
- 玩家可进入防守战或自动结算。

验收：

- 玩家有足够时间建防御塔或驻军。
- 自动结算会根据防御塔和驻军改变结果。
- 防守战胜利清除 Raid。
- 防守失败让埋骨地 Damaged 或 Lost。

## 阶段 9：持久化

建议新增：

```text
src/Application/World/StrategicWorldSaveService.cs
```

最低保存：

- `DefinitionId`
- `WorldTick`
- 玩家资源。
- 场域控制权。
- 建筑实例。
- 驻军。
- 威胁倒计时和阶段。

验收：

- 保存后重载，埋骨地仍保持控制权。
- 建筑和驻军不丢失。
- Raid 倒计时不重置。

## 阶段 10：清理旧原型入口

当前状态：

- 已完成清理。旧静态战役地图、旧 overmap、旧自由探索场域原型不再作为主流程入口。
- 旧 `BattleRoot` 已被 `WorldSiteRoot` 替代。
- 场域详细地图目录固定为 `scenes/world/sites/`，当前示例为 `BonefieldSite.tscn`（埋骨地）。
- 通用场域运行壳固定为 `scenes/world/sites/WorldSiteRoot.tscn`。
- 场域详细交互点占位目录为 `scenes/world/site_interactions/` 和 `src/Presentation/World/SiteInteractions/`。

后续新增场域时：

- 只在 `sites/` 下增加具体场域详细地图。
- 交互点逻辑放入 `site_interactions/` 或对应脚本目录。
- 不恢复旧 `maps` 子目录、旧自由探索场域目录或旧 overmap 主入口。

## 阶段 11：场域经营态

目标：

- 把 `WorldSiteRoot` 做成战时 / 非战时可切换的场域壳。
- 非战时不是结算弹窗，而是可操作基地。
- 地图仍可作为交互面，不被全屏 UI 遮罩变成背景。

最低实现：

- 战斗结束后留在 `WorldSiteRoot`。
- 隐藏战斗 HUD 和战斗单位层。
- 显示非战时资源栏和经营面板。
- 建筑槽位先在经营面板中显示状态，不生成地图 Button。
- 后续需要地图交互时，建筑点使用 `scenes/world/site_interactions/` 下的场景实体并绑定 `SlotId`。
- 行动按钮继续来自 `WorldActionViewModel`。
- 执行行动后刷新资源、建筑、驻军、威胁。

当前状态：

- 已完成第一版。
- marker 位置暂由 `BattleAnchorId` / slot tag 映射到临时 grid cell。

后续：

- 将 marker 位置替换为手工 site map anchor。
- 加入建造、拆除、升级、修复交互。
- 加入场域对象持久化。

## 工程风险

### 风险：UI 先行导致逻辑写死

规避：

- 先写 Domain 和 Application。
- UI 只消费 ViewModel。

### 风险：BattleResult 仍只有胜负

规避：

- 先引入结构化 result，即使第一版只填 outcome。
- result applier 不直接读 WorldSiteRoot。

### 风险：建筑行为写死

规避：

- 所有建筑都从 `FacilityDefinition` 读取成本、效果、modifier。
- 兵营、矿场、防御塔不建专属 service。

### 风险：WorldTick 变成实时压力

规避：

- 只在玩家行动或战斗后推进。
- Attacking 阶段必须停下来让玩家处理。

## 实现完成定义

V1 完成需要满足：

- 玩家可以通过战略地图占领埋骨地。
- 玩家可以使用人口、经济、石材建造或训练。
- 埋骨地能产出石材。
- 墓园能生成 Raid。
- 防御塔和驻军能改变 Raid 或防守战。
- 战斗结果能回写埋骨地状态。
- 世界状态能保存和恢复。
- 旧的世界路线文档不再作为实现入口。
