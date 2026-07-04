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
            parcel.State = LandParcelState.Purchased;
            _mParcels.Add(parcel);
            RepriceParcel(parcel, $"{reason}: create rectangle");
            _mSelection = new ParcelSelection(parcel.Id, 0);
            MarkChanged($"{reason}: created {parcel} and selected it");
            return parcel;
        }

        public LandParcel CreatePolygon(string name, IEnumerable<float2> points, LandParcelState state, string reason)
        {
            var pointList = points == null ? new List<float2>() : new List<float2>(points);
            if (pointList.Count < ParcelGeometry.MinimumVertexCount)
            {
                _mWarn(
                    $"Parcel polygon create ignored ({reason}): pointCount={pointList.Count}, minimum={ParcelGeometry.MinimumVertexCount}.");
                return null;
            }

            var parcel = new LandParcel(Guid.NewGuid(), name, pointList)
            {
                State = state
            };
            _mParcels.Add(parcel);
            RepriceParcel(parcel, $"{reason}: create polygon");
            _mSelection = new ParcelSelection(parcel.Id, 0);
            MarkChanged($"{reason}: created polygon {parcel} and selected it");
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

        public bool MergeSelectedParcelWith(Guid targetId, string reason)
        {
            var selected = SelectedParcel;
            var target = FindParcel(targetId);
            if (selected == null || target == null || selected.Id == target.Id)
            {
                _mWarn(
                    $"Parcel merge ignored ({reason}): selected={FormatGuid(_mSelection.ParcelId)}, target={FormatGuid(targetId)}.");
                return false;
            }

            var mergedPoints = PolygonMath.ConvexHull(selected.Points.Concat(target.Points));
            if (mergedPoints.Count < ParcelGeometry.MinimumVertexCount)
            {
                _mWarn(
                    $"Parcel merge ignored ({reason}): selected={FormatGuid(selected.Id)}, target={FormatGuid(target.Id)}, hullPoints={mergedPoints.Count}.");
                return false;
            }

            var previousSelected = selected.ToString();
            var previousTarget = target.ToString();
            selected.Name = $"{selected.Name} + {target.Name}";
            selected.State = selected.IsBuildable && target.IsBuildable ? LandParcelState.Purchased : LandParcelState.Locked;
            selected.Points.Clear();
            selected.Points.AddRange(mergedPoints);
            _mParcels.Remove(target);
            _mSelection = new ParcelSelection(selected.Id, ClampVertexIndex(selected, _mSelection.VertexIndex));
            RepriceParcel(selected, $"{reason}: merge parcels");
            MarkChanged(
                $"{reason}: merged selected={previousSelected} with target={previousTarget}, result={selected}");
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

        public bool SetSelectedParcelState(LandParcelState state, string reason)
        {
            var selected = SelectedParcel;
            if (selected == null)
            {
                _mWarn($"Parcel state change ignored ({reason}): no selected parcel.");
                return false;
            }

            var previous = selected.State;
            selected.State = state;
            RepriceParcel(selected, $"{reason}: state change");
            MarkChanged($"{reason}: changed parcel {FormatGuid(selected.Id)} state from {previous} to {state}, {selected}");
            return true;
        }

        public bool MoveSelectedParcel(float2 delta, string reason)
        {
            var selected = SelectedParcel;
            if (selected != null)
            {
                return MoveParcel(selected.Id, delta, reason);
            }

            _mWarn($"Parcel move ignored ({reason}): no selected parcel.");
            return false;
        }

        public bool MoveParcel(Guid parcelId, float2 delta, string reason)
        {
            var parcel = FindParcel(parcelId);
            if (parcel == null)
            {
                _mWarn($"Parcel move ignored ({reason}): parcel={FormatGuid(parcelId)} was not found.");
                return false;
            }

            for (var i = 0; i < parcel.Points.Count; i++)
            {
                parcel.Points[i] += delta;
            }

            RepriceParcel(parcel, $"{reason}: move parcel");
            _mSelection = new ParcelSelection(parcel.Id, ClampVertexIndex(parcel, _mSelection.VertexIndex));
            MarkChanged($"{reason}: moved {parcel} by {ParcelGeometry.Format(delta)}");
            return true;
        }

        public bool MoveParcelTransient(Guid parcelId, float2 delta, string reason)
        {
            var parcel = FindParcel(parcelId);
            if (parcel == null)
            {
                _mWarn($"Transient parcel move ignored ({reason}): parcel={FormatGuid(parcelId)} was not found.");
                return false;
            }

            for (var i = 0; i < parcel.Points.Count; i++)
            {
                parcel.Points[i] += delta;
            }

            _mSelection = new ParcelSelection(parcel.Id, ClampVertexIndex(parcel, _mSelection.VertexIndex));
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

            RepriceParcel(selected, $"{reason}: resize parcel");
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

            return SetVertexPosition(
                selected.Id,
                _mSelection.VertexIndex,
                selected.Points[_mSelection.VertexIndex] + delta,
                reason);
        }

        public bool SetVertexPosition(Guid parcelId, int vertexIndex, float2 position, string reason)
        {
            var selected = FindParcel(parcelId);
            if (selected == null || vertexIndex < 0 || vertexIndex >= selected.Points.Count)
            {
                _mWarn(
                    $"Vertex set ignored ({reason}): parcel={FormatGuid(parcelId)}, vertex={vertexIndex}.");
                return false;
            }

            var previous = selected.Points[vertexIndex];
            selected.Points[vertexIndex] = position;
            _mSelection = new ParcelSelection(selected.Id, vertexIndex);
            RepriceParcel(selected, $"{reason}: set vertex");
            MarkChanged(
                $"{reason}: set vertex={vertexIndex} from {ParcelGeometry.Format(previous)} to {ParcelGeometry.Format(position)}, parcel={selected}");
            return true;
        }

        public bool SetVertexPositionTransient(Guid parcelId, int vertexIndex, float2 position, string reason)
        {
            var selected = FindParcel(parcelId);
            if (selected == null || vertexIndex < 0 || vertexIndex >= selected.Points.Count)
            {
                _mWarn(
                    $"Transient vertex set ignored ({reason}): parcel={FormatGuid(parcelId)}, vertex={vertexIndex}.");
                return false;
            }

            selected.Points[vertexIndex] = position;
            _mSelection = new ParcelSelection(selected.Id, vertexIndex);
            return true;
        }

        public bool CommitParcelGeometry(Guid parcelId, string reason)
        {
            var parcel = FindParcel(parcelId);
            if (parcel == null)
            {
                _mWarn($"Parcel geometry commit ignored ({reason}): parcel={FormatGuid(parcelId)} was not found.");
                return false;
            }

            RepriceParcel(parcel, $"{reason}: commit geometry");
            _mSelection = new ParcelSelection(parcel.Id, ClampVertexIndex(parcel, _mSelection.VertexIndex));
            MarkChanged($"{reason}: committed geometry for {parcel}");
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
            return InsertVertexOnEdge(selected.Id, index, reason);
        }

        public bool InsertVertexOnEdge(Guid parcelId, int edgeIndex, string reason)
        {
            var selected = FindParcel(parcelId);
            if (selected == null || selected.Points.Count < ParcelGeometry.MinimumVertexCount)
            {
                _mWarn(
                    $"Vertex insert ignored ({reason}): parcel={FormatGuid(parcelId)} is missing or invalid.");
                return false;
            }

            if (edgeIndex < 0 || edgeIndex >= selected.Points.Count)
            {
                _mWarn(
                    $"Vertex insert ignored ({reason}): edgeIndex={edgeIndex}, vertexCount={selected.Points.Count}.");
                return false;
            }

            var index = edgeIndex;
            var nextIndex = (index + 1) % selected.Points.Count;
            var inserted = (selected.Points[index] + selected.Points[nextIndex]) * 0.5f;
            selected.Points.Insert(index + 1, inserted);
            _mSelection = new ParcelSelection(selected.Id, index + 1);
            RepriceParcel(selected, $"{reason}: insert vertex");
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
            RepriceParcel(selected, $"{reason}: delete vertex");
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
            return TryGetContainingBuildableParcel(position, out _);
        }

        public bool TryGetContainingBuildableParcel(float2 position, out LandParcel parcel)
        {
            for (var i = 0; i < _mParcels.Count; i++)
            {
                var candidate = _mParcels[i];
                if (candidate.IsBuildable && PolygonMath.ContainsPoint(candidate.Points, position))
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
            return ParcelGeometry.TryGetUnionBounds(_mParcels.Where(parcel => parcel.IsBuildable), out min, out max)
                   || ParcelGeometry.TryGetUnionBounds(_mParcels, out min, out max);
        }

        public bool TryGetBuildableUnionBounds(out float2 min, out float2 max)
        {
            return ParcelGeometry.TryGetUnionBounds(_mParcels.Where(parcel => parcel.IsBuildable), out min, out max);
        }

        public bool TryAlignDefaultParcelToBounds(float2 min, float2 max, string reason)
        {
            if (_mParcels.Count != 1)
            {
                return false;
            }

            var parcel = _mParcels[0];
            if (!LooksLikeDefaultSeedParcel(parcel) || !math.all(max > min + ParcelGeometry.MinimumSize))
            {
                return false;
            }

            var previous = parcel.ToString();
            parcel.Points.Clear();
            parcel.Points.AddRange(CreateRectanglePoints(min, max));
            parcel.State = LandParcelState.Purchased;
            _mSelection = new ParcelSelection(parcel.Id, ClampVertexIndex(parcel, _mSelection.VertexIndex));
            RepriceParcel(parcel, $"{reason}: align default parcel");
            MarkChanged($"{reason}: aligned default parcel from {previous} to bounds {ParcelGeometry.Format(min)}..{ParcelGeometry.Format(max)}");
            return true;
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
            for (var i = 0; i < _mParcels.Count; i++)
            {
                RepriceParcel(_mParcels[i], $"{reason}: load");
            }

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
            var buildable = _mParcels.Count(parcel => parcel.IsBuildable);
            return
                $"parcels={_mParcels.Count}, buildable={buildable}, selected={FormatGuid(_mSelection.ParcelId)}, vertex={_mSelection.VertexIndex}, version={_mVersion}";
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
            RepriceParcel(parcel, $"{reason}: seed default");
            _mSelection = new ParcelSelection(parcel.Id, 0);
            _mInfo($"{reason}: seeded default {parcel}.");
        }

        private static List<float2> CreateRectanglePoints(float2 min, float2 max)
        {
            return new List<float2>
            {
                new float2(min.x, min.y),
                new float2(min.x, max.y),
                new float2(max.x, max.y),
                new float2(max.x, min.y)
            };
        }

        private static bool LooksLikeDefaultSeedParcel(LandParcel parcel)
        {
            if (parcel == null || parcel.Points.Count != 4)
            {
                return false;
            }

            var half = ParcelGeometry.DefaultSize * 0.5f;
            var expected = CreateRectanglePoints(ParcelGeometry.DefaultCenter - half, ParcelGeometry.DefaultCenter + half);
            for (var i = 0; i < expected.Count; i++)
            {
                if (math.distancesq(parcel.Points[i], expected[i]) > 1f)
                {
                    return false;
                }
            }

            return true;
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

        private void RepriceParcel(LandParcel parcel, string reason)
        {
            if (parcel == null)
            {
                return;
            }

            var previous = parcel.Price;
            var result = ParcelPriceCalculator.Calculate(parcel, _mParcels);
            parcel.Price = result.Price;
            _mInfo(
                $"Parcel price recalculated ({reason}): parcel={FormatGuid(parcel.Id)}, previous={previous}, {result.ToLogString()}.");
        }
    }
}
