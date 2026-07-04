using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace CustomLandParcel.Geometry
{
    internal static class PolygonMath
    {
        public static bool ContainsPoint(IReadOnlyList<float2> polygon, float2 point)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            var inside = false;
            var j = polygon.Count - 1;
            for (var i = 0; i < polygon.Count; i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                var crossesY = (pi.y > point.y) != (pj.y > point.y);
                if (crossesY)
                {
                    var xAtY = (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x;
                    if (point.x < xAtY)
                    {
                        inside = !inside;
                    }
                }

                j = i;
            }

            return inside;
        }

        public static float Area(IReadOnlyList<float2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return 0f;
            }

            var sum = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                sum += a.x * b.y - b.x * a.y;
            }

            return math.abs(sum) * 0.5f;
        }

        public static float2 Centroid(IReadOnlyList<float2> polygon)
        {
            if (polygon == null || polygon.Count == 0)
            {
                return float2.zero;
            }

            var sum = polygon.Aggregate(float2.zero, (current, t) => current + t);

            return sum / polygon.Count;
        }

        public static bool TryGetBounds(IReadOnlyList<float2> polygon, out float2 min, out float2 max)
        {
            min = new float2(float.MaxValue, float.MaxValue);
            max = new float2(float.MinValue, float.MinValue);
            if (polygon == null || polygon.Count == 0)
            {
                return false;
            }

            foreach (var t in polygon)
            {
                min = math.min(min, t);
                max = math.max(max, t);
            }

            return math.all(min <= max);
        }

        public static float DistanceToSegment(float2 point, float2 a, float2 b)
        {
            var ab = b - a;
            var lengthSq = math.lengthsq(ab);
            if (lengthSq <= 0.0001f)
            {
                return math.distance(point, a);
            }

            var t = math.clamp(math.dot(point - a, ab) / lengthSq, 0f, 1f);
            return math.distance(point, a + ab * t);
        }
    }
}
