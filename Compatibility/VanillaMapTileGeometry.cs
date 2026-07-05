using System.Collections.Generic;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using Game.Areas;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomLandParcel.Compatibility
{
    internal static class VanillaMapTileGeometry
    {
        internal static bool TryGetBounds(DynamicBuffer<Node> nodes, out float2 min, out float2 max)
        {
            min = new float2(float.MaxValue, float.MaxValue);
            max = new float2(float.MinValue, float.MinValue);
            if (nodes.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                var xz = nodes[i].m_Position.xz;
                min = math.min(min, xz);
                max = math.max(max, xz);
            }

            return true;
        }

        internal static bool TileOverlapsBuildableParcel(
            DynamicBuffer<Node> nodes,
            IReadOnlyList<LandParcel> parcels,
            float2 parcelMin,
            float2 parcelMax)
        {
            if (!TryGetBounds(nodes, out var tileMin, out var tileMax) ||
                !BoundsIntersect(tileMin, tileMax, parcelMin, parcelMax))
            {
                return false;
            }

            for (var i = 0; i < parcels.Count; i++)
            {
                var parcel = parcels[i];
                if (!parcel.IsBuildable ||
                    !PolygonMath.TryGetBounds(parcel.Points, out var currentMin, out var currentMax) ||
                    !BoundsIntersect(tileMin, tileMax, currentMin, currentMax))
                {
                    continue;
                }

                if (PolygonMath.ContainsPoint(parcel.Points, (tileMin + tileMax) * 0.5f))
                {
                    return true;
                }

                for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                {
                    if (PolygonMath.ContainsPoint(parcel.Points, nodes[nodeIndex].m_Position.xz))
                    {
                        return true;
                    }
                }

                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    if (PointInsideBounds(parcel.Points[pointIndex], tileMin, tileMax))
                    {
                        return true;
                    }
                }

                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    var a = parcel.Points[pointIndex];
                    var b = parcel.Points[(pointIndex + 1) % parcel.Points.Count];
                    if (SegmentIntersectsBounds(a, b, tileMin, tileMax))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool BoundsIntersect(float2 aMin, float2 aMax, float2 bMin, float2 bMax)
        {
            return math.all(aMin <= bMax) && math.all(bMin <= aMax);
        }

        private static bool PointInsideBounds(float2 point, float2 min, float2 max)
        {
            return math.all(point >= min) && math.all(point <= max);
        }

        private static bool SegmentIntersectsBounds(float2 start, float2 end, float2 min, float2 max)
        {
            if (PointInsideBounds(start, min, max) || PointInsideBounds(end, min, max))
            {
                return true;
            }

            var bottomLeft = new float2(min.x, min.y);
            var topLeft = new float2(min.x, max.y);
            var topRight = new float2(max.x, max.y);
            var bottomRight = new float2(max.x, min.y);
            return SegmentsIntersect(start, end, bottomLeft, topLeft)
                   || SegmentsIntersect(start, end, topLeft, topRight)
                   || SegmentsIntersect(start, end, topRight, bottomRight)
                   || SegmentsIntersect(start, end, bottomRight, bottomLeft);
        }

        private static bool SegmentsIntersect(float2 a, float2 b, float2 c, float2 d)
        {
            const float Epsilon = 0.001f;
            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) &&
                ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon)))
            {
                return true;
            }

            return math.abs(abC) <= Epsilon && PointOnSegment(c, a, b)
                   || math.abs(abD) <= Epsilon && PointOnSegment(d, a, b)
                   || math.abs(cdA) <= Epsilon && PointOnSegment(a, c, d)
                   || math.abs(cdB) <= Epsilon && PointOnSegment(b, c, d);
        }

        private static bool PointOnSegment(float2 point, float2 start, float2 end)
        {
            return point.x >= math.min(start.x, end.x) - 0.001f
                   && point.x <= math.max(start.x, end.x) + 0.001f
                   && point.y >= math.min(start.y, end.y) - 0.001f
                   && point.y <= math.max(start.y, end.y) + 0.001f;
        }

        private static float Cross(float2 origin, float2 a, float2 b)
        {
            var oa = a - origin;
            var ob = b - origin;
            return oa.x * ob.y - oa.y * ob.x;
        }
    }
}
