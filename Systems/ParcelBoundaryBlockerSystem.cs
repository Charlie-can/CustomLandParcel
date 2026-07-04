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
    /// Creates native MapTile-shaped blockers around the MVP parcel so vanilla validation raises ExceedsCityLimits.
    /// </summary>
    public partial class ParcelBoundaryBlockerSystem : GameSystemBase
    {
        public struct ParcelBoundaryBlocker : IComponentData
        {
        }

        private EntityQuery m_MapTilePrefabQuery;
        private EntityQuery m_MapTileQuery;
        private EntityQuery m_BlockerQuery;
        private EntityQuery m_BlockerReadyQuery;
        private bool m_Created;
        private int m_VerificationFramesRemaining;

        protected override void OnCreate()
        {
            base.OnCreate();
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
            if (m_Created)
            {
                VerifyBlockersAfterCreation();
                return;
            }

            if (m_Created || m_MapTilePrefabQuery.IsEmptyIgnoreFilter || m_MapTileQuery.IsEmptyIgnoreFilter)
            {
                if (m_VerificationFramesRemaining == 0)
                {
                    Mod.log.Info(
                        $"Parcel blocker waiting: prefabQueryEmpty={m_MapTilePrefabQuery.IsEmptyIgnoreFilter}, mapTileQueryEmpty={m_MapTileQuery.IsEmptyIgnoreFilter}.");
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
                LogPrefabDiagnostics(prefab, prefabs.Length);
                CreateBlockers(prefab, worldMin, worldMax);
            }
            finally
            {
                prefabs.Dispose();
            }

            m_Created = true;
            m_VerificationFramesRemaining = 120;
            Mod.log.Info(
                $"Created vanilla MapTile-style blockers around MVP parcel. World bounds x/z {FormatFloat2(worldMin)}..{FormatFloat2(worldMax)}; parcel {FormatFloat2(ConstructionRestrictionSystem.ParcelMin)}..{FormatFloat2(ConstructionRestrictionSystem.ParcelMax)}.");
        }

        private void ClearExistingBlockers()
        {
            if (!m_BlockerQuery.IsEmptyIgnoreFilter)
            {
                Mod.log.Info(
                    $"Clearing {m_BlockerQuery.CalculateEntityCount()} existing parcel blocker entity/entities before recreation.");
                EntityManager.DestroyEntity(m_BlockerQuery);
            }
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
                    $"Parcel blocker world bounds source: {entities.Length} vanilla map tile entity/entities; x/z bounds {FormatFloat2(worldMin)}..{FormatFloat2(worldMax)}.");
                return math.all(worldMin < worldMax);
            }
            finally
            {
                entities.Dispose();
            }
        }

        private void CreateBlockers(Entity prefab, float2 worldMin, float2 worldMax)
        {
            var parcelMin = ConstructionRestrictionSystem.ParcelMin;
            var parcelMax = ConstructionRestrictionSystem.ParcelMax;
            CreateBlocker(prefab, new float2(worldMin.x, worldMin.y), new float2(parcelMin.x, worldMax.y));
            CreateBlocker(prefab, new float2(parcelMax.x, worldMin.y), new float2(worldMax.x, worldMax.y));
            CreateBlocker(prefab, new float2(parcelMin.x, worldMin.y), new float2(parcelMax.x, parcelMin.y));
            CreateBlocker(prefab, new float2(parcelMin.x, parcelMax.y), new float2(parcelMax.x, worldMax.y));
        }

        private void CreateBlocker(Entity prefab, float2 min, float2 max)
        {
            if (math.any(max - min <= 1f))
            {
                Mod.log.Warn(
                    $"Skipped parcel blocker rectangle with invalid or tiny size: min={FormatFloat2(min)}, max={FormatFloat2(max)}.");
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
                $"Created parcel blocker entity {FormatEntity(entity)} rect min={FormatFloat2(min)}, max={FormatFloat2(max)}, area={(max.x - min.x) * (max.y - min.y):F0}, components=Area+Node+Triangle+Geometry+PrefabRef+Native+Updated.");
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
                    $"Parcel blocker verification: totalMarked={m_BlockerQuery.CalculateEntityCount()}, readyForAreaSearch={m_BlockerReadyQuery.CalculateEntityCount()}, updatedStillPresent={CountBlockersWithUpdated()}.");
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

        private static string FormatFloat2(float2 value)
        {
            return $"({value.x:F1}, {value.y:F1})";
        }
    }
}