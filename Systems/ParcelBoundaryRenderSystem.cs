using Colossal.Mathematics;
using Game;
using Game.Rendering;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Draws the MVP parcel through the game's overlay renderer so it is visible in the normal CS2 render pipeline.
    /// </summary>
    public partial class ParcelBoundaryRenderSystem : GameSystemBase
    {
        private OverlayRenderSystem _mOverlayRenderSystem;
        private int _mFramesUntilLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mOverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            Mod.log.Info(
                "ParcelBoundaryRenderSystem enabled. Drawing MVP parcel buildable area through OverlayRenderSystem.");
        }

        protected override void OnUpdate()
        {
            if (_mOverlayRenderSystem == null)
            {
                _mOverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            }

            var buffer = _mOverlayRenderSystem.GetBuffer(out var dependencies);
            dependencies.Complete();

            var min = ConstructionRestrictionSystem.ParcelMin;
            var max = ConstructionRestrictionSystem.ParcelMax;
            DrawBuildableArea(buffer, min, max);
            _mOverlayRenderSystem.AddBufferWriter(default(JobHandle));

            if (_mFramesUntilLog <= 0)
            {
                Mod.log.Info(
                    $"Parcel overlay marker submitted this frame: parcel={FormatFloat2(min)}..{FormatFloat2(max)}, outer dashed boundary + interior grid/diagonals.");
                _mFramesUntilLog = 300;
            }

            _mFramesUntilLog--;
        }

        private static void DrawBuildableArea(OverlayRenderSystem.Buffer buffer, float2 min, float2 max)
        {
            var outlineColor = new Color(0.05f, 1f, 0.28f, 1f);
            var fillColor = new Color(0.05f, 1f, 0.28f, 0.85f);
            var gridColor = new Color(0.05f, 1f, 0.28f, 0.45f);
            const OverlayRenderSystem.StyleFlags style = OverlayRenderSystem.StyleFlags.Projected |
                                                           OverlayRenderSystem.StyleFlags.DepthFadeBelow;

            DrawDashedSegment(buffer, outlineColor, fillColor, style, new float2(min.x, min.y), new float2(max.x, min.y), 28f, 72f, 28f);
            DrawDashedSegment(buffer, outlineColor, fillColor, style, new float2(max.x, min.y), new float2(max.x, max.y), 28f, 72f, 28f);
            DrawDashedSegment(buffer, outlineColor, fillColor, style, new float2(max.x, max.y), new float2(min.x, max.y), 28f, 72f, 28f);
            DrawDashedSegment(buffer, outlineColor, fillColor, style, new float2(min.x, max.y), new float2(min.x, min.y), 28f, 72f, 28f);

            const int divisions = 4;
            for (var i = 1; i < divisions; i++)
            {
                var t = i / (float)divisions;
                var x = math.lerp(min.x, max.x, t);
                var z = math.lerp(min.y, max.y, t);
                DrawSegment(buffer, gridColor, style, new float2(x, min.y), new float2(x, max.y), 10f);
                DrawSegment(buffer, gridColor, style, new float2(min.x, z), new float2(max.x, z), 10f);
            }

            DrawSegment(buffer, gridColor, style, min, max, 10f);
            DrawSegment(buffer, gridColor, style, new float2(min.x, max.y), new float2(max.x, min.y), 10f);
        }

        private static void DrawSegment(
            OverlayRenderSystem.Buffer buffer,
            Color color,
            OverlayRenderSystem.StyleFlags style,
            float2 start,
            float2 end,
            float width)
        {
            buffer.DrawLine(color, color, 0f, style, CreateLine(start, end), width, new float2(0.25f, 0.25f));
        }

        private static void DrawDashedSegment(
            OverlayRenderSystem.Buffer buffer,
            Color outlineColor,
            Color fillColor,
            OverlayRenderSystem.StyleFlags style,
            float2 start,
            float2 end,
            float width,
            float dashLength,
            float gapLength)
        {
            buffer.DrawDashedLine(outlineColor, fillColor, 4f, style, CreateLine(start, end), width, dashLength, gapLength);
        }

        private static Line3.Segment CreateLine(float2 start, float2 end)
        {
            return new Line3.Segment
            {
                a = new float3(start.x, 0f, start.y),
                b = new float3(end.x, 0f, end.y)
            };
        }

        private static string FormatFloat2(float2 value)
        {
            return $"({value.x:F1}, {value.y:F1})";
        }
    }
}
