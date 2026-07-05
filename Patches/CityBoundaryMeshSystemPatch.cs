using Game.Rendering;
using UnityEngine;

namespace CustomLandParcel.Patches
{
    internal static class CityBoundaryMeshSystemPatch
    {
        private static void Postfix(ref bool __result, ref Mesh mesh, ref Material material)
        {
            if (Mod.Settings == null || Mod.Settings.ShowVanillaUnlockedMapTileBorders)
            {
                return;
            }

            mesh = null;
            material = null;
            __result = false;
        }
    }
}
