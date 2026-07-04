# Parcel Edit Tool Design

## Goal

Build a native-feeling in-map parcel editing tool for CustomLandParcel. Players draw custom polygon parcel boundaries directly on the map, inspect their price in UI, and must pay game money before the parcel becomes buildable.

The tool should feel closer to a Cities: Skylines II map tool than a debug panel: mouse input, terrain raycast, hover highlights, drag handles, and immediate overlay feedback are the primary workflow. The UI panel remains an assistant for precise edits, purchase, and list management.

## User Workflow

1. Player opens the CustomLandParcel panel and activates parcel edit mode.
2. Left-clicking empty terrain starts drawing a new polygon.
3. Further left-clicks add vertices.
4. Clicking the first vertex, or pressing Enter, completes the polygon when it has at least three valid vertices.
5. The completed parcel is created as `Available`, not `Purchased`.
6. The UI shows area, price, vertex count, and a purchase button.
7. Purchase checks the city money balance, subtracts the parcel price if possible, and changes the parcel to `Purchased`.
8. Construction, roads, and bulldoze remain allowed only inside `Purchased` parcels.

Existing parcels support:

- Click inside polygon to select parcel.
- Click vertex to select vertex.
- Drag vertex to edit the boundary.
- Drag polygon interior to move the whole parcel.
- Click or hover an edge to select it.
- Insert a vertex on a selected edge.
- Delete a selected vertex when the polygon still has at least three vertices.

## State Model

`LandParcelState` keeps the existing values:

- `Available`: a drawn custom parcel that has not been purchased. It is visible and selectable, but not buildable.
- `Purchased`: a paid parcel. Only these parcels are accepted by construction restriction checks.
- `Locked`: reserved for future milestone or scenario restrictions.

New polygons created by the edit tool default to `Available`. The seeded default parcel can remain `Purchased` for current development usability, but user-created polygons must follow the paid purchase path.

## Architecture

### ParcelEditToolSystem

Owns game-tool behavior:

- Activation/deactivation from UI binding.
- Mouse input.
- Terrain raycast to world position.
- Draft polygon creation.
- Drag lifecycle for vertex and parcel movement.
- Enter/Esc/Delete shortcuts.
- Logs state transitions and rejected operations.

This system should avoid storing parcel data itself. It should call `ParcelStoreSystem` methods and keep only transient input state.

### ParcelEditSession

Small state container for the active edit operation:

- Current mode: `Idle`, `Drawing`, `HoverVertex`, `HoverEdge`, `HoverParcel`, `DragVertex`, `DragParcel`.
- Hovered parcel id, edge index, vertex index.
- Drag start position and original points needed to apply stable deltas.
- Draft polygon points.

This should be independent enough that logs can print a compact session summary.

### ParcelEditHitTest

Pure geometry helper:

- Find nearest vertex within a world-space radius.
- Find nearest edge within a world-space radius.
- Find containing polygon.
- Return a ranked hit result: vertex first, edge second, polygon interior third.

This should not touch ECS or game systems. It reads parcel points and a cursor `float2`.

### ParcelStore / ParcelStoreSystem

Add formal editing APIs:

- `CreatePolygon(name, points, state, reason)`
- `SetSelectedVertexPosition(position, reason)`
- `SetVertexPosition(parcelId, vertexIndex, position, reason)`
- `InsertVertexOnEdge(parcelId, edgeIndex, position, reason)`
- `MoveParcel(parcelId, delta, reason)`
- `TryPurchaseSelectedParcel(cost, reason)` or a purchase-specific system call

The store must validate minimum vertex count, recalculate price after geometry edits, and reject invalid operations with detailed logs.

### ParcelPurchaseSystem

Owns real game-money purchase:

- Reads selected parcel.
- Calculates current price.
- Reads `PlayerMoney` from `CitySystem.City`.
- If money is insufficient, leaves parcel `Available`, logs price and current money, and exposes failure state to UI.
- If money is sufficient, subtracts price with the same `PlayerMoney.Subtract(cost)` pattern used by vanilla `MapTilePurchaseSystem`, then marks parcel `Purchased`.

Purchase must be backend-authoritative. UI only requests purchase.

### ParcelBoundaryRenderSystem

Extend overlay rendering to show edit state:

- Purchased parcel boundary: green.
- Available parcel boundary: yellow/orange.
- Selected parcel: brighter outline.
- Hovered vertex/edge: highlight.
- Draft polygon: temporary line strip.
- Invalid draft polygon: red feedback.

This system should read `ParcelStoreSystem` and `ParcelEditSession`, but should not mutate parcel data.

### ParcelUISystem

Remain an assistant panel:

- Activate/deactivate edit tool.
- Select parcel from list.
- Rename parcel.
- Show area, price, state, vertex count.
- Trigger purchase.
- Show purchase failure status.
- Optional precise coordinate inputs for the selected vertex.

## Pricing

Use vanilla map tile purchase logic as the reference instead of inventing a flat area-only formula.

Observed vanilla logic in `Game.Simulation.MapTilePurchaseSystem`:

- Map tiles contain `MapFeatureElement` amounts.
- The tile prefab provides `MapFeatureData.m_Cost`.
- The tile prefab has `TilePurchaseCostFactor.m_Amount`.
- Baseline modifiers normalize tile size/resources.
- Purchase cost increases with owned tile count.
- Purchase subtracts from `PlayerMoney` on `CitySystem.City`.

For custom polygons, first implementation should approximate vanilla pricing:

1. Sample or overlap vanilla map tiles covered by the custom polygon.
2. For each covered tile, compute a tile-like value using its feature amounts and prefab feature costs.
3. Scale by polygon coverage ratio or sampled coverage count.
4. Apply a purchased-parcel count multiplier similar to the vanilla owned-tile multiplier.
5. Clamp to a minimum price.

If exact feature coverage is too expensive initially, use center/corner/grid sampling per tile and log the sampled coverage ratio. The calculation should be isolated in a `ParcelPriceCalculator` so it can be improved without changing UI or purchase flow.

## Error Handling And Logs

Key logs:

- Tool activation/deactivation.
- Raycast misses.
- Draft start/add/complete/cancel.
- Hit test result when selection changes.
- Drag start/update/end with parcel id and delta.
- Rejected invalid polygon or vertex deletion.
- Price calculation inputs: area, sampled tiles, feature value, multiplier, final price.
- Purchase success/failure with money before, price, money after.

UI-facing failure states:

- Not enough vertices.
- Invalid polygon.
- Insufficient funds.
- No selected parcel.

## Verification

Manual verification should cover:

- Draw a new polygon and confirm it appears as `Available`.
- Confirm construction inside an `Available` parcel is blocked.
- Purchase the parcel and confirm money decreases.
- Confirm construction and bulldoze are allowed inside `Purchased` and blocked outside.
- Drag one vertex and confirm price, area, overlay, save version, and build boundary update.
- Drag the whole parcel and confirm all vertices move together.
- Save and reload, confirming parcel state and vertices persist.

Build verification:

- `dotnet format --verify-no-changes --no-restore`
- `git diff --check`
- `dotnet build --no-restore` when packages are already restored
- Full `dotnet build` only when restore/deploy is needed

## Scope Boundaries

Included in this feature:

- Native map-style polygon drawing.
- Mouse drag editing for vertices and whole parcels.
- Available-to-purchased flow.
- Real money deduction.
- Vanilla-inspired pricing.
- Overlay feedback for edit state.

Excluded for this pass:

- Merge/split parcels.
- Undo/redo stack.
- Milestone locking.
- Exact computational geometry clipping against every map tile.
- Harmony patches.
