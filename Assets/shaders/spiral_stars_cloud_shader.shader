// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

Shader "Galaxy/StarClouds"
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
			Blend One One
			ZWrite Off
			Cull Off

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
				float3 color : COLOR0;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			float _WSScale;
			float4 _TxScale;

			float4 _Color;
			float _TransitionAlpha;

			StructuredBuffer<StarVertDescriptor> _Stars;
//			float4x4 _GalaxyWorld;

			v2f vert (uint vid : SV_VertexID)
			{
				uint starIndex, corner;
				StarQuadCorner(vid, starIndex, corner);
				StarVertDescriptor star = _Stars[starIndex];

				v2f o;
				float4 clipPos = UnityObjectToClipPos(float4(ComputeStarPosition(star), 1));
				o.vertex = StarQuadOffset(clipPos, corner, star.size * _WSScale * 2);
				o.color = (star.color * _TransitionAlpha * _Color * 0.5);
				o.color *= dot(o.color.xyz, 1);
				o.uv = StarQuadUVs[corner];

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float2 uv = i.uv * 2 - 1;

				// don't care about alpha, ScreenComposeShader will work it out from luminance
				return ((1 - dot(uv,uv)) * i.color).xyzx;
			}
			ENDCG
		}
	}
}