using Colossal.Logging;
using CustomLandParcel.Compatibility;
using CustomLandParcel.Patches;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Serialization;
using Game.Tools;
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

            var executableAssetPath = string.Empty;
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                executableAssetPath = asset.path;
                log.Info($"Current mod asset at {asset.path}");
            }

            Settings = new CustomLandParcelSettings(this);
            CustomLandParcelLocalization.Register(Settings, executableAssetPath);
            Settings.RegisterInOptionsUI();
            Settings.RegisterKeyBindings();
            log.Info("Registered CustomLandParcel settings, localization, and keybindings in the game options UI.");

            CustomLandParcelPatcher.Apply(executableAssetPath);

            updateSystem.UpdateAt<ParcelStoreSystem>(SystemUpdatePhase.Serialize);
            updateSystem.UpdateBefore<PreSerialize<VanillaMapTileUnlockSystem>>(SystemUpdatePhase.Serialize);
            updateSystem.UpdateAt<VanillaMapTileUnlockSystem>(SystemUpdatePhase.PreTool);
            updateSystem.UpdateAt<ParcelEditToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<ParcelBoundaryControlSystem>(SystemUpdatePhase.PostTool);
            updateSystem.UpdateAt<ConstructionRestrictionSystem>(SystemUpdatePhase.PostTool);
            updateSystem.UpdateBefore<ParcelDeletionRestrictionSystem, ToolApplySystem>(SystemUpdatePhase.ApplyTool);
            if (Settings.EnablePlacementDiagnostics)
            {
                updateSystem.UpdateAt<ParcelPlacementDiagnosticsSystem>(SystemUpdatePhase.PostTool);
                log.Info("Registered ParcelPlacementDiagnosticsSystem at PostTool because diagnostics are enabled.");
            }

            updateSystem.UpdateAt<VanillaMapTileVisibilitySystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAt<ConstructionRestrictionPresentationSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAt<ParcelBoundaryRenderSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<ParcelUISystem>(SystemUpdatePhase.UIUpdate);
            log.Info(
                $"Registered ParcelStoreSystem at Serialize, PreSerialize<VanillaMapTileUnlockSystem> before Serialize, VanillaMapTileUnlockSystem at PreTool, ParcelEditToolSystem at ToolUpdate, ParcelBoundaryControlSystem/ConstructionRestrictionSystem at PostTool, ParcelDeletionRestrictionSystem before ToolApplySystem at ApplyTool, diagnosticsEnabled={Settings.EnablePlacementDiagnostics}, VanillaMapTileVisibilitySystem/ConstructionRestrictionPresentationSystem at PreCulling, ParcelBoundaryRenderSystem at Rendering, ParcelUISystem at UIUpdate.");

        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            CustomLandParcelPatcher.Unapply();
            Settings?.UnregisterInOptionsUI();
            CustomLandParcelLocalization.Unregister();
            Settings = null;
        }
    }
}
