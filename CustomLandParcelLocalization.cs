using System;
using System.Collections.Generic;
using System.IO;
using Colossal.Localization;
using Game.SceneFlow;
using Newtonsoft.Json;
using UnityEngine;

namespace CustomLandParcel
{
    internal static class CustomLandParcelLocalization
    {
        private const string LocalizationDirectoryName = "Localization";
        private static readonly List<SourceRegistration> Registrations = new List<SourceRegistration>();

        public static void Register(CustomLandParcelSettings settings)
        {
            var manager = GameManager.instance.localizationManager;
            if (manager == null)
            {
                Mod.log.Warn("Cannot register CustomLandParcel localization: localization manager is not available.");
                return;
            }

            if (Registrations.Count > 0)
            {
                Mod.log.Warn(
                    $"CustomLandParcel localization register requested while {Registrations.Count} source(s) were already registered; removing old sources first.");
                Unregister();
            }

            RegisterLocale(manager, settings, "en-US", "en-US");
            RegisterLocale(manager, settings, "zh-HANS", "zh-HANS");
            RegisterLocale(manager, settings, "zh-CN", "zh-HANS");
            RegisterLocale(manager, settings, "zh", "zh-HANS");

            Mod.log.Info(
                $"Registered CustomLandParcel localization sources from files. activeLocale={manager.activeLocaleId}, sourceCount={Registrations.Count}.");
        }

        public static void Unregister()
        {
            var manager = GameManager.instance?.localizationManager;
            if (manager == null)
            {
                Registrations.Clear();
                return;
            }

            for (var i = 0; i < Registrations.Count; i++)
            {
                var registration = Registrations[i];
                manager.RemoveSource(registration.LocaleId, registration.Source);
            }

            Mod.log.Info($"Unregistered {Registrations.Count} CustomLandParcel localization source(s).");
            Registrations.Clear();
        }

        private static void RegisterLocale(
            LocalizationManager manager,
            CustomLandParcelSettings settings,
            string localeId,
            string fileLocaleId)
        {
            var entries = LoadEntries(settings, fileLocaleId);
            if (entries.Count == 0)
            {
                Mod.log.Warn($"Skipped CustomLandParcel localization locale={localeId}: no entries loaded from {fileLocaleId}.");
                return;
            }

            var source = new MemorySource(entries);
            manager.AddSource(localeId, source);
            Registrations.Add(new SourceRegistration(localeId, source));
            Mod.log.Info($"Registered CustomLandParcel localization locale={localeId}, fileLocale={fileLocaleId}, entries={entries.Count}.");
        }

        private static Dictionary<string, string> LoadEntries(CustomLandParcelSettings settings, string localeId)
        {
            var path = GetLocalizationPath(localeId);
            if (!File.Exists(path))
            {
                Mod.log.Warn($"CustomLandParcel localization file not found: {path}");
                return new Dictionary<string, string>();
            }

            var entries = new Dictionary<string, string>();
            Dictionary<string, string> rawEntries;
            try
            {
                rawEntries = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Mod.log.Error(exception, $"Failed to read CustomLandParcel localization file: {path}");
                return entries;
            }

            if (rawEntries == null)
            {
                Mod.log.Warn($"CustomLandParcel localization file contained no entries: {path}");
                return entries;
            }

            foreach (var pair in rawEntries)
            {
                var key = ResolveKey(settings, pair.Key.Trim());
                if (string.IsNullOrWhiteSpace(key))
                {
                    Mod.log.Warn($"Invalid localization key ignored: file={path}, key='{pair.Key}'.");
                    continue;
                }

                entries[key] = pair.Value ?? string.Empty;
            }

            return entries;
        }

        private static string GetLocalizationPath(string localeId)
        {
            var fileName = localeId + ".json";
            var candidates = new[]
            {
                TryGetAssemblyDirectory(),
                Path.Combine(Application.persistentDataPath, "Mods", nameof(CustomLandParcel)),
                AppDomain.CurrentDomain.BaseDirectory
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                var directory = candidates[i];
                if (string.IsNullOrEmpty(directory))
                {
                    continue;
                }

                var path = Path.Combine(directory, LocalizationDirectoryName, fileName);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return Path.Combine(Application.persistentDataPath, "Mods", nameof(CustomLandParcel),
                LocalizationDirectoryName, fileName);
        }

        private static string TryGetAssemblyDirectory()
        {
            try
            {
                var location = typeof(CustomLandParcelLocalization).Assembly.Location;
                return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
            }
            catch (Exception exception)
            {
                Mod.log.Warn(exception, "Could not resolve CustomLandParcel assembly directory for localization lookup.");
                return null;
            }
        }

        private static string ResolveKey(CustomLandParcelSettings settings, string key)
        {
            if (key == "@settings")
            {
                return settings.GetSettingsLocaleID();
            }

            if (key == "@bindingMap")
            {
                return settings.GetBindingMapLocaleID();
            }

            if (key == "@sectionDescription")
            {
                return "Options.OPTION_DESCRIPTION[" + settings.id + "]";
            }

            const string optionLabelPrefix = "@optionLabel:";
            if (key.StartsWith(optionLabelPrefix, StringComparison.Ordinal))
            {
                return settings.GetOptionLabelLocaleID(key.Substring(optionLabelPrefix.Length));
            }

            const string optionDescriptionPrefix = "@optionDescription:";
            if (key.StartsWith(optionDescriptionPrefix, StringComparison.Ordinal))
            {
                return settings.GetOptionDescLocaleID(key.Substring(optionDescriptionPrefix.Length));
            }

            const string bindingKeyPrefix = "@bindingKey:";
            if (key.StartsWith(bindingKeyPrefix, StringComparison.Ordinal))
            {
                return settings.GetBindingKeyLocaleID(key.Substring(bindingKeyPrefix.Length));
            }

            const string bindingHintPrefix = "@bindingHint:";
            if (key.StartsWith(bindingHintPrefix, StringComparison.Ordinal))
            {
                return settings.GetBindingKeyHintLocaleID(key.Substring(bindingHintPrefix.Length));
            }

            const string bindingDescriptionPrefix = "@bindingDescription:";
            if (key.StartsWith(bindingDescriptionPrefix, StringComparison.Ordinal))
            {
                return "Options.OPTION_DESCRIPTION[" + settings.id + "/" +
                       key.Substring(bindingDescriptionPrefix.Length) + "/Press]";
            }

            return key;
        }

        private sealed class SourceRegistration
        {
            public SourceRegistration(string localeId, MemorySource source)
            {
                LocaleId = localeId;
                Source = source;
            }

            public string LocaleId { get; }

            public MemorySource Source { get; }
        }
    }
}
