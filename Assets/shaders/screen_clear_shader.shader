// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// No stereo macros here, on purpose (CS-096). Two reasons, either of which is sufficient.
// It belongs to the galaxy's render-to-texture compose chain, which DrawStars disables whenever a headset
// is attached (`renderIntoDownscaledTarget && !XRSettings.isDeviceActive`) because a 2D RenderTexture
// cannot feed a stereo eye texture. And nothing reads its material any more: milky_way_prefab still
// serializes a `screenClearMaterial` key on SpiralGalaxy, but that field no longer exists in
// SpiralGalaxy.cs, so the value is a stale YAML leftover that Unity will drop the next time the prefab is
// saved. This shader is therefore a deletion candidate rather than a stereo fix -- see the CS-096 note.
Shader "Galaxy/ScreenClear"
{
	SubShader 
	{ 
		// Normal pass
		Pass 
		{
 			ZTest Always Cull Off
			ZWrite On
			Blend One Zero

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.vertex.z = 1;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				return 0;
			}
			ENDCG 

		}
	}
	Fallback Off 
}