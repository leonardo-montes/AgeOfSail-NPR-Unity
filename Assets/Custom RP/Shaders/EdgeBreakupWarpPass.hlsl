#ifndef EDGE_BREAKUP_PASS_INCLUDED
#define EDGE_BREAKUP_PASS_INCLUDED

//#define _COMPENSATE_DISTANCE
//#define _USE_SMOOTH_UV_GRADIENT
//#define _USE_SKEWED_META_TEXTURE

struct Attributes {
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	#if defined(_USE_SMOOTH_UV_GRADIENT)
		float4 tangentOS : TANGENT;
	#endif
    float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	#if defined(_USE_SMOOTH_UV_GRADIENT)
		float3 tangentVS : VAR_TANGENT;
		float3 binormalVS : VAR_BINORMAL;
		float4 uvGradWS : VAR_UV_GRADIENT;
	#endif
	float2 baseUV : VAR_BASE_UV;
	#if defined(_COMPENSATE_DISTANCE)
		float dist : VAR_DIST;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

// 4.1. Edge inflation
void Inflate(inout float4 positionCS_SS, in float3 normalWS, in float2 screenSize, in float offsetDistance, in float distanceFromCamera)
{
	float3 normalCS_SS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
	float2 offset = normalize(normalCS_SS.xy) / screenSize * positionCS_SS.w * offsetDistance;
	#if defined(_COMPENSATE_DISTANCE)
    	offset *= CompensateDistance(1.0, distanceFromCamera);
	#endif
	positionCS_SS.xy += offset;
}

float _EdgeBreakupDistance;
float4 _Test;

Varyings EdgeBreakupPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);

	float3 normalWS = mul((float3x3)UNITY_MATRIX_M, input.normalOS);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	#if defined(_COMPENSATE_DISTANCE)
		float distanceFromCamera = length(_WorldSpaceCameraPos.xyz - positionWS);
		output.dist = distanceFromCamera;
	#else
		float distanceFromCamera = 1.0;
	#endif
	output.positionCS_SS = TransformWorldToHClip(positionWS);
	Inflate(output.positionCS_SS, normalWS, _ScreenParams.xy, _EdgeBreakupDistance, distanceFromCamera);

	#if defined(_USE_SMOOTH_UV_GRADIENT)
		output.uvGradWS = _Test.xxzz;

		float3 tangentWS = mul((float3x3)UNITY_MATRIX_M, input.tangentOS.xyz);
		float3 binormalWS = normalize(cross(normalWS, tangentWS) * input.tangentOS.w);

		output.tangentVS = mul((float3x3)UNITY_MATRIX_V, tangentWS);
		output.binormalVS = mul((float3x3)UNITY_MATRIX_V, binormalWS);
	#endif

	output.baseUV.xy = TransformBaseUV(input.baseUV.xy);

	return output;
}

TEXTURE2D(_EdgeBreakupWarpTexture);
SAMPLER(sampler_EdgeBreakupWarpTexture);

float _EdgeBreakupWarpTextureScale;
float _EdgeBreakupSkew;

float4 EdgeBreakupPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);

	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	float4 warp = float4(0.5, 0.5, 0.0, 1.0);
	//warp.rg = SAMPLE_TEXTURE2D(_EdgeBreakupWarpTexture, sampler_EdgeBreakupWarpTexture, input.baseUV * _EdgeBreakupWarpTextureScale).rg;.

	float2 gradU, gradV;
	#if defined(_USE_SMOOTH_UV_GRADIENT)
		GetApproximateSmoothUVGradient(input.baseUV, input.uvGradWS, input.tangentVS, input.binormalVS, gradU, gradV);
	#else
		GetUVGradientFromDerivatives(input.baseUV, gradU, gradV);
	#endif

	float intensity = 1.0;

	// MetaTexture sampling
	#if defined(_USE_SKEWED_META_TEXTURE)
		warp = SampleMetaTextureSkewed(_EdgeBreakupWarpTexture, sampler_EdgeBreakupWarpTexture, input.baseUV, gradU, gradV, _EdgeBreakupWarpTextureScale, _EdgeBreakupSkew);
	#else
		warp = SampleMetaTexture(_EdgeBreakupWarpTexture, sampler_EdgeBreakupWarpTexture, input.baseUV, gradU, gradV, _EdgeBreakupWarpTextureScale);
	#endif

    // '4.4. Compensating for distance'
	#if defined(_COMPENSATE_DISTANCE)
    	CompensateDistance(intensity, input.dist, warp);
	#endif

	return warp;
}

#endif