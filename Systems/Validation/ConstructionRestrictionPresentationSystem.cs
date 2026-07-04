using Game;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Keeps parcel restriction error markers visible at render sampling time.
    /// </summary>
    public partial class ConstructionRestrictionPresentationSystem : GameSystemBase
    {
        private EntityQuery _mRestrictionQuery;
        private int _mLastRepairCount = -1;
        private int _mFramesSinceLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mRestrictionQuery = GetEntityQuery(
                ComponentType.ReadOnly<ParcelRestrictionError>(),
                ComponentType.ReadOnly<Temp>(),
                ComponentType.Exclude<Deleted>());
            Mod.log.Info("ConstructionRestrictionPresentationSystem enabled at PreCulling to stabilize parcel error rendering.");
        }

        protected override void OnUpdate()
        {
            var entities = _mRestrictionQuery.ToEntityArray(Allocator.Temp);
            try
            {
                var repairCount = 0;
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    if (EntityManager.HasComponent<Error>(entity))
                    {
                        continue;
                    }

                    EntityManager.AddComponent<Error>(entity);
                    repairCount++;
                }

                LogRepairs(repairCount, entities.Length);
            }
            finally
            {
                entities.Dispose();
            }
        }

        private void LogRepairs(int repairCount, int restrictedCount)
        {
            _mFramesSinceLog++;
            if (repairCount == _mLastRepairCount && (repairCount == 0 || _mFramesSinceLog < 60))
            {
                return;
            }

            _mLastRepairCount = repairCount;
            _mFramesSinceLog = 0;
            if (repairCount > 0)
            {
                Mod.log.Info(
                    $"Parcel presentation sync restored {repairCount} missing Error marker(s) before rendering; restrictedPreviewCount={restrictedCount}.");
            }
        }
    }
}
