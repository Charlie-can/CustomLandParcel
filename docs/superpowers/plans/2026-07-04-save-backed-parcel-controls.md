# Save-Backed Parcel Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a save-backed editable rectangular parcel boundary for CustomLandParcel, controlled by CS2 mod keybindings and reflected in placement blocking and overlay rendering.

**Architecture:** Add one authoritative `ParcelBoundsSystem` that owns the current parcel min/max and serializes it into the current save via `IJobSerializable`. Existing blocker, renderer, and diagnostics systems read this system instead of hardcoded constants. Add `CustomLandParcelSettings` plus a `ParcelBoundaryControlSystem` so hotkeys registered through the game's mod settings mutate the saved parcel state and trigger blocker recreation.

**Tech Stack:** Cities: Skylines II code mod API, Unity ECS systems, `Colossal.Serialization.Entities.IJobSerializable`, `Game.Modding.ModSetting`, `Game.Input.ProxyAction`, Rider code analysis, `dotnet build` with the CS2 ModPostProcessor.

---

### Task 1: Commit the Plan

**Files:**
- Create: `docs/superpowers/plans/2026-07-04-save-backed-parcel-controls.md`

- [ ] **Step 1: Verify plan file exists**

Run: `Test-Path docs/superpowers/plans/2026-07-04-save-backed-parcel-controls.md`
Expected: `True`

- [ ] **Step 2: Commit the plan**

Run:
```powershell
git add docs/superpowers/plans/2026-07-04-save-backed-parcel-controls.md
git commit -m "Add save-backed parcel controls plan"
```
Expected: commit succeeds.

### Task 2: Add Save-Backed Parcel State

**Files:**
- Create: `Systems/ParcelBounds.cs`
- Create: `Systems/ParcelBoundsSystem.cs`
- Modify: `Systems/ConstructionRestrictionSystem.cs`

- [ ] **Step 1: Create `ParcelBounds` value type**

Create a small internal struct with `Min`, `Max`, `Default`, `Normalize`, `Contains`, and formatting helpers. Use `Unity.Mathematics.float2` and keep the default parcel as `(-500, -500)` to `(500, 500)`.

- [ ] **Step 2: Create `ParcelBoundsSystem`**

Implement `GameSystemBase, IJobSerializable` with private bounds, public `Bounds`, `Version`, `SetBounds`, `Move`, `Resize`, and serializer methods that write schema version, min, and max.

- [ ] **Step 3: Change active systems to stop using static hardcoded bounds**

New active systems read `ParcelBoundsSystem.Bounds`; the inactive old validation system can keep a compatibility `Contains` wrapper using `ParcelBounds.Default`.

### Task 3: Wire Existing Systems to Dynamic Bounds

**Files:**
- Modify: `Systems/ParcelBoundaryBlockerSystem.cs`
- Modify: `Systems/ParcelBoundaryRenderSystem.cs`
- Modify: `Systems/ParcelPlacementDiagnosticsSystem.cs`

- [ ] **Step 1: Read `ParcelBoundsSystem` in each system**

In `OnCreate`, call `World.GetOrCreateSystemManaged<ParcelBoundsSystem>()` and store it in a private field.

- [ ] **Step 2: Recreate blockers on bounds changes**

Track the last applied `ParcelBoundsSystem.Version`. If it differs, destroy existing blockers and recreate the four native blocker rectangles around the current bounds. Log version and bounds.

- [ ] **Step 3: Render current bounds**

Read current bounds each frame and reduce dashed boundary intensity to a thinner, darker line.

- [ ] **Step 4: Update diagnostics**

Use current bounds for placement diagnostics and include current bounds/version in periodic log lines.

### Task 4: Add Mod Settings and Keybinding Control

**Files:**
- Create: `CustomLandParcelSettings.cs`
- Create: `Systems/ParcelBoundaryControlSystem.cs`
- Modify: `Mod.cs`

- [ ] **Step 1: Add `CustomLandParcelSettings`**

Subclass `ModSetting`. Define `ProxyBinding` properties decorated with `SettingsUIKeyboardBindingAttribute` for toggle, move, grow, and shrink actions.

- [ ] **Step 2: Register settings in `Mod.OnLoad`**

Create one static settings instance, call `RegisterInOptionsUI()` and `RegisterKeyBindings()`, then register `ParcelBoundsSystem` and `ParcelBoundaryControlSystem` before blocker, diagnostics, and render systems.

- [ ] **Step 3: Dispose settings in `Mod.OnDispose`**

Call `UnregisterInOptionsUI()` if the settings instance exists.

- [ ] **Step 4: Implement `ParcelBoundaryControlSystem`**

Resolve actions through `Mod.Settings.GetAction(actionName)`. When edit mode is off, only listen to toggle. When edit mode is on, use `WasPressedThisFrame()` to move or resize the parcel, call `ParcelBoundsSystem.Move/Resize`, and log every change.

### Task 5: Verify, Deploy, and Commit

**Files:**
- All modified C# files

- [ ] **Step 1: Format changed C# files in Rider or dotnet format if available**

Expected: formatting succeeds or no supported formatter is available.

- [ ] **Step 2: Run static/build checks**

Run `dotnet build`. Expected: build succeeds and deploys to the CS2 mod folder.

- [ ] **Step 3: Commit implementation**

Run:
```powershell
git add .
git commit -m "Add save-backed parcel controls"
```
Expected: commit succeeds.

- [ ] **Step 4: Read latest mod log after user test or build**

Check `C:\Users\charlie\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\CustomLandParcel.Mod.log` for load, settings registration, serializer, control, blocker recreation, and overlay log lines.
