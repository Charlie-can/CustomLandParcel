using System;
using Game.Areas;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomLandParcel.Compatibility
{
    internal sealed class VanillaMapTileVisibilitySync
    {
        private readonly EntityManager _mEntityManager;
        private readonly Action<Entity> _mMarkUpdated;
        private readonly Func<bool> _mShouldShowVanillaUnlockedMapTileBorders;

        internal VanillaMapTileVisibilitySync(
            EntityManager entityManager,
            Action<Entity> markUpdated,
            Func<bool> shouldShowVanillaUnlockedMapTileBorders)
        {
            _mEntityManager = entityManager;
            _mMarkUpdated = markUpdated;
            _mShouldShowVanillaUnlockedMapTileBorders = shouldShowVanillaUnlockedMapTileBorders;
        }

        internal bool ApplyMapTileVisibility(Entity entity)
        {
            if (_mShouldShowVanillaUnlockedMapTileBorders())
            {
                return RestoreMapTileVisibility(entity);
            }

            if (!_mEntityManager.HasComponent<Hidden>(entity))
            {
                _mEntityManager.AddComponentData(entity, default(VanillaMapTileHiddenByParcelSetting));
                _mEntityManager.AddComponentData(entity, default(Hidden));
                _mMarkUpdated(entity);
                return true;
            }

            return false;
        }

        internal int SyncVanillaOwnedMapTileVisibility(EntityQuery mapTileQuery, string reason)
        {
            if (_mShouldShowVanillaUnlockedMapTileBorders())
            {
                return RestoreMapTileVisibilityBySetting(mapTileQuery, reason);
            }

            using var entities = mapTileQuery.ToEntityArray(Allocator.Temp);
            var hidden = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!_mEntityManager.Exists(entity) ||
                    _mEntityManager.HasComponent<Native>(entity) ||
                    _mEntityManager.HasComponent<Hidden>(entity))
                {
                    continue;
                }

                _mEntityManager.AddComponentData(entity, default(VanillaMapTileHiddenByParcelSetting));
                _mEntityManager.AddComponentData(entity, default(Hidden));
                _mMarkUpdated(entity);
                hidden++;
            }

            if (hidden > 0)
            {
                Mod.log.Info($"Hidden {hidden} vanilla owned map tile border(s) by parcel setting ({reason}).");
            }

            return hidden;
        }

        internal int RestoreMapTileVisibility(EntityQuery hiddenBySettingQuery, string reason)
        {
            using var marked = hiddenBySettingQuery.ToEntityArray(Allocator.Temp);
            var shown = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                if (RestoreMapTileVisibility(marked[i]))
                {
                    shown++;
                }
            }

            if (shown > 0)
            {
                Mod.log.Info($"Restored visibility for {shown} vanilla map tile(s) hidden by parcel setting ({reason}).");
            }

            return shown;
        }

        private int RestoreMapTileVisibilityBySetting(EntityQuery mapTileQuery, string reason)
        {
            using var entities = mapTileQuery.ToEntityArray(Allocator.Temp);
            var shown = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (_mEntityManager.Exists(entity) &&
                    _mEntityManager.HasComponent<VanillaMapTileHiddenByParcelSetting>(entity) &&
                    RestoreMapTileVisibility(entity))
                {
                    shown++;
                }
            }

            if (shown > 0)
            {
                Mod.log.Info($"Restored {shown} vanilla owned map tile border(s) by parcel setting ({reason}).");
            }

            return shown;
        }

        internal int RestoreVisibleMapTilesOutsideBuildableBounds(
            EntityQuery hiddenBySettingQuery,
            float2 parcelMin,
            float2 parcelMax,
            Func<Entity, float2, float2, bool> overlapsBuildableParcel)
        {
            using var marked = hiddenBySettingQuery.ToEntityArray(Allocator.Temp);
            var shown = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                var entity = marked[i];
                var shouldStayHidden = _mEntityManager.Exists(entity)
                                       && _mEntityManager.HasComponent<MapTile>(entity)
                                       && !_mEntityManager.HasComponent<VanillaMapTileBlocker>(entity)
                                       && !_mShouldShowVanillaUnlockedMapTileBorders()
                                       && (!_mEntityManager.HasComponent<Native>(entity) ||
                                           overlapsBuildableParcel(entity, parcelMin, parcelMax));
                if (!shouldStayHidden && RestoreMapTileVisibility(entity))
                {
                    shown++;
                }
            }

            return shown;
        }

        internal bool RestoreMapTileVisibility(Entity entity)
        {
            if (!_mEntityManager.Exists(entity) || !_mEntityManager.HasComponent<VanillaMapTileHiddenByParcelSetting>(entity))
            {
                return false;
            }

            _mEntityManager.RemoveComponent<VanillaMapTileHiddenByParcelSetting>(entity);
            if (_mEntityManager.HasComponent<Hidden>(entity))
            {
                _mEntityManager.RemoveComponent<Hidden>(entity);
                _mMarkUpdated(entity);
                return true;
            }

            _mMarkUpdated(entity);
            return false;
        }
    }
}
