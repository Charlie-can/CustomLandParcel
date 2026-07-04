using System;
using System.Collections.Generic;
using System.Linq;
using CustomLandParcel.Geometry;
using Unity.Mathematics;

namespace CustomLandParcel.Data
{
    internal sealed class ParcelStore
    {
        private readonly List<LandParcel> _mParcels = new List<LandParcel>();
        private readonly Action<string> _mInfo;
        private readonly Action<string> _mWarn;
        private ParcelSelection _mSelection = ParcelSelection.Empty;
        private uint _mVersion;

        public ParcelStore(Action<string> info, Action<string> warn)
        {
            _mInfo = info;
            _mWarn = warn;
        }

        public IReadOnlyList<LandParcel> Parcels => _mParcels;

        public uint Version => _mVersion;

        public Guid SelectedParcelId => _mSelection.ParcelId;

        public int SelectedVertexIndex => _mSelection.VertexIndex;

        public LandParcel SelectedParcel => FindParcel(_mSelection.ParcelId);

        public void Initialize(uint version, string reason)
        {
            _mVersion = math.max(1u, version);
            EnsureDefaultParcel(reason);
            _mInfo($"ParcelStore initialized. {GetSummary()}.");
        }

        public LandParcel CreateRectangle(string name, float2 center, float2 size, string reason)
        {
            var parcel = ParcelGeometry.CreateRectangle(name, center, size);
            _mParcels.Add(parcel);
            _mSelection = new ParcelSelection(parcel.Id, 0);
            MarkChanged($"{reason}: created {parcel} and selected it");
            return parcel;
        }

        public bool DeleteSelectedParcel(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _mWarn($"Parcel delete ignored ({reason}): no selected parcel.");
                return false;
            }

            _mParcels.Remove(selected);
            _mSelection.ParcelId = _mParcels.Count == 0 ? Guid.Empty : _mParcels[0].Id;
            _mSelection.VertexIndex = ClampVertexIndex(SelectedParcel, _mSelection.VertexIndex);
            MarkChanged($"{reason}: deleted {selected}, nextSelected={FormatGuid(_mSelection.ParcelId)}");
            return true;
        }

        public bool SelectParcel(Guid id, string reason)
        {
            if (id == Guid.Empty || FindParcel(id) == null)
            {
                _mWarn($"Parcel selection ignored ({reason}): id={FormatGuid(id)} was not found.");
                return false;
            }

            _mSelection.ParcelId = id;
            _mSelection.VertexIndex = ClampVertexIndex(SelectedParcel, _mSelection.VertexIndex);
            MarkChanged(
                $"{reason}: selected parcel={FormatGuid(_mSelection.ParcelId)}, vertex={_mSelection.VertexIndex}");
            return true;
        }

        public bool SelectNextParcel(int direction, string reason)
        {
            if (_mParcels.Count == 0)
            {
                _mWarn($"Parcel select next ignored ({reason}): no parcels.");
                return false;
            }

            var current = _mParcels.FindIndex(parcel => parcel.Id == _mSelection.ParcelId);
            current = current < 0 ? 0 : (current + direction + _mParcels.Count) % _mParcels.Count;
            return SelectParcel(_mParcels[current].Id, reason);
        }

        public bool RenameSelectedParcel(string name, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _mWarn($"Parcel rename ignored ({reason}): no selected parcel.");
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
                _mWarn($"Parcel purchase ignored ({reason}): no selected parcel.");
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
                _mWarn($"Parcel move ignored ({reason}): no selected parcel.");
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
                _mWarn($"Parcel resize ignored ({reason}): no selected parcel.");
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
                _mWarn($"Vertex selection ignored ({reason}): no selected parcel.");
                return false;
            }

            if (vertexIndex < 0 || vertexIndex >= selected.Points.Count)
            {
                _mWarn(
                    $"Vertex selection ignored ({reason}): index={vertexIndex}, vertexCount={selected.Points.Count}.");
                return false;
            }

            _mSelection.VertexIndex = vertexIndex;
            MarkChanged($"{reason}: selected vertex={_mSelection.VertexIndex} parcel={FormatGuid(selected.Id)}");
            return true;
        }

        public bool MoveSelectedVertex(float2 delta, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || _mSelection.VertexIndex < 0 || _mSelection.VertexIndex >= selected.Points.Count)
            {
                _mWarn(
                    $"Vertex move ignored ({reason}): selectedParcel={FormatGuid(_mSelection.ParcelId)}, selectedVertex={_mSelection.VertexIndex}.");
                return false;
            }

            selected.Points[_mSelection.VertexIndex] += delta;
            ParcelGeometry.RecalculatePrice(selected);
            MarkChanged(
                $"{reason}: moved vertex={_mSelection.VertexIndex} by {ParcelGeometry.Format(delta)}, parcel={selected}");
            return true;
        }

        public bool InsertVertexAfterSelected(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || selected.Points.Count < ParcelGeometry.MinimumVertexCount)
            {
                _mWarn($"Vertex insert ignored ({reason}): selected parcel is invalid.");
                return false;
            }

            var index = _mSelection.VertexIndex >= 0 ? _mSelection.VertexIndex : selected.Points.Count - 1;
            var nextIndex = (index + 1) % selected.Points.Count;
            var inserted = (selected.Points[index] + selected.Points[nextIndex]) * 0.5f;
            selected.Points.Insert(index + 1, inserted);
            _mSelection.VertexIndex = index + 1;
            ParcelGeometry.RecalculatePrice(selected);
            MarkChanged(
                $"{reason}: inserted vertex={_mSelection.VertexIndex} at {ParcelGeometry.Format(inserted)}, parcel={selected}");
            return true;
        }

        public bool DeleteSelectedVertex(string reason)
        {
            var selected = SelectedParcel;
            if (selected == null || _mSelection.VertexIndex < 0 || _mSelection.VertexIndex >= selected.Points.Count)
            {
                _mWarn($"Vertex delete ignored ({reason}): no selected vertex.");
                return false;
            }

            if (selected.Points.Count <= ParcelGeometry.MinimumVertexCount)
            {
                _mWarn(
                    $"Vertex delete ignored ({reason}): parcel {FormatGuid(selected.Id)} already has minimum {ParcelGeometry.MinimumVertexCount} vertices.");
                return false;
            }

            var removed = selected.Points[_mSelection.VertexIndex];
            selected.Points.RemoveAt(_mSelection.VertexIndex);
            _mSelection.VertexIndex = ClampVertexIndex(selected, _mSelection.VertexIndex);
            ParcelGeometry.RecalculatePrice(selected);
            MarkChanged(
                $"{reason}: deleted vertex at {ParcelGeometry.Format(removed)}, nextVertex={_mSelection.VertexIndex}, parcel={selected}");
            return true;
        }

        public void ClearAllAndSeedDefault(string reason)
        {
            var previousCount = _mParcels.Count;
            _mParcels.Clear();
            _mSelection = ParcelSelection.Empty;
            EnsureDefaultParcel(reason);
            MarkChanged($"{reason}: cleared {previousCount} parcel(s) and seeded default parcel.");
        }

        public bool IsBuildable(float2 position)
        {
            return TryGetContainingPurchasedParcel(position, out _);
        }

        public bool TryGetContainingPurchasedParcel(float2 position, out LandParcel parcel)
        {
            for (var i = 0; i < _mParcels.Count; i++)
            {
                var candidate = _mParcels[i];
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
            return ParcelGeometry.TryGetUnionBounds(_mParcels.Where(parcel => parcel.IsPurchased), out min, out max)
                   || ParcelGeometry.TryGetUnionBounds(_mParcels, out min, out max);
        }

        public void ReplaceFromSave(
            IEnumerable<LandParcel> parcels,
            Guid selectedParcelId,
            int selectedVertexIndex,
            uint savedVersion,
            string reason)
        {
            _mParcels.Clear();
            _mParcels.AddRange(parcels);
            _mVersion = math.max(savedVersion, _mVersion) + 1;
            _mSelection = new ParcelSelection(selectedParcelId, selectedVertexIndex);
            EnsureValidSelection(reason);
            EnsureDefaultParcel($"{reason} fallback");
            _mInfo($"{reason}: replaced store from save. {GetSummary()}.");
        }

        public void SetDefaults(string reason)
        {
            _mParcels.Clear();
            _mSelection = ParcelSelection.Empty;
            EnsureDefaultParcel(reason);
            _mVersion++;
            _mInfo($"{reason}: defaults applied. {GetSummary()}.");
        }

        public string GetSummary()
        {
            var purchased = _mParcels.Count(parcel => parcel.IsPurchased);
            return
                $"parcels={_mParcels.Count}, purchased={purchased}, selected={FormatGuid(_mSelection.ParcelId)}, vertex={_mSelection.VertexIndex}, version={_mVersion}";
        }

        public static string FormatGuid(Guid id)
        {
            return id == Guid.Empty ? "<none>" : id.ToString("N");
        }

        private void EnsureDefaultParcel(string reason)
        {
            if (_mParcels.Count != 0)
            {
                return;
            }

            var parcel =
                ParcelGeometry.CreateRectangle("Parcel 1", ParcelGeometry.DefaultCenter, ParcelGeometry.DefaultSize);
            parcel.State = LandParcelState.Purchased;
            _mParcels.Add(parcel);
            _mSelection = new ParcelSelection(parcel.Id, 0);
            _mInfo($"{reason}: seeded default {parcel}.");
        }

        private void EnsureValidSelection(string reason)
        {
            if (_mParcels.Count == 0)
            {
                _mSelection = ParcelSelection.Empty;
                return;
            }

            if (FindParcel(_mSelection.ParcelId) == null)
            {
                _mSelection.ParcelId = _mParcels[0].Id;
                _mWarn(
                    $"{reason}: selected parcel was missing; selected first parcel {FormatGuid(_mSelection.ParcelId)}.");
            }

            _mSelection.VertexIndex = ClampVertexIndex(SelectedParcel, _mSelection.VertexIndex);
        }

        private LandParcel FindParcel(Guid id)
        {
            return id == Guid.Empty ? null : _mParcels.FirstOrDefault(parcel => parcel.Id == id);
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
            _mVersion++;
            _mInfo($"ParcelStore changed: {message}; {GetSummary()}.");
        }
    }
}
