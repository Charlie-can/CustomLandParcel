using Game;
using Game.Common;
using Game.Net;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using CustomLandParcel.Geometry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Marks temporary construction previews outside purchased custom parcels and shows the vanilla city-limit error icon.
    /// </summary>
    public partial class ConstructionRestrictionSystem : GameSystemBase
    {
        private EntityQuery _mObjectPreviewQuery;
        private EntityQuery _mCurvePreviewQuery;
        private EntityQuery _mToolErrorPrefabQuery;
        private ParcelStoreSystem _mParcelStoreSystem;
        private IconCommandSystem _mIconCommandSystem;
        private Entity _mExceedsCityLimitsPrefab;
        private int _mLastInvalidCount = -1;
        private int _mLastIconCount = -1;
        private int _mMissingPrefabLogCooldownFrames;
        private int _mCurveOutsideLogCooldownFrames;
        private int _mFramesSinceLog;
        private int _mNoPreviewLogCooldownFrames;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            _mIconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();

            _mObjectPreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Transform>(),
                ComponentType.Exclude<Deleted>());

            _mCurvePreviewQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.Exclude<Deleted>());

            _mToolErrorPrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<NotificationIconData>(),
                ComponentType.ReadOnly<ToolErrorData>());

            Mod.log.Info($"ConstructionRestrictionSystem enabled. {_mParcelStoreSystem.GetSummary()}.");
        }

        protected override void OnUpdate()
        {
            if (_mObjectPreviewQuery.IsEmptyIgnoreFilter && _mCurvePreviewQuery.IsEmptyIgnoreFilter)
            {
                LogNoActivePreviews();
                return;
            }

            RefreshExceedsCityLimitsPrefab();

            var invalidCount = 0;
            var iconCount = 0;
            var iconCommandBuffer = _mIconCommandSystem.CreateCommandBuffer();
            invalidCount += RestrictObjectPreviews(iconCommandBuffer, ref iconCount);
            invalidCount += RestrictCurvePreviews(iconCommandBuffer, ref iconCount);

            _mFramesSinceLog++;
            if ((invalidCount != _mLastInvalidCount || iconCount != _mLastIconCount) &&
                (invalidCount == 0 || _mFramesSinceLog >= 30))
            {
                _mLastInvalidCount = invalidCount;
                _mLastIconCount = iconCount;
                _mFramesSinceLog = 0;
                Mod.log.Info(
                    $"Parcel validation: {invalidCount} active construction preview entity/entities outside purchased parcels; cityLimitIcons={iconCount}; iconPrefab={FormatEntity(_mExceedsCityLimitsPrefab)}. {_mParcelStoreSystem.GetSummary()}.");
            }
        }

        private void LogNoActivePreviews()
        {
            _mLastInvalidCount = 0;
            _mLastIconCount = 0;
            _mFramesSinceLog = 0;
            if (_mNoPreviewLogCooldownFrames > 0)
            {
                _mNoPreviewLogCooldownFrames--;
                return;
            }

            Mod.log.Info($"Parcel validation idle: no active construction preview entities. {_mParcelStoreSystem.GetSummary()}.");
            _mNoPreviewLogCooldownFrames = 300;
        }

        private int RestrictObjectPreviews(IconCommandBuffer iconCommandBuffer, ref int iconCount)
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
                        ClearOwnError(entity, iconCommandBuffer);
                        continue;
                    }

                    var position = transforms[i].m_Position;
                    var valid = _mParcelStoreSystem.IsBuildable(new float2(position.x, position.z));
                    SetRestrictionError(entity, !valid, position, iconCommandBuffer, ref iconCount);

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

        private int RestrictCurvePreviews(IconCommandBuffer iconCommandBuffer, ref int iconCount)
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
                        ClearOwnError(entity, iconCommandBuffer);
                        continue;
                    }

                    var curve = curves[i].m_Bezier;
                    var valid = PlacementPreviewUtility.TryGetFirstOutsideCurveSample(
                        curve,
                        _mParcelStoreSystem,
                        out var outsidePoint,
                        out var outsideSample);
                    var iconPosition = PlacementPreviewUtility.EvaluateBezier(curve, 0.5f);
                    SetRestrictionError(entity, !valid, iconPosition, iconCommandBuffer, ref iconCount);

                    if (!valid)
                    {
                        LogCurveOutsideParcel(entity, temp, outsidePoint, outsideSample);
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

        private void LogCurveOutsideParcel(Entity entity, Temp temp, float3 outsidePoint, float outsideSample)
        {
            if (_mCurveOutsideLogCooldownFrames > 0)
            {
                _mCurveOutsideLogCooldownFrames--;
                return;
            }

            Mod.log.Info(
                $"Parcel validation blocked curve preview: entity={FormatEntity(entity)}, firstOutsideSample={outsideSample:F2}, firstOutsidePoint={ParcelGeometry.Format(outsidePoint.xz)}, tempFlags={temp.m_Flags}. {_mParcelStoreSystem.GetSummary()}.");
            _mCurveOutsideLogCooldownFrames = 30;
        }

        private void SetRestrictionError(
            Entity entity,
            bool blocked,
            float3 iconPosition,
            IconCommandBuffer iconCommandBuffer,
            ref int iconCount)
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

                AddCityLimitIcon(entity, iconPosition, iconCommandBuffer, ref iconCount);
            }
            else if (hasOwnError)
            {
                ClearOwnError(entity, iconCommandBuffer);
            }
        }

        private void ClearOwnError(Entity entity, IconCommandBuffer iconCommandBuffer)
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

            if (_mExceedsCityLimitsPrefab != Entity.Null)
            {
                iconCommandBuffer.Remove(entity, _mExceedsCityLimitsPrefab);
            }
        }

        private void AddCityLimitIcon(
            Entity entity,
            float3 iconPosition,
            IconCommandBuffer iconCommandBuffer,
            ref int iconCount)
        {
            if (_mExceedsCityLimitsPrefab == Entity.Null)
            {
                if (_mMissingPrefabLogCooldownFrames <= 0)
                {
                    Mod.log.Warn(
                        "Parcel validation blocked an outside preview, but no ToolErrorData prefab for ErrorType.ExceedsCityLimits is available yet. The Error marker will block construction, but the vanilla city-limit tooltip cannot be shown.");
                    _mMissingPrefabLogCooldownFrames = 120;
                }
                else
                {
                    _mMissingPrefabLogCooldownFrames--;
                }

                return;
            }

            iconCommandBuffer.Add(
                entity,
                _mExceedsCityLimitsPrefab,
                iconPosition,
                IconPriority.Error,
                IconClusterLayer.Default,
                IconFlags.IgnoreTarget,
                Entity.Null,
                isTemp: true,
                disallowCluster: true);
            iconCount++;
        }

        private void RefreshExceedsCityLimitsPrefab()
        {
            if (_mExceedsCityLimitsPrefab != Entity.Null && EntityManager.Exists(_mExceedsCityLimitsPrefab))
            {
                return;
            }

            _mExceedsCityLimitsPrefab = Entity.Null;
            var entities = _mToolErrorPrefabQuery.ToEntityArray(Allocator.Temp);
            var toolErrors = _mToolErrorPrefabQuery.ToComponentDataArray<ToolErrorData>(Allocator.Temp);
            try
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (toolErrors[i].m_Error != ErrorType.ExceedsCityLimits)
                    {
                        continue;
                    }

                    _mExceedsCityLimitsPrefab = entities[i];
                    Mod.log.Info(
                        $"Parcel validation selected vanilla ExceedsCityLimits error prefab {FormatEntity(_mExceedsCityLimitsPrefab)} from {entities.Length} ToolErrorData prefab(s).");
                    return;
                }
            }
            finally
            {
                entities.Dispose();
                toolErrors.Dispose();
            }
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null ? "null" : $"{entity.Index}:{entity.Version}";
        }
    }
}
