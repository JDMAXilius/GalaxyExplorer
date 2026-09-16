Shader "Cosmic/BeingPoints"
{
    Properties
    {
        _BaseColor ("Base colour", Color) = (0, 0.69, 1, 1)
        _VariantColor ("Variant colour", Color) = (0.07, 0.42, 1, 1)
        _TouchColor ("Touch colour", Color) = (0.03, 0, 1, 1)
        _ActiveColor ("Active colour", Color) = (1, 1, 1, 1)
        _Size ("Point size (object)", Float) = 0.07
        _Speed ("Spin speed", Float) = 1
        _Blend ("Reveal", Range(0, 1)) = 1
        _Active ("Active", Range(0, 1)) = 0
        [HideInInspector] _SelfTime ("Spin time", Float) = 0
        _Bands ("Voice bands, low to high", Vector) = (0, 0, 0, 0)
        _Articulate ("Belt push at full band", Float) = 0.35
        _BandExtent ("Half height of the source mesh", Float) = 0.5
        [HideInInspector] _TouchPoint ("Touch point (world)", Vector) = (1000000, 1000000, 1000000, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"

            float4 _BaseColor, _VariantColor, _TouchColor, _ActiveColor, _Bands, _TouchPoint;
            float _Size, _Speed, _Blend, _Active, _SelfTime, _Articulate, _BandExtent;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 randoms : TEXCOORD0;
                float corner : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            static const float2 Offsets[4] = { float2(0, -1), float2(1, 0), float2(-1, 0), float2(0, 1) };
            static const float2 Uvs[4] = { float2(-1, -1), float2(1, -1), float2(-1, 1), float2(1, 1) };

            float3 SpinY(float3 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = SpinY(v.vertex.xyz, _SelfTime * v.randoms.x * _Speed);
                float belt = saturate(v.vertex.y / max(_BandExtent, 1e-4) * 0.5 + 0.5);
                float4 pick = float4(belt < 0.25, belt >= 0.25 && belt < 0.5, belt >= 0.5 && belt < 0.75, belt >= 0.75);
                p *= 1 + _Articulate * dot(pick, _Bands);
                float3 world = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                float touch = saturate(0.2 - distance(world, _TouchPoint.xyz)) * 5;
                p *= 1 + 0.2 * touch + 0.05 * _Active;
                o.color = lerp(lerp(lerp(_BaseColor, _VariantColor, v.randoms.z), _ActiveColor, _Active), _TouchColor, touch);
                float size = _Size * clamp(v.randoms.y, 0.2, 1) * (1 + 0.2 * touch + 0.2 * _Active);
                size *= size;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                uint corner = (uint)v.corner;
                o.pos.x += Offsets[corner].x * size * o.pos.w * UNITY_MATRIX_P._11;
                o.pos.y += Offsets[corner].y * size * o.pos.w * UNITY_MATRIX_P._22;
                o.uv = Uvs[corner];
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float l = length(i.uv);
                return fixed4(i.color.rgb, (1 - l) * (1 - step(1, l)) * _Blend);
            }
            ENDCG
        }
    }
}
