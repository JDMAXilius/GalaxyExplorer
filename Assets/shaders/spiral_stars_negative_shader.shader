// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
Shader "Galaxy/StarsNeg"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_WSScale ("World Space Scale", Float) = 1
		_TxScale ("TX Scale", Vector) = (0,0,0,0)
		[HDR] _Color ("Color", Color) = (1,1,1,1)
		_TransitionAlpha("Transition Alpha", Float) = 1
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100


		Pass
		{
			Blend One OneMinusSrcAlpha
			ZWrite Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5
			
			#include "UnityCG.cginc"
			#include "cginc/StarVertDescriptor.cginc"
			#include "cginc/StarPositionCompute.cginc"
			#include "cginc/StarQuad.cginc"

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR0;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			float _WSScale;
			float4 _TxScale;

			float3 _LocalCamDir;

			float4 _Color;
			float _TransitionAlpha;

			StructuredBuffer<StarVertDescriptor> _Stars;

			v2f vert (uint vid : SV_VertexID)
			{
				uint starIndex, corner;
				StarQuadCorner(vid, starIndex, corner);
				StarVertDescriptor star = _Stars[starIndex];

				v2f o;
				float3 pos = ComputeStarPosition(star);
				float fade = saturate(dot(normalize(pos), _LocalCamDir));

				float4 clipPos = UnityObjectToClipPos(float4(pos, 1));
				o.vertex = StarQuadOffset(clipPos, corner, star.size * _WSScale);
				o.color = float4(star.color, 1) * fade * _TransitionAlpha;
				o.uv = StarQuadUVs[corner] * 0.5 + star.uv;

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float4 color = tex2D(_MainTex, i.uv + float2(0,.5)).a * i.color;
				color.a = dot(color.xyz, 1) / 3.0;

				clip(color.a - 0.05);

				return color * _Color;
			}
			ENDCG
		}
	}
}