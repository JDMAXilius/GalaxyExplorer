// Licensed under the MIT License. See LICENSE in the project root for license information.

// The dark half of a nebula: the dust lanes that take light away instead of adding it.
//
// Identical to CosmicSimulation/NebulaVolume in every respect but the blend. The additive pass can only ever
// make a pixel brighter, so a cloud built from it alone has no lanes, no silhouette and no sense of depth
// beyond its own glow - and dust is most of what gives Orion and the Pillars their shape. This is the same
// premultiplied darkening the galaxies' own dust uses (spiral_stars_negative_shader), so the two families of
// object are lit and shadowed by the same arithmetic.
//
// The builder writes the colour premultiplied and puts the opacity in alpha; a point with alpha 0 does nothing.

// A nebula as a cloud of gas standing in three dimensions, rather than a photograph on a dome.
//
// This is the Cosmic Web's point path with two differences: every point carries its own colour, sampled from
// the real plate rather than mixed from a ramp, and there is a depth cue, because the thing this draws is meant
// to read as having a front and a back. Same StarVertDescriptor buffer, same StarQuad.cginc vertex-stage quad
// expansion, six vertices per point, drawn with CommandBuffer.DrawProcedural by NebulaVolume.
//
// How the shared struct is read here (NebulaVolumeBuilder documents the other half):
//   ellipseDistance  distance from the volume's axis     curveOffset  angle about it
//   yOffset          height                              ellipseOffset  0 at the far side, 1 at the near side
//   color            the plate's own colour at this point, already weighted by the point's share of the light
//   size             per-point sprite size multiplier    random  shimmer phase
//
// _Age turns the whole volume about its vertical axis, on the same reasoning as the Cosmic Web: doing the drift
// here rather than on the transform means it can never fight the hands, because ManipulationHandler owns the
// transform and this owns the drift.
//
// <b>The depth cue is the point of the shader.</b> An additive cloud has no occlusion - every point adds light,
// so a cloud drawn flat looks like a flat smear of light however carefully its points were placed in depth.
// Two things fix that cheaply. Points on the far side are dimmed and pulled towards the fog colour
// (_DepthDim / _DepthTint), which is what a real cloud does to its own far side by absorption; and the sprite
// grows with distance from the volume's centre rather than staying a fixed angular size, so the near gas reads
// as near. Both are vertex-stage arithmetic on a value the bake already computed.
//
// Additive, so the alpha it writes has to grow with the light it adds, or the passthrough compositor would show
// the room through the brightest knots (Technical Overview 7.1).
Shader "CosmicSimulation/NebulaVolumeDust"
{
    Properties
    {
        _MainTex ("Point sprite atlas", 2D) = "white" {}
        [HDR] _Color ("Overall tint", Color) = (1, 1, 1, 1)

        [Header(Size)]
        _WSScale ("Sprite half size (m)", Float) = 0.012
        _Age ("Volume rotation (rad)", Float) = 0
        _TransitionAlpha ("Transition Alpha", Float) = 1

        [Header(Depth cue)]
        _DepthDim ("How much the far side dims", Range(0, 1)) = 0.55
        [HDR] _DepthTint ("Colour the far side sinks towards", Color) = (0.05, 0.06, 0.12, 1)

        [Header(Shimmer)]
        _Shimmer ("Shimmer depth", Range(0, 1)) = 0.12
        _ShimmerSpeed ("Shimmer speed", Float) = 0.5

        [Header(Splat shape)]
        // See the note in nebula_volume_shader: undeclared here, these defaulted to zero and collapsed every
        // splat to nothing. Dust is stretched harder than gas because shape is the job of this layer.
        _Aniso ("Splat stretch (1 is round)", Range(0.25, 6)) = 2.8
        _Opacity ("How much a splat occludes", Range(0, 2)) = 1.0

        [Header(Near fade)]
        _NearFadeStart ("Near fade start (m)", Float) = 0.12
        _NearFadeRange ("Near fade range (m)", Float) = 0.30
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        LOD 100

        Pass
        {
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "cginc/StarVertDescriptor.cginc"
            #include "cginc/StarQuad.cginc"

            float _Aniso;
            float _Opacity;

            // SV_VertexID is the only real input: every point is expanded from the buffer. The instance id sits
            // beside it because that is where UNITY_SETUP_INSTANCE_ID looks for it under single-pass instanced
            // and multiview stereo.
            struct appdata
            {
                uint vid : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                // Not COLOR0: the plate's bright knots are HDR and some mobile drivers clamp colour
                // interpolators to 0..1, which would flatten exactly the parts the look depends on.
                float3 tint : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Color;
            float _WSScale;
            float _Age;
            float _TransitionAlpha;

            float _DepthDim;
            float4 _DepthTint;

            float _Shimmer;
            float _ShimmerSpeed;

            float _NearFadeStart;
            float _NearFadeRange;

            StructuredBuffer<StarVertDescriptor> _Stars;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint pointIndex, corner;
                StarQuadCorner(v.vid, pointIndex, corner);
                StarVertDescriptor p = _Stars[pointIndex];

                // Cylindrical, so that _Age is one add rather than a matrix: the bake converts whatever the
                // depth model produced into radius, angle and height once, here it is a single sin/cos pair.
                float angle = p.curveOffset + _Age;
                float3 localPos = float3(cos(angle) * p.ellipseDistance, p.yOffset, sin(angle) * p.ellipseDistance);

                float4 clipPos = UnityObjectToClipPos(float4(localPos, 1));

                // Depth along the axis the plate was projected on, 0 far, 1 near. Baked rather than derived
                // from the view, because the player walks around inside this and a view-derived front would
                // swim as they moved.
                float depth = saturate(p.ellipseOffset);

                // Near gas larger, far gas smaller. A quarter either side of the authored size is enough to
                // read without making the far side vanish.
                float sizeByDepth = lerp(0.78, 1.25, depth);
                float halfSize = max(p.size * _WSScale * sizeByDepth, 0);

                // Same oriented splat as the gas: dust that is all identical squares reads as noise, and dust
                // is the layer whose whole job is shape.
                float splatAngle = p.random * 6.2831853;
                float2 dir = float2(cos(splatAngle), sin(splatAngle));
                float2 unit = StarQuadOffsets[corner];
                // _Aniso is the aspect ratio of the splat, and the square root is what makes it mean that.
                // Stretching by (a, 1/a) is area preserving but the aspect it produces is a*a, so 2.8 was an
                // eight-to-one needle rather than an elongated blob - a field of dark splinters, which is
                // exactly what it looked like. sqrt(a) either side keeps the area and makes the number honest.
                float stretch = sqrt(max(_Aniso, 1e-3));
                float2 stretched = float2(unit.x * stretch, unit.y / stretch);
                float2 oriented = float2(stretched.x * dir.x - stretched.y * dir.y,
                                         stretched.x * dir.y + stretched.y * dir.x) * halfSize;

                clipPos.x += oriented.x * UNITY_MATRIX_P._11;
                clipPos.y += oriented.y * UNITY_MATRIX_P._22;
                o.vertex = clipPos;
                o.uv = StarQuadUVs[corner] * 0.5 + p.uv + float2(0, .5);

                float shimmer = 1.0 + _Shimmer * sin(_Time.y * _ShimmerSpeed + p.random * 6.2831853);

                // The player can stand inside this, so a point can end up a hand's width from the eye where its
                // sprite would fill the view. Fade those out in the vertex stage: no depth texture, no prepass,
                // one point's worth of arithmetic.
                float3 worldPos = mul(unity_ObjectToWorld, float4(localPos, 1)).xyz;
                float eyeDistance = distance(worldPos, _WorldSpaceCameraPos);
                float nearFade = saturate((eyeDistance - _NearFadeStart) / max(_NearFadeRange, 1e-4));

                // What absorption does to the far side of a real cloud, at the cost of one lerp: dimmer, and
                // shifted towards the colour of the gas in front of it.
                float dim = lerp(1.0 - _DepthDim, 1.0, depth);
                float3 colour = lerp(_DepthTint.rgb * p.color, p.color, depth) * dim;

                o.tint = colour * _Color.rgb * (_TransitionAlpha * shimmer * nearFade);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Dust occludes by how much of the pixel it covers, not by how bright it is. That is the
                // difference between dust and gas: a dark dust lane is still opaque, and tying its alpha to
                // its own brightness would make the darkest dust - the part doing the most work - invisible.
                float coverage = tex2D(_MainTex, i.uv).a;
                float3 rgb = i.tint * coverage;
                return fixed4(rgb, saturate(coverage * _Opacity));
            }
            ENDCG
        }
    }

    Fallback Off
}
