using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    internal readonly struct ParcelBounds
    {
        public static readonly ParcelBounds Default = new ParcelBounds(
            new float2(-500f, -500f),
            new float2(500f, 500f));

        public ParcelBounds(float2 min, float2 max)
        {
            Min = min;
            Max = max;
        }

        public float2 Min { get; }

        public float2 Max { get; }

        public float2 Size => Max - Min;

        public float2 Center => (Min + Max) * 0.5f;

        public ParcelBounds Normalize()
        {
            var min = math.min(Min, Max);
            var max = math.max(Min, Max);
            return new ParcelBounds(min, max);
        }

        public ParcelBounds Move(float2 delta)
        {
            return new ParcelBounds(Min + delta, Max + delta);
        }

        public ParcelBounds Resize(float amount)
        {
            var center = Center;
            var halfSize = math.max(Size * 0.5f + new float2(amount, amount), new float2(50f, 50f));
            return new ParcelBounds(center - halfSize, center + halfSize);
        }

        public bool Contains(float2 point)
        {
            return point.x >= Min.x && point.x <= Max.x && point.y >= Min.y && point.y <= Max.y;
        }

        public override string ToString()
        {
            return $"{Format(Min)}..{Format(Max)}";
        }

        public static string Format(float2 value)
        {
            return $"({value.x:F1}, {value.y:F1})";
        }
    }
}