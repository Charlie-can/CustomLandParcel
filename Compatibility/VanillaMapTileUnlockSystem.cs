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

        public struct VanillaMapTileBlocker : IComponentData
        {
        }

        public struct VanillaMapTileUnlockedByParcel : IComponentData
        {
        }

        public struct VanillaMapTileLockedByParcel : IComponentData
        {
        }

        public struct VanillaMapTileHiddenByParcelSetting : IComponentData
        {
        }

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
            LockAllUnownedMapTilesOutsideCustomParcels("no buildable custom parcel union");
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

        private void LockAllUnownedMapTilesOutsideCustomParcels(string reason)
        {
            using var entities = _mVanillaMapTileQuery.ToEntityArray(Allocator.Temp);
            var locked = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (LockMapTileOutsideCustomParcel(entities[i]))
                {
                    locked++;
                }
            }

            if (locked > 0)
            {
                Mod.log.Info($"Locked {locked} vanilla map tile(s) because there is no buildable custom parcel union ({reason}).");
            }
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

            _mInitialVanillaBoundsChecked = true;
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

                var nodes = EntityManager.GetBuffer<Node>(entity, true);
                for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                {
                    var point = nodes[nodeIndex].m_Position.xz;
                    min = math.min(min, point);
                    max = math.max(max, point);
                    found = true;
                }
            }

            if (!found)
            {
                Mod.log.Info(
                    $"Initial vanilla owned MapTile bounds not found; default custom parcel remains unchanged. mapTileCandidates={entities.Length}, {_mParcelStoreSystem.GetSummary()}.");
                return;
            }

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
            if (nodes.Length == 0)
            {
                return false;
            }

            var tileMin = new float2(float.MaxValue, float.MaxValue);
            var tileMax = new float2(float.MinValue, float.MinValue);
            for (var i = 0; i < nodes.Length; i++)
            {
                var xz = nodes[i].m_Position.xz;
                tileMin = math.min(tileMin, xz);
                tileMax = math.max(tileMax, xz);
            }

            if (!BoundsIntersect(tileMin, tileMax, parcelMin, parcelMax))
            {
                return false;
            }

            for (var i = 0; i < _mParcelStoreSystem.Parcels.Count; i++)
            {
                var parcel = _mParcelStoreSystem.Parcels[i];
                if (!parcel.IsBuildable || !PolygonMath.TryGetBounds(parcel.Points, out var currentMin, out var currentMax) ||
                    !BoundsIntersect(tileMin, tileMax, currentMin, currentMax))
                {
                    continue;
                }

                if (PolygonMath.ContainsPoint(parcel.Points, (tileMin + tileMax) * 0.5f))
                {
                    return true;
                }

                for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                {
                    if (PolygonMath.ContainsPoint(parcel.Points, nodes[nodeIndex].m_Position.xz))
                    {
                        return true;
                    }
                }

                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    if (PointInsideBounds(parcel.Points[pointIndex], tileMin, tileMax))
                    {
                        return true;
                    }
                }

                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    var a = parcel.Points[pointIndex];
                    var b = parcel.Points[(pointIndex + 1) % parcel.Points.Count];
                    if (SegmentIntersectsBounds(a, b, tileMin, tileMax))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool BoundsIntersect(float2 aMin, float2 aMax, float2 bMin, float2 bMax)
        {
            return math.all(aMin <= bMax) && math.all(bMin <= aMax);
        }

        private static bool PointInsideBounds(float2 point, float2 min, float2 max)
        {
            return math.all(point >= min) && math.all(point <= max);
        }

        private static bool SegmentIntersectsBounds(float2 start, float2 end, float2 min, float2 max)
        {
            if (PointInsideBounds(start, min, max) || PointInsideBounds(end, min, max))
            {
                return true;
            }

            var bottomLeft = new float2(min.x, min.y);
            var topLeft = new float2(min.x, max.y);
            var topRight = new float2(max.x, max.y);
            var bottomRight = new float2(max.x, min.y);
            return SegmentsIntersect(start, end, bottomLeft, topLeft)
                   || SegmentsIntersect(start, end, topLeft, topRight)
                   || SegmentsIntersect(start, end, topRight, bottomRight)
                   || SegmentsIntersect(start, end, bottomRight, bottomLeft);
        }

        private static bool SegmentsIntersect(float2 a, float2 b, float2 c, float2 d)
        {
            const float epsilon = 0.001f;
            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon)) &&
                ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon)))
            {
                return true;
            }

            return math.abs(abC) <= epsilon && PointOnSegment(c, a, b)
                   || math.abs(abD) <= epsilon && PointOnSegment(d, a, b)
                   || math.abs(cdA) <= epsilon && PointOnSegment(a, c, d)
                   || math.abs(cdB) <= epsilon && PointOnSegment(b, c, d);
        }

        private static bool PointOnSegment(float2 point, float2 start, float2 end)
        {
            return point.x >= math.min(start.x, end.x) - 0.001f
                   && point.x <= math.max(start.x, end.x) + 0.001f
                   && point.y >= math.min(start.y, end.y) - 0.001f
                   && point.y <= math.max(start.y, end.y) + 0.001f;
        }

        private static float Cross(float2 origin, float2 a, float2 b)
        {
            var oa = a - origin;
            var ob = b - origin;
            return oa.x * ob.y - oa.y * ob.x;
        }
    }
}
