using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Colossal.Mathematics;
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

        private EntityQuery m_MapTilePrefabQuery;
        private EntityQuery m_MapTileQuery;
        private EntityQuery m_BlockerQuery;
        private EntityQuery m_BlockerReadyQuery;
        private ParcelStoreSystem m_ParcelStoreSystem;
        private bool m_Created;
        private uint m_AppliedParcelVersion;
        private uint m_PendingParcelVersion;
        private int m_RebuildDelayFramesRemaining;
        private int m_VerificationFramesRemaining;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            m_MapTilePrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTileData>(),
                ComponentType.ReadOnly<AreaData>(),
                ComponentType.ReadOnly<PrefabData>());
            m_MapTileQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTile>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<VanillaMapTileBlocker>());
            m_BlockerQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileBlocker>());
            m_BlockerReadyQuery = GetEntityQuery(
                ComponentType.ReadOnly<VanillaMapTileBlocker>(),
                ComponentType.ReadOnly<Area>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Triangle>(),
                ComponentType.ReadOnly<Game.Areas.Geometry>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Native>());
            Mod.log.Info("VanillaMapTileBlockerSystem enabled as compatibility layer. Waiting for map tile prefab and map tile entities.");
        }

        protected override void OnUpdate()
        {
            var currentVersion = m_ParcelStoreSystem.Version;
            if (m_Created && m_AppliedParcelVersion == currentVersion)
            {
                VerifyBlockersAfterCreation();
                return;
            }

            if (m_Created && !ParcelChangeReadyToApply(currentVersion))
            {
                return;
            }

            if (m_MapTilePrefabQuery.IsEmptyIgnoreFilter || m_MapTileQuery.IsEmptyIgnoreFilter)
            {
                if (m_VerificationFramesRemaining == 0)
                {
                    Mod.log.Info(
                        $"Parcel blocker waiting: prefabQueryEmpty={m_MapTilePrefabQuery.IsEmptyIgnoreFilter}, mapTileQueryEmpty={m_MapTileQuery.IsEmptyIgnoreFilter}, {m_ParcelStoreSystem.GetSummary()}.");
                    m_VerificationFramesRemaining = 120;
                }

                m_VerificationFramesRemaining--;
                return;
            }

            if (!TryGetWorldBounds(out var worldMin, out var worldMax))
            {
                return;
            }

            if (!m_ParcelStoreSystem.TryGetActiveUnionBounds(out var parcelMin, out var parcelMax))
            {
                Mod.log.Warn($"Parcel blocker skipped: no parcel union bounds. {m_ParcelStoreSystem.GetSummary()}.");
                return;
            }

            var prefabs = m_MapTilePrefabQuery.ToEntityArray(Allocator.Temp);
            try
            {
                var prefab = prefabs[0];
                LogPrefabDiagnostics(prefab, prefabs.Length);
                UpsertBlockers(prefab, worldMin, worldMax, parcelMin, parcelMax);
                m_Created = true;
                m_AppliedParcelVersion = currentVersion;
                m_PendingParcelVersion = 0;
                m_RebuildDelayFramesRemaining = 0;
                m_VerificationFramesRemaining = 120;
                Mod.log.Info(
                    $"Applied vanilla MapTile-style blockers around active parcel union. World bounds x/z {ParcelGeometry.Format(worldMin)}..{ParcelGeometry.Format(worldMax)}; activeParcelBounds={ParcelGeometry.Format(parcelMin)}..{ParcelGeometry.Format(parcelMax)}; {m_ParcelStoreSystem.GetSummary()}.");
            }
            finally
            {
                prefabs.Dispose();
            }
        }

        private bool ParcelChangeReadyToApply(uint currentVersion)
        {
            if (m_PendingParcelVersion != currentVersion)
            {
                m_PendingParcelVersion = currentVersion;
                m_RebuildDelayFramesRemaining = RebuildDelayFrames;
                Mod.log.Info(
                    $"Parcel blocker rebuild queued after parcel change. appliedVersion={m_AppliedParcelVersion}, pendingVersion={m_PendingParcelVersion}, delayFrames={RebuildDelayFrames}, {m_ParcelStoreSystem.GetSummary()}.");
                return false;
            }

            if (m_RebuildDelayFramesRemaining > 0)
            {
                m_RebuildDelayFramesRemaining--;
                return false;
            }

            return true;
        }

        private bool TryGetWorldBounds(out float2 worldMin, out float2 worldMax)
        {
            worldMin = new float2(float.MaxValue, float.MaxValue);
            worldMax = new float2(float.MinValue, float.MinValue);

            var entities = m_MapTileQuery.ToEntityArray(Allocator.Temp);
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
            using var existing = m_BlockerQuery.ToEntityArray(Allocator.Temp);
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
            if (m_VerificationFramesRemaining <= 0)
            {
                return;
            }

            if (m_VerificationFramesRemaining == 120 || m_VerificationFramesRemaining == 60 ||
                m_VerificationFramesRemaining == 1)
            {
                Mod.log.Info(
                    $"Parcel blocker verification: totalMarked={m_BlockerQuery.CalculateEntityCount()}, readyForAreaSearch={m_BlockerReadyQuery.CalculateEntityCount()}, updatedStillPresent={CountBlockersWithUpdated()}, appliedVersion={m_AppliedParcelVersion}, {m_ParcelStoreSystem.GetSummary()}.");
            }

            m_VerificationFramesRemaining--;
        }

        private int CountBlockersWithUpdated()
        {
            var entities = m_BlockerQuery.ToEntityArray(Allocator.Temp);
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
