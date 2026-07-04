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

        private EntityQuery _mObjectPreviewQuery;
        private EntityQuery _mCurvePreviewQuery;
        private ParcelStoreSystem _mParcelStoreSystem;
        private int _mLastInvalidCount = -1;
        private int _mFramesSinceLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();

            _mObjectPreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Transform>(),
                ComponentType.Exclude<Deleted>());

            _mCurvePreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.Exclude<Deleted>());

            Mod.log.Info($"ConstructionRestrictionSystem enabled. {_mParcelStoreSystem.GetSummary()}.");
        }

        protected override void OnUpdate()
        {
            var invalidCount = 0;
            invalidCount += RestrictObjectPreviews();
            invalidCount += RestrictCurvePreviews();

            _mFramesSinceLog++;
            if (invalidCount != _mLastInvalidCount && (invalidCount == 0 || _mFramesSinceLog >= 30))
            {
                _mLastInvalidCount = invalidCount;
                _mFramesSinceLog = 0;
                Mod.log.Info(
                    $"Parcel validation: {invalidCount} active construction preview entity/entities outside purchased parcels. {_mParcelStoreSystem.GetSummary()}.");
            }
        }

        private int RestrictObjectPreviews()
        {
            var entities = _mObjectPreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = _mObjectPreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var transforms = _mObjectPreviewQuery.ToComponentDataArray<Transform>(Allocator.Temp);

            try
            {
                var invalidCount = 0;
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var temp = temps[i];
                    if (!PlacementPreviewUtility.ShouldValidate(temp))
                    {
                        ClearOwnError(entity);
                        continue;
                    }

                    var position = transforms[i].m_Position;
                    var valid = _mParcelStoreSystem.IsBuildable(new float2(position.x, position.z));
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
            var entities = _mCurvePreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = _mCurvePreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var curves = _mCurvePreviewQuery.ToComponentDataArray<Curve>(Allocator.Temp);

            try
            {
                var invalidCount = 0;
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var temp = temps[i];
                    if (!PlacementPreviewUtility.ShouldValidate(temp))
                    {
                        ClearOwnError(entity);
                        continue;
                    }

                    var curve = curves[i].m_Bezier;
                    var valid = PlacementPreviewUtility.CurveInsideParcel(curve, _mParcelStoreSystem);
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
    }
}
