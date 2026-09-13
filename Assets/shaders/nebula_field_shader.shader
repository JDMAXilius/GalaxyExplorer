// Licensed under the MIT License. See LICENSE in the project root for license information.

// The nebula as a field rather than as points: a raymarch through a baked 3D density texture, which is how
// SpaceEngine draws its own - "no polygons, only math" - and the only technique here where making the volume
// bigger costs nothing, because a field has no point count to thin out.
//
// It is drawn on a box the player stands inside, so the faces are flipped and the march starts at the eye.
// Each step reads density and colour from the 3D texture, adds that step's emission and multiplies what is
// behind it by its extinction: the gas glows AND occludes, which additive sprites can never do at any count.
//
// Two production details are lifted straight from SpaceEngine's own account, and without them this looks
// like a cheap volumetric: the ray start is dithered per pixel, which trades hard banding for grain the eye
// forgives, and the dither is animated so the grain does not sit still and read as a texture.
//
// Stereo: the eye index is set up after the vertex stage and _WorldSpaceCameraPos is read per pixel, because
// under single-pass instanced that uniform is per-eye and a march from the wrong eye's origin is a subtly
// wrong image in one eye only - invisible on Link, obvious on the device.

Shader "CosmicSimulation/NebulaField"
{
    Properties
    {
        _Volume ("Density volume (RGB colour, A density)", 3D) = "" {}
        _Steps ("March steps", Range(8, 64)) = 28
        _Density ("Density multiplier", Range(0, 8)) = 1.6
        _Emission ("Emission multiplier", Range(0, 8)) = 1.5
        _Extinction ("How much the gas blocks", Range(0, 8)) = 1.0
        _Dither ("Ray start dither", Range(0, 2)) = 1
        _StepJitterSpeed ("Dither animation speed", Range(0, 8)) = 1.7
        _Radius ("Volume half-size in local units", Float) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        Pass
        {
            // Premultiplied: the march returns light already weighted by its own coverage, and alpha says how
            // much of the scene behind survives.
            Blend One OneMinusSrcAlpha
            Cull Front      // the player is inside the box
            ZWrite Off
            ZTest Always    // and inside it, so a depth test against the box itself is meaningless
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX3D(_Volume);
            float _Steps;
            float _Density;
            float _Emission;
            float _Extinction;
            float _Dither;
            float _StepJitterSpeed;
            float _Radius;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            // Cheap hash for the per-pixel ray offset. Interleaved gradient noise: one madd and a frac, and it
            // dithers better than a texture lookup because neighbouring pixels never share an offset.
            float Dither(float2 pixel)
            {
                float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(pixel, magic.xy)));
            }

            // Slab test against the local-space box, so the march covers only the part of the ray inside it.
            bool Slab(float3 origin, float3 direction, float radius, out float near, out float far)
            {
                float3 inverse = 1.0 / (direction + 1e-6);
                float3 a = (-radius - origin) * inverse;
                float3 b = (radius - origin) * inverse;
                float3 lo = min(a, b);
                float3 hi = max(a, b);
                near = max(max(lo.x, lo.y), lo.z);
                far = min(min(hi.x, hi.y), hi.z);
                return far > max(near, 0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Per-eye camera position, in this object's space: under single-pass instanced the world
                // camera is not one point, and marching both eyes from one origin is wrong in a way only the
                // device shows.
                float3 originLocal = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 direction = normalize(i.localPos - originLocal);

                float near, far;
                if (!Slab(originLocal, direction, _Radius, near, far)) discard;
                near = max(near, 0);

                int steps = (int)_Steps;
                float span = far - near;
                float stepSize = span / steps;

                float2 pixel = i.screenPos.xy / max(i.screenPos.w, 1e-5) * _ScreenParams.xy;
                float jitter = Dither(pixel + frac(_Time.y * _StepJitterSpeed) * 64.0) * _Dither;

                float3 position = originLocal + direction * (near + jitter * stepSize);

                float3 light = 0;
                float transmittance = 1;

                [loop]
                for (int step = 0; step < steps; step++)
                {
                    // Local space is -radius..radius; the volume is sampled in 0..1.
                    float3 uvw = position / (_Radius * 2.0) + 0.5;
                    float4 sample = UNITY_SAMPLE_TEX3D(_Volume, uvw);

                    float density = sample.a * _Density;
                    if (density > 0.001)
                    {
                        float absorbed = exp(-density * _Extinction * stepSize);
                        // Emission integrated over the step rather than point-sampled, so the result does not
                        // change brightness when the step count does.
                        light += transmittance * sample.rgb * density * _Emission * stepSize;
                        transmittance *= absorbed;
                        if (transmittance < 0.01) break;
                    }

                    position += direction * stepSize;
                }

                return fixed4(light, saturate(1 - transmittance));
            }
            ENDCG
        }
    }

    Fallback Off
}
