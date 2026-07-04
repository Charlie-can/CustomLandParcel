# Parcel Edit Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现地图内多边形地块编辑、位置变化后的价格重算，以及真实游戏现金购买。

**Architecture:** 后端以 `ParcelStoreSystem` 为唯一地块数据入口，`ParcelEditToolSystem` 只保存临时输入状态并调用 Store API。价格计算隔离到 `ParcelPriceCalculator`，购买隔离到 `ParcelPurchaseSystem`，UI 只触发工具激活和购买请求。

**Tech Stack:** Cities: Skylines II code mod API, C#/.NET, Unity ECS, `ToolBaseSystem`, `ToolRaycastSystem`, `Colossal.UI.Binding`, `OverlayRenderSystem`.

---

### Task 1: Store And Price Architecture

**Files:**
- Create: `Geometry/ParcelPriceCalculator.cs`
- Modify: `Geometry/ParcelGeometry.cs`
- Modify: `Data/ParcelStore.cs`
- Modify: `Systems/ParcelStoreSystem.cs`

- [x] 增加 `ParcelPriceCalculator`，用面积、质心位置、已购买地块数量计算价格，并输出可记录的价格明细。
- [x] 让所有几何变更 API 都调用统一的 `RepriceParcel`，包括整块移动、节点移动、插入节点、删除节点和缩放。
- [x] 增加正式 API：创建多边形、设置节点绝对位置、在指定边插入节点、移动指定地块、设置选中地块状态。
- [x] 所有拒绝路径写明确日志，包含地块 id、节点索引、原因。

### Task 2: Real Purchase System

**Files:**
- Create: `Systems/ParcelPurchaseSystem.cs`
- Modify: `Systems/ParcelUISystem.cs`
- Modify: `Mod.cs`

- [x] 新增 `ParcelPurchaseSystem`，通过 `CitySystem.City` 读取 `PlayerMoney`。
- [x] 购买前重算价格；资金不足时不改状态，并记录现金、价格、地块 id。
- [x] 资金足够时使用 `PlayerMoney.Subtract(cost)` 扣款，再把地块改为 `Purchased`。
- [x] UI 的 `purchaseSelectedParcel` binding 改为调用购买系统，不直接改 Store 状态。

### Task 3: Map Edit Tool

**Files:**
- Create: `Systems/ParcelEditMode.cs`
- Create: `Systems/ParcelEditHitTest.cs`
- Create: `Systems/ParcelEditSession.cs`
- Create: `Systems/ParcelEditToolSystem.cs`
- Modify: `Systems/ParcelUISystem.cs`
- Modify: `Mod.cs`

- [x] 增加纯几何 hit test，优先命中节点，其次边线，最后多边形内部。
- [x] 增加编辑 session，记录模式、悬停结果、拖拽起点、草稿点。
- [x] 新增 `ToolBaseSystem` 工具：左键新建/加点/选中/拖拽，右键或 Esc 取消，完成后创建 `Available` 地块。
- [x] UI binding 增加 `setParcelEditToolActive` 和 `parcelEditToolActive`。
- [x] 工具激活、关闭、raycast 失败、草稿加点、完成、取消、拖拽开始和结束都写日志。

### Task 4: Overlay Feedback

**Files:**
- Modify: `Systems/ParcelBoundaryRenderSystem.cs`

- [x] overlay 读取编辑 session，绘制草稿多边形。
- [x] 悬停节点、悬停边线、拖拽节点使用更亮的颜色。
- [x] `Available`、`Purchased`、`Locked` 继续使用状态色。

### Task 5: Verification And Commit

**Files:**
- All changed files

- [x] 跑 `dotnet format --verify-no-changes --no-restore`。
- [x] 跑 `git diff --check`。
- [x] 跑 `dotnet build --no-restore`；如果需要部署或 restore，再直接提权跑完整 `dotnet build`。
- [x] 检查 diff，确认没有无关改动。
- [x] 提交实现。
