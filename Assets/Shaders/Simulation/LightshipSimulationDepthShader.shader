Shader "Custom/LightshipSimulationDepthShader"
{
  Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
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

            sampler2D_float _MainTex;

            // ZBufferParams protected by foreign camera settings
            float _ZBufferParams_Z;
            float _ZBufferParams_W;

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample depth (nonlinear)
                float depth = tex2D(_MainTex, i.uv).r;

                // Return linear eye depth
                // This is the same as LinearEyeDepth makro: https://forum.unity.com/threads/decodedepthnormal-linear01depth-lineareyedepth-explanations.608452/
                return  1.0f / (_ZBufferParams_Z * depth + _ZBufferParams_W);
            }

            ENDCG
        }
    }
}
