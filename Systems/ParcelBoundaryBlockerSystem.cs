using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Colossal.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Creates native MapTile-shaped blockers around the current parcel so vanilla validation raises ExceedsCityLimits.
    /// </summary>
    public partial class ParcelBoundaryBlockerSystem : GameSystemBase
    {
        private const int RebuildDelayFrames = 20;

        public struct ParcelBoundaryBlocker : IComponentData
        {
        }

        private EntityQuery m_MapTilePrefabQuery;
        private EntityQuery m_MapTileQuery;
        private EntityQuery m_BlockerQuery;
        private EntityQuery m_BlockerReadyQuery;
        private ParcelBoundsSystem m_ParcelBoundsSystem;
        private bool m_Created;
        private uint m_AppliedParcelVersion;
        private uint m_PendingParcelVersion;
        private int m_RebuildDelayFramesRemaining;
        private int m_VerificationFramesRemaining;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ParcelBoundsSystem = World.GetOrCreateSystemManaged<ParcelBoundsSystem>();
            m_MapTilePrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTileData>(),
                ComponentType.ReadOnly<AreaData>(),
                ComponentType.ReadOnly<PrefabData>());
            m_MapTileQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapTile>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<ParcelBoundaryBlocker>());
            m_BlockerQuery = GetEntityQuery(ComponentType.ReadOnly<ParcelBoundaryBlocker>());
            m_BlockerReadyQuery = GetEntityQuery(
                ComponentType.ReadOnly<ParcelBoundaryBlocker>(),
                ComponentType.ReadOnly<Area>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Triangle>(),
                ComponentType.ReadOnly<Geometry>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Native>());
            Mod.log.Info("ParcelBoundaryBlockerSystem enabled. Waiting for map tile prefab and map tile entities.");
        }

        protected override void OnUpdate()
        {
            var currentVersion = m_ParcelBoundsSystem.Version;
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
                        $"Parcel blocker waiting: prefabQueryEmpty={m_MapTilePrefabQuery.IsEmptyIgnoreFilter}, mapTileQueryEmpty={m_MapTileQuery.IsEmptyIgnoreFilter}, parcel={m_ParcelBoundsSystem.Bounds}, parcelVersion={currentVersion}.");
                    m_VerificationFramesRemaining = 120;
                }

                m_VerificationFramesRemaining--;
                return;
            }

            ClearExistingBlockers();

            if (!TryGetWorldBounds(out var worldMin, out var worldMax))
            {
                return;
            }

            var prefabs = m_MapTilePrefabQuery.ToEntityArray(Allocator.Temp);
            try
            {
                var prefab = prefabs[0];
                var parcel = m_ParcelBoundsSystem.Bounds;
                LogPrefabDiagnostics(prefab, prefabs.Length);
                CreateBlockers(prefab, worldMin, worldMax, parcel);
                m_Created = true;
                m_AppliedParcelVersion = currentVersion;
                m_PendingParcelVersion = 0;
                m_RebuildDelayFramesRemaining = 0;
                m_VerificationFramesRemaining = 120;
                Mod.log.Info(
                    $"Created vanilla MapTile-style blockers around parcel. World bounds x/z {ParcelBounds.Format(worldMin)}..{ParcelBounds.Format(worldMax)}; parcel {parcel}; parcelVersion={m_AppliedParcelVersion}.");
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
                    $"Parcel blocker rebuild queued after parcel change. appliedVersion={m_AppliedParcelVersion}, pendingVersion={m_PendingParcelVersion}, delayFrames={RebuildDelayFrames}, parcel={m_ParcelBoundsSystem.Bounds}.");
                return false;
            }

            if (m_RebuildDelayFramesRemaining > 0)
            {
                m_RebuildDelayFramesRemaining--;
                return false;
            }

            return true;
        }

        private void ClearExistingBlockers()
        {
            if (!m_BlockerQuery.IsEmptyIgnoreFilter)
            {
                Mod.log.Info(
                    $"Clearing {m_BlockerQuery.CalculateEntityCount()} existing parcel blocker entity/entities before recreation. previousVersion={m_AppliedParcelVersion}, nextVersion={m_ParcelBoundsSystem.Version}.");
                EntityManager.DestroyEntity(m_BlockerQuery);
            }

            m_Created = false;
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
                    $"Parcel blocker world bounds source: {entities.Length} vanilla map tile entity/entities; x/z bounds {ParcelBounds.Format(worldMin)}..{ParcelBounds.Format(worldMax)}.");
                return math.all(worldMin < worldMax);
            }
            finally
            {
                entities.Dispose();
            }
        }

        private void CreateBlockers(Entity prefab, float2 worldMin, float2 worldMax, ParcelBounds parcel)
        {
            CreateBlocker(prefab, new float2(worldMin.x, worldMin.y), new float2(parcel.Min.x, worldMax.y));
            CreateBlocker(prefab, new float2(parcel.Max.x, worldMin.y), new float2(worldMax.x, worldMax.y));
            CreateBlocker(prefab, new float2(parcel.Min.x, worldMin.y), new float2(parcel.Max.x, parcel.Min.y));
            CreateBlocker(prefab, new float2(parcel.Min.x, parcel.Max.y), new float2(parcel.Max.x, worldMax.y));
        }

        private void CreateBlocker(Entity prefab, float2 min, float2 max)
        {
            if (math.any(max - min <= 1f))
            {
                Mod.log.Warn(
                    $"Skipped parcel blocker rectangle with invalid or tiny size: min={ParcelBounds.Format(min)}, max={ParcelBounds.Format(max)}.");
                return;
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new PrefabRef(prefab));
            EntityManager.AddComponentData(entity, new Area(AreaFlags.Complete));
            EntityManager.AddBuffer<Node>(entity);
            EntityManager.AddBuffer<Triangle>(entity);
            EntityManager.AddComponentData(entity, CreateGeometry(min, max));

            if (!EntityManager.HasComponent<Native>(entity))
            {
                EntityManager.AddComponentData(entity, default(Native));
            }

            EntityManager.AddComponentData(entity, default(ParcelBoundaryBlocker));
            EntityManager.AddComponentData(entity, default(Updated));

            var nodes = EntityManager.GetBuffer<Node>(entity);
            nodes.ResizeUninitialized(4);
            nodes[0] = new Node(new float3(min.x, 0f, min.y), float.MinValue);
            nodes[1] = new Node(new float3(min.x, 0f, max.y), float.MinValue);
            nodes[2] = new Node(new float3(max.x, 0f, max.y), float.MinValue);
            nodes[3] = new Node(new float3(max.x, 0f, min.y), float.MinValue);

            var triangles = EntityManager.GetBuffer<Triangle>(entity);
            triangles.ResizeUninitialized(2);
            triangles[0] = new Triangle(0, 1, 2);
            triangles[1] = new Triangle(0, 2, 3);

            Mod.log.Info(
                $"Created parcel blocker entity {FormatEntity(entity)} rect min={ParcelBounds.Format(min)}, max={ParcelBounds.Format(max)}, area={(max.x - min.x) * (max.y - min.y):F0}, components=Area+Node+Triangle+Geometry+PrefabRef+Native+Updated.");
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
                    $"Parcel blocker verification: totalMarked={m_BlockerQuery.CalculateEntityCount()}, readyForAreaSearch={m_BlockerReadyQuery.CalculateEntityCount()}, updatedStillPresent={CountBlockersWithUpdated()}, parcel={m_ParcelBoundsSystem.Bounds}, parcelVersion={m_AppliedParcelVersion}.");
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

        private static Geometry CreateGeometry(float2 min, float2 max)
        {
            var bounds = new Bounds3(new float3(min.x, 0f, min.y), new float3(max.x, 0f, max.y));
            return new Geometry
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
