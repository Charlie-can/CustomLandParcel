using Colossal.Mathematics;
using CustomLandParcel.Geometry;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Cancels tool delete definitions outside active custom parcels before vanilla ApplyTool systems consume them.
    /// </summary>
    public partial class ParcelDeletionRestrictionSystem : GameSystemBase
    {
        private EntityQuery _mDeleteDefinitionQuery;
        private ParcelStoreSystem _mParcelStoreSystem;
        private int _mLastBlockedCount = -1;
        private int _mFramesSinceLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            _mDeleteDefinitionQuery = GetEntityQuery(
                ComponentType.ReadOnly<CreationDefinition>(),
                ComponentType.Exclude<Deleted>());
            RequireForUpdate(_mDeleteDefinitionQuery);
            Mod.log.Info($"ParcelDeletionRestrictionSystem enabled at ApplyTool. {_mParcelStoreSystem.GetSummary()}.");
        }

        protected override void OnUpdate()
        {
            var entities = _mDeleteDefinitionQuery.ToEntityArray(Allocator.Temp);
            var definitions = _mDeleteDefinitionQuery.ToComponentDataArray<CreationDefinition>(Allocator.Temp);

            try
            {
                var deleteCount = 0;
                var blockedCount = 0;
                var firstBlocked = string.Empty;

                for (var i = 0; i < entities.Length; i++)
                {
                    var definition = definitions[i];
                    if ((definition.m_Flags & CreationFlags.Delete) == 0)
                    {
                        continue;
                    }

                    deleteCount++;
                    if (IsDeletionInsideParcel(entities[i], definition, out var samplePoint, out var sampleKind))
                    {
                        continue;
                    }

                    EntityManager.DestroyEntity(entities[i]);
                    blockedCount++;
                    if (firstBlocked.Length == 0)
                    {
                        firstBlocked =
                            $"definition={FormatEntity(entities[i])}, original={FormatEntity(definition.m_Original)}, kind={sampleKind}, point={ParcelGeometry.Format(samplePoint)}";
                    }
                }

                LogResult(deleteCount, blockedCount, firstBlocked);
            }
            finally
            {
                entities.Dispose();
                definitions.Dispose();
            }
        }

        private bool IsDeletionInsideParcel(
            Entity definitionEntity,
            CreationDefinition definition,
            out float2 samplePoint,
            out string sampleKind)
        {
            if (EntityManager.HasComponent<ObjectDefinition>(definitionEntity))
            {
                var objectDefinition = EntityManager.GetComponentData<ObjectDefinition>(definitionEntity);
                samplePoint = objectDefinition.m_Position.xz;
                sampleKind = "object-definition";
                return _mParcelStoreSystem.IsBuildable(samplePoint);
            }

            if (EntityManager.HasComponent<NetCourse>(definitionEntity))
            {
                var course = EntityManager.GetComponentData<NetCourse>(definitionEntity);
                sampleKind = "net-definition";
                return PlacementPreviewUtility.TryValidateCurveInsideParcel(
                    course.m_Curve,
                    _mParcelStoreSystem,
                    out var outsidePoint,
                    out _)
                    ? SetInside(out samplePoint)
                    : SetOutside(out samplePoint, outsidePoint.xz);
            }

            if (EntityManager.HasBuffer<Game.Areas.Node>(definitionEntity))
            {
                sampleKind = "area-definition";
                return AreaNodesInsideParcel(
                    EntityManager.GetBuffer<Game.Areas.Node>(definitionEntity, isReadOnly: true),
                    out samplePoint);
            }

            if (definition.m_Original != Entity.Null && EntityManager.Exists(definition.m_Original))
            {
                return IsOriginalInsideParcel(definition.m_Original, out samplePoint, out sampleKind);
            }

            samplePoint = default;
            sampleKind = "unknown";
            return false;
        }

        private bool IsOriginalInsideParcel(Entity original, out float2 samplePoint, out string sampleKind)
        {
            if (EntityManager.HasComponent<Transform>(original))
            {
                var transform = EntityManager.GetComponentData<Transform>(original);
                samplePoint = transform.m_Position.xz;
                sampleKind = "object-original";
                return _mParcelStoreSystem.IsBuildable(samplePoint);
            }

            if (EntityManager.HasComponent<Curve>(original))
            {
                var curve = EntityManager.GetComponentData<Curve>(original);
                sampleKind = "curve-original";
                return PlacementPreviewUtility.TryValidateCurveInsideParcel(
                    curve.m_Bezier,
                    _mParcelStoreSystem,
                    out var outsidePoint,
                    out _)
                    ? SetInside(out samplePoint)
                    : SetOutside(out samplePoint, outsidePoint.xz);
            }

            if (EntityManager.HasComponent<Game.Net.Node>(original))
            {
                var node = EntityManager.GetComponentData<Game.Net.Node>(original);
                samplePoint = node.m_Position.xz;
                sampleKind = "node-original";
                return _mParcelStoreSystem.IsBuildable(samplePoint);
            }

            if (EntityManager.HasBuffer<Game.Areas.Node>(original))
            {
                sampleKind = "area-original";
                return AreaNodesInsideParcel(
                    EntityManager.GetBuffer<Game.Areas.Node>(original, isReadOnly: true),
                    out samplePoint);
            }

            samplePoint = default;
            sampleKind = "unknown-original";
            return false;
        }

        private bool AreaNodesInsideParcel(DynamicBuffer<Game.Areas.Node> nodes, out float2 samplePoint)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                samplePoint = nodes[i].m_Position.xz;
                if (!_mParcelStoreSystem.IsBuildable(samplePoint))
                {
                    return false;
                }
            }

            samplePoint = nodes.Length == 0 ? default : nodes[0].m_Position.xz;
            return nodes.Length > 0;
        }

        private void LogResult(int deleteCount, int blockedCount, string firstBlocked)
        {
            _mFramesSinceLog++;
            if (blockedCount == _mLastBlockedCount && (blockedCount == 0 || _mFramesSinceLog < 30))
            {
                return;
            }

            _mLastBlockedCount = blockedCount;
            _mFramesSinceLog = 0;
            if (blockedCount > 0)
            {
                Mod.log.Info(
                    $"Parcel deletion validation blocked {blockedCount}/{deleteCount} delete definition(s) outside active parcels; firstBlocked={firstBlocked}. {_mParcelStoreSystem.GetSummary()}.");
            }
            else if (deleteCount > 0)
            {
                Mod.log.Info(
                    $"Parcel deletion validation allowed {deleteCount} delete definition(s) inside active parcels. {_mParcelStoreSystem.GetSummary()}.");
            }
        }

        private static bool SetInside(out float2 samplePoint)
        {
            samplePoint = default;
            return true;
        }

        private static bool SetOutside(out float2 samplePoint, float2 point)
        {
            samplePoint = point;
            return false;
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null ? "null" : $"{entity.Index}:{entity.Version}";
        }
    }
}
