Shader "Custom RP/Edge Breakup Pass Only" {
	Properties {
		_BaseMap("Texture", 2D) = "white" {}
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		
		[Enum(Off, 0, Back, 1, Front, 2)] _Cull ("Cull", Float) = 2
		
		[Toggle(_USE_SMOOTH_UV_GRADIENT)] _UseSmoothUVGradient ("Edge breakup: use smooth UV gradient", Float) = 0
		[Toggle(_COMPENSATE_RADIAL_ANGLE)] _CompensateRadialAngle ("Edge breakup: compensate for radial angle", Float) = 0
		[Toggle(_COMPENSATE_SKEW)] _CompensateSkew ("Edge breakup: compensate for skew", Float) = 0
		[Toggle(_COMPENSATE_DISTANCE)] _CompensateDistance ("Edge breakup: compensate for distance", Float) = 0
		[Toggle(_USE_ANIMATED_LINE_BOIL)] _UseAnimatedLineBoil ("Edge breakup: use animated line boil", Float) = 0
		[Enum(Realtime, 0, 24fps, 1, 12fps, 2, 8fps, 3)] _AnimatedLineBoilFramerate ("Edge breakup: animated line boil framerate", Float) = 3
		_WorldSpaceUVGradient("World Space UV Gradient", Vector) = (1, 1, 0, 0)
	}
	
	SubShader {
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "LitInput.hlsl"
		ENDHLSL

		Pass {
			Tags {
				"LightMode" = "EdgeBreakup"
			}

			Cull [_Cull]

			HLSLPROGRAM
			#pragma target 4.5
			#pragma shader_feature _USE_SMOOTH_UV_GRADIENT
			#pragma shader_feature _COMPENSATE_RADIAL_ANGLE
			#pragma shader_feature _COMPENSATE_SKEW
			#pragma shader_feature _COMPENSATE_DISTANCE
			#pragma shader_feature _USE_ANIMATED_LINE_BOIL
			#pragma multi_compile_instancing
			#pragma vertex EdgeBreakupPassVertex
			#pragma fragment EdgeBreakupPassFragment
			#include "../ShaderLibrary/MetaTexture.hlsl"
			#include "EdgeBreakupWarpPass.hlsl"
			ENDHLSL
		}
	}

	//CustomEditor "CustomShaderGUI"
}