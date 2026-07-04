using System.Collections.Generic;
using CustomLandParcel.Data;
using Unity.Mathematics;

namespace CustomLandParcel.Geometry
{
    internal static class ParcelGeometry
    {
        public const int MinimumVertexCount = 3;

        public static readonly float2 DefaultCenter = float2.zero;

        public static readonly float2 DefaultSize = new float2(1000f, 1000f);

        public static readonly float2 MinimumSize = new float2(100f, 100f);

        public static LandParcel CreateRectangle(string name, float2 center, float2 size)
        {
            var half = math.max(size * 0.5f, MinimumSize * 0.5f);
            var points = new[]
            {
                center + new float2(-half.x, -half.y),
                center + new float2(-half.x, half.y),
                center + new float2(half.x, half.y),
                center + new float2(half.x, -half.y)
            };

            var parcel = new LandParcel(System.Guid.NewGuid(), name, points);
            RecalculatePrice(parcel);
            return parcel;
        }

        public static int RecalculatePrice(LandParcel parcel)
        {
            parcel.Price = ParcelPriceCalculator.Calculate(parcel, null).Price;
            return parcel.Price;
        }

        public static bool TryGetUnionBounds(IEnumerable<LandParcel> parcels, out float2 min, out float2 max)
        {
            min = new float2(float.MaxValue, float.MaxValue);
            max = new float2(float.MinValue, float.MinValue);
            var found = false;
            foreach (var parcel in parcels)
            {
                if (!PolygonMath.TryGetBounds(parcel.Points, out var parcelMin, out var parcelMax))
                {
                    continue;
                }

                min = math.min(min, parcelMin);
                max = math.max(max, parcelMax);
                found = true;
            }

            return found;
        }

        public static string Format(float2 value)
        {
            return $"({value.x:F1}, {value.y:F1})";
        }
    }
}
