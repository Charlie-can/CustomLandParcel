using Game;
using Game.Areas;
using Game.Common;
using Game.Tools;
using Unity.Entities;

namespace CustomLandParcel.Compatibility
{
    /// <summary>
    /// Hides vanilla MapTile border overlays after tools have generated Temp/Error/Warning areas and before rendering.
    /// AreaBorderRenderSystem only draws MapTile borders from those transient area entities, so permanent MapTiles are not touched here.
    /// </summary>
    public partial class VanillaMapTileVisibilitySystem : GameSystemBase
    {
        private const int LogCooldownFrames = 300;

        private EntityQuery _mRenderableMapTileBorderQuery;
        private EntityQuery _mHiddenBySettingQuery;
        private VanillaMapTileVisibilitySync _mVisibilitySync;
        private int _mLogCooldownFrames;
        private bool _mLastShowVanillaBorders = true;
        private bool _mLegacyHiddenMarkersCleared;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mRenderableMapTileBorderQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MapTile>(),
                    ComponentType.ReadOnly<Node>()
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Error>(),
                    ComponentType.ReadOnly<Warning>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<VanillaMapTileBlocker>()
                }
            });
            _mHiddenBySettingQuery = GetEntityQuery(ComponentType.ReadOnly<VanillaMapTileHiddenByParcelSetting>());
            _mVisibilitySync = new VanillaMapTileVisibilitySync(
                EntityManager,
                MarkUpdated,
                ShouldShowVanillaUnlockedMapTileBorders);
            Mod.log.Info("VanillaMapTileVisibilitySystem enabled for render-time vanilla MapTile border visibility.");
        }

        protected override void OnDestroy()
        {
            _mVisibilitySync.RestoreMapTileVisibility(_mHiddenBySettingQuery, "visibility system destroyed");
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            var showVanillaBorders = ShouldShowVanillaUnlockedMapTileBorders();
            var changed = 0;
            if (!_mLegacyHiddenMarkersCleared)
            {
                changed += _mVisibilitySync.RestoreMapTileVisibility(
                    _mHiddenBySettingQuery,
                    "legacy visibility marker cleanup");
                _mLegacyHiddenMarkersCleared = true;
            }

            changed += showVanillaBorders
                ? _mVisibilitySync.RestoreMapTileVisibility(_mHiddenBySettingQuery, "render visibility enabled")
                : _mVisibilitySync.SyncMapTileBorderVisibility(
                    _mRenderableMapTileBorderQuery,
                    "render visibility refresh");

            if (_mLastShowVanillaBorders != showVanillaBorders)
            {
                _mLastShowVanillaBorders = showVanillaBorders;
                _mLogCooldownFrames = 0;
                Mod.log.Info(
                    $"Vanilla MapTile border visibility setting changed: showVanillaBorders={showVanillaBorders}, changedEntities={changed}.");
                return;
            }

            if (changed > 0 && _mLogCooldownFrames <= 0)
            {
                Mod.log.Info(
                    $"Vanilla MapTile border visibility synchronized: showVanillaBorders={showVanillaBorders}, changedEntities={changed}.");
                _mLogCooldownFrames = LogCooldownFrames;
            }

            if (_mLogCooldownFrames > 0)
            {
                _mLogCooldownFrames--;
            }
        }

        private static bool ShouldShowVanillaUnlockedMapTileBorders()
        {
            return Mod.Settings == null || Mod.Settings.ShowVanillaUnlockedMapTileBorders;
        }

        private void MarkUpdated(Entity entity)
        {
            if (!EntityManager.HasComponent<Updated>(entity))
            {
                EntityManager.AddComponentData(entity, default(Updated));
            }
        }
    }
}
