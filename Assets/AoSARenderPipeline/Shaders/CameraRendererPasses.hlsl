#ifndef AOSA_CAMERA_RENDERER_STACK_PASSES_INCLUDED
#define AOSA_CAMERA_RENDERER_STACK_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_Source0);
TEXTURE2D(_Source1);
TEXTURE2D(_Source2);
TEXTURE2D(_Source3);
TEXTURE2D(_Source4);
TEXTURE2D(_Source5);
TEXTURE2D(_Source6);
TEXTURE2D(_Source7);
TEXTURE2D(_Source8);

float4 GetSource(TEXTURE2D(source), float2 screenUV)
{
	return SAMPLE_TEXTURE2D_LOD(source, sampler_linear_clamp, screenUV, 0);
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

struct Buffers
{
	float4 buffer0 : SV_TARGET0;
	float4 buffer1 : SV_TARGET1;
	float4 buffer2 : SV_TARGET2;
	float4 buffer3 : SV_TARGET3;
	float4 buffer4 : SV_TARGET4;
};

float4 CopyPassFragment (Varyings input) : SV_TARGET
{
	return GetSource(_Source0, input.screenUV);
}

Buffers CopyArrayPassFragment (Varyings input)
{
	Buffers buffers;
	buffers.buffer0 = GetSource(_Source0, input.screenUV);
	buffers.buffer1 = GetSource(_Source1, input.screenUV);
	buffers.buffer2 = GetSource(_Source2, input.screenUV);
	buffers.buffer3 = GetSource(_Source3, input.screenUV);
	buffers.buffer4 = GetSource(_Source4, input.screenUV);
	return buffers;
}

// Separable Gaussian Blur From: https://www.shadertoy.com/view/ltBXRh
float normpdf(in float x, in float sigma)
{
	return 0.39894 * exp(-0.5 * x * x / (sigma * sigma)) / sigma;
}

float _BlurRadius;

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

void BlurSample(out float4 res[5], in float kernel[BLUR_MSIZE], in float texelSize, in float2 uv, in float2 dir)
{
	int i;
	[unroll]
	for (i = 0; i < 5; ++i)
	{
		res[i] = 0.0;
	}

	float2 currUV;
	[unroll]
	for (i = -BLUR_KSIZE; i <= BLUR_KSIZE; ++i)
	{
		currUV = uv + dir * float(i) * texelSize;
		res[0] += kernel[BLUR_KSIZE + i] * GetSource(_Source0, currUV);
		res[1] += kernel[BLUR_KSIZE + i] * GetSource(_Source1, currUV);
		res[2] += kernel[BLUR_KSIZE + i] * GetSource(_Source2, currUV);
		res[3] += kernel[BLUR_KSIZE + i] * GetSource(_Source3, currUV);
		res[4] += kernel[BLUR_KSIZE + i] * GetSource(_Source4, currUV);
	}
}

Buffers HorizontalBlurPassFragment (Varyings input)
{
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float texelSize = _BlurRadius * (_ScreenParams.y / _ScreenParams.x);
	float4 res[5];
	BlurSample(res, kernel, texelSize, input.screenUV.xy, float2(1.0, 0.0));

	Buffers buffers;
	buffers.buffer0 = res[0] / Z;
	buffers.buffer1 = res[1] / Z;
	buffers.buffer2 = res[2] / Z;
	buffers.buffer3 = res[3] / Z;
	buffers.buffer4 = res[4] / Z;
	return buffers;
}

Buffers VerticalBlurPassFragment (Varyings input)
{
	float kernel[BLUR_MSIZE];
	float Z;
	
	FillKernel(kernel, Z);

	float texelSize = _BlurRadius;
	float4 res[5];
	BlurSample(res, kernel, texelSize, input.screenUV.xy, float2(0.0, 1.0));

	Buffers buffers;
	buffers.buffer0 = res[0] / Z;
	buffers.buffer1 = res[1] / Z;
	buffers.buffer2 = res[2] / Z;
	buffers.buffer3 = res[3] / Z;
	buffers.buffer4 = res[4] / Z;
	return buffers;
}

int _TotalLightCount;
float4 _LightColors[MAX_LIGHT_COUNT];

void GetBlurredShadows(in TEXTURE2D(source), inout float blurredShadows[MAX_LIGHT_COUNT], inout float blurredSpeculars[MAX_LIGHT_COUNT], in float2 uv, in int startId)
{
	float4 data = GetSource(source, uv);

	blurredShadows[startId] = data.r;
	blurredSpeculars[startId] = data.g;

	blurredShadows[startId + 1] = data.b;
	blurredSpeculars[startId + 1] = data.a;
}

void GetBlurredShadows(in TEXTURE2D(source0), in TEXTURE2D(source1), in TEXTURE2D(source2), in TEXTURE2D(source3), in TEXTURE2D(source4), out float blurredShadows[MAX_LIGHT_COUNT], out float blurredSpeculars[MAX_LIGHT_COUNT], in float2 uv)
{
	int i;
	[unroll]
	for (i = 0; i < MAX_LIGHT_COUNT; ++i)
	{
		blurredShadows[i] = 0.0;
		blurredSpeculars[i] = 0.0; 
	}

	float4 data = GetSource(source0, uv);
	blurredShadows[0] = data.b;
	blurredSpeculars[0] = data.a;
	if (_TotalLightCount > 1)
		GetBlurredShadows(source1, blurredShadows, blurredSpeculars, uv, 1);
	if (_TotalLightCount > 3)
		GetBlurredShadows(source2, blurredShadows, blurredSpeculars, uv, 3);
	if (_TotalLightCount > 5)
		GetBlurredShadows(source3, blurredShadows, blurredSpeculars, uv, 5);
	if (_TotalLightCount > 7)
		GetBlurredShadows(source4, blurredShadows, blurredSpeculars, uv, 7);
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

void ApplyColoredShadows(inout float3 color, in float4 shadowedColor, in float3 litColor, in float blurredShadows[MAX_LIGHT_COUNT], in float blurredSpeculars[MAX_LIGHT_COUNT], in float breakup)
{
	if (_TotalLightCount <= 0)
	{
		color = shadowedColor.rgb;
		return;
	}

	int steppingCount = int(breakup * 8);

	// 'To produce hard-edged silhouettes with rounded corners, we threshold the Blur Pass red channel.'
	float shadow = SteppedGradient(blurredShadows[0], steppingCount);

	// 'We create the Fuchs-inspired “inner glow” effect by inverting the Blur Pass and
	//  clamping it to add a bit of light to the interior of the dark regions.'
	shadow += ((1.0 - blurredShadows[0] - 0.5) * 0.1) * (1.0 - shadow) * _LightColors[0].a;

	float specular = step(0.5, blurredSpeculars[0]);

	color = lerp(color, litColor * _LightColors[0].rgb, shadow) + specular * _LightColors[0].rgb;

	for (int i = 1; i < _TotalLightCount; ++i)
	{
		shadow = SteppedGradient(blurredShadows[i], steppingCount);
		shadow += ((1.0 - blurredShadows[i] - 0.5) * 0.1) * (1.0 - shadow) * _LightColors[i].a;
		specular = step(0.5, blurredSpeculars[i]);
		color += litColor * _LightColors[i].rgb * shadow + specular * _LightColors[i].rgb;
	}
}

// From https://www.ryanjuckett.com/photoshop-blend-modes-in-hlsl/
float BlendMode_Overlay(float base, float blend)
{
	return (base <= 0.5) ? 2 * base * blend : 1 - 2 * (1 - base) * (1 - blend);
}

// From https://www.ryanjuckett.com/photoshop-blend-modes-in-hlsl/
float3 BlendMode_Overlay(float3 base, float3 blend)
{
	return float3(BlendMode_Overlay(base.r, blend.r), BlendMode_Overlay(base.g, blend.g), BlendMode_Overlay(base.b, blend.b));
}

// From https://docs.unity3d.com/Packages/com.unity.shadergraph@6.9/manual/Saturation-Node.html
float3 Saturation(float3 color, float saturation)
{
	float luma = dot(color, float3(0.2126729, 0.7151522, 0.0721750));
	return luma.xxx + saturation.xxx * (color - luma.xxx);
}

float4 CompositingPassFragment (Varyings input) : SV_TARGET
{	
	float4 litColor = GetSource(_Source0, input.screenUV);
	float4 shadowedColor = GetSource(_Source1, input.screenUV);
	float4 overlay = GetSource(_Source2, input.screenUV);
	
	float2 shadowBuffer0 = GetSource(_Source3, input.screenUV).rg;
	float breakup = shadowBuffer0.r;
	float saturation = shadowedColor.a;

	float blurredShadows[MAX_LIGHT_COUNT];
	float blurredSpeculars[MAX_LIGHT_COUNT];
	GetBlurredShadows(_Source4, _Source5, _Source6, _Source7, _Source8, blurredShadows, blurredSpeculars, input.screenUV);

	float4 color = float4(0.0, 0.0, 0.0, litColor.a);
	ApplyColoredShadows(color.rgb, shadowedColor, litColor.rgb, blurredShadows, blurredSpeculars, breakup);

	// Overlay
	color.rgb = lerp(color, BlendMode_Overlay(color, overlay.rgb), overlay.a);

	// Saturation
	color.rgb = Saturation(color, saturation);

	// Reapply sky
	color = lerp(color, litColor, 1.0 - litColor.a);

	return color;
}

void ApplyColoredSpeculars(inout float3 color, in float blurredSpeculars[MAX_LIGHT_COUNT])
{
	if (_TotalLightCount <= 0)
	{
		return;
	}

	for (int i = 0; i < _TotalLightCount; ++i)
	{
		color += blurredSpeculars[i] * _LightColors[i].rgb;
	}
}

float _WarpWidth;

float4 FinalCompositingPassFragment (Varyings input) : SV_TARGET
{
	float2 warp = (GetSource(_Source1, input.screenUV).rg * 2.0 - 1.0) * _WarpWidth;
	float2 warpedUV = input.screenUV + warp;
	
	float blurredShadows[MAX_LIGHT_COUNT];
	float blurredSpeculars[MAX_LIGHT_COUNT];
	GetBlurredShadows(_Source2, _Source3, _Source4, _Source5, _Source6, blurredShadows, blurredSpeculars, input.screenUV);
	
	float4 color = GetSource(_Source0, warpedUV);
	ApplyColoredSpeculars(color.rgb, blurredSpeculars);

	return color;
}

#endif