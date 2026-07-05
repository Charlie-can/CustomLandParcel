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
        private ParcelEditToolSystem _mParcelEditToolSystem;
        private int _mFramesUntilLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mOverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            _mParcelEditToolSystem = World.GetOrCreateSystemManaged<ParcelEditToolSystem>();
            Mod.log.Info(
                "ParcelBoundaryRenderSystem enabled. Drawing all parcel polygons and selected vertex handles through OverlayRenderSystem.");
        }

        protected override void OnUpdate()
        {
            _mOverlayRenderSystem ??= World.GetOrCreateSystemManaged<OverlayRenderSystem>();

            _mParcelStoreSystem ??= World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            _mParcelEditToolSystem ??= World.GetOrCreateSystemManaged<ParcelEditToolSystem>();

            var buffer = _mOverlayRenderSystem.GetBuffer(out var dependencies);
            dependencies.Complete();

            DrawParcels(
                buffer,
                _mParcelStoreSystem,
                _mParcelEditToolSystem.Session,
                _mParcelEditToolSystem.IsToolActive);
            DrawDraft(buffer, _mParcelEditToolSystem.Session);
            _mOverlayRenderSystem.AddBufferWriter(default(JobHandle));

            if (_mFramesUntilLog <= 0)
            {
                Mod.log.Info(
                    $"Parcel overlay submitted this frame: {_mParcelStoreSystem.GetSummary()}, editSession={_mParcelEditToolSystem.Session.GetSummary()}.");
                _mFramesUntilLog = 300;
            }

            _mFramesUntilLog--;
        }

        private static void DrawParcels(
            OverlayRenderSystem.Buffer buffer,
            ParcelStoreSystem store,
            ParcelEditSession session,
            bool editToolActive)
        {
            const OverlayRenderSystem.StyleFlags style = OverlayRenderSystem.StyleFlags.Projected |
                                                           OverlayRenderSystem.StyleFlags.DepthFadeBelow;
            const float width = 7f;
            const float dashLength = 64f;
            const float gapLength = 48f;
            var showEditHandles = editToolActive ||
                                  session.IsDrawing ||
                                  session.IsDragging ||
                                  session.Hover.Kind != ParcelEditHitKind.None;

            foreach (var parcel in store.Parcels)
            {
                if (parcel.Points.Count < 2)
                {
                    continue;
                }

                var selected = parcel.Id == store.SelectedParcelId;
                var outlineColor = GetOutlineColor(parcel.State, selected);
                var fillColor = GetFillColor(parcel.State, selected);
                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    var hoveringEdge = session.Hover.Kind == ParcelEditHitKind.Edge
                                       && session.Hover.ParcelId == parcel.Id
                                       && session.Hover.EdgeIndex == pointIndex;
                    DrawDashedSegment(
                        buffer,
                        hoveringEdge ? new Color(0.78f, 1f, 0.98f, 0.98f) : outlineColor,
                        hoveringEdge ? new Color(0.78f, 1f, 0.98f, 0.46f) : fillColor,
                        style,
                        parcel.Points[pointIndex],
                        parcel.Points[(pointIndex + 1) % parcel.Points.Count],
                        hoveringEdge ? width + 3f : width,
                        dashLength,
                        gapLength);
                }

                if (!showEditHandles || (!selected && session.Hover.ParcelId != parcel.Id))
                {
                    continue;
                }

                for (var vertexIndex = 0; vertexIndex < parcel.Points.Count; vertexIndex++)
                {
                    var point = parcel.Points[vertexIndex];
                    var hoveringVertex = session.Hover.Kind == ParcelEditHitKind.Vertex
                                         && session.Hover.ParcelId == parcel.Id
                                         && session.Hover.VertexIndex == vertexIndex;
                    var handleColor = hoveringVertex
                        ? new Color(1f, 0.96f, 0.55f, 0.98f)
                        : vertexIndex == store.SelectedVertexIndex
                        ? new Color(0.76f, 1f, 0.95f, 0.95f)
                        : new Color(0.62f, 0.95f, 0.72f, 0.78f);
                    buffer.DrawCircle(
                        handleColor,
                        handleColor,
                        1.5f,
                        style,
                        new float2(0f, 1f),
                        new float3(point.x, 0f, point.y),
                        hoveringVertex ? 44f : vertexIndex == store.SelectedVertexIndex ? 38f : 26f);
                }
            }
        }

        private static void DrawDraft(OverlayRenderSystem.Buffer buffer, ParcelEditSession session)
        {
            if (!session.IsDrawing || session.DraftPoints.Count == 0)
            {
                return;
            }

            const OverlayRenderSystem.StyleFlags style = OverlayRenderSystem.StyleFlags.Projected |
                                                           OverlayRenderSystem.StyleFlags.DepthFadeBelow;
            var outlineColor = new Color(1f, 0.82f, 0.18f, 0.92f);
            var fillColor = new Color(1f, 0.82f, 0.18f, 0.34f);
            for (var i = 0; i < session.DraftPoints.Count - 1; i++)
            {
                DrawDashedSegment(
                    buffer,
                    outlineColor,
                    fillColor,
                    style,
                    session.DraftPoints[i],
                    session.DraftPoints[i + 1],
                    8f,
                    48f,
                    32f);
            }

            for (var i = 0; i < session.DraftPoints.Count; i++)
            {
                var point = session.DraftPoints[i];
                var handleColor = i == 0 && session.Hover.Kind == ParcelEditHitKind.Vertex
                    ? new Color(1f, 1f, 1f, 0.98f)
                    : outlineColor;
                buffer.DrawCircle(
                    handleColor,
                    handleColor,
                    1.5f,
                    style,
                    new float2(0f, 1f),
                    new float3(point.x, 0f, point.y),
                    i == 0 ? 42f : 28f);
            }
        }

        private static Color GetOutlineColor(LandParcelState state, bool selected)
        {
            if (selected)
            {
                return new Color(0.2f, 1f, 0.58f, 0.9f);
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
                return new Color(0.2f, 1f, 0.58f, 0.32f);
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
