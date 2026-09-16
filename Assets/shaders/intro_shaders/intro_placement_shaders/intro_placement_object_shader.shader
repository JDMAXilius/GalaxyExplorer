Shader "GalaxyExplorer/Placement"
{
    Properties
    {
        _BaseColor("Base color", Color) = (1.0, 1.0, 1.0, 1.0)
        _VariantColor("Variant color", Color) = (1.0, 1.0, 1.0, 1.0)
        _TouchColor("Touch color", Color) = (1.0, 1.0, 0.0, 1.0)
        _ActiveColor("Active color", Color) = (0.0, 0.0, 1.0, 1.0)
        _Size("Point Size", Float) = .02
        _Speed("Rotation Speed", Float) = 2.0
        _Blend("Reveal", Range(0,1)) = 1
        [Toggle]_Active("Active", Float) = 0
        [HideInInspector]_SelfTime("Time", Float) = 0.0
        _Bands("Voice bands, low to high", Vector) = (0, 0, 0, 0)
        _Articulate("Articulation", Float) = 0
        _BandExtent("Half height of the source mesh", Float) = 0.5
        _Density("Share of points shown", Range(0,1)) = 1
        _Inward("Pull into the sphere", Range(0,1)) = 0
        _ClipFadeDistance("Camera clip fade distance units", Float) = .1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4                // "LessEqual"
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "LightMode" = "ForwardBase"}

        pass
        {

            ZWrite Off
            ZTest[_ZTest]
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing

            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            float4 _BaseColor;
            float4 _VariantColor;
            float4 _TouchColor;
            float4 _ActiveColor;
            float _Size;
            float _Active;
            float _Speed;
            float _Blend;
            float _ClipFadeDistance;
            float _SelfTime;
            float4 _Bands;
            float _Articulate;
            float _BandExtent;
            float _Density;
            float _Inward;

            float4 _ProximityLightData[2*6];

            struct appdata
            {
                float4 vertex : POSITION;
                float4 randoms : TEXCOORD0;
                float corner : TEXCOORD1; // 0 bottom, 1 right, 2 left, 3 top
//                float3 normal : NORMAL;
//                float3 randBlend : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            
            // inverseW is to counteract the effect of perspective-correct interpolation so that the lines
            // look the same thickness regardless of their depth in the scene.
            struct g2f
            {
                float4 clipPos : SV_POSITION;
                float4 randoms : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
                float2 proximity_size : TEXCOORD2;
                float shown : TEXCOORD3;
//                float3 normal : NORMAL;
//                float3 randBlend :TEXCOORD0;
//                float clip : TEXCOORD1;
//                float4 light :TEXCOORD2;
//                float4 color : COLOR;
                // LIGHTING_COORDS(2,3)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // Quad corner offsets and UVs, matching the geometry shader this replaces (Meta Quest's driver doesn't
            // support geometry shaders with multiview stereo).
            static const float2 SparkleOffsets[4] = { float2(0, -1), float2(1, 0), float2(-1, 0), float2(0, 1) };
            static const float2 SparkleUVs[4] = { float2(-1, -1), float2(1, -1), float2(-1, 1), float2(1, 1) };

            float4x4 rotationMatrix(float3 axis, float angle)
            {
                axis = normalize(axis);
                float s = sin(angle);
                float c = cos(angle);
                float oc = 1.0 - c;
                return float4x4(oc * axis.x * axis.x + c, oc * axis.x * axis.y - axis.z * s, oc * axis.z * axis.x + axis.y * s,                                      0.0,
                                oc * axis.x * axis.y + axis.z * s,                           oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,  0.0,
                                oc * axis.z * axis.x - axis.y * s,                           oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c,           0.0,
                                0.0,                                                         0.0,                                0.0,                                1.0);
            }

            g2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                g2f o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

//                float3 axis = v.randoms.xyz*2-1;
//                axis.y = -abs(axis.y);
                float3 axis = float3(0,-1,0);
                float4 rotated = mul(rotationMatrix(axis, _SelfTime*v.randoms.x*_Speed), v.vertex);

                // The voice, as shape: the sphere is four belts from bottom to top, bass to treble, and each
                // belt swells with its own band, so a syllable moves a region rather than the whole ball.
                float belt = saturate(v.vertex.y / max(_BandExtent, 1e-4) * 0.5 + 0.5);
                float4 pick = float4(belt < .25, belt >= .25 && belt < .5, belt >= .5 && belt < .75, belt >= .75);
                rotated.xyz *= 1 + _Articulate * dot(pick, _Bands);

                // The being's speaking pose (owner's direction, 16 Sep): points fall into the sphere rather than
                // pushing out of it, each to its own depth, harder where its belt is loud, so the shell fills in
                // like the intro's ball. The intro leaves _Inward at 0 and _Density at 1 and is unchanged.
                float depth = _Inward * frac(v.randoms.w * 7.31 + v.randoms.x) * (0.55 + 0.45 * dot(pick, _Bands));
                rotated.xyz *= 1 - saturate(depth);
                o.shown = saturate((_Density - v.randoms.w) * 25 + 1);
                float3 worldPos = mul(unity_ObjectToWorld, rotated);
                
                o.proximity_size.x = saturate(.2-min(distance(worldPos, _ProximityLightData[0]), distance(worldPos, _ProximityLightData[6])))*5;
                rotated *= 1+.2*o.proximity_size.x+.05*_Active;
                o.color = lerp(lerp(lerp(_BaseColor, _VariantColor, v.randoms.z), _ActiveColor , _Active), _TouchColor, o.proximity_size.x);
                
				float size = _Size * clamp(v.randoms.y, .2, 1) * (1+.2*o.proximity_size.x+.2*_Active);
				size *= size;
				o.proximity_size.y = size;
				
                o.clipPos = UnityObjectToClipPos(rotated);
                o.randoms = v.randoms;

                // Camera-facing sparkle: offset this vertex to its quad corner in clip space.
                uint corner = (uint)v.corner;
                float quadSize = o.proximity_size.y * o.clipPos.w;
                o.clipPos.x += SparkleOffsets[corner].x * quadSize * UNITY_MATRIX_P._11;
                o.clipPos.y += SparkleOffsets[corner].y * quadSize * UNITY_MATRIX_P._22;
                o.uv = SparkleUVs[corner];
//                o.normal = UnityObjectToWorldNormal(v.normal);
//                o.randBlend = float3(v.randBlend.xy, step(v.randBlend.z, _Blend));
//                o.clip = saturate((-UnityObjectToViewPos(v.vertex).z-_ProjectionParams.y)/_ClipFadeDistance);

                return o;
            }

            float3 TransformHSV(
                float3 c,  // color to transform
                float h,          // hue shift (in degrees)
                float s,          // saturation multiplier (scalar)
                float v           // value multiplier (scalar)
            ) {
                float vsu = v*s*cos(h*UNITY_PI/180);
                float vsw = v*s*sin(h*UNITY_PI/180);

                float3 ret;
                ret.r = (.299*v + .701*vsu + .168*vsw)*c.r
                    +   (.587*v - .587*vsu + .330*vsw)*c.g
                    +   (.114*v - .114*vsu - .497*vsw)*c.b;
                ret.g = (.299*v - .299*vsu - .328*vsw)*c.r
                    +   (.587*v + .413*vsu + .035*vsw)*c.g
                    +   (.114*v - .114*vsu + .292*vsw)*c.b;
                ret.b = (.299*v - .300*vsu + 1.25*vsw)*c.r
                    +   (.587*v - .588*vsu - 1.05*vsw)*c.g
                    +   (.114*v + .886*vsu - .203*vsw)*c.b;
                return ret;
            }



            float4 frag(g2f i) : COLOR
            {
                float l = length(abs(i.uv));
                float a = (1-l) * (1-step(1, l));
//                float a = saturate(1-l);
//                a *= a;
//                return fixed4(i.color.rgb, tex2D(_MainTex, i.uv).a);
                return fixed4(i.color.rgb, a*_Blend*i.shown);
//                return fixed4(i.uv, 0, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
