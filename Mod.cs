using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using CustomLandParcel.Systems;

namespace CustomLandParcel
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(CustomLandParcel)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));
            updateSystem.UpdateAt<ConstructionRestrictionSystem>(SystemUpdatePhase.PostTool);
            updateSystem.UpdateAt<ParcelBoundaryRenderSystem>(SystemUpdatePhase.Rendering);
            log.Info("Registered ConstructionRestrictionSystem at PostTool and ParcelBoundaryRenderSystem at Rendering.");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                log.Info($"Current mod asset at {asset.path}");
            }
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }
}
