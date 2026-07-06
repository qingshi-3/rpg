# rpg

一个使用 Godot 4.5 开发的 2D 像素风轻 RTS RPG 项目。

当前产品方向暂定为：

```text
有温度的异世界建国式轻 RTS 肉鸽 RPG
```

每一局都是一段小型异世界 RPG 战役。玩家从弱小据点出发，在模块化随机小世界中探索、招募伙伴、经营势力、处理事件、收集情报，并在英雄带队的轻 RTS 战斗中读懂敌人的计划、破坏 Boss 的规则，逐步建立自己的新生势力。

## 游戏介绍

这个项目不希望只把“英雄、意图、实时指挥、养成、兵团指挥”等标签堆在一起，而是围绕一个更完整的体验主轴展开：

```text
领地给玩家势力感。
角色给玩家情感锚点。
战斗给玩家破局成就。
```

大世界提供探索、整顿、招募、经营、剧情事件和战前准备；场域进入战时后提供低负担但高机制密度的英雄带队轻 RTS 体验。敌人意图不是单纯的 UI 提示，而是玩家读取敌方计划、提前准备并改变战局的策略窗口。

## 核心体验

- 模块化随机小世界：每局世界有变化，但城镇、据点、矿洞、危险区域、招募点、Boss 巢穴等模块保持可理解的稳定结构。
- 有温度的伙伴养成：队友不只是数值卡片，而是拥有名字、性格、立场、关系、事件和战斗机制的角色。
- 轻量经营与整顿：战后允许玩家治疗、训练、修整、侦察、互动和准备，不默认施加强时间压力。
- 机制型轻 RTS 战斗：小怪和 Boss 都应有清晰机制身份，避免纯血量和攻击力的数值互拍。
- 敌人意图与情报：通过侦察、角色能力和战场 UI 让玩家逐步读懂敌人的推进、攻击、蓄力、召唤和控制计划。
- 战斗与大世界互相影响：大世界准备改变战斗，战斗结果也改变世界状态、角色关系和后续事件。

## 项目目标

- 优先验证“异世界小世界 + 机制型轻 RTS + 建立势力”的垂直切片。
- 构建大世界与可持久运营场域的双层结构，并让经营准备和战时结果产生真实互相影响。
- 打磨低负担、可读、可反制的机制型战斗，而不是堆叠复杂操作。
- 为后续角色情感、剧情事件、据点经营、敌人意图、兵团指挥和局外成长保留清晰扩展边界。
- 保持资源、脚本、场景和文档结构清晰，方便长期迭代。

## 技术栈

- 引擎：Godot 4.5
- 渲染：GL Compatibility
- 类型：2D 像素风 / 英雄带队轻 RTS / RPG / 轻量肉鸽
- 脚本：C#

## 本地运行

1. 使用 Godot 4.5 打开本目录。
2. 在编辑器中导入 `project.godot`。
3. 运行战役地图 Demo：`scenes/world/CampaignMapDemo.tscn`。
4. 也可以单独运行场域战时执行场景：`scenes/world/sites/WorldSiteRoot.tscn`。

当前战役地图 Demo 会读取第一章“亡灵军团指挥官”配置。第一层交互是自由选择地点，进入地点后再触发事件池、遭遇或据点动作；`WorldSiteRoot` 也可以独立运行，用于调试战时闭环。

## C# 开发

使用 Rider 编写 C# 时，优先打开 `rpg.sln`，不要只按普通文件夹打开项目。

项目使用 `Godot.NET.Sdk`，Godot C# 类型提示来自自动引用的 `GodotSharp` 包。若 Rider 没有识别 `using Godot;`，先执行：

```powershell
dotnet restore rpg.sln
dotnet build rpg.sln
```

## 推荐目录约定

后续开发可按以下结构逐步整理：

```text
assets/      美术、音频、字体、源动画包与导入素材
resource/    Godot 作者资源、数据资源、主题、TileSet 与 Shader
scenes/      Godot 场景文件
src/         C# 源码与窄 GDScript 适配器
config/      JSON 等纯文本索引与映射
gameplay-design/    玩法设计权威文档
system-design/      系统架构权威文档
design-proposals/   架构/设计变更提案
gameplay-alignment/ 对齐缺口、实施提案与验收记录
```

## 项目文档

- 协作与权威路由：`AGENTS.md`、`gameplay-alignment/authority-map.md`
- 当前玩法方向：`gameplay-design/README.md`、`gameplay-design/content-systems-long-term-design.md`
- 战斗指挥规则：`gameplay-design/details/combat-command/README.md`
- 系统架构入口：`system-design/README.md`
- 战斗运行时与 AI 边界：`system-design/battle-runtime-architecture.md`、`system-design/battle-ai-boundary-architecture.md`
- 小队战术区域架构：`system-design/battle-group-tactical-region-architecture.md`
- 资源、场景、代码与配置目录边界：`system-design/resource-authoring-taxonomy.md`
- 对齐缺口与实现提案：`gameplay-alignment/gap-register.md`、`gameplay-alignment/implementation-proposals/`

## 开发原则

- 小步提交，优先完成可运行、可验证的垂直切片。
- 像素资源保持统一分辨率、比例和导入设置。
- 核心系统先做简单稳定版本，再根据实际需求扩展。
