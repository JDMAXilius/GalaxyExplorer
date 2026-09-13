// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Draws a nebula as a marched density field: the gas itself, as opposed to the points that describe it.
    ///
    /// <para>It is a box the player stands inside, with <c>CosmicSimulation/NebulaField</c> on it and the
    /// nebula's baked <see cref="Texture3D"/> in the material. The shader flips the faces and marches from the
    /// eye, so being inside is the normal case rather than an edge case.</para>
    ///
    /// <para><b>Why this sits beside the splats rather than replacing them.</b> The field is continuous and
    /// occludes, which is what makes it read as gas, and it costs the same whatever radius it is given. What it
    /// cannot do is detail: at 64 cubed it holds the shape of the cloud, not the grain of it, and a marched
    /// field on its own looks like beautifully lit fog. The splats carry the grain - individual points the eye
    /// can catch and resolve - and the stars carry the sparkle. Three representations of the same object at
    /// three frequencies, which is what every renderer that does this well ends up doing.</para>
    ///
    /// <para><b>Cost, honestly.</b> This is the expensive one: a fragment marching 28 steps over whatever
    /// fraction of the screen the box covers, and from inside the box that fraction is all of it. On a
    /// tile-based mobile GPU that is the single most expensive thing in this app. It is off by default for
    /// exactly that reason - <see cref="enabled"/> is the toggle the research document promised - and it wants
    /// measuring on the device before it is switched on anywhere that ships.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NebulaField : MonoBehaviour
    {
        private static readonly int VolumeId = Shader.PropertyToID("_Volume");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int StepsId = Shader.PropertyToID("_Steps");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");

        [SerializeField]
        [Tooltip("The baked density volume, written by Cosmic Simulation > Build Nebula Fields.")]
        private Texture3D volume;

        [SerializeField]
        [Tooltip("Half-width of the field in metres. A field costs the same at any size.")]
        private float radiusMetres = 9f;

        [SerializeField]
        [Range(8, 64)]
        [Tooltip("March steps. Fewer is cheaper and bands more; the shader dithers the start to hide it.")]
        private int steps = 28;

        [SerializeField]
        [Range(0f, 8f)]
        private float density = 1.6f;

        [SerializeField]
        [Range(0f, 8f)]
        private float emission = 1.5f;

        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Material _material;

        private void OnEnable()
        {
            if (volume == null)
            {
                Debug.LogWarning($"NebulaField on '{name}' has no baked volume, so there is nothing to march.", this);
                enabled = false;
                return;
            }

            var shader = Shader.Find("CosmicSimulation/NebulaField");
            if (shader == null)
            {
                Debug.LogError("NebulaField: shader 'CosmicSimulation/NebulaField' not found.", this);
                enabled = false;
                return;
            }

            _filter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            if (_filter.sharedMesh == null) _filter.sharedMesh = Box();

            _renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // Instanced so the per-frame numbers can never be written into the shared asset and land in a commit.
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _renderer.sharedMaterial = _material;
            _renderer.enabled = true;

            Push();
        }

        private void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
            if (_material != null) DestroyImmediate(_material);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _material != null) Push();
        }

        private void Push()
        {
            _material.SetTexture(VolumeId, volume);

            // The mesh is a unit box, so the transform carries the size and the shader is told the half-width
            // it should march in its own space - which is 0.5 for a unit box, whatever metres that becomes.
            transform.localScale = Vector3.one * (radiusMetres * 2f);
            _material.SetFloat(RadiusId, 0.5f);
            _material.SetFloat(StepsId, steps);
            _material.SetFloat(DensityId, density);
            _material.SetFloat(EmissionId, emission);
        }

        /// <summary>A unit cube. Built rather than referenced so the component needs no prefab wiring.</summary>
        private static Mesh Box()
        {
            var mesh = new Mesh { name = "nebula_field_box" };
            var h = 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(-h, h, -h),
                new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h),
            };

            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                1, 6, 5, 1, 2, 6,
                5, 7, 4, 5, 6, 7,
                4, 3, 0, 4, 7, 3,
                3, 6, 2, 3, 7, 6,
                4, 1, 5, 4, 0, 1,
            };

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
