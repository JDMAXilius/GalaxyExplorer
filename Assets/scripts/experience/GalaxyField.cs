// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicSimulation
{
    /// <summary>
    /// One rectangle cut out of a deep-field plate: a single real galaxy, framed.
    ///
    /// <para>Everything in it is measured, not designed. <see cref="UvRect"/> is where
    /// <c>GalaxyFieldBuilder</c> found a compact, isolated source in the plate; <see cref="Floor"/> is the sky
    /// level it measured around that source, which the shader subtracts so the cutout does not add a grey
    /// square to the view; <see cref="Gain"/> normalises a faint cutout against a bright one so the field does
    /// not consist of three visible galaxies and four hundred smudges.</para>
    /// </summary>
    [System.Serializable]
    public struct GalaxyCutout
    {
        /// <summary>Index into <c>GalaxyField.plateMaterials</c>: which deep-field plate this came out of.</summary>
        public int Plate;

        /// <summary>The sub-rectangle of that plate, in 0-1 UV.</summary>
        public Rect UvRect;

        /// <summary>Width over height of the cutout in pixels, so a non-square plate does not stretch it.</summary>
        public float Aspect;

        /// <summary>The sky level measured around the source, 0-1, subtracted before the additive blend.</summary>
        public float Floor;

        /// <summary>Brightness normalisation for this cutout, around 1.</summary>
        public float Gain;
    }

    /// <summary>
    /// The Galaxies deep field (GDD 4.6, F-25, CS-063/CS-064): a few hundred real galaxies scattered through a
    /// shell around the player, seen at a distance, drifting slowly.
    ///
    /// <para><b>Why this is not a point cloud.</b> <c>DrawStars</c> — the renderer behind the Milky Way,
    /// Andromeda and the Cosmic Web — is a hero renderer and the rendering research is blunt about it
    /// (docs/research/scale_and_rendering.md 3.2-3.4): below about 20 cm of apparent width a point-cloud
    /// galaxy is drawing more than one sub-pixel additive quad per covered pixel and is strictly worse than a
    /// single billboard, for roughly a hundred times the triangle setup. Every galaxy here is 3-25 cm. So the
    /// field is billboards, and the budget is trivial: a few hundred quads is a thousand triangles and two
    /// draw calls, against a frame ceiling of 400,000 point sprites (Technical Overview 7.4).</para>
    ///
    /// <para><b>Why one mesh per plate rather than <c>Graphics.DrawMeshInstanced</c>.</b> Both are one draw
    /// call at this count. A plain <c>MeshRenderer</c> wins on everything else: it is the path multiview is
    /// most thoroughly exercised on (the whole field is stereo-correct or the whole app is), it is culled and
    /// sorted by the ordinary renderer, it follows the transform for free so the director's grow-in and the
    /// player's two-handed scale need no per-frame matrix rebuild, and it costs zero CPU per frame. The
    /// per-instance data an instanced draw would put in a constant buffer simply rides in the vertex streams
    /// instead; <c>galaxy_sprite_shader</c> documents the layout.</para>
    ///
    /// <para><b>The geometry is built at run time, not baked.</b> Same reasoning as
    /// <see cref="CosmicWebRenderer"/>: the arrangement is deterministic from <see cref="seed"/>, so a
    /// run-time build is exactly as reproducible as an asset and costs nothing to store. What <i>is</i> baked,
    /// because it is the expensive and genuinely authored part, is the cutout table: the builder scans the
    /// plates for galaxies and writes the winning sub-rectangles into <see cref="cutouts"/>.</para>
    ///
    /// <para><b>The shell is centred on the player, not on the content root.</b> The director spawns a
    /// module's content about two metres in front of the player, which is right for a galaxy on a table and
    /// wrong for a universe you are standing in the middle of — it would put some galaxies a hand's width from
    /// the face and the rest five metres behind. So a child node carries an offset, measured once when the
    /// experience opens, that puts the centre of the shell where the player's head is. The root itself never
    /// moves, so <c>FreePlacementAnchor</c>'s home pose, the grab sphere and Restore all behave exactly as
    /// they do for every other module.</para>
    ///
    /// <para><b>Nothing here writes to the transform.</b> The drift is a shader uniform, so it can never
    /// fight a two-handed grab.</para>
    /// </summary>
    [DefaultExecutionOrder(55)]
    public class GalaxyField : MonoBehaviour
    {
        private const string ShellName = "shell";
        private const string MeshNamePrefix = "plate_";

        /// <summary>One call per plate, so the whole field is two draw calls. Far below any sane cap.</summary>
        private const int MaxSprites = 4000;

        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int TransitionAlphaId = Shader.PropertyToID("_TransitionAlpha");

        [Header("Source")]
        [SerializeField]
        [Tooltip("One material per deep-field plate, each using CosmicSimulation/GalaxySprite. Written by " +
                 "Cosmic Simulation -> Build Galaxies Content.")]
        private Material[] plateMaterials = new Material[0];

        [SerializeField]
        [Tooltip("Sub-rectangles of those plates, each framing one real galaxy. Found by the builder, not " +
                 "authored by hand.")]
        private GalaxyCutout[] cutouts = new GalaxyCutout[0];

        [Header("Field")]
        [SerializeField]
        [Tooltip("Anything makes the same field again. Change it for a different arrangement of the same galaxies.")]
        private int seed = 20260912;

        [SerializeField]
        [Tooltip("Galaxies in the whole field. GDD 4.6 asks for 300-500.")]
        private int spriteCount = 420;

        [SerializeField]
        [Tooltip("Nearest a galaxy sits to the centre of the shell, in metres at local scale 1.")]
        private float shellInnerRadius = 2.2f;

        [SerializeField]
        [Tooltip("Furthest a galaxy sits from the centre of the shell. GDD 4.6 asks for a 6 m sphere, so 3 m.")]
        private float shellOuterRadius = 3f;

        [SerializeField]
        [Tooltip("How far a galaxy may be nudged off its even-coverage direction, as a fraction of the shell. " +
                 "Zero is a visible lattice; one is clumpy.")]
        [Range(0f, 1f)]
        private float directionJitter = 0.32f;

        [Header("Sprites")]
        [SerializeField]
        [Tooltip("Smallest galaxy, in metres of width at local scale 1. GDD 4.6: 3 cm.")]
        private float minSpriteMetres = 0.03f;

        [SerializeField]
        [Tooltip("Largest galaxy, in metres of width at local scale 1. GDD 4.6: 25 cm.")]
        private float maxSpriteMetres = 0.25f;

        [SerializeField]
        [Tooltip("Above 1 makes small galaxies common and large ones rare, which is what a real field looks " +
                 "like and also what keeps the fill cost down.")]
        [Range(1f, 5f)]
        private float sizeBias = 2.4f;

        [SerializeField]
        [Tooltip("How far the warm/cool tint may stray from white. The plates already carry the galaxies' own " +
                 "colour, so this is a nudge, not a recolour.")]
        [Range(0f, 1f)]
        private float tintSpread = 0.35f;

        [SerializeField]
        [Tooltip("Dimmest and brightest a galaxy may be drawn, before its cutout's own gain.")]
        private Vector2 brightnessRange = new Vector2(0.55f, 1.2f);

        [Header("Drift")]
        [SerializeField]
        [Tooltip("Degrees per second the field turns about its vertical axis. GDD 4.6 asks for about 1.")]
        private float driftDegreesPerSecond = 1f;

        [SerializeField]
        [Tooltip("How much individual galaxies differ from that rate. A little shear is what makes a shell of " +
                 "billboards read as having depth instead of as one turning wall.")]
        [Range(0f, 0.6f)]
        private float driftSpread = 0.18f;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Put the centre of the shell where the player's head is when the experience opens, rather " +
                 "than at the content root the director spawns this on, which sits about 2 m in front of them.")]
        private bool centreOnViewer = true;

        [SerializeField]
        [Tooltip("Furthest the shell centre may be moved from the content root, in metres. A guard: a stale or " +
                 "mis-parented camera must not fling the field across the room.")]
        private float maxViewerOffsetMetres = 3f;

        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private MaterialPropertyBlock _block;
        private Transform _shell;
        private float _age;
        private bool _built;

        /// <summary>How many galaxy sprites are actually in the field.</summary>
        public int SpriteCount { get; private set; }

        /// <summary>How many distinct cutouts the field is drawing from.</summary>
        public int CutoutCount => cutouts == null ? 0 : cutouts.Length;

        /// <summary>
        /// True when the shell is put around the player's head. The director reads this to leave such content
        /// where it put itself, and to keep the place's panel inside the shell rather than beyond it.
        /// </summary>
        public bool CentresOnViewer => centreOnViewer;

        /// <summary>Where the middle of the shell is in the world: the player's head when the place opened.</summary>
        public Vector3 ShellCentre => _shell != null ? _shell.position : transform.position;

        /// <summary>Half-width of the field in metres at local scale 1, for anything that has to size itself to it.</summary>
        public float ShellRadiusMetres => shellOuterRadius;

        /// <summary>Fades the whole field out without touching its scale. 1 is fully lit.</summary>
        public float TransitionAlpha { get; set; } = 1f;

        /// <summary>Throws the field away and builds a new one from the serialized settings.</summary>
        public void Rebuild()
        {
            Clear();
            Build();
        }

        /// <summary>
        /// Moves the shell so its centre sits on the main camera again. Not called on its own: the field is
        /// placed once when it opens, because a shell that chased the head would turn walking into sliding.
        /// Nothing calls it yet; Recenter does not, so in a headset the shell stays where it was first placed.
        /// </summary>
        public void RecentreOnViewer()
        {
            if (_shell != null)
            {
                _shell.localPosition = ViewerOffset();
                FollowShell();
            }
        }

        // ---------- lifecycle

        private void OnEnable()
        {
            // Awake has not necessarily run when something adds this component and uses it the same frame, so
            // the build happens from wherever it is first needed rather than from one entry point.
            Build();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Update()
        {
            if (!_built)
            {
                return;
            }

            // Radians, wrapped, so a long session cannot walk the float off into visible stepping.
            _age = Mathf.Repeat(_age + Time.deltaTime * driftDegreesPerSecond * Mathf.Deg2Rad, Mathf.PI * 2f);

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            // A property block rather than a material instance: these two values change every frame, and
            // writing them into the shared material asset is exactly the runtime leak the working rules in
            // CLAUDE.md forbid committing.
            _block.Clear();
            _block.SetFloat(AgeId, _age);
            _block.SetFloat(TransitionAlphaId, TransitionAlpha);

            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].SetPropertyBlock(_block);
                }
            }
        }

        // ---------- building

        private void Build()
        {
            if (_built)
            {
                return;
            }

            if (cutouts == null || cutouts.Length == 0)
            {
                Debug.LogError(
                    $"GalaxyField on '{name}': no galaxy cutouts, so there is nothing to draw. Run " +
                    "Cosmic Simulation -> Build Galaxies Content, which scans the deep-field plates and " +
                    "writes them here.");
                return;
            }

            if (plateMaterials == null || plateMaterials.Length == 0)
            {
                Debug.LogError(
                    $"GalaxyField on '{name}': no plate materials. Run Cosmic Simulation -> Build Galaxies " +
                    "Content, which creates them and assigns them here.");
                return;
            }

            var shell = new GameObject(ShellName);
            shell.transform.SetParent(transform, false);
            shell.transform.localPosition = ViewerOffset();
            _shell = shell.transform;
            FollowShell();

            var placed = Place();
            SpriteCount = placed.Count;

            for (var plate = 0; plate < plateMaterials.Length; plate++)
            {
                var material = plateMaterials[plate];
                if (material == null)
                {
                    continue;
                }

                var mesh = BuildMesh(placed, plate);
                if (mesh == null)
                {
                    continue;
                }

                var child = new GameObject(MeshNamePrefix + plate);
                child.transform.SetParent(_shell, false);

                child.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;

                _meshes.Add(mesh);
                _renderers.Add(renderer);
            }

            _built = true;

            if (_renderers.Count == 0)
            {
                Debug.LogWarning(
                    $"GalaxyField on '{name}': {placed.Count} galaxies were placed but none of them landed on " +
                    "a plate with a material, so nothing is drawn. Check the plate materials on this component.");
            }
        }

        private void Clear()
        {
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                {
                    DestroySafely(_renderers[i].gameObject);
                }
            }

            _renderers.Clear();

            for (var i = 0; i < _meshes.Count; i++)
            {
                if (_meshes[i] != null)
                {
                    DestroySafely(_meshes[i]);
                }
            }

            _meshes.Clear();

            if (_shell != null)
            {
                DestroySafely(_shell.gameObject);
                _shell = null;
            }

            SpriteCount = 0;
            _built = false;
        }

        private static void DestroySafely(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        /// <summary>
        /// The named-galaxy pins are built around their node's origin at just inside the shell's radius, so the
        /// node goes wherever the shell's centre goes. Without this, once the shell sits on the player's head
        /// the pins would hang two metres off it (CS-199).
        /// </summary>
        private void FollowShell()
        {
            foreach (var pins in GetComponentsInChildren<GalaxyPins>(true))
            {
                if (pins.transform.parent == transform)
                {
                    pins.transform.localPosition = _shell.localPosition;
                }
            }
        }

        /// <summary>
        /// Where to put the centre of the shell, in this transform's local space. Zero — the content root
        /// itself — when there is no camera to measure against or the offset is asked to be absurd.
        /// </summary>
        private Vector3 ViewerOffset()
        {
            if (!centreOnViewer)
            {
                return Vector3.zero;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return Vector3.zero;
            }

            var local = transform.InverseTransformPoint(camera.transform.position);
            var limit = Mathf.Max(0f, maxViewerOffsetMetres);
            return local.magnitude > limit ? local.normalized * limit : local;
        }

        // ---------- placement

        private struct Placed
        {
            public Vector3 Centre;
            public float HalfX;
            public float HalfY;
            public float Roll;
            public float Rate;
            public float Brightness;
            public Color Tint;
            public int Cutout;
        }

        /// <summary>
        /// Scatters the galaxies through the shell.
        ///
        /// Directions come off a golden-angle spiral, which covers a sphere evenly without the pole clumping a
        /// naive two-random-angles scheme produces, and are then jittered so the even coverage does not read as
        /// a lattice. Radius, size, roll, tint, brightness and drift rate are all independent draws: the point
        /// is that no two galaxies in the field look placed.
        /// </summary>
        private List<Placed> Place()
        {
            var count = Mathf.Clamp(spriteCount, 0, MaxSprites);
            var result = new List<Placed>(count);
            if (count == 0 || cutouts.Length == 0)
            {
                return result;
            }

            var rng = new Rng(seed);

            var inner = Mathf.Max(0.01f, Mathf.Min(shellInnerRadius, shellOuterRadius));
            var outer = Mathf.Max(inner + 0.01f, shellOuterRadius);
            var smallest = Mathf.Max(0.001f, Mathf.Min(minSpriteMetres, maxSpriteMetres));
            var largest = Mathf.Max(smallest, maxSpriteMetres);

            // Every cutout gets used about equally often: a shuffled round robin, reshuffled each time it wraps.
            var order = new int[cutouts.Length];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (var i = 0; i < count; i++)
            {
                if (i % order.Length == 0)
                {
                    Shuffle(order, ref rng);
                }

                var cutoutIndex = order[i % order.Length];
                var cutout = cutouts[cutoutIndex];

                var y = 1f - (i + 0.5f) * 2f / count;
                var ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var theta = goldenAngle * i;
                var direction = new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);
                direction = (direction + InsideUnitSphere(ref rng) * directionJitter).normalized;
                if (direction.sqrMagnitude < 1e-6f)
                {
                    direction = Vector3.forward;
                }

                var radius = Mathf.Lerp(inner, outer, rng.Next01());

                // Biased small: a power above 1 pushes the draw toward zero, and zero is the smallest galaxy.
                var width = Mathf.Lerp(smallest, largest, Mathf.Pow(rng.Next01(), Mathf.Max(1f, sizeBias)));
                var aspect = cutout.Aspect > 0.01f ? cutout.Aspect : 1f;
                var halfX = 0.5f * width * (aspect >= 1f ? 1f : aspect);
                var halfY = 0.5f * width * (aspect >= 1f ? 1f / aspect : 1f);

                // Cool to warm, centred on white. The plates carry the real colour; this only stops four
                // hundred copies of thirty images from reading as four hundred identical stamps.
                var warm = (rng.Next01() - 0.5f) * 2f * Mathf.Clamp01(tintSpread);
                var tint = new Color(
                    1f + 0.20f * warm,
                    1f + 0.02f * warm,
                    1f - 0.22f * warm,
                    1f);

                var gain = cutout.Gain > 0.01f ? cutout.Gain : 1f;

                result.Add(new Placed
                {
                    Centre = direction * radius,
                    HalfX = halfX,
                    HalfY = halfY,
                    Roll = rng.Next01() * Mathf.PI * 2f,
                    Rate = Mathf.Lerp(1f - driftSpread, 1f + driftSpread, rng.Next01()),
                    Brightness = Mathf.Lerp(brightnessRange.x, brightnessRange.y, rng.Next01()) * gain,
                    Tint = tint,
                    Cutout = cutoutIndex,
                });
            }

            return result;
        }

        /// <summary>
        /// One mesh holding every galaxy that came out of one plate, four vertices and two triangles each.
        /// Returns null when this plate has no galaxies in the field.
        /// </summary>
        private Mesh BuildMesh(List<Placed> placed, int plate)
        {
            var quads = 0;
            for (var i = 0; i < placed.Count; i++)
            {
                if (cutouts[placed[i].Cutout].Plate == plate)
                {
                    quads++;
                }
            }

            if (quads == 0)
            {
                return null;
            }

            var vertices = new List<Vector3>(quads * 4);
            var uv0 = new List<Vector2>(quads * 4);
            var uv1 = new List<Vector4>(quads * 4);
            var uv2 = new List<Vector4>(quads * 4);
            var colors = new List<Color>(quads * 4);
            var triangles = new List<int>(quads * 6);

            for (var i = 0; i < placed.Count; i++)
            {
                var sprite = placed[i];
                var cutout = cutouts[sprite.Cutout];
                if (cutout.Plate != plate)
                {
                    continue;
                }

                var rect = cutout.UvRect;
                var sin = Mathf.Sin(sprite.Roll);
                var cos = Mathf.Cos(sprite.Roll);
                var extras = new Vector4(Mathf.Clamp01(cutout.Floor), sprite.Rate, sprite.Brightness, 0f);
                var baseIndex = vertices.Count;

                for (var corner = 0; corner < 4; corner++)
                {
                    var cx = (corner & 1) == 0 ? -1f : 1f;
                    var cy = (corner & 2) == 0 ? -1f : 1f;

                    var ox = cx * sprite.HalfX;
                    var oy = cy * sprite.HalfY;

                    vertices.Add(sprite.Centre);
                    uv0.Add(new Vector2(
                        rect.xMin + (cx * 0.5f + 0.5f) * rect.width,
                        rect.yMin + (cy * 0.5f + 0.5f) * rect.height));
                    // xy: the rolled offset the shader expands the quad by. zw: the unrolled corner, which is
                    // what the radial fade measures, so the fade stays a disc however the sprite is rolled.
                    uv1.Add(new Vector4(ox * cos - oy * sin, ox * sin + oy * cos, cx, cy));
                    uv2.Add(extras);
                    colors.Add(sprite.Tint);
                }

                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
            }

            var mesh = new Mesh { name = $"galaxy_field_plate_{plate}" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);

            // Stated rather than recalculated. Every vertex holds a sprite's *centre*, so recalculated bounds
            // would be the shell and would miss both the quad the shader expands around each centre and the
            // drift that turns the whole field — and the field would then pop out of view at the edges of a
            // glance. A sphere the size of the shell plus the largest sprite can never be wrong.
            var reach = Mathf.Max(shellOuterRadius, shellInnerRadius) + Mathf.Max(maxSpriteMetres, minSpriteMetres);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (reach * 2f));

            mesh.UploadMeshData(false);
            return mesh;
        }

        // ---------- deterministic randomness
        //
        // Its own generator rather than UnityEngine.Random: that one is global state, so seeding it here would
        // silently change every other random draw in the frame, and anything else drawing from it would change
        // this field. Xorshift32, which is more than enough for scattering billboards.

        private struct Rng
        {
            private uint _state;

            public Rng(int seed)
            {
                _state = (uint)seed;
                if (_state == 0u)
                {
                    _state = 2463534242u;
                }
            }

            public uint NextUInt()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }

            /// <summary>0 inclusive, 1 exclusive.</summary>
            public float Next01()
            {
                return (NextUInt() & 0xFFFFFFu) / 16777216f;
            }
        }

        private static void Shuffle(int[] values, ref Rng rng)
        {
            for (var i = values.Length - 1; i > 0; i--)
            {
                var j = (int)(rng.NextUInt() % (uint)(i + 1));
                var swap = values[i];
                values[i] = values[j];
                values[j] = swap;
            }
        }

        private static Vector3 InsideUnitSphere(ref Rng rng)
        {
            // Rejection sampling, capped: uniform inside the sphere, and it cannot spin forever on a
            // pathological generator.
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var v = new Vector3(
                    rng.Next01() * 2f - 1f,
                    rng.Next01() * 2f - 1f,
                    rng.Next01() * 2f - 1f);
                if (v.sqrMagnitude <= 1f)
                {
                    return v;
                }
            }

            return Vector3.zero;
        }
    }
}
