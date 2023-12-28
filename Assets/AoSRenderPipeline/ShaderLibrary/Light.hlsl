#ifndef AOS_LIGHT_INCLUDED
#define AOS_LIGHT_INCLUDED

CBUFFER_START(_CustomLight)
	float4 _DirectionalLightDirectionAndMask;
	float4 _DirectionalLightShadowData;
CBUFFER_END

struct Light
{
	float3 direction;
	float attenuation;
	uint renderingLayerMask;
};

DirectionalShadowData GetDirectionalShadowData (ShadowData shadowData)
{
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData.x;
	data.tileIndex = _DirectionalLightShadowData.y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData.z;
	data.shadowMaskChannel = _DirectionalLightShadowData.w;
	return data;
}

Light GetDirectionalLight (ShadowData shadowData, float3 normal, float3 position)
{
	Light light;
	light.direction = _DirectionalLightDirectionAndMask.xyz;
	light.renderingLayerMask = asuint(_DirectionalLightDirectionAndMask.w);
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, normal, position);
	return light;
}

#endif