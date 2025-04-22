Shader "Lightship/OcclusionMesh"
{
    Properties
    {
        // Retrievable properties
        _Depth ("DepthTexture", 2D) = "black" {}
        _ColorMask ("Color Mask", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Offset 1, 1
            ColorMask [_ColorMask]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #pragma multi_compile_local __ FEATURE_SUPPRESSION
            #pragma multi_compile_local __ FEATURE_STABILIZATION

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                uint vid : SV_VertexID;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _Depth;

#ifdef FEATURE_STABILIZATION
            sampler2D _FusedDepth;
            float4x4 _FusedDepthTransform;
#endif

#ifdef FEATURE_SUPPRESSION
            sampler2D _Suppression;
            float4x4 _SuppressionTransform;
#endif

            // Matrices
            float4 _Intrinsics;
            float4x4 _Extrinsics;

            float _UnityCameraForwardScale;
            float _StabilizationThreshold;
            int _ImageWidth;
            int _ImageHeight;

            inline float ScaleEyeDepth(float d)
            {
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;
                return d < _ProjectionParams.y ? 0.0f : d;
            }

            inline float4 WorldToClipPos(float3 posWorld)
            {
              float4 clipPos;
              #if defined(STEREO_CUBEMAP_RENDER_ON)
                float3 offset = ODSOffset(posWorld, unity_HalfStereoSeparation.x);
                clipPos = mul(UNITY_MATRIX_VP, float4(posWorld + offset, 1.0));
              #else
                clipPos = mul(UNITY_MATRIX_VP, float4(posWorld, 1.0));
              #endif
              return clipPos;
            }

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Get the pixel coordinates for the vertex
                uint2 pixel = uint2(v.vid % _ImageWidth, v.vid / _ImageHeight);

                // Calculate UV coordinates for the vertex
                float4 uv = float4(pixel.x / (float)_ImageWidth, pixel.y / (float)_ImageHeight, 0.0f, 0.0f);
                uv.y = 1.0f - uv.y; // Flip y-axis (native image is mirrored)

                // Sample depth
                float eye_depth = tex2Dlod(_Depth, uv).r;

#ifdef FEATURE_STABILIZATION
                // Calculate UV coordinates for the fused depth
                // This is not a native image, so we have to un-invert the y-axis
                float4 fusedDepthUV = mul(_FusedDepthTransform, float4(uv.x, 1.0f - uv.y, 1.0f, 1.0f));

                // Sample fused depth
                const float eps = 0.01f;
                float fused_depth_linear_eye =
                  tex2Dlod(_FusedDepth, float4(fusedDepthUV.x / fusedDepthUV.z, fusedDepthUV.y / fusedDepthUV.z, 0, 0)) + eps;

                // Determine the depth value
                bool useFrameDepth = (abs(fused_depth_linear_eye - eye_depth) / fused_depth_linear_eye) >= _StabilizationThreshold;
                eye_depth = useFrameDepth ? eye_depth : fused_depth_linear_eye;
#endif

                // Convert from image space to view space
                float4 view_position;
                view_position.x = (pixel.x - _Intrinsics.z) * eye_depth / _Intrinsics.x;
                view_position.y = (pixel.y - _Intrinsics.w) * eye_depth / _Intrinsics.y;
                view_position.z = ScaleEyeDepth(eye_depth);
                view_position.w = 1.0f;

#ifdef FEATURE_SUPPRESSION
                // Sample the suppression mask
                float4 suppression_uv = mul(_SuppressionTransform, float4(uv.x, 1.0f - uv.y, 1.0f, 1.0f));
                float mask = tex2Dlod(_Suppression, float4(suppression_uv.x / suppression_uv.z, suppression_uv.y / suppression_uv.z, 0, 0)).r;

                // Determine whether to offset the vertex
                float scalar = mask > 0.5f ? 1000.0f : 1.0f;
                view_position = view_position * scalar;
                view_position.w = 1.0f;
#endif

                // Transform to world space
                float4 world_position = mul(_Extrinsics, view_position);

                // Transform to clip space
                o.pos = WorldToClipPos(world_position.xyz);

                // Set colors to debug UV coordinates
                o.color.x = uv.x;
                o.color.z = uv.y;

                // Set colors to debug eye depth
                const float max_view_disp = 1.0f;
                o.color.y = (1.0f / eye_depth) / max_view_disp;

                // For debug visualization, we need opaque
                o.color.w = 1.0f;

                return o;
            }

            half4 frag(v2f i) : COLOR
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Use the input color from the vertex, in the event we're using debug visualization.
                return i.color;
            }

            ENDCG
        }
    }
}
