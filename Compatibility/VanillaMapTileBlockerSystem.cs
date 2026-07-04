using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Colossal.Mathematics;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using CustomLandParcel.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomLandParcel.Compatibility
{
    /// <summary>
    /// Compatibility layer that creates native MapTile-shaped blockers around active parcel union bounds.
    /// The game then uses its own area validation path to raise city-limit style errors outside parcels.
    /// </summary>
    public partial class VanillaMapTileBlockerSystem : GameSystemBase
    {
        private const int RebuildDelayFrames = 20;

        public struct VanillaMapTileBlocker : IComponentData
        {
        }

        public struct VanillaMapTileUnlockedByParcel : IComponentData
        {
        }

        private EntityQuery _mMapTilePrefabQuery;
        private EntityQuery _mMapTileQuery;
        private EntityQuery _mVanillaMapTileQuery;
        private EntityQuery _mBlockerQuery;
        private EntityQuery _mBlockerReadyQuery;
        private EntityQuery _mUnlockedByParcelQuery;
        private ParcelStoreSystem _mParcelStoreSystem;
        private bool _mCreated;
        private uint _mAppliedParcelVersion;
        private uint _mPendingParcelVersion;
        private int _mRebuildDelayFramesRemaining;
        private int _mVerificationFramesRemaining;
        private bool _mLastCompatibilityEnabled;
        private int _mDisabledLogCooldownFrames;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            _mMapTilePrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTileData>(),
                ComponentType.ReadOnly<AreaData>(),
                ComponentType.ReadOnly<PrefabData>());
            _mMapTileQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTile>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<VanillaMapTileBlocker>());
            _mVanillaMapTileQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTile>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<VanillaMapTileBlocker>());
            _mBlockerQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileBlocker>());
            _mUnlockedByParcelQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileUnlockedByParcel>());
            _mBlockerReadyQuery = GetEntityQuery(
                ComponentType.ReadOnly<VanillaMapTileBlocker>(),
                ComponentType.ReadOnly<Area>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Triangle>(),
                ComponentType.ReadOnly<Game.Areas.Geometry>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Native>());
            Mod.log.Info("VanillaMapTileBlockerSystem enabled as compatibility layer. Waiting for map tile prefab and map tile entities.");
        }

        protected override void OnDestroy()
        {
            RestoreUnlockedMapTiles("system destroyed");
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
                    $"VanillaMapTileBlockerSystem compatibility layer enabled by settings. {_mParcelStoreSystem.GetSummary()}.");
            }

            var currentVersion = _mParcelStoreSystem.Version;
            if (_mCreated && _mAppliedParcelVersion == currentVersion)
            {
                VerifyBlockersAfterCreation();
                return;
            }

            if (_mCreated && !ParcelChangeReadyToApply(currentVersion))
            {
                return;
            }

            if (_mMapTilePrefabQuery.IsEmptyIgnoreFilter || _mMapTileQuery.IsEmptyIgnoreFilter)
            {
                if (_mVerificationFramesRemaining == 0)
                {
                    Mod.log.Info(
                        $"Parcel blocker waiting: prefabQueryEmpty={_mMapTilePrefabQuery.IsEmptyIgnoreFilter}, mapTileQueryEmpty={_mMapTileQuery.IsEmptyIgnoreFilter}, {_mParcelStoreSystem.GetSummary()}.");
                    _mVerificationFramesRemaining = 120;
                }

                _mVerificationFramesRemaining--;
                return;
            }

            if (!TryGetWorldBounds(out var worldMin, out var worldMax))
            {
                return;
            }

            if (!_mParcelStoreSystem.TryGetActiveUnionBounds(out var parcelMin, out var parcelMax))
            {
                Mod.log.Warn($"Parcel blocker skipped: no parcel union bounds. {_mParcelStoreSystem.GetSummary()}.");
                return;
            }

            var prefabs = _mMapTilePrefabQuery.ToEntityArray(Allocator.Temp);
            try
            {
                var prefab = prefabs[0];
                LogPrefabDiagnostics(prefab, prefabs.Length);
                UpsertBlockers(prefab, worldMin, worldMax, parcelMin, parcelMax);
                RefreshUnlockedMapTiles(parcelMin, parcelMax);
                _mCreated = true;
                _mAppliedParcelVersion = currentVersion;
                _mPendingParcelVersion = 0;
                _mRebuildDelayFramesRemaining = 0;
                _mVerificationFramesRemaining = 120;
                Mod.log.Info(
                    $"Applied vanilla MapTile-style blockers around active parcel union. World bounds x/z {ParcelGeometry.Format(worldMin)}..{ParcelGeometry.Format(worldMax)}; activeParcelBounds={ParcelGeometry.Format(parcelMin)}..{ParcelGeometry.Format(parcelMax)}; {_mParcelStoreSystem.GetSummary()}.");
            }
            finally
            {
                prefabs.Dispose();
            }
        }

        private static bool IsCompatibilityEnabled()
        {
            return Mod.Settings != null && Mod.Settings.EnableVanillaMapTileCompatibility;
        }

        private void DisableCompatibilityLayer()
        {
            if (_mCreated || !_mBlockerQuery.IsEmptyIgnoreFilter || !_mUnlockedByParcelQuery.IsEmptyIgnoreFilter)
            {
                using var blockers = _mBlockerQuery.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < blockers.Length; i++)
                {
                    EntityManager.DestroyEntity(blockers[i]);
                }

                RestoreUnlockedMapTiles("compatibility disabled");
                Mod.log.Info(
                    $"VanillaMapTileBlockerSystem compatibility layer disabled; destroyed {blockers.Length} blocker entity/entities.");
            }
            else if (_mDisabledLogCooldownFrames <= 0)
            {
                Mod.log.Info(
                    "VanillaMapTileBlockerSystem compatibility layer is disabled by settings. Direct ConstructionRestrictionSystem remains the primary validation path.");
                _mDisabledLogCooldownFrames = 600;
            }

            _mCreated = false;
            _mLastCompatibilityEnabled = false;
            _mAppliedParcelVersion = 0;
            _mPendingParcelVersion = 0;
            _mRebuildDelayFramesRemaining = 0;
            _mVerificationFramesRemaining = 0;
            _mDisabledLogCooldownFrames--;
        }

        private bool ParcelChangeReadyToApply(uint currentVersion)
        {
            if (_mPendingParcelVersion != currentVersion)
            {
                _mPendingParcelVersion = currentVersion;
                _mRebuildDelayFramesRemaining = RebuildDelayFrames;
                Mod.log.Info(
                    $"Parcel blocker rebuild queued after parcel change. appliedVersion={_mAppliedParcelVersion}, pendingVersion={_mPendingParcelVersion}, delayFrames={RebuildDelayFrames}, {_mParcelStoreSystem.GetSummary()}.");
                return false;
            }

            if (_mRebuildDelayFramesRemaining > 0)
            {
                _mRebuildDelayFramesRemaining--;
                return false;
            }

            return true;
        }

        private bool TryGetWorldBounds(out float2 worldMin, out float2 worldMax)
        {
            worldMin = new float2(float.MaxValue, float.MaxValue);
            worldMax = new float2(float.MinValue, float.MinValue);

            var entities = _mMapTileQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (entities.Length == 0)
                {
                    Mod.log.Warn(
                        "Parcel blocker cannot compute world bounds yet: map tile entity query returned 0 entities.");
                    return false;
                }

                for (var i = 0; i < entities.Length; i++)
                {
                    var nodes = EntityManager.GetBuffer<Node>(entities[i], true);
                    for (var j = 0; j < nodes.Length; j++)
                    {
                        var xz = nodes[j].m_Position.xz;
                        worldMin = math.min(worldMin, xz);
                        worldMax = math.max(worldMax, xz);
                    }
                }

                Mod.log.Info(
                    $"Parcel blocker world bounds source: {entities.Length} vanilla map tile entity/entities; x/z bounds {ParcelGeometry.Format(worldMin)}..{ParcelGeometry.Format(worldMax)}.");
                return math.all(worldMin < worldMax);
            }
            finally
            {
                entities.Dispose();
            }
        }

        private void UpsertBlockers(Entity prefab, float2 worldMin, float2 worldMax, float2 parcelMin, float2 parcelMax)
        {
            using var existing = _mBlockerQuery.ToEntityArray(Allocator.Temp);
            var blockerCount = 0;

            UpsertBlocker(prefab, existing, blockerCount++, new float2(worldMin.x, worldMin.y), new float2(parcelMin.x, worldMax.y));
            UpsertBlocker(prefab, existing, blockerCount++, new float2(parcelMax.x, worldMin.y), new float2(worldMax.x, worldMax.y));
            UpsertBlocker(prefab, existing, blockerCount++, new float2(parcelMin.x, worldMin.y), new float2(parcelMax.x, parcelMin.y));
            UpsertBlocker(prefab, existing, blockerCount++, new float2(parcelMin.x, parcelMax.y), new float2(parcelMax.x, worldMax.y));

            for (var i = blockerCount; i < existing.Length; i++)
            {
                Mod.log.Warn(
                    $"Destroying unexpected extra parcel blocker entity {FormatEntity(existing[i])}. expected=4, actual={existing.Length}.");
                EntityManager.DestroyEntity(existing[i]);
            }
        }

        private void UpsertBlocker(Entity prefab, NativeArray<Entity> existing, int index, float2 min, float2 max)
        {
            if (math.any(max - min <= 1f))
            {
                Mod.log.Warn(
                    $"Skipped parcel blocker rectangle with invalid or tiny size: min={ParcelGeometry.Format(min)}, max={ParcelGeometry.Format(max)}.");
                return;
            }

            var created = index >= existing.Length;
            var entity = created ? EntityManager.CreateEntity() : existing[index];

            UpsertComponent(entity, new PrefabRef(prefab));
            UpsertComponent(entity, new Area(AreaFlags.Complete));
            UpsertComponent(entity, CreateGeometry(min, max));

            if (!EntityManager.HasComponent<Native>(entity))
            {
                EntityManager.AddComponentData(entity, default(Native));
            }

            if (!EntityManager.HasComponent<VanillaMapTileBlocker>(entity))
            {
                EntityManager.AddComponentData(entity, default(VanillaMapTileBlocker));
            }

            if (!EntityManager.HasComponent<Updated>(entity))
            {
                EntityManager.AddComponentData(entity, default(Updated));
            }

            var nodes = GetOrCreateBuffer<Node>(entity);
            nodes.ResizeUninitialized(4);
            nodes[0] = new Node(new float3(min.x, 0f, min.y), float.MinValue);
            nodes[1] = new Node(new float3(min.x, 0f, max.y), float.MinValue);
            nodes[2] = new Node(new float3(max.x, 0f, max.y), float.MinValue);
            nodes[3] = new Node(new float3(max.x, 0f, min.y), float.MinValue);

            var triangles = GetOrCreateBuffer<Triangle>(entity);
            triangles.ResizeUninitialized(2);
            triangles[0] = new Triangle(0, 1, 2);
            triangles[1] = new Triangle(0, 2, 3);

            Mod.log.Info(
                $"{(created ? "Created" : "Updated")} parcel blocker entity {FormatEntity(entity)} rect min={ParcelGeometry.Format(min)}, max={ParcelGeometry.Format(max)}, area={(max.x - min.x) * (max.y - min.y):F0}, components=Area+Node+Triangle+Geometry+PrefabRef+Native+Updated.");
        }

        private void UpsertComponent<T>(Entity entity, T value) where T : unmanaged, IComponentData
        {
            if (EntityManager.HasComponent<T>(entity))
            {
                EntityManager.SetComponentData(entity, value);
            }
            else
            {
                EntityManager.AddComponentData(entity, value);
            }
        }

        private DynamicBuffer<T> GetOrCreateBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            if (!EntityManager.HasBuffer<T>(entity))
            {
                return EntityManager.AddBuffer<T>(entity);
            }

            return EntityManager.GetBuffer<T>(entity);
        }

        private void LogPrefabDiagnostics(Entity prefab, int prefabCount)
        {
            if (!EntityManager.HasComponent<AreaGeometryData>(prefab))
            {
                Mod.log.Warn(
                    $"Parcel blocker selected prefab {FormatEntity(prefab)} from {prefabCount} candidate(s), but it has no AreaGeometryData component.");
                return;
            }

            var geometryData = EntityManager.GetComponentData<AreaGeometryData>(prefab);
            Mod.log.Info(
                $"Parcel blocker selected prefab {FormatEntity(prefab)} from {prefabCount} candidate(s): AreaGeometryData type={geometryData.m_Type}, flags={geometryData.m_Flags}, snapDistance={geometryData.m_SnapDistance:F2}, maxHeight={geometryData.m_MaxHeight:F2}.");
        }

        private void VerifyBlockersAfterCreation()
        {
            if (_mVerificationFramesRemaining <= 0)
            {
                return;
            }

            if (_mVerificationFramesRemaining == 120 || _mVerificationFramesRemaining == 60 ||
                _mVerificationFramesRemaining == 1)
            {
                Mod.log.Info(
                    $"Parcel blocker verification: totalMarked={_mBlockerQuery.CalculateEntityCount()}, readyForAreaSearch={_mBlockerReadyQuery.CalculateEntityCount()}, updatedStillPresent={CountBlockersWithUpdated()}, appliedVersion={_mAppliedParcelVersion}, {_mParcelStoreSystem.GetSummary()}.");
            }

            _mVerificationFramesRemaining--;
        }

        private int CountBlockersWithUpdated()
        {
            var entities = _mBlockerQuery.ToEntityArray(Allocator.Temp);
            try
            {
                var count = 0;
                for (var i = 0; i < entities.Length; i++)
                {
                    if (EntityManager.HasComponent<Updated>(entities[i]))
                    {
                        count++;
                    }
                }

                return count;
            }
            finally
            {
                entities.Dispose();
            }
        }

        private void RefreshUnlockedMapTiles(float2 parcelMin, float2 parcelMax)
        {
            var restored = RestoreStaleUnlockedMapTiles(parcelMin, parcelMax);
            var unlocked = 0;
            var alreadyUnlockedByParcel = 0;
            var alreadyVanillaOwned = 0;
            using var entities = _mVanillaMapTileQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!TileOverlapsPurchasedParcel(entity, parcelMin, parcelMax))
                {
                    continue;
                }

                if (EntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity))
                {
                    alreadyUnlockedByParcel++;
                    continue;
                }

                if (!EntityManager.HasComponent<Native>(entity))
                {
                    alreadyVanillaOwned++;
                    continue;
                }

                EntityManager.RemoveComponent<Native>(entity);
                EntityManager.AddComponentData(entity, default(VanillaMapTileUnlockedByParcel));
                MarkUpdated(entity);
                unlocked++;
            }

            if (unlocked > 0 || restored > 0)
            {
                Mod.log.Info(
                    $"Parcel map tile unlock mask refreshed: unlocked={unlocked}, restored={restored}, alreadyUnlockedByParcel={alreadyUnlockedByParcel}, alreadyVanillaOwned={alreadyVanillaOwned}, mapTileCandidates={entities.Length}, activeParcelBounds={ParcelGeometry.Format(parcelMin)}..{ParcelGeometry.Format(parcelMax)}, {_mParcelStoreSystem.GetSummary()}.");
            }
        }

        private int RestoreStaleUnlockedMapTiles(float2 parcelMin, float2 parcelMax)
        {
            var restored = 0;
            using var marked = _mUnlockedByParcelQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < marked.Length; i++)
            {
                var entity = marked[i];
                var shouldStayUnlocked = EntityManager.Exists(entity)
                                         && EntityManager.HasComponent<MapTile>(entity)
                                         && !EntityManager.HasComponent<Native>(entity)
                                         && !EntityManager.HasComponent<VanillaMapTileBlocker>(entity)
                                         && TileOverlapsPurchasedParcel(entity, parcelMin, parcelMax);
                if (!shouldStayUnlocked && RestoreUnlockedMapTile(entity))
                {
                    restored++;
                }
            }

            return restored;
        }

        private void RestoreUnlockedMapTiles(string reason)
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
        }

        private bool RestoreUnlockedMapTile(Entity entity)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity))
            {
                return false;
            }

            EntityManager.RemoveComponent<VanillaMapTileUnlockedByParcel>(entity);
            if (!EntityManager.HasComponent<Native>(entity))
            {
                EntityManager.AddComponentData(entity, default(Native));
            }

            MarkUpdated(entity);
            return true;
        }

        private void MarkUpdated(Entity entity)
        {
            if (!EntityManager.HasComponent<Updated>(entity))
            {
                EntityManager.AddComponentData(entity, default(Updated));
            }
        }

        private bool TileOverlapsPurchasedParcel(Entity entity, float2 parcelMin, float2 parcelMax)
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
                if (!parcel.IsPurchased || !PolygonMath.TryGetBounds(parcel.Points, out var currentMin, out var currentMax) ||
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

        private static Game.Areas.Geometry CreateGeometry(float2 min, float2 max)
        {
            var bounds = new Bounds3(new float3(min.x, 0f, min.y), new float3(max.x, 0f, max.y));
            return new Game.Areas.Geometry
            {
                m_Bounds = bounds,
                m_CenterPosition = new float3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f),
                m_SurfaceArea = math.max(0f, max.x - min.x) * math.max(0f, max.y - min.y)
            };
        }

        private static string FormatEntity(Entity entity)
        {
            return $"{entity.Index}:{entity.Version}";
        }
    }
}
