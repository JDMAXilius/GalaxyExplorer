// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// One nebula's cloud of gas, baked from its plate by <c>NebulaVolumeBuilder</c> and drawn by
    /// <see cref="NebulaVolume"/>.
    ///
    /// <para>The points reuse <see cref="StarVertDescriptor"/> rather than declaring a fourth point struct,
    /// because the whole additive-sprite path - the compute buffer stride, <c>StarQuad.cginc</c>, the six
    /// vertices per point - is shared with the galaxies and the Cosmic Web, and a new struct would fork all of
    /// it to save nothing. The fields are reinterpreted, and both halves of that reinterpretation are written
    /// down: here, and at the top of <c>nebula_volume_shader.shader</c>.</para>
    ///
    /// <list type="table">
    /// <item><term>ellipseDistance</term><description>distance from the volume's vertical axis, metres</description></item>
    /// <item><term>curveOffset</term><description>angle about that axis, radians</description></item>
    /// <item><term>yOffset</term><description>height on that axis, metres</description></item>
    /// <item><term>ellipseOffset</term><description>where the point sits front to back, 0 far, 1 near</description></item>
    /// <item><term>color</term><description>the plate's colour here, already weighted by this point's share of the light</description></item>
    /// <item><term>size</term><description>per-point sprite size multiplier</description></item>
    /// <item><term>random</term><description>shimmer phase</description></item>
    /// <item><term>uv</term><description>which cell of the point sprite atlas to draw</description></item>
    /// </list>
    ///
    /// <para><see cref="Provenance"/> is not decoration. D-010 requires that a modelled appearance says it is
    /// modelled, and the depth of a nebula is the most modelled thing in this project: the plate gives two
    /// dimensions and the third is reconstructed. The string records which model produced it so that the claim
    /// in the panel copy and the geometry on screen cannot drift apart without somebody noticing.</para>
    /// </summary>
    public class NebulaVolumeData : ScriptableObject
    {
        [Tooltip("The baked cloud. See the class note for what each field of the shared struct means here.")]
        public StarVertDescriptor[] points;

        [Tooltip("Which destination this belongs to.")]
        public string destinationId;

        [Tooltip("The plate the colours and structure were read from.")]
        public string plateAssetPath;

        [Tooltip("How the third dimension was reconstructed, in words, for CREDITS.md and the panel copy.")]
        [TextArea(2, 6)]
        public string Provenance;

        [Tooltip("Widest extent of the cloud in metres, for the grab collider and the scale limits.")]
        public float radiusMetres;
    }
}
