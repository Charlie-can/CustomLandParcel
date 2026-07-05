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

        public CustomLandParcelSettings(IMod mod)
            : base(mod)
        {
        }

        public bool EnableVanillaMapTileCompatibility { get; set; } = DefaultEnableVanillaMapTileCompatibility;

        public bool ShowVanillaUnlockedMapTileBorders { get; set; } = DefaultShowVanillaUnlockedMapTileBorders;

        [SettingsUISlider(min = 0f, max = 255f, step = 1f, unit = "integer")]
        public int ParcelBoundaryRed { get; set; } = DefaultParcelBoundaryRed;

        [SettingsUISlider(min = 0f, max = 255f, step = 1f, unit = "integer")]
        public int ParcelBoundaryGreen { get; set; } = DefaultParcelBoundaryGreen;

        [SettingsUISlider(min = 0f, max = 255f, step = 1f, unit = "integer")]
        public int ParcelBoundaryBlue { get; set; } = DefaultParcelBoundaryBlue;

        [SettingsUISlider(min = 0f, max = 100f, step = 1f, unit = "percentage")]
        public int ParcelBoundaryOpacity { get; set; } = DefaultParcelBoundaryOpacity;

        [SettingsUISlider(min = 0f, max = 100f, step = 1f, unit = "percentage")]
        public int ParcelFillOpacity { get; set; } = DefaultParcelFillOpacity;

        [SettingsUISlider(min = 2f, max = 14f, step = 1f, unit = "integer")]
        public int ParcelBoundaryWidth { get; set; } = DefaultParcelBoundaryWidth;

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
        }
    }
}
