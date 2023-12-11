#ifndef AGE_OF_SAIL_LIGHTING_COLORED_INCLUDED
#define AGE_OF_SAIL_LIGHTING_COLORED_INCLUDED

float3 IncomingLight (Surface surface, Light light)
{
	float ndotl = saturate(dot(surface.normal, light.direction) * 0.5 + 0.5);
	return ndotl * step(0.5, light.attenuation) * light.color;
}

float3 SpecularHighlight (Surface surface, BRDF brdf, Light light)
{
	return step(0.1, SpecularStrength(surface, brdf, light) * length(brdf.specular) * light.attenuation) * light.color;
}

void GetLighting (Surface surface, BRDF brdf, Light light, inout float3 color, inout float3 spec)
{
	color += IncomingLight(surface, light);
	spec += SpecularHighlight(surface, brdf, light);
}

bool RenderingLayersOverlap (Surface surface, Light light)
{
	return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

void GetLighting (Surface surfaceWS, BRDF brdf, GI gi, out float3 color, out float3 spec)
{
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	color = 0.0;
	spec = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++)
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		if (RenderingLayersOverlap(surfaceWS, light))
		{
			GetLighting(surfaceWS, brdf, light, color, spec);
		}
	}
	
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++)
		{
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light))
			{
				GetLighting(surfaceWS, brdf, light, color, spec);
			}
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++)
		{
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light))
			{
				GetLighting(surfaceWS, brdf, light, color, spec);
			}
		}
	#endif
}

#endif