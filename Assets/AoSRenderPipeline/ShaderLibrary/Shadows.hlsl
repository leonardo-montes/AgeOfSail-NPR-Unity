#ifndef AOS_SHADOWS_INCLUDED
#define AOS_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_CASCADE_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

struct ShadowData
{
	int cascadeIndex;
	float cascadeBlend;
	float strength;
};

float FadedShadowStrength (float distance, float scale, float fade)
{
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData (float depth, float3 position, float dither)
{
	ShadowData data;
	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
	int i;
	for (i = 0; i < _CascadeCount; i++)
	{
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(position, sphere.xyz);
		if (distanceSqr < sphere.w)
		{
			float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
			if (i == _CascadeCount - 1)
			{
				data.strength *= fade;
			}
			else
			{
				data.cascadeBlend = fade;
			}
			break;
		}
	}
	
	if (i == _CascadeCount && _CascadeCount > 0)
	{
		data.strength = 0.0;
	}
	#if defined(_CASCADE_BLEND_DITHER)
		else if (data.cascadeBlend < dither)
		{
			i += 1;
		}
	#endif
	#if !defined(_CASCADE_BLEND_SOFT)
		data.cascadeBlend = 1.0;
	#endif
	data.cascadeIndex = i;
	return data;
}

struct DirectionalShadowData
{
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};

float SampleDirectionalShadowAtlas (float3 positionSTS)
{
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterDirectionalShadow (float3 positionSTS)
{
	#if defined(DIRECTIONAL_FILTER_SETUP)
		real weights[DIRECTIONAL_FILTER_SAMPLES];
		real2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
		{
			shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
		}
		return shadow;
	#else
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif
}

float GetCascadedShadow (DirectionalShadowData directional, ShadowData global, float3 normal, float3 position)
{
	float3 normalBias = normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(position + normalBias, 1.0)).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0)
	{
		normalBias = normal *	(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(position + normalBias, 1.0)).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
	}
	return shadow;
}

float GetDirectionalShadowAttenuation (DirectionalShadowData directional, ShadowData global, float3 normal, float3 position)
{
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif

	return GetCascadedShadow(directional, global, normal, position);
}

#endif