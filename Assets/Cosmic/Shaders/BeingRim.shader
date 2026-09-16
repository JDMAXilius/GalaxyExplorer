Shader "Cosmic/BeingRim"
{
    Properties
    {
        _Color ("Colour", Color) = (0.43, 0.69, 0.96, 1)
        _Multiplier ("Glow", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Multiplier;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float edge : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 normal = normalize(UnityObjectToWorldNormal(v.normal));
                float3 view = normalize(WorldSpaceViewDir(v.vertex));
                float edge = 1 - dot(view, normal);
                edge *= edge;
                o.edge = lerp(edge * edge, edge, _Multiplier);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _Color;
                c.a *= i.edge;
                return c;
            }
            ENDCG
        }
    }
}
