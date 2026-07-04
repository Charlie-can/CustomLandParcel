using System.Collections.Generic;
using Colossal.Localization;
using Game.SceneFlow;

namespace CustomLandParcel
{
    internal static class CustomLandParcelLocalization
    {
        private static readonly List<SourceRegistration> Registrations = new List<SourceRegistration>();

        public static void Register(CustomLandParcelSettings settings)
        {
            var manager = GameManager.instance.localizationManager;
            if (manager == null)
            {
                Mod.log.Warn("Cannot register CustomLandParcel localization: localization manager is not available.");
                return;
            }

            RegisterLocale(manager, "en-US", CreateEnglish(settings));
            RegisterLocale(manager, "zh-HANS", CreateChinese(settings));
            RegisterLocale(manager, "zh-CN", CreateChinese(settings));
            RegisterLocale(manager, "zh", CreateChinese(settings));

            Mod.log.Info(
                $"Registered CustomLandParcel localization sources. activeLocale={manager.activeLocaleId}, sourceCount={Registrations.Count}.");
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
            string localeId,
            Dictionary<string, string> entries)
        {
            var source = new MemorySource(entries);
            manager.AddSource(localeId, source);
            Registrations.Add(new SourceRegistration(localeId, source));
        }

        private static Dictionary<string, string> CreateChinese(CustomLandParcelSettings settings)
        {
            return CreateEntries(
                settings,
                "自定义地块边界",
                "编辑当前存档中的可建造地块边界。",
                "切换地块编辑模式",
                "开启或关闭自定义地块边界编辑模式。",
                "向北移动地块",
                "将可建造地块向北移动一步。",
                "向南移动地块",
                "将可建造地块向南移动一步。",
                "向西移动地块",
                "将可建造地块向西移动一步。",
                "向东移动地块",
                "将可建造地块向东移动一步。",
                "扩大地块",
                "从中心向外扩大可建造地块。",
                "缩小地块",
                "从边缘向中心缩小可建造地块。");
        }

        private static Dictionary<string, string> CreateEnglish(CustomLandParcelSettings settings)
        {
            return CreateEntries(
                settings,
                "Custom Land Parcel",
                "Edit the buildable parcel boundary stored in the current save.",
                "Toggle parcel edit mode",
                "Turns custom parcel boundary editing on or off.",
                "Move parcel north",
                "Moves the buildable parcel one step north.",
                "Move parcel south",
                "Moves the buildable parcel one step south.",
                "Move parcel west",
                "Moves the buildable parcel one step west.",
                "Move parcel east",
                "Moves the buildable parcel one step east.",
                "Grow parcel",
                "Expands the buildable parcel outward from its center.",
                "Shrink parcel",
                "Shrinks the buildable parcel inward toward its center.");
        }

        private static Dictionary<string, string> CreateEntries(
            CustomLandParcelSettings settings,
            string sectionName,
            string sectionDescription,
            string toggleLabel,
            string toggleDescription,
            string moveNorthLabel,
            string moveNorthDescription,
            string moveSouthLabel,
            string moveSouthDescription,
            string moveWestLabel,
            string moveWestDescription,
            string moveEastLabel,
            string moveEastDescription,
            string growLabel,
            string growDescription,
            string shrinkLabel,
            string shrinkDescription)
        {
            var entries = new Dictionary<string, string>
            {
                [settings.GetSettingsLocaleID()] = sectionName,
                [settings.GetBindingMapLocaleID()] = sectionName,
                ["Options.OPTION_DESCRIPTION[" + settings.id + "]"] = sectionDescription
            };

            AddAction(
                entries,
                settings,
                nameof(CustomLandParcelSettings.ToggleEditMode),
                CustomLandParcelSettings.ToggleEditModeAction,
                toggleLabel,
                toggleDescription);
            AddAction(
                entries,
                settings,
                nameof(CustomLandParcelSettings.MoveNorth),
                CustomLandParcelSettings.MoveNorthAction,
                moveNorthLabel,
                moveNorthDescription);
            AddAction(
                entries,
                settings,
                nameof(CustomLandParcelSettings.MoveSouth),
                CustomLandParcelSettings.MoveSouthAction,
                moveSouthLabel,
                moveSouthDescription);
            AddAction(
                entries,
                settings,
                nameof(CustomLandParcelSettings.MoveWest),
                CustomLandParcelSettings.MoveWestAction,
                moveWestLabel,
                moveWestDescription);
            AddAction(
                entries,
                settings,
                nameof(CustomLandParcelSettings.MoveEast),
                CustomLandParcelSettings.MoveEastAction,
                moveEastLabel,
                moveEastDescription);
            AddAction(
                entries,
                settings,
                nameof(CustomLandParcelSettings.Grow),
                CustomLandParcelSettings.GrowAction,
                growLabel,
                growDescription);
            AddAction(
                entries,
                settings,
                nameof(CustomLandParcelSettings.Shrink),
                CustomLandParcelSettings.ShrinkAction,
                shrinkLabel,
                shrinkDescription);

            return entries;
        }

        private static void AddAction(
            IDictionary<string, string> entries,
            CustomLandParcelSettings settings,
            string propertyName,
            string actionName,
            string label,
            string description)
        {
            entries[settings.GetOptionLabelLocaleID(propertyName)] = label;
            entries[settings.GetOptionDescLocaleID(propertyName)] = description;
            entries[settings.GetBindingKeyLocaleID(actionName)] = label;
            entries[settings.GetBindingKeyHintLocaleID(actionName)] = label;
            entries["Options.OPTION_DESCRIPTION[" + settings.id + "/" + actionName + "/Press]"] = description;
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