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

        public static void Apply(string executableAssetPath)
        {
            if (_mHarmony != null)
            {
                return;
            }

            try
            {
                var harmonyAssembly = LoadHarmonyAssembly(executableAssetPath);
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

        private static Assembly LoadHarmonyAssembly(string executableAssetPath)
        {
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "0Harmony");
            if (loadedAssembly != null)
            {
                Mod.log.Info($"CustomLandParcel Harmony assembly already loaded: {SafeAssemblyLocation(loadedAssembly)}.");
                return loadedAssembly;
            }

            try
            {
                var assembly = Assembly.Load("0Harmony");
                Mod.log.Info($"CustomLandParcel Harmony assembly loaded by name: {SafeAssemblyLocation(assembly)}.");
                return assembly;
            }
            catch (Exception exception)
            {
                Mod.log.Warn(exception, "CustomLandParcel Harmony assembly was not loadable by name; probing mod folders.");
            }

            foreach (var directory in GetHarmonyProbeDirectories(executableAssetPath))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                try
                {
                    var assemblyPath = Path.Combine(directory, "0Harmony.dll");
                    if (!File.Exists(assemblyPath))
                    {
                        continue;
                    }

                    var assembly = Assembly.LoadFrom(assemblyPath);
                    Mod.log.Info($"CustomLandParcel Harmony assembly loaded from {assemblyPath}.");
                    return assembly;
                }
                catch (Exception exception)
                {
                    Mod.log.Warn(exception, $"CustomLandParcel Harmony probe failed for directory '{directory}'.");
                }
            }

            throw new FileNotFoundException("0Harmony.dll was not found in loaded assemblies or known mod folders.");
        }

        private static string[] GetHarmonyProbeDirectories(string executableAssetPath)
        {
            return new[]
            {
                SafeDirectoryName(executableAssetPath),
                SafeDirectoryName(typeof(CustomLandParcelPatcher).Assembly.Location),
                SafeDirectoryName(typeof(Mod).Assembly.Location),
                AppDomain.CurrentDomain.BaseDirectory,
                Directory.GetCurrentDirectory()
            };
        }

        private static string SafeDirectoryName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    return path;
                }

                return Path.GetDirectoryName(path);
            }
            catch
            {
                return null;
            }
        }

        private static string SafeAssemblyLocation(Assembly assembly)
        {
            try
            {
                return string.IsNullOrWhiteSpace(assembly.Location) ? "<no location>" : assembly.Location;
            }
            catch
            {
                return "<unavailable>";
            }
        }
    }
}
