Shader "Lightship/OcclusionEffect"
{
    Properties
    {
        _DepthTexture("Depth Texture", 2D) = "black" {}
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

            sampler2D _DepthTexture;
            float4 _Intrinsics;
            float4x4 _Extrinsics;

            float _UnityCameraForwardScale;
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
                uint2 pixel = uint2(v.vid % (uint)_ImageWidth, v.vid / (uint)_ImageHeight);

                // Calculate UV coordinates for the vertex
                float4 uv = float4(pixel.x / (float)_ImageWidth, pixel.y / (float)_ImageHeight, 0.0f, 0.0f);
                uv.y = 1.0f - uv.y; // Flip y-axis (native image is mirrored)

                // Sample depth
                float eyeDepth = tex2Dlod(_DepthTexture, uv).r;

                // Convert from image space to view space
                float4 viewPosition;
                viewPosition.x = (pixel.x - _Intrinsics.z) * eyeDepth / _Intrinsics.x;
                viewPosition.y = (pixel.y - _Intrinsics.w) * eyeDepth / _Intrinsics.y;
                viewPosition.z = ScaleEyeDepth(eyeDepth);
                viewPosition.w = 1.0f;

                // Transform to world space
                float4 worldPosition = mul(_Extrinsics, viewPosition);

                // Transform to clip space
                o.pos = WorldToClipPos(worldPosition.xyz);

                // Set colors to debug UV coordinates
                o.color.x = uv.x;
                o.color.z = uv.y;

                // Set colors to debug eye depth
                const float max_view_disp = 1.0f;
                o.color.y = (1.0f / eyeDepth) / max_view_disp;

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
