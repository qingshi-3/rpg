# 三国群英传参考架构指导

本文定义本项目后续开发在需求不完整、效果描述不充分或系统边界不清时的默认参照。

参照对象是本机只读检查过的 Unity 工程：

```text
C:\Users\qs\Desktop\Unity3D三国群英传完整源码
```

该工程不作为代码来源、素材来源或 IP 模板。它在本项目中的价值是提供一个已经闭环的战略战役产品结构：

```text
战略世界状态
-> 玩家选择势力、场域或部队行动
-> 部队移动、城市/场域变化、遭遇生成
-> 战斗入口
-> 战斗结算
-> 世界、角色、资源、势力状态回写
-> 下一轮战略选择
```

当后续任务没有明确说明实现效果时，默认目标不是自由发挥新玩法，而是优先补齐上述闭环。最低保底效果应当达到“像三群一样能把战略层、角色/部队、战斗和结算串起来”，再在项目自身方向上做差异化。

## 关系定位

本项目与三群的关系是：

```text
三群是战略战役骨架参考。
不是产品题材目标。
不是代码风格参考。
不是可复用资产库。
不是战斗体验上限。
```

当前产品方向是“大地图战略 + 人物社交经营 + 回合制战棋”的战略 RPG。三群式结构在本项目中的职责是提供大地图战略骨架：势力、场域、部队移动、遭遇、战斗入口和结果回写。玩家身份应服务于战略、人物经营和战棋三层玩法，不应被实现文档提前锁死。

可以学习：

- 大地图不是装饰，而是持久状态容器。
- 城市/场域、君主/势力、武将/角色、军团/部队都用数据维护。
- 玩家行动会创建部队、改变地图、触发遭遇。
- 战斗不是孤立副本，战斗结果会回写世界。
- 剧本数据让同一套运行时代码承载多套开局。
- 内政、出征、选将、战斗、存档形成完整产品链路。

必须摒弃：

- 直接复用三国题材、人物、城池、武将技、图像、音频或文本。
- 复刻老 Unity 的全局单例堆状态。
- 把大量规则写进场景控制器。
- 用数组下标和魔法数字表达长期规则。
- 一种技能一个硬编码脚本，缺少统一效果模型。
- 战略层和战斗层直接互相改内部状态。

## 三群源码中的产品骨架

只读检查显示，该 Unity 工程不是单纯素材包，而是一个具备完整战略战役结构的工程。

### 场景链路

三群工程的主要场景：

```text
StartScene
ContinueGame
HowToPlay
SelectKing
InternalAffairs
Strategy
SelectGeneralToWar
WarScene
GameVictory
GameOver
CheatMaker
```

它表达的是清晰的产品流：

```text
开始/读档
-> 选择剧本与君主
-> 内政整顿
-> 战略地图
-> 选择目标或部队
-> 战前选将
-> 战斗
-> 战果回写
-> 返回战略地图或结束
```

后续本项目做任何大世界、场域、战斗入口、角色队伍或据点功能时，都应检查是否落在这个链路中。不能只完成一个孤立界面或一场孤立战斗。

### 数据规模与运行态

三群工程中稳定存在这些运行对象：

```text
KingInfo       势力/君主状态
CityInfo       城市状态
GeneralInfo    武将状态
ArmyInfo       出征军团状态
EquipmentInfo  装备状态
MagicDataInfo  武将技配置
```

剧本 XML 负责初始化：

```text
MOD01..MOD05
King:    不同开局势力
City:    48 个城市的归属、人口、金钱、预备兵、防御
General: 255 个武将的归属、位置、技能、装备、属性、兵种、阵型
```

本项目不需要复制这些字段，但应继承它的结构思想：

```text
开局定义只描述初始状态。
运行态承载变化。
战斗消耗请求。
结算返回结果。
世界状态保存变化。
```

### 战略地图职责

三群的 `Strategy` 层承担：

- 地图浏览。
- 城市旗帜与归属显示。
- 玩家选择城市或军团。
- 部队在地图上移动。
- 部队遭遇部队。
- 部队攻击城市。
- AI 城市行动和军团行动。
- 月推进、资源增长、恢复、征兵。
- 战斗发生前的对话和切场景。
- 胜利和失败检测。

本项目战略世界层也必须承担同等级别的“战役状态编排”责任。战略层不能退化成一个进入战斗的菜单。

### 内政职责

三群 `InternalAffairs` 层承担：

- 使用物品。
- 查看武将。
- 招降俘虏。
- 搜索人才。
- 筑城。
- 开发。
- 势力图。
- 武将升迁。
- 结束内政。
- 存档。

本项目不需要逐项复刻，但场域经营 / 整备系统应保留同类产品价值：

```text
玩家在战斗外做准备。
准备改变后续战略和战斗条件。
准备不是纯菜单，而是战役节奏的一部分。
```

### 战斗职责

三群 `WarScene` 层承担：

- 双方主将。
- 士兵和骑兵生成。
- 小兵自动寻找目标、前进、后退、撤退、死亡。
- 主将移动、攻击、受击、死亡、撤退。
- 武将技释放和范围伤害。
- 战斗结束判断。
- 战斗结果写回武将、兵力、俘虏、城市或军团。

本项目战斗形态是回合制网格战棋，不应照搬三群的实时小兵冲杀。但战斗必须保留三群式产品责任：

```text
战斗消耗来自世界的角色、部队、设施、情报和修正。
战斗产生可写回世界的结果。
战斗不是独立演示场。
```

## 本项目的默认抽象

三群概念应映射为本项目的中性抽象，而不是照搬命名。

| 三群概念 | 本项目默认抽象 | 说明 |
|---|---|---|
| 城市 | `WorldSite` / 场域 | 持久、可操作、可争夺的世界节点。 |
| 君主 / 势力 | `Faction` / 玩家势力 / 敌对势力 | 控制权、敌友关系、胜败归属。 |
| 武将 | `Character` | 长期角色身份，进入战斗后生成 `BattleUnit`。 |
| 军团 | `Party` / `WorldArmy` / `Expedition` | 在世界层移动、驻守、出征或遭遇。 |
| 内政 | `WorldSiteOperation` / `WorldAction` | 战斗外准备、建设、训练、侦察、互动。 |
| 武将技 | `AbilityDefinition` + `Effect` | 不使用一技能一专属流程。 |
| 剧本 MOD | `RunDefinition` / `StrategicWorldDefinition` | 一局战役的初始状态和规则集合。 |
| 战斗结果 | `BattleResult` | 回写世界、角色、资源、设施、威胁。 |
| 存档 | `RunSaveState` | 保存运行态，不保存纯展示临时状态。 |

后续命名优先使用本项目稳定术语，尤其是：

```text
WorldSite / 场域
Character
BattleUnit
Faction
WorldAction
BattleStartRequest
BattleResult
Run
```

不要把可运营场域称为城市、战斗场景或普通 Scene。

## 默认开发目标

当需求只说“做大世界”“做场域”“做战斗入口”“做出征”“做据点”“做角色队伍”而没有更详细效果时，默认必须满足以下目标。

### 1. 有运行态，不只是配置

配置定义负责描述初始内容：

```text
WorldSiteDefinition
CharacterDefinition
FactionDefinition
FacilityDefinition
WorldActionDefinition
EncounterDefinition
BattleMapDefinition
```

运行态负责记录变化：

```text
WorldSiteState
CharacterState
FactionState
PartyState
FacilityInstance
ThreatPlanState
RunState
```

如果一个系统只读 Definition，不保存 State，通常还没有达到三群式战略战役骨架。

### 2. 有行动，不只是查看

场域或据点界面至少要能承载 `WorldAction`：

```text
查看状态
-> 选择行动
-> 校验条件
-> 消耗资源或时间
-> 应用效果
-> 推进 WorldTick 或产生战斗请求
```

仅显示场域介绍、按钮跳转或静态 UI，不算完成战略层能力。

### 3. 有出征或遭遇入口

世界层需要能产生战斗入口：

```text
玩家主动出征
敌方 Raid 到达
野外遭遇
场域争夺
Boss 巢穴挑战
事件触发战斗
```

入口应统一转成 `BattleStartRequest`，而不是让具体场景直接加载具体战斗。

### 4. 有战斗回写

战斗结束必须生成可消费结果：

```text
BattleResult
  Outcome
  Casualties
  CapturedOrRescuedCharacters
  ResourceChanges
  SiteControlChanges
  FacilityChanges
  ThreatChanges
  RelationshipEvents
  Unlocks
```

世界层消费结果后改变运行态。战斗层不直接改世界内部对象。

### 5. 有节奏闭环

每完成一次重要行动，默认应考虑是否推进：

```text
WorldTick
敌方威胁倒计时
资源产出
角色恢复或疲劳
设施生产
事件刷新
可见情报变化
```

三群用年月推进和 AI 月行动维持战役节奏。本项目可以更轻，但不能让世界永远静止。

## 推荐运行流

后续默认采用下面的顶层链路：

```text
RunStart
-> Load StrategicWorldDefinition
-> Create RunState
-> Enter StrategicWorld
-> Player selects WorldSite / Party / WorldAction
-> Resolve WorldAction
-> Maybe create EncounterRequest
-> Maybe create BattleStartRequest
-> Enter Battle
-> Produce BattleResult
-> Apply BattleResult to RunState
-> Advance WorldTick
-> Present changed world state
-> Continue or end run
```

这个链路比具体界面更重要。界面可以临时，链路不能断。

## 数据驱动规则

三群工程已经把剧本、武将技等内容部分放进 XML，但仍有大量硬编码。我们要保留“数据承载内容”的方向，去掉“硬编码承载规则”的问题。

### 应数据化

- 场域定义。
- 场域初始归属。
- 资源类型。
- 建筑和槽位。
- 世界行动。
- 行动条件。
- 行动消耗。
- 行动效果。
- 威胁规则。
- 战斗入口。
- 战斗地图引用。
- 角色基础身份和战斗能力。
- 能力、卡牌、规则、意图和效果。
- 奖励池和事件池。

### 应通用化

三群里具体武将技脚本很多。本项目应把能力拆成：

```text
AbilityDefinition
  Costs
  TargetRules
  Conditions
  Effects
  Presentation
```

只有确实无法用已有效果表达的新机制，才新增通用 `Effect`、`Condition` 或 `TargetRule`。不要为某个剧情、某个场域、某张卡、某个 Boss 技能写一次性流程。

### 可暂时硬编码

在早期验证中，可以短期硬编码：

- 原型入口按钮。
- 临时测试数据。
- Debug 命令。
- 单个演示地图的摆放。

但只要该内容要进入正式闭环，就必须迁移到 Definition 或统一运行时合同。

## 战略世界设计规则

参考三群时，优先学习它的世界状态密度，而不是三国地图形状。

### 场域必须有状态

一个可运营场域至少应能表达：

```text
SiteId
OwnerFaction
ControlState
Resources
Facilities
Garrison
Entrances
Threats
DamageLevel
Tags
LastChangedTick
```

如果一个场域没有控制权、资源、设施、驻军、威胁或战斗入口中的任何一类变化，它更像普通地点，不应承担 `WorldSite` 职责。

### 部队必须有目的

三群军团不是地图装饰，它们有来源、目标、位置、将领、金钱、武将、俘虏和状态。

本项目部队/远征至少应表达：

```text
PartyId
OwnerFaction
SourceSiteId
TargetSiteId
Members
GarrisonUnits
CargoResources
WorldPosition
Route
State
Intent
```

部队状态至少包括：

```text
Idle / Garrisoned / Moving / Intercepting / Attacking / Retreating / Defeated
```

### 敌方行动必须可预期

三群有 AI 月行动和军团移动。本项目不做完整 4X AI 时，也要至少有敌方威胁计划：

```text
EnemyThreatPlan
  SourceSiteId
  TargetSiteId
  CountdownTicks
  EnemyGroupId
  VisibleIntelLevel
  Stage
  Consequence
```

玩家需要能围绕威胁做选择：

```text
加固目标
驻军
主动拦截
侦察
提前打源头
选择放弃或承担后果
```

## 战斗设计规则

三群战斗是实时冲杀，本项目战斗是回合制战棋。差异不影响架构要求。

默认战斗必须满足：

- 有来自世界的上下文。
- 有明确目标，不只清空敌人。
- 有敌人意图或计划。
- 有地形、空间或机制影响。
- 有战斗后果。
- 有可读的结算。

战斗开始请求应包含：

```text
BattleStartRequest
  EncounterId
  SourceSiteId
  BattleKind
  PlayerParty
  EnemyGroup
  MapDefinition
  SiteModifiers
  FacilityModifiers
  IntelLevel
  VictoryConditions
  RetreatRules
```

战斗结果应避免只返回胜负：

```text
BattleResult
  Outcome
  CompletedObjectives
  FailedObjectives
  SurvivingUnits
  LostUnits
  InjuredCharacters
  ResourceDelta
  SiteStateDelta
  FacilityDelta
  ThreatDelta
  EventHooks
```

## UI 与反馈规则

三群的 UI 价值在于玩家始终知道自己处于哪一层：

```text
内政
战略
选目标
选将
战斗
结算
```

本项目后续 UI 不应只追求好看，必须表达决策信息。

战略 UI 至少要回答：

- 当前我控制什么。
- 每个场域发生了什么变化。
- 我有哪些可执行行动。
- 行动需要什么成本。
- 行动会改变什么。
- 敌人下一步可能做什么。
- 进入战斗会带入哪些修正。

战斗 UI 至少要回答：

- 现在是谁的阶段。
- AP 还剩多少。
- 敌人意图是什么。
- 我方规则驱动单位会做什么。
- 当前行动影响哪些格子和目标。
- 战斗胜负条件是什么。
- 结算将怎样影响世界。

## 存档与调试规则

三群的存档保存君主、城市、武将、军团、时间和地图位置。这个方向是正确的：存档应保存战役运行态，而不是只保存当前场景。

本项目存档默认保存：

```text
Run metadata
Random seed
WorldSiteState[]
FactionState[]
CharacterState[]
PartyState[]
FacilityInstance[]
ThreatPlanState[]
ResourceStore
WorldTick
Important event history
```

调试工具可以参考三群 `CheatMaker` 的产品价值，但不能污染正式运行逻辑。调试工具应通过正式接口修改状态，以便暴露合同问题。

## 反跑偏规则

当一个任务没有说清楚时，按下面顺序判断，不要自行发明新的主玩法。

### 先问它属于哪一层

```text
Run
World
WorldSite Operation
Character
Story Event
Battle Prep
Battle
Reward
Save / Debug
```

如果找不到归属，不要直接实现成孤立脚本。

### 再问它如何进入闭环

必须能回答至少一个：

- 它改变哪个运行态。
- 它提供哪个行动。
- 它创建哪个战斗入口。
- 它改变哪个战斗条件。
- 它消费哪个战斗结果。
- 它推进哪个威胁、事件或世界 Tick。

如果都回答不了，它大概率只是装饰或临时演示。

### 默认补齐三群式链路

如果用户只说“做一个场域”，默认不是只做地图节点，而是：

```text
WorldSiteDefinition
WorldSiteState
可见 UI
至少一个 WorldAction
至少一个状态变化
必要时一个 BattleEntrance
保存/加载可承载该状态
```

如果用户只说“做一个敌人据点”，默认不是只摆怪，而是：

```text
敌方控制的 WorldSite
威胁或 Raid 来源
可被侦察或攻击
战斗入口
战斗胜利后的控制权或威胁变化
```

如果用户只说“做出征”，默认不是只切战斗，而是：

```text
选择来源场域
选择目标场域
选择角色/部队
支付成本或占用人口
创建 Party 或 BattleStartRequest
结算后回写来源与目标
推进 WorldTick
```

如果用户只说“做一个技能”，默认不是写一个专属脚本，而是：

```text
AbilityDefinition
TargetRule
Condition
Effect
Preview
战斗日志/反馈
测试用例
```

## 验收清单

后续跨系统功能完成前，用这份清单自检。

- 是否有 Definition 和 Runtime State 的区分。
- 是否避免把具体内容写死在流程脚本里。
- 是否通过请求/结果跨越 World 与 Battle 边界。
- 是否让战斗结果回写世界或角色状态。
- 是否让世界准备改变战斗条件。
- 是否有可见的玩家决策成本和后果。
- 是否有敌方计划、威胁或世界推进，而不是静态地图。
- 是否能保存关键运行态。
- 是否没有引入三群式全局单例堆状态。
- 是否没有为了单个内容写一次性硬编码系统。
- 是否保持 `WorldSite` 等项目稳定术语。

如果一项功能没有通过这些问题，它还没有达到“三群战略战役骨架”的保底标准。

## 一句话约束

后续开发默认目标：

```text
先做出三群式战略战役闭环的骨架，
再把题材、战斗、角色关系和机制表达换成本项目自己的方向。
```

不能反过来先做孤立玩法、孤立 UI、孤立战斗，再期待它们以后自然拼成一局战役。
