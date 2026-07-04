using System;
using System.Collections.Generic;
using CustomLandParcel.Data;
using Unity.Mathematics;

namespace CustomLandParcel.Geometry
{
    internal readonly struct ParcelPriceResult
    {
        public ParcelPriceResult(int price, float area, float2 centroid, float locationFactor, float ownershipFactor)
        {
            Price = price;
            Area = area;
            Centroid = centroid;
            LocationFactor = locationFactor;
            OwnershipFactor = ownershipFactor;
        }

        public int Price { get; }

        public float Area { get; }

        public float2 Centroid { get; }

        public float LocationFactor { get; }

        public float OwnershipFactor { get; }

        public string ToLogString()
        {
            return
                $"price={Price}, area={Area:F1}, centroid={ParcelGeometry.Format(Centroid)}, locationFactor={LocationFactor:F3}, ownershipFactor={OwnershipFactor:F3}";
        }
    }

    internal static class ParcelPriceCalculator
    {
        private const int MinimumPrice = 1000;
        private const float AreaPriceFactor = 0.25f;
        private const float OwnershipStep = 0.08f;
        private const float LocationScale = 12000f;
        private const float MaxLocationFactor = 1.35f;

        public static ParcelPriceResult Calculate(LandParcel parcel, IReadOnlyList<LandParcel> allParcels)
        {
            if (parcel == null)
            {
                return new ParcelPriceResult(0, 0f, float2.zero, 1f, 1f);
            }

            var area = PolygonMath.Area(parcel.Points);
            var centroid = PolygonMath.Centroid(parcel.Points);
            var locationFactor = CalculateLocationFactor(centroid);
            var ownershipFactor = CalculateOwnershipFactor(allParcels);
            var rawPrice = area * AreaPriceFactor * locationFactor * ownershipFactor;
            var price = math.max(MinimumPrice, (int)math.round(rawPrice));
            return new ParcelPriceResult(price, area, centroid, locationFactor, ownershipFactor);
        }

        private static float CalculateLocationFactor(float2 centroid)
        {
            var normalizedDistance = math.length(centroid) / LocationScale;
            return math.clamp(1f + normalizedDistance * 0.12f, 1f, MaxLocationFactor);
        }

        private static float CalculateOwnershipFactor(IReadOnlyList<LandParcel> allParcels)
        {
            var activeCount = 0;
            if (allParcels != null)
            {
                for (var i = 0; i < allParcels.Count; i++)
                {
                    if (allParcels[i].IsBuildable)
                    {
                        activeCount++;
                    }
                }
            }

            return 1f + Math.Max(0, activeCount) * OwnershipStep;
        }
    }
}
