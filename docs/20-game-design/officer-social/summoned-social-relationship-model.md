# 召唤角色与抽象关系模型

本文定义《异世界的召唤》中召唤角色、来源展示、社交属性和关系称号的稳定分层。

## 核心原则

```text
来源是叙事标签，关系与社交属性才是玩法事实。
```

系统不得写：

```text
if actor == 刘备 && target == 张飞
```

系统应写：

```text
if relation.kind == sworn_sibling
if relation.tags contains oath
if relation.metrics.Trust >= 80
```

三国、水浒、幕末和原创角色都只是给同一套抽象系统填初始数据。新世界中的事件负责改变这些数据。

## 分层

### OriginProfile

`OriginProfile` 只负责展示、内容分组和叙事语境。

示例：

- 三国
- 水浒
- 幕末
- 原创异世界

它可以影响：

- 角色面板显示。
- 召唤演出。
- 图鉴分类。
- 文案风味。
- 内容池筛选。

它不应直接影响：

- 战斗规则。
- 忠诚判断。
- 关系变化。
- 事件条件。
- AI 行为。

如果某个来源需要玩法差异，应把差异写成抽象 trait、relationship、bond、duty 或 definition，而不是在运行时代码里判断来源。

### SocialProfile

角色的社会属性和价值倾向应抽象表达。

示例维度：

- 重义
- 野心
- 谨慎
- 好战
- 仁厚
- 孤傲
- 复仇心
- 纪律性
- 共情
- 支配欲
- 厌恶背叛
- 尊重强者

这些属性供情感、事件、任命、招募、自动行动和关系变化查询。

### RelationshipMetrics

关系底层以方向性数值保存。

`A -> B` 和 `B -> A` 可以不同。

第一版可用维度：

- `Trust` / 信任
- `Affection` / 亲近
- `Resentment` / 怨恨
- `Obligation` / 亏欠或义务
- `Respect` / 敬重
- `Fear` / 畏惧
- `Loyalty` / 忠诚
- `Rivalry` / 竞争

事件和情感系统主要修改这一层。

### RelationshipKind

关系类型表达抽象语义。

第一版可用类型：

- `sworn_sibling` / 结义
- `blood_family` / 血亲
- `lord_vassal` / 主从
- `mentor_student` / 师徒
- `benefactor_debtor` / 恩义
- `rival` / 竞争者
- `enemy` / 仇敌
- `companion` / 同伴
- `old_acquaintance` / 旧识

系统判断应优先使用 `RelationshipKind`、tag 和 metrics，而不是角色 id。

### BondDefinition

关系称号或契约是对底层关系组合的命名和内容包装。

示例：

```text
BondDefinition: taoyuan_oath
DisplayName: 桃园结义
BaseKind: sworn_sibling
Tags: oath, brotherhood, legendary_bond

InitialMetrics:
  Trust: 100
  Affection: 100
  Resentment: 0
  Obligation: 90
  Loyalty: 85
  Respect: 90

StateRules:
  oath_shaken: Trust < 70 or Resentment > 30
  oath_broken: Trust < 35 and Resentment > 60
  enemies: Resentment > 80
```

`桃园结义` 不是硬编码逻辑，而是 `sworn_sibling + oath + 高信任 + 高亲近 + 低怨恨` 的著名内容实例。

原创内容也可以复用同一套抽象：

```text
BondDefinition: blood_moon_oath
DisplayName: 血月盟誓
BaseKind: sworn_sibling
Tags: oath, brotherhood
InitialMetrics:
  Trust: 85
  Affection: 70
  Obligation: 95
```

## 事件反应

事件应作用于抽象层。

示例：

```text
Event: abandon_ally_in_battle
Conditions:
  relation.kind == sworn_sibling
Effects:
  Trust -35
  Resentment +40
  Affection -20
```

```text
Event: rescue_from_capture
Effects:
  Trust +20
  Obligation +30
  Affection +10
```

事件可以来自：

- 世界互动。
- 战斗结果。
- 任命和晋升。
- 驻守、委托和区域治理。
- 召唤适应事件。
- 旧关系在新世界中的冲突。

## 内容示例

```text
Character: 刘备
OriginProfile: 三国
SocialProfile:
  traits: 仁厚, 重义, 号召

Character: 张飞
OriginProfile: 三国
SocialProfile:
  traits: 好战, 重义, 冲动

Relationship:
  from: 刘备
  to: 张飞
  kind: sworn_sibling
  bond: taoyuan_oath
  tags: oath, brotherhood
  metrics:
    Trust: 100
    Affection: 100
    Resentment: 0
    Obligation: 90
```

运行时系统只看到 `sworn_sibling`、`oath`、`Trust`、`Affection`、`Resentment` 等抽象信息。

## 系统边界

- 角色来源、人物名和历史背景只提供配置输入，不直接写进玩法代码。
- 情感系统修改 trait、relationship、bond state 和 memory，不直接操作世界或战斗内部状态。
- 世界、战斗、任命和事件系统通过结构化事件改变关系。
- UI 应优先展示玩家能理解和决策的关系状态，例如 `桃园结义：稳固 / 动摇 / 裂痕 / 义断`，底层数值用于解释和高级详情。
