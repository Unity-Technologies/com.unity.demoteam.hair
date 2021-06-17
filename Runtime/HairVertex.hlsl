#ifndef __HAIRVERTEX_HLSL__
#define __HAIRVERTEX_HLSL__

#pragma editor_sync_compilation

#include "HairSimData.hlsl"
#include "HairSimDebugDrawUtility.hlsl"

/*
#pragma multi_compile __ LAYOUT_INTERLEAVED
// 0 == particles grouped by strand, i.e. root, root+1, root, root+1
// 1 == particles grouped by index, i.e. root, root, root+1, root+1

#pragma multi_compile __ STAGING_COMPRESSION
// 0 == staging data full precision
// 1 == staging data compressed
*/

#ifndef HAIR_VERTEX_ID_LINES
#define HAIR_VERTEX_ID_LINES 0
#endif
#ifndef HAIR_VERTEX_ID_STRIPS
#define HAIR_VERTEX_ID_STRIPS 0
#endif
#ifndef HAIR_VERTEX_SRC_SOLVER
#define HAIR_VERTEX_SRC_SOLVER 0
#endif
#ifndef HAIR_VERTEX_SRC_STAGING
#define HAIR_VERTEX_SRC_STAGING 0
#endif

#if HAIR_VERTEX_SRC_STAGING
#define STRAND_PARTICLE_COUNT _StagingVertexCount
#else
#define STRAND_PARTICLE_COUNT _StrandParticleCount
#endif

#if LAYOUT_INTERLEAVED
  #define DECLARE_STRAND(x)							\
	const uint strandIndex = x;						\
	const uint strandParticleBegin = strandIndex;	\
	const uint strandParticleStride = _StrandCount;	\
	const uint strandParticleEnd = strandParticleBegin + strandParticleStride * STRAND_PARTICLE_COUNT;
#else
  #define DECLARE_STRAND(x)													\
	const uint strandIndex = x;												\
	const uint strandParticleBegin = strandIndex * STRAND_PARTICLE_COUNT;	\
	const uint strandParticleStride = 1;									\
	const uint strandParticleEnd = strandParticleBegin + strandParticleStride * STRAND_PARTICLE_COUNT;
#endif

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

struct HairVertex
{
	float3 positionOS;
	float3 normalOS;
	float3 tangentOS;
	float3 bitangentOS;
	float2 rootUV;
	float2 strandUV;
	uint strandIndex;
	float3 strandNormalTS;
	float3 debugColor;
};

HairVertex GetHairVertex_Live(in uint vertexID, in float2 vertexUV)
{
#if HAIR_VERTEX_ID_STRIPS
	uint linearParticleIndex = vertexID >> 1;
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

	float3 positionWS = GetCameraRelativePositionWS(p);
	//TODO motion vector
	//float3 motionWS = LoadPosition(i) -  LoadPositionPrev(i)
	
	float3 bitangentWS = normalize(r0 + r1);
	float3 tangentWS = normalize(cross(bitangentWS, GetWorldSpaceNormalizeViewDir(positionWS)));
	float3 normalWS = cross(tangentWS, bitangentWS);

	HairVertex v;
	{
#if HAIR_VERTEX_ID_STRIPS
		v.positionOS = TransformWorldToObject(positionWS + tangentWS * (_StrandDiameter * _StrandScale * (vertexUV.x - 0.5)));
#else
		v.positionOS = TransformWorldToObject(positionWS);
#endif
		v.normalOS = TransformWorldToObjectNormal(normalWS);
		v.tangentOS = TransformWorldToObjectNormal(tangentWS);
		v.bitangentOS = TransformWorldToObjectNormal(bitangentWS);
		v.rootUV = _RootUV[strandIndex];
		v.strandUV = vertexUV;
		v.strandIndex = strandIndex;
		v.strandNormalTS.x = saturate(2.0 * vertexUV.x - 1.0);
		v.strandNormalTS.y = 0.0;
		v.strandNormalTS.z = sqrt(max(1e-5, 1.0 - v.strandNormalTS.x * v.strandNormalTS.x));
		v.debugColor = ColorCycle(strandIndex, _StrandCount);
	}
	return v;
}

HairVertex GetHairVertex_Static(in float3 positionOS, in float3 normalOS, in float3 tangentOS)
{
	HairVertex v;
	{
		v.positionOS = positionOS;
		v.normalOS = normalOS;
		v.tangentOS = tangentOS;
		v.bitangentOS = normalize(cross(normalOS, tangentOS));
		v.rootUV = float2(0.0, 0.0);
		v.strandUV = float2(0.0, 0.0);
		v.strandIndex = 0;
		v.strandNormalTS = float3(0.0, 0.0, 1.0);
		v.debugColor = float3(0.5, 0.5, 0.5);
	}
	return v;
}

void HairVertex_float(
	in uint in_vertexID,
	in float2 in_vertexUV,
	in float3 in_staticPositionOS,
	in float3 in_staticNormalOS,
	in float3 in_staticTangentOS,
	out float3 out_positionOS,
	out float3 out_normalOS,
	out float3 out_tangentOS,
	out float3 out_bitangentOS,
	out float2 out_rootUV,
	out float2 out_strandUV,
	out uint out_strandIndex,
	out float3 out_strandNormalTS,
	out float3 out_debugColor)
{
#if (HAIR_VERTEX_ID_LINES || HAIR_VERTEX_ID_STRIPS)
	HairVertex v = GetHairVertex_Live(in_vertexID, in_vertexUV);
#else
	HairVertex v = GetHairVertex_Static(in_staticPositionOS, in_staticNormalOS, in_staticTangentOS);
#endif
	{
		out_positionOS = v.positionOS;
		out_normalOS = v.normalOS;
		out_tangentOS = v.tangentOS;
		out_bitangentOS = v.bitangentOS;
		out_rootUV = v.rootUV;
		out_strandUV = v.strandUV;
		out_strandIndex = v.strandIndex;
		out_strandNormalTS = v.strandNormalTS;
		out_debugColor = v.debugColor;
	}
}

#endif//__HAIRVERTEX_HLSL__
