﻿Shader "Custom RP/Lit" {
	
	Properties {
		_BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
		
		[NoScaleOffset] _BaseShadowedMap ("Shadowed Texture", 2D) = "white" {}
		_BaseShadowedColor("Shadowed Color", Color) = (0.0, 0.0, 0.0, 1.0)
		_BreakupMap ("Breakup map", 2D) = "black" {}
		_BaseColorOverlay("Color overlay", Color) = (0.5, 0.5, 0.5, 1.0)
		_BaseColorSaturation("Saturation", Float) = 1.0

		[Toggle(_MASK_MAP)] _MaskMapToggle ("Mask Map", Float) = 0
		[NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Occlusion ("Occlusion", Range(0, 1)) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		_Fresnel ("Fresnel", Range(0, 1)) = 1

		[Toggle(_NORMAL_MAP)] _NormalMapToggle ("Normal Map", Float) = 0
		[NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
		_NormalScale("Normal Scale", Range(0, 1)) = 1

		[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)

		[Toggle(_DETAIL_MAP)] _DetailMapToggle ("Detail Maps", Float) = 0
		_DetailMap("Details", 2D) = "linearGrey" {}
		[NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {}
		_DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
		_DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
		_DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1
		
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0

		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		[Enum(Off, 0, Back, 1, Front, 2)] _Cull ("Cull", Float) = 2

		[HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
		
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
			Tags {
				"LightMode" = "CustomLit"
			}

			Cull [_Cull]
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma shader_feature _PREMULTIPLY_ALPHA
			#pragma shader_feature _MASK_MAP
			#pragma shader_feature _NORMAL_MAP
			#pragma shader_feature _DETAIL_MAP
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
			#pragma multi_compile _ _LIGHTS_PER_OBJECT
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_PASS _AGE_OF_SAIL_RP_COLOR_PASS
			#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_COLORED_PASS
			#pragma multi_compile_instancing
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			#include "LitPass.hlsl"
			ENDHLSL
		}

		Pass {
			Tags {
				"LightMode" = "ShadowCaster"
			}

			ColorMask 0

			Cull [_Cull]
			
			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "LitInput.hlsl"
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
			#include "LitInput.hlsl"
			#include "MetaPass.hlsl"
			ENDHLSL
		}

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
			#define _EDGE_BREAKUP_WARP_PASS
			#include "../ShaderLibrary/MetaTexture.hlsl"
			#include "LitInput.hlsl"
			#include "EdgeBreakupWarpPass.hlsl"
			ENDHLSL
		}
	}

	CustomEditor "CustomShaderGUI"
}