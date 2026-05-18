# Godot 人工配置友好指南

本文档沉淀 Godot 编辑器内人工配置的长期规则，指导单位、地图 Marker、场景模板、Resource 定义等内容如何做成可配置、可检查、可维护的形态。

它不替代 `gameplay-design/` 和 `system-design/` 的权威架构；当本文档和权威文档冲突时，以权威文档为准。

## 核心目标

人工配置应该让内容作者在 Godot Inspector 里完成大部分内容生产，而不是频繁改运行时代码。

推荐模型是：

```text
少量稳定通用代码
-> 清晰的 Resource / Scene / Marker 配置入口
-> 明确的校验和诊断
-> Runtime 只消费纯数据或快照
```

编辑器友好不等于把规则藏进编辑器脚本。Inspector、`@tool` / `[Tool]`、预览节点、动态属性列表只负责降低配置成本；战斗、世界、结算、AI、存档等运行时权威仍在对应系统中。

## 总原则

- 优先用 Godot 原生配置面：`.tres`、`.tscn`、`SpriteFrames`、`PackedScene`、`Theme`、`Shader`、`NodePath`、`Resource` 引用。
- 优先静态导出字段，再考虑动态 Inspector。普通字段用 `[Export]`、`[ExportGroup]`、枚举、范围提示和资源引用即可。
- 当字段只在某个模式下有意义时，才考虑动态属性列表或工具节点隐藏复杂度。
- 人工配置入口必须有稳定 ID、清晰中文显示名、可读分组、合理默认值和失败诊断。
- Scene 负责可视结构和可交互锚点；Resource 负责内容事实；Runtime 负责解释这些事实。
- 不为了一个具体单位、一个具体 Marker、一个具体剧情点新增专属运行时代码。
- 不把编辑器预览节点当成运行时事实来源。运行时只消费提取后的纯数据、快照或定义资源。

## Inspector 友好写法

### 静态导出优先

适合长期稳定字段：

- `Resource` 定义类加 `[GlobalClass]`，方便在 Inspector 中直接创建和引用。
- 用 `[ExportGroup]` 把字段按作者理解分组，例如“生命”“行动”“攻击”“战斗占位”“自动布局”。
- 用枚举表达有限选项，例如 Marker 类型、目标模式、方向模式。
- 用 `PropertyHint.Range` 限制数值范围，避免作者输入无效尺寸、速度、透明阈值。
- 用 `Resource` / `PackedScene` / `NodePath` 引用代替散落字符串路径。

### 动态 Inspector 只解决真实复杂度

AdvancedExports 这类方案的价值在于：当一个资源的字段会随模式变化时，让 Inspector 只展示当前模式真正需要的字段。

适合动态属性的场景：

- 同一个 Marker 类型不同，才需要不同字段。
- 同一个能力定义支持多种目标模式，只有某些目标模式需要方向、范围或区域字段。
- 同一个可视资源支持自动布局和手动布局，手动字段只在关闭自动布局时才需要突出。

不适合动态属性的场景：

- 只是为了少显示几个稳定字段。
- 字段间有运行时规则依赖，应该由校验器或 Runtime 解释。
- 需要频繁扫描资源、改场景树、写文件或触发导入。

如果使用动态属性列表，需要同时满足：

- 属性变化只影响编辑器显示，不改变运行时权威。
- 模式字段变化后调用属性列表刷新。
- 默认值可独立成立，隐藏字段不会变成不可见的运行时陷阱。
- 复杂逻辑有低噪声诊断或校验入口。

### 工具脚本要克制

`[Tool]` 节点适合做编辑器预览、网格吸附、可视化区域和一次性辅助按钮。它不适合承载业务规则。

工具脚本规则：

- 所有编辑器逻辑都要先判断编辑器环境。
- `_Process` 中只做极轻量刷新；昂贵计算要缓存、按需执行或用显式按钮触发。
- 不在 `_Process` 中写资源、改大量子节点或触发导入。
- 任何自动吸附、自动布局、自动修复都必须可解释，失败时保留作者输入并输出诊断。
- 工具节点退出运行时后不能留下额外运行时事实。

## 单位配置

当前单位不是“一单位一场景”。新单位的目标配置链路是：

```text
assets/battle/units/<faction>/<unit-folder>/unit.tres
-> BattleUnitDefinition
-> BattleUnitVisualDefinition
-> SpriteFrames
-> BattleUnitFactory
-> scenes/battle/entities/units/BattleUnitBase.tscn
```

`BattleUnitBase.tscn` 是唯一完整战斗单位组件场景。它提供共享的 `BattleEntity`、`VisualRoot/AnimatedSprite2D`、碰撞、选择、血量、移动、攻击、能力、动画、音频和表现组件。

每个单位的差异应放在资源里：

- `unit.tres`：单位 ID、中文名、数值、占位、可选能力、控制方式、音频和可视定义。
- `visual.tres`：`SpriteFrames`、动画集、自动布局参数、少量手动布局兜底。
- `frames.tres`：`idle`、`move`、`attack`、`hit`、`defeated` 等动画帧。
- ability `.tres`：攻击、技能、目标规则和效果。

`scenes/battle/entities/units/Militia.tscn`、`PlayerKnight.tscn`、`BoneSkeletonWarrior.tscn`、`BoneSkeletonArcher.tscn` 这类旧场景是 legacy visual sketch，不是新单位权威配置入口。

### 新增单位检查

1. 在 `assets/battle/units/` 的种族目录下创建单位包目录。
2. 放入 PNG、`.import`、`frames.tres`、`visual.tres`、`unit.tres`。
3. `unit.tres` 的 `Id` 使用稳定 ASCII ID；`DisplayName` 使用中文语义名。
4. `Visual` 指向同包 `visual.tres`，`visual.tres` 指向同包 `frames.tres`。
5. 默认保持 `AutoLayoutFromSpriteFrames = true`，除非该单位确实需要特殊锚点或缩放。
6. 通过 `FootprintWidth` / `FootprintHeight` 表达占格，别靠拉伸 Sprite 假装大体型。
7. 普通攻击和技能优先配置 `AbilityDefinition`，不要把具体技能写死进单位工厂。
8. 在 Godot 中打开资源确认引用不丢失，再进入站点或战斗场景检查待机、移动、攻击、受击和死亡表现。

### 单位 Inspector 优化方向

单位资源的 Inspector 应按作者工作流组织，而不是按代码字段出现顺序组织：

```text
基础身份：Id / DisplayName / ControlMode
表现：Visual / Audio / DebugMarkerColor
生命：MaxHp
行动：MoveRange / CanEnterWater
攻击：AttackDamage / AttackRange / Abilities
战斗占位：Footprint / Blocking / Targetable
```

如果后续单位字段继续膨胀，优先拆成子资源，例如 `CombatStatsResource`、`FootprintDefinition`、`UnitAuthoringTags`，而不是让一个 `BattleUnitDefinition` 变成超长表单。

### 单位预览工作台

单位最终效果需要通过专用工作台验收，不要求为每个单位创建独立场景。

默认调试入口在当前具体场域场景内：

```text
scenes/world/sites/impl/BonefieldSite.tscn
  UnitPreviewWorkbench
```

这个节点默认隐藏，避免影响正常运行。调试单位视觉时，在具体场域场景里选中 `UnitPreviewWorkbench`，打开可见性；然后选中它下面的 `PreviewRoot/AnimatedSprite2D`，将目标 `frames.tres` 拖到这个子节点自己的 `SpriteFrames` 字段。父节点只负责布局、占格和动画切换辅助参数。这样能像摆一个普通单位一样，在真实地图、格子尺度和相机环境中检查动画帧、比例、锚点和占格视觉。

`WorldSiteRoot.tscn` 是场域运行壳，不是可创作的具体地图，不应拥有默认作者工具节点。后续出现多个场域时，单位预览入口应沉淀成每个具体场域都能复用的场域模板约定，或由新的抽象场域基底承载；不要把 `BonefieldSite` 当成长期唯一入口。

独立工作台模板保留为复用资源：

```text
scenes/tools/battle/UnitPreviewWorkbench.tscn
```

使用方式：

1. 在 Godot 中打开当前具体场域场景，例如 `BonefieldSite.tscn`，或打开独立模板 `UnitPreviewWorkbench.tscn`。
2. 选中 `UnitPreviewWorkbench`，打开可见性。
3. 选中子节点 `PreviewRoot/AnimatedSprite2D`，将目标 `frames.tres` 拖到它自己的 `SpriteFrames` 字段。
4. 回到 `UnitPreviewWorkbench`，通过 `AnimationNameSet` 下拉选择动画命名预设，不要拖入 `BattleUnitAnimationSet` 资源。
5. 按对应 `visual.tres` 设置 `AutoLayoutFromSpriteFrames`、`TargetMaxSpriteSizePixels`、`GroundAnchorOffsetPixels`、`VisibleAlphaThreshold`、`Offset`、`ManualScale` 和 `PreviewModulate`。
6. 通过 `PreviewAnimation` 在 `Idle`、`Move`、`Attack`、`Hit`、`Defeated` 间切换。
7. 用 `FootprintWidth` / `FootprintHeight` 仅预览占格带来的统一视觉放大；真实占格仍写在 `unit.tres`。
8. 观察 `FootprintOverlay`、缩放、锚点和状态文本。
9. 将确认后的参数人工写回对应 `visual.tres`；如占格需要变化，再回到对应 `unit.tres` 修改 `FootprintWidth` / `FootprintHeight`。

工作台不再加载 `unit.tres`，也不实例化完整 `BattleUnitBase.tscn`。它把场景内固定的 `PreviewRoot/AnimatedSprite2D` 当成一个可直接配置的单位视觉节点；脚本只读取这个子节点已有的 `SpriteFrames`，不会清空或覆盖它。布局计算必须复用 `BattleUnitVisualLayoutCalculator`，该计算器同时被 `BattleUnitFactory` 使用，避免工作台和运行时出现两套锚点 / 缩放算法。

工作台只做编辑器验收，不拥有单位事实。单位权威仍然是 `unit.tres`、`visual.tres`、`frames.tres` 和能力 / 音频资源。

## Semantic Marker 配置

地图语义区域应通过场景中的 Marker 节点人工摆放，而不是在代码或文档里维护坐标表。

目标结构：

```text
Map scene
  SemanticMarkers
    BuildingSlotMapMarker
    DeploymentZoneMapMarker
```

`SemanticMapMarker` 是抽象通用基类，不直接放到地图场景里。地图里实例化业务子场景：

- `BuildingSlotMapMarker.tscn`：固定语义为 `BuildingSlot`，用于建筑槽位。
- `DeploymentZoneMapMarker.tscn`：固定语义为 `DeploymentZone`，额外导出 `DeploymentSide`、可选 `FactionId` 和 `Priority`。`DeploymentSide = Player` 时预览为浅绿色，`Enemy` 时预览为浅红色。

通用基类只保留这些跨业务字段：

- `MarkerId`：同一张地图内唯一。
- `Width` / `Height`：从左上角锚点向右、向下扩展的格子尺寸。
- `CellHeight`：多高度地图中的格面高度。
- `Tags`：给消费者过滤用。
- `SnapToGrid`：编辑器中吸附到地图坐标层。
- `DrawEditorPreview`：显示编辑器覆盖区域。

运行时提取后只消费 `SemanticMapMarkerData`。Marker 节点本身、编辑器绘制、工具脚本状态都不是运行时权威。

### Marker 配置检查

1. 地图场景有 `SemanticMarkers` 根节点。
2. 每个 Marker 的 `MarkerId` 在当前地图内唯一。
3. `BuildingSlot` 的 `MarkerId` 和 `FacilitySlotDefinition.SlotId` 对齐。
4. `DeploymentZone` 使用 `DeploymentSide` 表达玩家 / 敌人 / 共享部署归属，不依赖 Marker 名称或节点名。
5. `Width` / `Height` 覆盖的格子没有越出地图有效区域。
6. 开启 `DrawEditorPreview` 后，作者能直接看见区域、外框和锚点。
7. 运行时日志没有 `SemanticMapMarkerDiagnostic`。

### Marker Inspector 优化方向

如果后续 Marker 类型增加，优先新增业务子脚本和子场景；只有同一个业务子类内部确实存在模式切换时，才考虑动态 Inspector：

- `BuildingSlot` 强调 `MarkerId`、`Width`、`Height`、`Tags`。
- `DeploymentZone` 强调 `DeploymentSide`、可选 `FactionId`、`Priority`、入口方向相关字段。
- `Entrance` 强调 `FactionId`、方向、容量、可见性。
- 战术 Marker 强调 `Tags`、`Priority`、战术语义，不暴露设施字段。

这个优化属于编辑器体验，不应改变 `SemanticMapMarkerData` 的纯数据边界。

## 场景模板配置

需要复用的 UI、单位、Marker、反馈、按钮、行项目都应做成 `.tscn` 模板，然后由代码实例化和绑定数据。

推荐：

- 共享战斗单位用 `BattleUnitBase.tscn`。
- 重复 UI 行、按钮、Marker、反馈数字用独立模板场景。
- 地图语义用场景内业务 marker 子场景，不直接实例化抽象 `SemanticMapMarker`。
- 需要跨场景复用的路径集中在工厂或配置资源中。

避免：

- 在业务逻辑里 `new` 出完整 UI 树。
- 为每个单位复制一份完整战斗单位场景。
- 在场景脚本里硬编码大量具体单位、剧情、奖励或设施坐标。
- 让模板场景拥有运行时业务规则。

## 校验与诊断

人工配置越多，越需要明确失败方式：

- 缺资源、缺 ID、重复 ID、非法引用要有日志或校验报告。
- Runtime 不应静默使用错误兜底掩盖配置问题。
- 可以有迁移适配器，但适配器不能拥有新的业务事实。
- 批量生成或批量改名必须产出报告，便于审计。
- 文档、资源和代码不一致时，先确认权威文档，再修资源或代码。

最低检查清单：

- 单位：`unit.tres` 能被 `BattleUnitFactory` 按 ID 找到。
- 单位：`Visual`、`SpriteFrames`、标准动画名存在。
- 单位：占位和阻挡配置能被部署、寻路和表现共同解释。
- Marker：提取结果中有预期类型和 ID。
- Marker：消费者按类型和标签过滤，不把所有 Marker 混用。
- 场景：模板节点路径和代码导出的 `NodePath` 一致。

## 何时改代码

人工配置不能替代系统能力。出现以下情况时才考虑改代码：

- 需要新的运行时规则、目标类型、效果原语或结算事实。
- 需要新的地图语义类型并被多个系统消费。
- 现有 Resource 无法表达稳定内容结构。
- Inspector 已经复杂到影响作者判断，需要拆分资源或引入动态属性。
- 工具脚本只能靠高频扫描和隐式修复维持正确，说明数据边界需要重做。

改代码前先判断所属系统：Content Definition、Map、Battle Runtime、Presentation、AI、World、Settlement 或 Save。不要用一个局部工具脚本绕过长期架构。
