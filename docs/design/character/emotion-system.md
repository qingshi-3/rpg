# 情感与关系系统

本文记录大世界情感系统的稳定规则。它不是剧情文案库，而是 NPC、种族、阵营和玩家势力互动时的底层生成与查询规则。

## 核心判断

情感系统必须有两层：

```text
跨局稳定的种族 / 文化情感基调
+ 单局生成的 NPC 个体差异
```

种族基调提供长期可识别性。NPC 差异提供具体角色的新鲜感和故事空间。

召唤角色额外遵守一条规则：

```text
来源时空只提供展示和内容语境；情感系统只读取抽象 trait、relationship、bond、memory 和 modifier。
```

不要让情感规则按具体角色名、来源作品或来源时代写特殊判断。刘备和张飞的特殊性应来自 `结义`、`誓约`、高信任、高亲近和低怨恨等数据，而不是运行时代码识别“刘备 + 张飞”。

## 种族情感基调

每个种族应有稳定的情感倾向。无论玩家开几局，这个大基调都存在。

示例：

- 精灵：互助、守护、审慎、重视自然与长期承诺。
- 兽人：武勇、直率、重视力量与部族荣誉，但不等于默认见人就打。
- 人类：适应性强、利益判断重、内部差异大。
- 亡灵：执念、服从、恐惧或记忆残响，具体取决于亡灵来源。

这些基调不是绝对行为脚本，而是概率权重和解释框架。

种族本身不属于情感系统。种族定义属于角色 / 单位定义：

```text
CharacterRaceDefinition
  -> RaceEmotionProfileDefinition
  -> EmotionSystem
```

情感系统读取角色的 RaceId、角色属性、文化 / 阵营 / 职业修正 id，再生成情感状态。它不反向定义或拥有种族。

角色定义是可选适配层，不是情感生成的唯一入口。随机大世界中的 NPC 可以直接通过 `EmotionNpcGenerationRequest` 生成情感状态。

## NPC 个体差异

同一种族下的 NPC 可以偏离种族基调，但偏离应当有可解释的原因。

示例：

- 一个兽人可以非常善良、克制，甚至主动保护弱者。
- 一个精灵可以冷漠、傲慢，拒绝帮助外族。
- 一个亡灵可以保留生前记忆，因此对某个村庄或角色产生保护欲。

偏离不是为了反转而反转。它应该来自：

- 成长经历。
- 部族、家族或组织背景。
- 创伤、誓言、诅咒、异变或救赎。
- 与玩家势力或其他 NPC 的历史关系。

## 随机生成规则

NPC 生成不应直接随机一个完全独立的人格。推荐顺序：

```text
读取 CharacterDefinition
-> 读取 CharacterRaceDefinition
-> 读取 RaceEmotionProfileDefinition
-> 合并文化 / 阵营 / 职业 / 个人修正
-> 把角色属性映射为情感倾向微调
-> 生成个人差异
-> 生成关系初始值和记忆钩子
```

个人差异可以改变某些倾向，但不应完全抹掉种族基调，除非该 NPC 被标记为特殊角色。

随机个体差异不应由 NPC 名字或具体角色 id 决定。推荐输入是：

- `RaceId`：用于读取稳定种族情感基调。
- `ModifierIds`：文化、阵营、职业、个人模板、背景、异变、创伤等情感修正。
- `TraitInputs`：挂在单位 / NPC 上的直接情感属性，例如额外同情心、攻击性、纪律、好奇心。
- `RelationshipInputs`：初始关系输入，例如对玩家势力的信任或怨恨。
- `Seed` / `VariationKey`：由世界生成器提供，用于生成单局个体差异。

`ActorId` 只用于保存和查询运行态，不参与随机性格计算。这样同一套“情感个体模板”可以挂到不同 NPC 身上，而不会因为名字改变性格。

## 可查询维度

第一版不需要复杂人格模型，但至少应能查询：

- 对玩家势力的信任。
- 对危险和牺牲的态度。
- 对帮助弱者的倾向。
- 对荣誉、利益、秩序或自由的重视程度。
- 对其他种族或阵营的默认偏见。
- 是否存在关键记忆或关系钩子。

这些结果服务于事件、招募、据点互动、派遣任务、战斗支援和剧情分支。

## 系统边界

- 情感系统提供状态和查询结果，不直接写剧情文本。
- 剧情事件可以读取情感结果，并通过结构化结果改变关系或记忆。
- 大世界只触发互动和事件，不直接硬编码 NPC 情感变化。
- 战斗结果可以产生情感事件，但 Battle 不直接持有长期关系状态。
- 种族定义、角色定义、种族情感基调和 NPC 差异应走 Definition / 配置，避免硬编码在单个场景脚本里。
- 当情感系统需要其他系统概念时，只依赖对应系统的 Definition / enum，不实现其他系统内部逻辑。
- 关系称号和契约应作为 `BondDefinition` 一类数据定义，底层仍由 trust、affection、resentment、obligation 等关系数值驱动。

## 后台模块契约

情感后台分三层：

```text
Definitions/Characters
  CharacterDefinition / CharacterRaceDefinition
        |
Definitions/Emotion
  RaceEmotionProfileDefinition / EmotionProfileModifierDefinition
  EmotionEventDefinition / EmotionConditionDefinition / EmotionEffectDefinition
        |
Domain/Emotion
  EmotionWorldState / EmotionActorState / Relationship / Memory / Event
        |
Application/Emotion
  IEmotionSystem
```

Definition 层只描述数据和可配置规则；Domain 层只保存运行态情感状态并应用结构化事件；Application 层负责把角色定义、修正 id、属性、事件定义和查询条件连接起来。

情感系统读取：

- `CharacterDefinition.Race.Id`
- `CharacterRaceDefinition.BaselineAttributes`
- `CharacterRaceDefinition.DefaultEmotionModifierIds`
- `CharacterDefinition.CultureId`
- `CharacterDefinition.FactionId`
- `CharacterDefinition.ProfessionId`
- `CharacterDefinition.EmotionModifierIds`
- `RaceEmotionProfileDefinition`
- `EmotionProfileModifierDefinition`
- `EmotionNpcGenerationRequest.ModifierIds`
- `EmotionNpcGenerationRequest.TraitInputs`
- `EmotionNpcGenerationRequest.RelationshipInputs`
- `EmotionNpcGenerationRequest.Seed`
- `EmotionNpcGenerationRequest.VariationKey`

情感系统不拥有种族，不生成世界互动，不保存剧情文本，不接管招募、据点、战斗或存档。

## 事件、条件和效果

外部系统接入时应把结果转换为情感事件，而不是直接改 NPC 字段。

- `EmotionEventDefinition`：可配置的事件模板，包含条件和效果。
- `EmotionConditionDefinition`：判断 trait、relationship、disposition、memory tag 或 actor 是否存在。
- `EmotionEffectDefinition`：产生 trait delta、relationship delta 或 memory。
- `EmotionEvent`：运行态结构化事件，可由世界、剧情、招募、据点或战斗结果构造。

事件效果只改变情感运行态：

- trait：如同情心、秩序、自由、荣誉、贪婪。
- relationship：如信任、亲近、恐惧、尊重、怨恨。
- memory：结构化记忆钩子和 tag，供后续剧情或招募查询。

## 最小接入示例

```csharp
IEmotionSystem emotions = new EmotionSystem(emotionDatabase, runSeed);

EmotionActorSnapshot npc = emotions.GenerateActor(characterDefinition, seed).Value;

EmotionEvent rescued = new(
    "rescued_by_player",
    EmotionEventKind.WorldInteraction,
    npc.ActorId,
    "world_event:rescue",
    new[] { new EmotionTraitDelta(npc.ActorId, EmotionAxis.Helpfulness, 8) },
    new[] { new EmotionRelationshipDelta(npc.ActorId, "player_faction", EmotionRelationshipMetric.Trust, 15) },
    new EmotionMemoryState("rescued_by_player", "rescued_by_player", "被玩家势力救助", 20, new[] { "rescued", "player_faction" }));

emotions.ApplyEvent(rescued);

bool canRecruit = emotions.IsDispositionAtLeast(npc.ActorId, "player_faction", EmotionDisposition.Friendly)
    && emotions.HasMemoryTag(npc.ActorId, "rescued");
```

如果事件已经写成 Definition，可以通过 `ApplyEvent(eventDefinitionId, context)` 或 `ApplyEvent(eventDefinition, context)` 接入。`EmotionConditionContext` 提供本次事件的 subject、target、source 和 event id。

随机大世界 NPC 也可以绕开角色定义，直接输入情感数据：

```csharp
EmotionNpcGenerationRequest request = new(
    actorId: "orc_guard_17",
    displayName: "兽人守卫",
    raceId: "orc",
    modifierIds: new[] { "profession:guard", "scarred_but_kind" },
    seed: worldNpcSeed,
    isSpecial: false,
    disableIndividualVariance: false,
    traitInputs: new[] { new EmotionTraitDelta("", EmotionAxis.Empathy, 18) },
    relationshipInputs: new[] { new EmotionRelationshipDelta("", "player_faction", EmotionRelationshipMetric.Trust, -10) },
    variationKey: "orc_guard_kind_variant");

EmotionActorSnapshot generated = emotions.GenerateActor(request).Value;
```

这里 `actorId` 只是存取 id；最终性格来自种族基调、modifier、trait input、relationship input，以及 `seed + variationKey` 产生的随机微调。

长期状态由 `EmotionWorldState` 表达。需要交给存档或其他后台模块时使用 `ExportState()` 取得克隆；恢复已有状态时使用 `UseState(state)`。

## 设计风险

- 如果只有种族刻板印象，角色会变扁。
- 如果完全随机个体差异，种族和世界观会失去识别度。
- 如果情感只变成数值加减，它不会产生角色温度。
- 如果情感系统直接控制剧情和战斗，会破坏系统边界。
