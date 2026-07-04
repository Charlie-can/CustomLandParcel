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
        private bool m_Created;

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
            Mod.log.Info("ParcelBoundaryBlockerSystem enabled. Waiting for map tiles.");
        }

        protected override void OnUpdate()
        {
            if (m_Created || m_MapTilePrefabQuery.IsEmptyIgnoreFilter || m_MapTileQuery.IsEmptyIgnoreFilter)
            {
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
                CreateBlockers(prefab, worldMin, worldMax);
            }
            finally
            {
                prefabs.Dispose();
            }

            m_Created = true;
            Mod.log.Info(
                $"Created vanilla MapTile blockers around MVP parcel. World bounds x/z {worldMin}..{worldMax}.");
        }

        private void ClearExistingBlockers()
        {
            if (!m_BlockerQuery.IsEmptyIgnoreFilter)
            {
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
    }
}