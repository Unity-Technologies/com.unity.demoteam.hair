#ifndef __HAIRVERTEX_HLSL__
#define __HAIRVERTEX_HLSL__

#include "HairSimData.hlsl"
#include "HairSimDebugDrawUtility.hlsl"

#ifndef HAIR_VERTEX_DYNAMIC
#define HAIR_VERTEX_DYNAMIC 0
#endif

#if LAYOUT_INTERLEAVED
  #define DECLARE_STRAND(x)							\
	const uint strandIndex = x;						\
	const uint strandParticleBegin = strandIndex;	\
	const uint strandParticleStride = _StrandCount;	\
	const uint strandParticleEnd = strandParticleBegin + strandParticleStride * _StrandParticleCount;
#else
  #define DECLARE_STRAND(x)													\
	const uint strandIndex = x;												\
	const uint strandParticleBegin = strandIndex * _StrandParticleCount;	\
	const uint strandParticleStride = 1;									\
	const uint strandParticleEnd = strandParticleBegin + strandParticleStride * _StrandParticleCount;
#endif

struct HairVertex
{
	float3 position;
	float3 normal;
	float3 tangent;
	float3 bitangent;
	float4 uv;
};

HairVertex GetHairVertex(in float3 staticPos, in float3 staticDir, in float4 uvID)
{
	DECLARE_STRAND(uvID.z);
	const uint i = uvID.w;

#if HAIR_VERTEX_DYNAMIC
	float3 p = _ParticlePosition[i].xyz;
	float3 r_prev = (i == strandParticleBegin) ? normalize(_RootDirection[strandIndex].xyz) : normalize(p - _ParticlePosition[i - strandParticleStride].xyz);
	float3 r_next = (i == strandParticleEnd - strandParticleStride) ? r_prev : normalize(_ParticlePosition[i + strandParticleStride].xyz - p);

	//TODO
	//float3 motion = GetCameraRelativePositionWS(p - _ParticlePositionPrev[i].xyz);
	float3 position = GetCameraRelativePositionWS(p);
	float3 bitangent = normalize(r_prev + r_next);
#else
	float3 position = GetCameraRelativePositionWS(staticPos);
	float3 bitangent = staticDir;
#endif
	float3 tangent = normalize(cross(bitangent, GetWorldSpaceNormalizeViewDir(position)));
	float3 normal = cross(tangent, bitangent);

	HairVertex v;
	{
#if HAIR_VERTEX_DYNAMIC
		v.position = TransformWorldToObject(position + tangent * (_StrandDiameter * _StrandScale) * (uvID.x - 0.5));
#else
		v.position = TransformWorldToObject(position);
#endif
		v.normal = normal;
		v.tangent = tangent;
		v.bitangent = bitangent;
		v.uv = float4(uvID.xy, 0.0, 0.0);//TODO store root-uv in zw
	}

	return v;
}

void HairVertex_float(
	in float3 in_staticPos,
	in float3 in_staticDir,
	in float4 in_uvID,
	out float3 out_position,
	out float3 out_normal,
	out float3 out_tangent,
	out float3 out_bitangent,
	out float4 out_uv,
	out float3 out_tsCylinder,
	out float3 out_debugColor)
{
	HairVertex v = GetHairVertex(in_staticPos, in_staticDir, in_uvID);
	{
		out_position = v.position;
		out_normal = v.normal;
		out_tangent = v.tangent;
		out_bitangent = v.bitangent;
		out_uv = v.uv;
	}

	float s = 2.0 * v.uv.x - 1.0;
	{
		// [0..1] -> [-1..1]
		out_tsCylinder.x = 2.0 * v.uv.x - 1.0;
		out_tsCylinder.y = 0.0;
		out_tsCylinder.z = sqrt(max(1e-5, 1.0 - s * s));
	}

	out_debugColor = ColorCycle(in_uvID.z, _StrandCount);
}

#endif//__HAIRVERTEX_HLSL__
