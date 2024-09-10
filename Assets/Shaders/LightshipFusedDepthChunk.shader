Shader "Lightship/FusedDepthChunk"
{
  Properties
  {
    _ShadowColor ("Shadow Color", Color) = (0.35, 0.4, 0.45, 1.0)
  }

  SubShader
  {
    Tags
    {
        "IgnoreProjector"="True"
        "RenderType"="Transparent"
        "Queue"="AlphaTest"
    }
    Blend zero SrcColor
    Cull Back

    // Write to ZBuffer to be able to capture the depth of the fused mesh
    ZWrite On

    CGPROGRAM

    #pragma surface surf ShadowOnly addshadow

    fixed4 _ShadowColor;

    struct Input
    {
        float2 uv_MainTex;
    };

    inline fixed4 LightingShadowOnly (SurfaceOutput surf, fixed3 dir, fixed atten)
    {
        fixed4 c;
        c.rgb = lerp(surf.Albedo, float3(1.0,1.0,1.0), atten);
        c.a = 1.0f - atten;
        return c;
    }

    void surf (Input i, inout SurfaceOutput o)
    {
        o.Albedo = lerp(float3(1.0,1.0,1.0), _ShadowColor.rgb, 1.0f);
        o.Alpha = 1.0;
    }

    ENDCG

  }
  FallBack Off
}
