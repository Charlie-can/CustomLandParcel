using CustomLandParcel.Data;
using Game;
using Game.City;
using Game.Simulation;
using Unity.Entities;
using UnityEngine.Scripting;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Authoritative game-money purchase flow for custom parcels.
    /// </summary>
    public partial class ParcelPurchaseSystem : GameSystemBase
    {
        private CitySystem _mCitySystem;
        private ParcelStoreSystem _mParcelStoreSystem;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            _mCitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            Mod.log.Info("ParcelPurchaseSystem enabled. Purchases use CitySystem.City PlayerMoney.Subtract and ParcelStoreSystem state changes.");
        }

        protected override void OnUpdate()
        {
        }

        internal bool PurchaseSelectedParcel(string reason)
        {
            var selected = _mParcelStoreSystem.SelectedParcel;
            if (selected == null)
            {
                Mod.log.Warn($"Parcel purchase rejected ({reason}): no selected parcel.");
                return false;
            }

            if (selected.State == LandParcelState.Purchased)
            {
                Mod.log.Info(
                    $"Parcel purchase ignored ({reason}): parcel={ParcelStoreSystem.FormatGuid(selected.Id)} is already Purchased.");
                return true;
            }

            if (_mCitySystem.City == Entity.Null || !EntityManager.HasComponent<PlayerMoney>(_mCitySystem.City))
            {
                Mod.log.Warn(
                    $"Parcel purchase rejected ({reason}): city entity or PlayerMoney component is missing, city={_mCitySystem.City}.");
                return false;
            }

            var money = EntityManager.GetComponentData<PlayerMoney>(_mCitySystem.City);
            var price = selected.Price;
            var before = money.money;
            if (price <= 0)
            {
                Mod.log.Warn(
                    $"Parcel purchase rejected ({reason}): non-positive price={price}, parcel={ParcelStoreSystem.FormatGuid(selected.Id)}.");
                return false;
            }

            if (before < price)
            {
                Mod.log.Warn(
                    $"Parcel purchase rejected ({reason}): insufficient funds, parcel={ParcelStoreSystem.FormatGuid(selected.Id)}, money={before}, price={price}.");
                return false;
            }

            money.Subtract(price);
            EntityManager.SetComponentData(_mCitySystem.City, money);
            _mParcelStoreSystem.SetSelectedParcelState(LandParcelState.Purchased, $"{reason}: paid price={price}");
            var after = EntityManager.GetComponentData<PlayerMoney>(_mCitySystem.City).money;
            Mod.log.Info(
                $"Parcel purchase completed ({reason}): parcel={ParcelStoreSystem.FormatGuid(selected.Id)}, moneyBefore={before}, price={price}, moneyAfter={after}.");
            return true;
        }
    }
}
