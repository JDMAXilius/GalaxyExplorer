// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Planets/OrbitalTrail" 
{
	Properties
	{
		[HDR] _Color("Color", Color) = (1,1,1,1)
		[HDR] _PlanetHighlightColor ("Highlight Color", Color) = (0,0,0,0)

		_MainTex("Falloff Texture", 2D) = "white" {}
		_TransitionAlpha("Transition Alpha", Float) = 1
		_Truthfulness("Truthfulness (0 = schematic, 1 = real)", Float) = 1
		_Width ("Width", Float) = .012

		_GlobalScale ("Global Scale", Float) = 1
		_FadeOffDistanceAroundPlanet ("FadeOff Distance Around Planet", Float) = 1
		_TrailTailAngle ("Tail Angle Offset", Float) = 1
	}

	SubShader
	{
		Tags
		{ 
			"Queue" = "Transparent" 
			"RenderType" = "Transparent" 
		}
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off


			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 4.5
				#pragma fragmentoption ARB_precision_hint_fastest
				#pragma multi_compile _ IN_TRANSITION REALSCALE SCHEMATIC

				#include "UnityCG.cginc"
				#include "cginc/NearClip.cginc"

#define MAX_ORBIT 9

				struct OrbitDataPoint
				{
					float3 realPos;
					float3 schematicPos;
					uint globalIndex;

					uint orbitStartIndex;
					uint orbitEntryCount;
					uint orbitIndex;
				};

				// SV_VertexID is the only real geometry input -- every segment is expanded from _OrbitsData. It has to
				// move into a struct so UNITY_VERTEX_INPUT_INSTANCE_ID has somewhere to live: under single-pass
				// instanced stereo the eye index arrives as the instance id, and UNITY_SETUP_INSTANCE_ID reads it off
				// the input struct. A bare `uint vid : SV_VertexID` parameter gives it nowhere to arrive. Same shape as
				// cosmic_web_points_shader, which shares this draw path.
				struct appdata
				{
					uint vid : SV_VertexID;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f 
				{
					float4 vertex : SV_POSITION;
					float2 texCoord : TEXCOORD1;
					float3 wPos : TEXCOORD2;
					float4 planetPosAndRadius : TEXCOORD3;
					float3 nextDirection : TEXCOORD4;
					float  clipAmount : TEXCOORD5;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				float4 _Color;
				float4 _PlanetHighlightColor;

				sampler2D _MainTex;
				float4 _MainTex_ST;
				float _TransitionAlpha;

				float _Truthfulness;

				float4x4 _Orbits2World;
				float _Width;

				StructuredBuffer<OrbitDataPoint> _OrbitsData;

				float _FadeOffDistanceAroundPlanet;
				float _GlobalScale;
				float _TrailTailAngle;

				float4 planetPositionsAndRadius0;
				float4 planetPositionsAndRadius1;
				float4 planetPositionsAndRadius2;
				float4 planetPositionsAndRadius3;
				float4 planetPositionsAndRadius4;
				float4 planetPositionsAndRadius5;
				float4 planetPositionsAndRadius6;
				float4 planetPositionsAndRadius7;
				float4 planetPositionsAndRadius8;


				uint tIndex(uint index, OrbitDataPoint origin)
				{
					return origin.orbitStartIndex + (index + origin.orbitEntryCount) % origin.orbitEntryCount;
				}

				float4 selectPlanet(int index)
				{
					[flatten]
					switch (index)
					{
						default:
						case 0: return planetPositionsAndRadius0;
						case 1: return planetPositionsAndRadius1;
						case 2: return planetPositionsAndRadius2;
						case 3: return planetPositionsAndRadius3;
						case 4: return planetPositionsAndRadius4;
						case 5: return planetPositionsAndRadius5;
						case 6: return planetPositionsAndRadius6;
						case 7: return planetPositionsAndRadius7;
						case 8: return planetPositionsAndRadius8;
					}
				}

				// Each orbit point draws the segment to the next point as a quad (two triangles, six vertices),
				// mitred with the neighbouring segments. This used to be a geometry shader, which Meta Quest's GPU
				// driver does not support together with multiview stereo.
				static const uint SegmentStripIndex[6] = { 0, 1, 2, 2, 1, 3 };

				v2f vert(appdata v)
				{
					// The output is declared up here, not down beside the first o.* write, because UNITY_MATRIX_VP below
					// is unity_StereoMatrixVP[unity_StereoEyeIndex] under single-pass instanced -- the eye index has to be
					// set before the mvp is built, and UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO needs the struct to exist.
					// Do not tidy this back down to where it was.
					v2f o = (v2f)0;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_TRANSFER_INSTANCE_ID(v, o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					uint id = v.vid / 6;
					uint corner = SegmentStripIndex[v.vid % 6];

					OrbitDataPoint p1 = _OrbitsData[id];

					OrbitDataPoint p0 = _OrbitsData[tIndex(p1.globalIndex - 1, p1)];
					OrbitDataPoint p2 = _OrbitsData[tIndex(p1.globalIndex + 1, p1)];
					OrbitDataPoint p3 = _OrbitsData[tIndex(p1.globalIndex + 2, p1)];

					float4x4 mvp = mul(UNITY_MATRIX_VP, _Orbits2World);

#if IN_TRANSITION
					float truthfulness = _Truthfulness;
#elif REALSCALE
					const float truthfulness = 1;
#else // SCHEMATIC
					const float truthfulness = 0;
#endif

					float3 pos0 = lerp(p0.schematicPos, p0.realPos, truthfulness);
					float3 pos1 = lerp(p1.schematicPos, p1.realPos, truthfulness);
					float3 pos2 = lerp(p2.schematicPos, p2.realPos, truthfulness);
					float3 pos3 = lerp(p3.schematicPos, p3.realPos, truthfulness);

					float3 wPos0 = mul(_Orbits2World, float4(pos1, 1)).xyz;
					float3 wPos1 = mul(_Orbits2World, float4(pos2, 1)).xyz;

					float4 points[4];
					points[0] = mul(mvp, float4(pos0, 1));
					points[1] = mul(mvp, float4(pos1, 1));
					points[2] = mul(mvp, float4(pos2, 1));
					points[3] = mul(mvp, float4(pos3, 1));

					float2 correctPoints[4];
					[unroll]
					for (int i = 0; i < 4; i++)
					{
						correctPoints[i] = points[i].xy / abs(points[i].w);
					}

					float2 direction0 = normalize(correctPoints[1] - correctPoints[0]);
					float2 direction1 = normalize(correctPoints[2] - correctPoints[1]);
					float2 direction2 = normalize(correctPoints[3] - correctPoints[2]);

					// Screen-space sides (perpendicular to the averaged tangent) at the segment's start and end.
					float2 tangentStart = normalize(direction1 + direction0);
					float2 tangentEnd = normalize(direction2 + direction1);
					float2 sideStart = float2(tangentStart.y, -tangentStart.x);
					float2 sideEnd = float2(tangentEnd.y, -tangentEnd.x);

					// Corners 0/1 sit at the segment start, 2/3 at its end; even corners on the negative side.
					bool atEnd = corner >= 2;
					float sign = (corner == 0 || corner == 2) ? -1 : 1;
					float4 basePoint = atEnd ? points[2] : points[1];
					float2 side = atEnd ? sideEnd : sideStart;

					o.vertex = float4(basePoint.xyz + sign * _Width * float3(side, 0) * basePoint.w, basePoint.w);
					o.texCoord = float2(sign < 0 ? 0 : 1, 1);
					o.wPos = atEnd ? wPos1 : wPos0;
					o.planetPosAndRadius = selectPlanet(p1.orbitIndex);
					o.nextDirection = normalize(wPos1 - wPos0);
					o.clipAmount = CalcVertClipAmount(wPos0);

					return o;
				}

				fixed4 frag(v2f i) : COLOR
				{			
					UNITY_SETUP_INSTANCE_ID(i);
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

					float3 toPlanet = i.wPos - i.planetPosAndRadius.xyz;
					float distanceFromPlanet = length(toPlanet);

					toPlanet = normalize(toPlanet);
					
					float planetDistanceOpacity = saturate(distanceFromPlanet / _FadeOffDistanceAroundPlanet / _GlobalScale);
					planetDistanceOpacity = lerp(planetDistanceOpacity, 1, dot(i.nextDirection, toPlanet) < 0);

					float4 planetTailHighlight = lerp(_PlanetHighlightColor, 0, saturate(dot(i.nextDirection, toPlanet) - _TrailTailAngle));

					min16float4 finalColor = tex2D(_MainTex, i.texCoord).aaaa * (_Color + planetTailHighlight) * planetDistanceOpacity * _TransitionAlpha;

					return ApplyVertClipAmount(finalColor, i.clipAmount);
				}
				ENDCG
			}
		}
	FallBack "Diffuse"
}
