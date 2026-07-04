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
            foreach (var parcel in store.Parcels)
            {
                writer.Write(parcel.Id.ToString("N"));
                writer.Write(parcel.Name ?? string.Empty);
                writer.Write((int)parcel.State);
                writer.Write(parcel.Price);
                writer.Write(parcel.Points.Count);
                foreach (var t in parcel.Points)
                {
                    writer.Write(t);
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

            DiscardUnsupportedSchemaPayload(ref reader, schemaVersion);
            warn($"Unsupported parcel save schema={schemaVersion}; resetting custom parcels to current defaults.");
            store.ReplaceFromSave(Array.Empty<LandParcel>(), Guid.Empty, -1, store.Version + 1,
                $"unsupported schema={schemaVersion} deserialize");
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

        private static void DiscardUnsupportedSchemaPayload<TReader>(ref TReader reader, int schemaVersion)
            where TReader : struct, IReader
        {
            if (schemaVersion != 1)
            {
                return;
            }

            reader.Read(out float2 _);
            reader.Read(out float2 _);
        }

        private static bool TryParseGuid(string text, out Guid id)
        {
            return Guid.TryParseExact(text, "N", out id) || Guid.TryParse(text, out id);
        }
    }
}
