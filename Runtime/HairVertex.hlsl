#ifndef __HAIRVERTEX_HLSL__
#define __HAIRVERTEX_HLSL__

/* required pragmas
#pragma multi_compile __ STAGING_COMPRESSION
// 0 == staging data full precision
// 1 == staging data compressed

#pragma multi_compile HAIR_VERTEX_ID_LINES HAIR_VERTEX_ID_STRIPS HAIR_VERTEX_ID_TUBES
// *_LINES == render as line segments
// *_STRIPS == render as view facing strips
// *_TUBES == render as tubes

#pragma multi_compile HAIR_VERTEX_SRC_SOLVER HAIR_VERTEX_SRC_STAGING
// *_SOLVER == source vertex from solver data
// *_STAGING == source vertex data from staging data
*/

#ifndef HAIR_VERTEX_IMPL_WS_POS_VIEW_DIR
#define HAIR_VERTEX_IMPL_WS_POS_VIEW_DIR(x) GetWorldSpaceNormalizeViewDir(x)
#endif
#ifndef HAIR_VERTEX_IMPL_WS_POS_TO_RWS
#define HAIR_VERTEX_IMPL_WS_POS_TO_RWS(x) GetCameraRelativePositionWS(x)
#endif
#ifndef HAIR_VERTEX_IMPL_WS_VEC_TO_OS
#define HAIR_VERTEX_IMPL_WS_VEC_TO_OS(v) TransformWorldToObjectNormal(v)
#endif

#ifndef normalize_safe
#define normalize_safe(x) (x * rsqrt(max(1e-7, dot(x, x))))
#endif

#ifndef HAIR_VERTEX_ID_LINES
#define HAIR_VERTEX_ID_LINES 0
#endif
#ifndef HAIR_VERTEX_ID_STRIPS
#define HAIR_VERTEX_ID_STRIPS 0
#endif
#ifndef HAIR_VERTEX_ID_TUBES
#define HAIR_VERTEX_ID_TUBES 0
#endif
#ifndef HAIR_VERTEX_SRC_SOLVER
#define HAIR_VERTEX_SRC_SOLVER 0
#endif
#ifndef HAIR_VERTEX_SRC_STAGING
#define HAIR_VERTEX_SRC_STAGING 0
#endif

#include "HairSimData.hlsl"
#include "HairSimDebugDrawUtility.hlsl"

#ifndef UNITY_PREV_MATRIX_I_M// not defined by e.g. URP graphs prior to 2021.2.x
#define UNITY_PREV_MATRIX_I_M UNITY_MATRIX_I_M
#endif

/* UNITY_VERSION < 202120
#ifndef UNITY_SHADER_VARIABLES_INCLUDED
float4x4 unity_MatrixPreviousMI;
#endif

#ifndef UNITY_PREV_MATRIX_I_M
#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI
#endif
*/

#if HAIR_VERTEX_SRC_STAGING
#define STRAND_PARTICLE_COUNT	_StagingVertexCount
#define STRAND_PARTICLE_OFFSET	_StagingVertexOffset
#else
#define STRAND_PARTICLE_COUNT	_StrandParticleCount
#define STRAND_PARTICLE_OFFSET	_StrandParticleOffset
#endif

#define DECLARE_STRAND(x)													\
	const uint strandIndex = x;												\
	const uint strandParticleBegin = strandIndex * STRAND_PARTICLE_OFFSET;	\
	const uint strandParticleStride = _StrandParticleStride;				\
	const uint strandParticleEnd = strandParticleBegin + strandParticleStride * STRAND_PARTICLE_COUNT;

float3 LoadPosition(uint i)
{
#if HAIR_VERTEX_SRC_STAGING
	return LoadStagingPosition(i);
#else
	return _ParticlePosition[i].xyz;
#endif
}

float3 LoadPositionPrev(uint i)
{
#if HAIR_VERTEX_SRC_STAGING
	return LoadStagingPositionPrev(i);
#else
	return _ParticlePositionPrev[i].xyz;
#endif
}

struct HairVertexWS
{
	float3 positionWS;
	float3 positionWSPrev;
	float3 normalWS;
	float3 tangentWS;
	float3 bitangentWS;
	float2 rootUV;
	float2 strandUV;
	uint strandIndex;
	float3 strandNormalTS;
	float3 strandDebugColor;
	float3 particleUserData;
};

struct HairVertex
{
	float3 positionOS;
	float3 positionOSPrev;
	float3 normalOS;
	float3 tangentOS;
	float3 bitangentOS;
	float2 rootUV;
	float2 strandUV;
	uint strandIndex;
	float3 strandNormalTS;
	float3 strandDebugColor;
	float3 particleUserData;
};

float3 GetStrandNormalTangentSpace(in float2 strandVertexUV)
{
	float3 strandNormalTS;
	{
		strandNormalTS.x = 2.0 * saturate(strandVertexUV.x) - 1.0;
		strandNormalTS.y = 0.0;
		strandNormalTS.z = sqrt(max(1e-5, 1.0 - strandNormalTS.x * strandNormalTS.x));
	}
	return strandNormalTS;
}

float3 GetStrandDebugColor(in int strandIndex)
{
	uint strandIndexLo = _LODGuideIndex[(_LODIndexLo * _StrandCount) + strandIndex];
	uint strandIndexHi = _LODGuideIndex[(_LODIndexHi * _StrandCount) + strandIndex];
	float3 strandColorLo = ColorCycle(strandIndexLo, _LODGuideCount[_LODCount - 1]);
	float3 strandColorHi = ColorCycle(strandIndexHi, _LODGuideCount[_LODCount - 1]);
	return lerp(strandColorLo, strandColorHi, _LODBlendFraction);
}

void UnpackTubeOffsets(float texcoord, out float offsetU, out float offsetV)
{
	// Unpack the UV back into a 16-bit uint.
	const uint unpacked = (uint)(texcoord * ((1u << 16u) - 1u) + 0.5);

	// Then just check the first bit in the 8-bit partitions. 
	offsetU = (unpacked & (1 << 8u)) != 0;
	offsetV = (unpacked & (1 << 0u)) != 0;
}

HairVertexWS GetHairVertexWS_Live(in uint vertexID, in float2 vertexUV)
{
#if HAIR_VERTEX_ID_STRIPS
	uint linearParticleIndex = vertexID >> 1;
#elif HAIR_VERTEX_ID_TUBES
	uint linearParticleIndex = vertexID >> 2;
#else
	uint linearParticleIndex = vertexID;
#endif

	DECLARE_STRAND(linearParticleIndex / STRAND_PARTICLE_COUNT);
	const uint i = strandParticleBegin + (linearParticleIndex % STRAND_PARTICLE_COUNT) * strandParticleStride;
	const uint i_next = i + strandParticleStride;
	const uint i_prev = i - strandParticleStride;
	const uint i_head = strandParticleBegin;
	const uint i_tail = strandParticleEnd - strandParticleStride;

	float3 p = LoadPosition(i);
	float3 r0 = (i == i_head)
		? LoadPosition(i_next) - p
		: p - LoadPosition(i_prev);
	float3 r1 = (i == i_tail)
		? r0
		: LoadPosition(i_next) - p;

	float3 curvePositionRWS = HAIR_VERTEX_IMPL_WS_POS_TO_RWS(p);
	float3 curvePositionRWSPrev = HAIR_VERTEX_IMPL_WS_POS_TO_RWS(LoadPositionPrev(i));

	float3 vertexBitangentWS = (r0 + r1);// approx tangent to curve
	float3 vertexTangentWS = normalize_safe(cross(vertexBitangentWS, HAIR_VERTEX_IMPL_WS_POS_VIEW_DIR(curvePositionRWS)));
	float3 vertexNormalWS = cross(vertexTangentWS, vertexBitangentWS);

#if HAIR_VERTEX_ID_STRIPS
	float3 vertexOffsetWS = vertexTangentWS * ((0.01 * _ParticleDiameter[i]) * (vertexUV.x - 0.5));
#elif HAIR_VERTEX_ID_TUBES
	float3 vertexOffsetWS = float3(0.0, 0.0, 0.0);
	{
		// Normal requires normalization for tube offset to work.
		vertexNormalWS = normalize_safe(vertexNormalWS);
		
		float offsetU, offsetV;
		UnpackTubeOffsets(vertexUV.x, offsetU, offsetV);
		
		vertexOffsetWS += vertexTangentWS * ((0.01 * _ParticleDiameter[i]) * (offsetU - 0.5));
		vertexOffsetWS += vertexNormalWS  * ((0.01 * _ParticleDiameter[i]) * (offsetV - 0.5));
	}
#else
	float3 vertexOffsetWS = float3(0.0, 0.0, 0.0);
#endif
	float3 vertexPositionWS = curvePositionRWS + vertexOffsetWS;
	float3 vertexPositionWSPrev = curvePositionRWSPrev + vertexOffsetWS;

	HairVertexWS x;
	{
		x.positionWS = vertexPositionWS;
		x.positionWSPrev = vertexPositionWSPrev;
		x.normalWS = vertexNormalWS;
		x.tangentWS = vertexTangentWS;
		x.bitangentWS = vertexBitangentWS;
		x.rootUV = _RootUV[strandIndex];
		x.strandUV = vertexUV; //  * float2(1.0, _RootScale[strandIndex]);
		x.strandIndex = strandIndex;
		x.strandNormalTS = GetStrandNormalTangentSpace(vertexUV);
		x.strandDebugColor = GetStrandDebugColor(strandIndex);
		x.particleUserData = _ParticleDiameter[i].xxx;
	}
	return x;
}

HairVertex GetHairVertex_Live(in uint vertexID, in float2 vertexUV)
{
	HairVertexWS x = GetHairVertexWS_Live(vertexID, vertexUV);
	HairVertex v;
	{
		v.positionOS = mul(UNITY_MATRIX_I_M, float4(x.positionWS, 1.0)).xyz;
		v.positionOSPrev = mul(UNITY_PREV_MATRIX_I_M, float4(x.positionWSPrev, 1.0)).xyz;
		v.normalOS = HAIR_VERTEX_IMPL_WS_VEC_TO_OS(x.normalWS);
		v.tangentOS = HAIR_VERTEX_IMPL_WS_VEC_TO_OS(x.tangentWS);
		v.bitangentOS = HAIR_VERTEX_IMPL_WS_VEC_TO_OS(x.bitangentWS);
		v.rootUV = x.rootUV;
		v.strandUV = x.strandUV;
		v.strandIndex = x.strandIndex;
		v.strandNormalTS = x.strandNormalTS;
		v.strandDebugColor = x.strandDebugColor;
		v.particleUserData = x.particleUserData;
	}
	return v;
}

HairVertex GetHairVertex_Static(in float3 positionOS, in float3 normalOS, in float3 tangentOS)
{
	HairVertex v;
	{
		v.positionOS = positionOS;
		v.positionOSPrev = positionOS;
		v.normalOS = normalOS;
		v.tangentOS = tangentOS;
		v.bitangentOS = normalize(cross(normalOS, tangentOS));
		v.rootUV = float2(0.0, 0.0);
		v.strandUV = float2(0.0, 0.0);
		v.strandIndex = 0;
		v.strandNormalTS = float3(0.0, 0.0, 1.0);
		v.strandDebugColor = float3(0.5, 0.5, 0.5);
		v.particleUserData = 0.0;
	}
	return v;
}

HairVertex GetHairVertex(
	in uint in_vertexID,
	in float2 in_vertexUV,
	in float3 in_staticPositionOS,
	in float3 in_staticNormalOS,
	in float3 in_staticTangentOS)
{
#if (HAIR_VERTEX_ID_LINES || HAIR_VERTEX_ID_STRIPS || HAIR_VERTEX_ID_TUBES)
	return GetHairVertex_Live(in_vertexID, in_vertexUV);
#else
	return GetHairVertex_Static(in_staticPositionOS, in_staticNormalOS, in_staticTangentOS);
#endif
}

void HairVertex_float(
	in uint in_vertexID,
	in float2 in_vertexUV,
	in float3 in_staticPositionOS,
	in float3 in_staticNormalOS,
	in float3 in_staticTangentOS,
	out float3 out_positionOS,
	out float3 out_motionOS,
	out float3 out_normalOS,
	out float3 out_tangentOS,
	out float3 out_bitangentOS,
	out float2 out_rootUV,
	out float2 out_strandUV,
	out uint out_strandIndex,
	out float3 out_strandNormalTS,
	out float3 out_strandDebugColor,
	out float3 out_strandParticleUserData)
{
	HairVertex v = GetHairVertex(in_vertexID, in_vertexUV, in_staticPositionOS, in_staticNormalOS, in_staticTangentOS);
	{
		out_positionOS = v.positionOS;
		out_motionOS = v.positionOS - v.positionOSPrev;
		out_normalOS = v.normalOS;
		out_tangentOS = v.tangentOS;
		out_bitangentOS = v.bitangentOS;
		out_rootUV = v.rootUV;
		out_strandUV = v.strandUV;
		out_strandIndex = v.strandIndex;
		out_strandNormalTS = v.strandNormalTS;
		out_strandDebugColor = v.strandDebugColor;
		out_strandParticleUserData = v.particleUserData;
	}
}

#endif//__HAIRVERTEX_HLSL__
