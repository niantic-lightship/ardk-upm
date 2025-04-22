Shader "Lightship/Editor/Custom/RotateTextureShader"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Rotation ("Rotation", Float) = 0.0
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _Rotation;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.texcoord - 0.5;
                float cosTheta = cos(_Rotation);
                float sinTheta = sin(_Rotation);
                float2x2 rotationMatrix = float2x2(cosTheta, -sinTheta, sinTheta, cosTheta);
                uv = mul(rotationMatrix, uv);
                uv += 0.5;
                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
