# 情感玩法查询契约

本文记录情感系统向其他玩法系统暴露的应用层查询结果。它不是招募、派遣、战斗支援、恋爱或剧情系统本体，只负责把情感状态转换成可消费的结构化判断。

## 定位

情感系统的职责是回答：

- 这个 NPC 对玩家势力是否愿意进一步接触。
- 这个 NPC 执行某类大世界任务时，情感状态会提高还是降低效率。
- 这个 NPC 当前是否存在忠诚风险。
- 这个 NPC 是否愿意提供战斗支援。
- 某个社交关系门槛是否满足。
- 某个事件大概率会引发正向、负向还是中性反应。

情感系统不负责：

- 决定最终招募成功率。
- 执行任务派遣。
- 修改战斗流程。
- 生成剧情文本。
- 判断恋爱或羁绊的内容合法性与具体剧情。

这些系统应把自己的业务条件传入 query，再消费 result 中的情感修正值、门槛结果和解释因子。

## 查询入口

`IEmotionSystem` 提供以下玩法侧查询：

- `EvaluateRecruitment(EmotionRecruitmentQuery query)`
- `EvaluateTaskAssignment(EmotionTaskAssignmentQuery query)`
- `EvaluateLoyalty(EmotionLoyaltyQuery query)`
- `EvaluateBattleSupport(EmotionBattleSupportQuery query)`
- `EvaluateRelationshipGate(EmotionRelationshipGateQuery query)`
- `EvaluateEventReaction(EmotionEventReactionQuery query)`

这些接口只读取 `EmotionWorldState`，不会修改情感状态。状态变化仍然必须通过 `EmotionEvent` 或 `EmotionEventDefinition` 进入系统。

## 输出语义

招募输出：

- `CanRecruit`
- `RecruitmentChanceModifierPercent`
- `BlockReasonCode`
- `MissingRequiredMemoryTags`
- `ActiveBlockingMemoryTags`
- `Factors`

派遣输出：

- `CanAssign`
- `EfficiencyModifierPercent`
- `LoyaltyRiskDelta`
- `BlockReasonCode`
- `Factors`

忠诚输出：

- `RiskScore`
- `RiskLevel`
- `DesertionChanceModifierPercent`
- `Factors`

战斗支援输出：

- `CanSupport`
- `SupportChanceModifierPercent`
- `MoraleModifierPercent`
- `BlockReasonCode`
- `Factors`

关系门槛输出：

- `Passed`
- `Score`
- `RequiredScore`
- `BlockReasonCode`
- `MissingRequiredMemoryTags`
- `ActiveBlockingMemoryTags`
- `Factors`

事件反应输出：

- `Score`
- `Tone`
- `RelationshipDeltaPreview`
- `Factors`

`Factors` 是调试和后续 UI 解释用的低噪声拆分项。它们是稳定 id 和数值，不是玩家可见文案。

## 外部依赖

情感系统只依赖外部系统的公开枚举，不实现外部系统内部逻辑。

当前新增的外部定义：

- `WorldTaskKind`：大世界任务类型枚举，供派遣情感修正使用。

后续如果接入据点、经济、阵营、剧情或关系系统，也应优先新增轻量 definition / enum，而不是让情感系统反向依赖具体业务实现。

## 评分原则

评分不是最终业务判定。它只提供情感侧倾向：

- `Relationship` 提供对目标的信任、亲近、恐惧、尊重和怨恨。
- `Trait` 提供角色个体倾向，例如同情心、攻击性、勇气、秩序、自由、忠诚。
- `Disposition` 提供综合关系态度。
- `MemoryTag` 提供结构化历史钩子。
- `Difficulty`、`Danger`、`Pressure`、`Risk` 等输入由调用系统提供。

调用方可以把情感结果与资源、技能、职业、任务难度、剧情条件、世界状态等其他系统结果合并，得出最终玩法结果。

## 接入示例

```csharp
EmotionOperationResult<EmotionRecruitmentResult> result =
    emotions.EvaluateRecruitment(new EmotionRecruitmentQuery(
        actorId: "orc_guard_17",
        recruiterFactionId: "player_faction",
        difficulty: 25,
        offerValue: 10,
        requiredMemoryTags: new[] { "rescued" },
        blockingMemoryTags: new[] { "betrayed_by_player" }));

if (result.Success && result.Value.CanRecruit)
{
    int chanceModifier = result.Value.RecruitmentChanceModifierPercent;
}
```

这个结果只表示情感侧允许招募，并给出概率修正。最终是否招募成功仍由招募系统结合其他规则决定。
