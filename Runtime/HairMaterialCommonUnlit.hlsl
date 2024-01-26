#ifndef __HAIRMATERIALCOMMONUNLIT_HLSL__
#define __HAIRMATERIALCOMMONUNLIT_HLSL__

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
	float4 packedID : TEXCOORD0;
	float3 staticPositionOS : POSITION;
	float3 staticTangentOS : TANGENT;
	float3 staticNormalOS : NORMAL;
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
	HairVertexData v = GetHairVertex(IN.packedID, IN.staticPositionOS, IN.staticNormalOS, IN.staticTangentOS);
	{
		UnlitVaryings OUT;
		OUT.positionCS = TransformObjectToHClip(v.surfacePosition);
		OUT.strandColor = v.strandIndexColor;
		OUT.strandUV = v.surfaceUV;
		return OUT;
	}
}

float4 UnlitFrag(UnlitVaryings IN) : SV_Target
{
	return float4(IN.strandColor, 1.0);
}

#endif//__HAIRMATERIALCOMMONUNLIT_HLSL__
