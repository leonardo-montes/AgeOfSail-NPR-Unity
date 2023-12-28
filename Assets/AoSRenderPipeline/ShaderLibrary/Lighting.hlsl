#ifndef AOS_LIGHTING_INCLUDED
#define AOS_LIGHTING_INCLUDED

float IncomingLight (float3 normal, Light light)
{
	float ndotl = saturate(dot(normal, light.direction) * 0.5 + 0.5);
	#if defined(_USE_SHADOWS)
	return ndotl * step(0.5, light.attenuation);
	#else
	return ndotl;
	#endif
}

float SpecularHighlight (float3 viewDirection, float3 normal, float smoothness, Light light)
{	
	float3 halfVector = normalize(light.direction + viewDirection);
	float NdotH = dot(normal, halfVector);
	float specularIntensity = pow(NdotH, 1.0 / max(0.001, smoothness * smoothness)) * step(0.001, smoothness);
	return step(0.5, light.attenuation * specularIntensity);
}

float2 GetLighting (float3 viewDirection, float3 normal, float smoothness, Light light)
{
	float2 result;
	result.r = IncomingLight(normal, light);
	result.g = SpecularHighlight(viewDirection, normal, smoothness, light);
	return result;
}

bool RenderingLayersOverlap (uint renderingLayerMask, Light light)
{
	return (renderingLayerMask & light.renderingLayerMask) != 0;
}

float2 GetLighting (uint renderingLayerMask, float3 normal, float depth, float3 position, float dither, float smoothness, float3 viewDirection)
{
	ShadowData shadowData = GetShadowData(depth, position, dither);
	
	float2 color = 0.0;
	Light light = GetDirectionalLight(shadowData, normal, position);
	if (RenderingLayersOverlap(renderingLayerMask, light))
	{
		color += GetLighting(viewDirection, normal, smoothness, light);
	}
	
	return color;
}

#endif