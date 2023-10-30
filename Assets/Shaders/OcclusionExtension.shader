Shader "Lightship/OcclusionExtension"
{
    Properties
    {
        _Depth ("DepthTexture", 2D) = "black" {}
        _FusedDepth("FusedDepthTexture", 2D) = "black" {}
        _Semantics ("SemanticsTexture", 2D) = "black" {}
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
              float2 depth_uv : TEXCOORD0;

#ifdef FEATURE_SUPPRESSION
              float3 semantics_uv : TEXCOORD1;
#endif

#ifdef FEATURE_STABILIZATION
              float2 vertex_uv : TEXCOORD2;
#endif
            };

            // Samplers for depth and semantics textures
            sampler2D _Depth;
            sampler2D _FusedDepth;
            sampler2D _Semantics;

            // The semantics texture is raw and it requires a full affine and warp transform
            // The depth texture is pre-warped so it only requires the default display matrix
            float4x4 _SemanticsTransform;
            float4x4 _DisplayMatrix;

            float _UnityCameraForwardScale;
            float _StabilizationThreshold;
            int _BitMask;

            inline float ConvertDistanceToDepth(float d)
            {
                // Account for scale
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;

                // Clip any distances smaller than the near clip plane, and compute the depth value from the distance.
                return (d < _ProjectionParams.y) ? 0.0f : ((1.0f / _ZBufferParams.z) * ((1.0f / d) - _ZBufferParams.w));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.depth_uv = mul(_DisplayMatrix, float4(v.uv, 1.0f, 1.0f)).xy;

#ifdef FEATURE_SUPPRESSION
                o.semantics_uv = mul(_SemanticsTransform, float4(v.uv, 1.0f, 1.0f)).xyz;
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

            fragOutput frag(v2f i)
            {
              fragOutput o;

              // Infer the far plane distance
#ifdef UNITY_REVERSED_Z
              const float maxDepth = 0.0f;
#else
              const float maxDepth = 1.0f;
#endif

#ifdef FEATURE_STABILIZATION
              // Sample non-linear frame depth
              float frameDepth = ConvertDistanceToDepth(tex2D(_Depth, i.depth_uv).r);

              // Sample non-linear fused depth
              float fusedDepth = tex2D(_FusedDepth, i.vertex_uv).r;

              // Linearize and compare
              float frameLinear = Linear01Depth(frameDepth);
              float fusedLinear = Linear01Depth(fusedDepth);
              bool useFrameDepth = fusedLinear == maxDepth || (abs(fusedLinear - frameLinear) / fusedLinear) >= _StabilizationThreshold;

              // Determine the depth value
              float depth = useFrameDepth ? frameDepth : fusedDepth;
#else
              float depth = ConvertDistanceToDepth(tex2D(_Depth, i.depth_uv).r);
#endif

#ifdef FEATURE_SUPPRESSION
              // Sample semantics
              float2 semantics_uv = float2(i.semantics_uv.x / i.semantics_uv.z, i.semantics_uv.y / i.semantics_uv.z);
              uint mask = asuint(tex2D(_Semantics, semantics_uv).r);
              o.depth = (mask & _BitMask) != 0 ? maxDepth : depth;
#else
              o.depth = depth;
#endif

#ifdef FEATURE_DEBUG

#ifdef UNITY_REVERSED_Z
              o.color = fixed4(o.depth, o.depth, o.depth, 1.0f);
#else
              o.color = fixed4(1.0f - o.depth, 1.0f - o.depth, 1.0f - o.depth, 1.0f);
#endif

#endif
              return o;
            }
            ENDCG
        }
    }
}
