// Licensed under the MIT License. See LICENSE in the project root for license information.

// Bloom for the Built-in pipeline, written because there is no post-processing package in this project and
// because a star without bloom is one dim pixel rather than a star.
//
// The structure is the dual-filter pyramid every mobile renderer converges on: a threshold prefilter with a
// soft knee, a chain of half-resolution downsamples with a 13-tap filter, then a 9-tap tent on the way back
// up, each level added to the one above. It costs a few small draws and reads far less bandwidth than one
// wide blur at full resolution, which matters on a tiler where bandwidth is the budget.
//
// Karis' average is in the first downsample on purpose: a single very bright sub-pixel star otherwise
// flickers violently as it crosses a pixel boundary, and weighting by 1/(1+luma) turns that into a shimmer
// you can live with. It is applied only at the first level, where the aliasing is.

Shader "CosmicSimulation/Bloom"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
    UNITY_DECLARE_SCREENSPACE_TEXTURE(_BloomTex);

    float4 _MainTex_TexelSize;
    float4 _Filter;      // x threshold, y threshold - knee, z 2 * knee, w 0.25 / knee
    float _Intensity;
    float _Scatter;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }

    float3 Sample(float2 uv)
    {
        return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv).rgb;
    }

    // 13-tap box-of-boxes. Wider than a 4-tap for the same number of samples in practice, because the
    // overlapping inner box kills the ringing a plain box leaves on a bright point.
    float3 Downsample(float2 uv, float2 texel)
    {
        float3 a = Sample(uv + texel * float2(-2, -2));
        float3 b = Sample(uv + texel * float2( 0, -2));
        float3 c = Sample(uv + texel * float2( 2, -2));
        float3 d = Sample(uv + texel * float2(-1, -1));
        float3 e = Sample(uv + texel * float2( 1, -1));
        float3 f = Sample(uv + texel * float2(-2,  0));
        float3 g = Sample(uv);
        float3 h = Sample(uv + texel * float2( 2,  0));
        float3 i = Sample(uv + texel * float2(-1,  1));
        float3 j = Sample(uv + texel * float2( 1,  1));
        float3 k = Sample(uv + texel * float2(-2,  2));
        float3 l = Sample(uv + texel * float2( 0,  2));
        float3 m = Sample(uv + texel * float2( 2,  2));

        float3 inner = (d + e + i + j) * 0.5;
        float3 corners = (a + c + k + m) * 0.125;
        float3 edges = (b + f + h + l) * 0.25;
        return (g * 0.125 + inner * 0.25 + corners * 0.25 + edges * 0.25) * 0.5;
    }

    float3 Tent(float2 uv, float2 texel)
    {
        float3 sum = Sample(uv + texel * float2(-1, -1));
        sum += Sample(uv + texel * float2(0, -1)) * 2;
        sum += Sample(uv + texel * float2(1, -1));
        sum += Sample(uv + texel * float2(-1, 0)) * 2;
        sum += Sample(uv) * 4;
        sum += Sample(uv + texel * float2(1, 0)) * 2;
        sum += Sample(uv + texel * float2(-1, 1));
        sum += Sample(uv + texel * float2(0, 1)) * 2;
        sum += Sample(uv + texel * float2(1, 1));
        return sum * 0.0625;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0 - threshold with a soft knee, and Karis' average against single-pixel flicker
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 colour = Downsample(i.uv, _MainTex_TexelSize.xy);

                float brightest = max(colour.r, max(colour.g, colour.b));
                float soft = clamp(brightest - _Filter.y, 0, _Filter.z);
                soft = soft * soft * _Filter.w;
                float contribution = max(soft, brightest - _Filter.x) / max(brightest, 1e-5);
                colour *= contribution;

                float luma = dot(colour, float3(0.2126, 0.7152, 0.0722));
                return float4(colour / (1 + luma), 1);
            }
            ENDCG
        }

        // 1 - downsample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return float4(Downsample(i.uv, _MainTex_TexelSize.xy), 1);
            }
            ENDCG
        }

        // 2 - upsample and add into the level above
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return float4(Tent(i.uv, _MainTex_TexelSize.xy) * _Scatter, 1);
            }
            ENDCG
        }

        // 3 - composite over the scene
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 scene = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv).rgb;
                float3 bloom = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_BloomTex, i.uv).rgb;
                return float4(scene + bloom * _Intensity, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
