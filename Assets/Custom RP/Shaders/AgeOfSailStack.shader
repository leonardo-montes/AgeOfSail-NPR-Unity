Shader "Hidden/Custom RP/Age Of Sail Stack" {
	
	SubShader {
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "AgeOfSailStackPasses.hlsl"
		ENDHLSL
		
		Pass {
			Name "Copy"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_COLORED_PASS
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyPassFragment
			ENDHLSL
		}
		
		Pass {
			Name "Final Shadow Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_COLORED_PASS
				#pragma vertex DefaultPassVertex
				#pragma fragment FinalShadowPassFragment
			ENDHLSL
		}
		
		Pass {
			Name "Horizontal Blur Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_COLORED_PASS
				#pragma vertex DefaultPassVertex
				#pragma fragment HorizontalBlurPassFragment
			ENDHLSL
		}
		
		Pass {
			Name "Vertical Blur Pass"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma multi_compile _ _AGE_OF_SAIL_RP_SHADOW_COLORED_PASS
				#pragma vertex DefaultPassVertex
				#pragma fragment VerticalBlurPassFragment
			ENDHLSL
		}
	}
}