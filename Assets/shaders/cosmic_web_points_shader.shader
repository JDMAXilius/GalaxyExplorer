// Licensed under the MIT License. See LICENSE in the project root for license information.

// The Cosmic Web's point sprites (GDD 4.7, Technical Overview 7.3).
//
// This is the galaxy's star path with a different point distribution and a violet ramp: the same
// StarVertDescriptor buffer, the same StarQuad.cginc vertex-stage quad expansion, six vertices per point,
// drawn with CommandBuffer.DrawProcedural. CosmicWebGenerator fills the buffer; CosmicWebRenderer owns it.
//
// It is a separate shader from Galaxy/Stars rather than a property added to it, for three reasons. The galaxy
// is shipping content and its shader is the app's hottest vertex program, so it is not somewhere to add a
// colour ramp nobody else wants. Galaxy/Stars predates the multiview rules in Technical Overview 7.1 and
// carries none of the stereo macros, and quietly bolting them on to a shipping shader is a regression waiting
// to happen. And the web decodes its positions differently: one sin/cos pair instead of the galaxy's four
// transcendentals, because its points are plain cylindrical coordinates, not places on a spiral ellipse.
//
// How the shared struct is read here (CosmicWebGenerator documents the other half):
//   ellipseDistance  distance from the volume's axis      curveOffset  angle about it
//   yOffset          height                               ellipseOffset  0..1 along the violet ramp
//   color            brightness only; the hue is _VoidColor/_FilamentColor/_NodeColor, so the web can be
//                    recoloured on the material without regenerating a point
//
// _Age turns the whole volume about its vertical axis. The GDD asks for 0.5 deg/min, and doing it here rather
// than on the transform means the drift can never fight the hands: ManipulationHandler owns the transform,
// this owns the drift, and the grab collider is a sphere so it does not care either way.
//
// Additive, so the alpha it writes has to grow with the light it adds or the passthrough compositor would show
// the room through the brightest knots (Technical Overview 7.1). Cosmic Web is a Full Black module today, but
// the dock's passthrough toggle can override that at any time.
Shader "CosmicSimulation/CosmicWebPoints"
{
    Properties
    {
        _MainTex ("Point sprite atlas", 2D) = "white" {}

        [Header(Violet ramp)]
        [HDR] _VoidColor ("Void tracers", Color) = (0.20, 0.13, 0.38, 1)
        [HDR] _FilamentColor ("Filaments", Color) = (0.55, 0.30, 1.00, 1)
        [HDR] _NodeColor ("Nodes", Color) = (0.95, 0.72, 1.00, 1)
        _RampMid ("Where the filament colour sits", Range(0.05, 0.95)) = 0.55
        [HDR] _Color ("Overall tint", Color) = (1, 1, 1, 1)

        [Header(Size and life)]
        _WSScale ("Sprite half size (m)", Float) = 0.0032
        _Age ("Volume rotation (rad)", Float) = 0
        _Shimmer ("Shimmer depth", Range(0, 1)) = 0.18
        _ShimmerSpeed ("Shimmer speed", Float) = 0.7
        _TransitionAlpha ("Transition Alpha", Float) = 1

        [Header(Near fade)]
        _NearFadeStart ("Near fade start (m)", Float) = 0.15
        _NearFadeRange ("Near fade range (m)", Float) = 0.35
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
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "cginc/StarVertDescriptor.cginc"
            #include "cginc/StarQuad.cginc"

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
                // Not COLOR0: the ramp colours are HDR and some mobile drivers clamp colour interpolators to
                // 0..1, which would flatten the bright knots the whole look depends on.
                float3 tint : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _VoidColor;
            float4 _FilamentColor;
            float4 _NodeColor;
            float _RampMid;
            float4 _Color;

            float _WSScale;
            float _Shimmer;
            float _ShimmerSpeed;
            float _TransitionAlpha;

            float _NearFadeStart;
            float _NearFadeRange;

            // _Age is also declared by cginc/StarPositionCompute.cginc, which this shader deliberately does not
            // include: the web's positions are cylindrical, so one sin/cos pair replaces the galaxy's ellipse maths.
            float _Age;

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

                float angle = p.curveOffset + _Age;
                float3 localPos = float3(cos(angle) * p.ellipseDistance, p.yOffset, sin(angle) * p.ellipseDistance);

                float4 clipPos = UnityObjectToClipPos(float4(localPos, 1));
                o.vertex = StarQuadOffset(clipPos, corner, p.size * _WSScale);
                o.uv = StarQuadUVs[corner] * 0.5 + p.uv + float2(0, .5);

                // Two lerps rather than a branch: at ramp 0 the void colour, at _RampMid the filament colour,
                // at 1 the node colour, monotonic in between.
                float ramp = saturate(p.ellipseOffset);
                float tLow = saturate(ramp / max(_RampMid, 1e-4));
                float tHigh = saturate((ramp - _RampMid) / max(1.0 - _RampMid, 1e-4));
                float3 rampColor = lerp(lerp(_VoidColor.rgb, _FilamentColor.rgb, tLow), _NodeColor.rgb, tHigh);

                float shimmer = 1.0 + _Shimmer * sin(_Time.y * _ShimmerSpeed + p.random * 6.2831853);

                // The player stands inside the volume, so a point can end up a hand's width from the eye where
                // its sprite would fill the view. Fade those out instead, in the vertex stage: no depth
                // texture, no prepass, and one point's worth of arithmetic.
                float3 worldPos = mul(unity_ObjectToWorld, float4(localPos, 1)).xyz;
                float eyeDistance = distance(worldPos, _WorldSpaceCameraPos);
                float nearFade = saturate((eyeDistance - _NearFadeStart) / max(_NearFadeRange, 1e-4));

                o.tint = rampColor * p.color * _Color.rgb * (_TransitionAlpha * shimmer * nearFade);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 rgb = i.tint * tex2D(_MainTex, i.uv).a;
                return fixed4(rgb, saturate(dot(rgb, 1.0)));
            }
            ENDCG
        }
    }

    Fallback Off
}
