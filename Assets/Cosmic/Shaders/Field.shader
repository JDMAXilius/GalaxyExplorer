Shader "Cosmic/Field"
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

            // Streams: POSITION centre, TEXCOORD1 xy rolled corner offset (m) + zw unrolled corner (the disc fade), TEXCOORD2 floor / drift share / brightness.
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

                float rate = v.params.y;
                float s, c;
                sincos(_Age * rate, s, c);
                float3 p = v.vertex.xyz;
                p = float3(c * p.x + s * p.z, p.y, c * p.z - s * p.x);

                float4 clipPos = UnityObjectToClipPos(float4(p, 1));

                float objectScale = length(unity_ObjectToWorld._m00_m10_m20);
                float2 offset = v.corner.xy * objectScale;
                clipPos.x += offset.x * UNITY_MATRIX_P._11;
                clipPos.y += offset.y * UNITY_MATRIX_P._22;
                o.pos = clipPos;

                o.uv = v.uv;
                o.quad = v.corner.zw;
                o.floorLevel = saturate(v.params.x + _Floor);

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

                plate = max(plate - i.floorLevel, 0.0) / max(1.0 - i.floorLevel, 1e-3);

                float radius = length(i.quad);
                float vignette = 1.0 - smoothstep(min(_RadialStart, 0.999), 1.0, radius);

                float3 rgb = plate * i.tint.rgb * (i.tint.a * vignette);

                return fixed4(rgb, saturate(dot(rgb, 1.0)));
            }
            ENDCG
        }
    }

    Fallback Off
}
