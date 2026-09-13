Shader "Cosmic/Atmosphere"
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
				UNITY_VERTEX_INPUT_INSTANCE_ID
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
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);

				// Fresnel is resolved per pixel: interpolated across a sphere's triangles it bands worst exactly at the silhouette, which is the only place it shows.
				o.viewOffset = _WorldSpaceCameraPos - worldPos;
				o.clipAmount = CalcVertClipAmount(worldPos);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				float3 normal = normalize(i.worldNormal);
				float3 view = normalize(i.viewOffset);

				float rim = pow(saturate(1.0 - saturate(dot(normal, view))), _RimPower) * _RimScale;

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
