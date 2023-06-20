#ifndef __HAIRMATERIALCOMMONUNLIT_HLSL__
#define __HAIRMATERIALCOMMONUNLIT_HLSL__

/* required pragmas
#pragma multi_compile __ STAGING_COMPRESSION
// 0 == staging data full precision
// 1 == staging data compressed

#pragma multi_compile __ HAIR_VERTEX_ID_LINES HAIR_VERTEX_ID_STRIPS
// *_LINES == render as line segments
// *_STRIPS == render as view facing strips

#pragma multi_compile HAIR_VERTEX_SRC_SOLVER HAIR_VERTEX_SRC_STAGING
// *_SOLVER == source vertex from solver data
// *_STAGING == source vertex data from staging data
*/

//----------
// includes

#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderConfig.cs.hlsl"
#ifdef SHADEROPTIONS_CAMERA_RELATIVE_RENDERING
#undef SHADEROPTIONS_CAMERA_RELATIVE_RENDERING
#define SHADEROPTIONS_CAMERA_RELATIVE_RENDERING (0)
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariables.hlsl"
#ifndef UNITY_MATRIX_M
#define UNITY_MATRIX_M unity_ObjectToWorld
#endif
#ifndef UNITY_MATRIX_I_M
#define UNITY_MATRIX_I_M unity_WorldToObject
#endif
#ifndef UNITY_PREV_MATRIX_M
#define UNITY_PREV_MATRIX_M UNITY_MATRIX_M
#endif
#ifndef UNITY_PREV_MATRIX_I_M
#define UNITY_PREV_MATRIX_I_M UNITY_MATRIX_I_M
#endif
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"

#include "HairVertex.hlsl"

//------------
// structures

struct UnlitAttribs
{
	float vertexID : TEXCOORD0;
	float2 vertexUV : TEXCOORD1;
	float3 staticPositionOS : POSITION;
	float3 staticNormalOS : NORMAL;
	float3 staticTangentOS : TANGENT;
};

struct UnlitVaryings
{
	float4 positionCS : SV_POSITION;
	float3 strandColor : COLOR;
	float2 strandUV : TEXCOORD0;
};

//----------
// programs

UnlitVaryings UnlitVert(UnlitAttribs IN)
{
	HairVertex hair = GetHairVertex((uint)IN.vertexID, IN.vertexUV, IN.staticPositionOS, IN.staticNormalOS, IN.staticTangentOS);
	{
		UnlitVaryings OUT;
		OUT.positionCS = TransformObjectToHClip(hair.positionOS);
		OUT.strandColor = hair.strandDebugColor;
		OUT.strandUV = hair.strandUV;
		return OUT;
	}
}

float4 UnlitFrag(UnlitVaryings IN) : SV_Target
{
	return float4(IN.strandColor, 1.0);
}

#endif//__HAIRMATERIALCOMMONUNLIT_HLSL__
