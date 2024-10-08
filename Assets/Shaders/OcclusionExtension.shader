Shader "Lightship/OcclusionExtension"
{
    Properties
    {
        _Depth ("DepthTexture", 2D) = "black" {}
        _FusedDepth("FusedDepthTexture", 2D) = "black" {}
        _Suppression ("SuppressionTexture", 2D) = "black" {}
        _StabilizationThreshold ("Stabilization Threshold", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background+1"
            "RenderType" = "Background"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Cull Off
            ZTest Always
            ZWrite On
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "Always"
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ FEATURE_SUPPRESSION
            #pragma multi_compile_local __ FEATURE_STABILIZATION
            #pragma multi_compile_local __ FEATURE_EDGE_SMOOTHING
            #pragma multi_compile_local __ FEATURE_DEBUG

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
              float4 vertex : SV_POSITION;
              float3 depth_uv : TEXCOORD0;

#ifdef FEATURE_SUPPRESSION
              float3 suppression_uv : TEXCOORD1;
#endif

#ifdef FEATURE_STABILIZATION
              float2 vertex_uv : TEXCOORD2;
#endif
            };

            // Samplers for depth and semantics textures
            sampler2D _Depth;
            sampler2D _FusedDepth;
            sampler2D _Suppression;

            // UV transforms
            float4x4 _SuppressionTransform;
            float4x4 _DepthTransform;

            float _UnityCameraForwardScale;
            float _StabilizationThreshold;

            float4 _DepthTextureParams;

            inline float ConvertDistanceToDepth(float d)
            {
                // Account for scale
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;

                // Clip any distances smaller than the near clip plane, and compute the depth value from the distance.
                return (d < _ProjectionParams.y) ? 0.0f : ((1.0f / _ZBufferParams.z) * ((1.0f / d) - _ZBufferParams.w));
            }

            // Z buffer to linear depth
            inline float LinearEyeDepth(float z, float4 zParams)
            {
              return 1.0f / (zParams.z * z + zParams.w);
            }

            // Inverse of Linear Eye Depth
            inline float EyeDepthToNonLinear(float eyeDepth, float4 zParams)
            {
              return (1.0f / eyeDepth - zParams.w) / zParams.z;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.depth_uv = mul(_DepthTransform, float4(v.uv, 1.0f, 1.0f)).xyz;

#ifdef FEATURE_SUPPRESSION
                o.suppression_uv = mul(_SuppressionTransform, float4(v.uv, 1.0f, 1.0f)).xyz;
#endif

#ifdef FEATURE_STABILIZATION
                o.vertex_uv = v.uv;
#endif

                return o;
            }

            struct fragOutput {
              float depth : SV_Depth;
#ifdef FEATURE_DEBUG
              fixed4 color : SV_Target;
#endif
            };

            // Modified from:
            // https://forum.unity.com/threads/how-to-make-data-shader-support-bilinear-trilinear.356692/#post-6407712
            // Texure's filtering mode doens't matter, will always be mip 0 bilinear after this
            // TexelSize should be in Unity's default format, zw = texel dimensions, xy == 1 / zw
            float SampleFloatBilinear(sampler2D tex, float2 uv, float4 texelSize)
            {
                // Scale & offset uvs to integer values at texel centers
                float2 uv_texels = uv * texelSize.zw + 0.5f;

                // Get uvs for the center of the 4 surrounding texels by flooring
                float4 uv_min_max = float4((floor(uv_texels) - 0.5f) * texelSize.xy, (floor(uv_texels) + 0.5f) * texelSize.xy);

                // Blend factor
                float2 uv_frac = frac(uv_texels);

                // Sample all 4 texels
                float a = tex2Dlod(tex, float4(uv_min_max.xy, 0.0f, 0.0f)).r;
                float b = tex2Dlod(tex, float4(uv_min_max.xw, 0.0f, 0.0f)).r;
                float c = tex2Dlod(tex, float4(uv_min_max.zy, 0.0f, 0.0f)).r;
                float d = tex2Dlod(tex, float4(uv_min_max.zw, 0.0f, 0.0f)).r;

                // Bilinear interpolation
                return lerp(lerp(a, b, uv_frac.y), lerp(c, d, uv_frac.y), uv_frac.x);
            }

            // Samples the depth texture for distances
            inline float SampleLinearEyeDepth(sampler2D s, float2 uv, float4 texelSize) {
#ifdef FEATURE_EDGE_SMOOTHING
              return SampleFloatBilinear(s, uv, texelSize);
#else
              return tex2D(s, uv).r;
#endif
            }

            fragOutput frag(v2f i)
            {
              fragOutput o;

              // Infer the far plane distance
#ifdef UNITY_REVERSED_Z
              const float maxDepth = 0.0f;
#else
              const float maxDepth = 1.0f;
#endif

              float2 depth_uv = float2(i.depth_uv.x / i.depth_uv.z, i.depth_uv.y / i.depth_uv.z);

#ifdef FEATURE_STABILIZATION
              // Sample non-linear frame depth
              float frameDepthLinearEye = SampleLinearEyeDepth(_Depth, depth_uv, _DepthTextureParams);
              float frameDepth = ConvertDistanceToDepth(frameDepthLinearEye);

              // Sample non-linear fused depth
              const float eps = 0.1f;
              float fusedDepthLinearEye = tex2D(_FusedDepth, i.vertex_uv) + eps;
              float fusedDepth = EyeDepthToNonLinear(fusedDepthLinearEye, _ZBufferParams);

              // Linearize and compare
              bool useFrameDepth = (abs(fusedDepthLinearEye - frameDepthLinearEye) / fusedDepthLinearEye) >= _StabilizationThreshold;

              // Determine the depth value
              float depth = useFrameDepth ? frameDepth : fusedDepth;
#else
              float depth = ConvertDistanceToDepth(SampleLinearEyeDepth(_Depth, depth_uv, _DepthTextureParams));
#endif

#ifdef FEATURE_SUPPRESSION
              // Sample semantics
              float2 suppression_uv = float2(i.suppression_uv.x / i.suppression_uv.z, i.suppression_uv.y / i.suppression_uv.z);
              float mask = tex2D(_Suppression, suppression_uv).r;
              o.depth = ((depth * (1.0-mask)) + (maxDepth * mask));
#else
              o.depth = depth;
#endif

#ifdef FEATURE_DEBUG

#ifdef UNITY_REVERSED_Z
              fixed3 out_color = fixed3(o.depth, o.depth, o.depth);
#else
              fixed3 out_color = fixed3(1.0f - o.depth, 1.0f - o.depth, 1.0f - o.depth);
#endif

#if UNITY_COLORSPACE_GAMMA
              o.color = fixed4(LinearToGammaSpace(out_color), 1.0f);
#else
              o.color = fixed4(out_color, 1.0f);
#endif

#endif
              return o;
            }
            ENDCG
        }
    }
}
