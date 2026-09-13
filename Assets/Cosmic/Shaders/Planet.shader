Shader "Cosmic/Planet"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_NormalAlpha("Normal Texture", 2D) = "white" {}
		_RingsAlphaTex("Rings Alpha Tex", 2D) = "white" {}
		_SunDirection("Sun Direction", Vector) = (1,1,1,0)
		_LightAmount("Light amount (LightSide)", Vector) = (1,.2, 0, 0)
		[HDR]_LightTint("Sunlight Tint", Color) = (1,1,1,1)
		[HDR]_AmbientColor("Ambient Color", Color) = (1,1,1,1)
		[HDR]_AlbedoMultiplier("Albedo Multiplier", Color) = (1,1,1,1)
		[HDR]_NightLightColor("Night Light Color", Color) = (1,1,1,1)

		[HDR]_FresnelColor("Fresnel Color", Color) = (1,1,1,1)
		[HDR]_FresnelDarkSideColor("Fresnel Dark Side Color", Color) = (1,1,1,1)
		_FresnelTerm("Fresnel (Term, Offset)", Vector) = (0,0,0,0)

		_SpecParams("Specular (Power, Offset, Mask Multiplier)", Vector) = (60,0,0,0)
		_NormalScale("Normal Scale", Vector) = (1,1,1,1)

		_LightGammaCorrection("Light Gamma Correction (Multiplier, Power)", Vector) = (1,1,1,1)

		_OuterRingRadius("Outer Ring Radius", Float) = 1
		_InnerRingRadius("Inner Ring Radius", Float) = 0.7

		_TransitionAlpha("TransitionAlpha", Float) = 1
		_SRCBLEND("Source Blend", float) = 1
		_DSTBLEND("Destination Blend", float) = 0
		_ZWRITE("ZWrite", float) = 1
		_CULL("Cull", float) = 2
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"LightMode" = "ForwardBase"
		}

		Pass
		{
			Fog { Mode Off }
			Lighting Off
			Blend [_SRCBLEND] [_DSTBLEND]
			ZWrite [_ZWRITE]
			Cull [_CULL]

			CGPROGRAM
			#pragma target 4.5
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma shader_feature_local _ _EARTH _SATURN _ALPHA

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "cginc/NearClip.cginc"

			struct appdata
			{
				float4 vertex : POSITION;

				float3 normal : NORMAL0;
				float2 uv : TEXCOORD0;
			#ifndef _SATURN
				float4 tangent : TANGENT0;
			#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 uv : TEXCOORD0;
				float  clipAmount : TEXCOORD1;

				float4 vertex : SV_POSITION;

				float3 normal : NORMAL0;
			#ifndef _SATURN
				float3 tangent : TANGENT0;
				float3 binormal : TANGENT1;
			#endif

				float4 fresnel : TEXCOORD2;

				float specAmount : COLOR0;
			#ifdef _SATURN
				float  ringsIntersectionRadius : TEXCOORD5;
				float3 ringsToIntersection : TEXCOORD6;
				float  ringsUv : TEXCOORD7;
			#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			sampler2D _NormalAlpha;
			float4 _NormalAlpha_ST;

			float4 _SunDirection;
			float4 _LightAmount;
			float4 _NightLightColor;

			float3 _AmbientColor;
			float3 _AlbedoMultiplier;

			float4 _FresnelTerm;

			float3 _FresnelColor;
			float3 _FresnelDarkSideColor;

			float3 _SpecParams;
			float3 _NormalScale;

			float4 _LightGammaCorrection;
			float _TransitionAlpha;
			float3 _LightTint;

		#ifdef _SATURN
			sampler2D _RingsAlphaTex;
			float4 _RingsAlphaTex_ST;

			float _OuterRingRadius;
			float _InnerRingRadius;

			float3 _PlanetUp;
			float3 _PlanetCenter;
		#endif

			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				float3 wPos = mul(unity_ObjectToWorld, v.vertex);
				o.vertex = UnityObjectToClipPos(v.vertex);
			#ifdef _SATURN
				o.uv = float4(0, 0, TRANSFORM_TEX(v.uv, _MainTex));
			#else
				o.uv = float4(TRANSFORM_TEX(v.uv, _NormalAlpha), TRANSFORM_TEX(v.uv, _MainTex));
			#endif
				o.clipAmount = CalcVertClipAmount(wPos);

				o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
			#ifndef _SATURN
				o.tangent = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
				o.binormal = normalize(cross(o.normal, o.tangent) * v.tangent.w);
			#endif

				float3 camToPixel = normalize(wPos - _WorldSpaceCameraPos);
				float3 camToSunDirection = normalize(_SunDirection.xyz - camToPixel);

				min16float fresnel = saturate(dot(camToPixel, o.normal) * _FresnelTerm.x + _FresnelTerm.y);
				fresnel *= fresnel;

				o.fresnel.a = fresnel;

				min16float ndotl = -dot(o.normal, _SunDirection);
				min16float3 fresnelSideColor = lerp(_FresnelColor, _FresnelDarkSideColor, ndotl * .5f + .5f);

				o.fresnel.xyz = fresnelSideColor;

				min16float ndoth = saturate(dot(o.normal, camToSunDirection));
				o.specAmount = (pow(ndoth, (min16float)_SpecParams.x) + (min16float)_SpecParams.y) * (min16float)_SpecParams.z;

			#ifdef _SATURN
				min16float rayFromPointToPlaneIntersection = -(dot(wPos, _PlanetUp) - dot(_PlanetCenter, _PlanetUp)) / dot(_SunDirection, _PlanetUp);
				min16float3 rayFromPointIntersectionPos = wPos + rayFromPointToPlaneIntersection * _SunDirection;

				min16float3 toIntersection = rayFromPointIntersectionPos - wPos;
				o.ringsIntersectionRadius = length(rayFromPointIntersectionPos - _PlanetCenter);
				o.ringsToIntersection = toIntersection;

				min16float ringsUv = (o.ringsIntersectionRadius - (min16float)_InnerRingRadius) / ((min16float)_OuterRingRadius - (min16float)_InnerRingRadius);
				o.ringsUv = ringsUv;
			#endif

				return o;
			}

			min16float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				min16float4 albedoSpec = tex2D(_MainTex, i.uv.zw);

			#ifdef _SATURN
				min16float ringsIntersectionRadius = i.ringsIntersectionRadius;
				min16float ringsUv = i.ringsUv;

				min16float hasRingsShadow = (ringsIntersectionRadius > (min16float)_InnerRingRadius && ringsIntersectionRadius < (min16float)_OuterRingRadius);
				min16float ringsTransmissive = tex2D(_RingsAlphaTex, min16float2(ringsUv, (min16float)1)).a;

			#if LOD_FADE_CROSSFADE
				hasRingsShadow *= (min16float)unity_LODFade.x;
			#endif

				min16float ringShadow = (min16float)1 - hasRingsShadow * ringsTransmissive;

				if (dot((min16float3)i.ringsToIntersection, (min16float3)_SunDirection) < 0)
				{
					ringShadow = 1;
				}

				min16float3 worldNormal = i.normal;

				min16float ndotl = saturate(dot(worldNormal, (min16float3)_SunDirection.xyz)) * ringShadow;
			#else
				min16float4 normalAlpha = tex2D(_NormalAlpha, i.uv.xy);

				min16float3 textureNormal = (normalAlpha.xyz * (min16float)2.0f - (min16float)1.0f) * (min16float3)_NormalScale;

				// Spelled out rather than mul(textureNormal, float3x3(tangent, binormal, normal)) because that compiles to movs and dp3s where this compiles to mads.
				min16float3 tn = textureNormal.xxx * (min16float3)i.tangent;
				tn += textureNormal.yyy * (min16float3)i.binormal;
				tn += textureNormal.zzz * (min16float3)i.normal;
				min16float3 worldNormal = normalize(tn);

				min16float ndotl = saturate(dot(worldNormal, (min16float3)_SunDirection.xyz));
			#endif

				min16float gammaIntensity = pow(ndotl * _LightGammaCorrection.x, _LightGammaCorrection.y);

				min16float3 lightAmount = gammaIntensity * lerp((min16float3)_LightAmount.x, 0, ndotl * (min16float).5 - (min16float).5) * _LightTint;
				lightAmount = (max(0, lightAmount) + (min16float3)_AmbientColor.xyz);

				min16float3 baseColor = albedoSpec.xyz * (min16float3)_AlbedoMultiplier * lightAmount;

			#ifdef _EARTH
				baseColor += saturate(normalAlpha.a * (min16float3)_NightLightColor * ((min16float)1.0f - lightAmount));
			#endif

				min16float fresnel = i.fresnel.a;

			#if LOD_FADE_CROSSFADE
				fresnel *= (min16float)unity_LODFade.x;
			#endif
				min16float3 fresnelSideColor = i.fresnel.xyz;

				min16float specAmount = (min16float)i.specAmount * albedoSpec.a;

			#ifdef _ALPHA
				min16float alpha = (min16float)_TransitionAlpha * albedoSpec.a;
			#else
				min16float alpha = (min16float)_TransitionAlpha;
			#endif

				min16float4 finalColor = min16float4(specAmount.xxx + lerp(baseColor, fresnelSideColor, fresnel), alpha);

				return ApplyVertClipAmount(finalColor, i.clipAmount);
			}

			ENDCG
		}
	}
	FallBack "Diffuse"
}
