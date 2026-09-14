// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The sky around a nebula: an equirectangular panorama on a sphere the player stands inside.
    ///
    /// <para><b>Why this exists when PlaceShell already draws a dome.</b> PlaceShell builds its layers in
    /// <c>Start</c> and detaches itself from its parent on the way, which is right for the app - the shell
    /// belongs to a destination that is being opened - and useless for a scene you want to open and look at.
    /// Nothing appears until play begins, so every one of the seven nebula scenes read as an empty black room
    /// in the editor and there was no way to judge the sky against the gas without pressing play first. This
    /// draws the same panorama with no lifecycle opinions at all.</para>
    ///
    /// <para>The sphere is inverted - wound so the inside faces are the ones drawn - and unlit, because a sky
    /// is a backdrop rather than geometry. It is deliberately dim: the gas in front has to stay the brightest
    /// thing in the frame or the nebula is a silhouette against its own stars.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class NebulaSky : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The sky material, carrying this nebula's own equirectangular panorama.")]
        private Material skyMaterial;

        [SerializeField]
        [Tooltip("Radius in metres. Outside the gas, inside the camera's far plane.")]
        private float radiusMetres = 90f;

        [SerializeField]
        [Range(8, 96)]
        private int segments = 64;

        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Mesh _mesh;

        private void OnEnable()
        {
            EnsureRenderer();
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPreRender += OnCameraPreRender;
        }

        private void OnDisable()
        {
            Camera.onPreRender -= OnCameraPreRender;
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnCameraPreRender(Camera camera)
        {
            if (camera == null || camera.cameraType == CameraType.Preview) return;
            if (_renderer == null) EnsureRenderer();
        }

        private void Update()
        {
            if (_renderer == null) EnsureRenderer();
        }

        private void EnsureRenderer()
        {
            if (skyMaterial == null || _renderer != null) return;

            // Explicit null checks, never ?? - UnityEngine.Object overloads == and ?? does not consult it.
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            _filter = filter;

            if (_mesh == null) _mesh = Sphere(segments, segments / 2);
            _filter.sharedMesh = _mesh;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _renderer = meshRenderer;
            _renderer.sharedMaterial = skyMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _renderer.enabled = true;

            transform.localScale = Vector3.one * radiusMetres;
        }

        /// <summary>Sets the panorama and size from the builder.</summary>
        public void Configure(Material material, float radius)
        {
            skyMaterial = material;
            radiusMetres = radius;
        }

        /// <summary>
        /// A unit sphere wound inside-out, with equirectangular UVs: u around, v up. The winding is reversed
        /// rather than the material set to Cull Front, so the mesh is correct on its own and does not depend
        /// on which shader happens to be dropped on it.
        /// </summary>
        private static Mesh Sphere(int longitudes, int latitudes)
        {
            longitudes = Mathf.Max(8, longitudes);
            latitudes = Mathf.Max(4, latitudes);

            var vertices = new Vector3[(longitudes + 1) * (latitudes + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[longitudes * latitudes * 6];

            var v = 0;
            for (var lat = 0; lat <= latitudes; lat++)
            {
                var theta = Mathf.PI * lat / latitudes;      // 0 at the top
                var sinTheta = Mathf.Sin(theta);
                var cosTheta = Mathf.Cos(theta);

                for (var lon = 0; lon <= longitudes; lon++, v++)
                {
                    var phi = 2f * Mathf.PI * lon / longitudes;
                    vertices[v] = new Vector3(sinTheta * Mathf.Cos(phi), cosTheta, sinTheta * Mathf.Sin(phi));

                    // v runs 1 at the top down to 0, which is how an equirectangular plate is stored.
                    uvs[v] = new Vector2((float)lon / longitudes, 1f - (float)lat / latitudes);
                }
            }

            var t = 0;
            for (var lat = 0; lat < latitudes; lat++)
            {
                for (var lon = 0; lon < longitudes; lon++)
                {
                    var a = lat * (longitudes + 1) + lon;
                    var b = a + longitudes + 1;

                    // Reversed from the usual order, so the faces point inward at the player.
                    triangles[t++] = a;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;

                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = b + 1;
                }
            }

            var mesh = new Mesh { name = "nebula_sky_sphere" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
