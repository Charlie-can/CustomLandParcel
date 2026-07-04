using System;
using System.Collections.Generic;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    internal sealed class ParcelEditSession
    {
        public ParcelEditMode Mode { get; private set; }

        public ParcelEditHit Hover { get; private set; } = ParcelEditHit.None;

        public ParcelEditHit DragTarget { get; private set; } = ParcelEditHit.None;

        public float2 DragStartPosition { get; private set; }

        public List<float2> DragOriginalPoints { get; } = new List<float2>();

        public List<float2> DraftPoints { get; } = new List<float2>();

        public bool IsDrawing => Mode == ParcelEditMode.Drawing;

        public bool IsDragging => Mode == ParcelEditMode.DragVertex || Mode == ParcelEditMode.DragParcel;

        public void SetHover(ParcelEditHit hit)
        {
            Hover = hit;
            if (IsDragging || IsDrawing)
            {
                return;
            }

            switch (hit.Kind)
            {
                case ParcelEditHitKind.Vertex:
                    Mode = ParcelEditMode.HoverVertex;
                    break;
                case ParcelEditHitKind.Edge:
                    Mode = ParcelEditMode.HoverEdge;
                    break;
                case ParcelEditHitKind.Parcel:
                    Mode = ParcelEditMode.HoverParcel;
                    break;
                default:
                    Mode = ParcelEditMode.Idle;
                    break;
            }
        }

        public void StartDrawing(float2 firstPoint)
        {
            DraftPoints.Clear();
            DraftPoints.Add(firstPoint);
            Hover = ParcelEditHit.None;
            DragTarget = ParcelEditHit.None;
            Mode = ParcelEditMode.Drawing;
        }

        public void AddDraftPoint(float2 point)
        {
            DraftPoints.Add(point);
            Mode = ParcelEditMode.Drawing;
        }

        public void ClearDraft()
        {
            DraftPoints.Clear();
            if (!IsDragging)
            {
                Mode = ParcelEditMode.Idle;
            }
        }

        public void StartDrag(ParcelEditHit target, float2 startPosition, LandParcel parcel)
        {
            DragTarget = target;
            DragStartPosition = startPosition;
            DragOriginalPoints.Clear();
            if (parcel != null)
            {
                DragOriginalPoints.AddRange(parcel.Points);
            }

            Mode = target.Kind == ParcelEditHitKind.Vertex ? ParcelEditMode.DragVertex : ParcelEditMode.DragParcel;
        }

        public void EndDrag()
        {
            DragTarget = ParcelEditHit.None;
            DragOriginalPoints.Clear();
            Mode = Hover.Kind == ParcelEditHitKind.None ? ParcelEditMode.Idle : ModeFromHit(Hover.Kind);
        }

        public void Cancel()
        {
            Hover = ParcelEditHit.None;
            DragTarget = ParcelEditHit.None;
            DragOriginalPoints.Clear();
            DraftPoints.Clear();
            Mode = ParcelEditMode.Idle;
        }

        public string GetSummary()
        {
            return
                $"mode={Mode}, hover=[{Hover}], drag=[{DragTarget}], draftPoints={DraftPoints.Count}, dragOriginalPoints={DragOriginalPoints.Count}";
        }

        private static ParcelEditMode ModeFromHit(ParcelEditHitKind hitKind)
        {
            switch (hitKind)
            {
                case ParcelEditHitKind.Vertex:
                    return ParcelEditMode.HoverVertex;
                case ParcelEditHitKind.Edge:
                    return ParcelEditMode.HoverEdge;
                case ParcelEditHitKind.Parcel:
                    return ParcelEditMode.HoverParcel;
                default:
                    return ParcelEditMode.Idle;
            }
        }
    }
}
