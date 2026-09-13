Shader "Cosmic/Points"
{
    Properties
    {
        _MainTex ("Point sprite atlas", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1, 1, 1, 1)
        _WSScale ("Sprite half size (m)", Float) = 1
        _TransitionAlpha ("Transition alpha", Float) = 1
        _Age ("Rotation (rad)", Float) = 0

        [Header(Web ramp)]
        [HDR] _VoidColor ("Void tracers", Color) = (0.20, 0.13, 0.38, 1)
        [HDR] _FilamentColor ("Filaments", Color) = (0.55, 0.30, 1.00, 1)
        [HDR] _NodeColor ("Nodes", Color) = (0.95, 0.72, 1.00, 1)
        _RampMid ("Where the filament colour sits", Range(0.05, 0.95)) = 0.55

        [Header(Nebula depth cue)]
        _DepthDim ("How much the far side dims", Range(0, 1)) = 0.55
        [HDR] _DepthTint ("Colour the far side sinks towards", Color) = (0.05, 0.06, 0.12, 1)

        [Header(Shimmer and near fade)]
        _Shimmer ("Shimmer depth", Range(0, 1)) = 0.18
        _ShimmerSpeed ("Shimmer speed", Float) = 0.7
        _NearFadeStart ("Near fade start (m)", Float) = 0.15
        _NearFadeRange ("Near fade range (m)", Float) = 0.35

        [Header(Blend state)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z write", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        LOD 100

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            // multi_compile because the shader is Always Included and stripping has no material to learn a keyword from.
            #pragma multi_compile _LAYER_STARS _LAYER_CLOUDS _LAYER_DUST _LAYER_WEB _LAYER_NEBULA

            #include "UnityCG.cginc"
            #include "cginc/Points.cginc"

            struct appdata
            {
                uint vid : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Color;
            float _WSScale;
            float _TransitionAlpha;
            float3 _LocalCamDir;

            float4 _VoidColor;
            float4 _FilamentColor;
            float4 _NodeColor;
            float _RampMid;

            float _DepthDim;
            float4 _DepthTint;

            float _Shimmer;
            float _ShimmerSpeed;
            float _NearFadeStart;
            float _NearFadeRange;

            float Shimmer(uint pointIndex)
            {
                return 1.0 + _Shimmer * sin(_Time.y * _ShimmerSpeed + StarPhase(pointIndex) * 6.2831853);
            }

            float NearFade(float3 localPos)
            {
                float3 worldPos = mul(unity_ObjectToWorld, float4(localPos, 1)).xyz;
                return saturate((distance(worldPos, _WorldSpaceCameraPos) - _NearFadeStart) / max(_NearFadeRange, 1e-4));
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint pointIndex, corner;
                StarQuadCorner(v.vid, pointIndex, corner);
                StarVert p = _Stars[pointIndex];

            #if defined(_LAYER_WEB) || defined(_LAYER_NEBULA)
                float3 localPos = CylinderPosition(p);
            #else
                float3 localPos = SpiralPosition(p);
            #endif

                float4 clipPos = UnityObjectToClipPos(float4(localPos, 1));

            #if defined(_LAYER_CLOUDS)
                o.vertex = StarQuadOffset(clipPos, corner, p.size * _WSScale * 2);
                float3 cloud = p.color * _TransitionAlpha * _Color.rgb * 0.5;
                o.color = float4(cloud * dot(cloud, 1), 1);
                o.uv = StarQuadUVs[corner];
            #elif defined(_LAYER_DUST)
                float fade = saturate(dot(normalize(localPos), _LocalCamDir));
                o.vertex = StarQuadOffset(clipPos, corner, p.size * _WSScale);
                o.color = float4(p.color, 1) * fade * _TransitionAlpha;
                o.uv = StarQuadUVs[corner] * 0.5 + p.uv;
            #elif defined(_LAYER_WEB)
                float ramp = saturate(p.ellipseOffset);
                float tLow = saturate(ramp / max(_RampMid, 1e-4));
                float tHigh = saturate((ramp - _RampMid) / max(1.0 - _RampMid, 1e-4));
                float3 rampColor = lerp(lerp(_VoidColor.rgb, _FilamentColor.rgb, tLow), _NodeColor.rgb, tHigh);
                o.vertex = StarQuadOffset(clipPos, corner, p.size * _WSScale);
                o.uv = StarQuadUVs[corner] * 0.5 + p.uv + float2(0, .5);
                o.color = float4(rampColor * p.color * _Color.rgb *
                    (_TransitionAlpha * Shimmer(pointIndex) * NearFade(localPos)), 1);
            #elif defined(_LAYER_NEBULA)
                float depth = saturate(p.ellipseOffset);
                o.vertex = StarQuadOffset(clipPos, corner, p.size * _WSScale * lerp(0.78, 1.25, depth));
                o.uv = StarQuadUVs[corner] * 0.5 + p.uv + float2(0, .5);
                float dim = lerp(1.0 - _DepthDim, 1.0, depth);
                float3 gas = lerp(_DepthTint.rgb * p.color, p.color, depth) * dim;
                o.color = float4(gas * _Color.rgb *
                    (_TransitionAlpha * Shimmer(pointIndex) * NearFade(localPos)), 1);
            #else
                o.vertex = StarQuadOffset(clipPos, corner, p.size * _WSScale);
                o.color = float4(p.color, 1) * _TransitionAlpha * _Color;
                o.uv = StarQuadUVs[corner] * 0.5 + p.uv + float2(0, .5);
            #endif

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            #if defined(_LAYER_CLOUDS)
                float2 uv = i.uv * 2 - 1;
                return ((1 - dot(uv, uv)) * i.color.xyz).xyzx;
            #elif defined(_LAYER_DUST)
                float4 color = tex2D(_MainTex, i.uv + float2(0, .5)).a * i.color;
                color.a = dot(color.xyz, 1) / 3.0;
                clip(color.a - 0.05);
                return color * _Color;
            #elif defined(_LAYER_WEB) || defined(_LAYER_NEBULA)
                float3 rgb = i.color.rgb * tex2D(_MainTex, i.uv).a;
                return fixed4(rgb, saturate(dot(rgb, 1.0)));
            #else
                float4 color = tex2D(_MainTex, i.uv).a * float4(i.color.xyz, 1.0);
                color.a = dot(color.xyz, 1);
                return color;
            #endif
            }
            ENDCG
        }
    }

    Fallback Off
}
