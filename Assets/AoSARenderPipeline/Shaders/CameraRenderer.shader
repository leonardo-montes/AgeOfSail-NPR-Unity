Shader "Hidden/AoSA RP/Camera Renderer Stack"
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
			Name "Copy Array"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyArrayPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Downsample Array"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment DownsampleArrayPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Horizontal Blur Pass Array"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment HorizontalBlurPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Vertical Blur Pass Array"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment VerticalBlurPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Compositing Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CompositingPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Final Compositing Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment FinalCompositingPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Debug Multiple"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment DebugMultiplePassFragment
			ENDHLSL
		}
	}
}