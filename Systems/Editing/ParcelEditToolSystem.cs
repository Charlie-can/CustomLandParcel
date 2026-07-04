using System;
using System.Linq;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using Game.Areas;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Native tool-style mouse editor for drawing and editing custom parcel polygons.
    /// </summary>
    public partial class ParcelEditToolSystem : ToolBaseSystem
    {
        private const float VertexHitRadius = 55f;
        private const float EdgeHitRadius = 38f;
        private const float ClosePolygonRadius = 75f;
        private const float DragStartDistance = 8f;

        private ParcelStoreSystem _mParcelStoreSystem;
        private bool _mPointerDown;
        private bool _mDragStarted;
        private float2 _mPointerDownPosition;
        private float2 _mLastDragPosition;
        private ParcelEditHit _mPointerDownHit;

        internal ParcelEditSession Session { get; } = new ParcelEditSession();

        public override string toolID => "CustomLandParcel.ParcelEditTool";

        internal bool IsToolActive => m_ToolSystem != null && m_ToolSystem.activeTool == this;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            Mod.log.Info("ParcelEditToolSystem enabled and registered with ToolSystem.");
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!TryGetCursorPosition(out var cursorPosition))
            {
                if (Session.IsDrawing || Session.IsDragging)
                {
                    Mod.log.Warn($"Parcel edit tool raycast failed while active. {Session.GetSummary()}.");
                }

                return inputDeps;
            }

            UpdateHover(cursorPosition);
            HandleInput(cursorPosition);
            return inputDeps;
        }

        public override PrefabBase GetPrefab()
        {
            return null;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            return false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
            m_ToolRaycastSystem.netLayerMask = Layer.None;
            m_ToolRaycastSystem.areaTypeMask = AreaTypeMask.None;
        }

        internal void SetToolActive(bool active, string reason)
        {
            if (active)
            {
                m_ToolSystem.activeTool = this;
                Mod.log.Info($"Parcel edit tool activated ({reason}). {_mParcelStoreSystem.GetSummary()}.");
                return;
            }

            if (IsToolActive)
            {
                Session.Cancel();
                _mPointerDown = false;
                _mDragStarted = false;
                m_ToolSystem.activeTool = m_DefaultToolSystem;
                Mod.log.Info($"Parcel edit tool deactivated ({reason}). {_mParcelStoreSystem.GetSummary()}.");
            }
        }

        private bool TryGetCursorPosition(out float2 position)
        {
            if (GetRaycastResult(out Entity _, out var hit))
            {
                position = new float2(hit.m_HitPosition.x, hit.m_HitPosition.z);
                return true;
            }

            position = float2.zero;
            return false;
        }

        private void UpdateHover(float2 cursorPosition)
        {
            if (Session.IsDragging)
            {
                return;
            }

            if (Session.IsDrawing)
            {
                var closeHit = GetDraftCloseHit(cursorPosition);
                Session.SetHover(closeHit);
                return;
            }

            var hit = ParcelEditHitTest.FindBestHit(
                _mParcelStoreSystem.Parcels,
                cursorPosition,
                VertexHitRadius,
                EdgeHitRadius);
            if (!hit.HasSameTarget(Session.Hover))
            {
                Mod.log.Info($"Parcel edit hover changed: {hit}.");
            }

            Session.SetHover(hit);
        }

        private void HandleInput(float2 cursorPosition)
        {
            if (cancelAction.WasPressedThisFrame())
            {
                CancelCurrentOperation("cancel action");
                return;
            }

            if (applyAction.WasPressedThisFrame())
            {
                BeginPointerAction(cursorPosition);
            }

            if (_mPointerDown && applyAction.IsInProgress())
            {
                UpdatePointerAction(cursorPosition);
            }

            if (_mPointerDown && applyAction.WasReleasedThisFrame())
            {
                EndPointerAction(cursorPosition);
            }
        }

        private void BeginPointerAction(float2 cursorPosition)
        {
            _mPointerDown = true;
            _mDragStarted = false;
            _mPointerDownPosition = cursorPosition;
            _mLastDragPosition = cursorPosition;
            _mPointerDownHit = Session.Hover;

            if (Session.IsDrawing)
            {
                return;
            }

            if (_mPointerDownHit.Kind == ParcelEditHitKind.Vertex || _mPointerDownHit.Kind == ParcelEditHitKind.Parcel)
            {
                var parcel = FindParcel(_mPointerDownHit.ParcelId);
                Session.StartDrag(_mPointerDownHit, cursorPosition, parcel);
                Mod.log.Info($"Parcel edit drag prepared: hit={_mPointerDownHit}, {Session.GetSummary()}.");
            }
        }

        private void UpdatePointerAction(float2 cursorPosition)
        {
            if (!Session.IsDragging)
            {
                return;
            }

            var dragDelta = cursorPosition - _mPointerDownPosition;
            if (!_mDragStarted && math.length(dragDelta) < DragStartDistance)
            {
                return;
            }

            _mDragStarted = true;
            if (Session.DragTarget.Kind == ParcelEditHitKind.Vertex)
            {
                _mParcelStoreSystem.SetVertexPositionTransient(
                    Session.DragTarget.ParcelId,
                    Session.DragTarget.VertexIndex,
                    cursorPosition,
                    "map drag vertex");
                return;
            }

            if (Session.DragTarget.Kind == ParcelEditHitKind.Parcel && Session.DragOriginalPoints.Count > 0)
            {
                var frameDelta = cursorPosition - _mLastDragPosition;
                _mParcelStoreSystem.MoveParcelTransient(Session.DragTarget.ParcelId, frameDelta, "map drag parcel");
                _mLastDragPosition = cursorPosition;
            }
        }

        private void EndPointerAction(float2 cursorPosition)
        {
            _mPointerDown = false;

            if (Session.IsDragging)
            {
                if (_mDragStarted)
                {
                    _mParcelStoreSystem.CommitParcelGeometry(Session.DragTarget.ParcelId, "map drag end");
                    Mod.log.Info(
                        $"Parcel edit drag ended: target={Session.DragTarget}, delta={ParcelGeometry.Format(cursorPosition - _mPointerDownPosition)}, changed=True.");
                    Session.EndDrag();
                    return;
                }

                Session.EndDrag();
            }

            if (Session.IsDrawing)
            {
                ContinueDrawing(cursorPosition);
                return;
            }

            switch (_mPointerDownHit.Kind)
            {
                case ParcelEditHitKind.Edge:
                    _mParcelStoreSystem.SelectParcel(_mPointerDownHit.ParcelId, "map select edge parcel");
                    _mParcelStoreSystem.InsertVertexOnEdge(
                        _mPointerDownHit.ParcelId,
                        _mPointerDownHit.EdgeIndex,
                        "map insert vertex on edge");
                    break;
                case ParcelEditHitKind.Parcel:
                    _mParcelStoreSystem.SelectParcel(_mPointerDownHit.ParcelId, "map select parcel");
                    break;
                case ParcelEditHitKind.Vertex:
                    _mParcelStoreSystem.SelectParcel(_mPointerDownHit.ParcelId, "map select vertex parcel");
                    _mParcelStoreSystem.SelectVertex(_mPointerDownHit.VertexIndex, "map select vertex");
                    break;
                default:
                    Session.StartDrawing(cursorPosition);
                    Mod.log.Info($"Parcel edit draft started at {ParcelGeometry.Format(cursorPosition)}.");
                    break;
            }
        }

        private void ContinueDrawing(float2 cursorPosition)
        {
            if (ShouldCloseDraft(cursorPosition))
            {
                CompleteDraft("map close draft");
                return;
            }

            Session.AddDraftPoint(cursorPosition);
            Mod.log.Info(
                $"Parcel edit draft point added: point={ParcelGeometry.Format(cursorPosition)}, pointCount={Session.DraftPoints.Count}.");
        }

        private void CompleteDraft(string reason)
        {
            if (Session.DraftPoints.Count < ParcelGeometry.MinimumVertexCount)
            {
                Mod.log.Warn(
                    $"Parcel edit draft completion rejected ({reason}): pointCount={Session.DraftPoints.Count}.");
                return;
            }

            var parcel = _mParcelStoreSystem.CreatePolygon(
                $"Parcel {_mParcelStoreSystem.Parcels.Count + 1}",
                Session.DraftPoints.ToArray(),
                LandParcelState.Available,
                reason);
            Session.ClearDraft();
            Mod.log.Info(
                $"Parcel edit draft completed ({reason}): parcel={ParcelStoreSystem.FormatGuid(parcel?.Id ?? Guid.Empty)}, {_mParcelStoreSystem.GetSummary()}.");
        }

        private void CancelCurrentOperation(string reason)
        {
            if (!Session.IsDrawing && !Session.IsDragging)
            {
                SetToolActive(false, reason);
                return;
            }

            Mod.log.Info($"Parcel edit operation canceled ({reason}). {Session.GetSummary()}.");
            Session.Cancel();
            _mPointerDown = false;
            _mDragStarted = false;
        }

        private ParcelEditHit GetDraftCloseHit(float2 cursorPosition)
        {
            if (Session.DraftPoints.Count < ParcelGeometry.MinimumVertexCount)
            {
                return ParcelEditHit.None;
            }

            var distance = math.distance(cursorPosition, Session.DraftPoints[0]);
            return distance <= ClosePolygonRadius
                ? new ParcelEditHit(ParcelEditHitKind.Vertex, Guid.Empty, 0, -1, distance)
                : ParcelEditHit.None;
        }

        private bool ShouldCloseDraft(float2 cursorPosition)
        {
            if (Session.DraftPoints.Count < ParcelGeometry.MinimumVertexCount)
            {
                return false;
            }

            return math.distance(cursorPosition, Session.DraftPoints[0]) <= ClosePolygonRadius;
        }

        private LandParcel FindParcel(Guid parcelId)
        {
            return _mParcelStoreSystem.Parcels.FirstOrDefault(parcel => parcel.Id == parcelId);
        }
    }
}
