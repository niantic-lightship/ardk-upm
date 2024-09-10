// Copies camera depth texture to the target, while converting it to metric eye depth
Shader "Lightship/CopyEyeDepth"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _CameraDepthTexture;

            float4 frag (v2f i) : SV_Target
            {
              // Convert to metric eye depth
              float depth = DECODE_EYEDEPTH(tex2D(_CameraDepthTexture, i.uv).r);

              // Write eye depth to target
              return float4(depth, depth, depth, 1.0f);
            }
            ENDCG
        }
    }
}
