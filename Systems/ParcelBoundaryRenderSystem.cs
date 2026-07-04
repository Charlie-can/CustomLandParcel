using Colossal.Mathematics;
using CustomLandParcel.Data;
using Game;
using Game.Rendering;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Draws custom parcel polygons through the game's overlay renderer.
    /// </summary>
    public partial class ParcelBoundaryRenderSystem : GameSystemBase
    {
        private OverlayRenderSystem _mOverlayRenderSystem;
        private ParcelStoreSystem _mParcelStoreSystem;
        private int _mFramesUntilLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mOverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            Mod.log.Info(
                "ParcelBoundaryRenderSystem enabled. Drawing all parcel polygons and selected vertex handles through OverlayRenderSystem.");
        }

        protected override void OnUpdate()
        {
            if (_mOverlayRenderSystem == null)
            {
                _mOverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            }

            if (_mParcelStoreSystem == null)
            {
                _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            }

            var buffer = _mOverlayRenderSystem.GetBuffer(out var dependencies);
            dependencies.Complete();

            DrawParcels(buffer, _mParcelStoreSystem);
            _mOverlayRenderSystem.AddBufferWriter(default(JobHandle));

            if (_mFramesUntilLog <= 0)
            {
                Mod.log.Info($"Parcel overlay submitted this frame: {_mParcelStoreSystem.GetSummary()}.");
                _mFramesUntilLog = 300;
            }

            _mFramesUntilLog--;
        }

        private static void DrawParcels(OverlayRenderSystem.Buffer buffer, ParcelStoreSystem store)
        {
            const OverlayRenderSystem.StyleFlags style = OverlayRenderSystem.StyleFlags.Projected |
                                                           OverlayRenderSystem.StyleFlags.DepthFadeBelow;
            const float width = 7f;
            const float dashLength = 64f;
            const float gapLength = 48f;

            for (var i = 0; i < store.Parcels.Count; i++)
            {
                var parcel = store.Parcels[i];
                if (parcel.Points.Count < 2)
                {
                    continue;
                }

                var selected = parcel.Id == store.SelectedParcelId;
                var outlineColor = GetOutlineColor(parcel.State, selected);
                var fillColor = GetFillColor(parcel.State, selected);
                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    DrawDashedSegment(
                        buffer,
                        outlineColor,
                        fillColor,
                        style,
                        parcel.Points[pointIndex],
                        parcel.Points[(pointIndex + 1) % parcel.Points.Count],
                        width,
                        dashLength,
                        gapLength);
                }

                if (!selected)
                {
                    continue;
                }

                for (var vertexIndex = 0; vertexIndex < parcel.Points.Count; vertexIndex++)
                {
                    var point = parcel.Points[vertexIndex];
                    var handleColor = vertexIndex == store.SelectedVertexIndex
                        ? new Color(1f, 1f, 1f, 0.95f)
                        : new Color(0.82f, 0.92f, 1f, 0.78f);
                    buffer.DrawCircle(
                        handleColor,
                        handleColor,
                        1.5f,
                        style,
                        new float2(0f, 1f),
                        new float3(point.x, 0f, point.y),
                        vertexIndex == store.SelectedVertexIndex ? 38f : 26f);
                }
            }
        }

        private static Color GetOutlineColor(LandParcelState state, bool selected)
        {
            if (selected)
            {
                return new Color(1f, 1f, 1f, 0.9f);
            }

            switch (state)
            {
                case LandParcelState.Purchased:
                    return new Color(0.35f, 0.92f, 0.45f, 0.74f);
                case LandParcelState.Locked:
                    return new Color(0.45f, 0.45f, 0.45f, 0.45f);
                default:
                    return new Color(1f, 0.86f, 0.26f, 0.66f);
            }
        }

        private static Color GetFillColor(LandParcelState state, bool selected)
        {
            if (selected)
            {
                return new Color(1f, 1f, 1f, 0.34f);
            }

            switch (state)
            {
                case LandParcelState.Purchased:
                    return new Color(0.35f, 0.92f, 0.45f, 0.24f);
                case LandParcelState.Locked:
                    return new Color(0.45f, 0.45f, 0.45f, 0.18f);
                default:
                    return new Color(1f, 0.86f, 0.26f, 0.24f);
            }
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
