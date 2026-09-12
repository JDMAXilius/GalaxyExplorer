// Licensed under the MIT License. See LICENSE in the project root for license information.

// One layer of a surrounding place shell (see PlaceShell.cs).
//
// A destination shell is two inward-facing spheres drawn with this one shader under two materials:
//
//   * the SKY  -- a large sphere (about 60 m) whose centre follows the player's head, so it never moves
//                 relative to them and reads as the far distance. Opaque: its base colour has alpha 1, which
//                 replaces the room entirely. It carries the destination's plate as a dome-shaped patch plus
//                 a procedural star field.
//   * the GAS  -- a much smaller sphere (about 7 m) pinned to a fixed point in the room. Transparent, alpha
//                 taken from a luminance band of the same plate at a different zoom, turning slowly. Because
//                 it does NOT follow the head, leaning and walking move it against the sky, and that parallax
//                 is the only thing that makes a pair of spheres read as a volume you are inside rather than
//                 a painted ball. Everything else here is in service of that one cue.
//
// WHERE THE DEPTH COMES FROM. The plates are ordinary photographs: three colour channels, no alpha, black sky
// around the subject. The precedent for getting structure out of that is CosmicSimulation/NebulaCard, which
// takes a *luminance band* per card so the faint outer wisps and the bright core land on different planes.
// The same window is here, as _BandLow/_BandHigh/_BandFeather, weighted by _BandWeight:
//
//   * the sky material sets _BandWeight 0 -- it wants the whole photograph, because it is the backdrop;
//   * the gas material sets _BandWeight 1 -- it wants one band only, so the black sky of the photograph is
//     never drawn at all and the layer is a torn sheet of nebulosity rather than a second opaque ball.
//
// HOW THE PLATE IS PROJECTED. The sphere mesh PlaceShell generates carries proper equirectangular UVs
// (u = azimuth, 0.5 at local +Z; v = elevation, 0.5 at the horizon) with a duplicated seam column, so the
// mapping costs nothing in the fragment shader -- no atan2, no asin, no seam. _SpreadU/_SpreadV then decide how
// much sky one plate covers: 1.0 is a full 360x180 wrap, and the default 0.42 puts the plate in a patch about
// 150 degrees wide centred on local +Z, fading out through _EdgeFeather into the base colour. The dome is the
// default deliberately. One landscape photograph stretched over an entire sphere is visibly a stretched
// photograph, with pinched poles; a wide patch over a dark tinted void is both cheaper to look at and honest
// about what we actually have.
//
// THE STARS are computed, not textured. The direction is projected onto the dominant cube face, cut into
// cells, and each cell either holds a jittered point or does not, from a hash of the cell. A cube face avoids
// the clumping a lat/long grid gets at the poles, one cell lookup (not nine) keeps it to about a dozen ALU, and
// the jitter is held inside the middle 60% of its cell so a star can never be clipped in half by a cell edge.
//
// COST, and why it is shaped this way. An inward sphere is close to full-screen overdraw by definition, and a
// Quest 3 is fill-bound, so the whole design is about keeping the number of full-screen layers down:
//   * ONE pass, no second pass, no depth texture, no grab pass. Two draw calls for the whole shell.
//   * One tex2D per fragment. The band, the patch mask and the star field are all ALU.
//   * The gas material sets _AlphaClip, so the large majority of its fragments -- everything the luminance
//     band rejects -- are discarded before the blend rather than blended at alpha zero. On a tiler that skips
//     the framebuffer read-modify-write, which is the expensive half.
//   * ZWrite is off on both and the sphere is far enough out that nothing occludes it, so there is no depth
//     work at all.
// See PlaceShell.cs for the measured budget statement.
//
// STEREO. This is the one shader in the project where a missing macro is unmissable: a shell fills the eye, so
// an eye that samples the wrong slice or gets the wrong view matrix is a broken headset, not a subtle artefact.
// Android builds single-pass instanced, so the full set is here and matches nebula_card_shader.shader exactly:
// multi_compile_instancing, UNITY_VERTEX_INPUT_INSTANCE_ID on both structs, UNITY_VERTEX_OUTPUT_STEREO on the
// interpolator, and SETUP/TRANSFER/INITIALIZE in the vertex stage with SETUP_INSTANCE_ID and
// SETUP_STEREO_EYE_INDEX_POST_VERTEX in the fragment stage. There is nothing here that reads a screen-space
// texture, which removes the other half of the usual stereo trouble.
//
// PASSTHROUGH. Colour blends as ordinary alpha but the alpha channel accumulates premultiplied
// (One OneMinusSrcAlpha), the same as NebulaCard: the compositor uses the eye buffer's alpha to decide how much
// room shows through, and plain SrcAlpha blending on that channel would square it. In practice a shell asks for
// EnvironmentMode.FullBlack and there is no room behind it, but the blend is correct either way.
Shader "CosmicSimulation/PlaceShell"
{
    Properties
    {
        _MainTex ("Plate", 2D) = "black" {}
        _Color ("Tint and layer opacity", Color) = (1, 1, 1, 1)
        _BaseColor ("Void colour (alpha 1 makes this layer the sky)", Color) = (0.012, 0.014, 0.024, 1)

        [Header(Plate placement)]
        _SpreadU ("Azimuth spread (1 = full wrap)", Range(0.05, 1)) = 0.42
        _SpreadV ("Elevation spread (1 = pole to pole)", Range(0.05, 1)) = 0.28
        _PlateYaw ("Plate yaw (deg)", Range(-180, 180)) = 0
        _PlatePitch ("Plate pitch (deg)", Range(-90, 90)) = 0
        _EdgeFeather ("Patch edge feather", Range(0.01, 1)) = 0.45
        _PlateGain ("Plate brightness", Range(0, 4)) = 1
        _PlateOpacity ("Plate opacity", Range(0, 1)) = 1

        [Header(Luminance band)]
        _BandWeight ("Band the plate (0 = whole photo, 1 = one band)", Range(0, 1)) = 0
        _BandLow ("Band low", Range(0, 2)) = 0.06
        _BandHigh ("Band high", Range(0, 2)) = 2
        _BandFeather ("Band feather", Range(0.001, 0.5)) = 0.12

        [Header(Star field)]
        _StarColor ("Star tint", Color) = (0.85, 0.9, 1, 1)
        _StarDensity ("Cells per cube face", Range(4, 256)) = 64
        _StarSize ("Star size (cell fraction)", Range(0.01, 0.5)) = 0.09
        _StarFill ("Fraction of cells holding a star", Range(0, 1)) = 0.12
        _StarBrightness ("Star brightness", Range(0, 4)) = 1
        _StarWarmth ("Warm to cool spread", Range(0, 1)) = 0.35

        [Header(Runtime)]
        _Fade ("Fade (driven by PlaceShell)", Range(0, 1)) = 1
        _AlphaClip ("Discard below this alpha", Range(0, 0.2)) = 0
    }

    SubShader
    {
        // Queue is written per material by PlaceShellBuilder, not taken from here: the sky goes to 1900 (behind
        // everything) and the gas to 2900 (over the sky, under the nebula cards at 3000 and under the dim quad
        // and world-space UI above that). 2900 is also >= 2500, so the gas sorts back to front like any other
        // transparent object rather than front to back.
        Tags { "Queue" = "Background" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Skybox" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        // The mesh is a normal outward-wound sphere -- the same one any other code could reuse -- and we are
        // standing inside it, so the inner surface is the back faces. Culling the front ones is what turns a
        // ball into a room, and it costs nothing.
        Cull Front

        ZWrite Off
        ZTest LEqual
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;     // plate UV, already placed and spread
                float2 patch : TEXCOORD1;  // the same UV before tiling, kept for the patch mask
                float3 dir : TEXCOORD2;    // object-space direction, for the star field
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _BaseColor;

            float _SpreadU;
            float _SpreadV;
            float _PlateYaw;
            float _PlatePitch;
            float _EdgeFeather;
            float _PlateGain;
            float _PlateOpacity;

            float _BandWeight;
            float _BandLow;
            float _BandHigh;
            float _BandFeather;

            fixed4 _StarColor;
            float _StarDensity;
            float _StarSize;
            float _StarFill;
            float _StarBrightness;
            float _StarWarmth;

            float _Fade;
            float _AlphaClip;

            // Two decorrelated values from a cell index. sin/frac is exact enough for a star field and is one
            // of the cheapest hashes on an Adreno; the star positions only have to be stable, not uniform.
            float2 ShellHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;

                // The mesh UV is a true equirectangular map of the sphere, so placing the plate is a scale and
                // an offset about the patch centre -- no trigonometry, and no seam, because the mesh duplicates
                // its seam column. Done per vertex because it is affine in UV and therefore interpolates exactly.
                float2 centre = float2(0.5 + _PlateYaw / 360.0, 0.5 + _PlatePitch / 180.0);
                float2 placed = (v.uv - centre) / max(float2(_SpreadU, _SpreadV), 1e-4) + 0.5;

                o.patch = placed;
                o.uv = placed * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // ---- the patch the plate is allowed to occupy.
                // Outside the plate's own 0..1 the smoothstep has already reached 1, so the mask is exactly
                // zero there and the clamped edge texels are never smeared across the rest of the sky.
                float2 c = abs(i.patch - 0.5) * 2.0;
                float edge = max(c.x, c.y);
                float patch = 1.0 - smoothstep(saturate(1.0 - _EdgeFeather), 1.0, edge);

                // ---- the plate, and the luminance band taken out of it
                fixed4 plate = tex2D(_MainTex, i.uv);
                float luma = dot(plate.rgb, float3(0.299, 0.587, 0.114));
                float window = smoothstep(_BandLow - _BandFeather, _BandLow + _BandFeather, luma) *
                               (1.0 - smoothstep(_BandHigh - _BandFeather, _BandHigh + _BandFeather, luma));
                float plateA = lerp(1.0, window, _BandWeight) * patch * _PlateOpacity;

                // ---- the star field: one cell on the dominant cube face
                float3 d = normalize(i.dir);
                float3 a = abs(d);
                float3 f;
                float face;
                if (a.x >= a.y && a.x >= a.z)
                {
                    f = float3(d.y, d.z, a.x);
                    face = d.x > 0.0 ? 0.0 : 1.0;
                }
                else if (a.y >= a.z)
                {
                    f = float3(d.z, d.x, a.y);
                    face = d.y > 0.0 ? 2.0 : 3.0;
                }
                else
                {
                    f = float3(d.x, d.y, a.z);
                    face = d.z > 0.0 ? 4.0 : 5.0;
                }

                float2 p = (f.xy / max(f.z, 1e-5)) * _StarDensity + face * 37.0;
                float2 cell = floor(p);
                float2 local = p - cell;

                float2 jitter = ShellHash(cell);
                float2 pick = ShellHash(cell + 11.37);

                // Kept inside the middle 60% of the cell, so a star of radius _StarSize (a small fraction of a
                // cell) can never be cut in half by the cell boundary we chose not to pay nine lookups to avoid.
                float2 starCentre = 0.5 + (jitter - 0.5) * 0.6;
                float keep = step(1.0 - _StarFill, pick.x);
                float magnitude = pick.y;
                float radius = _StarSize * (0.45 + 0.55 * magnitude);
                float star = (1.0 - smoothstep(radius * 0.25, radius, length(local - starCentre))) * keep * magnitude;

                float3 starRgb = star * _StarBrightness *
                                 lerp(_StarColor.rgb, _StarColor.rgb * float3(1.25, 1.0, 0.72),
                                      saturate((jitter.x - 0.5) * 2.0) * _StarWarmth);

                // ---- compose
                float3 rgb = _BaseColor.rgb + plate.rgb * _PlateGain * plateA + starRgb;
                float alpha = saturate(_BaseColor.a + plateA + star);

                rgb *= _Color.rgb;
                alpha *= _Color.a * _Fade;

                // Zero on the sky material (its base alpha is 1, so nothing is ever discarded); a small value on
                // the gas, where most of the sphere is band-rejected and would otherwise blend at alpha zero.
                clip(alpha - _AlphaClip);

                return fixed4(rgb, saturate(alpha));
            }
            ENDCG
        }
    }

    Fallback Off
}
