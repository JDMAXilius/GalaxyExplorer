// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
Shader "Unlit/OccluderShader"
{
    Properties
    {
        _TransitionAlpha("Transition Alpha", Float) = 0
    }
        SubShader
        {
            Tags { "Queue" = "Overlay" }
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                // SV_POSITION, not POSITION. POSITION on a vertex *output* is a legacy alias the compiler tolerates,
                // but UNITY_VERTEX_OUTPUT_STEREO sits beside it and writes SV_RenderTargetArrayIndex, and the two only
                // agree on a properly declared clip-space output.
                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                float _TransitionAlpha;

                v2f vert(appdata v)
                {
                    v2f o;
                    // This is a full-screen fade quad, so there is no parallax to lose -- but on the single-pass
                    // instanced Android build, without the slice index every fragment lands in eye 0 and only the left
                    // eye ever fades to black. The macros are what make the fade cover both eyes, not what makes it 3D.
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    return float4(0,0,0,_TransitionAlpha);
                }
                ENDCG
            }
        }
}
