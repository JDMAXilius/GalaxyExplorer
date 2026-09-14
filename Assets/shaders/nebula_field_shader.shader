// Licensed under the MIT License. See LICENSE in the project root for license information.

// The nebula as a field rather than as points: a raymarch through a baked 3D density texture, which is how
// SpaceEngine draws its own - "no polygons, only math" - and the only technique here where making the volume
// bigger costs nothing, because a field has no point count to thin out.
//
// It is drawn on a box the player stands inside, so the faces are flipped and the march starts at the eye.
// Each step reads density and colour from the 3D texture, adds that step's emission and multiplies what is
// behind it by its extinction: the gas glows AND occludes, which additive sprites can never do at any count.
//
// <b>Why there is noise in here as well as a baked texture.</b> A 64 cubed field holds the shape of a cloud
// and nothing finer - marched on its own it is beautifully lit fog, smooth everywhere, which is exactly the
// soup the point version produced for the opposite reason. Real nebulae are fibrous: ribbons and sheets with
// genuine black between them. That structure is produced here rather than baked, by domain warping - running
// the sample position through a noise field before sampling a second one - which is the standard way to turn
// round blobs into filaments and costs texture memory nothing. The base field says where the object is; the
// warp says what its gas is doing.
//
// <b>And why the contrast curve matters more than anything else.</b> Density straight out of the texture
// fills the volume: every voxel has some gas in it, so every ray accumulates something and the frame has no
// black anywhere. _Floor cuts the bottom off and _Contrast bends what is left, so most of the volume is
// genuinely empty and the gas that remains is somewhere. Without this the march is grey mist however good the
// noise is.
//
// Two production details are lifted straight from SpaceEngine's own account, and without them this looks
// like a cheap volumetric: the ray start is dithered per pixel, which trades hard banding for grain the eye
// forgives, and the dither is animated so the grain does not sit still and read as a texture.
//
// Stereo: the eye index is set up after the vertex stage and _WorldSpaceCameraPos is read per pixel, because
// under single-pass instanced that uniform is per-eye and a march from the wrong eye's origin is a subtly
// wrong image in one eye only - invisible on Link, obvious on the device.
//
// Cost, honestly: this is the expensive path. Every step evaluates the warp and the detail, and from inside
// the box the box is the whole screen. It is fine on a desktop GPU and it is the thing to measure first on a
// Quest - see _Steps and _DetailOctaves, which are the two knobs that buy frames.

Shader "CosmicSimulation/NebulaField"
{
    Properties
    {
        _Volume ("Density volume (RGB colour, A density)", 3D) = "" {}
        _Steps ("March steps", Range(8, 64)) = 48
        _Density ("Density multiplier", Range(0, 8)) = 2.8
        _Emission ("Emission multiplier", Range(0, 8)) = 7.4
        _Extinction ("How much the gas blocks", Range(0, 8)) = 1.6

        [Header(Structure)]
        _Floor ("Density floor (cuts the mist)", Range(0, 1)) = 0.22
        _Contrast ("Density contrast", Range(0.5, 6)) = 2.4
        _DetailScale ("Detail frequency", Range(0.5, 24)) = 6.5
        _DetailStrength ("How much detail bites", Range(0, 1)) = 0.85
        _WarpScale ("Warp frequency", Range(0.2, 8)) = 1.9
        _WarpStrength ("Warp strength (filaments)", Range(0, 2)) = 0.75
        _Drift ("How fast the gas turns over", Range(0, 1)) = 0.02

        [Header(Colour)]
        [HDR] _CoreColour ("Colour at the centre", Color) = (0.55, 0.75, 1.6, 1)
        [HDR] _ShellColour ("Colour at the rim", Color) = (1.5, 0.42, 0.28, 1)
        _ColourMix ("How much the ramp overrides the plate", Range(0, 1)) = 0.18
        _RampStart ("Where the rim colour starts", Range(0, 1)) = 0.10
        _RampEnd ("Where the rim colour wins", Range(0, 1)) = 0.90
        _ColourScale ("Size of the colour regions", Range(0.2, 6)) = 0.85
        _Saturation ("Colour saturation", Range(0, 3)) = 1.55

        [Header(Lighting)]
        // Off by default (_LightStrength 0), because most of these objects are self-luminous: an emission
        // nebula glows because its own gas is ionised, and lighting it from a direction would be a lie.
        // The Pillars are the exception and the reason this exists - they are opaque dust lit from outside
        // by a cluster off the top of frame, and they only make sense with a direction.
        _LightDirection ("Direction the light comes from", Vector) = (0, 1, 0.35, 0)
        [HDR] _LightColour ("Light colour", Color) = (1.4, 1.15, 0.85, 1)
        _LightStrength ("How much of the gas is lit rather than glowing", Range(0, 3)) = 0
        _ShadowSteps ("Shadow march steps", Range(0, 12)) = 5
        _ShadowDensity ("How hard the shadows are", Range(0, 8)) = 2.2

        [Header(Flow)]
        _Streak ("How far the gas streaks outward", Range(0, 0.9)) = 0.44

        [Header(Ray)]
        _Dither ("Ray start dither", Range(0, 2)) = 0.75
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

            // The detail and the warp are the expensive half of this shader - six noise evaluations per
            // step, eight hashes each - and they are the half a mobile GPU cannot afford. Compiled out
            // rather than multiplied by zero, because a zero strength still pays for the evaluation.
            #pragma multi_compile _ NEBULA_CHEAP

            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX3D(_Volume);
            float _Steps;
            float _Density;
            float _Emission;
            float _Extinction;

            float _Floor;
            float _Contrast;
            float _DetailScale;
            float _DetailStrength;
            float _WarpScale;
            float _WarpStrength;
            float _Drift;

            float4 _CoreColour;
            float4 _ShellColour;
            float _ColourMix;
            float _RampStart;
            float _RampEnd;
            float _ColourScale;
            float _Saturation;
            float _Streak;

            float4 _LightDirection;
            float4 _LightColour;
            float _LightStrength;
            float _ShadowSteps;
            float _ShadowDensity;

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

            // ---------------------------------------------------------------- noise
            //
            // Value noise rather than gradient noise: one hash per corner instead of a dot product per corner,
            // and at the frequencies used here the difference is not visible through gas. Everything below
            // runs 28-odd times per pixel, so the cheap version is the right version.

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);    // smoothstep, so there are no creases on the lattice

                float n000 = Hash(i + float3(0, 0, 0));
                float n100 = Hash(i + float3(1, 0, 0));
                float n010 = Hash(i + float3(0, 1, 0));
                float n110 = Hash(i + float3(1, 1, 0));
                float n001 = Hash(i + float3(0, 0, 1));
                float n101 = Hash(i + float3(1, 0, 1));
                float n011 = Hash(i + float3(0, 1, 1));
                float n111 = Hash(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);

                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            float Fbm3(float3 p)
            {
                float sum = 0.5 * ValueNoise(p);
                p *= 2.03;
                sum += 0.25 * ValueNoise(p);
                p *= 2.01;
                sum += 0.125 * ValueNoise(p);
                return sum / 0.875;
            }

            float Fbm2(float3 p)
            {
                float sum = 0.5 * ValueNoise(p);
                p *= 2.03;
                sum += 0.25 * ValueNoise(p);
                return sum / 0.75;
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

            // How much of the light from _LightDirection survives to this point.
            //
            // A short march towards the source, accumulating the baked density on the way. Five steps is
            // enough for dust: what matters is whether there is a column in the way, not the exact optical
            // depth of it. This is what makes the Pillars read as pillars - their whole shape is the shadow
            // of a dense head, and without it they are three lumps of fog that happen to be column shaped.
            float LightReaching(float3 position, float radius)
            {
                int steps = (int)_ShadowSteps;
                if (steps <= 0 || _LightStrength <= 0.001) return 1.0;

                float3 toLight = normalize(_LightDirection.xyz + 1e-5);
                float stepSize = (radius * 2.0) / max(steps, 1);
                float blocked = 0;

                [loop]
                for (int s = 1; s <= steps; s++)
                {
                    float3 at = position + toLight * (stepSize * s);
                    float3 uvw = at / (radius * 2.0) + 0.5;

                    // Outside the box there is nothing left to block the light.
                    if (any(uvw < 0.0) || any(uvw > 1.0)) break;

                    blocked += UNITY_SAMPLE_TEX3D(_Volume, uvw).a * stepSize;
                }

                return exp(-blocked * _ShadowDensity);
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

                // White noise from the 3D hash rather than interleaved gradient noise.
                //
                // IGN is the better dither for a full-resolution effect, but it is a regular pattern, and at
                // this step count over a long span it stopped hiding the banding and started showing itself:
                // the exterior view had a visible mesh across the gas. A hash of the pixel and the frame has
                // no pattern to see - it reads as film grain, which the eye forgives and bloom softens.
                float jitter = frac(Dither(pixel)
                    + Hash(float3(17.0, 43.0, floor(_Time.y * _StepJitterSpeed * 60.0)))) * _Dither;

                float3 position = originLocal + direction * (near + jitter * stepSize);

                // Slow enough that it is never seen moving, fast enough that a long look is not a photograph.
                float drift = _Time.y * _Drift;

                float3 light = 0;
                float transmittance = 1;

                [loop]
                for (int step = 0; step < steps; step++)
                {
                    // Local space is -radius..radius; the volume is sampled in 0..1.
                    float3 uvw = position / (_Radius * 2.0) + 0.5;
                    float4 baked = UNITY_SAMPLE_TEX3D(_Volume, uvw);

                    if (baked.a > 0.002)
                    {
                        // Normalised position, so the noise does not change scale with the volume's radius.
                        float3 q = position / max(_Radius, 1e-3);

                        // Domain warp: sample one noise field to displace the point at which a second is
                        // read. This is the step that turns round clumps into ribbons and sheets - the
                        // structure a nebula actually has - and it is why there is black between the gas
                        // instead of an even haze.
                        float3 warp = float3(
                            Fbm2(q * _WarpScale + float3(11.5, 3.1, 7.7) + drift),
                            Fbm2(q * _WarpScale + float3(31.7, 17.3, 2.9) - drift),
                            Fbm2(q * _WarpScale + float3(57.3, 41.9, 23.1) + drift * 0.5)) * 2.0 - 1.0;

#ifdef NEBULA_CHEAP
                        // Cheap path: the baked field only, no warp and no procedural detail. It loses the
                        // filaments and keeps the shape, which is the right thing to lose first - a smooth
                        // cloud still reads as a nebula, and an empty frame does not.
                        float density = baked.a;
                        float detail = 0.5;
#else
                        // Streak the sampling space outward from the centre.
                        //
                        // Isotropic noise gives isotropic gas - clouds that are the same in every direction,
                        // which is not what an expanding remnant or an ionised cavity looks like. The material
                        // was thrown outward, so the filaments run outward. Compressing the radial component
                        // of the sample position stretches every feature along that axis, which is the cheapest
                        // possible anisotropy: no extra noise evaluations, three dot products.
                        float3 outward = normalize(q + 1e-5);
                        float along = dot(q, outward);
                        float3 across = q - outward * along;
                        float3 flow = across + outward * along * (1.0 - _Streak);

                        float detail = Fbm3((flow + warp * _WarpStrength) * _DetailScale);

                        // The detail multiplies rather than adds, so it can empty a region completely -
                        // adding would only ever brighten, and the voids are the point.
                        float density = baked.a * lerp(1.0 - _DetailStrength, 1.0 + _DetailStrength, detail);
#endif

                        // Cut the bottom off and bend what is left. Without this every voxel has a little gas
                        // in it, every ray accumulates something, and the result is grey mist in every
                        // direction however good the noise above is.
                        density = saturate((density - _Floor) / max(1.0 - _Floor, 1e-3));
                        density = pow(density, _Contrast) * _Density;

                        if (density > 0.001)
                        {
                            // Hot in the middle, cool at the rim - the two-colour structure every real
                            // emission nebula has, because the ionising star is in the centre. The plate's
                            // own colour is still there underneath; _ColourMix says how much the ramp wins.
                            //
                            // Two things decide which end of the ramp a piece of gas takes. Radius, because
                            // the ionising stars are in the middle; and the detail noise, because a nebula is
                            // not an onion - it is interleaved ribbons at different excitations, and blending
                            // on radius alone from inside a hollow shell gives one colour in every direction,
                            // which is what the first version of this did.
                            float radial = saturate(length(q));

                            // Colour comes from its OWN low-frequency field, not from the detail noise.
                            //
                            // Blending on the detail meant the colour alternated at the frequency of the
                            // filaments themselves - every ribbon a different hue from its neighbour, which
                            // averages to mud at any distance. Real nebulae, and the reference art, put one
                            // colour across a whole region and another across the next: a blue side and an
                            // orange side, metres apart, because excitation follows where the hot stars are
                            // and not where every wisp happens to be. The term is centred on zero so it can
                            // push the blend both ways; added, it only ever drove towards the rim colour.
#ifdef NEBULA_CHEAP
                            float region = 0.5;
#else
                            float region = Fbm2(q * _ColourScale + float3(5.1, 9.7, 2.3));
#endif
                            float mixT = smoothstep(_RampStart, _RampEnd,
                                radial * 0.46 + (region - 0.48) * 1.35);
                            float3 ramp = lerp(_CoreColour.rgb, _ShellColour.rgb, mixT);

                            // The ramp carries the hue and the plate carries the brightness. Multiplying the
                            // plate by a red ramp - the obvious thing, and the wrong one - cannot ever produce
                            // blue, so every nebula came out one colour however the palette was set.
                            float lum = max(dot(baked.rgb, float3(0.2126, 0.7152, 0.0722)), 1e-4);
                            float3 colour = lerp(baked.rgb, ramp * lum, _ColourMix);

                            // Emission gas is not pastel. Its light is a few narrow lines, so what reaches the
                            // eye is far more saturated than any photograph of it - plates are stretched hard
                            // to show faint structure and stretching flattens colour towards grey. Pushing it
                            // back is most of the difference between the reference art and a washed-out
                            // version of the same geometry.
                            float grey = dot(colour, float3(0.2126, 0.7152, 0.0722));
                            colour = max(lerp(grey.xxx, colour, _Saturation), 0.0);

                            // Lit rather than glowing, where the object calls for it. Mixed in rather than
                            // replacing the emission, because even the Pillars have ionised rims that do
                            // genuinely emit - it is the opaque body of the column that is only ever lit.
                            if (_LightStrength > 0.001)
                            {
                                float lit = LightReaching(position, _Radius);
                                float3 direct = colour * _LightColour.rgb * lit;
                                colour = lerp(colour, direct, saturate(_LightStrength));
                            }

                            float absorbed = exp(-density * _Extinction * stepSize);
                            // Emission integrated over the step rather than point-sampled, so the result does
                            // not change brightness when the step count does.
                            light += transmittance * colour * density * _Emission * stepSize;
                            transmittance *= absorbed;
                            if (transmittance < 0.01) break;
                        }
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
