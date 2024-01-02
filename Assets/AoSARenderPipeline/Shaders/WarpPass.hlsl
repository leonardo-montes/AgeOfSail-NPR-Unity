#ifndef AOSA_WARP_PASS_INCLUDED
#define AOSA_WARP_PASS_INCLUDED

struct Attributes
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	#if defined(_USE_SMOOTH_UV_GRADIENT)
		float4 tangentOS : TANGENT;
	#endif
    float2 baseUV : TEXCOORD0;
    float3 normalInflatedOS : TEXCOORD3;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS_SS : SV_POSITION;
	#if defined(_USE_SMOOTH_UV_GRADIENT)
		float4 uvGrad : VAR_UV_GRADIENT;
	#endif
	float2 baseUV : VAR_BASE_UV;
	#if defined(_COMPENSATE_RADIAL_ANGLE)
		float4 screenUV : VAR_SCREEN_UV;
	#endif
	#if defined(_COMPENSATE_DISTANCE)
		float dist : VAR_DIST;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

float _WarpGlobalDistanceFade;

// 4.1. Edge inflation
void Inflate(inout float4 positionCS_SS, in float3 normalCS_SS, in float2 screenSize, in float offsetDistance, in float distanceFromCamera)
{
	float2 offset = normalize(normalCS_SS.xy) / float2((screenSize.y / screenSize.x), 1.0) * positionCS_SS.w * offsetDistance;
	#if defined(_COMPENSATE_DISTANCE)
    	offset *= CompensateDistance(1.0, distanceFromCamera * _WarpGlobalDistanceFade * GetDistanceFadeMultiplier());
	#endif
	positionCS_SS.xy += offset;
}

float _WarpWidth;

Varyings WarpPassVertex (Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);

	// World-space normal
	float3 normalWS = mul((float3x3)UNITY_MATRIX_M, input.normalOS);

	// World-space position
	float3 positionWS = TransformObjectToWorld(input.positionOS);

	// Screen-space position
	float3 positionVS = mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).xyz;

	// Distance from camera
	float distanceFromCamera = length(positionVS);
	#if defined(_COMPENSATE_DISTANCE)
		output.dist = distanceFromCamera;
	#endif

	// Clip-space position
	output.positionCS_SS = mul(UNITY_MATRIX_P, float4(positionVS, 1.0));

	// Inflate the mesh along normals with fixed width
	float3 normalInflatedWS = mul((float3x3)UNITY_MATRIX_M, input.normalInflatedOS);
	float3 normalInflatedCS_SS = mul((float3x3)UNITY_MATRIX_VP, input.normalInflatedOS);
	Inflate(output.positionCS_SS, normalInflatedCS_SS, _ScreenParams.xy, _WarpWidth * GetWidthMultiplier(), distanceFromCamera);

	#if defined(_USE_SMOOTH_UV_GRADIENT)
		// World-space tangent and binormal from object-space
		float3 tangentWS = mul((float3x3)UNITY_MATRIX_M, input.tangentOS.xyz);
		float3 binormalWS = normalize(cross(normalWS, tangentWS) * input.tangentOS.w);
		
		// View-space tangent and binormal from world-space
		float3 tangentVS = mul((float3x3)UNITY_MATRIX_V, tangentWS).xyz;
		float3 binormalVS = mul((float3x3)UNITY_MATRIX_V, binormalWS).xyz;
		
		// Get world-space UV gradients ∇wu and ∇wv.
		//
		// We have to scale it by the camera distance. We normalize the camera distance using the projection matrix's first entry (P[0,0]).
		// P[0,0] is the aspect ratio (height / width) multiplied by the field of view { 1 / tan(fov / 2) } so that we get consistent results
		// even when those settings change.
		//
		// The float value at the end is there to have consistent results with GetUVGradientFromDerivatives().
		float scale = (distanceFromCamera / UNITY_MATRIX_P[0][0]) * 0.0002;
		float2 gradUVWS = GetWorldSpaceUVGradient();
		float2 gradUWS = gradUVWS.x * scale;
		float2 gradVWS = gradUVWS.y * scale;

		// Get Approximate Smooth UV Gradient from screen-space tangent and binormal
		output.uvGrad = GetApproximateSmoothUVGradient(tangentVS, binormalVS, positionVS / distanceFromCamera, gradUWS, gradVWS);
	#endif
	
	// Screen-space UV
	#if defined(_COMPENSATE_RADIAL_ANGLE)
		output.screenUV = output.positionCS_SS;
	#endif

	// Base UV
	output.baseUV.xy = TransformBaseUV(input.baseUV.xy);

	return output;
}

TEXTURE2D(_WarpTexture);
SAMPLER(sampler_WarpTexture);

float _WarpTextureScale;
float4 _LineBoilTime;

float4 WarpPassFragment (Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);

	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	float4 warp = float4(0.5, 0.5, 0.0, 1.0);

	float2 uv = input.baseUV;

	// '4.2. Animated line boil'
	#if defined(_USE_ANIMATED_LINE_BOIL)
		uv.x += (_LineBoilTime[GetAnimatedLineBoilFramerate()] * 0.3) % 1.0;
	#endif

	float2 gradU, gradV;
	#if defined(_USE_SMOOTH_UV_GRADIENT)
		gradU = input.uvGrad.xy;
		gradV = input.uvGrad.zw;
	#else
		GetUVGradientFromDerivatives(uv, gradU, gradV);
	#endif

	float intensity = 1.0;

	// '3.3. Compensating for radial angle'
	#if defined(_COMPENSATE_RADIAL_ANGLE)
		float2 screenUV = input.screenUV.xy / input.screenUV.w;
    	float a = GetRadialAngleCompensationCoefficient(screenUV, UNITY_MATRIX_P);
	#else
		float a = 1.0;
	#endif

	// MetaTexture sampling
	#if defined(_COMPENSATE_SKEW)
		warp = SampleMetaTextureSkewed(_WarpTexture, sampler_WarpTexture, uv, gradU, gradV, a, _WarpTextureScale, GetSkew());
	#else
		warp = SampleMetaTexture(_WarpTexture, sampler_WarpTexture, uv, gradU, gradV, a, _WarpTextureScale);
	#endif

	// Per object width multiplier (aka warp amount)
	warp = (warp - 0.5) * GetWidthMultiplier() + 0.5;

	// '4.3. Compensating for camera roll'
	float2 heading = normalize(gradU);
	//warp.rg -= 0.5;
	//warp.rg = float2(warp.r * heading.x + warp.g * heading.y, warp.r * heading.y - warp.g * heading.x) + 0.5;

    // '4.4. Compensating for distance'
	#if defined(_COMPENSATE_DISTANCE)
    	CompensateDistance(intensity, input.dist * _WarpGlobalDistanceFade * GetDistanceFadeMultiplier(), warp);
	#endif

	return warp;
}

#endif