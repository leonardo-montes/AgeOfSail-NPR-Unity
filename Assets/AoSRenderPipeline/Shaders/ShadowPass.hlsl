#ifndef AOS_SHADOW_PASS_INCLUDED
#define AOS_SHADOW_PASS_INCLUDED

#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS_SS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float3 normalWS : VAR_NORMAL;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowPassVertex (Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

float4 ShadowPassFragment (Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	ClipLOD(config.fragment, unity_LODFade.x);
		
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif
	
	#if defined(_UNLIT)
		return float4(1.0, 0.0, 0.0, GetFinalAlpha(base.a));
	#endif

	float3 position = input.positionWS;
	float3 normal = normalize(input.normalWS);
	float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	float depth = -TransformWorldToView(input.positionWS).z;
	float smoothness = GetSmoothness(config);
	float dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
	uint renderingLayerMask = asuint(unity_RenderingLayer.x);

	float3 color;

	float breakupMap = GetBreakup(config);
	float breakupMapResult = clamp(breakupMap, 0.3, 0.6) * step(0.01, breakupMap);
	
	// R: 'In the red channel, we render a simplified Lambert-shaded version of the objects, with cast shadows.'
	// G: 'The green channel denotes objects that need to have screen-space lens effects, such as light sources or specular highlights'
	color.rg = GetLighting(renderingLayerMask, normal, depth, position, dither, smoothness, viewDirection);
	color.r -= breakupMap * 0.5 * (1.0 - color.r);

	// B: 'We also apply an optional breakup map texture to the blue channel on certain surfaces'
	color.b = breakupMapResult;
	
	return float4(color, GetFinalAlpha(base.a));
}

#endif