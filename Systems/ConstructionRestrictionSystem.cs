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
    /// MVP validation system: treats a fixed rectangle near the map center as the only purchased parcel.
    /// Temporary construction previews outside that rectangle are marked with the vanilla Error marker.
    /// </summary>
    public partial class ConstructionRestrictionSystem : GameSystemBase
    {
        private struct ParcelRestrictionError : IComponentData
        {
        }

        private EntityQuery m_ObjectPreviewQuery;
        private EntityQuery m_CurvePreviewQuery;
        private int m_LastInvalidCount = -1;
        private int m_FramesSinceLog;

        internal static readonly float2 ParcelMin = new float2(-500f, -500f);
        internal static readonly float2 ParcelMax = new float2(500f, 500f);

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ObjectPreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Transform>(),
                ComponentType.Exclude<Deleted>());

            m_CurvePreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.Exclude<Deleted>());

            Mod.log.Info("ConstructionRestrictionSystem enabled. MVP buildable rectangle: x/z -500..500");
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
                    $"Parcel validation: {invalidCount} active construction preview entity/entities outside MVP parcel.");
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
                    var valid = Contains(new float2(position.x, position.z));
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

        private static bool CurveInsideParcel(Colossal.Mathematics.Bezier4x3 curve)
        {
            for (var i = 0; i <= 8; i++)
            {
                var t = i / 8f;
                var position = EvaluateBezier(curve, t);
                if (!Contains(new float2(position.x, position.z)))
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

        internal static bool Contains(float2 point)
        {
            return point.x >= ParcelMin.x && point.x <= ParcelMax.x && point.y >= ParcelMin.y && point.y <= ParcelMax.y;
        }
    }
}