﻿Shader "AoS RP/Unlit"
{
	Properties
	{
		_BaseMap("Texture", 2D) = "white" {}
		[HDR] _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0

		_BaseColorOverlay("Color overlay", Color) = (0.5, 0.5, 0.5, 0.0)
		_BaseColorSaturation("Saturation", Float) = 1.0

		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		[Enum(Off, 0, Back, 1, Front, 2)] _Cull ("Cull", Float) = 2

		[Header(Edge breakup)]
		[Toggle] _Warp ("Edge Breakup Warp Pass", Float) = 1
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
			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha

			Cull [_Cull]
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_PASS _AGE_OF_SAIL_RP_COLOR_PASS
			#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_COLORED_PASS
			#pragma multi_compile_instancing
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			#include "UnlitInput.hlsl"
			#include "UnlitPass.hlsl"
			ENDHLSL
		}

		Pass
		{
			Tags { "LightMode" = "ShadowCaster" }

			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma multi_compile_instancing
			#pragma vertex DepthPassVertex
			#pragma fragment DepthPassFragment
			#include "UnlitInput.hlsl"
			#include "DepthPass.hlsl"
			ENDHLSL
		}

		Pass
		{
			Tags { "LightMode" = "ShadowPass" }

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma shader_feature _PREMULTIPLY_ALPHA
			#pragma shader_feature _USE_SHADOWS
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment
			#define _AGE_OF_SAIL_RP_SHADOW_PASS
			#define _UNLIT
			#include "UnlitInput.hlsl"
			#include "ShadowPass.hlsl"
			ENDHLSL
		}


		Pass
		{
			Tags { "LightMode" = "ColorPass" }

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment
			#define _AGE_OF_SAIL_RP_COLOR_PASS
			#define _UNLIT
			#include "UnlitInput.hlsl"
			#include "ColorPass.hlsl"
			ENDHLSL
		}

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