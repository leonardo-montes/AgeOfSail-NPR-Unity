#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "LitInput.hlsl"
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

	float breakupMap = GetBreakup(config);

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

// From https://www.ryanjuckett.com/photoshop-blend-modes-in-hlsl/
float BlendMode_Overlay(float base, float blend)
{
	return (base <= 0.5) ? 2 * base * blend : 1 - 2 * (1 - base) * (1 - blend);
}

// From https://www.ryanjuckett.com/photoshop-blend-modes-in-hlsl/
float3 BlendMode_Overlay(float3 base, float3 blend)
{
	return float3(BlendMode_Overlay(base.r, blend.r), BlendMode_Overlay(base.g, blend.g), BlendMode_Overlay(base.b, blend.b));
}

// From https://docs.unity3d.com/Packages/com.unity.shadergraph@6.9/manual/Saturation-Node.html
float3 Saturation(float3 color, float saturation)
{
    float luma = dot(color, float3(0.2126729, 0.7151522, 0.0721750));
    return luma.xxx + saturation.xxx * (color - luma.xxx);
}

float4 LitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	ClipLOD(config.fragment, unity_LODFade.x);
		
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	float4 baseShadowed = GetBaseShadowed(config);

	float2 screenUV = input.screenUV.xy / input.screenUV.w;
	float3 finalShadow = SAMPLE_TEXTURE2D_LOD(_FinalShadowRT, sampler_FinalShadowRT, screenUV, 0).rgb;

	// We use the Final Shadow Pass red channel to blend between these lit and shadowed texture maps on all objects.
	float3 color = lerp(baseShadowed.rgb, base.rgb, max(1.0 - baseShadowed.a, finalShadow.r));

	// Specular
	color = lerp(color, 1.0, step(0.8, finalShadow.g));

	// Overlay
	float4 colorOverlay = GetBaseColorOverlay();
	color = lerp(color, BlendMode_Overlay(color, colorOverlay.rgb), colorOverlay.a);

	// Saturation
	color = Saturation(color, GetBaseColorSaturation());

	//color = finalShadow.r;
	
	return float4(color, GetFinalAlpha(base.a));
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