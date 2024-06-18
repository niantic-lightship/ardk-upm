Shader "Custom/VertexColor" {
	Properties {
		_Alpha("Alpha", float) = 1.0
		_Grayscale("Grayscale", Integer) = 0
	}

	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 200
		
		CGPROGRAM
		#pragma surface surf Lambert vertex:vert 
		#pragma target 3.0

		struct Input {
			float4 vertColor;
		};

		float _Alpha;
		int _Grayscale;

		void vert(inout appdata_full v, out Input o){
			UNITY_INITIALIZE_OUTPUT(Input, o);
			if (_Grayscale == 1)
			{
			  o.vertColor.rgb = dot(float3(0.3, 0.6, 0.1), v.color.rgb);
			}
			else {
			  o.vertColor = v.color;
			}
		}

		void surf (Input IN, inout SurfaceOutput o) {
			o.Albedo = IN.vertColor.rgb;
			o.Alpha = _Alpha;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
