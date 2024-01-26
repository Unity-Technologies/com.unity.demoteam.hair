#ifndef __HAIRMATERIALCOMMONBUILTIN_HLSL__
#define __HAIRMATERIALCOMMONBUILTIN_HLSL__

//----------
// includes

#define UNITY_COMMON_INCLUDED

#ifndef real
#define real float
#endif
#ifndef real3
#define real3 float3
#endif
#ifndef real3x3
#define real3x3 float3x3
#endif
#ifndef SafeNormalize
#define SafeNormalize(v) (normalize(v))
#endif

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

#if !SHADER_TARGET_SURFACE_ANALYSIS
#include "HairVertex.hlsl"
#endif

//------------
// structures

struct appdata_hair
{
	float4 packedID : TEXCOORD0;
#if HAIR_VERTEX_LIVE//TODO reintroduce similar editor-only variant to suppress editor warnings on some platforms
	float4 vertex : COLOR;
#else
	float4 vertex : POSITION;
#endif
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
};

struct Input
{
	float3 strandTangentWS;
	float3 strandColor;
	float2 strandUV;
};

//----------
// programs

void BuiltinVert(inout appdata_hair a, out Input o)
{
#if !SHADER_TARGET_SURFACE_ANALYSIS
	HairVertexData v = GetHairVertex(a.packedID, a.vertex.xyz, a.normal.xyz, (a.tangent.xyz * a.tangent.w));
	{
		a.vertex = float4(v.surfacePosition, 1.0);
		a.normal = v.surfacePosition;
		a.tangent = float4(v.surfaceTangent, 1.0);

		UNITY_INITIALIZE_OUTPUT(Input, o);
		o.strandTangentWS = TransformObjectToWorldNormal(v.surfaceTangent);
		o.strandColor = v.strandIndexColor;
		o.strandUV = v.surfaceUV;
	}
#else
	o.strandTangentWS = TransformObjectToWorldNormal(a.tangent.xyz);
	o.strandColor = float3(0.5, 0.5, 0.5);
	o.strandUV = float2(0.5, 0.0);
#endif
}

void BuiltinSurf(Input IN, inout SurfaceOutput o)
{
#if !SHADER_TARGET_SURFACE_ANALYSIS
	float3 normalTS = GetSurfaceNormalTS(IN.strandUV);
#else
	float3 normalTS = float3(0.0, 0.0, 1.0);
#endif

	o.Albedo = IN.strandColor;
	o.Normal = TransformTangentToWorld(normalTS,
		float3x3(
			normalize(IN.strandTangentWS),
			normalize(cross(IN.strandTangentWS, o.Normal)),
			o.Normal
		)
	);
}

#endif//__HAIRMATERIALCOMMONBUILTIN_HLSL__
