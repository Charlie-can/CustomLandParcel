using Colossal.Logging;
using CustomLandParcel.Compatibility;
using Game;
using Game.Modding;
using Game.SceneFlow;
using CustomLandParcel.Systems;

namespace CustomLandParcel
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(CustomLandParcel)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        public static CustomLandParcelSettings Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            Settings = new CustomLandParcelSettings(this);
            CustomLandParcelLocalization.Register(Settings);
            Settings.RegisterInOptionsUI();
            Settings.RegisterKeyBindings();
            log.Info("Registered CustomLandParcel settings, localization, and keybindings in the game options UI.");

            updateSystem.UpdateAt<ParcelStoreSystem>(SystemUpdatePhase.Serialize);
            updateSystem.UpdateAt<ParcelBoundaryControlSystem>(SystemUpdatePhase.PostTool);
            updateSystem.UpdateAt<ConstructionRestrictionSystem>(SystemUpdatePhase.PostTool);
            updateSystem.UpdateAt<VanillaMapTileBlockerSystem>(SystemUpdatePhase.PostTool);
            updateSystem.UpdateAt<ParcelPlacementDiagnosticsSystem>(SystemUpdatePhase.PostTool);
            updateSystem.UpdateAt<ConstructionRestrictionPresentationSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAt<ParcelBoundaryRenderSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<ParcelUISystem>(SystemUpdatePhase.UIUpdate);
            log.Info(
                "Registered ParcelStoreSystem at Serialize, ParcelBoundaryControlSystem/ConstructionRestrictionSystem/VanillaMapTileBlockerSystem/ParcelPlacementDiagnosticsSystem at PostTool, ConstructionRestrictionPresentationSystem at PreCulling, ParcelBoundaryRenderSystem at Rendering, ParcelUISystem at UIUpdate.");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                log.Info($"Current mod asset at {asset.path}");
            }
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            Settings?.UnregisterInOptionsUI();
            CustomLandParcelLocalization.Unregister();
            Settings = null;
        }
    }
}
