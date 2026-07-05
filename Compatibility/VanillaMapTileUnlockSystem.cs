using Game;
using Game.Areas;
using Game.Common;
using Game.Tools;
using CustomLandParcel.Geometry;
using CustomLandParcel.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomLandParcel.Compatibility
{
    /// <summary>
    /// Runtime compatibility layer for vanilla map tiles.
    /// It unlocks vanilla Native MapTile entities that overlap buildable custom parcels, while
    /// ConstructionRestrictionSystem remains the only source of custom-parcel boundary blocking.
    /// </summary>
    public partial class VanillaMapTileUnlockSystem : GameSystemBase
    {
        private const int UnlockMaskRefreshFrames = 15;
        private const int InitialVanillaBoundsRetryFrames = 300;

        private EntityQuery _mVanillaMapTileQuery;
        private EntityQuery _mLegacyBlockerQuery;
        private EntityQuery _mUnlockedByParcelQuery;
        private EntityQuery _mLockedByParcelQuery;
        private EntityQuery _mHiddenBySettingQuery;
        private ParcelStoreSystem _mParcelStoreSystem;
        private int _mUnlockMaskRefreshFramesRemaining;
        private bool _mLastCompatibilityEnabled;
        private bool _mLegacyBlockersChecked;
        private bool _mInitialVanillaBoundsChecked;
        private int _mInitialVanillaBoundsAttempts;
        private int _mDisabledLogCooldownFrames;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            _mVanillaMapTileQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTile>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<VanillaMapTileBlocker>());
            _mLegacyBlockerQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileBlocker>());
            _mUnlockedByParcelQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileUnlockedByParcel>());
            _mLockedByParcelQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileLockedByParcel>());
            _mHiddenBySettingQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileHiddenByParcelSetting>());
            Mod.log.Info("VanillaMapTileUnlockSystem enabled as vanilla MapTile unlock compatibility layer.");
        }

        protected override void OnDestroy()
        {
            RestoreMapTileVisibility("system destroyed");
            RestoreLockedMapTiles("system destroyed");
            RestoreUnlockedMapTiles("system destroyed");
            DestroyLegacyBlockers("system destroyed");
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (!IsCompatibilityEnabled())
            {
                DisableCompatibilityLayer();
                return;
            }

            if (!_mLastCompatibilityEnabled)
            {
                _mLastCompatibilityEnabled = true;
                Mod.log.Info(
                    $"Vanilla MapTile unlock compatibility enabled by settings. {_mParcelStoreSystem.GetSummary()}.");
            }

            if (!_mLegacyBlockersChecked)
            {
                DestroyLegacyBlockers("legacy fake blocker cleanup");
                _mLegacyBlockersChecked = true;
            }

            if (_mUnlockMaskRefreshFramesRemaining > 0)
            {
                _mUnlockMaskRefreshFramesRemaining--;
                return;
            }

            MaintainUnlockedMapTiles();
            _mUnlockMaskRefreshFramesRemaining = UnlockMaskRefreshFrames;
        }

        private static bool IsCompatibilityEnabled()
        {
            return Mod.Settings == null || Mod.Settings.EnableVanillaMapTileCompatibility;
        }

        private void DisableCompatibilityLayer()
        {
            var destroyedBlockers = DestroyLegacyBlockers("compatibility disabled");
            var shownTiles = RestoreMapTileVisibility("compatibility disabled");
            var unlockedTiles = RestoreLockedMapTiles("compatibility disabled");
            var restoredTiles = RestoreUnlockedMapTiles("compatibility disabled");
            if (destroyedBlockers > 0 || restoredTiles > 0 || unlockedTiles > 0 || shownTiles > 0)
            {
                Mod.log.Info(
                    $"Vanilla MapTile unlock compatibility disabled by settings; destroyedLegacyBlockers={destroyedBlockers}, restoredTiles={restoredTiles}, unlockedTiles={unlockedTiles}, shownTiles={shownTiles}.");
            }
            else if (_mDisabledLogCooldownFrames <= 0)
            {
                Mod.log.Warn(
                    "Vanilla MapTile unlock compatibility is disabled by settings; vanilla unpurchased map tiles will keep blocking construction outside city limits.");
                _mDisabledLogCooldownFrames = 600;
            }

            _mLastCompatibilityEnabled = false;
            _mLegacyBlockersChecked = false;
            _mUnlockMaskRefreshFramesRemaining = 0;
            _mDisabledLogCooldownFrames--;
        }

        private void MaintainUnlockedMapTiles()
        {
            TryAlignDefaultParcelToVanillaOwnedTiles();
            if (_mParcelStoreSystem.TryGetBuildableUnionBounds(out var parcelMin, out var parcelMax))
            {
                RefreshUnlockedMapTiles(parcelMin, parcelMax);
                return;
            }

            RestoreMapTileVisibility("no buildable custom parcel union");
            RestoreLockedMapTiles("no buildable custom parcel union");
            RestoreUnlockedMapTiles("no buildable custom parcel union");
        }

        private void RefreshUnlockedMapTiles(float2 parcelMin, float2 parcelMax)
        {
            var shownBySetting = RestoreVisibleMapTilesOutsideBuildableBounds(parcelMin, parcelMax);
            var unlocked = 0;
            var locked = 0;
            var hiddenBySetting = 0;
            var alreadyUnlockedByParcel = 0;
            var alreadyVanillaOwned = 0;
            var overlapCandidates = 0;
            using var entities = _mVanillaMapTileQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!TileOverlapsBuildableParcel(entity, parcelMin, parcelMax))
                {
                    if (LockMapTileOutsideCustomParcel(entity))
                    {
                        locked++;
                    }

                    continue;
                }

                overlapCandidates++;
                if (ApplyMapTileVisibility(entity))
                {
                    hiddenBySetting++;
                }

                if (EntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity))
                {
                    alreadyUnlockedByParcel++;
                    if (EntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
                    {
                        EntityManager.RemoveComponent<VanillaMapTileLockedByParcel>(entity);
                        MarkUpdated(entity);
                    }

                    continue;
                }

                if (!EntityManager.HasComponent<Native>(entity))
                {
                    alreadyVanillaOwned++;
                    if (EntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
                    {
                        EntityManager.RemoveComponent<VanillaMapTileLockedByParcel>(entity);
                        MarkUpdated(entity);
                    }

                    continue;
                }

                EntityManager.RemoveComponent<Native>(entity);
                if (EntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
                {
                    EntityManager.RemoveComponent<VanillaMapTileLockedByParcel>(entity);
                }

                EntityManager.AddComponentData(entity, default(VanillaMapTileUnlockedByParcel));
                MarkUpdated(entity);
                unlocked++;
            }

            if (unlocked > 0 || locked > 0 || hiddenBySetting > 0 || shownBySetting > 0)
            {
                Mod.log.Info(
                    $"Parcel map tile ownership synchronized: unlockedInside={unlocked}, lockedOutside={locked}, hiddenBySetting={hiddenBySetting}, shownBySetting={shownBySetting}, overlapCandidates={overlapCandidates}, alreadyUnlockedByParcel={alreadyUnlockedByParcel}, alreadyVanillaOwnedInside={alreadyVanillaOwned}, mapTileCandidates={entities.Length}, buildableBounds={ParcelGeometry.Format(parcelMin)}..{ParcelGeometry.Format(parcelMax)}, showVanillaBorders={ShouldShowVanillaUnlockedMapTileBorders()}, {_mParcelStoreSystem.GetSummary()}.");
            }
        }

        private bool LockMapTileOutsideCustomParcel(Entity entity)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<MapTile>(entity) ||
                EntityManager.HasComponent<VanillaMapTileBlocker>(entity))
            {
                return false;
            }

            var wasUnlockedByParcel = EntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity);
            if (wasUnlockedByParcel)
            {
                EntityManager.RemoveComponent<VanillaMapTileUnlockedByParcel>(entity);
            }

            if (!EntityManager.HasComponent<Native>(entity))
            {
                EntityManager.AddComponentData(entity, default(Native));
                if (!wasUnlockedByParcel && !EntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
                {
                    EntityManager.AddComponentData(entity, default(VanillaMapTileLockedByParcel));
                }

                RestoreMapTileVisibility(entity);
                MarkUpdated(entity);
                return true;
            }

            if (wasUnlockedByParcel)
            {
                RestoreMapTileVisibility(entity);
                MarkUpdated(entity);
            }

            return false;
        }

        private int RestoreUnlockedMapTiles(string reason)
        {
            using var marked = _mUnlockedByParcelQuery.ToEntityArray(Allocator.Temp);
            var restored = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                if (RestoreUnlockedMapTile(marked[i]))
                {
                    restored++;
                }
            }

            if (restored > 0)
            {
                Mod.log.Info($"Parcel map tile unlock mask restored {restored} vanilla map tile(s) ({reason}).");
            }

            return restored;
        }

        private int RestoreLockedMapTiles(string reason)
        {
            using var marked = _mLockedByParcelQuery.ToEntityArray(Allocator.Temp);
            var unlocked = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                var entity = marked[i];
                if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
                {
                    continue;
                }

                EntityManager.RemoveComponent<VanillaMapTileLockedByParcel>(entity);
                if (EntityManager.HasComponent<Native>(entity))
                {
                    EntityManager.RemoveComponent<Native>(entity);
                    MarkUpdated(entity);
                    unlocked++;
                }
            }

            if (unlocked > 0)
            {
                Mod.log.Info($"Restored {unlocked} vanilla map tile(s) locked by parcel ownership sync ({reason}).");
            }

            return unlocked;
        }

        private bool RestoreUnlockedMapTile(Entity entity)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity))
            {
                return false;
            }

            EntityManager.RemoveComponent<VanillaMapTileUnlockedByParcel>(entity);
            RestoreMapTileVisibility(entity);
            if (!EntityManager.HasComponent<Native>(entity))
            {
                EntityManager.AddComponentData(entity, default(Native));
            }

            MarkUpdated(entity);
            return true;
        }

        private int DestroyLegacyBlockers(string reason)
        {
            using var blockers = _mLegacyBlockerQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < blockers.Length; i++)
            {
                EntityManager.DestroyEntity(blockers[i]);
            }

            if (blockers.Length > 0)
            {
                Mod.log.Info($"Destroyed {blockers.Length} legacy parcel MapTile blocker entity/entities ({reason}).");
            }

            return blockers.Length;
        }

        private void TryAlignDefaultParcelToVanillaOwnedTiles()
        {
            if (_mInitialVanillaBoundsChecked)
            {
                return;
            }

            using var entities = _mVanillaMapTileQuery.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
            {
                return;
            }

            _mInitialVanillaBoundsAttempts++;
            var found = false;
            var min = new float2(float.MaxValue, float.MaxValue);
            var max = new float2(float.MinValue, float.MinValue);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (EntityManager.HasComponent<Native>(entity) || !EntityManager.HasBuffer<Node>(entity))
                {
                    continue;
                }

                if (VanillaMapTileGeometry.TryGetBounds(EntityManager.GetBuffer<Node>(entity, true), out var tileMin, out var tileMax))
                {
                    min = math.min(min, tileMin);
                    max = math.max(max, tileMax);
                    found = true;
                }
            }

            if (!found)
            {
                if (_mInitialVanillaBoundsAttempts >= InitialVanillaBoundsRetryFrames)
                {
                    _mInitialVanillaBoundsChecked = true;
                    Mod.log.Warn(
                        $"Initial vanilla owned MapTile bounds not found after {_mInitialVanillaBoundsAttempts} attempt(s); default custom parcel remains unchanged. mapTileCandidates={entities.Length}, {_mParcelStoreSystem.GetSummary()}.");
                }
                else if (_mInitialVanillaBoundsAttempts == 1)
                {
                    Mod.log.Info(
                        $"Initial vanilla owned MapTile bounds not ready yet; retrying. mapTileCandidates={entities.Length}, {_mParcelStoreSystem.GetSummary()}.");
                }

                return;
            }

            _mInitialVanillaBoundsChecked = true;
            var changed = _mParcelStoreSystem.TryAlignDefaultParcelToBounds(
                min,
                max,
                "initial vanilla owned MapTile bounds");
            Mod.log.Info(
                $"Initial vanilla owned MapTile bounds checked: changed={changed}, bounds={ParcelGeometry.Format(min)}..{ParcelGeometry.Format(max)}, mapTileCandidates={entities.Length}, {_mParcelStoreSystem.GetSummary()}.");
        }

        private int RestoreMapTileVisibility(string reason)
        {
            using var marked = _mHiddenBySettingQuery.ToEntityArray(Allocator.Temp);
            var shown = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                if (RestoreMapTileVisibility(marked[i]))
                {
                    shown++;
                }
            }

            if (shown > 0)
            {
                Mod.log.Info($"Restored visibility for {shown} vanilla map tile(s) hidden by parcel setting ({reason}).");
            }

            return shown;
        }

        private int RestoreVisibleMapTilesOutsideBuildableBounds(float2 parcelMin, float2 parcelMax)
        {
            using var marked = _mHiddenBySettingQuery.ToEntityArray(Allocator.Temp);
            var shown = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                var entity = marked[i];
                var shouldStayHidden = EntityManager.Exists(entity)
                                       && EntityManager.HasComponent<MapTile>(entity)
                                       && !EntityManager.HasComponent<VanillaMapTileBlocker>(entity)
                                       && !ShouldShowVanillaUnlockedMapTileBorders()
                                       && TileOverlapsBuildableParcel(entity, parcelMin, parcelMax);
                if (!shouldStayHidden && RestoreMapTileVisibility(entity))
                {
                    shown++;
                }
            }

            return shown;
        }

        private bool ApplyMapTileVisibility(Entity entity)
        {
            if (ShouldShowVanillaUnlockedMapTileBorders())
            {
                return RestoreMapTileVisibility(entity);
            }

            if (!EntityManager.HasComponent<Hidden>(entity))
            {
                EntityManager.AddComponentData(entity, default(VanillaMapTileHiddenByParcelSetting));
                EntityManager.AddComponentData(entity, default(Hidden));
                MarkUpdated(entity);
                return true;
            }

            return false;
        }

        private bool RestoreMapTileVisibility(Entity entity)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<VanillaMapTileHiddenByParcelSetting>(entity))
            {
                return false;
            }

            EntityManager.RemoveComponent<VanillaMapTileHiddenByParcelSetting>(entity);
            if (EntityManager.HasComponent<Hidden>(entity))
            {
                EntityManager.RemoveComponent<Hidden>(entity);
                MarkUpdated(entity);
                return true;
            }

            MarkUpdated(entity);
            return false;
        }

        private static bool ShouldShowVanillaUnlockedMapTileBorders()
        {
            return Mod.Settings == null || Mod.Settings.ShowVanillaUnlockedMapTileBorders;
        }

        private void MarkUpdated(Entity entity)
        {
            if (!EntityManager.HasComponent<Updated>(entity))
            {
                EntityManager.AddComponentData(entity, default(Updated));
            }
        }

        private bool TileOverlapsBuildableParcel(Entity entity, float2 parcelMin, float2 parcelMax)
        {
            if (!EntityManager.HasBuffer<Node>(entity))
            {
                return false;
            }

            var nodes = EntityManager.GetBuffer<Node>(entity, true);
            return VanillaMapTileGeometry.TileOverlapsBuildableParcel(
                nodes,
                _mParcelStoreSystem.Parcels,
                parcelMin,
                parcelMax);
        }
    }
}
