Shader "Custom/Cyberpunk_Shader" 
{
	Properties {
		_AlbedoTex ("Albedo (RGB)", 2D) = "white" {}
		_Color1("Color 1", Color) = (1,1,1,1)
		_Color2("Color 2", Color) = (1,1,1,1)
		_Color3("Color 3", Color) = (1,1,1,1)

		_ColorMaskTex ("ColorMask", 2D) = "white" {}
		_OcclusionTex("Occlusion", 2D) = "white" {}

		_MetallicTex ("Metallness", 2D) = "black" {}
		_NormalTex ("Normals", 2D) = "bump" {}
		_EmissiveTex ("Emissive", 2D) = "black" {}
	}
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		struct Input {
			float2 uv_AlbedoTex;
			float4 color: Color;
			float2 uv_ColorMaskTex;
			float2 uv2_OcclusionTex;
			float2 uv_MetallicTex;
			float2 uv_NormalTex;
			float2 uv_EmissiveTex;
		};

		sampler2D _AlbedoTex;
		sampler2D _ColorMaskTex;
		sampler2D _OcclusionTex;
		sampler2D _MetallicTex;
		sampler2D _NormalTex;
		sampler2D _EmissiveTex;
		float4 _Color1;
		float4 _Color2;
		float4 _Color3;

		void surf (Input IN, inout SurfaceOutputStandard o) {
			//fixed3 vc = (1.0 - IN.color) * tex2D(_ColorMaskTex, IN.uv_ColorMaskTex);
			//o.Albedo = tex2D(_AlbedoTex, IN.uv_AlbedoTex).rgb * _Color.rgb * (1.0 - vc).rgb;

			fixed3 c1 = 1.0 - ((1.0 - _Color1) * tex2D(_ColorMaskTex, IN.uv_ColorMaskTex) * (1.0 - IN.color.r));
			fixed3 c2 = 1.0 - ((1.0 - _Color2) * tex2D(_ColorMaskTex, IN.uv_ColorMaskTex) * (1.0 - IN.color.g));
			fixed3 c3 = 1.0 - ((1.0 - _Color3) * tex2D(_ColorMaskTex, IN.uv_ColorMaskTex) * (1.0 - IN.color.b));
			/*
			fixed3 c = 1.0 - ((1.0 - c1 * c2 * c3) * (1 - IN.color.a));
			fixed3 vc = 1.0 - ((1.0 - IN.color) * (IN.color.a));
			o.Albedo = tex2D(_AlbedoTex, IN.uv_AlbedoTex).rgb * c.rgb * vc.rgb;*/

			o.Albedo = tex2D(_AlbedoTex, IN.uv_AlbedoTex).rgb * c1.rgb * c2.rgb * c3.rgb;
			o.Alpha = tex2D(_OcclusionTex, IN.uv2_OcclusionTex).a;
			o.Occlusion = tex2D(_OcclusionTex, IN.uv2_OcclusionTex).r;

			fixed4 mc = tex2D(_MetallicTex, IN.uv_MetallicTex);
			o.Metallic = mc.r;
			o.Smoothness = mc.a;
			o.Normal = UnpackNormal(tex2D(_NormalTex, IN.uv_NormalTex));
			o.Emission = tex2D(_EmissiveTex, IN.uv_EmissiveTex).rgb * IN.color.rgb;


		}
		ENDCG
	}
	FallBack "Diffuse"
}
