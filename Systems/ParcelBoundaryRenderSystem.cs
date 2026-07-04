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
    /// Draws the current parcel through the game's overlay renderer so it is visible in the normal CS2 render pipeline.
    /// </summary>
    public partial class ParcelBoundaryRenderSystem : GameSystemBase
    {
        private OverlayRenderSystem _mOverlayRenderSystem;
        private ParcelBoundsSystem _mParcelBoundsSystem;
        private int _mFramesUntilLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mOverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            _mParcelBoundsSystem = World.GetOrCreateSystemManaged<ParcelBoundsSystem>();
            Mod.log.Info(
                "ParcelBoundaryRenderSystem enabled. Drawing current parcel buildable area through OverlayRenderSystem.");
        }

        protected override void OnUpdate()
        {
            if (_mOverlayRenderSystem == null)
            {
                _mOverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            }

            if (_mParcelBoundsSystem == null)
            {
                _mParcelBoundsSystem = World.GetOrCreateSystemManaged<ParcelBoundsSystem>();
            }

            var buffer = _mOverlayRenderSystem.GetBuffer(out var dependencies);
            dependencies.Complete();

            var bounds = _mParcelBoundsSystem.Bounds;
            DrawParcelBoundary(buffer, bounds);
            _mOverlayRenderSystem.AddBufferWriter(default(JobHandle));

            if (_mFramesUntilLog <= 0)
            {
                Mod.log.Info(
                    $"Parcel overlay marker submitted this frame: parcel={bounds}, parcelVersion={_mParcelBoundsSystem.Version}, subtle dashed boundary only.");
                _mFramesUntilLog = 300;
            }

            _mFramesUntilLog--;
        }

        private static void DrawParcelBoundary(OverlayRenderSystem.Buffer buffer, ParcelBounds bounds)
        {
            var outlineColor = new Color(0.62f, 0.78f, 0.86f, 0.48f);
            var fillColor = new Color(0.62f, 0.78f, 0.86f, 0.30f);
            const OverlayRenderSystem.StyleFlags style = OverlayRenderSystem.StyleFlags.Projected |
                                                           OverlayRenderSystem.StyleFlags.DepthFadeBelow;
            const float width = 7f;
            const float dashLength = 64f;
            const float gapLength = 48f;

            DrawDashedSegment(
                buffer,
                outlineColor,
                fillColor,
                style,
                new float2(bounds.Min.x, bounds.Min.y),
                new float2(bounds.Max.x, bounds.Min.y),
                width,
                dashLength,
                gapLength);
            DrawDashedSegment(
                buffer,
                outlineColor,
                fillColor,
                style,
                new float2(bounds.Max.x, bounds.Min.y),
                new float2(bounds.Max.x, bounds.Max.y),
                width,
                dashLength,
                gapLength);
            DrawDashedSegment(
                buffer,
                outlineColor,
                fillColor,
                style,
                new float2(bounds.Max.x, bounds.Max.y),
                new float2(bounds.Min.x, bounds.Max.y),
                width,
                dashLength,
                gapLength);
            DrawDashedSegment(
                buffer,
                outlineColor,
                fillColor,
                style,
                new float2(bounds.Min.x, bounds.Max.y),
                new float2(bounds.Min.x, bounds.Min.y),
                width,
                dashLength,
                gapLength);
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
            buffer.DrawDashedLine(
                outlineColor,
                fillColor,
                4f,
                style,
                CreateLine(start, end),
                width,
                dashLength,
                gapLength);
        }

        private static Line3.Segment CreateLine(float2 start, float2 end)
        {
            return new Line3.Segment
            {
                a = new float3(start.x, 0f, start.y),
                b = new float3(end.x, 0f, end.y)
            };
        }
    }
}
