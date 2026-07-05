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
        private VanillaMapTileOwnershipSync _mOwnershipSync;
        private VanillaMapTileVisibilitySync _mVisibilitySync;
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
            _mVisibilitySync = new VanillaMapTileVisibilitySync(
                EntityManager,
                MarkUpdated,
                ShouldShowVanillaUnlockedMapTileBorders);
            _mOwnershipSync = new VanillaMapTileOwnershipSync(
                EntityManager,
                MarkUpdated,
                _mVisibilitySync.RestoreMapTileVisibility);
            Mod.log.Info("VanillaMapTileUnlockSystem enabled as vanilla MapTile unlock compatibility layer.");
        }

        protected override void OnDestroy()
        {
            _mVisibilitySync.RestoreMapTileVisibility(_mHiddenBySettingQuery, "system destroyed");
            _mOwnershipSync.RestoreLockedMapTiles(_mLockedByParcelQuery, "system destroyed");
            _mOwnershipSync.RestoreUnlockedMapTiles(_mUnlockedByParcelQuery, "system destroyed");
            DestroyLegacyBlockers("system destroyed");
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (_mUnlockMaskRefreshFramesRemaining > 0)
            {
                _mUnlockMaskRefreshFramesRemaining--;
                return;
            }

            if (!IsCompatibilityEnabled())
            {
                DisableCompatibilityLayer();
                _mUnlockMaskRefreshFramesRemaining = UnlockMaskRefreshFrames;
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
            var unlockedTiles = _mOwnershipSync.RestoreLockedMapTiles(_mLockedByParcelQuery, "compatibility disabled");
            var restoredTiles = _mOwnershipSync.RestoreUnlockedMapTiles(_mUnlockedByParcelQuery, "compatibility disabled");
            if (destroyedBlockers > 0 || restoredTiles > 0 || unlockedTiles > 0)
            {
                Mod.log.Info(
                    $"Vanilla MapTile unlock compatibility disabled by settings; destroyedLegacyBlockers={destroyedBlockers}, restoredTiles={restoredTiles}, unlockedTiles={unlockedTiles}, showVanillaBorders={ShouldShowVanillaUnlockedMapTileBorders()}.");
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

            _mOwnershipSync.RestoreLockedMapTiles(_mLockedByParcelQuery, "no buildable custom parcel union");
            _mOwnershipSync.RestoreUnlockedMapTiles(_mUnlockedByParcelQuery, "no buildable custom parcel union");
        }

        private void RefreshUnlockedMapTiles(float2 parcelMin, float2 parcelMax)
        {
            var unlocked = 0;
            var locked = 0;
            var alreadyUnlockedByParcel = 0;
            var alreadyVanillaOwned = 0;
            var overlapCandidates = 0;
            using var entities = _mVanillaMapTileQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!TileOverlapsBuildableParcel(entity, parcelMin, parcelMax))
                {
                    if (_mOwnershipSync.LockMapTileOutsideCustomParcel(entity))
                    {
                        locked++;
                    }

                    continue;
                }

                overlapCandidates++;
                switch (_mOwnershipSync.ApplyBuildableOwnership(entity))
                {
                    case VanillaMapTileOwnershipResult.AlreadyUnlockedByParcel:
                        alreadyUnlockedByParcel++;
                        break;
                    case VanillaMapTileOwnershipResult.AlreadyVanillaOwned:
                        alreadyVanillaOwned++;
                        break;
                    case VanillaMapTileOwnershipResult.UnlockedByParcel:
                        unlocked++;
                        break;
                }
            }

            if (unlocked > 0 || locked > 0)
            {
                Mod.log.Info(
                    $"Parcel map tile ownership synchronized: unlockedInside={unlocked}, lockedOutside={locked}, overlapCandidates={overlapCandidates}, alreadyUnlockedByParcel={alreadyUnlockedByParcel}, alreadyVanillaOwnedInside={alreadyVanillaOwned}, mapTileCandidates={entities.Length}, buildableBounds={ParcelGeometry.Format(parcelMin)}..{ParcelGeometry.Format(parcelMax)}, showVanillaBorders={ShouldShowVanillaUnlockedMapTileBorders()}, {_mParcelStoreSystem.GetSummary()}.");
            }
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
