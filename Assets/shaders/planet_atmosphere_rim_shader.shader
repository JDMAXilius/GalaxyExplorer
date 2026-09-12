// Licensed under the MIT License. See LICENSE in the project root for license information.
//
// A planet's atmosphere seen edge on: bright where the surface turns away from you, invisible where it faces you.
// Goes on a sphere shell a couple of per cent larger than the body, so the rim shows in the gap.
//
// This exists because halo_shader cannot do it. That shader has no view-dependent term anywhere: its glow is a
// tangent-space normal map baked into glow_normal_alpha_texture and painted onto a purpose-built *_glow_mesh
// inside each planet's FBX. On a plain sphere it lights a ball instead of a limb, and no material setting can
// add a Fresnel term the shader never computes.
Shader "CosmicSimulation/PlanetAtmosphereRim"
{
	Properties
	{
		[HDR] _Color ("Rim Color", Color) = (0.372, 0.663, 0.972, 1)
		_RimPower ("Rim Falloff", Range(0.5, 12)) = 3.2
		_RimScale ("Rim Strength", Range(0, 4)) = 1.15
		_SunDirection ("Sun Direction", Vector) = (0,0,0,0)
		_SunInfluence ("Sun Influence", Range(0,1)) = 0.8
		_SunWrap ("Terminator Softness", Range(0,1)) = 0.35
		_TransitionAlpha ("TransitionAlpha", Float) = 1
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
		LOD 100

		Blend SrcAlpha One
		ZWrite Off
		ZTest LEqual
		Cull Back

		Pass
		{
			Fog { Mode Off }
			Lighting Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5
			// Without this the STEREO_INSTANCING_ON variant is never compiled, so the instancing macros below
			// do nothing and both eyes would draw with the left eye's matrices if the render mode ever changes
			// to Single Pass Instanced. Multiview does not need it; compiling it costs one variant.
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"
			#include "cginc/NearClip.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 worldNormal : TEXCOORD0;
				float3 viewOffset : TEXCOORD1;
				float clipAmount : TEXCOORD2;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			fixed4 _Color;
			float _RimPower;
			float _RimScale;
			float3 _SunDirection;
			float _SunInfluence;
			float _SunWrap;
			float _TransitionAlpha;

			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);

				// Carried to the fragment stage rather than resolved here: a Fresnel term interpolated across a
				// sphere's triangles bands worst exactly at the silhouette, which is the only place it matters.
				o.viewOffset = _WorldSpaceCameraPos - worldPos;
				o.clipAmount = CalcVertClipAmount(worldPos);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float3 normal = normalize(i.worldNormal);
				float3 view = normalize(i.viewOffset);

				float rim = pow(saturate(1.0 - saturate(dot(normal, view))), _RimPower) * _RimScale;

				// _SunDirection is written every frame by SunLightReceiver. Left at zero — no sun in the scene, a
				// body held on its own, the desktop preview — the dot product would erase the rim, so an unset
				// direction has to mean "lit all the way round" rather than "show nothing".
				float sunLength = length(_SunDirection);
				float lambert = dot(normal, _SunDirection / max(sunLength, 0.0001));
				float day = saturate((lambert + _SunWrap) / (1.0 + _SunWrap));
				day = lerp(1.0, day, step(0.0001, sunLength));

				float lit = lerp(1.0, day, _SunInfluence);
				float alpha = saturate(_Color.a * rim * lit * _TransitionAlpha);

				min16float4 finalColor = min16float4((min16float3)_Color.rgb, (min16float)alpha);
				return ApplyVertClipAmount(finalColor, (min16float)i.clipAmount);
			}
			ENDCG
		}
	}

	FallBack Off
}
