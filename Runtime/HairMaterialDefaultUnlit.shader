Shader "Hair/Default/HairMaterialDefaultUnlit"
{
	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile_local_vertex HAIR_VERTEX_STATIC HAIR_VERTEX_LIVE

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
