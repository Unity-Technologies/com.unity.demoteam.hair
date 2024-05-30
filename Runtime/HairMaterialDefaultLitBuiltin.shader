Shader "Hair/Default/HairMaterialDefaultLitBuiltin"
{
	CGINCLUDE

	#pragma target 5.0

	#pragma multi_compile_local_vertex HAIR_VERTEX_STATIC HAIR_VERTEX_LIVE

	#include "HairMaterialCommonBuiltin.hlsl"

	ENDCG

	SubShader
	{
		Tags { "RenderType" = "Opaque" "DisableBatching" = "True" "Queue" = "Geometry" }
		ZTest LEqual
		ZWrite On

		CGPROGRAM

		#pragma surface BuiltinSurf Lambert vertex:BuiltinVert addshadow fullforwardshadows noinstancing nolightmap nodynlightmap nometa
		#pragma enable_cbuffer// required on some targets to include custom constant buffers in surface shaders (e.g. metal)

		ENDCG
	}

	Fallback "Hair/Default/HairMaterialDefaultUnlit"
}
