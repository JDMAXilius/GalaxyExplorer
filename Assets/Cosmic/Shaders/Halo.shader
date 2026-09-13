Shader "Cosmic/Halo"
{
	Properties
	{
		[HDR] _Color ("Halo Color", Color) = (1,1,1,1)
		_MainTex ("Texture", 2D) = "white" {}
		_SunDirection("Sun Direction", Vector) = (1,1,1,0)
		[HDR] _AmbientColor ("Ambient Color", Color) = (0,0,0,0)
		_TransitionAlpha("TransitionAlpha", Float) = 1
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry+70" }
		LOD 100
		Blend SrcAlpha One
		ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "cginc/NearClip.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;

				float3 normal : NORMAL0;
				float4 tangent : TANGENT0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float  clipAmount : TEXCOORD2;
				float3 normal : NORMAL0;
				float3 tangent : TANGENT0;
				float3 binormal : TANGENT1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;

			float3 _SunDirection;
			float4 _AmbientColor;
			float _TransitionAlpha;

			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				float3 wPos = mul(unity_ObjectToWorld, v.vertex);
				o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
				o.tangent = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
				o.binormal = normalize(cross(o.normal, o.tangent) * v.tangent.w);
				o.clipAmount = CalcVertClipAmount(wPos);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				fixed4 col = tex2D(_MainTex, i.uv);

				float3 textureNormal = col.xyz * 2 - 1;
				float3x3 tangent2World = float3x3(i.tangent, i.binormal, i.normal);
				float3 worldNormal = normalize(mul(textureNormal, tangent2World));

				float lightAmount = saturate(dot(worldNormal, _SunDirection));
				float4 color = _Color * col.a * lightAmount + _AmbientColor * col.a;

				min16float4 finalColor = min16float4(color.xyz, color.a * _TransitionAlpha);

				return ApplyVertClipAmount(finalColor, i.clipAmount);
			}
			ENDCG
		}
	}
}
