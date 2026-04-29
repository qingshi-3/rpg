# rpg

一个从零开始制作的 2D 像素风 RPG 游戏项目，使用 Godot 4.5 开发。

## 项目目标

- 构建一个可扩展的 2D 像素风 RPG 基础框架。
- 优先打通地图、角色移动、交互、战斗、物品与任务等核心玩法闭环。
- 保持资源、脚本和场景结构清晰，方便后续持续迭代。

## 技术栈

- 引擎：Godot 4.5
- 渲染：GL Compatibility
- 类型：2D 像素风 RPG
- 脚本：C#

## 本地运行

1. 使用 Godot 4.5 打开本目录。
2. 在编辑器中导入 `project.godot`。
3. 运行主场景 `scenes/battle/BattleRoot.tscn`。

当前主场景是通用战斗壳，默认加载教程战斗地图。

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
assets/      美术、音频、字体等原始与导入资源
scenes/      Godot 场景文件
src/         C# 源码
assets/      Godot Resource 数据与导入资产
docs/        设计文档、技术变更与测试说明
```

## 项目文档

- 文档入口：`docs/README.md`
- 协作规则：`docs/collaboration/ai-collaboration.md`
- 产品层：`docs/design/product/vision.md`
- 玩法层：`docs/design/gameplay/core-loop.md`
- 系统层：`docs/design/systems/README.md`
- 世界层：`docs/design/world/README.md`
- 内容层：`docs/content/tutorial/tutorial-battle.md`
- 路线图：`docs/roadmap/current-design-progress.md`、`docs/roadmap/development-priority.md`

## 开发原则

- 小步提交，优先完成可运行、可验证的垂直切片。
- 像素资源保持统一分辨率、比例和导入设置。
- 核心系统先做简单稳定版本，再根据实际需求扩展。
