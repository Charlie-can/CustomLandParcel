using Game;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// MVP parcel boundary renderer. Uses a simple LineRenderer so the custom parcel is visible immediately.
    /// </summary>
    public partial class ParcelBoundaryRenderSystem : GameSystemBase
    {
        private static readonly int kBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int kColor = Shader.PropertyToID("_Color");

        private GameObject _mLineObject;
        private LineRenderer _mLineRenderer;
        private Material _mMaterial;
        private readonly List<LineRenderer> _mAreaMarkerRenderers = new List<LineRenderer>();

        protected override void OnCreate()
        {
            base.OnCreate();
            CreateLineRenderer();
            Mod.log.Info("ParcelBoundaryRenderSystem enabled. Drawing MVP parcel boundary with LineRenderer.");
        }

        protected override void OnDestroy()
        {
            if (_mLineObject != null)
            {
                Object.Destroy(_mLineObject);
                _mLineObject = null;
            }

            if (_mMaterial != null)
            {
                Object.Destroy(_mMaterial);
                _mMaterial = null;
            }

            _mAreaMarkerRenderers.Clear();

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (_mLineRenderer == null)
            {
                CreateLineRenderer();
            }
        }

        private void CreateLineRenderer()
        {
            if (_mLineObject != null)
            {
                return;
            }

            _mLineObject = new GameObject("Custom Land Parcel MVP Boundary");
            Object.DontDestroyOnLoad(_mLineObject);

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _mMaterial = new Material(shader);
                SetMaterialColor(_mMaterial, new Color(0.05f, 1f, 0.35f, 0.95f));
                Mod.log.Info($"Parcel boundary renderer material created with shader '{shader.name}'.");
            }
            else
            {
                Mod.log.Warn(
                    "Parcel boundary renderer could not find Sprites/Default or Unlit/Color shader; LineRenderer will use Unity fallback material.");
            }

            var min = ConstructionRestrictionSystem.ParcelMin;
            var max = ConstructionRestrictionSystem.ParcelMax;
            const float y = 80f;
            var boundaryColor = new Color(0.05f, 1f, 0.35f, 0.98f);
            var markerColor = new Color(0.05f, 1f, 0.35f, 0.42f);

            _mLineRenderer = CreateRenderer("Buildable Area Outer Boundary", true, 4, 24f, boundaryColor);
            _mLineRenderer.SetPosition(0, ToVector3(new float3(min.x, y, min.y)));
            _mLineRenderer.SetPosition(1, ToVector3(new float3(max.x, y, min.y)));
            _mLineRenderer.SetPosition(2, ToVector3(new float3(max.x, y, max.y)));
            _mLineRenderer.SetPosition(3, ToVector3(new float3(min.x, y, max.y)));

            CreateBuildableAreaMarkers(min, max, y + 2f, markerColor);

            Mod.log.Info(
                $"Parcel buildable area marker created: parcel={FormatFloat2(min)}..{FormatFloat2(max)}, boundaryWidth={_mLineRenderer.widthMultiplier:F1}, interiorMarkerLines={_mAreaMarkerRenderers.Count}.");
        }

        private void CreateBuildableAreaMarkers(float2 min, float2 max, float y, Color markerColor)
        {
            const int divisions = 4;
            for (var i = 1; i < divisions; i++)
            {
                var t = i / (float)divisions;
                var x = math.lerp(min.x, max.x, t);
                var z = math.lerp(min.y, max.y, t);
                CreateMarkerSegment(
                    $"Buildable Area Vertical Marker {i}",
                    new float3(x, y, min.y),
                    new float3(x, y, max.y),
                    markerColor);
                CreateMarkerSegment(
                    $"Buildable Area Horizontal Marker {i}",
                    new float3(min.x, y, z),
                    new float3(max.x, y, z),
                    markerColor);
            }

            CreateMarkerSegment(
                "Buildable Area Diagonal Marker A",
                new float3(min.x, y, min.y),
                new float3(max.x, y, max.y),
                markerColor);
            CreateMarkerSegment(
                "Buildable Area Diagonal Marker B",
                new float3(min.x, y, max.y),
                new float3(max.x, y, min.y),
                markerColor);
        }

        private void CreateMarkerSegment(string name, float3 start, float3 end, Color color)
        {
            var renderer = CreateRenderer(name, false, 2, 8f, color);
            renderer.SetPosition(0, ToVector3(start));
            renderer.SetPosition(1, ToVector3(end));
            _mAreaMarkerRenderers.Add(renderer);
        }

        private LineRenderer CreateRenderer(string name, bool loop, int positionCount, float width, Color color)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_mLineObject.transform, false);
            var renderer = child.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.loop = loop;
            renderer.positionCount = positionCount;
            renderer.widthMultiplier = width;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.startColor = color;
            renderer.endColor = color;
            if (_mMaterial != null)
            {
                renderer.material = _mMaterial;
            }

            return renderer;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty(kBaseColor))
            {
                material.SetColor(kBaseColor, color);
            }

            if (material.HasProperty(kColor))
            {
                material.SetColor(kColor, color);
            }
        }

        private static string FormatFloat2(float2 value)
        {
            return $"({value.x:F1}, {value.y:F1})";
        }
    }
}