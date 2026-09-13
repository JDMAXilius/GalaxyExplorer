Shader "Cosmic/Orbit"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (1,1,1,1)
        [HDR] _PlanetHighlightColor("Highlight Color", Color) = (0,0,0,0)
        _MainTex("Falloff Texture", 2D) = "white" {}
        _TransitionAlpha("Transition Alpha", Float) = 1
        _Truthfulness("Truthfulness (0 = schematic, 1 = real)", Float) = 0
        _Width("Width", Float) = 0.0035
        _GlobalScale("Global Scale", Float) = 1
        _FadeOffDistanceAroundPlanet("FadeOff Distance Around Planet", Float) = 0.04
        _TrailTailAngle("Tail Angle Offset", Float) = -1.07
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "UnityCG.cginc"
            #include "cginc/NearClip.cginc"

            struct OrbitPoint
            {
                float3 schematic;
                float3 real;
            };

            struct OrbitSpan
            {
                int4 range;
                float4 planetPositionAndRadius;
            };

            // SV_VertexID lives in a struct so UNITY_VERTEX_INPUT_INSTANCE_ID has somewhere to arrive: under single pass instanced the eye index is the instance id.
            struct appdata
            {
                uint vid : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texCoord : TEXCOORD1;
                float3 wPos : TEXCOORD2;
                float4 planetPosAndRadius : TEXCOORD3;
                float3 nextDirection : TEXCOORD4;
                float clipAmount : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _PlanetHighlightColor;
            float _TransitionAlpha;
            float _Truthfulness;
            float _Width;
            float _GlobalScale;
            float _FadeOffDistanceAroundPlanet;
            float _TrailTailAngle;
            float _OrbitCount;
            float4x4 _Orbits2World;

            StructuredBuffer<OrbitPoint> _OrbitsData;
            StructuredBuffer<OrbitSpan> _OrbitSpans;

            static const uint SegmentStripIndex[6] = { 0, 1, 2, 2, 1, 3 };

            uint tIndex(uint local, int4 range)
            {
                uint count = (uint)range.y;
                return (uint)range.x + (local + count) % count;
            }

            float3 samplePoint(uint index)
            {
                OrbitPoint p = _OrbitsData[index];
                return lerp(p.schematic, p.real, _Truthfulness);
            }

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint id = v.vid / 6;
                uint corner = SegmentStripIndex[v.vid % 6];

                OrbitSpan span = _OrbitSpans[0];
                for (int s = 0; s < (int)_OrbitCount; s++)
                {
                    OrbitSpan candidate = _OrbitSpans[s];
                    if ((int)id >= candidate.range.x && (int)id < candidate.range.x + candidate.range.y)
                    {
                        span = candidate;
                    }
                }

                uint local = id - (uint)span.range.x;
                float3 pos0 = samplePoint(tIndex(local - 1, span.range));
                float3 pos1 = samplePoint(id);
                float3 pos2 = samplePoint(tIndex(local + 1, span.range));
                float3 pos3 = samplePoint(tIndex(local + 2, span.range));

                float4x4 mvp = mul(UNITY_MATRIX_VP, _Orbits2World);

                float3 wPos0 = mul(_Orbits2World, float4(pos1, 1)).xyz;
                float3 wPos1 = mul(_Orbits2World, float4(pos2, 1)).xyz;

                float4 points[4];
                points[0] = mul(mvp, float4(pos0, 1));
                points[1] = mul(mvp, float4(pos1, 1));
                points[2] = mul(mvp, float4(pos2, 1));
                points[3] = mul(mvp, float4(pos3, 1));

                float2 correctPoints[4];
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    correctPoints[i] = points[i].xy / abs(points[i].w);
                }

                float2 direction0 = normalize(correctPoints[1] - correctPoints[0]);
                float2 direction1 = normalize(correctPoints[2] - correctPoints[1]);
                float2 direction2 = normalize(correctPoints[3] - correctPoints[2]);

                float2 tangentStart = normalize(direction1 + direction0);
                float2 tangentEnd = normalize(direction2 + direction1);
                float2 sideStart = float2(tangentStart.y, -tangentStart.x);
                float2 sideEnd = float2(tangentEnd.y, -tangentEnd.x);

                bool atEnd = corner >= 2;
                float edgeSign = (corner == 0 || corner == 2) ? -1 : 1;
                float4 basePoint = atEnd ? points[2] : points[1];
                float2 side = atEnd ? sideEnd : sideStart;

                o.vertex = float4(basePoint.xyz + edgeSign * _Width * float3(side, 0) * basePoint.w, basePoint.w);
                o.texCoord = float2(edgeSign < 0 ? 0 : 1, 1);
                o.wPos = atEnd ? wPos1 : wPos0;
                o.planetPosAndRadius = span.planetPositionAndRadius;
                o.nextDirection = normalize(wPos1 - wPos0);
                o.clipAmount = CalcVertClipAmount(wPos0);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 toPlanet = i.wPos - i.planetPosAndRadius.xyz;
                float distanceFromPlanet = length(toPlanet);
                toPlanet = normalize(toPlanet);

                float planetDistanceOpacity = saturate(distanceFromPlanet / _FadeOffDistanceAroundPlanet / _GlobalScale);
                planetDistanceOpacity = lerp(planetDistanceOpacity, 1, dot(i.nextDirection, toPlanet) < 0);

                float4 planetTailHighlight = lerp(_PlanetHighlightColor, 0, saturate(dot(i.nextDirection, toPlanet) - _TrailTailAngle));

                min16float4 finalColor = tex2D(_MainTex, i.texCoord).aaaa * (_Color + planetTailHighlight) * planetDistanceOpacity * _TransitionAlpha;

                return ApplyVertClipAmount(finalColor, i.clipAmount);
            }
            ENDCG
        }
    }
}
