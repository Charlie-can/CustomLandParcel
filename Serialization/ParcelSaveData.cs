using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using Unity.Mathematics;

namespace CustomLandParcel.Serialization
{
    internal static class ParcelSaveData
    {
        private const int SaveSchemaVersion = 2;

        public static void Write<TWriter>(ref TWriter writer, ParcelStore store)
            where TWriter : struct, IWriter
        {
            writer.Write(SaveSchemaVersion);
            writer.Write(store.Version);
            writer.Write(store.SelectedParcelId.ToString("N"));
            writer.Write(store.SelectedVertexIndex);
            writer.Write(store.Parcels.Count);
            for (var i = 0; i < store.Parcels.Count; i++)
            {
                var parcel = store.Parcels[i];
                writer.Write(parcel.Id.ToString("N"));
                writer.Write(parcel.Name ?? string.Empty);
                writer.Write((int)parcel.State);
                writer.Write(parcel.Price);
                writer.Write(parcel.Points.Count);
                for (var j = 0; j < parcel.Points.Count; j++)
                {
                    writer.Write(parcel.Points[j]);
                }
            }
        }

        public static void Read<TReader>(ref TReader reader, ParcelStore store, Action<string> warn)
            where TReader : struct, IReader
        {
            reader.Read(out int schemaVersion);
            if (schemaVersion >= 2)
            {
                ReadCurrentSchema(ref reader, store, warn, schemaVersion);
                return;
            }

            ReadLegacyRectangle(ref reader, store, schemaVersion);
        }

        private static void ReadCurrentSchema<TReader>(
            ref TReader reader,
            ParcelStore store,
            Action<string> warn,
            int schemaVersion)
            where TReader : struct, IReader
        {
            reader.Read(out uint savedVersion);
            reader.Read(out string selectedParcelIdText);
            reader.Read(out int selectedVertexIndex);
            reader.Read(out int parcelCount);

            var parcels = new List<LandParcel>(math.max(0, parcelCount));
            for (var i = 0; i < parcelCount; i++)
            {
                reader.Read(out string idText);
                reader.Read(out string name);
                reader.Read(out int state);
                reader.Read(out int price);
                reader.Read(out int pointCount);

                var points = new List<float2>(math.max(0, pointCount));
                for (var j = 0; j < pointCount; j++)
                {
                    reader.Read(out float2 point);
                    points.Add(point);
                }

                if (TryParseGuid(idText, out var id) && points.Count >= ParcelGeometry.MinimumVertexCount)
                {
                    var parcel = new LandParcel(id, name, points)
                    {
                        State = (LandParcelState)state,
                        Price = price
                    };
                    ParcelGeometry.RecalculatePrice(parcel);
                    parcels.Add(parcel);
                }
                else
                {
                    warn($"Skipped invalid serialized parcel: schema={schemaVersion}, id='{idText}', name='{name}', pointCount={pointCount}.");
                }
            }

            var selectedParcelId = TryParseGuid(selectedParcelIdText, out var selectedId) ? selectedId : Guid.Empty;
            store.ReplaceFromSave(parcels, selectedParcelId, selectedVertexIndex, savedVersion, $"deserialize schema={schemaVersion}");
        }

        private static void ReadLegacyRectangle<TReader>(ref TReader reader, ParcelStore store, int schemaVersion)
            where TReader : struct, IReader
        {
            reader.Read(out float2 min);
            reader.Read(out float2 max);
            var normalizedMin = math.min(min, max);
            var normalizedMax = math.max(min, max);
            var center = (normalizedMin + normalizedMax) * 0.5f;
            var size = math.max(normalizedMax - normalizedMin, ParcelGeometry.MinimumSize);
            var parcel = ParcelGeometry.CreateRectangle("Migrated Parcel", center, size);
            store.ReplaceFromSave(
                new[] { parcel },
                parcel.Id,
                0,
                store.Version + 1,
                $"legacy schema={schemaVersion} deserialize");
        }

        private static bool TryParseGuid(string text, out Guid id)
        {
            return Guid.TryParseExact(text, "N", out id) || Guid.TryParse(text, out id);
        }
    }
}
