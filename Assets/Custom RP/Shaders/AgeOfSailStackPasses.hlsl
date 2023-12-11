#ifndef AGE_OF_SAIL_STACK_PASSES_INCLUDED
#define AGE_OF_SAIL_STACK_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_Source);
TEXTURE2D(_Source2);
TEXTURE2D(_Source3);
TEXTURE2D(_Source4);

float4 _Source_TexelSize;

float4 GetSourceTexelSize ()
{
	return _Source_TexelSize;
}

float4 GetSource(float2 screenUV)
{
	return SAMPLE_TEXTURE2D_LOD(_Source, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceBicubic (float2 screenUV)
{
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_Source, sampler_linear_clamp), screenUV,
		_Source_TexelSize.zwxy, 1.0, 0.0
	);
}

float4 GetSource2(float2 screenUV)
{
	return SAMPLE_TEXTURE2D_LOD(_Source2, sampler_linear_clamp, screenUV, 0);
}

float4 GetSource3(float2 screenUV)
{
	return SAMPLE_TEXTURE2D_LOD(_Source3, sampler_linear_clamp, screenUV, 0);
}

float4 GetSource4(float2 screenUV)
{
	return SAMPLE_TEXTURE2D_LOD(_Source4, sampler_linear_clamp, screenUV, 0);
}

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID)
{
	Varyings output;
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}

#if defined(_AGE_OF_SAIL_RP_SHADOW_COLORED_PASS)
struct Buffers 
{
	float4 buffer0 : SV_TARGET0;
	float4 buffer1 : SV_TARGET1;
};
#endif

#if defined(_AGE_OF_SAIL_RP_SHADOW_COLORED_PASS)
Buffers CopyPassFragment (Varyings input)
{
	Buffers buffers;
	buffers.buffer0 = GetSource(input.screenUV);
	buffers.buffer1 = GetSource2(input.screenUV);
	return buffers;
}
#else
float4 CopyPassFragment (Varyings input) : SV_TARGET
{
	return GetSource(input.screenUV);
}
#endif

float _ShadowThreshold;
float _ShadowThresholdSoftness;

float SteppedGradient (float x, int n)
{
	float softness = 0.01;

	float res = 0, t;
	int cnt = max(1, n);
	for (int i = 0; i < cnt; ++i)
	{
		t = ((float(i) / (cnt))) * _ShadowThresholdSoftness * min(n, 1);
		res += smoothstep(_ShadowThreshold + t - softness, _ShadowThreshold + t + softness, x);
	}
	return res / cnt;

	// 'For texture indication, wherever the Shadow Pass blue channel is non-zero, we increase the number of steps in the
	//  thresholding operation.'
	//return smoothstep(_ShadowThreshold - _ShadowThresholdSoftness, _ShadowThreshold + _ShadowThresholdSoftness, x);
}

float3 SteppedGradient (float3 x, float3 y, int n)
{
	float softness = 0.01;

	float newX = saturate(length(x) * 0.6);
	newX = step(0.1, length(x));

	float3 res = 0;
	float t;
	int cnt = max(1, n);
	
	for (int i = 0; i < cnt; ++i)
	{
		t = ((float(i) / (cnt))) * _ShadowThresholdSoftness * min(n, 1);
		res += y * smoothstep(_ShadowThreshold + t - softness, _ShadowThreshold + t + softness, newX);
	}
	return res / cnt;

	// 'For texture indication, wherever the Shadow Pass blue channel is non-zero, we increase the number of steps in the
	//  thresholding operation.'
	//return smoothstep(_ShadowThreshold - _ShadowThresholdSoftness, _ShadowThreshold + _ShadowThresholdSoftness, x);
}

#if defined(_AGE_OF_SAIL_RP_SHADOW_COLORED_PASS)
Buffers FinalShadowPassFragment (Varyings input)
{
	float4 shadow = GetSource(input.screenUV);
	float4 shadowSpecThresh = GetSource2(input.screenUV);
	float4 blur0 = GetSource3(input.screenUV);
	float4 blur1 = GetSource4(input.screenUV);

	// Shadows
	// 'To produce hard-edged silhouettes with rounded corners, we threshold the Blur Pass red channel.'
	//float3 resultShadow = SteppedGradient(blur0.r, int(shadow.b * 8));
	float3 resultShadow = SteppedGradient(blur0.rgb, shadow.rgb, int(shadowSpecThresh.a * 8));
	// 'We create the Fuchs-inspired “inner glow” effect by inverting the Blur Pass and
    //  clamping it to add a bit of light to the interior of the dark regions.'
	resultShadow.rgb += (1.0 - blur0.rgb - 0.5) * 0.3 * saturate(1.0 - length(resultShadow.rgb));

	// Specular
	float3 resultSpec = shadowSpecThresh.rgb;

	Buffers buffers;
	buffers.buffer0 = float4(resultShadow, 1.0);
	buffers.buffer1 = float4(resultSpec, shadowSpecThresh.a);
	return buffers;
}
#else
float4 FinalShadowPassFragment (Varyings input) : SV_TARGET
{
	float4 shadow = GetSource(input.screenUV);
	float4 blur = GetSource2(input.screenUV);

	float4 result;

	// R: Shadows
	// 'To produce hard-edged silhouettes with rounded corners, we threshold the Blur Pass red channel.'
	result.r = SteppedGradient(blur.r, int(shadow.b * 8));
	// 'We create the Fuchs-inspired “inner glow” effect by inverting the Blur Pass and
    //  clamping it to add a bit of light to the interior of the dark regions.'
	result.r += ((1.0 - blur.r - 0.5) * 0.1) * (1.0 - result.r);

	// G: Specular
	result.g = shadow.g;

	// Unused
	result.b = shadow.b;
	result.a = shadow.a;

	return result;
}
#endif

// Separable Gaussian Blur From: https://www.shadertoy.com/view/ltBXRh
float normpdf(in float x, in float sigma)
{
	return 0.39894 * exp(-0.5 * x * x / (sigma * sigma)) / sigma;
}

float _BlurSigma;

#define BLUR_MSIZE 25
#define BLUR_KSIZE (BLUR_MSIZE - 1) / 2
#define BLUR_SIGMA 7

void FillKernel(out float kernel[BLUR_MSIZE], out float Z)
{
	Z = 0;
	int j;
	[unroll]
	for (j = 0; j <= BLUR_KSIZE; ++j)
	{
		kernel[BLUR_KSIZE+j] = kernel[BLUR_KSIZE-j] = normpdf(float(j), BLUR_SIGMA);
	}

	[unroll]
	for (j = 0; j < BLUR_MSIZE; ++j)
	{
		Z += kernel[j];
	}
}

#if defined(_AGE_OF_SAIL_RP_SHADOW_COLORED_PASS)
Buffers HorizontalBlurPassFragment (Varyings input)
{
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float4 res0 = 0.0;
	float4 res1 = 0.0;
	float texelSize = _BlurSigma * (_ScreenParams.y / _ScreenParams.x);
	for (int i=-BLUR_KSIZE; i <= BLUR_KSIZE; ++i)
	{
		res0 += kernel[BLUR_KSIZE + i] * GetSource((input.screenUV.xy + float2(float(i) * texelSize, 0.0)));
		res1 += kernel[BLUR_KSIZE + i] * GetSource2((input.screenUV.xy + float2(float(i) * texelSize, 0.0)));
	}
	
	Buffers buffers;
	buffers.buffer0 = res0 / Z;
	buffers.buffer1 = res1 / Z;
	return buffers;
}
#else
float4 HorizontalBlurPassFragment (Varyings input) : SV_TARGET
{
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float3 res = 0.0;
	float texelSize = _BlurSigma * (_ScreenParams.y / _ScreenParams.x);
	for (int i=-BLUR_KSIZE; i <= BLUR_KSIZE; ++i)
	{
		res += kernel[BLUR_KSIZE + i] * GetSource((input.screenUV.xy + float2(float(i) * texelSize, 0.0))).rgb;
	}
	
	return float4(res / Z, 1.0);
}
#endif

#if defined(_AGE_OF_SAIL_RP_SHADOW_COLORED_PASS)
Buffers VerticalBlurPassFragment (Varyings input)
{
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float4 res0 = 0.0;
	float4 res1 = 0.0;
	float texelSize = _BlurSigma;
	for (int i=-BLUR_KSIZE; i <= BLUR_KSIZE; ++i)
	{
		res0 += kernel[BLUR_KSIZE + i] * GetSource((input.screenUV.xy + float2(0.0, float(i) * texelSize)));
		res1 += kernel[BLUR_KSIZE + i] * GetSource2((input.screenUV.xy + float2(0.0, float(i) * texelSize)));
	}

	Buffers buffers;
	buffers.buffer0 = res0 / Z;
	buffers.buffer1 = res1 / Z;
	return buffers;
}
#else
float4 VerticalBlurPassFragment (Varyings input) : SV_TARGET
{
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float3 res = 0.0;
	float texelSize = _BlurSigma;
	for (int i=-BLUR_KSIZE; i <= BLUR_KSIZE; ++i)
	{
		res += kernel[BLUR_KSIZE + i] * GetSource((input.screenUV.xy + float2(0.0, float(i) * texelSize))).rgb;
	}

	return float4(res / Z, 1.0);
}
#endif

#endif