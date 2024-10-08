Shader "Unlit/LightshipPlaybackBackground"
{
    Properties
    {
        _CameraTex ("Texture", 2D) = "black" {}
        _EnvironmentDepth("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Name "AR Camera Background (Lightship)"
            Cull Off
            ZWrite On
            ZTest Always
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "Always"
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local __ LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED
            #pragma multi_compile_local __ ANDROID_PLATFORM

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct fragment_output
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            // Do not change variable name.
            // It must match UnityEngine.XR.ARFoundation.ARCameraBackground's k_DisplayTransformName value.
            float4x4 _UnityDisplayTransform;
            float _UnityCameraForwardScale;


            v2f vert (appdata v)
            {
                v2f o;

                // Transform the position from object space to clip space.
                o.position = UnityObjectToClipPos(v.vertex);
#if ANDROID_PLATFORM
                o.texcoord = mul(_UnityDisplayTransform, float3(v.uv.x, 1.0f - v.uv.y, 1.0f)).xy;
#else
                o.texcoord = mul(float3(v.uv, 1.0f), _UnityDisplayTransform).xy;
#endif
                return o;
            }

            // Plane samplers
            sampler2D _CameraTex;

#if LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED
            sampler2D_float _EnvironmentDepth;
#endif // LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED

            inline float ConvertDistanceToDepth(float d)
            {
                // Account for scale
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;

                // Clip any distances smaller than the near clip plane, and compute the depth value from the distance.
                return (d < _ProjectionParams.y) ? 0.0f : ((1.0f / _ZBufferParams.z) * ((1.0f / d) - _ZBufferParams.w));
            }

            fragment_output frag(v2f i)
            {
                // Infer the far plane distance
#ifdef UNITY_REVERSED_Z
                const float maxDepth = 0.0f;
#else
                const float maxDepth = 1.0f;
#endif

                // Sample color
                float4 color = tex2D(_CameraTex, i.texcoord);
                float depth = maxDepth;

#if LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED
                // Sample the environment depth (in meters).
                float envDistance = tex2D(_EnvironmentDepth, i.texcoord).x;
                depth = ConvertDistanceToDepth(envDistance);
#endif // LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED

                fragment_output o;
                o.color = half4(color.x, color.y, color.z, 1.0);
                o.depth = depth;
                return o;
            }
            ENDHLSL
        }
    }
}
