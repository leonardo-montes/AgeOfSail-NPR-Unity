#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	#if defined(_AGE_OF_SAIL_RP_SHADOW_PASS)
		UNITY_DEFINE_INSTANCED_PROP(float4, _BreakupMap_ST)
	#elif defined(_AGE_OF_SAIL_RP_COLOR_PASS)
		UNITY_DEFINE_INSTANCED_PROP(float4, _BaseShadowedColor)
		UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColorOverlay)
		UNITY_DEFINE_INSTANCED_PROP(float, _BaseColorSaturation)
		UNITY_DEFINE_INSTANCED_PROP(float4, _BaseShadowedMap_ST)
	#endif
	#if defined(_EDGE_BREAKUP_WARP_PASS)
		UNITY_DEFINE_INSTANCED_PROP(float, _AnimatedLineBoilFramerate)
		UNITY_DEFINE_INSTANCED_PROP(float2, _WorldSpaceUVGradient)
		UNITY_DEFINE_INSTANCED_PROP(float, _EdgeBreakupDistanceFadeMultiplier)
		UNITY_DEFINE_INSTANCED_PROP(float, _EdgeBreakupWidthMultiplier)
		UNITY_DEFINE_INSTANCED_PROP(float, _EdgeBreakupSkew)
	#endif
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig {
	Fragment fragment;
	float4 color;
	float2 baseUV;
	float3 flipbookUVB;
	bool flipbookBlending;
	bool nearFade;
	bool softParticles;
};

InputConfig GetInputConfig (float4 positionSS, float2 baseUV) {
	InputConfig c;
	c.fragment = GetFragment(positionSS);
	c.color = 1.0;
	c.baseUV = baseUV;
	c.flipbookUVB = 0.0;
	c.flipbookBlending = false;
	c.nearFade = false;
	c.softParticles = false;
	return c;
}

float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV (float2 detailUV) {
	return 0.0;
}

float4 GetMask (InputConfig c) {
	return 1.0;
}

float4 GetDetail (InputConfig c) {
	return 0.0;
}

float4 GetBase (InputConfig c) {
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	if (c.flipbookBlending) {
		baseMap = lerp(
			baseMap, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy),
			c.flipbookUVB.z
		);
	}
	if (c.nearFade) {
		float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) /
			INPUT_PROP(_NearFadeRange);
		baseMap.a *= saturate(nearAttenuation);
	}
	if (c.softParticles) {
		float depthDelta = c.fragment.bufferDepth - c.fragment.depth;
		float nearAttenuation = (depthDelta - INPUT_PROP(_SoftParticlesDistance)) /
			INPUT_PROP(_SoftParticlesRange);
		baseMap.a *= saturate(nearAttenuation);
	}
	float4 baseColor = INPUT_PROP(_BaseColor);
	return baseMap * baseColor * c.color;
}

float2 GetDistortion (InputConfig c) {
	float4 rawMap = SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, c.baseUV);
	if (c.flipbookBlending) {
		rawMap = lerp(
			rawMap, SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, c.flipbookUVB.xy),
			c.flipbookUVB.z
		);
	}
	return DecodeNormal(rawMap, INPUT_PROP(_DistortionStrength)).xy;
}

float GetDistortionBlend (InputConfig c) {
	return INPUT_PROP(_DistortionBlend);
}

float GetFinalAlpha (float alpha) {
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

float3 GetNormalTS (InputConfig c) {
	return float3(0.0, 0.0, 1.0);
}

float3 GetEmission (InputConfig c) {
	return GetBase(c).rgb;
}

float GetCutoff (InputConfig c) {
	return INPUT_PROP(_Cutoff);
}

float GetMetallic (InputConfig c) {
	return 0.0;
}

float GetSmoothness (InputConfig c) {
	return 0.0;
}

float GetFresnel (InputConfig c) {
	return 0.0;
}

#if defined(_AGE_OF_SAIL_RP_SHADOW_PASS)	
	TEXTURE2D(_BreakupMap);
	SAMPLER(sampler_BreakupMap);
	
	float GetBreakup (InputConfig c) {
		float4 baseST = INPUT_PROP(_BreakupMap_ST);
		return SAMPLE_TEXTURE2D(_BreakupMap, sampler_BreakupMap, c.baseUV * baseST.xy + baseST.zw).r;
	}
#elif defined(_AGE_OF_SAIL_RP_COLOR_PASS)
	TEXTURE2D(_BaseShadowedMap);
	SAMPLER(sampler_BaseShadowedMap);

	float4 GetBaseShadowed (InputConfig c) {
		float4 baseMap = SAMPLE_TEXTURE2D(_BaseShadowedMap, sampler_BaseShadowedMap, c.baseUV);
		if (c.flipbookBlending) {
			baseMap = lerp(
				baseMap, SAMPLE_TEXTURE2D(_BaseShadowedMap, sampler_BaseShadowedMap, c.flipbookUVB.xy),
				c.flipbookUVB.z
			);
		}
		if (c.nearFade) {
			float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) /
				INPUT_PROP(_NearFadeRange);
			baseMap.a *= saturate(nearAttenuation);
		}
		if (c.softParticles) {
			float depthDelta = c.fragment.bufferDepth - c.fragment.depth;
			float nearAttenuation = (depthDelta - INPUT_PROP(_SoftParticlesDistance)) /
				INPUT_PROP(_SoftParticlesRange);
			baseMap.a *= saturate(nearAttenuation);
		}
		float4 baseColor = INPUT_PROP(_BaseShadowedColor);
		return baseMap * baseColor * c.color;
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

#if defined(_EDGE_BREAKUP_WARP_PASS)
	float GetWidthMultiplier()
	{
		return INPUT_PROP(_EdgeBreakupWidthMultiplier);
	}

	float GetAnimatedLineBoilFramerate()
	{
		return INPUT_PROP(_AnimatedLineBoilFramerate);
	}

	float2 GetWorldSpaceUVGradient()
	{
		return INPUT_PROP(_WorldSpaceUVGradient);
	}

	float GetDistanceFadeMultiplier()
	{
		return INPUT_PROP(_EdgeBreakupDistanceFadeMultiplier);
	}

	float GetSkew()
	{
		return INPUT_PROP(_EdgeBreakupSkew);
	}
#endif

#endif