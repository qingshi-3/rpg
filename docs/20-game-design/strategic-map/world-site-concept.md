# 战略地点 / WorldSite 命名约定

本文只定义长期命名边界。具体数据字段、行动、UI 和战斗合同继续看 Strategic World V1 系列文档。

## 术语

- 中文设计术语：战略地点；具体类型优先写城池、据点、资源点、关隘、遗迹、地牢或机会事件。
- 代码术语：`WorldSite`。
- 运行态：`WorldSiteState`。
- 定义态：`WorldSiteDefinition`。

战略地点是大世界上的持久运营对象。它可以是城池、据点、矿洞、墓园、关隘、遗迹、地牢、野外资源点或中立据点。只有核心管理点才使用完整城池系统。

## 核心定位

`WorldSite` 是本项目区别于纯战略节点和菜单据点的关键对象。

它应该同时满足三件事：

```text
大地图上可被发现、争夺、进入和影响。
小地图内可进行非战时经营、交互、部署和整备。
战时可承载英雄带兵轻 RTS 战斗或后台自动结算，并把结果回写到长期状态。
```

因此战略地点不是三群式薄节点，也不是只有经营按钮的据点 UI。新增地点内容时，应优先判断它是否需要长期状态和可进入空间；如果只是短期遭遇、伏击、救援或夺取，优先建模为 `Opportunity` / `EncounterRequest`，不要滥用 `WorldSite`。

## 为什么不用战斗场景

场域不是一次性战斗副本。场域需要长期保存：

- 控制权。
- 战时 / 非战时模式。
- 建筑和设施槽位。
- 驻军。
- 本地资源和可抢物资。
- 损毁状态。
- 已死亡或已移除的重要 NPC / 敌人 / 机关。
- 敌方威胁和入口状态。

战斗只是场域进入战时后的执行方式之一。战斗结束后，结果必须回写到 `WorldSiteState`。

## 和 Godot 场景的边界

Godot 的 `.tscn` 仍然可以叫 scene。以下命名不改：

- `SiteScenePath`
- `ReturnScenePath`
- `SceneFilePath`
- `WorldSiteRoot`
- `ChangeSceneToFile`

这些是引擎加载和运行壳概念，不是设计层的“场域”。后续文档如果讨论大世界上的可运营对象，应使用“场域 / WorldSite”；只有讨论 Godot 资源、节点树或 `.tscn` 时才使用“场景”。

## 场景目录约定

当前世界层场景按职责分开：

```text
scenes/world/StrategicWorldRoot.tscn
  战略大地图、世界推进、场域选择、敌军移动

scenes/world/sites/WorldSiteRoot.tscn
  大世界可接入的场域入口；负责加载具体场域详细地图，并承载战时 / 非战时模式

scenes/world/sites/impl/
  具体场域详细地图实现，例如 BonefieldSite.tscn（埋骨地）
  这里是具体场景制作目录；由 sites/WorldSiteRoot.tscn 加载，后续接入大世界地图入口。

scenes/world/site_interactions/
  场域详细交互点的场景或资源占位，具体逻辑后续补充
```

对应脚本目录：

```text
src/Presentation/World/StrategicWorldRoot.cs
src/Presentation/World/Sites/
src/Presentation/World/SiteInteractions/
```

不要把场域详细地图放回 `maps` 子目录，也不要恢复旧的自由探索场域目录。

## V1 关系

```text
StrategicWorldDefinition
  -> WorldSiteDefinition[]

StrategicWorldState
  -> SiteStates: Dictionary<SiteId, WorldSiteState>

WorldSite
  非战时：通过小地图交互点和必要 UI 做建造、驻军、资源、威胁处理
  战时：生成 BattleStartRequest，交给 WorldSiteRoot 执行
  战后：BattleResult 回写 WorldSiteState
```

## 制作分层

不是每个 `WorldSite` 都需要完整手工关卡。

```text
普通场域：通用地图模板 + 少量设施槽位 / 事件 / 战斗入口。
关键场域：手工地形、重要 NPC、设施布局、战斗目标和持久对象记忆。
玩家核心场域：允许更完整的经营、驻军部署、修复、升级和长期变化。
```

场域制作应优先复用 `WorldSiteRoot`、设施槽位、交互点场景和通用行动定义。不要为单个场域复制一套专属运行逻辑。

## 场域模式切换

第一版固定四种模式：

```text
Peacetime -> Alert -> Wartime -> Aftermath -> Peacetime
```

- `Peacetime`：非战时经营。
- `Alert`：敌方威胁已生成或接近，玩家可以做战前准备。
- `Wartime`：触发战斗后进入 `WorldSiteRoot`，暂停完整经营行动。
- `Aftermath`：战斗或自动结算刚回写，下一次世界步后回到 `Peacetime`；如果仍有威胁则回到 `Alert`。

模式切换由 `WorldSiteModeTransitionService` 统一处理，并写出 `SiteModeChanged` 事件。
