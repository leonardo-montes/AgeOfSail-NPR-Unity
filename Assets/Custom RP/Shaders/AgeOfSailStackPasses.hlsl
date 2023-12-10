#ifndef AGE_OF_SAIL_STACK_PASSES_INCLUDED
#define AGE_OF_SAIL_STACK_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_Source);
TEXTURE2D(_Source2);

float4 _Source_TexelSize;

float4 GetSourceTexelSize () {
	return _Source_TexelSize;
}

float4 GetSource(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_Source, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceBicubic (float2 screenUV) {
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_Source, sampler_linear_clamp), screenUV,
		_Source_TexelSize.zwxy, 1.0, 0.0
	);
}

float4 GetSource2(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_Source2, sampler_linear_clamp, screenUV, 0);
}

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
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

float4 CopyPassFragment (Varyings input) : SV_TARGET {
	return GetSource(input.screenUV);
}

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

float4 FinalShadowPassFragment (Varyings input) : SV_TARGET {
	float4 shadow = GetSource(input.screenUV);
	float4 blur = GetSource2(input.screenUV);

	float4 result;

	// R: Shadows
	// 'To produce hard-edged silhouettes with rounded corners, we threshold the Blur Pass red channel.'
	result.r = SteppedGradient(blur.r, int(shadow.b * 8));
	// 'We create the Fuchs-inspired “inner glow” effect by inverting the Blur Pass and
    //  clamping it to add a bit of light to the interior of the dark regions.'
	result.r += ((1.0 - blur.r - 0.5) * 0.1) * (1.0 - result.r);

	//result.r = shadow.r;

	// G: Specular (for bloom)
	// 'Here we also add bloom effects based on the Final Shadow Pass green channel.'
	result.g = shadow.g + blur.g;

	// Unused
	result.b = shadow.b;
	result.a = shadow.a;

	return result;
}

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
	for (int j = 0; j <= BLUR_KSIZE; ++j) {
		kernel[BLUR_KSIZE+j] = kernel[BLUR_KSIZE-j] = normpdf(float(j), BLUR_SIGMA);
	}

	for (int j = 0; j < BLUR_MSIZE; ++j) {
		Z += kernel[j];
	}
}

float4 HorizontalBlurPassFragment (Varyings input) : SV_TARGET {
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float3 res = 0.0;
	float texelSize = _BlurSigma * (_ScreenParams.y / _ScreenParams.x);
	for (int i=-BLUR_KSIZE; i <= BLUR_KSIZE; ++i) {
		res += kernel[BLUR_KSIZE + i] * GetSource((input.screenUV.xy + float2(float(i) * texelSize, 0.0))).rgb;
	}
	
	return float4(res / Z, 1.0);
}

float4 VerticalBlurPassFragment (Varyings input) : SV_TARGET {
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float3 res = 0.0;
	float texelSize = _BlurSigma;
	for (int i=-BLUR_KSIZE; i <= BLUR_KSIZE; ++i) {
		res += kernel[BLUR_KSIZE + i] * GetSource((input.screenUV.xy + float2(0.0, float(i) * texelSize))).rgb;
	}

	return float4(res / Z, 1.0);
}

#endif