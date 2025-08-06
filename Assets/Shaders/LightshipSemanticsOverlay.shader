Shader "Lightship/SemanticsOverlay"
{
    SubShader
    {
        // No culling or depth testing
        ZWrite Off ZTest Always
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
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 HSVtoRGB(half3 arg1)
            {
                half4 K = half4(1.0h, 2.0h / 3.0h, 1.0h / 3.0h, 3.0h);
                half3 P = abs(frac(arg1.xxx + K.xyz) * 6.0h - K.www);
                return arg1.z * lerp(K.xxx, saturate(P - K.xxx), arg1.y);
            }

            half4 ConfidenceToColor(float confidence, float alpha)
            {
                half hue = lerp(0.70h, -0.15h, saturate(confidence));
                if (hue < 0.0h)
                {
                    hue += 1.0h;
                }
                return half4(HSVtoRGB(half3(hue, 0.9h, 0.6h)), alpha);
            }

            float4 ConvertFromImageToView(float2 uv, float4 intrinsics, float imageWidth, float imageHeight, float depth)
            {
                // Get the pixel coordinates for the vertex
                uint2 pixel = uint2((uint)floor(uv.x * imageWidth), (uint)floor(uv.y * imageHeight));

                float4 viewPosition;
                viewPosition.x = (pixel.x - intrinsics.z) * depth / intrinsics.x;
                viewPosition.y = (pixel.y - intrinsics.w) * depth / intrinsics.y;
                viewPosition.z = -depth;
                viewPosition.w = 1.0f;
                return viewPosition;
            }

            inline float4 WorldToClipPos(float3 posWorld)
            {
              float4 clipPos;
              #if defined(STEREO_CUBEMAP_RENDER_ON) || defined(UNITY_SINGLE_PASS_STEREO)
                float3 offset = ODSOffset(posWorld, unity_HalfStereoSeparation.x);
                clipPos = mul(UNITY_MATRIX_VP, float4(posWorld + offset, 1.0));
              #else
                clipPos = mul(UNITY_MATRIX_VP, float4(posWorld, 1.0));
              #endif
              return clipPos;
            }

            // Sampler
            sampler2D _Semantics;
            float _ImageWidth;
            float _ImageHeight;
            float _BackprojectionDistance;

            // Matrices to transform UV coordinates
            float4x4 _SamplerMatrix;

            // Matrices to transform image space to world space
            float4 _Intrinsics;
            float4x4 _Extrinsics;

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Convert from image space to view space
                float4 viewPosition = ConvertFromImageToView(v.uv, _Intrinsics, _ImageWidth, _ImageHeight, _BackprojectionDistance);

                // Convert from view space to world space
                viewPosition = mul(_Extrinsics, viewPosition);

                // Convert from world space to clip space
                o.vertex = WorldToClipPos(viewPosition.xyz);

                // Apply the sampler matrix to the UV coordinates
                o.uv = mul(_SamplerMatrix, float4(v.uv.x, v.uv.y, 1.0f, 1.0f)).xyz;

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Transform projected UVs into 2D UV coordinates
                float2 semantic_uv = float2(i.uv.x / i.uv.z, i.uv.y / i.uv.z);

                // Calculate a mask such that, if UV is outside [0,1] region, return zero confidence
                float2 inside = step(0.0, semantic_uv) * step(semantic_uv, 1.0);
                float clampMask = inside.x * inside.y;

                // Sample the semantics texture
                float confidence = tex2D(_Semantics, semantic_uv) * clampMask;

                return ConfidenceToColor(confidence, 0.5h);
            }
            ENDCG
        }
    }
}
