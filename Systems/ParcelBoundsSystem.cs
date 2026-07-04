using Colossal.Serialization.Entities;
using Game;
using Unity.Jobs;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Owns the editable parcel for the current save and serializes it with the save game.
    /// </summary>
    public partial class ParcelBoundsSystem : GameSystemBase, IJobSerializable
    {
        private const int SaveSchemaVersion = 1;

        private ParcelBounds _bounds;
        private uint _version;

        internal ParcelBounds Bounds => _bounds;

        internal uint Version => _version;

        protected override void OnCreate()
        {
            base.OnCreate();
            _bounds = ParcelBounds.Default;
            _version = 1;
            Mod.log.Info($"ParcelBoundsSystem enabled. Default parcel={_bounds}, version={_version}.");
        }

        protected override void OnUpdate()
        {
        }

        internal bool Contains(float2 point)
        {
            return _bounds.Contains(point);
        }

        internal void SetBounds(ParcelBounds bounds, string reason)
        {
            var normalized = bounds.Normalize();
            if (math.all(normalized.Min == _bounds.Min) && math.all(normalized.Max == _bounds.Max))
            {
                return;
            }

            var previous = _bounds;
            _bounds = normalized;
            _version++;
            Mod.log.Info(
                $"Parcel bounds changed ({reason}): {previous} -> {_bounds}, version={_version}.");
        }

        internal void Move(float2 delta, string reason)
        {
            SetBounds(_bounds.Move(delta), reason);
        }

        internal void Resize(float amount, string reason)
        {
            SetBounds(_bounds.Resize(amount), reason);
        }

        public JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps)
            where TWriter : struct, IWriter
        {
            inputDeps.Complete();
            var writer = writerData.GetWriter<TWriter>();
            writer.Write(SaveSchemaVersion);
            writer.Write(_bounds.Min);
            writer.Write(_bounds.Max);
            Mod.log.Info(
                $"Serialized parcel bounds: schema={SaveSchemaVersion}, parcel={_bounds}, version={_version}.");
            return default;
        }

        public JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
            where TReader : struct, IReader
        {
            inputDeps.Complete();
            var reader = readerData.GetReader<TReader>();
            reader.Read(out int schemaVersion);
            reader.Read(out float2 min);
            reader.Read(out float2 max);

            _bounds = new ParcelBounds(min, max).Normalize();
            _version++;
            Mod.log.Info(
                $"Deserialized parcel bounds: schema={schemaVersion}, parcel={_bounds}, version={_version}.");
            return default;
        }

        public JobHandle SetDefaults(Context context)
        {
            _bounds = ParcelBounds.Default;
            _version++;
            Mod.log.Info(
                $"Parcel bounds defaults applied for context purpose={context.purpose}: parcel={_bounds}, version={_version}.");
            return default;
        }
    }
}