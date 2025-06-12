Shader "Unlit/LightshipScanningStripes"
{
    Properties
    {
        _MainTex ("ImageFromUnityCamera", 2D) = "white" {}
        _ColorTex ("RaycastColors", 2D) = "white" {}
        _NormalTex ("RaycastNormals", 2D) = "white" {}
        _PositionAndConfidenceTex("RaycastPositionAndConfidence", 2D) = "white" {}
        _ScreenOrientation("ScreenOrientation", int) = 0
        _StripeColor ("StripeColor", Color) = (1,0,0,1)
        _ArCameraTex ("ArCameraImage", 2D) = "white" {}
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
            fixed4 _StripeColor;
            sampler2D _MainTex;
            sampler2D _ColorTex;
            sampler2D _ArCameraTex;
            float4 _MainTex_TexelSize;
            float4 _ColorTex_TexelSize;
            float4 _ArCameraTex_TexelSize;
            int _ScreenOrientation;

            struct v2f
            {
                float2 uv : TEXCOORD0;
            };

            v2f vert (
                float4 vertex : POSITION, // vertex position input
                float2 uv : TEXCOORD0, // texture coordinate input
                out float4 outpos : SV_POSITION // clip space position output
            )
            {
                v2f o;
                o.uv = uv;
                outpos = UnityObjectToClipPos(vertex);
                return o;
            }

            inline float2 GetSampleUV(float2 uv)
            {
                // Rotate to account for screen orientation. The raycast buffers are always in landscape left
                // orientation regardless of screen orientation, and are flipped on the Y axis.
                uv -= 0.5f;
                switch (_ScreenOrientation) {
                    case 1:  uv = float2(-uv.y, -uv.x); break;  // Portrait
                    case 2:  uv = float2( uv.y,  uv.x); break;  // PortraitUpsideDown
                    case 3:  uv = float2( uv.x, -uv.y); break;  // LandscapeLeft
                    default: uv = float2(-uv.x,  uv.y); break;  // LandscapeRight
                }
                // The aspect ratio of the raycast buffer and the output buffer may differ. If so, scale the
                // raycast data to cover the output buffer (aspect fill).
                float2 outSize = _ScreenOrientation <= 2 ? _MainTex_TexelSize.wz : _MainTex_TexelSize.zw;
                float outAspect = outSize.x / outSize.y;
                float inAspect = _ArCameraTex_TexelSize.z / _ArCameraTex_TexelSize.w;
                float2 scale = min(1.0f, float2(outAspect / inAspect, inAspect / outAspect));
                return uv * scale + 0.5f;
            }

            fixed4 frag (v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                // Sample camera and confidence textures.
                float2 uv = GetSampleUV(i.uv);
                const half4 cameraColor = tex2D(_ArCameraTex, uv);
                const half conf = tex2D(_ColorTex, uv).w;

                // Compute the color of the diagonal stripes.
                unsigned int stripeCoord = ((int) (screenPos.x + _ScreenParams.y - screenPos.y)) & 15;
                fixed4 stripeColor = (stripeCoord < 10) ? _StripeColor : 1;

                // Blend stripe and camera image based on confidence.
                return stripeColor * (1-conf) + cameraColor * conf;
            }
            ENDCG
        }
    }
}
