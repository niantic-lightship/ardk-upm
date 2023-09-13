Shader "Lightship/OcclusionExtension"
{
    Properties
    {
        _Depth ("DepthTexture", 2D) = "black" {}
        _Semantics ("SemanticsTexture", 2D) = "black" {}
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

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
              float4 vertex : SV_POSITION;
              float3 semantics_uv : TEXCOORD0;
              float2 depth_uv : TEXCOORD1;
            };

            // Samplers for depth and semantics textures
            sampler2D _Depth;
            sampler2D _Semantics;

            // The semantics texture is raw and it requires a full affine and warp transform
            // The depth texture is pre-warped so it only requires the default display matrix
            float4x4 _SemanticsTransform;
            float4x4 _DisplayMatrix;

            float _UnityCameraForwardScale;
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
                o.semantics_uv = mul(_SemanticsTransform, float4(v.uv, 1.0f, 1.0f)).xyz;
                o.depth_uv = mul(_DisplayMatrix, float4(v.uv, 1.0f, 1.0f)).xy;
                return o;
            }

            struct fragOutput {
              float depth : SV_Depth;
            };

            fragOutput frag(v2f i)
            {
              // Sample semantics
              float2 semantics_uv = float2(i.semantics_uv.x / i.semantics_uv.z, i.semantics_uv.y / i.semantics_uv.z);
              uint mask = asuint(tex2D(_Semantics, semantics_uv).r);

              fragOutput o;

              if ((mask & _BitMask) != 0) {
                // Push the depth value to the far plane
#ifdef UNITY_REVERSED_Z
                o.depth = 0.0f;
#else
                o.depth = 1.0f;
#endif
              } else {
                // Write the default depth if the pixel does not satisfy the semantic mask
                o.depth = ConvertDistanceToDepth(tex2D(_Depth, i.depth_uv).r);
              }

              return o;
            }
            ENDCG
        }
    }
}
