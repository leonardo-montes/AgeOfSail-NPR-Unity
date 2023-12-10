#ifndef AGE_OF_SAIL_LIGHTING_INCLUDED
#define AGE_OF_SAIL_LIGHTING_INCLUDED

float2 IncomingLight (Surface surface, Light light)
{
	//return saturate(dot(surface.normal, light.direction) * light.attenuation);
	float ndotl = saturate(dot(surface.normal, light.direction) * 0.5 + 0.5);
	return ndotl * step(0.5, light.attenuation);
}

float2 SpecularHighlight (Surface surface, BRDF brdf, Light light)
{
	return step(0.1, SpecularStrength(surface, brdf, light) * brdf.specular * light.attenuation);
}

float2 GetLighting (Surface surface, BRDF brdf, Light light)
{
	float2 result;
	result.r = IncomingLight(surface, light);
	result.g = SpecularHighlight(surface, brdf, light);
	return result;
}

bool RenderingLayersOverlap (Surface surface, Light light)
{
	return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

float2 GetLighting (Surface surfaceWS, BRDF brdf, GI gi)
{
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	float2 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++)
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		if (RenderingLayersOverlap(surfaceWS, light))
		{
			color += GetLighting(surfaceWS, brdf, light);
		}
	}
	
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++)
		{
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light))
			{
				color += GetLighting(surfaceWS, brdf, light);
			}
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++)
		{
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light))
			{
				color += GetLighting(surfaceWS, brdf, light);
			}
		}
	#endif
	return color;
}

#endif