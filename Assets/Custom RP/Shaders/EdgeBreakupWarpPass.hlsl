#ifndef EDGE_BREAKUP_PASS_INCLUDED
#define EDGE_BREAKUP_PASS_INCLUDED

struct Attributes {
	float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings EdgeBreakupPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(positionWS);
	output.baseUV.xy = TransformBaseUV(input.baseUV.xy);
	return output;
}

float4 EdgeBreakupPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);

	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

    base.rgb = float3(0.5, 0.5, 0.0);

	return float4(base.rgb, GetFinalAlpha(base.a));
}

#endif