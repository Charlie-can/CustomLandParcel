using System;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using CustomLandParcel.Geometry;
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
        private ParcelStoreSystem _parcelStoreSystem;
        private RawValueBinding _parcelsBinding;
        private uint _lastLoggedVersion;

        public override GameMode gameMode => GameMode.Game;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            _parcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();

            AddUpdateBinding(_parcelsBinding = new RawValueBinding(Group, "parcels", BindParcels));
            AddUpdateBinding(new GetterValueBinding<string>(
                Group,
                "selectedParcelId",
                () => ParcelStoreSystem.FormatGuid(_parcelStoreSystem.SelectedParcelId)));
            AddUpdateBinding(new GetterValueBinding<int>(
                Group,
                "selectedVertexIndex",
                () => _parcelStoreSystem.SelectedVertexIndex));
            AddUpdateBinding(new GetterValueBinding<uint>(
                Group,
                "version",
                () => _parcelStoreSystem.Version));
            AddUpdateBinding(new GetterValueBinding<string>(
                Group,
                "summary",
                () => _parcelStoreSystem.GetSummary()));

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

            Mod.log.Info($"ParcelUISystem enabled. Binding group='{Group}', {_parcelStoreSystem.GetSummary()}.");
        }

        [Preserve]
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (_lastLoggedVersion != _parcelStoreSystem.Version)
            {
                _lastLoggedVersion = _parcelStoreSystem.Version;
                Mod.log.Info($"ParcelUISystem observed store change: {_parcelStoreSystem.GetSummary()}.");
            }
        }

        private void BindParcels(IJsonWriter writer)
        {
            writer.ArrayBegin((uint)_parcelStoreSystem.Parcels.Count);
            for (var i = 0; i < _parcelStoreSystem.Parcels.Count; i++)
            {
                var parcel = _parcelStoreSystem.Parcels[i];
                PolygonMath.TryGetBounds(parcel.Points, out var min, out var max);
                writer.TypeBegin("customLandParcel.Parcel");
                writer.PropertyName("id");
                writer.Write(parcel.Id.ToString("N"));
                writer.PropertyName("name");
                writer.Write(parcel.Name);
                writer.PropertyName("state");
                writer.Write(parcel.State.ToString());
                writer.PropertyName("price");
                writer.Write(parcel.Price);
                writer.PropertyName("area");
                writer.Write(PolygonMath.Area(parcel.Points));
                writer.PropertyName("selected");
                writer.Write(parcel.Id == _parcelStoreSystem.SelectedParcelId);
                writer.PropertyName("min");
                writer.Write(min);
                writer.PropertyName("max");
                writer.Write(max);
                writer.PropertyName("points");
                writer.ArrayBegin((uint)parcel.Points.Count);
                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    writer.Write(parcel.Points[pointIndex]);
                }

                writer.ArrayEnd();
                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }

        private void AddRectangle()
        {
            var offset = _parcelStoreSystem.Parcels.Count * 1250f;
            var parcel = _parcelStoreSystem.CreateRectangle(
                $"Parcel {_parcelStoreSystem.Parcels.Count + 1}",
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

            _parcelStoreSystem.SelectParcel(id, "ui selectParcel");
            LogTrigger($"selectParcel id={idText}");
        }

        private void SelectVertex(int index)
        {
            _parcelStoreSystem.SelectVertex(index, "ui selectVertex");
            LogTrigger($"selectVertex index={index}");
        }

        private void SelectNextParcel(int direction)
        {
            _parcelStoreSystem.SelectNextParcel(direction == 0 ? 1 : math.sign(direction), "ui selectNextParcel");
            LogTrigger($"selectNextParcel direction={direction}");
        }

        private void RenameSelectedParcel(string name)
        {
            _parcelStoreSystem.RenameSelectedParcel(name, "ui renameSelectedParcel");
            LogTrigger($"renameSelectedParcel name='{name}'");
        }

        private void DeleteSelectedParcel()
        {
            _parcelStoreSystem.DeleteSelectedParcel("ui deleteSelectedParcel");
            LogTrigger("deleteSelectedParcel");
        }

        private void PurchaseSelectedParcel()
        {
            _parcelStoreSystem.PurchaseSelectedParcel("ui purchaseSelectedParcel");
            LogTrigger("purchaseSelectedParcel");
        }

        private void MoveSelectedParcel(float2 delta)
        {
            _parcelStoreSystem.MoveSelectedParcel(delta, "ui moveSelectedParcel");
            LogTrigger($"moveSelectedParcel delta={ParcelBounds.Format(delta)}");
        }

        private void MoveSelectedVertex(float2 delta)
        {
            _parcelStoreSystem.MoveSelectedVertex(delta, "ui moveSelectedVertex");
            LogTrigger($"moveSelectedVertex delta={ParcelBounds.Format(delta)}");
        }

        private void InsertVertexAfterSelected()
        {
            _parcelStoreSystem.InsertVertexAfterSelected("ui insertVertexAfterSelected");
            LogTrigger("insertVertexAfterSelected");
        }

        private void DeleteSelectedVertex()
        {
            _parcelStoreSystem.DeleteSelectedVertex("ui deleteSelectedVertex");
            LogTrigger("deleteSelectedVertex");
        }

        private void ClearAndSeedDefault()
        {
            _parcelStoreSystem.ClearAllAndSeedDefault("ui clearAndSeedDefault");
            LogTrigger("clearAndSeedDefault");
        }

        private void LogTrigger(string message)
        {
            _parcelsBinding.Update();
            Mod.log.Info($"Parcel UI trigger handled: {message}; {_parcelStoreSystem.GetSummary()}.");
        }

        private static bool TryParseGuid(string text, out Guid id)
        {
            return Guid.TryParseExact(text, "N", out id) || Guid.TryParse(text, out id);
        }
    }
}
