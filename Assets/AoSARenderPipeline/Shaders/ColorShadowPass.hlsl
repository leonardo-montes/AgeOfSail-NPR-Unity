#ifndef AOSA_COLOR_SHADOW_PASS_INCLUDED
#define AOSA_COLOR_SHADOW_PASS_INCLUDED

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

Varyings ColorShadowPassVertex (Attributes input)
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

struct Buffers
{
	float4 buffer0 : SV_TARGET0;
	float4 buffer1 : SV_TARGET1;
	float4 buffer2 : SV_TARGET2;
	float4 buffer3 : SV_TARGET3;
	float4 buffer4 : SV_TARGET4;
	float4 buffer5 : SV_TARGET5;
	float4 buffer6 : SV_TARGET6;
	float4 buffer7 : SV_TARGET7;
};

Buffers ColorShadowPassFragment (Varyings input)
{
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	ClipLOD(config.fragment, unity_LODFade.x);
		
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	float3 position = input.positionWS;
	float3 normal = normalize(input.normalWS);
	float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	float depth = -TransformWorldToView(input.positionWS).z;
	float smoothness = GetSmoothness(config);
	float dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
	uint renderingLayerMask = asuint(unity_RenderingLayer.x);

	float3 litColor = base.rgb;
	float3 shadowedColor = GetBaseShadowed(config).rgb;
	float4 overlay = GetBaseColorOverlay();
	float saturation = GetBaseColorSaturation();

	float breakupMap = GetBreakup(config);
	float breakupMapResult = clamp(breakupMap, 0.3, 0.6) * step(0.01, breakupMap);
	
	// R: 'In the red channel, we render a simplified Lambert-shaded version of the objects, with cast shadows.'
	// G: 'The green channel denotes objects that need to have screen-space lens effects, such as light sources or specular highlights'
	/*color.rg = GetLighting(renderingLayerMask, normal, depth, position, dither, smoothness, viewDirection);
	color.r -= breakupMap * 0.5 * (1.0 - color.r);*/
	float shadows[9];
	float speculars[9];
	[unroll]
	for (int i = 0; i < 9; ++i)
	{
		shadows[i] = 0.0;
		speculars[i] = 0.0;
	}

	#if defined(_UNLIT)
		shadowedColor = litColor;
	#endif

	GetLighting(renderingLayerMask, normal, depth, position, dither, smoothness, viewDirection, shadows, speculars);

	Buffers buffers;
	buffers.buffer0 = float4(litColor, GetFinalAlpha(base.a));
	buffers.buffer1 = float4(shadowedColor, saturation);
	buffers.buffer2 = overlay;
	buffers.buffer3 = float4(breakupMapResult, 0.0, shadows[0], speculars[0]);
	buffers.buffer4 = float4(shadows[1], speculars[1], shadows[2], speculars[2]);
	buffers.buffer5 = float4(shadows[3], speculars[3], shadows[4], speculars[4]);
	buffers.buffer6 = float4(shadows[5], speculars[5], shadows[6], speculars[6]);
	buffers.buffer7 = float4(shadows[7], speculars[7], shadows[8], speculars[8]);
	return buffers; 
}

#endif