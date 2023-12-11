Shader "Custom RP/Unlit" {
	
	Properties {
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
		[Toggle] _EdgeBreakup ("Edge Breakup Warp Pass", Float) = 1
		[Toggle(_USE_SMOOTH_UV_GRADIENT)] _UseSmoothUVGradient ("Use smooth UV gradient", Float) = 0
		[Toggle(_COMPENSATE_RADIAL_ANGLE)] _CompensateRadialAngle ("Compensate for radial angle", Float) = 0
		[Toggle(_COMPENSATE_SKEW)] _CompensateSkew ("Compensate for skew", Float) = 0
		[Toggle(_COMPENSATE_DISTANCE)] _CompensateDistance ("Compensate for distance", Float) = 0
		[Toggle(_USE_ANIMATED_LINE_BOIL)] _UseAnimatedLineBoil ("Use animated line boil", Float) = 0
		[Enum(Realtime, 0, 24fps, 1, 12fps, 2, 8fps, 3)] _AnimatedLineBoilFramerate ("Framerate", Float) = 3
		_WorldSpaceUVGradient("World Space UV Gradient", Vector) = (1, 1, 0, 0)
		_EdgeBreakupDistanceFadeMultiplier("Distance fade multiplier", Float) = 1.0
		_EdgeBreakupWidthMultiplier("Width (aka warp amount) multiplier", Float) = 1.0
		_EdgeBreakupSkew("Skew", Float) = 4.0
	}
	
	SubShader {
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		ENDHLSL

		Pass {
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

		Pass {
			Tags {
				"LightMode" = "ShadowCaster"
			}

			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "UnlitInput.hlsl"
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
		}

		Pass {
			Tags {
				"LightMode" = "Meta"
			}

			Cull Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			#include "UnlitInput.hlsl"
			#include "MetaPass.hlsl"
			ENDHLSL
		}

		Pass {
			Tags {
				"LightMode" = "EdgeBreakup"
			}

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
			#define _EDGE_BREAKUP_WARP_PASS
			#include "../ShaderLibrary/MetaTexture.hlsl"
			#include "UnlitInput.hlsl"
			#include "EdgeBreakupWarpPass.hlsl"
			ENDHLSL
		}
	}

	CustomEditor "CustomShaderGUI"
}