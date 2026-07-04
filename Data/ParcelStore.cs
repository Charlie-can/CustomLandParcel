using System;
using System.Collections.Generic;
using System.Linq;
using CustomLandParcel.Geometry;
using Unity.Mathematics;

namespace CustomLandParcel.Data
{
    internal sealed class ParcelStore
    {
        private readonly List<LandParcel> _parcels = new List<LandParcel>();
        private readonly Action<string> _info;
        private readonly Action<string> _warn;
        private ParcelSelection _selection = ParcelSelection.Empty;
        private uint _version;

        public ParcelStore(Action<string> info, Action<string> warn)
        {
            _info = info;
            _warn = warn;
        }

        public IReadOnlyList<LandParcel> Parcels => _parcels;

        public uint Version => _version;

        public Guid SelectedParcelId => _selection.ParcelId;

        public int SelectedVertexIndex => _selection.VertexIndex;

        public LandParcel SelectedParcel => FindParcel(_selection.ParcelId);

        public void Initialize(uint version, string reason)
        {
            _version = math.max(1u, version);
            EnsureDefaultParcel(reason);
            _info($"ParcelStore initialized. {GetSummary()}.");
        }

        public LandParcel CreateRectangle(string name, float2 center, float2 size, string reason)
        {
            var parcel = ParcelGeometry.CreateRectangle(name, center, size);
            _parcels.Add(parcel);
            _selection = new ParcelSelection(parcel.Id, 0);
            MarkChanged($"{reason}: created {parcel} and selected it");
            return parcel;
        }

        public bool DeleteSelectedParcel(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _warn($"Parcel delete ignored ({reason}): no selected parcel.");
                return false;
            }

            _parcels.Remove(selected);
            _selection.ParcelId = _parcels.Count == 0 ? Guid.Empty : _parcels[0].Id;
            _selection.VertexIndex = ClampVertexIndex(SelectedParcel, _selection.VertexIndex);
            MarkChanged($"{reason}: deleted {selected}, nextSelected={FormatGuid(_selection.ParcelId)}");
            return true;
        }

        public bool SelectParcel(Guid id, string reason)
        {
            if (id == Guid.Empty || FindParcel(id) == null)
            {
                _warn($"Parcel selection ignored ({reason}): id={FormatGuid(id)} was not found.");
                return false;
            }

            _selection.ParcelId = id;
            _selection.VertexIndex = ClampVertexIndex(SelectedParcel, _selection.VertexIndex);
            MarkChanged($"{reason}: selected parcel={FormatGuid(_selection.ParcelId)}, vertex={_selection.VertexIndex}");
            return true;
        }

        public bool SelectNextParcel(int direction, string reason)
        {
            if (_parcels.Count == 0)
            {
                _warn($"Parcel select next ignored ({reason}): no parcels.");
                return false;
            }

            var current = _parcels.FindIndex(parcel => parcel.Id == _selection.ParcelId);
            current = current < 0 ? 0 : (current + direction + _parcels.Count) % _parcels.Count;
            return SelectParcel(_parcels[current].Id, reason);
        }

        public bool RenameSelectedParcel(string name, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _warn($"Parcel rename ignored ({reason}): no selected parcel.");
                return false;
            }

            var previous = selected.Name;
            selected.Name = string.IsNullOrWhiteSpace(name) ? selected.Name : name.Trim();
            MarkChanged($"{reason}: renamed parcel {FormatGuid(selected.Id)} from '{previous}' to '{selected.Name}'");
            return true;
        }

        public bool PurchaseSelectedParcel(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _warn($"Parcel purchase ignored ({reason}): no selected parcel.");
                return false;
            }

            selected.State = LandParcelState.Purchased;
            MarkChanged($"{reason}: purchased {selected}");
            return true;
        }

        public bool MoveSelectedParcel(float2 delta, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _warn($"Parcel move ignored ({reason}): no selected parcel.");
                return false;
            }

            for (var i = 0; i < selected.Points.Count; i++)
            {
                selected.Points[i] += delta;
            }

            MarkChanged($"{reason}: moved {selected} by {ParcelGeometry.Format(delta)}");
            return true;
        }

        public bool ResizeSelectedParcel(float amount, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _warn($"Parcel resize ignored ({reason}): no selected parcel.");
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

            ParcelGeometry.RecalculatePrice(selected);
            MarkChanged($"{reason}: resized {selected} by {amount:F1}");
            return true;
        }

        public bool SelectVertex(int vertexIndex, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _warn($"Vertex selection ignored ({reason}): no selected parcel.");
                return false;
            }

            if (vertexIndex < 0 || vertexIndex >= selected.Points.Count)
            {
                _warn($"Vertex selection ignored ({reason}): index={vertexIndex}, vertexCount={selected.Points.Count}.");
                return false;
            }

            _selection.VertexIndex = vertexIndex;
            MarkChanged($"{reason}: selected vertex={_selection.VertexIndex} parcel={FormatGuid(selected.Id)}");
            return true;
        }

        public bool MoveSelectedVertex(float2 delta, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || _selection.VertexIndex < 0 || _selection.VertexIndex >= selected.Points.Count)
            {
                _warn($"Vertex move ignored ({reason}): selectedParcel={FormatGuid(_selection.ParcelId)}, selectedVertex={_selection.VertexIndex}.");
                return false;
            }

            selected.Points[_selection.VertexIndex] += delta;
            ParcelGeometry.RecalculatePrice(selected);
            MarkChanged($"{reason}: moved vertex={_selection.VertexIndex} by {ParcelGeometry.Format(delta)}, parcel={selected}");
            return true;
        }

        public bool InsertVertexAfterSelected(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || selected.Points.Count < ParcelGeometry.MinimumVertexCount)
            {
                _warn($"Vertex insert ignored ({reason}): selected parcel is invalid.");
                return false;
            }

            var index = _selection.VertexIndex >= 0 ? _selection.VertexIndex : selected.Points.Count - 1;
            var nextIndex = (index + 1) % selected.Points.Count;
            var inserted = (selected.Points[index] + selected.Points[nextIndex]) * 0.5f;
            selected.Points.Insert(index + 1, inserted);
            _selection.VertexIndex = index + 1;
            ParcelGeometry.RecalculatePrice(selected);
            MarkChanged($"{reason}: inserted vertex={_selection.VertexIndex} at {ParcelGeometry.Format(inserted)}, parcel={selected}");
            return true;
        }

        public bool DeleteSelectedVertex(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || _selection.VertexIndex < 0 || _selection.VertexIndex >= selected.Points.Count)
            {
                _warn($"Vertex delete ignored ({reason}): no selected vertex.");
                return false;
            }

            if (selected.Points.Count <= ParcelGeometry.MinimumVertexCount)
            {
                _warn($"Vertex delete ignored ({reason}): parcel {FormatGuid(selected.Id)} already has minimum {ParcelGeometry.MinimumVertexCount} vertices.");
                return false;
            }

            var removed = selected.Points[_selection.VertexIndex];
            selected.Points.RemoveAt(_selection.VertexIndex);
            _selection.VertexIndex = ClampVertexIndex(selected, _selection.VertexIndex);
            ParcelGeometry.RecalculatePrice(selected);
            MarkChanged($"{reason}: deleted vertex at {ParcelGeometry.Format(removed)}, nextVertex={_selection.VertexIndex}, parcel={selected}");
            return true;
        }

        public void ClearAllAndSeedDefault(string reason)
        {
            var previousCount = _parcels.Count;
            _parcels.Clear();
            _selection = ParcelSelection.Empty;
            EnsureDefaultParcel(reason);
            MarkChanged($"{reason}: cleared {previousCount} parcel(s) and seeded default parcel.");
        }

        public bool IsBuildable(float2 position)
        {
            return TryGetContainingPurchasedParcel(position, out _);
        }

        public bool TryGetContainingPurchasedParcel(float2 position, out LandParcel parcel)
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

        public bool TryGetActiveUnionBounds(out float2 min, out float2 max)
        {
            return ParcelGeometry.TryGetUnionBounds(_parcels.Where(parcel => parcel.IsPurchased), out min, out max)
                   || ParcelGeometry.TryGetUnionBounds(_parcels, out min, out max);
        }

        public void ReplaceFromSave(
            IEnumerable<LandParcel> parcels,
            Guid selectedParcelId,
            int selectedVertexIndex,
            uint savedVersion,
            string reason)
        {
            _parcels.Clear();
            _parcels.AddRange(parcels);
            _version = math.max(savedVersion, _version) + 1;
            _selection = new ParcelSelection(selectedParcelId, selectedVertexIndex);
            EnsureValidSelection(reason);
            EnsureDefaultParcel($"{reason} fallback");
            _info($"{reason}: replaced store from save. {GetSummary()}.");
        }

        public void SetDefaults(string reason)
        {
            _parcels.Clear();
            _selection = ParcelSelection.Empty;
            EnsureDefaultParcel(reason);
            _version++;
            _info($"{reason}: defaults applied. {GetSummary()}.");
        }

        public string GetSummary()
        {
            var purchased = _parcels.Count(parcel => parcel.IsPurchased);
            return $"parcels={_parcels.Count}, purchased={purchased}, selected={FormatGuid(_selection.ParcelId)}, vertex={_selection.VertexIndex}, version={_version}";
        }

        public static string FormatGuid(Guid id)
        {
            return id == Guid.Empty ? "<none>" : id.ToString("N");
        }

        private void EnsureDefaultParcel(string reason)
        {
            if (_parcels.Count != 0)
            {
                return;
            }

            var parcel = ParcelGeometry.CreateRectangle("Parcel 1", ParcelGeometry.DefaultCenter, ParcelGeometry.DefaultSize);
            _parcels.Add(parcel);
            _selection = new ParcelSelection(parcel.Id, 0);
            _info($"{reason}: seeded default {parcel}.");
        }

        private void EnsureValidSelection(string reason)
        {
            if (_parcels.Count == 0)
            {
                _selection = ParcelSelection.Empty;
                return;
            }

            if (FindParcel(_selection.ParcelId) == null)
            {
                _selection.ParcelId = _parcels[0].Id;
                _warn($"{reason}: selected parcel was missing; selected first parcel {FormatGuid(_selection.ParcelId)}.");
            }

            _selection.VertexIndex = ClampVertexIndex(SelectedParcel, _selection.VertexIndex);
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

            return index < 0 ? 0 : math.min(index, parcel.Points.Count - 1);
        }

        private void MarkChanged(string message)
        {
            _version++;
            _info($"ParcelStore changed: {message}; {GetSummary()}.");
        }
    }
}
