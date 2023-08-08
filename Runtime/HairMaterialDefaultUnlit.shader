Shader "Hair/Default/HairMaterialDefaultUnlit"
{
	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ STAGING_COMPRESSION
	// 0 == staging data full precision
	// 1 == staging data compressed

	#pragma multi_compile __ HAIR_VERTEX_ID_LINES HAIR_VERTEX_ID_STRIPS HAIR_VERTEX_ID_TUBES
	// *_LINES == render as line segments
	// *_STRIPS == render as view facing strips

	#pragma multi_compile HAIR_VERTEX_SRC_SOLVER HAIR_VERTEX_SRC_STAGING
	// *_SOLVER == source vertex from solver data
	// *_STAGING == source vertex data from staging data

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
