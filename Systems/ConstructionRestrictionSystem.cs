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
    /// Marks temporary construction previews outside purchased custom parcels with the vanilla Error marker.
    /// </summary>
    public partial class ConstructionRestrictionSystem : GameSystemBase
    {
        private struct ParcelRestrictionError : IComponentData
        {
        }

        private EntityQuery m_ObjectPreviewQuery;
        private EntityQuery m_CurvePreviewQuery;
        private ParcelStoreSystem m_ParcelStoreSystem;
        private int m_LastInvalidCount = -1;
        private int m_FramesSinceLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();

            m_ObjectPreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Transform>(),
                ComponentType.Exclude<Deleted>());

            m_CurvePreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.Exclude<Deleted>());

            Mod.log.Info($"ConstructionRestrictionSystem enabled. {m_ParcelStoreSystem.GetSummary()}.");
        }

        protected override void OnUpdate()
        {
            var invalidCount = 0;
            invalidCount += RestrictObjectPreviews();
            invalidCount += RestrictCurvePreviews();

            m_FramesSinceLog++;
            if (invalidCount != m_LastInvalidCount && (invalidCount == 0 || m_FramesSinceLog >= 30))
            {
                m_LastInvalidCount = invalidCount;
                m_FramesSinceLog = 0;
                Mod.log.Info(
                    $"Parcel validation: {invalidCount} active construction preview entity/entities outside purchased parcels. {m_ParcelStoreSystem.GetSummary()}.");
            }
        }

        private int RestrictObjectPreviews()
        {
            var entities = m_ObjectPreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = m_ObjectPreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var transforms = m_ObjectPreviewQuery.ToComponentDataArray<Transform>(Allocator.Temp);

            try
            {
                var invalidCount = 0;
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var temp = temps[i];
                    if (!ShouldValidate(temp))
                    {
                        ClearOwnError(entity);
                        continue;
                    }

                    var position = transforms[i].m_Position;
                    var valid = m_ParcelStoreSystem.IsBuildable(new float2(position.x, position.z));
                    SetRestrictionError(entity, !valid);

                    if (!valid)
                    {
                        invalidCount++;
                    }
                }

                return invalidCount;
            }
            finally
            {
                entities.Dispose();
                temps.Dispose();
                transforms.Dispose();
            }
        }

        private int RestrictCurvePreviews()
        {
            var entities = m_CurvePreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = m_CurvePreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var curves = m_CurvePreviewQuery.ToComponentDataArray<Curve>(Allocator.Temp);

            try
            {
                var invalidCount = 0;
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var temp = temps[i];
                    if (!ShouldValidate(temp))
                    {
                        ClearOwnError(entity);
                        continue;
                    }

                    var curve = curves[i].m_Bezier;
                    var valid = CurveInsideParcel(curve);
                    SetRestrictionError(entity, !valid);

                    if (!valid)
                    {
                        invalidCount++;
                    }
                }

                return invalidCount;
            }
            finally
            {
                entities.Dispose();
                temps.Dispose();
                curves.Dispose();
            }
        }

        private static bool ShouldValidate(Temp temp)
        {
            const TempFlags applyFlags = TempFlags.Create | TempFlags.Modify | TempFlags.Replace | TempFlags.Upgrade;
            if ((temp.m_Flags & applyFlags) == 0)
            {
                return false;
            }

            if ((temp.m_Flags & (TempFlags.Hidden | TempFlags.Delete | TempFlags.Cancel | TempFlags.Select |
                                 TempFlags.Optional)) != 0)
            {
                return false;
            }

            return (temp.m_Flags & (TempFlags.Essential | TempFlags.IsLast)) != 0;
        }

        private void SetRestrictionError(Entity entity, bool blocked)
        {
            var hasOwnError = EntityManager.HasComponent<ParcelRestrictionError>(entity);

            if (blocked)
            {
                if (!EntityManager.HasComponent<Error>(entity))
                {
                    EntityManager.AddComponent<Error>(entity);
                }

                if (!hasOwnError)
                {
                    EntityManager.AddComponent<ParcelRestrictionError>(entity);
                }
            }
            else if (hasOwnError)
            {
                ClearOwnError(entity);
            }
        }

        private void ClearOwnError(Entity entity)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<ParcelRestrictionError>(entity))
            {
                return;
            }

            EntityManager.RemoveComponent<ParcelRestrictionError>(entity);
            if (EntityManager.HasComponent<Error>(entity))
            {
                EntityManager.RemoveComponent<Error>(entity);
            }
        }

        private bool CurveInsideParcel(Colossal.Mathematics.Bezier4x3 curve)
        {
            for (var i = 0; i <= 8; i++)
            {
                var t = i / 8f;
                var position = EvaluateBezier(curve, t);
                if (!m_ParcelStoreSystem.IsBuildable(new float2(position.x, position.z)))
                {
                    return false;
                }
            }

            return true;
        }

        private static float3 EvaluateBezier(Colossal.Mathematics.Bezier4x3 curve, float t)
        {
            var u = 1f - t;
            return u * u * u * curve.a
                   + 3f * u * u * t * curve.b
                   + 3f * u * t * t * curve.c
                   + t * t * t * curve.d;
        }
    }
}
