using System;
using Colossal.Serialization.Entities;
using CustomLandParcel.Data;
using CustomLandParcel.Serialization;
using Game;
using Unity.Jobs;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// ECS lifecycle and save-game bridge for the authoritative parcel store.
    /// </summary>
    public partial class ParcelStoreSystem : GameSystemBase, IJobSerializable
    {
        private ParcelStore _store;

        internal ParcelStore Store => _store;

        internal System.Collections.Generic.IReadOnlyList<LandParcel> Parcels => _store.Parcels;

        internal uint Version => _store.Version;

        internal Guid SelectedParcelId => _store.SelectedParcelId;

        internal int SelectedVertexIndex => _store.SelectedVertexIndex;

        internal LandParcel SelectedParcel => _store.SelectedParcel;

        protected override void OnCreate()
        {
            base.OnCreate();
            _store = new ParcelStore(Mod.log.Info, Mod.log.Warn);
            _store.Initialize(1, "system create");
            Mod.log.Info($"ParcelStoreSystem enabled as ECS/save bridge. {_store.GetSummary()}.");
        }

        protected override void OnUpdate()
        {
        }

        internal LandParcel CreateRectangle(string name, float2 center, float2 size, string reason)
        {
            return _store.CreateRectangle(name, center, size, reason);
        }

        internal bool DeleteSelectedParcel(string reason)
        {
            return _store.DeleteSelectedParcel(reason);
        }

        internal bool SelectParcel(Guid id, string reason)
        {
            return _store.SelectParcel(id, reason);
        }

        internal bool SelectNextParcel(int direction, string reason)
        {
            return _store.SelectNextParcel(direction, reason);
        }

        internal bool RenameSelectedParcel(string name, string reason)
        {
            return _store.RenameSelectedParcel(name, reason);
        }

        internal bool PurchaseSelectedParcel(string reason)
        {
            return _store.PurchaseSelectedParcel(reason);
        }

        internal bool MoveSelectedParcel(float2 delta, string reason)
        {
            return _store.MoveSelectedParcel(delta, reason);
        }

        internal bool ResizeSelectedParcel(float amount, string reason)
        {
            return _store.ResizeSelectedParcel(amount, reason);
        }

        internal bool SelectVertex(int vertexIndex, string reason)
        {
            return _store.SelectVertex(vertexIndex, reason);
        }

        internal bool MoveSelectedVertex(float2 delta, string reason)
        {
            return _store.MoveSelectedVertex(delta, reason);
        }

        internal bool InsertVertexAfterSelected(string reason)
        {
            return _store.InsertVertexAfterSelected(reason);
        }

        internal bool DeleteSelectedVertex(string reason)
        {
            return _store.DeleteSelectedVertex(reason);
        }

        internal void ClearAllAndSeedDefault(string reason)
        {
            _store.ClearAllAndSeedDefault(reason);
        }

        internal bool IsBuildable(float2 position)
        {
            return _store.IsBuildable(position);
        }

        internal bool TryGetContainingPurchasedParcel(float2 position, out LandParcel parcel)
        {
            return _store.TryGetContainingPurchasedParcel(position, out parcel);
        }

        internal bool TryGetActiveUnionBounds(out float2 min, out float2 max)
        {
            return _store.TryGetActiveUnionBounds(out min, out max);
        }

        internal string GetSummary()
        {
            return _store.GetSummary();
        }

        public JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps)
            where TWriter : struct, IWriter
        {
            inputDeps.Complete();
            var writer = writerData.GetWriter<TWriter>();
            ParcelSaveData.Write(ref writer, _store);
            Mod.log.Info($"Serialized ParcelStoreSystem. {_store.GetSummary()}.");
            return default;
        }

        public JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
            where TReader : struct, IReader
        {
            inputDeps.Complete();
            var reader = readerData.GetReader<TReader>();
            ParcelSaveData.Read(ref reader, _store, Mod.log.Warn);
            Mod.log.Info($"Deserialized ParcelStoreSystem. {_store.GetSummary()}.");
            return default;
        }

        public JobHandle SetDefaults(Context context)
        {
            _store.SetDefaults($"set defaults purpose={context.purpose}");
            Mod.log.Info($"ParcelStoreSystem defaults applied: purpose={context.purpose}, {_store.GetSummary()}.");
            return default;
        }

        internal static string FormatGuid(Guid id)
        {
            return ParcelStore.FormatGuid(id);
        }
    }
}
