#ifndef AOSA_LIGHT_INCLUDED
#define AOSA_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 18

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

	int _OtherLightCount;
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light
{
	float3 direction;
	float attenuation;
	uint renderingLayerMask;
};

int GetDirectionalLightCount ()
{
	return _DirectionalLightCount;
}

DirectionalShadowData GetDirectionalShadowData (int lightIndex, ShadowData shadowData)
{
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
	return data;
}

Light GetDirectionalLight (int index, ShadowData shadowData, float3 normal, float3 position)
{
	Light light;
	light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
	light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[index].w);
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, normal, position);
	return light;
}

int GetOtherLightCount ()
{
	return _OtherLightCount;
}

OtherShadowData GetOtherShadowData (int lightIndex)
{
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetOtherLight (int index, ShadowData shadowData, float3 normal, float3 position)
{
	Light light;
	float3 lightPosition = _OtherLightPositions[index].xyz;
	float3 ray = lightPosition - position;
	light.direction = normalize(ray);
	float distance = max(length(ray), 0.00001);
	float rangeAttenuation = step(distance, _OtherLightPositions[index].w);
	float4 spotAngles = _OtherLightSpotAngles[index];
	float3 spotDirection = _OtherLightDirectionsAndMasks[index].xyz;
	light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[index].w);
	float spotAttenuation = Square(saturate(dot(spotDirection, light.direction) * spotAngles.x + spotAngles.y));
	OtherShadowData otherShadowData = GetOtherShadowData(index);
	otherShadowData.lightPositionWS = lightPosition;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, normal, position) * spotAttenuation * rangeAttenuation;
	return light;
}

#endif