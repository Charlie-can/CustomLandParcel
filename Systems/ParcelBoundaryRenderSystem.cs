using Game;
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
            _mLineRenderer = _mLineObject.AddComponent<LineRenderer>();
            _mLineRenderer.useWorldSpace = true;
            _mLineRenderer.loop = true;
            _mLineRenderer.positionCount = 4;
            _mLineRenderer.widthMultiplier = 18f;
            _mLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mLineRenderer.receiveShadows = false;
            _mLineRenderer.startColor = new Color(0f, 0.95f, 1f, 0.95f);
            _mLineRenderer.endColor = new Color(0f, 0.95f, 1f, 0.95f);

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _mMaterial = new Material(shader);
                SetMaterialColor(_mMaterial, new Color(0f, 0.95f, 1f, 0.95f));
                _mLineRenderer.material = _mMaterial;
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
            _mLineRenderer.SetPosition(0, ToVector3(new float3(min.x, y, min.y)));
            _mLineRenderer.SetPosition(1, ToVector3(new float3(max.x, y, min.y)));
            _mLineRenderer.SetPosition(2, ToVector3(new float3(max.x, y, max.y)));
            _mLineRenderer.SetPosition(3, ToVector3(new float3(min.x, y, max.y)));

            Mod.log.Info(
                $"Parcel boundary renderer points: ({min.x:F1}, {y:F1}, {min.y:F1}) -> ({max.x:F1}, {y:F1}, {min.y:F1}) -> ({max.x:F1}, {y:F1}, {max.y:F1}) -> ({min.x:F1}, {y:F1}, {max.y:F1}); width={_mLineRenderer.widthMultiplier:F1}.");
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
    }
}