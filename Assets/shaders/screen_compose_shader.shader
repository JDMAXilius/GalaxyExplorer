// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// No stereo macros here, on purpose (CS-096). This shader only runs on the galaxy's render-to-texture
// compose chain, and that chain is switched off whenever a headset is attached: DrawStars computes
// _useDownscaledTarget as `renderIntoDownscaledTarget && !XRSettings.isDeviceActive`, because the
// down-scaled targets are plain 2D RenderTextures and cannot feed a stereo eye texture. Under XR every
// galaxy layer draws straight into the eye buffer and neither this material nor its Blits are touched.
// Only pass 0 has a caller (the two CommandBuffer.Blit calls in DrawStars.UpdateCommandBuffer); passes
// 1-3, the box blends, have no consumer anywhere in the project.
// So adding the macros to four passes would be an unverifiable change to dead-on-XR code. If the
// down-scaled path is ever revived for XR, the fix is a per-eye RenderTexture array plus XRSettings-aware
// Blits in DrawStars -- macros on this shader alone would not make it correct.
Shader "Galaxy/ScreenCompose"
{
	Properties { _MainTex ("Main Texture", any) = "" {} }

	SubShader 
	{ 
		// Normal pass
		Pass 
		{
			ZWrite Off
			ZTest LEqual
			Blend One Zero

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			
			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
//				o.vertex.z = 1;
				o.texcoord = v.texcoord.xy;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				return tex2D(_MainTex, i.texcoord);
			}
			ENDCG 

		}
		
		// Box blend pass
		Pass 
		{
 			Cull Front
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;

			float4 _Color;
			
			struct appdata_t 
			{
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float4 uvproj : TEXCOORD0;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				o.uvproj.xy = TRANSFORM_TEX(o.vertex, _MainTex);
				o.uvproj.zw = o.vertex.zw;

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{ 
				i.uvproj /= i.uvproj.w;
				i.uvproj = i.uvproj * .5 + .5;

				float4 color = tex2D(_MainTex, i.uvproj);
				return float4(color.xyz, dot(color.xyz, .5));
			}
			ENDCG 
		}

		// Box blend pass when in the UnityEditor camera
		Pass 
		{
 			Cull Front
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;

			float4 _Color;
			
			struct appdata_t 
			{
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float4 uvproj : TEXCOORD0;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				o.uvproj.xy = TRANSFORM_TEX(o.vertex, _MainTex);
				o.uvproj.zw = o.vertex.zw;

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{ 
				i.uvproj /= i.uvproj.w;
				i.uvproj = (i.uvproj + 1) * 0.5;

				// This is what the UnityEditor camera needs
				i.uvproj.y = 1 - i.uvproj.y;

				float4 color = tex2D(_MainTex, i.uvproj);
				return float4(color.xyz, dot(color.xyz, .6));
			}
			ENDCG 
		}

		// Box blend pass when in Spectator view
		Pass
		{
			Cull Front
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;

			float4 _Color;

			struct appdata_t
			{
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float4 uvproj : TEXCOORD0;
			};

			v2f vert(appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				o.uvproj.xy = TRANSFORM_TEX(o.vertex, _MainTex);
				o.uvproj.zw = o.vertex.zw;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				i.uvproj /= i.uvproj.w;
				i.uvproj = i.uvproj * .5 + .5;
				// spectatorview...
				i.uvproj.y = 1 - i.uvproj.y;

				float4 color = tex2D(_MainTex, i.uvproj);
				return float4(color.xyz, dot(color.xyz, .5));
			}
			ENDCG
		}
	}
	Fallback Off 
}