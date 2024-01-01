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
TEXTURE2D(_Source9);
TEXTURE2D(_Source10);
TEXTURE2D(_Source11);
TEXTURE2D(_Source12);
TEXTURE2D(_Source13);

float4 _Source0_TexelSize;

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
	if (_ProjectionParams.x < 0.0)
	{
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


// ------------------------------------------------------------------------------------------------------------
// -                                              COPY PASS                                                   -
// ------------------------------------------------------------------------------------------------------------

float4 CopyPassFragment (Varyings input) : SV_TARGET
{
	return GetSource(_Source0, input.screenUV);
}


// ------------------------------------------------------------------------------------------------------------
// -                                           COPY ARRAY PASS                                                -
// ------------------------------------------------------------------------------------------------------------

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


// ------------------------------------------------------------------------------------------------------------
// -                                      DOWNSAMPLE ARRAY PASS                                               -
// ------------------------------------------------------------------------------------------------------------

// see '2.6 - Box Sampling' at 'https://catlikecoding.com/unity/tutorials/advanced-rendering/bloom/'
float4 BoxSample (TEXTURE2D(source), float2 uv, float4 offset)
{
	return (GetSource(source, uv + offset.zw) +
		    GetSource(source, uv + offset.zy) +
		    GetSource(source, uv + offset.xw) +
		    GetSource(source, uv + offset.xy)) * 0.25;
}

Buffers DownsampleArrayPassFragment (Varyings input)
{
	float2 uv = input.screenUV;
	float4 offset = _Source0_TexelSize.xyxy * float2(-1.0, 1.0).xxyy;

	Buffers buffers;
	buffers.buffer0 = BoxSample(_Source0, uv, offset);
	buffers.buffer1 = BoxSample(_Source1, uv, offset);
	buffers.buffer2 = BoxSample(_Source2, uv, offset);
	buffers.buffer3 = BoxSample(_Source3, uv, offset);
	buffers.buffer4 = BoxSample(_Source4, uv, offset);
	return buffers;
}


// ------------------------------------------------------------------------------------------------------------
// -                                          1D BLUR PASS                                                    -
// ------------------------------------------------------------------------------------------------------------

float4 Sample1DGaussian (TEXTURE2D(source), float2 uv, float2 offset)
{
	return GetSource(source, uv)                   * 0.227 +
		   GetSource(source, uv + offset * -3.231) * 0.07  +
		   GetSource(source, uv + offset *  3.231) * 0.07  +
		   GetSource(source, uv + offset * -1.385) * 0.316 + 
		   GetSource(source, uv + offset *  1.385) * 0.316;
}

Buffers HorizontalBlurPassFragment (Varyings input)
{
	float2 uv = input.screenUV;
	float2 offset = float2(_Source0_TexelSize.x, 0.0);

	Buffers buffers;
	buffers.buffer0 = Sample1DGaussian(_Source0, uv, offset);
	buffers.buffer1 = Sample1DGaussian(_Source1, uv, offset);
	buffers.buffer2 = Sample1DGaussian(_Source2, uv, offset);
	buffers.buffer3 = Sample1DGaussian(_Source3, uv, offset);
	buffers.buffer4 = Sample1DGaussian(_Source4, uv, offset);
	return buffers;
}

Buffers VerticalBlurPassFragment (Varyings input)
{
	float2 uv = input.screenUV;
	float2 offset = float2(0.0, _Source0_TexelSize.y);

	Buffers buffers;
	buffers.buffer0 = Sample1DGaussian(_Source0, uv, offset);
	buffers.buffer1 = Sample1DGaussian(_Source1, uv, offset);
	buffers.buffer2 = Sample1DGaussian(_Source2, uv, offset);
	buffers.buffer3 = Sample1DGaussian(_Source3, uv, offset);
	buffers.buffer4 = Sample1DGaussian(_Source4, uv, offset);
	return buffers;
}


// ------------------------------------------------------------------------------------------------------------
// -                                           COMPOSITING PASS                                               -
// ------------------------------------------------------------------------------------------------------------

int _TotalLightCount;
float4 _LightColors[MAX_LIGHT_COUNT];

int _ShadowStepCount;
float _ShadowThreshold;
float _ShadowThresholdSoftness;
float _ShadowInnerGlow;

void SampleTextures(in TEXTURE2D(softBlurSource), in TEXTURE2D(heavyBlurSource),
	inout float2 shadows[MAX_LIGHT_COUNT], inout float speculars[MAX_LIGHT_COUNT], inout float blooms[MAX_LIGHT_COUNT], in float2 uv, in int startId)
{
	float4 softBlur = GetSource(softBlurSource, uv);
	float4 heavyBlur = GetSource(heavyBlurSource, uv);

	shadows[startId].r = softBlur.r;
	shadows[startId].g = heavyBlur.r;
	speculars[startId] = step(0.5, softBlur.g);
	blooms[startId] = heavyBlur.g + 0.5 * softBlur.g;

	shadows[startId + 1].r = softBlur.b;
	shadows[startId + 1].g = heavyBlur.b;
	speculars[startId + 1] = step(0.5, softBlur.a);
	blooms[startId + 1] = heavyBlur.a + 0.5 * softBlur.a;
}

void SampleTextures(in TEXTURE2D(soft0), in TEXTURE2D(heavy0), in TEXTURE2D(soft1), in TEXTURE2D(heavy1), in TEXTURE2D(soft2), in TEXTURE2D(heavy2),
	in TEXTURE2D(soft3), in TEXTURE2D(heavy3), in TEXTURE2D(soft4), in TEXTURE2D(heavy4), in TEXTURE2D(soft5), in TEXTURE2D(heavy5),
	out float2 shadows[MAX_LIGHT_COUNT], out float speculars[MAX_LIGHT_COUNT], out float blooms[MAX_LIGHT_COUNT], in float2 uv)
{
	int i;
	[unroll]
	for (i = 0; i < MAX_LIGHT_COUNT; ++i)
	{
		shadows[i] = 0.0;
		speculars[i] = 0.0; 
		blooms[i] = 0.0; 
	}

	if (_TotalLightCount > 0)
		SampleTextures(soft0, heavy0, shadows, speculars, blooms, uv, 0);
	if (_TotalLightCount > 2)
		SampleTextures(soft1, heavy1, shadows, speculars, blooms, uv, 2);
	if (_TotalLightCount > 4)
		SampleTextures(soft2, heavy2, shadows, speculars, blooms, uv, 4);
	if (_TotalLightCount > 6)
		SampleTextures(soft3, heavy3, shadows, speculars, blooms, uv, 6);
	if (_TotalLightCount > 8)
		SampleTextures(soft4, heavy4, shadows, speculars, blooms, uv, 8);
}

float SteppedGradient(float value, float threshold, float softness, float stepCount)
{
	float thresholdedValue = (value - threshold) / softness;
	return saturate(floor(thresholdedValue * stepCount) / stepCount);
}

void ApplyColoredShadows(inout float3 color, in float3 shadowedColor, in float3 litColor,
	in float2 shadows[MAX_LIGHT_COUNT], in float speculars[MAX_LIGHT_COUNT], in float breakup)
{
	if (_TotalLightCount <= 0)
	{
		color = shadowedColor;
		return;
	}

	float stepCount = floor(breakup * _ShadowStepCount + 1.0);
	float threshold = _ShadowThreshold;
	float thresholdSoftness = breakup * _ShadowThresholdSoftness;
	float innerGlow = _ShadowInnerGlow;

	float shadow = SteppedGradient(shadows[0].r, threshold, thresholdSoftness, stepCount);
	shadow = lerp((1.0 - shadows[0].g * innerGlow * 2.0) * shadow, shadow + (1.0 - shadows[0].g) * (1.0 - shadow) * innerGlow, _LightColors[0].a);

	color = lerp(shadowedColor, litColor * _LightColors[0].rgb, shadow) + speculars[0] * _LightColors[0].rgb;

	for (int i = 1; i < _TotalLightCount; ++i)
	{
		shadow = SteppedGradient(shadows[i].r, threshold, thresholdSoftness, stepCount);
		shadow = lerp((1.0 - shadows[i].g * innerGlow * 2.0) * shadow, shadow + (1.0 - shadows[i].g) * (1.0 - shadow) * innerGlow, _LightColors[i].a);
		color += litColor * _LightColors[i].rgb * shadow + speculars[i] * _LightColors[i].rgb;
	}
}

struct CompositingBuffers
{
	float4 buffer0 : SV_TARGET0;
	float4 buffer1 : SV_TARGET1;
	float4 buffer2 : SV_TARGET2;
	float4 buffer3 : SV_TARGET3;
};

CompositingBuffers CompositingPassFragment (Varyings input)
{	
	float4 litColor = GetSource(_Source0, input.screenUV);
	float4 shadowedColor = GetSource(_Source1, input.screenUV);
	
	float breakup = shadowedColor.a;

	float4 overlay = 0.5;
	float saturation = 1.0;

	float2 shadows[MAX_LIGHT_COUNT];
	float speculars[MAX_LIGHT_COUNT];
	float blooms[MAX_LIGHT_COUNT];
	SampleTextures(_Source2, _Source3, _Source4, _Source5, _Source6, _Source7, _Source8, _Source9, _Source10, _Source11, _Source12, _Source13, shadows, speculars, blooms, input.screenUV);

	float3 color = 0.0;
	ApplyColoredShadows(color.rgb, shadowedColor.rgb, litColor.rgb, shadows, speculars, breakup);

	// Reapply sky
	color = lerp(color, litColor.rgb, 1.0 - litColor.a);

	CompositingBuffers buffers;
	buffers.buffer0 = float4(color, 1.0);
	buffers.buffer1 = float4(blooms[0], blooms[1], blooms[2], blooms[3]);
	buffers.buffer2 = float4(blooms[4], blooms[5], blooms[6], blooms[7]);
	buffers.buffer3 = float4(blooms[8], blooms[9], 0.0, 0.0);
	return buffers;
}


// ------------------------------------------------------------------------------------------------------------
// -                                       FINAL COMPOSITING PASS                                             -
// ------------------------------------------------------------------------------------------------------------

void SampleTexturesBloom(in TEXTURE2D(source), inout float blooms[MAX_LIGHT_COUNT], in float2 uv, in int startId)
{
	float4 bloom = GetSource(source, uv);

	blooms[startId] = bloom.r;
	blooms[startId + 1] = bloom.g;
	blooms[startId + 2] = bloom.b;
	blooms[startId + 3] = bloom.a;
}

void SampleTexturesBloomShort(in TEXTURE2D(source), inout float blooms[MAX_LIGHT_COUNT], in float2 uv, in int startId)
{
	float4 bloom = GetSource(source, uv);

	blooms[startId] = bloom.r;
	blooms[startId + 1] = bloom.g;
}

void SampleTexturesBloom(in TEXTURE2D(bloom0), in TEXTURE2D(bloom1), in TEXTURE2D(bloom2), out float blooms[MAX_LIGHT_COUNT], in float2 uv)
{
	int i;
	[unroll]
	for (i = 0; i < MAX_LIGHT_COUNT; ++i)
	{
		blooms[i] = 0.0; 
	}
	
	SampleTexturesBloom(bloom0, blooms, uv, 0);
	if (_TotalLightCount > 4)
		SampleTexturesBloom(bloom1, blooms, uv, 4);
	if (_TotalLightCount > 8)
		SampleTexturesBloomShort(bloom2, blooms, uv, 8);
}

void ApplyColoredBlooms(inout float3 color, in float blooms[MAX_LIGHT_COUNT])
{
	if (_TotalLightCount <= 0)
	{
		return;
	}

	for (int i = 0; i < _TotalLightCount; ++i)
	{
		color += blooms[i] * _LightColors[i].rgb;
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

float _WarpWidth;
float _WarpBloom;

float4 _Overlay;
float _Saturation;

float4 FinalCompositingPassFragment (Varyings input) : SV_TARGET
{
	float2 warp = (GetSource(_Source1, input.screenUV).rg * 2.0 - 1.0) * _WarpWidth;
	float2 warpedUV = input.screenUV + warp;

	float4 color = GetSource(_Source0, warpedUV);

	if (_WarpBloom < 0.5)
	{
		color.a = GetSource(_Source0, input.screenUV).a;
	}
	
	float blooms[MAX_LIGHT_COUNT];
	SampleTexturesBloom(_Source2, _Source3, _Source4, blooms, input.screenUV + warp * _WarpBloom);
	ApplyColoredBlooms(color.rgb, blooms);

	// Overlay
	color.rgb = lerp(color.rgb, BlendMode_Overlay(color.rgb, _Overlay.rgb), _Overlay.a);

	// Saturation
	color.rgb = Saturation(color.rgb, _Saturation);

	return color;
}


// ------------------------------------------------------------------------------------------------------------
// -                                       DEBUG MULTIPLE PASS                                                -
// ------------------------------------------------------------------------------------------------------------

int _DebugCount;

float4 DebugMultiplePassFragment (Varyings input) : SV_TARGET
{
	float2 uv = input.screenUV;

	if (_DebugCount < 2)
		return GetSource(_Source0, uv);
	
	float4 color = 0.0;

	int cnt, i;
	float x, y, mask;

	float2 offset = _Source0_TexelSize.xy;

	cnt = ceil(sqrt(_DebugCount));

	TEXTURE2D(sources)[14];
	sources[0] = _Source0;
	sources[1] = _Source1;
	sources[2] = _Source2;
	sources[3] = _Source3;
	sources[4] = _Source4;
	sources[5] = _Source5;
	sources[6] = _Source6;
	sources[7] = _Source7;
	sources[8] = _Source8;
	sources[9] = _Source9;
	sources[10] = _Source10;
	sources[11] = _Source11;
	sources[12] = _Source12;
	sources[13] = _Source13;

	for (i = 0; i < _DebugCount; ++i)
	{
		float2 cntOff = float(cnt) + offset * cnt;
		x = (float(i) % cnt) / cntOff.x;
		y = (cnt - 1 - floor(float(i) / cnt)) / cntOff.y;

		x += (float(i) % cnt) * offset.x;
		y += floor(float(i) / cnt) * offset.y;

		mask = step(x, uv.x) * step(y, uv.y) * step(uv.x, x + 1.0 / cntOff.x) * step(uv.y, y + 1.0 / cntOff.y);
		color += GetSource(sources[i], (uv + float2(-x, -y)) * cntOff) * mask;
	}

	return color;
}

#endif