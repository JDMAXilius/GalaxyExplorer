// Licensed under the MIT License. See LICENSE in the project root for license information.

// The star at the heart of a nebula, drawn procedurally rather than from a flare texture.
//
// Every one of the reference images is composed around this: a single blown-out point with a halo and a set
// of spikes, and the gas arranged around it. It is not decoration - it is the thing that says there is a
// source, which is why the gas is lit and why it is expanding. Without it a nebula is a cloud with no reason.
//
// <b>Procedural, not a texture.</b> A flare texture has a resolution, and this is the one thing in the scene
// the player will put their face next to; a 512 sprite at arm's length in a headset is a blurred cross. The
// four terms below - core, halo, spikes, and the thin diagonal pair - are a few instructions each and stay
// sharp at any distance. They also cost no memory and no import settings, which is worth something in a
// project where the last three bugs were all import or lifecycle problems.
//
// <b>It relies on bloom.</b> The colour written here goes well above 1, which on an HDR camera is what makes
// the Bloom pass throw a halo around it. Without bloom this is a bright cross and nothing more. That is the
// correct dependency: a star is defined by what the eye and the lens do with light that is too bright, not by
// the light itself.
//
// Billboarded in the vertex stage against the view matrix, so it faces the camera in both eyes without a
// script touching the transform every frame - and correctly under single-pass instanced, where a transform
// aimed at "the camera" would be aimed at neither eye.

Shader "CosmicSimulation/NebulaStar"
{
    Properties
    {
        [HDR] _Colour ("Colour", Color) = (1, 0.95, 0.85, 1)
        _Intensity ("Overall intensity", Range(0, 40)) = 9
        _Size ("Size in metres", Float) = 2.5

        [Header(Core)]
        _CoreTightness ("How tight the core is", Range(4, 400)) = 90
        _CoreGain ("Core brightness", Range(0, 20)) = 6

        [Header(Halo)]
        _HaloFalloff ("Halo falloff", Range(0.5, 12)) = 3.2
        _HaloGain ("Halo brightness", Range(0, 8)) = 0.85

        [Header(Spikes)]
        _SpikeWidth ("Spike width", Range(0.002, 0.4)) = 0.045
        _SpikeSharp ("Spike sharpness", Range(0.5, 8)) = 1.6
        _SpikeLength ("Spike falloff with distance", Range(0.5, 8)) = 1.7
        _SpikeGain ("Spike brightness", Range(0, 8)) = 1.1
        _DiagonalGain ("Diagonal spike brightness", Range(0, 2)) = 0.35

        [Header(Twinkle)]
        _Twinkle ("Twinkle depth", Range(0, 1)) = 0.12
        _TwinkleSpeed ("Twinkle speed", Range(0, 8)) = 1.3
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            // Premultiplied. A star is pure emission so it only ever adds light, but the alpha still has to
            // grow with it or a passthrough compositor shows the room through the brightest thing on screen.
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            float4 _Colour;
            float _Intensity;
            float _Size;

            float _CoreTightness;
            float _CoreGain;
            float _HaloFalloff;
            float _HaloGain;
            float _SpikeWidth;
            float _SpikeSharp;
            float _SpikeLength;
            float _SpikeGain;
            float _DiagonalGain;
            float _Twinkle;
            float _TwinkleSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
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

                // Billboard about this object's origin, in view space, so it faces whichever eye is drawing.
                float3 centreWorld = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 centreView = mul(UNITY_MATRIX_V, float4(centreWorld, 1)).xyz;
                float3 offsetView = float3(v.vertex.x, v.vertex.y, 0) * _Size;

                o.pos = mul(UNITY_MATRIX_P, float4(centreView + offsetView, 1));
                o.uv = v.vertex.xy * 2.0;       // -1..1 across the quad
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 p = i.uv;
                float r = length(p);
                if (r > 1.0) discard;

                // Everything fades to nothing by the edge of the quad, so there is never a visible square.
                float edge = saturate(1.0 - r);

                float core = exp(-r * r * _CoreTightness) * _CoreGain;
                float halo = pow(edge, _HaloFalloff) * _HaloGain;

                // The cross. Real spikes come from the vanes holding a telescope's secondary mirror, which is
                // why they are straight, centred and always the same few angles - so they are worth drawing
                // literally rather than approximating with a blurred star shape.
                float falloff = pow(edge, _SpikeLength);
                float vertical = pow(saturate(1.0 - abs(p.x) / _SpikeWidth), _SpikeSharp);
                float horizontal = pow(saturate(1.0 - abs(p.y) / _SpikeWidth), _SpikeSharp);

                // A second pair at 45 degrees, fainter: four vanes give eight spikes, and the extra pair is
                // what stops the cross reading as a plus sign drawn on the screen.
                float2 d = float2(p.x + p.y, p.x - p.y) * 0.70710678;
                float diagonalA = pow(saturate(1.0 - abs(d.x) / _SpikeWidth), _SpikeSharp);
                float diagonalB = pow(saturate(1.0 - abs(d.y) / _SpikeWidth), _SpikeSharp);

                float spikes = (vertical + horizontal) * _SpikeGain * falloff
                             + (diagonalA + diagonalB) * _DiagonalGain * falloff;

                float twinkle = 1.0 + _Twinkle * sin(_Time.y * _TwinkleSpeed);
                float intensity = (core + halo + spikes) * _Intensity * twinkle;

                float3 rgb = _Colour.rgb * intensity;
                return fixed4(rgb, saturate(intensity));
            }
            ENDCG
        }
    }

    Fallback Off
}
