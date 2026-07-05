using Game.Rendering;
using UnityEngine;

namespace CustomLandParcel.Patches
{
    internal static class CityBoundaryMeshSystemPatch
    {
        private static bool _mLoggedSuppression;

        private static void Postfix(ref bool __result, ref Mesh mesh, ref Material material)
        {
            if (Mod.Settings == null || Mod.Settings.ShowVanillaUnlockedMapTileBorders)
            {
                _mLoggedSuppression = false;
                return;
            }

            mesh = null;
            material = null;
            __result = false;
            if (!_mLoggedSuppression)
            {
                _mLoggedSuppression = true;
                Mod.log.Info("Vanilla CityBoundary mesh suppressed because ShowVanillaUnlockedMapTileBorders is false.");
            }
        }
    }
}
