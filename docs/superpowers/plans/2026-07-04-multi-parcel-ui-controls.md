# Multi-Parcel UI Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade CustomLandParcel from one save-backed rectangle to multiple save-backed polygon parcels with UI bindings, selection, vertex editing commands, overlay rendering, and detailed diagnostics.

**Architecture:** Introduce an authoritative `ParcelStoreSystem` that owns all parcel data and serializes it with the save. Rendering, placement diagnostics, blocker compatibility, and construction validation read this store instead of hardcoded rectangle bounds. A `ParcelUISystem` exposes parcel state and editing commands through CS2's `UISystemBase` binding pattern.

**Tech Stack:** Cities: Skylines II code mod API, Unity ECS, `Colossal.Serialization.Entities.IJobSerializable`, `Colossal.UI.Binding`, `OverlayRenderSystem`, C#/.NET Framework 4.8.

---

### Task 1: Add Multi-Parcel Data And Geometry

**Files:**
- Create: `Data/LandParcel.cs`
- Create: `Geometry/PolygonMath.cs`
- Create: `Systems/ParcelStoreSystem.cs`

- [ ] Add a parcel data model with id, name, state, price, and editable `float2` vertices.
- [ ] Add pure polygon helpers for contains, area, centroid, bounds, and point-to-segment distance.
- [ ] Add `ParcelStoreSystem` with create, delete, select, rename, purchase, move parcel, move vertex, insert vertex, delete vertex, and serialization.
- [ ] Seed a default rectangle as the first available parcel when a new save/default context is initialized.
- [ ] Add detailed logs for every mutating command and every serialization path.

### Task 2: Wire Existing Runtime Systems To The Store

**Files:**
- Modify: `Systems/ConstructionRestrictionSystem.cs`
- Modify: `Systems/ParcelBoundaryRenderSystem.cs`
- Modify: `Systems/ParcelBoundaryBlockerSystem.cs`
- Modify: `Systems/ParcelPlacementDiagnosticsSystem.cs`
- Modify: `Systems/ParcelBoundaryControlSystem.cs`
- Modify: `Mod.cs`

- [ ] Register `ParcelStoreSystem` before consumers.
- [ ] Render all parcel polygon edges with state-specific colors and draw selected parcel vertices as handles.
- [ ] Validate object preview centers and curve samples against purchased parcels in `ParcelStoreSystem`.
- [ ] Rebuild vanilla blocker compatibility around the union bounds of all purchased parcels, falling back to all parcels if none are purchased.
- [ ] Keep keyboard controls useful by moving/resizing the selected parcel rather than a single rectangle.
- [ ] Add logs that identify active parcel counts, selected parcel, selected vertex, version, and validation counts.

### Task 3: Add UI Bindings

**Files:**
- Create: `Systems/ParcelUISystem.cs`
- Modify: `Mod.cs`

- [ ] Add a `UISystemBase` binding group named `customLandParcel`.
- [ ] Expose raw parcel list JSON, selected parcel id, selected vertex index, version, and edit mode.
- [ ] Add triggers for add rectangle, select, select next/previous, rename, delete, purchase, move parcel, move vertex, insert vertex after selected, delete selected vertex, and clear all debug parcels.
- [ ] Log every UI trigger with inputs, result, selected ids, and resulting version.

### Task 4: Build, Review, And Commit

**Files:**
- All changed files

- [ ] Build with CS2 toolchain environment properties restored for this shell if needed.
- [ ] Inspect changed C# files for formatting consistency and accidental unrelated edits.
- [ ] Check `git diff --stat` and `git diff --check`.
- [ ] Commit the implementation with a concise message.
