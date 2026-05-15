# 战略大世界 V1 UI 与交互

本文定义 V1 战略地图的最小可用界面。目标是让玩家理解资源、场域、建筑、驻军、敌方威胁和战斗入口。

## UI 原则

- 第一屏就是可操作战略地图，不做宣传式页面。
- 战略地图必须是地图表面，不是几个流程选项；TileMap 由场景配置，代码只读取场域锚点、场域视觉层和导航表面。
- 不暂停时由 `WorldClock` 自动推进 `WorldTick`，暂停和敌方到达必须可见可控。
- 敌方 Raid 行军要在地图上表现为移动军队 marker，而不是只写在文字列表里。
- 所有按钮和面板都来自状态和 definition，不写死场域专属 UI。
- 玩家可见文本默认中文。
- 行动按钮必须显示消耗、收益、不可用原因。
- 敌方 Raid 必须有可见倒计时或状态，不要突然结算。
- `WorldSiteRoot` 非战时 UI 不能把地图盖成背景；地图必须仍是可交互经营面。

## 世界推进命名

总机制名：

```text
WorldProgression / 世界推进
```

运行时拆分：

- `WorldClock`：不暂停时持续走的世界时钟。
- `WorldTick`：世界时钟触发的离散结算点。
- `SiteProduction`：场域收益结算，例如矿场产出石材。
- `ThreatProgression`：Raid、敌军行军、危机倒计时推进。
- `WorldEventQueue`：后续用于按时间触发剧情、经营和危机事件。

`WorldTimeline` 只用于历史记录、剧情链或计划排程，不作为自动收益和事件推进的核心系统名。

## 主界面结构

```text
StrategicWorldRoot
  TopResourceBar
  WorldMapPanel
  SiteDetailPanel
  ThreatPanel
  ActionPreviewPanel
  ResultToast / ResultDialog
```

## Site 内经营界面

战斗发生在具体 site 内。战斗结束后，默认不直接退出到战略地图，而是把 `WorldSiteRoot` 切换为非战时经营态。

### WorldSiteRoot Peacetime

第一版结构：

```text
WorldSiteRoot
  SiteTopBar
    ResourceLine
    ReturnToStrategicMapButton
  SiteInteractionEntities
    UnitPlacementEntity[]
    FacilityInteractionEntity[]  # 后续，使用 site_interactions 场景实体，不使用 UI Button
  SiteOperationPanel
    SiteOverview
    FacilitySlotList
    GarrisonList
    ThreatList
    ActionList
    Notice
```

交互要求：

- 地图不加全屏遮罩。
- 非战时隐藏战斗 HUD 和战斗单位层。
- 地图仍可缩放、拖拽、查看地形。
- 建筑点不使用 Control/Button marker；需要可视化时使用 `scenes/world/site_interactions/` 下的场景实体，并绑定 `SlotId`。
- 未制作建筑实体前，右侧面板只显示建筑槽位状态，建造行动由通用 action resolver 选择合法槽位。
- 行动按钮仍来自 `WorldActionViewModel`，不写死具体按钮。
- 执行行动后即时刷新资源、建筑、驻军、威胁和反馈。

当前实现：

- 已有资源栏、右侧经营面板和建筑槽位状态列表。
- 建筑槽位目前不在地图上生成 Button UI。
- 已能调用 V1 行动：启用矿场、建造防御塔、训练民兵、等待 / 整顿、处理 Raid。
- 己方场域行动面板提供“出征”入口；玩家从来源场域选择英雄和小兵，再回到大地图右键选择目的地。
- 非战斗进入 `WorldSiteRoot` 时，战斗 HUD 和战斗单位层隐藏，世界推进不会在该 scene 内继续 tick。
- 大地图己方场域行动面板提供 `进入场域`；该入口使用非战斗 visit handoff 传递目标 `WorldSite`，不走 `BattleSessionHandoff`。
- 场域驻军以 `WorldSiteUnitPlacement` 保存部署位置；点击驻军场景实体可选中，拖动到目标格后会持久化到 `StrategicWorldState`。
- 新进驻小队默认落入该场域 `DefaultGarrisonZone` 的空闲 cell；驻军数量超过容量时，大地图进驻指令会被拒绝。

后续要求：

- 真实 site 地图制作完成后，建筑实体必须绑定到手工 anchor 或明确的 `SlotId`。
- 建造、拆除、升级、修复要从面板行动推进到 tile/slot 场景交互。
- 场域内 NPC 死亡、物件破坏、设施状态变化需要进入持久化场域状态。

### TopResourceBar

显示玩家当前可用资源：

```text
人口 7/8    经济 10    石材 4    世界步 2
```

人口显示：

```text
可用人口 / 总人口
```

如果人口被矿场占用：

```text
人口 7/8
tooltip: 矿场占用 1
```

### WorldMapPanel

V1 场域节点：

```text
玩家营地
埋骨地
墓园
```

每个节点显示：

- 名称。
- 控制权颜色。
- 建筑小图标。
- 驻军数量。
- 威胁标记。
- 受损标记。

不要求第一版做真实地图行走，但第一屏必须是大地图体验，而不是普通流程节点图。场域应作为地图上的据点、资源点和敌方源头出现，右侧面板只承接选中后的区域经营信息。场域之间不画连线，地图地表由 TileMap 表达，敌军 marker 在地表上移动。

场景配置入口：

```text
StrategicWorldRoot
  WorldMapRoot
    StrategicMapLayer                # 陆地和基础地貌视觉层，不参与寻路判定
    SiteVisualLayer                  # 场域大地图素材层
    StrategicBridgeLayer             # 桥面视觉层，不参与寻路判定
    StrategicNavigationTileLayer     # 大地图行军导航绘制层，战略行军唯一权威可走面
    MapAnchors
      Sites
        player_camp
        bonefield
        graveyard
```

代码优先读取 `MapAnchors/Sites/<site_id>` 作为场域中心。没有锚点时才回退到 definition 的 `MapPosition`。`SiteVisualLayer` 只负责场域大地图素材，`Sites/<site_id>` 的锚点必须落在对应素材内部。运行时从锚点所在 cell 做 4 向连通扫描，得到该场域的视觉 footprint，并用它驱动场域点击热区、选中描边和标签位置；没有 footprint 时才回退到旧圆形图标。不同场域素材不能在 `SiteVisualLayer` 上边相连，否则会被识别成同一片素材。蓝黄临时角标已移除；英雄数量和兵团数量角标等待正式 UI / 图标资源后再接入。敌方 Raid marker 由敌方 `WorldArmyState` 的位置绘制，移动路径来自 `StrategicNavigation`，不再配置隐藏移动锚点。

大世界相机使用通用 `MapCameraController`。WASD、鼠标滚轮缩放和鼠标中键拖拽都属于相机导航输入；中键拖拽不得改变场域选择、小队选择、出征目标或行动状态。

### SiteDetailPanel

选中场域后显示：

```text
埋骨地
状态：玩家控制 / 受损 / 敌方控制
驻军：民兵 x1
建筑：矿场、 防御塔
产出：石材 +2 / 世界步
威胁：亡灵 Raid 2 步后到达
```

建筑区域用列表或小卡片：

```text
矿场
状态：运行
占用：人口 1
产出：石材 +2

防御塔
状态：完好
防守：+3
战斗：塔支援 1 次
```

### ThreatPanel

显示 active threats：

```text
亡灵袭击
来源：墓园
目标：埋骨地
状态：行军中
到达：2 世界步
```

当 threat 进入 Attacking：

```text
亡灵正在攻击埋骨地
[进入防守战] [自动结算] [放弃据点]
```

V1 可以先只做 `[进入防守战]` 和 `[自动结算]`。

### ActionPreviewPanel

玩家 hover 或选中行动时显示：

```text
建造防御塔
消耗：石材 4，经济 2
效果：埋骨地防守 +3；防守战获得塔支援 1 次
耗时：立即
```

不可用时：

```text
建造防御塔
无法执行：石材不足
需要：石材 4，经济 2
当前：石材 2，经济 5
```

## V1 场域交互

### 玩家营地

显示：

- 资源。
- 兵营。
- 驻军。
- 可行动。

行动：

```text
训练民兵
出征
等待 / 整顿
```

如果没有可用英雄或小兵：

```text
出征 disabled: 没有可出征英雄或小兵
```

### 埋骨地

Hostile 状态：

```text
行动：
  暂无经营行动
```

攻打埋骨地应从己方场域“出征”发起：选择玩家营地或其他己方场域，点击出征，选择英雄和小兵，再在大地图右键埋骨地。

PlayerHeld 状态：

```text
行动：
  启用矿场
  建造防御塔
  出征
  等待 / 整顿
```

Damaged 状态：

```text
行动：
  修复矿场
  建造防御塔
  防守 Raid
```

V1 如果没有修复行动，可以先显示受损说明，不提供修复。

### 墓园

V1 先作为敌方源头显示。

显示：

```text
墓园
状态：亡灵控制
威胁：会周期性袭击埋骨地
```

可行动：

- 暂无。
- 后续扩展侦察、进攻、Boss 战。

### 地表区域

V1 的非场域地表不作为可选节点。道路、草地、荒地、山口等由 TileMapLayer 制作，后续行军、路障、哨塔、商队和野战在地表规则稳定后再接入。

### 大地图小队指令

当前大地图小队交互采用 RTS 式操作：

- 左键点击玩家小队：选择单支小队。
- 左键拖框：圈选多支玩家小队。
- 右键点击地表：已选小队移动到目标点。
- 右键点击敌方埋骨地：已选小队进攻，抵达后触发攻占战。
- 右键点击己方场域：已选小队进驻，抵达后转为该场域驻军。
- 右键点击墓园：当前拒绝执行，因为墓园在 V1 是敌方源头，尚未配置攻占战。
- 已选小队悬停到不可执行的场域指令时，鼠标显示禁止光标。

当前出征发起流程：

- 可先通过 `进入场域` 打开己方场域详细地图；进入后世界推进暂停。
- 选中己方场域。
- 点击 `出征`。
- 在右侧编成中选择英雄和小兵；V1 当前以 `骑士` 作为英雄、`民兵` 作为小兵。
- 点击 `选择目的地`。
- 在大地图右键敌方场域、己方场域或空地。
- 创建小队时从出发场域扣除所选英雄和小兵；进驻或攻占胜利后再写回目标场域驻军。

## 行动按钮生成

UI 不应写：

```text
if selectedSite == bonefield:
  add BuildDefenseTower button
```

应由 runtime 查询：

```text
availableActions = WorldActionService.GetAvailableActions(selectedSiteId)
```

每个行动返回：

```text
WorldActionViewModel
  ActionId
  DisplayName
  Description
  IsEnabled
  DisabledReason
  CostLines[]
  EffectLines[]
  WarningLines[]
```

按钮只展示 ViewModel。

## 反馈

成功行动反馈：

```text
防御塔已建成。
埋骨地防守能力提升。
```

WorldTick 反馈：

```text
世界步推进到 3。
埋骨地矿场产出石材 +2。
亡灵 Raid 还有 1 步到达。
```

战斗返回反馈：

```text
埋骨地已被占领。
矿场槽位已解锁。
```

失败反馈：

```text
无法建造防御塔：石材不足。
```

## 最小界面验收

- 玩家能看到三种资源。
- 玩家能选中四个战略对象。
- 玩家能看到埋骨地控制权和建筑状态。
- 玩家能从己方场域发起出征，并通过大地图右键埋骨地触发攻占战。
- 玩家能在占领后建矿场和防御塔。
- 玩家能看到墓园 Raid 倒计时。
- Raid 到达时玩家能进入防守战或自动结算。
- 玩家能在大地图看到、选中并处理野外小场域 marker。
- 所有行动按钮都来自通用 action view model。

## 后续扩展预留

UI 结构必须允许添加：

- 更多资源。
- 更多建筑。
- 中立势力。
- 英雄技能。
- 多入口部署预览。
- 野外拦截战。
- 情感系统提示。
- 场域缩略 TileMap。

新增内容时应优先添加 definition 和 view model 字段，不改每个按钮的专属逻辑。
