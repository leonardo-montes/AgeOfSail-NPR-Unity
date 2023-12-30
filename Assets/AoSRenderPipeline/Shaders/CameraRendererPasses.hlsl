#ifndef AOS_CAMERA_RENDERER_STACK_PASSES_INCLUDED
#define AOS_CAMERA_RENDERER_STACK_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_Source0);
TEXTURE2D(_Source1);
TEXTURE2D(_Source2);

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
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}


// ------------------------------------------------------------------------------------------------------------
// -                                              COPY PASS                                                   -
// ------------------------------------------------------------------------------------------------------------
float4 CopyPassFragment (Varyings input) : SV_TARGET
{
	return GetSource(_Source0, input.screenUV);
}


// ------------------------------------------------------------------------------------------------------------
// -                                           DOWNSAMPLE PASS                                                -
// ------------------------------------------------------------------------------------------------------------

float4 DownsamplePassFragment (Varyings input) : SV_TARGET
{
	// see '2.6 - Box Sampling' at 'https://catlikecoding.com/unity/tutorials/advanced-rendering/bloom/'
	float2 uv = input.screenUV;
	float4 offset = _Source0_TexelSize.xyxy * float2(-1.0, 1.0).xxyy;
	return (GetSource(_Source0, uv + offset.zw) +
		    GetSource(_Source0, uv + offset.zy) +
		    GetSource(_Source0, uv + offset.xw) +
		    GetSource(_Source0, uv + offset.xy)) * 0.25;
}


// ------------------------------------------------------------------------------------------------------------
// -                                             1D BLUR PASS                                                 -
// ------------------------------------------------------------------------------------------------------------

float4 Sample1DGaussian (float2 uv, float2 offset)
{
	return GetSource(_Source0, uv)                   * 0.227 +
		   GetSource(_Source0, uv + offset * -3.231) * 0.07  +
		   GetSource(_Source0, uv + offset *  3.231) * 0.07  +
		   GetSource(_Source0, uv + offset * -1.385) * 0.316 + 
		   GetSource(_Source0, uv + offset *  1.385) * 0.316;
}

float4 HorizontalBlurPassFragment (Varyings input) : SV_TARGET
{
	return Sample1DGaussian(input.screenUV, float2(_Source0_TexelSize.x, 0.0));
}

float4 VerticalBlurPassFragment (Varyings input) : SV_TARGET
{
	return Sample1DGaussian(input.screenUV, float2(0.0, _Source0_TexelSize.y));
}


// ------------------------------------------------------------------------------------------------------------
// -                                           FINAL SHADOW PASS                                              -
// ------------------------------------------------------------------------------------------------------------

int _ShadowStepCount;
float _ShadowThreshold;
float _ShadowThresholdSoftness;
float _ShadowInnerGlow;

float SteppedGradient(float value, float threshold, float softness, float stepCount)
{
	float thresholdedValue = (value - threshold) / softness;
	return saturate(floor(thresholdedValue * stepCount) / stepCount);
}

float4 FinalShadowPassFragment (Varyings input) : SV_TARGET
{
	float2 uv = input.screenUV;

	float4 shadow = GetSource(_Source0, uv);
	float4 softBlur = GetSource(_Source1, uv);
	float4 heavyBlur = GetSource(_Source2, uv);

	// 5.1. Shadow shapes, inner glow, and indication
	// 'For texture indication, wherever the Shadow Pass blue
	//  channel is non-zero, we increase the number of steps in the
	//  thresholding operation.'
	float stepCount = floor(shadow.b * _ShadowStepCount + 1.0);
	float newShadow = SteppedGradient(softBlur.r, _ShadowThreshold, shadow.b * _ShadowThresholdSoftness, stepCount);
	
	// 'We create the Fuchsinspired “inner glow” effect by inverting the Blur Pass and
	//  clamping it to add a bit of light to the interior of the dark regions.'
	newShadow += (1.0 - heavyBlur.r) * (1.0 - newShadow) * _ShadowInnerGlow;

	// Keep specular for the Final Color Pass
	float specularBloom = heavyBlur.g + 0.5 * softBlur.g;
	
	return float4(newShadow, specularBloom, shadow.g, 1.0);
}


// ------------------------------------------------------------------------------------------------------------
// -                                           FINAL COLOR PASS                                               -
// ------------------------------------------------------------------------------------------------------------

float _WarpWidth;
float _WarpBloom;

float4 FinalColorPassFragment (Varyings input) : SV_TARGET
{
	// Warp
	float2 warp = (GetSource(_Source1, input.screenUV).rg * 2.0 - 1.0) * _WarpWidth;

	// Sample color
	float4 color = GetSource(_Source0, input.screenUV + warp);

	// Bloom
	float4 finalShadow = GetSource(_Source2, input.screenUV + warp * _WarpBloom);
	color += finalShadow.g;

	return color;
}

#endif