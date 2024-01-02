#ifndef AOS_COLOR_PASS_INCLUDED
#define AOS_COLOR_PASS_INCLUDED

#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes
{
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS_SS : SV_POSITION;
	float4 screenUV : VAR_SCREEN_UV;
	float3 positionWS : VAR_POSITION;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowPassVertex (Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);
	output.baseUV = TransformBaseUV(input.baseUV);
	output.screenUV = ComputeScreenPos(output.positionCS_SS);
	return output;
}

TEXTURE2D(_FinalShadowBuffer);

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

float4 ShadowPassFragment (Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	ClipLOD(config.fragment, unity_LODFade.x);
	
	// Get base texture and clip
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	// Apply lighting if needed
	#if defined(_UNLIT)
		float3 color = base.rgb;
	#else
		float2 screenUV = input.screenUV.xy / input.screenUV.w;
		float4 finalShadow = SAMPLE_TEXTURE2D_LOD(_FinalShadowBuffer, sampler_linear_clamp, screenUV, 0);
	
		float4 baseShadowed = GetBaseShadowed(config);
		
		// We use the Final Shadow Pass red channel to blend between these lit and shadowed texture maps on all objects.
		float3 color = lerp(baseShadowed.rgb, base.rgb, finalShadow.r);

		// Specular
		color = lerp(color, 1.0, finalShadow.b);
	#endif

	// Overlay
	float4 colorOverlay = GetBaseColorOverlay();
	color.rgb = lerp(color.rgb, BlendMode_Overlay(color.rgb, colorOverlay.rgb), colorOverlay.a);

	// Saturation
	color.rgb = Saturation(color.rgb, GetBaseColorSaturation());
	
	return float4(color, GetFinalAlpha(base.a));
}

#endif