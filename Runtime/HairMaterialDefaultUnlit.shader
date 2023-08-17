Shader "Hair/Default/HairMaterialDefaultUnlit"
{
	HLSLINCLUDE

	#pragma target 5.0

	#include "HairMaterialCommonUnlit.hlsl"

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" "DisableBatching" = "True" "Queue" = "Transparent-1" }
		ZTest LEqual
		ZWrite On

		Pass
		{
			HLSLPROGRAM

			#pragma vertex UnlitVert
			#pragma fragment UnlitFrag

			ENDHLSL
		}
	}

	Fallback Off
}
