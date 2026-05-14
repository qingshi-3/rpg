# 战略大世界 V1 待确定点

本文只记录实现主链路时仍需后续内容制作或设计确认的点。不得把这里当成替代实现的理由；V1 主链路仍以 `../../20-game-design/strategic-map/strategic-world-v1.md` 和实施文档为准。

## 战斗地图入口与设施锚点

当前系统已经在 `BattleStartRequest` 中传递入口、驻军、设施快照和 `BattleModifier`。

待确定：

- 埋骨地 TileMap 的正式入口位置。
- `bonefield_main_entrance`、`bonefield_defense_post`、`bonefield_north_tower` 等锚点在正式地图中的坐标。
- 多入口部署在最终 UI 中是战前选择，还是进入战斗后部署。

V1 处理：

- 战斗仍使用现有 `WorldSiteRoot.tscn` 和教程地图。
- 驻军民兵会在防守战中动态加入。
- 防御塔支援先表现为开场压制伤害，后续可替换成地图塔对象或主动支援技能。

## NPC 与情感系统接入

当前世界行动、战斗回写、资源、驻军、设施和威胁都会产生 `GameEvent`。

待确定：

- 哪些 NPC 订阅 `SiteControlChanged`、`GarrisonChanged`、`ThreatStageChanged`。
- NPC 对“守住据点”“据点受损”“驻军损失”的关系变化公式。
- 情感系统提示是在战略 UI 中直接显示，还是进入角色面板显示。

V1 处理：

- 主链路先只发事件，不做深度情感消费。
- 情感系统后续通过事件订阅接入，不直接写入世界行动服务。

## 持久化粒度

当前 V1 保存 `StrategicWorldState`，包括资源、场域、建筑、驻军、威胁和 WorldTick。

待确定：

- 战斗进行到一半时是否允许保存。
- 具体战斗地图内对象破坏是否需要跨战斗保存到 TileMap 级别。
- 后续多存档槽位和自动存档策略。

V1 处理：

- 非战时战略世界可手动保存 / 读取。
- 战斗结果回到世界后再保存世界状态。
- 不保存战斗中间帧和 Godot 节点状态。
