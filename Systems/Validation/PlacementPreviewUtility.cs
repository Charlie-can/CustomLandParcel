using Colossal.Mathematics;
using Game.Tools;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    internal static class PlacementPreviewUtility
    {
        public const int CurveSampleCount = 8;

        public static bool ShouldValidate(Temp temp)
        {
            const TempFlags applyFlags = TempFlags.Create | TempFlags.Delete | TempFlags.Modify | TempFlags.Replace |
                                         TempFlags.Upgrade;
            if ((temp.m_Flags & applyFlags) == 0)
            {
                return false;
            }

            if ((temp.m_Flags & (TempFlags.Hidden | TempFlags.Cancel | TempFlags.Select | TempFlags.Optional)) != 0)
            {
                return false;
            }

            return (temp.m_Flags & (TempFlags.Essential | TempFlags.IsLast)) != 0;
        }

        public static bool CurveInsideParcel(Bezier4x3 curve, ParcelStoreSystem parcelStoreSystem)
        {
            return TryGetFirstOutsideCurveSample(curve, parcelStoreSystem, out _, out _);
        }

        public static bool TryGetFirstOutsideCurveSample(
            Bezier4x3 curve,
            ParcelStoreSystem parcelStoreSystem,
            out float3 position,
            out float sample)
        {
            for (var i = 0; i <= CurveSampleCount; i++)
            {
                var t = i / (float)CurveSampleCount;
                position = EvaluateBezier(curve, t);
                if (!parcelStoreSystem.IsBuildable(new float2(position.x, position.z)))
                {
                    sample = t;
                    return false;
                }
            }

            position = default;
            sample = 0f;
            return true;
        }

        public static float3 EvaluateBezier(Bezier4x3 curve, float t)
        {
            var u = 1f - t;
            return u * u * u * curve.a
                   + 3f * u * u * t * curve.b
                   + 3f * u * t * t * curve.c
                   + t * t * t * curve.d;
        }
    }
}
