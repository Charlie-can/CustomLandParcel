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
        internal ParcelStore Store { get; private set; }

        internal System.Collections.Generic.IReadOnlyList<LandParcel> Parcels => Store.Parcels;

        internal uint Version => Store.Version;

        internal Guid SelectedParcelId => Store.SelectedParcelId;

        internal int SelectedVertexIndex => Store.SelectedVertexIndex;

        internal LandParcel SelectedParcel => Store.SelectedParcel;

        protected override void OnCreate()
        {
            base.OnCreate();
            Store = new ParcelStore(Mod.log.Info, Mod.log.Warn);
            Store.Initialize(1, "system create");
            Mod.log.Info($"ParcelStoreSystem enabled as ECS/save bridge. {Store.GetSummary()}.");
        }

        protected override void OnUpdate()
        {
        }

        internal LandParcel CreateRectangle(string name, float2 center, float2 size, string reason)
        {
            return Store.CreateRectangle(name, center, size, reason);
        }

        internal LandParcel CreatePolygon(
            string name,
            System.Collections.Generic.IEnumerable<float2> points,
            LandParcelState state,
            string reason)
        {
            return Store.CreatePolygon(name, points, state, reason);
        }

        internal bool DeleteSelectedParcel(string reason)
        {
            return Store.DeleteSelectedParcel(reason);
        }

        internal bool MergeSelectedParcelWith(Guid targetId, string reason)
        {
            return Store.MergeSelectedParcelWith(targetId, reason);
        }

        internal bool SelectParcel(Guid id, string reason)
        {
            return Store.SelectParcel(id, reason);
        }

        internal bool SelectNextParcel(int direction, string reason)
        {
            return Store.SelectNextParcel(direction, reason);
        }

        internal bool RenameSelectedParcel(string name, string reason)
        {
            return Store.RenameSelectedParcel(name, reason);
        }

        internal bool PurchaseSelectedParcel(string reason)
        {
            return Store.PurchaseSelectedParcel(reason);
        }

        internal bool SetSelectedParcelState(LandParcelState state, string reason)
        {
            return Store.SetSelectedParcelState(state, reason);
        }

        internal bool MoveSelectedParcel(float2 delta, string reason)
        {
            return Store.MoveSelectedParcel(delta, reason);
        }

        internal bool MoveParcel(Guid parcelId, float2 delta, string reason)
        {
            return Store.MoveParcel(parcelId, delta, reason);
        }

        internal bool MoveParcelTransient(Guid parcelId, float2 delta, string reason)
        {
            return Store.MoveParcelTransient(parcelId, delta, reason);
        }

        internal bool ResizeSelectedParcel(float amount, string reason)
        {
            return Store.ResizeSelectedParcel(amount, reason);
        }

        internal bool SelectVertex(int vertexIndex, string reason)
        {
            return Store.SelectVertex(vertexIndex, reason);
        }

        internal bool MoveSelectedVertex(float2 delta, string reason)
        {
            return Store.MoveSelectedVertex(delta, reason);
        }

        internal bool SetVertexPosition(Guid parcelId, int vertexIndex, float2 position, string reason)
        {
            return Store.SetVertexPosition(parcelId, vertexIndex, position, reason);
        }

        internal bool SetVertexPositionTransient(Guid parcelId, int vertexIndex, float2 position, string reason)
        {
            return Store.SetVertexPositionTransient(parcelId, vertexIndex, position, reason);
        }

        internal bool CommitParcelGeometry(Guid parcelId, string reason)
        {
            return Store.CommitParcelGeometry(parcelId, reason);
        }

        internal bool InsertVertexAfterSelected(string reason)
        {
            return Store.InsertVertexAfterSelected(reason);
        }

        internal bool InsertVertexOnEdge(Guid parcelId, int edgeIndex, string reason)
        {
            return Store.InsertVertexOnEdge(parcelId, edgeIndex, reason);
        }

        internal bool DeleteSelectedVertex(string reason)
        {
            return Store.DeleteSelectedVertex(reason);
        }

        internal void ClearAllAndSeedDefault(string reason)
        {
            Store.ClearAllAndSeedDefault(reason);
        }

        internal bool IsBuildable(float2 position)
        {
            return Store.IsBuildable(position);
        }

        internal bool TryGetContainingPurchasedParcel(float2 position, out LandParcel parcel)
        {
            return Store.TryGetContainingPurchasedParcel(position, out parcel);
        }

        internal bool TryGetActiveUnionBounds(out float2 min, out float2 max)
        {
            return Store.TryGetActiveUnionBounds(out min, out max);
        }

        internal string GetSummary()
        {
            return Store.GetSummary();
        }

        public JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps)
            where TWriter : struct, IWriter
        {
            inputDeps.Complete();
            var writer = writerData.GetWriter<TWriter>();
            ParcelSaveData.Write(ref writer, Store);
            Mod.log.Info($"Serialized ParcelStoreSystem. {Store.GetSummary()}.");
            return default;
        }

        public JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
            where TReader : struct, IReader
        {
            inputDeps.Complete();
            var reader = readerData.GetReader<TReader>();
            ParcelSaveData.Read(ref reader, Store, Mod.log.Warn);
            Mod.log.Info($"Deserialized ParcelStoreSystem. {Store.GetSummary()}.");
            return default;
        }

        public JobHandle SetDefaults(Context context)
        {
            Store.SetDefaults($"set defaults purpose={context.purpose}");
            Mod.log.Info($"ParcelStoreSystem defaults applied: purpose={context.purpose}, {Store.GetSummary()}.");
            return default;
        }

        internal static string FormatGuid(Guid id)
        {
            return ParcelStore.FormatGuid(id);
        }
    }
}
