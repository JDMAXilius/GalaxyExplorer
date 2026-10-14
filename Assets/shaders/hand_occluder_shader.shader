// Licensed under the MIT License. See LICENSE in the project root for license information.

// Depth only, drawn just before the opaque content (CS-288). Laid on the tracked hand mesh while the room shows
// through, it makes a real hand between the eye and a planet hide the planet: nothing is drawn where the hand is,
// so the camera's clear alpha of 0 lets the passthrough hand show instead. The outline shader's own depth pass
// runs in the transparent queue, after the planets, so it cannot do this.
//
// HandLook adds and removes this material; nothing else should. Kept out of full VR, where there is no room to
// show and the hole would read as black.
Shader "CosmicSimulation/HandOccluder"
{
    SubShader
    {
        Tags { "Queue" = "Geometry-1" "RenderType" = "Opaque" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Occlude"
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }

    Fallback Off
}
