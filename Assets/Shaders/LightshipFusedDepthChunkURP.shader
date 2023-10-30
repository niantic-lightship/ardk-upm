Shader "Lightship/FusedDepthChunkURP"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.35, 0.4, 0.45, 1.0)
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "AlphaTest"
        }
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            Blend zero SrcColor
            Cull Back

            // Write to ZBuffer to be able to capture the depth of the fused mesh
            ZWrite On

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTSHIP_URP

            struct vIn
            {
                float4 positionOS : POSITION;
            };

            struct vOut
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

#if (defined(LIGHTSHIP_URP))

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
            CBUFFER_END

            vOut vert(vIn input)
            {
                vOut output;
                VertexPositionInputs vInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vInput.positionCS;
                output.positionWS = vInput.positionWS;
                return output;
            }

            half4 frag(vOut i) : SV_Target
            {
                half4 color = half4(1, 1, 1, 1);

                #if (defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN))
                    VertexPositionInputs vertexInput = (VertexPositionInputs)0;
                    vertexInput.positionWS = i.positionWS;
                    float4 shadowCoord = GetShadowCoord(vertexInput);
                    half shadowAttenutation = MainLightRealtimeShadow(shadowCoord);
                    color = lerp(half4(1, 1, 1, 1), _ShadowColor, (1.0 - shadowAttenutation) * _ShadowColor.a);
                #endif

                return color;
            }

#else

            vOut vert(vIn input)
            {
                vOut output;
                output.positionCS = float4(1.0f, 1.0f, 1.0f, 1.0f);
                output.positionWS = float3(1.0f, 1.0f, 1.0f);
                return output;
            }

            half4 frag(vOut i) : SV_Target
            {
                half4 color = half4(1, 1, 1, 1);
                return color;
            }

#endif

            ENDHLSL
        }
    }
}
