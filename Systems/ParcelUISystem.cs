using System;
using Colossal.UI.Binding;
using CustomLandParcel.Geometry;
using CustomLandParcel.UI;
using Game;
using Game.SceneFlow;
using Game.UI;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Backend bindings for an in-game parcel management panel.
    /// </summary>
    public partial class ParcelUISystem : UISystemBase
    {
        private const string Group = "customLandParcel";
        private ParcelStoreSystem _mParcelStoreSystem;
        private RawValueBinding _mParcelsBinding;
        private uint _mLastLoggedVersion;

        public override GameMode gameMode => GameMode.Game;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();

            AddUpdateBinding(_mParcelsBinding = new RawValueBinding(Group, "parcels", BindParcels));
            AddUpdateBinding(new GetterValueBinding<string>(
                Group,
                "selectedParcelId",
                () => ParcelStoreSystem.FormatGuid(_mParcelStoreSystem.SelectedParcelId)));
            AddUpdateBinding(new GetterValueBinding<int>(
                Group,
                "selectedVertexIndex",
                () => _mParcelStoreSystem.SelectedVertexIndex));
            AddUpdateBinding(new GetterValueBinding<uint>(
                Group,
                "version",
                () => _mParcelStoreSystem.Version));
            AddUpdateBinding(new GetterValueBinding<string>(
                Group,
                "summary",
                () => _mParcelStoreSystem.GetSummary()));

            AddBinding(new TriggerBinding(Group, "addRectangle", AddRectangle));
            AddBinding(new TriggerBinding<string>(Group, "selectParcel", SelectParcel));
            AddBinding(new TriggerBinding<int>(Group, "selectVertex", SelectVertex));
            AddBinding(new TriggerBinding<int>(Group, "selectNextParcel", SelectNextParcel));
            AddBinding(new TriggerBinding<string>(Group, "renameSelectedParcel", RenameSelectedParcel));
            AddBinding(new TriggerBinding(Group, "deleteSelectedParcel", DeleteSelectedParcel));
            AddBinding(new TriggerBinding(Group, "purchaseSelectedParcel", PurchaseSelectedParcel));
            AddBinding(new TriggerBinding<float2>(Group, "moveSelectedParcel", MoveSelectedParcel));
            AddBinding(new TriggerBinding<float2>(Group, "moveSelectedVertex", MoveSelectedVertex));
            AddBinding(new TriggerBinding(Group, "insertVertexAfterSelected", InsertVertexAfterSelected));
            AddBinding(new TriggerBinding(Group, "deleteSelectedVertex", DeleteSelectedVertex));
            AddBinding(new TriggerBinding(Group, "clearAndSeedDefault", ClearAndSeedDefault));

            Mod.log.Info($"ParcelUISystem enabled. Binding group='{Group}', {_mParcelStoreSystem.GetSummary()}.");
        }

        [Preserve]
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (_mLastLoggedVersion != _mParcelStoreSystem.Version)
            {
                _mLastLoggedVersion = _mParcelStoreSystem.Version;
                Mod.log.Info($"ParcelUISystem observed store change: {_mParcelStoreSystem.GetSummary()}.");
            }
        }

        private void BindParcels(IJsonWriter writer)
        {
            ParcelUIWriter.WriteParcels(writer, _mParcelStoreSystem.Store);
        }

        private void AddRectangle()
        {
            var offset = _mParcelStoreSystem.Parcels.Count * 1250f;
            var parcel = _mParcelStoreSystem.CreateRectangle(
                $"Parcel {_mParcelStoreSystem.Parcels.Count + 1}",
                new float2(offset, 0f),
                new float2(1000f, 1000f),
                "ui addRectangle");
            LogTrigger($"addRectangle created={ParcelStoreSystem.FormatGuid(parcel.Id)}");
        }

        private void SelectParcel(string idText)
        {
            if (!TryParseGuid(idText, out var id))
            {
                Mod.log.Warn($"Parcel UI trigger selectParcel ignored: invalid id='{idText}'.");
                return;
            }

            _mParcelStoreSystem.SelectParcel(id, "ui selectParcel");
            LogTrigger($"selectParcel id={idText}");
        }

        private void SelectVertex(int index)
        {
            _mParcelStoreSystem.SelectVertex(index, "ui selectVertex");
            LogTrigger($"selectVertex index={index}");
        }

        private void SelectNextParcel(int direction)
        {
            _mParcelStoreSystem.SelectNextParcel(direction == 0 ? 1 : math.sign(direction), "ui selectNextParcel");
            LogTrigger($"selectNextParcel direction={direction}");
        }

        private void RenameSelectedParcel(string name)
        {
            _mParcelStoreSystem.RenameSelectedParcel(name, "ui renameSelectedParcel");
            LogTrigger($"renameSelectedParcel name='{name}'");
        }

        private void DeleteSelectedParcel()
        {
            _mParcelStoreSystem.DeleteSelectedParcel("ui deleteSelectedParcel");
            LogTrigger("deleteSelectedParcel");
        }

        private void PurchaseSelectedParcel()
        {
            _mParcelStoreSystem.PurchaseSelectedParcel("ui purchaseSelectedParcel");
            LogTrigger("purchaseSelectedParcel");
        }

        private void MoveSelectedParcel(float2 delta)
        {
            _mParcelStoreSystem.MoveSelectedParcel(delta, "ui moveSelectedParcel");
            LogTrigger($"moveSelectedParcel delta={ParcelGeometry.Format(delta)}");
        }

        private void MoveSelectedVertex(float2 delta)
        {
            _mParcelStoreSystem.MoveSelectedVertex(delta, "ui moveSelectedVertex");
            LogTrigger($"moveSelectedVertex delta={ParcelGeometry.Format(delta)}");
        }

        private void InsertVertexAfterSelected()
        {
            _mParcelStoreSystem.InsertVertexAfterSelected("ui insertVertexAfterSelected");
            LogTrigger("insertVertexAfterSelected");
        }

        private void DeleteSelectedVertex()
        {
            _mParcelStoreSystem.DeleteSelectedVertex("ui deleteSelectedVertex");
            LogTrigger("deleteSelectedVertex");
        }

        private void ClearAndSeedDefault()
        {
            _mParcelStoreSystem.ClearAllAndSeedDefault("ui clearAndSeedDefault");
            LogTrigger("clearAndSeedDefault");
        }

        private void LogTrigger(string message)
        {
            _mParcelsBinding.Update();
            Mod.log.Info($"Parcel UI trigger handled: {message}; {_mParcelStoreSystem.GetSummary()}.");
        }

        private static bool TryParseGuid(string text, out Guid id)
        {
            return Guid.TryParseExact(text, "N", out id) || Guid.TryParse(text, out id);
        }
    }
}
