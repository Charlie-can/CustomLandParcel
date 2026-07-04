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
        private GameObject m_LineObject;
        private LineRenderer m_LineRenderer;
        private Material m_Material;

        protected override void OnCreate()
        {
            base.OnCreate();
            CreateLineRenderer();
            Mod.log.Info("ParcelBoundaryRenderSystem enabled. Drawing MVP parcel boundary with LineRenderer.");
        }

        protected override void OnDestroy()
        {
            if (m_LineObject != null)
            {
                Object.Destroy(m_LineObject);
                m_LineObject = null;
            }

            if (m_Material != null)
            {
                Object.Destroy(m_Material);
                m_Material = null;
            }

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (m_LineRenderer == null)
            {
                CreateLineRenderer();
            }
        }

        private void CreateLineRenderer()
        {
            if (m_LineObject != null)
            {
                return;
            }

            m_LineObject = new GameObject("Custom Land Parcel MVP Boundary");
            Object.DontDestroyOnLoad(m_LineObject);
            m_LineRenderer = m_LineObject.AddComponent<LineRenderer>();
            m_LineRenderer.useWorldSpace = true;
            m_LineRenderer.loop = true;
            m_LineRenderer.positionCount = 4;
            m_LineRenderer.widthMultiplier = 18f;
            m_LineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_LineRenderer.receiveShadows = false;
            m_LineRenderer.startColor = new Color(0f, 0.95f, 1f, 0.95f);
            m_LineRenderer.endColor = new Color(0f, 0.95f, 1f, 0.95f);

            var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                m_Material = new Material(shader);
                SetMaterialColor(m_Material, new Color(0f, 0.95f, 1f, 0.95f));
                m_LineRenderer.material = m_Material;
            }

            var min = ConstructionRestrictionSystem.ParcelMin;
            var max = ConstructionRestrictionSystem.ParcelMax;
            const float y = 80f;
            m_LineRenderer.SetPosition(0, ToVector3(new float3(min.x, y, min.y)));
            m_LineRenderer.SetPosition(1, ToVector3(new float3(max.x, y, min.y)));
            m_LineRenderer.SetPosition(2, ToVector3(new float3(max.x, y, max.y)));
            m_LineRenderer.SetPosition(3, ToVector3(new float3(min.x, y, max.y)));
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }
}