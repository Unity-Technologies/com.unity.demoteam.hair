Shader "Hair/Default/HairMaterialDefaultLitBuiltin"
{
	CGINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ STAGING_COMPRESSION
	// 0 == staging data full precision
	// 1 == staging data compressed

	#pragma multi_compile __ HAIR_VERTEX_ID_LINES HAIR_VERTEX_ID_STRIPS
	// *_LINES == render as line segments
	// *_STRIPS == render as view facing strips

	#pragma multi_compile HAIR_VERTEX_SRC_SOLVER HAIR_VERTEX_SRC_STAGING
	// *_SOLVER == source vertex from solver data
	// *_STAGING == source vertex data from staging data

	#include "HairMaterialCommonBuiltin.hlsl"

	ENDCG

	SubShader
	{
		Tags { "RenderType" = "Opaque" "DisableBatching" = "True" "Queue" = "Geometry" }
		ZTest LEqual
		ZWrite On

		CGPROGRAM

		#pragma surface BuiltinSurf Lambert vertex:BuiltinVert addshadow fullforwardshadows nolightmap nodynlightmap nometa
		#pragma enable_cbuffer// required on some targets to include custom constant buffers in surface shaders (e.g. metal)

		ENDCG
	}

	Fallback "Hair/Default/HairMaterialDefaultUnlit"
}
