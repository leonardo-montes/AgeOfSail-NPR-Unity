#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#if defined(_AGE_OF_SAIL_RP_SHADOW_PASS)
	#include "../ShaderLibrary/AgeOfSailLighting.hlsl"
#else
	#include "../ShaderLibrary/Lighting.hlsl"
#endif

struct Attributes {
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	float2 baseUV : TEXCOORD0;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	float4 screenUV : VAR_SCREEN_UV;
	float3 positionWS : VAR_POSITION;
	float3 normalWS : VAR_NORMAL;
	#if defined(_NORMAL_MAP)
		float4 tangentWS : VAR_TANGENT;
	#endif
	float2 baseUV : VAR_BASE_UV;
	#if defined(_DETAIL_MAP)
		float2 detailUV : VAR_DETAIL_UV;
	#endif
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	TRANSFER_GI_DATA(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	#if defined(_NORMAL_MAP)
		output.tangentWS = float4(
			TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w
		);
	#endif
	output.baseUV = TransformBaseUV(input.baseUV);
	output.screenUV = ComputeScreenPos(output.positionCS_SS);
	#if defined(_DETAIL_MAP)
		output.detailUV = TransformDetailUV(input.baseUV);
	#endif
	return output;
}

#if defined(_AGE_OF_SAIL_RP_SHADOW_PASS)
TEXTURE2D(_BreakupMap);
SAMPLER(sampler_BreakupMap);
float4 _BreakupMap_ST;

float4 LitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	ClipLOD(config.fragment, unity_LODFade.x);
		
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif
	
	Surface surface;
	surface.position = input.positionWS;
	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(
			GetNormalTS(config), input.normalWS, input.tangentWS
		);
		surface.interpolatedNormal = input.normalWS;
	#else
		surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
	#endif
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(config);
	surface.occlusion = GetOcclusion(config);
	surface.smoothness = GetSmoothness(config);
	surface.fresnelStrength = GetFresnel(config);
	surface.dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
	
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);

	float3 color;

	float breakupMap = SAMPLE_TEXTURE2D(_BreakupMap, sampler_BreakupMap, input.baseUV * _BreakupMap_ST.xy).r;

	// R: 'In the red channel, we render a simplified Lambert-shaded version of the objects, with cast shadows.'
	// G: 'The green channel denotes objects that need to have screen-space lens effects, such as light sources or specular highlights'
	color.rg = GetLighting(surface, brdf, gi);
	color.r -= breakupMap * 0.5 * (1.0 - color.r);

	// B: 'We also apply an optional breakup map texture to the blue channel on certain surfaces'
	color.b = clamp(breakupMap, 0.3, 0.6) * step(0.01, breakupMap);
	
	return float4(color, GetFinalAlpha(surface.alpha));
}
#elif defined(_AGE_OF_SAIL_RP_COLOR_PASS)
TEXTURE2D(_FinalShadowRT);
SAMPLER(sampler_FinalShadowRT);

float4 LitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	ClipLOD(config.fragment, unity_LODFade.x);
		
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif
	
	Surface surface;
	surface.position = input.positionWS;
	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(
			GetNormalTS(config), input.normalWS, input.tangentWS
		);
		surface.interpolatedNormal = input.normalWS;
	#else
		surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
	#endif
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(config);
	surface.occlusion = GetOcclusion(config);
	surface.smoothness = GetSmoothness(config);
	surface.fresnelStrength = GetFresnel(config);
	surface.dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	float3 colorLit = surface.color;
	float3 colorShadowed = surface.color * float3(0.1, 0.1, 0.15);

	float2 screenUV = input.screenUV.xy / input.screenUV.w;
	float3 finalShadow = SAMPLE_TEXTURE2D_LOD(_FinalShadowRT, sampler_FinalShadowRT, screenUV, 0).rgb;

	// We use the Final Shadow Pass red channel to blend between these lit and shadowed texture maps on all objects.
	float3 color = lerp(colorShadowed, colorLit, finalShadow.r);

	// Specular
	color = lerp(color + finalShadow.g * finalShadow.g, 1.0, step(0.8, finalShadow.g));

	//color = finalShadow.r;
	
	return float4(color, GetFinalAlpha(surface.alpha));
}
#else
float4 LitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	ClipLOD(config.fragment, unity_LODFade.x);
	
	#if defined(_MASK_MAP)
		config.useMask = true;
	#endif
	#if defined(_DETAIL_MAP)
		config.detailUV = input.detailUV;
		config.useDetail = true;
	#endif
	
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif
	
	Surface surface;
	surface.position = input.positionWS;
	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(
			GetNormalTS(config), input.normalWS, input.tangentWS
		);
		surface.interpolatedNormal = input.normalWS;
	#else
		surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
	#endif
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(config);
	surface.occlusion = GetOcclusion(config);
	surface.smoothness = GetSmoothness(config);
	surface.fresnelStrength = GetFresnel(config);
	surface.dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	float3 color = GetLighting(surface, brdf, gi);
	color += GetEmission(config);
	return float4(color, GetFinalAlpha(surface.alpha));
}
#endif

#endif