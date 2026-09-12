// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
Shader "Sun/LensFlare"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		[HDR] _Tint ("Tint Color", Color) = (1,1,1,1)

		_FadeParams ("Object Space Fade Begin (X) and Fade End (Y)", Vector) = (0,1,0,0)

		_TransitionAlpha("TransitionAlpha", Float) = 1
		_DistFromCamera("DistFromCamera", Float) = 2
	}
	SubShader
	{
		Tags{ "RenderType" = "Transparent" "Queue" = "Geometry+100" }
		Blend One One
		ZWrite Off
		ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float fade : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			float4 _Tint;
			float _TransitionAlpha;
			float4 _FadeParams;

			float _DistFromCamera;
			float _CurrentScale;
			
			v2f vert (appdata v)
			{
				// The v2f declaration was moved to the top so the stereo setup can sit where it does in
				// every other shader here: UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO needs the output declared,
				// and the pair has to precede any eye-dependent matrix read.
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				float2 fadeParams = _FadeParams.xy * _CurrentScale;

				float fade = saturate((_DistFromCamera - fadeParams.x) / (fadeParams.y - fadeParams.x));

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.fade = fade;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv).aaaa * _Tint * _TransitionAlpha * i.fade;

				return col;
			}
			ENDCG
		}
	}
}
