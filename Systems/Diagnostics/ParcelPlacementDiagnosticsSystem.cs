using System.Text;
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
    /// Logs what temporary construction previews look like to the game while testing parcel validation.
    /// </summary>
    public partial class ParcelPlacementDiagnosticsSystem : GameSystemBase
    {
        private const int MaxSamples = 3;
        private EntityQuery _mObjectPreviewQuery;
        private EntityQuery _mCurvePreviewQuery;
        private ParcelStoreSystem _mParcelStoreSystem;
        private int _mLastOutsideCount = -1;
        private int _mLastOutsideErrorCount = -1;
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

            Mod.log.Info(
                $"ParcelPlacementDiagnosticsSystem enabled. {_mParcelStoreSystem.GetSummary()}. Logging only active create/modify/replace/upgrade previews.");
        }

        protected override void OnUpdate()
        {
            var diagnostics = new PlacementDiagnostics();
            CollectObjectDiagnostics(ref diagnostics);
            CollectCurveDiagnostics(ref diagnostics);

            _mFramesSinceLog++;
            var shouldLog = diagnostics.OutsideCount != _mLastOutsideCount
                            || diagnostics.OutsideWithErrorCount != _mLastOutsideErrorCount
                            || (diagnostics.OutsideCount > 0 && _mFramesSinceLog >= 60);

            if (!shouldLog)
            {
                return;
            }

            _mLastOutsideCount = diagnostics.OutsideCount;
            _mLastOutsideErrorCount = diagnostics.OutsideWithErrorCount;
            _mFramesSinceLog = 0;

            if (diagnostics.ActiveCount == 0)
            {
                Mod.log.Info(
                    $"Placement diagnostics: no active construction preview entities found this frame. {_mParcelStoreSystem.GetSummary()}.");
                return;
            }

            Mod.log.Info(
                $"Placement diagnostics: active={diagnostics.ActiveCount}, inside={diagnostics.InsideCount}, outside={diagnostics.OutsideCount}, outsideWithError={diagnostics.OutsideWithErrorCount}, outsideWithoutError={diagnostics.OutsideCount - diagnostics.OutsideWithErrorCount}, {_mParcelStoreSystem.GetSummary()}, samples={diagnostics.Samples}.");

            if (diagnostics.OutsideCount > 0 && diagnostics.OutsideWithErrorCount == 0)
            {
                Mod.log.Warn(
                    "Placement diagnostics: outside previews are not receiving Error. Check ConstructionRestrictionSystem registration first; enable vanilla map tile compatibility only if a tool bypasses the direct restriction path.");
            }
        }

        private void CollectObjectDiagnostics(ref PlacementDiagnostics diagnostics)
        {
            var entities = _mObjectPreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = _mObjectPreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var transforms = _mObjectPreviewQuery.ToComponentDataArray<Transform>(Allocator.Temp);

            try
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (!PlacementPreviewUtility.ShouldValidate(temps[i]))
                    {
                        continue;
                    }

                    var position = transforms[i].m_Position;
                    AddPointDiagnostics(ref diagnostics, entities[i], new float2(position.x, position.z), "object",
                        temps[i]);
                }
            }
            finally
            {
                entities.Dispose();
                temps.Dispose();
                transforms.Dispose();
            }
        }

        private void CollectCurveDiagnostics(ref PlacementDiagnostics diagnostics)
        {
            var entities = _mCurvePreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = _mCurvePreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var curves = _mCurvePreviewQuery.ToComponentDataArray<Curve>(Allocator.Temp);

            try
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (!PlacementPreviewUtility.ShouldValidate(temps[i]))
                    {
                        continue;
                    }

                    var curve = curves[i].m_Bezier;
                    var center = PlacementPreviewUtility.EvaluateBezier(curve, 0.5f);
                    AddCurveDiagnostics(ref diagnostics, entities[i], curve, new float2(center.x, center.z), temps[i]);
                }
            }
            finally
            {
                entities.Dispose();
                temps.Dispose();
                curves.Dispose();
            }
        }

        private void AddPointDiagnostics(
            ref PlacementDiagnostics diagnostics,
            Entity entity,
            float2 point,
            string kind,
            Temp temp)
        {
            diagnostics.ActiveCount++;
            var inside = _mParcelStoreSystem.IsBuildable(point);
            if (inside)
            {
                diagnostics.InsideCount++;
                return;
            }

            AddOutsideDiagnostics(ref diagnostics, entity, point, kind, temp);
        }

        private void AddCurveDiagnostics(
            ref PlacementDiagnostics diagnostics,
            Entity entity,
            Colossal.Mathematics.Bezier4x3 curve,
            float2 center,
            Temp temp)
        {
            diagnostics.ActiveCount++;
            var inside = PlacementPreviewUtility.CurveInsideParcel(curve, _mParcelStoreSystem);
            if (inside)
            {
                diagnostics.InsideCount++;
                return;
            }

            AddOutsideDiagnostics(ref diagnostics, entity, center, "curve", temp);
        }

        private void AddOutsideDiagnostics(
            ref PlacementDiagnostics diagnostics,
            Entity entity,
            float2 samplePoint,
            string kind,
            Temp temp)
        {
            diagnostics.OutsideCount++;
            var hasError = EntityManager.HasComponent<Error>(entity);
            if (hasError)
            {
                diagnostics.OutsideWithErrorCount++;
            }

            if (diagnostics.SampleCount >= MaxSamples)
            {
                return;
            }

            if (diagnostics.SampleCount > 0)
            {
                diagnostics.Samples.Append(" | ");
            }

            diagnostics.Samples.Append(kind);
            diagnostics.Samples.Append(" entity=");
            diagnostics.Samples.Append(FormatEntity(entity));
            diagnostics.Samples.Append(" point=");
            diagnostics.Samples.Append(ParcelGeometry.Format(samplePoint));
            diagnostics.Samples.Append(" hasError=");
            diagnostics.Samples.Append(hasError);
            diagnostics.Samples.Append(" tempFlags=");
            diagnostics.Samples.Append(temp.m_Flags);
            diagnostics.SampleCount++;
        }

        private static string FormatEntity(Entity entity)
        {
            return $"{entity.Index}:{entity.Version}";
        }

        private sealed class PlacementDiagnostics
        {
            public int ActiveCount;
            public int InsideCount;
            public int OutsideCount;
            public int OutsideWithErrorCount;
            public int SampleCount;
            public readonly StringBuilder Samples = new StringBuilder();
        }
    }
}
