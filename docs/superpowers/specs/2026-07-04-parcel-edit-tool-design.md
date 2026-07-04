# 地块编辑工具设计

## 目标

为 CustomLandParcel 做一个接近原生体验的地图内地块编辑工具。玩家直接在地图上绘制自定义多边形地块边界，在 UI 中查看价格，并且必须使用游戏内现金购买后，这块地才会变成可建造区域。

这个工具的主操作应该像 Cities: Skylines II 自己的地图工具，而不是调试面板：鼠标输入、地形射线、悬停高亮、拖拽控制点、实时 overlay 反馈是主要工作流。UI 面板只作为辅助，用于精确编辑、购买、列表管理和状态展示。

## 用户流程

1. 玩家打开 CustomLandParcel 面板，激活地块编辑模式。
2. 在空地上左键点击，开始绘制一个新的多边形。
3. 继续左键点击，逐个添加节点。
4. 当至少有 3 个有效节点时，点击第一个节点或按 Enter 完成多边形。
5. 完成后的地块创建为 `Available`，不是 `Purchased`。
6. UI 显示面积、价格、节点数量和购买按钮。
7. 点击购买时检查城市现金；现金足够就扣除地块价格，并把地块状态改为 `Purchased`。
8. 建造、道路和拆除仍然只允许发生在 `Purchased` 地块内。

已有地块支持：

- 点击多边形内部：选中地块。
- 点击节点：选中节点。
- 拖拽节点：修改边界。
- 拖拽多边形内部：移动整个地块。
- 点击或悬停边线：选中边。
- 在选中边上插入节点。
- 删除选中节点，但删除后仍必须至少保留 3 个节点。

## 状态模型

`LandParcelState` 继续使用现有值：

- `Available`：已经画出来但还没购买的自定义地块。它可见、可选择，但不可建造。
- `Purchased`：已经付费购买的地块。只有这种地块会被建造限制系统判定为可建造。
- `Locked`：预留给未来里程碑、场景或规则限制。

编辑工具新画出的多边形默认是 `Available`。当前开发用的默认初始地块可以继续保持 `Purchased`，方便验证现有功能；但用户通过工具创建的新地块必须走“先画边界、再付费购买”的流程。

## 架构

### ParcelEditToolSystem

负责游戏工具行为：

- 从 UI binding 激活或关闭工具。
- 处理鼠标输入。
- 对地形做 raycast，得到世界坐标。
- 创建草稿多边形。
- 管理节点拖拽和整块地拖拽生命周期。
- 处理 Enter、Esc、Delete 等快捷键。
- 记录状态切换和被拒绝的操作日志。

这个系统不直接保存地块数据。它只保存临时输入状态，并通过 `ParcelStoreSystem` 修改正式数据。

### ParcelEditSession

保存当前编辑操作的小型状态对象：

- 当前模式：`Idle`、`Drawing`、`HoverVertex`、`HoverEdge`、`HoverParcel`、`DragVertex`、`DragParcel`。
- 当前悬停的地块 id、边索引、节点索引。
- 拖拽起点，以及为了稳定计算 delta 所需的原始节点数据。
- 正在绘制的草稿多边形节点。

这个对象要足够独立，方便日志输出一行简洁的 session 摘要。

### ParcelEditHitTest

纯几何命中测试工具：

- 在世界坐标半径内查找最近节点。
- 在世界坐标半径内查找最近边线。
- 判断光标是否位于某个多边形内部。
- 返回有优先级的命中结果：节点优先，其次边线，最后多边形内部。

它不依赖 ECS 或游戏系统，只读取地块点集和当前光标的 `float2` 坐标。

### ParcelStore / ParcelStoreSystem

增加正式编辑 API：

- `CreatePolygon(name, points, state, reason)`
- `SetSelectedVertexPosition(position, reason)`
- `SetVertexPosition(parcelId, vertexIndex, position, reason)`
- `InsertVertexOnEdge(parcelId, edgeIndex, position, reason)`
- `MoveParcel(parcelId, delta, reason)`
- `TryPurchaseSelectedParcel(cost, reason)` 或交给购买系统处理

Store 必须负责最小节点数量校验、几何变更后的价格重算，以及详细日志。非法操作要明确拒绝，不要静默失败。移动整个地块也属于几何变更，不能只平移节点；移动结束后必须重新计算覆盖到的地图格、资源采样和最终价格。

### ParcelPurchaseSystem

负责真实游戏现金购买：

- 读取当前选中地块。
- 计算当前价格。
- 通过 `CitySystem.City` 读取 `PlayerMoney`。
- 如果现金不足：保持地块为 `Available`，记录价格和当前现金，并把失败状态暴露给 UI。
- 如果现金足够：使用和原版 `MapTilePurchaseSystem` 一样的 `PlayerMoney.Subtract(cost)` 方式扣钱，然后把地块改为 `Purchased`。

购买必须由后端权威执行。UI 只发起购买请求，不自己判断或直接改状态。

### ParcelBoundaryRenderSystem

扩展 overlay 显示编辑状态：

- `Purchased` 地块边界：绿色。
- `Available` 地块边界：黄色或橙色。
- 选中地块：更亮的边框。
- 悬停节点或边线：高亮。
- 草稿多边形：临时线段。
- 非法草稿多边形：红色反馈。

这个系统读取 `ParcelStoreSystem` 和 `ParcelEditSession`，但不修改地块数据。

### ParcelUISystem

继续作为辅助面板：

- 激活或关闭编辑工具。
- 从列表中选择地块。
- 重命名地块。
- 显示面积、价格、状态、节点数量。
- 触发购买。
- 显示购买失败状态。
- 可选：为选中节点提供精确坐标输入。

## 价格计算

价格不要使用随手写的固定面积公式，应该参考原版地图格购买逻辑。

反编译 `Game.Simulation.MapTilePurchaseSystem` 后看到，原版逻辑主要使用：

- 地图格上的 `MapFeatureElement` 数量。
- 地图格 prefab 上的 `MapFeatureData.m_Cost`。
- 地图格 prefab 上的 `TilePurchaseCostFactor.m_Amount`。
- 用于归一化地图格面积和资源量的 baseline modifier。
- 已拥有地图格数量带来的递增价格。
- 购买时从 `CitySystem.City` 的 `PlayerMoney` 中扣款。

自定义多边形第一版应近似原版价格：

1. 找到或采样自定义多边形覆盖到的原版地图格。
2. 对每个覆盖到的地图格，根据它的 feature amount 和 prefab feature cost 计算类似原版地图格的价值。
3. 按多边形覆盖比例或采样命中比例缩放。
4. 使用类似原版“已购买数量越多价格越高”的 multiplier。
5. 设置最低价格，避免极小地块免费或接近免费。

如果第一版做精确裁剪成本太高，就先用地图格中心点、四角点和网格采样来估算覆盖比例，并把采样比例写入日志。价格计算必须隔离到 `ParcelPriceCalculator`，这样后续可以提高精度而不影响 UI 和购买流程。

只要多边形的位置或形状发生变化，都必须触发价格重算。尤其是拖拽整个地块时，面积可能不变，但它覆盖到的原版地图格、资源和购买倍率上下文可能变化，所以不能复用移动前的价格。

## 错误处理与日志

关键日志：

- 工具激活和关闭。
- raycast 失败。
- 草稿开始、加点、完成、取消。
- 选中对象变化时的 hit test 结果。
- 拖拽开始、更新、结束，包含地块 id 和 delta。
- 非法多边形或非法删除节点被拒绝。
- 价格计算输入：面积、采样地图格数量、feature value、multiplier、最终价格。
- 购买成功或失败：扣款前现金、价格、扣款后现金。

UI 需要展示的失败状态：

- 节点数量不足。
- 多边形非法。
- 资金不足。
- 没有选中地块。

## 验证

手动验证：

- 画一个新多边形，确认它显示为 `Available`。
- 确认在 `Available` 地块内仍然不能建造。
- 购买该地块，确认游戏现金减少。
- 确认在 `Purchased` 地块内可以建造和拆除，在边界外仍然不行。
- 拖拽一个节点，确认价格、面积、overlay、保存版本和建造边界都会更新。
- 拖拽整个地块，确认所有节点一起移动，并确认价格会按新位置重新计算。
- 保存并重新加载，确认地块状态和节点位置保留。

构建验证：

- `dotnet format --verify-no-changes --no-restore`
- `git diff --check`
- 已经 restore 过时优先跑 `dotnet build --no-restore`
- 只有需要 restore 或部署时才跑完整 `dotnet build`

## 范围边界

本次包含：

- 原生地图式多边形绘制。
- 鼠标拖拽编辑节点和整块地。
- `Available` 到 `Purchased` 的购买流程。
- 真实扣除游戏现金。
- 参考原版地图格购买逻辑的价格计算。
- 编辑状态 overlay 反馈。

本次不包含：

- 合并和拆分地块。
- 撤销和重做栈。
- 里程碑锁定。
- 对每个原版地图格做精确计算几何裁剪。
- Harmony patch。
