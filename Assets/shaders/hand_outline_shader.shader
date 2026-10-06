// Licensed under the MIT License. See LICENSE in the project root for license information.

// Translucent hand with a bright rim, for the XR Hands sample mesh (a SkinnedMeshRenderer; skinning happens
// before the vertex stage, so nothing here is skinning-specific).
//
// Two passes. The first writes depth only so the second, alpha-blended, pass draws each pixel once: without it
// the fingers show through the palm and the hand reads as a tangle. This replaces the sample DepthOnly material
// that used to sit in slot 0, which had no stereo macros and ran in whatever order the material array put it.
//
// Hand-written rather than the sample Shader Graph materials because those do not build for Built-in RP with
// Single Pass Instanced. The view direction is computed in the vertex stage: _WorldSpaceCameraPos is per-eye,
// and reading it in the fragment stage would also need UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX.
Shader "CosmicSimulation/HandOutline"
{
    Properties
    {
        _Color ("Body (a = base alpha)", Color) = (0.55, 0.75, 1.0, 0.18)
        _RimColor ("Rim (a = rim alpha)", Color) = (0.6, 0.85, 1.0, 1.0)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Cull Back
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            Name "DepthPrepass"
            ZWrite On
            ZTest LEqual
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        Pass
        {
            Name "Rim"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _RimColor;
            half _RimPower;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half ndv = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                half rim = pow(1.0h - ndv, _RimPower);

                fixed4 c;
                c.rgb = _Color.rgb + _RimColor.rgb * rim;
                // Rim alpha is added, not multiplied, so the outline stays visible over a bright room where the
                // faint body alone would vanish.
                c.a = saturate(_Color.a + _RimColor.a * rim);
                return c;
            }
            ENDCG
        }
    }

    Fallback Off
}
