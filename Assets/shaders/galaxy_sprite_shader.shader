// Licensed under the MIT License. See LICENSE in the project root for license information.

// One galaxy in the Galaxies deep field (GDD 4.6, CS-063/CS-064).
//
// The whole field is two meshes — one per source plate — of a few hundred camera-facing quads each, built at
// run time by GalaxyField. There is no instancing here and no geometry shader: the quads are ordinary mesh
// triangles, so the field costs exactly one draw call per plate and goes down the same well-trodden multiview
// path as every other MeshRenderer on the device. Per-sprite data rides in the vertex streams.
//
// **What a sprite actually is.** Not an artist's galaxy. Each quad shows a rectangle cut straight out of the
// Hubble Ultra Deep Field or Webb's First Deep Field — real photographs of real galaxies — picked by
// GalaxyFieldBuilder, which scans the plate for compact, isolated sources and bakes the winning sub-rectangles
// as UV rects. Nothing is redrawn, recoloured beyond a faint warm/cool tint, or invented. That is the whole
// reason this approach was chosen over generated art (docs/decisions.md D-010).
//
// **Why additive.** The plates are astronomical images: galaxies on a black sky, no alpha channel. Under
// Blend One One the black sky between the galaxies in a cutout adds nothing and is therefore transparent for
// free — no matte, no cutout mask, no new texture asset. Two things still have to be handled, and are:
//
//  * _Floor / the per-sprite floor in TEXCOORD2.x subtracts the cutout's own measured sky level before the
//    add, because JPEG never quite reaches black and the Webb plate has real intracluster glow in it. Without
//    it a cutout would add a faint grey square to the view.
//  * The radial fade kills the corners, so no sprite ever shows its rectangle. Between the two, a cutout reads
//    as a galaxy floating in the dark rather than as a photograph pasted on a card.
//
// **Passthrough.** Additive colour, but the alpha channel has to grow with the light being added or the
// compositor will show the room straight through the brightest galaxies (Technical Overview 7.1). Galaxies is
// a Full Black module by default, but the dock's passthrough toggle can override that at any time and GDD 4.6
// explicitly invites it, so this matters here more than it does for the Cosmic Web.
//
// **Stereo.** Android ships single-pass instanced / multiview. Every macro in the set is present —
// UNITY_VERTEX_INPUT_INSTANCE_ID on both structs, UNITY_VERTEX_OUTPUT_STEREO on the interpolators,
// UNITY_SETUP_INSTANCE_ID in both stages, UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO in the vertex stage and
// UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX in the fragment stage. Leaving any of them out compiles fine and
// then breaks exactly one eye, only on the headset. The clip-space quad expansion below is the same trick
// cginc/StarQuad.cginc uses for the galaxy and the Cosmic Web, and it is correct under the asymmetric
// projections a headset uses: clip.x = P._11 * viewX + ..., so a pure view-space x offset contributes
// P._11 * offset and nothing else.
Shader "CosmicSimulation/GalaxySprite"
{
    Properties
    {
        _MainTex ("Deep-field plate", 2D) = "black" {}
        [HDR] _Color ("Overall tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 4)) = 1.15

        [Header(Cutout cleanup)]
        _Floor ("Extra sky subtract", Range(0, 0.5)) = 0.0
        _RadialStart ("Edge fade start", Range(0, 1)) = 0.35

        [Header(Life)]
        _Age ("Field drift (rad)", Float) = 0
        _TransitionAlpha ("Transition alpha", Float) = 1

        [Header(Near fade)]
        _NearFadeStart ("Near fade start (m)", Float) = 0.25
        _NearFadeRange ("Near fade range (m)", Float) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        LOD 100

        Pass
        {
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // POSITION   the sprite's centre in field-local space. All four corners of a quad carry the same
            //            centre, so the drift rotation below moves the quad without deforming it.
            // TEXCOORD0  this corner's UV, already inside the cutout's sub-rectangle of the plate.
            // TEXCOORD1  xy: this corner's offset from the centre, in metres, already rolled by the sprite's
            //            own random angle. zw: the same corner as an unrolled (+-1, +-1), which is what the
            //            radial fade measures, so the fade is a disc rather than a rolled rectangle.
            // TEXCOORD2  x: the cutout's measured sky level, subtracted before the add.
            //            y: this sprite's share of the collective drift, so the field shears slowly instead of
            //               turning as one rigid ball, which is what makes it read as having depth.
            //            z: this sprite's brightness, the cutout's measured gain included. It lives here and
            //               not in the vertex colour's alpha because Unity's colour channel is unorm8 and
            //               would both clamp it at 1 and quantise it; this channel is a real float.
            // COLOR      rgb: a faint warm/cool tint, never more than a nudge — the plates already carry the
            //            galaxies' own colour and inventing more of it would be a lie.
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 corner : TEXCOORD1;
                float4 params : TEXCOORD2;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 quad : TEXCOORD1;
                // Not COLOR0: some mobile drivers clamp colour interpolators to 0..1, and the gain can push a
                // faint cutout's brightness above 1 on purpose.
                float4 tint : TEXCOORD2;
                float floorLevel : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;

            fixed4 _Color;
            float _Exposure;
            float _Floor;
            float _RadialStart;
            float _Age;
            float _TransitionAlpha;
            float _NearFadeStart;
            float _NearFadeRange;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // The collective drift (GDD 4.6 asks for about 1 deg/s). Done here rather than on the
                // transform for the same reason the Cosmic Web does it here: ManipulationHandler owns the
                // transform, so a drift written to it would fight the player's hands, and the grab shape is a
                // sphere that does not care either way.
                float rate = v.params.y;
                float s, c;
                sincos(_Age * rate, s, c);
                float3 p = v.vertex.xyz;
                p = float3(c * p.x + s * p.z, p.y, c * p.z - s * p.x);

                float4 clipPos = UnityObjectToClipPos(float4(p, 1));

                // The quad is expanded in clip space, so the object matrix never reaches the corner offsets and
                // the field's world scale has to be applied to them by hand. Uniform scale is assumed, which is
                // what ScaleLimits and ManipulationHandler between them guarantee.
                float objectScale = length(unity_ObjectToWorld._m00_m10_m20);
                float2 offset = v.corner.xy * objectScale;
                clipPos.x += offset.x * UNITY_MATRIX_P._11;
                clipPos.y += offset.y * UNITY_MATRIX_P._22;
                o.pos = clipPos;

                o.uv = v.uv;
                o.quad = v.corner.zw;
                o.floorLevel = saturate(v.params.x + _Floor);

                // The player stands inside the shell, so a sprite can end up close to the eye once the field
                // has been scaled down. Fade those rather than let one fill the view. Vertex stage: no depth
                // texture, no prepass, one sprite's worth of arithmetic.
                float3 worldPos = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                float eyeDistance = distance(worldPos, _WorldSpaceCameraPos);
                float nearFade = saturate((eyeDistance - _NearFadeStart) / max(_NearFadeRange, 1e-4));

                o.tint = float4(v.color.rgb * _Color.rgb,
                                v.params.z * _Color.a * _Exposure * _TransitionAlpha * nearFade);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 plate = tex2D(_MainTex, i.uv).rgb;

                // Take the cutout's own sky off before anything else, and rescale what is left so a subtracted
                // cutout is not also a dimmed one. Per channel, because the sky in these plates is not neutral.
                plate = max(plate - i.floorLevel, 0.0) / max(1.0 - i.floorLevel, 1e-3);

                // Corners away. _RadialStart is measured on the unrolled corner, which reaches 1 at the edge
                // mid-points and 1.414 at the corners, so everything outside the inscribed disc is already gone.
                float radius = length(i.quad);
                float vignette = 1.0 - smoothstep(min(_RadialStart, 0.999), 1.0, radius);

                float3 rgb = plate * i.tint.rgb * (i.tint.a * vignette);

                // Additive over passthrough: the eye buffer's alpha has to rise with the light being added, or
                // the compositor shows the room through the brightest galaxies.
                return fixed4(rgb, saturate(dot(rgb, 1.0)));
            }
            ENDCG
        }
    }

    Fallback Off
}
