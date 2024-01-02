Shader "Hidden/AoS RP/Camera Renderer Stack"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "CameraRendererPasses.hlsl"
		ENDHLSL
		
		Pass
		{
			Name "Copy"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Downsample"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment DownsamplePassFragment
			ENDHLSL
		}

		Pass
		{
			Name "Horizontal Blur Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment HorizontalBlurPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Vertical Blur Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment VerticalBlurPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Final Shadow Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment FinalShadowPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Final Color Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma shader_feature _WARP_BLOOM
				#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_COLORED_PASS
				#pragma vertex DefaultPassVertex
				#pragma fragment FinalColorPassFragment
			ENDHLSL
		}
	}
}