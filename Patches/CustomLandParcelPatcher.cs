using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Rendering;

namespace CustomLandParcel.Patches
{
    internal static class CustomLandParcelPatcher
    {
        private const string HarmonyId = "com.charlie.customlandparcel";
        private static object _mHarmony;
        private static Type _mHarmonyType;

        public static void Apply()
        {
            if (_mHarmony != null)
            {
                return;
            }

            try
            {
                var harmonyAssembly = LoadHarmonyAssembly();
                _mHarmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
                var harmonyMethodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true);
                var original = typeof(CityBoundaryMeshSystem).GetMethod(
                    nameof(CityBoundaryMeshSystem.GetBoundaryMesh),
                    BindingFlags.Instance | BindingFlags.Public);
                var postfix = typeof(CityBoundaryMeshSystemPatch).GetMethod(
                    "Postfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (original == null || postfix == null)
                {
                    throw new MissingMethodException("CityBoundaryMeshSystem.GetBoundaryMesh patch target was not found.");
                }

                _mHarmony = Activator.CreateInstance(_mHarmonyType, HarmonyId);
                var postfixMethod = Activator.CreateInstance(harmonyMethodType, postfix);
                var patchMethod = _mHarmonyType
                    .GetMethods()
                    .First(method => method.Name == "Patch" && method.GetParameters().Length == 5);
                patchMethod.Invoke(_mHarmony, new[] { original, null, postfixMethod, null, null });
                Mod.log.Info("CustomLandParcel Harmony patches applied: CityBoundaryMeshSystem.GetBoundaryMesh postfix.");
            }
            catch (Exception exception)
            {
                _mHarmony = null;
                _mHarmonyType = null;
                Mod.log.Error(exception, "Failed to apply CustomLandParcel Harmony patches.");
            }
        }

        public static void Unapply()
        {
            if (_mHarmony == null)
            {
                return;
            }

            try
            {
                _mHarmonyType.GetMethod("UnpatchAll", new[] { typeof(string) })?.Invoke(_mHarmony, new object[] { HarmonyId });
                Mod.log.Info("CustomLandParcel Harmony patches removed.");
            }
            catch (Exception exception)
            {
                Mod.log.Warn(exception, "Failed to remove CustomLandParcel Harmony patches.");
            }
            finally
            {
                _mHarmony = null;
                _mHarmonyType = null;
            }
        }

        private static Assembly LoadHarmonyAssembly()
        {
            try
            {
                return Assembly.Load("0Harmony");
            }
            catch
            {
                var assemblyPath = Path.Combine(
                    Path.GetDirectoryName(typeof(CustomLandParcelPatcher).Assembly.Location) ?? string.Empty,
                    "0Harmony.dll");
                return Assembly.LoadFrom(assemblyPath);
            }
        }
    }
}
