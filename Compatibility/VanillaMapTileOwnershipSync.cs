using System;
using Game.Areas;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace CustomLandParcel.Compatibility
{
    internal enum VanillaMapTileOwnershipResult
    {
        NoChange,
        AlreadyUnlockedByParcel,
        AlreadyVanillaOwned,
        UnlockedByParcel
    }

    internal sealed class VanillaMapTileOwnershipSync
    {
        private readonly EntityManager _mEntityManager;
        private readonly Action<Entity> _mMarkUpdated;
        private readonly Func<Entity, bool> _mRestoreVisibility;

        internal VanillaMapTileOwnershipSync(
            EntityManager entityManager,
            Action<Entity> markUpdated,
            Func<Entity, bool> restoreVisibility)
        {
            _mEntityManager = entityManager;
            _mMarkUpdated = markUpdated;
            _mRestoreVisibility = restoreVisibility;
        }

        internal VanillaMapTileOwnershipResult ApplyBuildableOwnership(Entity entity)
        {
            if (_mEntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity))
            {
                RemoveLockedMarker(entity);
                return VanillaMapTileOwnershipResult.AlreadyUnlockedByParcel;
            }

            if (!_mEntityManager.HasComponent<Native>(entity))
            {
                RemoveLockedMarker(entity);
                return VanillaMapTileOwnershipResult.AlreadyVanillaOwned;
            }

            _mEntityManager.RemoveComponent<Native>(entity);
            RemoveLockedMarker(entity);
            _mEntityManager.AddComponentData(entity, default(VanillaMapTileUnlockedByParcel));
            _mMarkUpdated(entity);
            return VanillaMapTileOwnershipResult.UnlockedByParcel;
        }

        internal bool LockMapTileOutsideCustomParcel(Entity entity)
        {
            if (!_mEntityManager.Exists(entity) ||
                !_mEntityManager.HasComponent<MapTile>(entity) ||
                _mEntityManager.HasComponent<VanillaMapTileBlocker>(entity))
            {
                return false;
            }

            var wasUnlockedByParcel = _mEntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity);
            if (wasUnlockedByParcel)
            {
                _mEntityManager.RemoveComponent<VanillaMapTileUnlockedByParcel>(entity);
            }

            if (!_mEntityManager.HasComponent<Native>(entity))
            {
                _mEntityManager.AddComponentData(entity, default(Native));
                if (!wasUnlockedByParcel && !_mEntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
                {
                    _mEntityManager.AddComponentData(entity, default(VanillaMapTileLockedByParcel));
                }

                _mRestoreVisibility(entity);
                _mMarkUpdated(entity);
                return true;
            }

            if (wasUnlockedByParcel)
            {
                _mRestoreVisibility(entity);
                _mMarkUpdated(entity);
            }

            return false;
        }

        internal int RestoreUnlockedMapTiles(EntityQuery unlockedByParcelQuery, string reason)
        {
            using var marked = unlockedByParcelQuery.ToEntityArray(Allocator.Temp);
            var restored = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                if (RestoreUnlockedMapTile(marked[i]))
                {
                    restored++;
                }
            }

            if (restored > 0)
            {
                Mod.log.Info($"Parcel map tile unlock mask restored {restored} vanilla map tile(s) ({reason}).");
            }

            return restored;
        }

        internal int RestoreLockedMapTiles(EntityQuery lockedByParcelQuery, string reason)
        {
            using var marked = lockedByParcelQuery.ToEntityArray(Allocator.Temp);
            var unlocked = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                var entity = marked[i];
                if (!_mEntityManager.Exists(entity) || !_mEntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
                {
                    continue;
                }

                _mEntityManager.RemoveComponent<VanillaMapTileLockedByParcel>(entity);
                if (_mEntityManager.HasComponent<Native>(entity))
                {
                    _mEntityManager.RemoveComponent<Native>(entity);
                    _mMarkUpdated(entity);
                    unlocked++;
                }
            }

            if (unlocked > 0)
            {
                Mod.log.Info($"Restored {unlocked} vanilla map tile(s) locked by parcel ownership sync ({reason}).");
            }

            return unlocked;
        }

        private void RemoveLockedMarker(Entity entity)
        {
            if (!_mEntityManager.HasComponent<VanillaMapTileLockedByParcel>(entity))
            {
                return;
            }

            _mEntityManager.RemoveComponent<VanillaMapTileLockedByParcel>(entity);
            _mMarkUpdated(entity);
        }

        private bool RestoreUnlockedMapTile(Entity entity)
        {
            if (!_mEntityManager.Exists(entity) || !_mEntityManager.HasComponent<VanillaMapTileUnlockedByParcel>(entity))
            {
                return false;
            }

            _mEntityManager.RemoveComponent<VanillaMapTileUnlockedByParcel>(entity);
            _mRestoreVisibility(entity);
            if (!_mEntityManager.HasComponent<Native>(entity))
            {
                _mEntityManager.AddComponentData(entity, default(Native));
            }

            _mMarkUpdated(entity);
            return true;
        }
    }
}
