// Licensed under the MIT License. See LICENSE in the project root for license information.

// One plane of a layered nebula overlay (GDD 4.3, Technical Overview 7.6).
//
// A nebula is four of these parallel transparent cards a hand's width apart along the view axis. What stops
// that reading as four posters in a stack is this shader:
//
//  * Each card takes a *luminance band* out of the same plate, so the faint outer wisps and the bright core
//    end up on different planes and separate visually as the player moves. The plates are photographs with no
//    alpha channel, so alpha has to come from luminance; the band window is also what keeps the black sky
//    around the subject from being drawn at all.
//  * A radial fade kills the corners, so no card ever shows its rectangle.
//  * A near fade dissolves a card as it approaches the eye, so leaning into the cloud does not slice it.
//  * A soft-depth fade dissolves a card where it intersects other geometry, so the seam a card makes against
//    a planet, a hand mesh or the black halo behind it is invisible.
//
// The depth fade is the one thing here with an outside dependency: it needs the camera depth texture, which
// in the built-in forward renderer only exists if some component asks the camera for it, and which costs a
// depth prepass on the Quest. So it lives behind the NEBULA_SOFT_DEPTH keyword, and NebulaOverlay turns both
// the keyword and the camera's DepthTextureMode.Depth on together, only while an overlay is open. With the
// keyword off the depth texture is never sampled, and the card still gets the band, radial and near fades.
// Even with the keyword on, an unwritten depth texture reads back as a constant that lands outside the usable
// range, and the shader treats that as "no occluder" -- so the failure mode is an unfaded card, never a black
// or invisible one.
//
// Passthrough: the colour blend is ordinary alpha, but the *alpha* channel accumulates premultiplied
// (One OneMinusSrcAlpha). The compositor uses the eye buffer's alpha to decide how much of the room shows
// through, and plain SrcAlpha blending would square it, leaving the room faintly visible through the brightest
// part of the nebula.
Shader "CosmicSimulation/NebulaCard"
{
    Properties
    {
        _MainTex ("Nebula plate", 2D) = "black" {}
        _Color ("Tint and layer opacity", Color) = (1, 1, 1, 1)

        [Header(Luminance band)]
        _BandLow ("Band low", Range(0, 2)) = 0.05
        _BandHigh ("Band high", Range(0, 2)) = 0.34
        _BandFeather ("Band feather", Range(0.001, 0.5)) = 0.12
        _TextureAlphaWeight ("Use the plate's own alpha", Range(0, 1)) = 0

        [Header(Fades)]
        _RadialStart ("Edge fade start", Range(0, 1)) = 0.55
        _NearFadeStart ("Near fade start (m)", Float) = 0.12
        _NearFadeRange ("Near fade range (m)", Float) = 0.25
        _SoftDepth ("Intersection fade (m)", Float) = 0.15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile __ NEBULA_SOFT_DEPTH

            #include "UnityCG.cginc"

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
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            float _BandLow;
            float _BandHigh;
            float _BandFeather;
            float _TextureAlphaWeight;

            float _RadialStart;
            float _NearFadeStart;
            float _NearFadeRange;
            float _SoftDepth;

#ifdef NEBULA_SOFT_DEPTH
            // Expands to a Texture2DArray under single-pass instanced / multiview, and a plain sampler
            // otherwise. SAMPLE_DEPTH_TEXTURE_PROJ picks the right eye slice from unity_StereoEyeIndex.
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
#endif

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                fixed4 plate = tex2D(_MainTex, i.uv);
                float luma = dot(plate.rgb, float3(0.299, 0.587, 0.114));

                // The band this card is responsible for. The topmost card is given a high edge above 1 so its
                // core is not halved by the upper shoulder of the window.
                float band = smoothstep(_BandLow - _BandFeather, _BandLow + _BandFeather, luma) *
                             (1.0 - smoothstep(_BandHigh - _BandFeather, _BandHigh + _BandFeather, luma));

                float alpha = band * lerp(1.0, plate.a, _TextureAlphaWeight) * _Color.a;

                // Corners away, so the card has no visible rectangle.
                float radius = length(i.uv - 0.5) * 2.0;
                alpha *= 1.0 - smoothstep(min(_RadialStart, 0.999), 1.0, radius);

                // Dissolve as the card reaches the eye, so leaning in does not slice the cloud open.
                alpha *= saturate((i.eyeDepth - _NearFadeStart) / max(_NearFadeRange, 1e-4));

#ifdef NEBULA_SOFT_DEPTH
                float raw = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                float sceneZ = LinearEyeDepth(raw);

                // A depth texture that was never rendered comes back as a constant, and either constant
                // linearises to something at or in front of the near plane. Read that as "nothing occludes
                // this fragment" rather than fading the whole card away.
                float usable = step(_ProjectionParams.y * 2.0, sceneZ);
                float soft = saturate((sceneZ - i.eyeDepth) / max(_SoftDepth, 1e-4));
                alpha *= lerp(1.0, soft, usable);
#endif

                return fixed4(plate.rgb * _Color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }

    Fallback Off
}
