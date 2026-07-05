using Game.Input;
using Game.Modding;
using Game.Settings;

namespace CustomLandParcel
{
    public sealed class CustomLandParcelSettings : ModSetting
    {
        public const string ToggleEditModeAction = "ToggleParcelEditMode";
        public const string MoveNorthAction = "MoveParcelNorth";
        public const string MoveSouthAction = "MoveParcelSouth";
        public const string MoveWestAction = "MoveParcelWest";
        public const string MoveEastAction = "MoveParcelEast";
        public const string GrowAction = "GrowParcel";
        public const string ShrinkAction = "ShrinkParcel";

        private const bool DefaultEnableVanillaMapTileCompatibility = true;
        private const bool DefaultShowVanillaUnlockedMapTileBorders = true;
        private const int DefaultParcelBoundaryRed = 51;
        private const int DefaultParcelBoundaryGreen = 255;
        private const int DefaultParcelBoundaryBlue = 148;
        private const int DefaultParcelBoundaryOpacity = 90;
        private const int DefaultParcelFillOpacity = 28;
        private const int DefaultParcelBoundaryWidth = 7;
        private const bool DefaultEnablePlacementDiagnostics = false;

        public CustomLandParcelSettings(IMod mod)
            : base(mod)
        {
        }

        public bool EnableVanillaMapTileCompatibility { get; set; } = DefaultEnableVanillaMapTileCompatibility;

        public bool ShowVanillaUnlockedMapTileBorders { get; set; } = DefaultShowVanillaUnlockedMapTileBorders;

        [SettingsUIHidden]
        public int ParcelBoundaryRed { get; set; } = DefaultParcelBoundaryRed;

        [SettingsUIHidden]
        public int ParcelBoundaryGreen { get; set; } = DefaultParcelBoundaryGreen;

        [SettingsUIHidden]
        public int ParcelBoundaryBlue { get; set; } = DefaultParcelBoundaryBlue;

        [SettingsUIHidden]
        public int ParcelBoundaryOpacity { get; set; } = DefaultParcelBoundaryOpacity;

        [SettingsUIHidden]
        public int ParcelFillOpacity { get; set; } = DefaultParcelFillOpacity;

        [SettingsUIHidden]
        public int ParcelBoundaryWidth { get; set; } = DefaultParcelBoundaryWidth;

        [SettingsUIHidden]
        public bool EnablePlacementDiagnostics { get; set; } = DefaultEnablePlacementDiagnostics;

        [SettingsUIKeyboardBinding(BindingKeyboard.B, ToggleEditModeAction, alt: true, ctrl: true)]
        public ProxyBinding ToggleEditMode { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.UpArrow, MoveNorthAction, alt: true)]
        public ProxyBinding MoveNorth { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.DownArrow, MoveSouthAction, alt: true)]
        public ProxyBinding MoveSouth { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.LeftArrow, MoveWestAction, alt: true)]
        public ProxyBinding MoveWest { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.RightArrow, MoveEastAction, alt: true)]
        public ProxyBinding MoveEast { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.Equals, GrowAction, alt: true)]
        public ProxyBinding Grow { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.Minus, ShrinkAction, alt: true)]
        public ProxyBinding Shrink { get; set; }

        public override void SetDefaults()
        {
            EnableVanillaMapTileCompatibility = DefaultEnableVanillaMapTileCompatibility;
            ShowVanillaUnlockedMapTileBorders = DefaultShowVanillaUnlockedMapTileBorders;
            ParcelBoundaryRed = DefaultParcelBoundaryRed;
            ParcelBoundaryGreen = DefaultParcelBoundaryGreen;
            ParcelBoundaryBlue = DefaultParcelBoundaryBlue;
            ParcelBoundaryOpacity = DefaultParcelBoundaryOpacity;
            ParcelFillOpacity = DefaultParcelFillOpacity;
            ParcelBoundaryWidth = DefaultParcelBoundaryWidth;
            EnablePlacementDiagnostics = DefaultEnablePlacementDiagnostics;
        }

        public bool SetParcelAppearanceValue(string key, int value)
        {
            switch (key)
            {
                case nameof(ParcelBoundaryRed):
                    ParcelBoundaryRed = Clamp(value, 0, 255);
                    break;
                case nameof(ParcelBoundaryGreen):
                    ParcelBoundaryGreen = Clamp(value, 0, 255);
                    break;
                case nameof(ParcelBoundaryBlue):
                    ParcelBoundaryBlue = Clamp(value, 0, 255);
                    break;
                case nameof(ParcelBoundaryOpacity):
                    ParcelBoundaryOpacity = Clamp(value, 0, 100);
                    break;
                case nameof(ParcelFillOpacity):
                    ParcelFillOpacity = Clamp(value, 0, 100);
                    break;
                case nameof(ParcelBoundaryWidth):
                    ParcelBoundaryWidth = Clamp(value, 2, 14);
                    break;
                default:
                    return false;
            }

            ApplyAndSave();
            return true;
        }

        public void SetShowVanillaUnlockedMapTileBorders(bool show)
        {
            ShowVanillaUnlockedMapTileBorders = show;
            ApplyAndSave();
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
