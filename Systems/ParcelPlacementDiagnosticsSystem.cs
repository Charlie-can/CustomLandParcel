using System.Text;
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
        private EntityQuery m_ObjectPreviewQuery;
        private EntityQuery m_CurvePreviewQuery;
        private int m_LastOutsideCount = -1;
        private int m_LastOutsideErrorCount = -1;
        private int m_FramesSinceLog;

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

            Mod.log.Info(
                $"ParcelPlacementDiagnosticsSystem enabled. Parcel x/z {FormatFloat2(ConstructionRestrictionSystem.ParcelMin)}..{FormatFloat2(ConstructionRestrictionSystem.ParcelMax)}. Logging only active create/modify/replace/upgrade previews.");
        }

        protected override void OnUpdate()
        {
            var diagnostics = new PlacementDiagnostics();
            CollectObjectDiagnostics(ref diagnostics);
            CollectCurveDiagnostics(ref diagnostics);

            m_FramesSinceLog++;
            var shouldLog = diagnostics.OutsideCount != m_LastOutsideCount
                            || diagnostics.OutsideWithErrorCount != m_LastOutsideErrorCount
                            || (diagnostics.OutsideCount > 0 && m_FramesSinceLog >= 60);

            if (!shouldLog)
            {
                return;
            }

            m_LastOutsideCount = diagnostics.OutsideCount;
            m_LastOutsideErrorCount = diagnostics.OutsideWithErrorCount;
            m_FramesSinceLog = 0;

            if (diagnostics.ActiveCount == 0)
            {
                Mod.log.Info("Placement diagnostics: no active construction preview entities found this frame.");
                return;
            }

            Mod.log.Info(
                $"Placement diagnostics: active={diagnostics.ActiveCount}, inside={diagnostics.InsideCount}, outside={diagnostics.OutsideCount}, outsideWithError={diagnostics.OutsideWithErrorCount}, outsideWithoutError={diagnostics.OutsideCount - diagnostics.OutsideWithErrorCount}, samples={diagnostics.Samples}.");

            if (diagnostics.OutsideCount > 0 && diagnostics.OutsideWithErrorCount == 0)
            {
                Mod.log.Warn(
                    "Placement diagnostics: outside previews are not receiving Error. Vanilla area validation is not hitting our parcel blockers for these preview entities.");
            }
        }

        private void CollectObjectDiagnostics(ref PlacementDiagnostics diagnostics)
        {
            var entities = m_ObjectPreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = m_ObjectPreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var transforms = m_ObjectPreviewQuery.ToComponentDataArray<Transform>(Allocator.Temp);

            try
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (!ShouldValidate(temps[i]))
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
            var entities = m_CurvePreviewQuery.ToEntityArray(Allocator.Temp);
            var temps = m_CurvePreviewQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            var curves = m_CurvePreviewQuery.ToComponentDataArray<Curve>(Allocator.Temp);

            try
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (!ShouldValidate(temps[i]))
                    {
                        continue;
                    }

                    var curve = curves[i].m_Bezier;
                    var center = EvaluateBezier(curve, 0.5f);
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
            var inside = ConstructionRestrictionSystem.Contains(point);
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
            var inside = CurveInsideParcel(curve);
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
            diagnostics.Samples.Append(FormatFloat2(samplePoint));
            diagnostics.Samples.Append(" hasError=");
            diagnostics.Samples.Append(hasError);
            diagnostics.Samples.Append(" tempFlags=");
            diagnostics.Samples.Append(temp.m_Flags);
            diagnostics.SampleCount++;
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

        private static bool CurveInsideParcel(Colossal.Mathematics.Bezier4x3 curve)
        {
            for (var i = 0; i <= 8; i++)
            {
                var t = i / 8f;
                var position = EvaluateBezier(curve, t);
                if (!ConstructionRestrictionSystem.Contains(new float2(position.x, position.z)))
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

        private static string FormatEntity(Entity entity)
        {
            return $"{entity.Index}:{entity.Version}";
        }

        private static string FormatFloat2(float2 value)
        {
            return $"({value.x:F1}, {value.y:F1})";
        }

        private sealed class PlacementDiagnostics
        {
            public int ActiveCount;
            public int InsideCount;
            public int OutsideCount;
            public int OutsideWithErrorCount;
            public int SampleCount;
            public StringBuilder Samples = new StringBuilder();
        }
    }
}