Shader "AoS RP/Warp Pass Only"
{
	Properties
	{
		_BaseMap("Texture", 2D) = "white" {}
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		
		[Enum(Off, 0, Back, 1, Front, 2)] _Cull ("Cull", Float) = 2
		
		[Header(Edge breakup)]
		[Toggle(_USE_SMOOTH_UV_GRADIENT)] _UseSmoothUVGradient ("Use smooth UV gradient", Float) = 0
		[Toggle(_COMPENSATE_RADIAL_ANGLE)] _CompensateRadialAngle ("Compensate for radial angle", Float) = 0
		[Toggle(_COMPENSATE_SKEW)] _CompensateSkew ("Compensate for skew", Float) = 0
		[Toggle(_COMPENSATE_DISTANCE)] _CompensateDistance ("Compensate for distance", Float) = 0
		[Toggle(_USE_ANIMATED_LINE_BOIL)] _UseAnimatedLineBoil ("Use animated line boil", Float) = 0
		[Enum(Realtime, 0, 24fps, 1, 12fps, 2, 8fps, 3)] _AnimatedLineBoilFramerate ("Framerate", Float) = 3
		[KeywordEnum(None, Contour, All)] _Reorient ("Reorient", Float) = 0
		_WorldSpaceUVGradient("World Space UV Gradient", Vector) = (1, 1, 0, 0)
		_WarpDistanceFadeMultiplier("Distance fade multiplier", Float) = 1.0
		_WarpWidthMultiplier("Width (aka warp amount) multiplier", Float) = 1.0
		_WarpSkew("Skew", Float) = 4.0
	}
	
	SubShader
	{
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		ENDHLSL

		Pass
		{
			Tags { "LightMode" = "WarpPass" }

			Cull [_Cull]

			HLSLPROGRAM
			#pragma target 4.5
			#pragma shader_feature _USE_SMOOTH_UV_GRADIENT
			#pragma shader_feature _COMPENSATE_RADIAL_ANGLE
			#pragma shader_feature _COMPENSATE_SKEW
			#pragma shader_feature _COMPENSATE_DISTANCE
			#pragma shader_feature _USE_ANIMATED_LINE_BOIL
			#pragma multi_compile _ _REORIENT_CONTOUR _REORIENT_ALL
			#pragma multi_compile_instancing
			#pragma vertex WarpPassVertex
			#pragma fragment WarpPassFragment
			#define _WARP_PASS
			#include "Assets/CommonRP/ShaderLibrary/MetaTexture.hlsl"
			#include "UnlitInput.hlsl"
			#include "WarpPass.hlsl"
			ENDHLSL
		}
	}

	CustomEditor "CustomShaderGUI"
}