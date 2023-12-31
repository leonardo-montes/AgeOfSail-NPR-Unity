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

	float breakupMap = GetBreakup(config);
	breakupMap = clamp(breakupMap, 0.3, 0.6) * step(0.01, breakupMap);
	
	float shadows[MAX_LIGHT_COUNT];
	float speculars[MAX_LIGHT_COUNT];
	[unroll]
	for (int i = 0; i < MAX_LIGHT_COUNT; ++i)
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
	buffers.buffer1 = float4(shadowedColor, breakupMap);
	buffers.buffer2 = float4(shadows[0], speculars[0], shadows[1], speculars[1]);
	buffers.buffer3 = float4(shadows[2], speculars[2], shadows[3], speculars[3]);
	buffers.buffer4 = float4(shadows[4], speculars[4], shadows[5], speculars[5]);
	buffers.buffer5 = float4(shadows[6], speculars[6], shadows[7], speculars[7]);
	buffers.buffer6 = float4(shadows[8], speculars[8], shadows[9], speculars[9]);
	return buffers; 
}

#endif