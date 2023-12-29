#ifndef AOSA_LIGHTING_INCLUDED
#define AOSA_LIGHTING_INCLUDED

float IncomingLight (float3 normal, Light light)
{
	float ndotl = saturate(dot(normal, light.direction) * 0.5 + 0.5);
	return ndotl * step(0.5, light.attenuation);
}

float SpecularHighlight (float3 viewDirection, float3 normal, float smoothness, Light light)
{	
	float3 halfVector = normalize(light.direction + viewDirection);
	float NdotH = dot(normal, halfVector);
	float specularIntensity = pow(NdotH, 1.0 / max(0.001, smoothness * smoothness)) * step(0.001, smoothness);
	return step(0.5, light.attenuation * specularIntensity);
}

void GetLighting (float3 viewDirection, float3 normal, float smoothness, Light light, inout float shadow, inout float specular)
{
	shadow = IncomingLight(normal, light);
	specular = SpecularHighlight(viewDirection, normal, smoothness, light);
}

bool RenderingLayersOverlap (uint renderingLayerMask, Light light)
{
	return (renderingLayerMask & light.renderingLayerMask) != 0;
}

void GetLighting (uint renderingLayerMask, float3 normal, float depth, float3 position, float dither, float smoothness, float3 viewDirection, inout float shadows[MAX_LIGHT_COUNT], inout float speculars[MAX_LIGHT_COUNT])
{
	ShadowData shadowData = GetShadowData(depth, position, dither);
	
	int id = 0;
	for (int i = 0; i < GetDirectionalLightCount(); i++)
	{
		#if defined(_UNLIT)
		shadows[id] = 1.0;
		#else
		Light light = GetDirectionalLight(i, shadowData, normal, position);
		if (RenderingLayersOverlap(renderingLayerMask, light))
		{
			GetLighting(viewDirection, normal, smoothness, light, shadows[id], speculars[id]);
			id++;
		}
		#endif
	}
	
	#if !defined(_UNLIT)
	for (int j = 0; j < GetOtherLightCount(); j++)
	{
		Light light = GetOtherLight(j, shadowData, normal, position);
		if (RenderingLayersOverlap(renderingLayerMask, light))
		{
			GetLighting(viewDirection, normal, smoothness, light, shadows[id], speculars[id]);
			id++;
		}
	}
	#endif
}

#endif