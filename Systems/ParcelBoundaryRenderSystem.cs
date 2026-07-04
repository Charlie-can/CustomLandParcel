using Game;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// MVP parcel boundary renderer. Draws the current hardcoded parcel as an in-world strip.
    /// </summary>
    public partial class ParcelBoundaryRenderSystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_CityBoundaryQuery;
        private Mesh m_Mesh;
        private Material m_FallbackMaterial;
        private Material m_BoundaryMaterial;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_CityBoundaryQuery = GetEntityQuery(ComponentType.ReadOnly<CityBoundaryData>());
            BuildMesh(20f);
            Mod.log.Info("ParcelBoundaryRenderSystem enabled. Drawing MVP parcel boundary.");
        }

        protected override void OnDestroy()
        {
            if (m_Mesh != null)
            {
                Object.Destroy(m_Mesh);
                m_Mesh = null;
            }

            if (m_FallbackMaterial != null)
            {
                Object.Destroy(m_FallbackMaterial);
                m_FallbackMaterial = null;
            }

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            var material = GetBoundaryMaterial();
            if (m_Mesh == null || material == null)
            {
                return;
            }

            foreach (var camera in Camera.allCameras)
            {
                if (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView)
                {
                    Graphics.DrawMesh(m_Mesh, Matrix4x4.identity, material, 0, camera, 0, null, false, false);
                }
            }
        }

        private Material GetBoundaryMaterial()
        {
            if (m_BoundaryMaterial != null)
            {
                return m_BoundaryMaterial;
            }

            if (!m_CityBoundaryQuery.IsEmptyIgnoreFilter)
            {
                var prefab = m_PrefabSystem.GetPrefab<CityBoundaryPrefab>(m_CityBoundaryQuery.GetSingletonEntity());
                if (prefab != null && prefab.m_Material != null)
                {
                    m_BoundaryMaterial = prefab.m_Material;
                    return m_BoundaryMaterial;
                }
            }

            if (m_FallbackMaterial == null)
            {
                var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    return null;
                }

                m_FallbackMaterial = new Material(shader)
                {
                    color = new Color(0.1f, 0.85f, 1f, 0.7f)
                };
                if (m_FallbackMaterial.HasProperty("_BaseColor"))
                {
                    m_FallbackMaterial.SetColor("_BaseColor", new Color(0.1f, 0.85f, 1f, 0.7f));
                }
            }

            return m_FallbackMaterial;
        }

        private void BuildMesh(float width)
        {
            var min = ConstructionRestrictionSystem.ParcelMin;
            var max = ConstructionRestrictionSystem.ParcelMax;
            var y = 12f;
            var points = new[]
            {
                new float3(min.x, y, min.y),
                new float3(max.x, y, min.y),
                new float3(max.x, y, max.y),
                new float3(min.x, y, max.y)
            };

            var vertices = new Vector3[16];
            var uvs = new Vector2[16];
            var colors = new Color32[16];
            var indices = new int[24];
            var color = new Color32(32, 220, 255, 210);

            for (var i = 0; i < 4; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % 4];
                var direction = b.xz - a.xz;
                var length = math.length(direction);
                var normal = length > 0f ? new float2(-direction.y, direction.x) / length * (width * 0.5f) : float2.zero;
                var offset = new float3(normal.x, 0f, normal.y);
                var vi = i * 4;
                var ii = i * 6;

                vertices[vi] = a + offset;
                vertices[vi + 1] = a - offset;
                vertices[vi + 2] = b + offset;
                vertices[vi + 3] = b - offset;
                uvs[vi] = new Vector2(1f, 0f);
                uvs[vi + 1] = new Vector2(0f, 0f);
                uvs[vi + 2] = new Vector2(1f, 1f);
                uvs[vi + 3] = new Vector2(0f, 1f);
                colors[vi] = color;
                colors[vi + 1] = color;
                colors[vi + 2] = color;
                colors[vi + 3] = color;
                indices[ii] = vi;
                indices[ii + 1] = vi + 1;
                indices[ii + 2] = vi + 2;
                indices[ii + 3] = vi + 2;
                indices[ii + 4] = vi + 1;
                indices[ii + 5] = vi + 3;
            }

            m_Mesh = new Mesh
            {
                name = "Custom land parcel boundary"
            };
            m_Mesh.vertices = vertices;
            m_Mesh.uv = uvs;
            m_Mesh.colors32 = colors;
            m_Mesh.triangles = indices;
            m_Mesh.RecalculateBounds();
        }
    }
}
