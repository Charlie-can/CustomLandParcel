using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Serialization.Entities;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using Game;
using Unity.Jobs;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Authoritative save-backed store for all custom land parcels in the current city.
    /// </summary>
    public partial class ParcelStoreSystem : GameSystemBase, IJobSerializable
    {
        private const int SaveSchemaVersion = 2;
        private const int MinimumVertexCount = 3;
        private readonly List<LandParcel> _parcels = new List<LandParcel>();
        private Guid _selectedParcelId;
        private int _selectedVertexIndex = -1;
        private uint _version;

        internal IReadOnlyList<LandParcel> Parcels => _parcels;

        internal uint Version => _version;

        internal Guid SelectedParcelId => _selectedParcelId;

        internal int SelectedVertexIndex => _selectedVertexIndex;

        internal LandParcel SelectedParcel => FindParcel(_selectedParcelId);

        protected override void OnCreate()
        {
            base.OnCreate();
            _version = 1;
            EnsureDefaultParcel("system create");
            Mod.log.Info(
                $"ParcelStoreSystem enabled. parcels={_parcels.Count}, selected={FormatGuid(_selectedParcelId)}, version={_version}.");
        }

        protected override void OnUpdate()
        {
        }

        internal LandParcel CreateRectangle(string name, float2 center, float2 size, string reason)
        {
            var half = math.max(size * 0.5f, new float2(50f, 50f));
            var points = new[]
            {
                center + new float2(-half.x, -half.y),
                center + new float2(-half.x, half.y),
                center + new float2(half.x, half.y),
                center + new float2(half.x, -half.y)
            };

            var parcel = new LandParcel(Guid.NewGuid(), name, points);
            RecalculatePrice(parcel);
            _parcels.Add(parcel);
            SelectParcel(parcel.Id, $"{reason}: auto-select created rectangle");
            MarkChanged($"{reason}: created {parcel}");
            return parcel;
        }

        internal bool DeleteSelectedParcel(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                Mod.log.Warn($"Parcel delete ignored ({reason}): no selected parcel.");
                return false;
            }

            _parcels.Remove(selected);
            _selectedParcelId = _parcels.Count == 0 ? Guid.Empty : _parcels[0].Id;
            _selectedVertexIndex = ClampVertexIndex(SelectedParcel, _selectedVertexIndex);
            MarkChanged($"{reason}: deleted {selected}, nextSelected={FormatGuid(_selectedParcelId)}");
            return true;
        }

        internal bool SelectParcel(Guid id, string reason)
        {
            if (id == Guid.Empty || FindParcel(id) == null)
            {
                Mod.log.Warn($"Parcel selection ignored ({reason}): id={FormatGuid(id)} was not found.");
                return false;
            }

            _selectedParcelId = id;
            _selectedVertexIndex = ClampVertexIndex(SelectedParcel, _selectedVertexIndex);
            MarkChanged($"{reason}: selected parcel={FormatGuid(_selectedParcelId)}, vertex={_selectedVertexIndex}");
            return true;
        }

        internal bool SelectNextParcel(int direction, string reason)
        {
            if (_parcels.Count == 0)
            {
                Mod.log.Warn($"Parcel select next ignored ({reason}): no parcels.");
                return false;
            }

            var current = _parcels.FindIndex(parcel => parcel.Id == _selectedParcelId);
            if (current < 0)
            {
                current = 0;
            }
            else
            {
                current = (current + direction + _parcels.Count) % _parcels.Count;
            }

            return SelectParcel(_parcels[current].Id, reason);
        }

        internal bool RenameSelectedParcel(string name, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                Mod.log.Warn($"Parcel rename ignored ({reason}): no selected parcel.");
                return false;
            }

            var previous = selected.Name;
            selected.Name = string.IsNullOrWhiteSpace(name) ? selected.Name : name.Trim();
            MarkChanged($"{reason}: renamed parcel {FormatGuid(selected.Id)} from '{previous}' to '{selected.Name}'");
            return true;
        }

        internal bool PurchaseSelectedParcel(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                Mod.log.Warn($"Parcel purchase ignored ({reason}): no selected parcel.");
                return false;
            }

            selected.State = LandParcelState.Purchased;
            MarkChanged($"{reason}: purchased {selected}");
            return true;
        }

        internal bool MoveSelectedParcel(float2 delta, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                Mod.log.Warn($"Parcel move ignored ({reason}): no selected parcel.");
                return false;
            }

            for (var i = 0; i < selected.Points.Count; i++)
            {
                selected.Points[i] += delta;
            }

            MarkChanged($"{reason}: moved {selected} by {ParcelBounds.Format(delta)}");
            return true;
        }

        internal bool ResizeSelectedParcel(float amount, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                Mod.log.Warn($"Parcel resize ignored ({reason}): no selected parcel.");
                return false;
            }

            var centroid = PolygonMath.Centroid(selected.Points);
            for (var i = 0; i < selected.Points.Count; i++)
            {
                var direction = selected.Points[i] - centroid;
                if (math.lengthsq(direction) <= 0.0001f)
                {
                    direction = new float2(1f, 0f);
                }

                selected.Points[i] += math.normalize(direction) * amount;
            }

            RecalculatePrice(selected);
            MarkChanged($"{reason}: resized {selected} by {amount:F1}");
            return true;
        }

        internal bool SelectVertex(int vertexIndex, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                Mod.log.Warn($"Vertex selection ignored ({reason}): no selected parcel.");
                return false;
            }

            if (vertexIndex < 0 || vertexIndex >= selected.Points.Count)
            {
                Mod.log.Warn(
                    $"Vertex selection ignored ({reason}): index={vertexIndex}, vertexCount={selected.Points.Count}.");
                return false;
            }

            _selectedVertexIndex = vertexIndex;
            MarkChanged($"{reason}: selected vertex={_selectedVertexIndex} parcel={FormatGuid(selected.Id)}");
            return true;
        }

        internal bool MoveSelectedVertex(float2 delta, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || _selectedVertexIndex < 0 || _selectedVertexIndex >= selected.Points.Count)
            {
                Mod.log.Warn(
                    $"Vertex move ignored ({reason}): selectedParcel={FormatGuid(_selectedParcelId)}, selectedVertex={_selectedVertexIndex}.");
                return false;
            }

            selected.Points[_selectedVertexIndex] += delta;
            RecalculatePrice(selected);
            MarkChanged(
                $"{reason}: moved vertex={_selectedVertexIndex} by {ParcelBounds.Format(delta)}, parcel={selected}");
            return true;
        }

        internal bool InsertVertexAfterSelected(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || selected.Points.Count < MinimumVertexCount)
            {
                Mod.log.Warn($"Vertex insert ignored ({reason}): selected parcel is invalid.");
                return false;
            }

            var index = _selectedVertexIndex >= 0 ? _selectedVertexIndex : selected.Points.Count - 1;
            var nextIndex = (index + 1) % selected.Points.Count;
            var inserted = (selected.Points[index] + selected.Points[nextIndex]) * 0.5f;
            selected.Points.Insert(index + 1, inserted);
            _selectedVertexIndex = index + 1;
            RecalculatePrice(selected);
            MarkChanged(
                $"{reason}: inserted vertex={_selectedVertexIndex} at {ParcelBounds.Format(inserted)}, parcel={selected}");
            return true;
        }

        internal bool DeleteSelectedVertex(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || _selectedVertexIndex < 0 || _selectedVertexIndex >= selected.Points.Count)
            {
                Mod.log.Warn($"Vertex delete ignored ({reason}): no selected vertex.");
                return false;
            }

            if (selected.Points.Count <= MinimumVertexCount)
            {
                Mod.log.Warn(
                    $"Vertex delete ignored ({reason}): parcel {FormatGuid(selected.Id)} already has minimum {MinimumVertexCount} vertices.");
                return false;
            }

            var removed = selected.Points[_selectedVertexIndex];
            selected.Points.RemoveAt(_selectedVertexIndex);
            _selectedVertexIndex = ClampVertexIndex(selected, _selectedVertexIndex);
            RecalculatePrice(selected);
            MarkChanged(
                $"{reason}: deleted vertex at {ParcelBounds.Format(removed)}, nextVertex={_selectedVertexIndex}, parcel={selected}");
            return true;
        }

        internal void ClearAllAndSeedDefault(string reason)
        {
            var previousCount = _parcels.Count;
            _parcels.Clear();
            _selectedParcelId = Guid.Empty;
            _selectedVertexIndex = -1;
            EnsureDefaultParcel(reason);
            MarkChanged($"{reason}: cleared {previousCount} parcel(s) and seeded default parcel.");
        }

        internal bool IsBuildable(float2 position)
        {
            return TryGetContainingPurchasedParcel(position, out _);
        }

        internal bool TryGetContainingPurchasedParcel(float2 position, out LandParcel parcel)
        {
            for (var i = 0; i < _parcels.Count; i++)
            {
                var candidate = _parcels[i];
                if (candidate.IsPurchased && PolygonMath.ContainsPoint(candidate.Points, position))
                {
                    parcel = candidate;
                    return true;
                }
            }

            parcel = null;
            return false;
        }

        internal bool TryGetActiveUnionBounds(out float2 min, out float2 max)
        {
            return TryGetUnionBounds(_parcels.Where(parcel => parcel.IsPurchased), out min, out max)
                   || TryGetUnionBounds(_parcels, out min, out max);
        }

        internal string GetSummary()
        {
            var purchased = _parcels.Count(parcel => parcel.IsPurchased);
            return $"parcels={_parcels.Count}, purchased={purchased}, selected={FormatGuid(_selectedParcelId)}, vertex={_selectedVertexIndex}, version={_version}";
        }

        public JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps)
            where TWriter : struct, IWriter
        {
            inputDeps.Complete();
            var writer = writerData.GetWriter<TWriter>();
            writer.Write(SaveSchemaVersion);
            writer.Write(_version);
            writer.Write(_selectedParcelId.ToString("N"));
            writer.Write(_selectedVertexIndex);
            writer.Write(_parcels.Count);
            for (var i = 0; i < _parcels.Count; i++)
            {
                var parcel = _parcels[i];
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

            Mod.log.Info($"Serialized ParcelStoreSystem: schema={SaveSchemaVersion}, {GetSummary()}.");
            return default;
        }

        public JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
            where TReader : struct, IReader
        {
            inputDeps.Complete();
            var reader = readerData.GetReader<TReader>();
            reader.Read(out int schemaVersion);

            _parcels.Clear();
            if (schemaVersion >= 2)
            {
                reader.Read(out uint savedVersion);
                reader.Read(out string selectedParcelIdText);
                reader.Read(out int selectedVertexIndex);
                reader.Read(out int parcelCount);
                for (var i = 0; i < parcelCount; i++)
                {
                    reader.Read(out string idText);
                    reader.Read(out string name);
                    reader.Read(out int state);
                    reader.Read(out int price);
                    reader.Read(out int pointCount);

                    var points = new List<float2>(pointCount);
                    for (var j = 0; j < pointCount; j++)
                    {
                        reader.Read(out float2 point);
                        points.Add(point);
                    }

                    if (TryParseGuid(idText, out var id) && points.Count >= MinimumVertexCount)
                    {
                        var parcel = new LandParcel(id, name, points)
                        {
                            State = (LandParcelState)state,
                            Price = price
                        };
                        RecalculatePrice(parcel);
                        _parcels.Add(parcel);
                    }
                    else
                    {
                        Mod.log.Warn(
                            $"Skipped invalid serialized parcel: id='{idText}', name='{name}', pointCount={pointCount}.");
                    }
                }

                _version = math.max(savedVersion, _version) + 1;
                _selectedParcelId = TryParseGuid(selectedParcelIdText, out var selectedId) ? selectedId : Guid.Empty;
                _selectedVertexIndex = selectedVertexIndex;
            }
            else
            {
                reader.Read(out float2 min);
                reader.Read(out float2 max);
                AddMigratedRectangle(min, max, "legacy schema deserialize");
                _version++;
            }

            EnsureValidSelection("deserialize");
            EnsureDefaultParcel("deserialize fallback");
            Mod.log.Info($"Deserialized ParcelStoreSystem: schema={schemaVersion}, {GetSummary()}.");
            return default;
        }

        public JobHandle SetDefaults(Context context)
        {
            _parcels.Clear();
            _selectedParcelId = Guid.Empty;
            _selectedVertexIndex = -1;
            EnsureDefaultParcel($"set defaults purpose={context.purpose}");
            _version++;
            Mod.log.Info($"ParcelStoreSystem defaults applied: purpose={context.purpose}, {GetSummary()}.");
            return default;
        }

        private void AddMigratedRectangle(float2 min, float2 max, string reason)
        {
            var normalizedMin = math.min(min, max);
            var normalizedMax = math.max(min, max);
            var points = new[]
            {
                normalizedMin,
                new float2(normalizedMin.x, normalizedMax.y),
                normalizedMax,
                new float2(normalizedMax.x, normalizedMin.y)
            };
            var parcel = new LandParcel(Guid.NewGuid(), "Migrated Parcel", points);
            RecalculatePrice(parcel);
            _parcels.Add(parcel);
            _selectedParcelId = parcel.Id;
            _selectedVertexIndex = 0;
            Mod.log.Info($"{reason}: migrated rectangle {ParcelBounds.Format(normalizedMin)}..{ParcelBounds.Format(normalizedMax)} into {parcel}.");
        }

        private void EnsureDefaultParcel(string reason)
        {
            if (_parcels.Count != 0)
            {
                return;
            }

            var parcel = new LandParcel(
                Guid.NewGuid(),
                "Parcel 1",
                new[]
                {
                    new float2(-500f, -500f),
                    new float2(-500f, 500f),
                    new float2(500f, 500f),
                    new float2(500f, -500f)
                });
            RecalculatePrice(parcel);
            _parcels.Add(parcel);
            _selectedParcelId = parcel.Id;
            _selectedVertexIndex = 0;
            Mod.log.Info($"{reason}: seeded default {parcel}.");
        }

        private void EnsureValidSelection(string reason)
        {
            if (_parcels.Count == 0)
            {
                _selectedParcelId = Guid.Empty;
                _selectedVertexIndex = -1;
                return;
            }

            if (FindParcel(_selectedParcelId) == null)
            {
                _selectedParcelId = _parcels[0].Id;
                Mod.log.Warn($"{reason}: selected parcel was missing; selected first parcel {FormatGuid(_selectedParcelId)}.");
            }

            _selectedVertexIndex = ClampVertexIndex(SelectedParcel, _selectedVertexIndex);
        }

        private LandParcel FindParcel(Guid id)
        {
            return id == Guid.Empty ? null : _parcels.FirstOrDefault(parcel => parcel.Id == id);
        }

        private static int ClampVertexIndex(LandParcel parcel, int index)
        {
            if (parcel == null || parcel.Points.Count == 0)
            {
                return -1;
            }

            if (index < 0)
            {
                return 0;
            }

            return math.min(index, parcel.Points.Count - 1);
        }

        private static void RecalculatePrice(LandParcel parcel)
        {
            parcel.Price = math.max(1000, (int)(PolygonMath.Area(parcel.Points) * 0.25f));
        }

        private static bool TryParseGuid(string text, out Guid id)
        {
            return Guid.TryParseExact(text, "N", out id) || Guid.TryParse(text, out id);
        }

        private static bool TryGetUnionBounds(IEnumerable<LandParcel> parcels, out float2 min, out float2 max)
        {
            min = new float2(float.MaxValue, float.MaxValue);
            max = new float2(float.MinValue, float.MinValue);
            var found = false;
            foreach (var parcel in parcels)
            {
                if (!PolygonMath.TryGetBounds(parcel.Points, out var parcelMin, out var parcelMax))
                {
                    continue;
                }

                min = math.min(min, parcelMin);
                max = math.max(max, parcelMax);
                found = true;
            }

            return found;
        }

        private void MarkChanged(string message)
        {
            _version++;
            Mod.log.Info($"ParcelStore changed: {message}; {GetSummary()}.");
        }

        internal static string FormatGuid(Guid id)
        {
            return id == Guid.Empty ? "<none>" : id.ToString("N");
        }
    }
}
