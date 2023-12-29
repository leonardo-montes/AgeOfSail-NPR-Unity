#ifndef AOSA_LIT_INPUT_INCLUDED
#define AOSA_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	#if defined(_AGE_OF_SAIL_RP_COLOR_SHADOW_PASS)
		UNITY_DEFINE_INSTANCED_PROP(float4, _BreakupMap_ST)
		UNITY_DEFINE_INSTANCED_PROP(float4, _BaseShadowedColor)
		UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColorOverlay)
		UNITY_DEFINE_INSTANCED_PROP(float, _BaseColorSaturation)
	#endif
	#if defined(_WARP_PASS)
		UNITY_DEFINE_INSTANCED_PROP(float, _AnimatedLineBoilFramerate)
		UNITY_DEFINE_INSTANCED_PROP(float2, _WorldSpaceUVGradient)
		UNITY_DEFINE_INSTANCED_PROP(float, _WarpDistanceFadeMultiplier)
		UNITY_DEFINE_INSTANCED_PROP(float, _WarpWidthMultiplier)
		UNITY_DEFINE_INSTANCED_PROP(float, _WarpSkew)
	#endif
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
	Fragment fragment;
	float2 baseUV;
};

InputConfig GetInputConfig (float4 positionSS, float2 baseUV)
{
	InputConfig c;
	c.fragment = GetFragment(positionSS);
	c.baseUV = baseUV;
	return c;
}

float2 TransformBaseUV (float2 baseUV)
{
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (InputConfig c)
{
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	return map * color;
}

float GetFinalAlpha (float alpha)
{
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

float GetCutoff (InputConfig c)
{
	return INPUT_PROP(_Cutoff);
}

float GetSmoothness (InputConfig c)
{
	return INPUT_PROP(_Smoothness);
}

#if defined(_AGE_OF_SAIL_RP_COLOR_SHADOW_PASS)
	TEXTURE2D(_BreakupMap);
	SAMPLER(sampler_BreakupMap);

	float GetBreakup (InputConfig c)
	{
		float4 baseST = INPUT_PROP(_BreakupMap_ST);
		return SAMPLE_TEXTURE2D(_BreakupMap, sampler_BreakupMap, c.baseUV * baseST.xy + baseST.zw).r;
	}

	TEXTURE2D(_BaseShadowedMap);
	SAMPLER(sampler_BaseShadowedMap);

	float4 GetBaseShadowed (InputConfig c)
	{
		float4 map = SAMPLE_TEXTURE2D(_BaseShadowedMap, sampler_BaseShadowedMap, c.baseUV);
		float4 color = INPUT_PROP(_BaseShadowedColor);
		return map * color;
	}

	float4 GetBaseColorOverlay()
	{
		return INPUT_PROP(_BaseColorOverlay);
	}

	float4 GetBaseColorSaturation()
	{
		return INPUT_PROP(_BaseColorSaturation);
	}
#endif

#if defined(_WARP_PASS)
	float GetWidthMultiplier()
	{
		return INPUT_PROP(_WarpWidthMultiplier);
	}

	int GetAnimatedLineBoilFramerate()
	{
		return INPUT_PROP(_AnimatedLineBoilFramerate);
	}

	float2 GetWorldSpaceUVGradient()
	{
		return INPUT_PROP(_WorldSpaceUVGradient);
	}

	float GetDistanceFadeMultiplier()
	{
		return INPUT_PROP(_WarpDistanceFadeMultiplier);
	}

	float GetSkew()
	{
		return INPUT_PROP(_WarpSkew);
	}
#endif

#endif