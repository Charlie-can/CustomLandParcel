using System;
using System.Collections.Generic;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    internal readonly struct ParcelEditHit
    {
        public ParcelEditHit(ParcelEditHitKind kind, Guid parcelId, int vertexIndex, int edgeIndex, float distance)
        {
            Kind = kind;
            ParcelId = parcelId;
            VertexIndex = vertexIndex;
            EdgeIndex = edgeIndex;
            Distance = distance;
        }

        public ParcelEditHitKind Kind { get; }

        public Guid ParcelId { get; }

        public int VertexIndex { get; }

        public int EdgeIndex { get; }

        public float Distance { get; }

        public static ParcelEditHit None => new ParcelEditHit(ParcelEditHitKind.None, Guid.Empty, -1, -1, float.MaxValue);

        public override string ToString()
        {
            return
                $"kind={Kind}, parcel={ParcelStoreSystem.FormatGuid(ParcelId)}, vertex={VertexIndex}, edge={EdgeIndex}, distance={Distance:F1}";
        }
    }

    internal static class ParcelEditHitTest
    {
        public static ParcelEditHit FindBestHit(
            IReadOnlyList<LandParcel> parcels,
            float2 position,
            float vertexRadius,
            float edgeRadius)
        {
            var vertexHit = FindNearestVertex(parcels, position, vertexRadius);
            if (vertexHit.Kind != ParcelEditHitKind.None)
            {
                return vertexHit;
            }

            var edgeHit = FindNearestEdge(parcels, position, edgeRadius);
            if (edgeHit.Kind != ParcelEditHitKind.None)
            {
                return edgeHit;
            }

            return FindContainingParcel(parcels, position);
        }

        private static ParcelEditHit FindNearestVertex(
            IReadOnlyList<LandParcel> parcels,
            float2 position,
            float radius)
        {
            var best = ParcelEditHit.None;
            if (parcels == null)
            {
                return best;
            }

            for (var parcelIndex = 0; parcelIndex < parcels.Count; parcelIndex++)
            {
                var parcel = parcels[parcelIndex];
                for (var vertexIndex = 0; vertexIndex < parcel.Points.Count; vertexIndex++)
                {
                    var distance = math.distance(position, parcel.Points[vertexIndex]);
                    if (distance <= radius && distance < best.Distance)
                    {
                        best = new ParcelEditHit(ParcelEditHitKind.Vertex, parcel.Id, vertexIndex, -1, distance);
                    }
                }
            }

            return best;
        }

        private static ParcelEditHit FindNearestEdge(
            IReadOnlyList<LandParcel> parcels,
            float2 position,
            float radius)
        {
            var best = ParcelEditHit.None;
            if (parcels == null)
            {
                return best;
            }

            for (var parcelIndex = 0; parcelIndex < parcels.Count; parcelIndex++)
            {
                var parcel = parcels[parcelIndex];
                for (var edgeIndex = 0; edgeIndex < parcel.Points.Count; edgeIndex++)
                {
                    var distance = PolygonMath.DistanceToSegment(
                        position,
                        parcel.Points[edgeIndex],
                        parcel.Points[(edgeIndex + 1) % parcel.Points.Count]);
                    if (distance <= radius && distance < best.Distance)
                    {
                        best = new ParcelEditHit(ParcelEditHitKind.Edge, parcel.Id, -1, edgeIndex, distance);
                    }
                }
            }

            return best;
        }

        private static ParcelEditHit FindContainingParcel(IReadOnlyList<LandParcel> parcels, float2 position)
        {
            if (parcels == null)
            {
                return ParcelEditHit.None;
            }

            for (var parcelIndex = parcels.Count - 1; parcelIndex >= 0; parcelIndex--)
            {
                var parcel = parcels[parcelIndex];
                if (PolygonMath.ContainsPoint(parcel.Points, position))
                {
                    return new ParcelEditHit(ParcelEditHitKind.Parcel, parcel.Id, -1, -1, 0f);
                }
            }

            return ParcelEditHit.None;
        }
    }
}
