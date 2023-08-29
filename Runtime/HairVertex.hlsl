#ifndef __HAIRVERTEX_HLSL__
#define __HAIRVERTEX_HLSL__

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

#include "HairSimData.hlsl"
#include "HairSimDebugDrawUtility.hlsl"

#ifndef UNITY_PREV_MATRIX_I_M// not defined by e.g. URP graphs prior to 2021.2.x
#define UNITY_PREV_MATRIX_I_M UNITY_MATRIX_I_M
#endif

#define STRAND_PARTICLE_COUNT	_StagingVertexCount
#define STRAND_PARTICLE_OFFSET	_StagingVertexOffset

#define DECLARE_STRAND(x)													\
	const uint strandIndex = x;												\
	const uint strandParticleBegin = strandIndex * STRAND_PARTICLE_OFFSET;	\
	const uint strandParticleStride = _StrandParticleStride;				\
	const uint strandParticleEnd = strandParticleBegin + strandParticleStride * STRAND_PARTICLE_COUNT;

float3 LoadPosition(uint i)
{
	return LoadStagingPosition(i);
}

float3 LoadPositionPrev(uint i)
{
	return LoadStagingPositionPrev(i);
}

int _DecodeVertexCount;
int _DecodeVertexWidth;
int _DecodeVertexComponentValue;
int _DecodeVertexComponentWidth;

float3 GetStrandDebugColor(in uint strandIndex)
{
	uint strandIndexLo = _LODGuideIndex[(_LODIndexLo * _StrandCount) + strandIndex];
	uint strandIndexHi = _LODGuideIndex[(_LODIndexHi * _StrandCount) + strandIndex];
	float3 strandColorLo = ColorCycle(strandIndexLo, _LODGuideCount[_LODCount - 1]);
	float3 strandColorHi = ColorCycle(strandIndexHi, _LODGuideCount[_LODCount - 1]);
	return lerp(strandColorLo, strandColorHi, _LODBlendFraction);
}

float3 GetSurfaceNormalTS(in float2 tubularUV)
{
	float3 surfaceNormalTS;
	{
		if (_DecodeVertexCount >= 2)
		{
			surfaceNormalTS.x = 4.0 * saturate(tubularUV.x) - 1.0;
			surfaceNormalTS.y = 0.0;
			surfaceNormalTS.z = sqrt(max(1e-5, 1.0 - surfaceNormalTS.x * surfaceNormalTS.x));
		}
		else
		{
			surfaceNormalTS = float3(0.0, 0.0, 1.0);
		}
	}
	return surfaceNormalTS;
}

float2 GetSurfaceUV(in float2 tubularUV)
{
	float2 surfaceUV = tubularUV;
	{
		if (_DecodeVertexCount >= 2)
		{
			surfaceUV.x *= 2.0;
		}
	}
	return surfaceUV;
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
};

HairVertexWS GetHairVertexWS_Live(in float4 packedID, in float2 packedUV)
{
	uint decodedStrandIndex;
	uint decodedVertexIndex;
	uint decodedVertexFacet;
	float decodedVertexSign;
	float2 decodedTubularUV;
	{
		if (_DecodeVertexComponentWidth == 32)//TODO replace runtime compatibility with asset versioning / upgrade
		{
			uint linearParticleIndex = packedID.x / _DecodeVertexCount;
		
			decodedStrandIndex = linearParticleIndex / STRAND_PARTICLE_COUNT;
			decodedVertexIndex = linearParticleIndex % STRAND_PARTICLE_COUNT;
			decodedVertexSign = -1.0;

			if (_DecodeVertexCount == 1)
				decodedTubularUV = packedUV;
			else
				decodedTubularUV = packedUV * float2(0.5, 1.0);
		}
		else
		{
			uint4 unpack = round(packedID * _DecodeVertexComponentValue);

			decodedStrandIndex = (unpack.w << ((_DecodeVertexComponentWidth << 1) - _DecodeVertexWidth)) |
								 (unpack.z << ((_DecodeVertexComponentWidth << 0) - _DecodeVertexWidth)) |
								 (unpack.y >> _DecodeVertexWidth);
			decodedVertexFacet = unpack.y & ((1 << _DecodeVertexWidth) - 1);
			decodedVertexIndex = unpack.x;
			decodedVertexSign = 1.0;
			
			//uint decodedStrandIndex = floor(dot(packedID.yzw, _DecodeStrandIndex.xyz))
			//uint decodedVertexFacet = frac(packedID.y * _DecodeStrandFacet.x) * _DecodeStrandFacet.y
			//uint decodedVertexIndex = packedID.x * _DecodeStrandIndex.w

			if (_DecodeVertexCount == 1)
			{
				decodedTubularUV.x = 0.5;
				decodedTubularUV.y = (packedID.x * _DecodeVertexComponentValue) / (float)(STRAND_PARTICLE_COUNT - 1);
			}
			else
			{
				decodedTubularUV.x = frac(packedID.y * (_DecodeVertexComponentValue / (float)(1 << _DecodeVertexWidth))) * ((1 << _DecodeVertexWidth) / (float)_DecodeVertexCount);
				decodedTubularUV.y = (packedID.x * _DecodeVertexComponentValue) / (float)(STRAND_PARTICLE_COUNT - 1);
			}
		}
	}

	DECLARE_STRAND(decodedStrandIndex);
	const uint i = strandParticleBegin + decodedVertexIndex * strandParticleStride;
	const uint i_next = i + strandParticleStride;
	const uint i_prev = i - strandParticleStride;
	const uint i_head = strandParticleBegin;
	const uint i_tail = strandParticleEnd - strandParticleStride;

	float3 p = LoadPosition(i);
	float3 r0 = (i == i_head) ? LoadPosition(i_next) - p : p - LoadPosition(i_prev);
	float3 r1 = (i == i_tail) ? r0 /* ............... */ : LoadPosition(i_next) - p;

	float3 curvePositionRWS = HAIR_VERTEX_IMPL_WS_POS_TO_RWS(p);
	float3 curvePositionRWSPrev = HAIR_VERTEX_IMPL_WS_POS_TO_RWS(LoadPositionPrev(i));

	float3 vertexBitangentWS = (r0 + r1);// approx tangent to curve
	float3 vertexTangentWS = decodedVertexSign * normalize_safe(cross(HAIR_VERTEX_IMPL_WS_POS_VIEW_DIR(curvePositionRWS), vertexBitangentWS));
	float3 vertexNormalWS = decodedVertexSign * normalize_safe(cross(vertexBitangentWS, vertexTangentWS));

	float2 vertexOffset2D = 0;
	float3 vertexOffsetWS = 0;
	{
		if (_DecodeVertexCount > 1)
		{
			// calc offset in plane
			sincos((0.5 - decodedTubularUV.x) * 6.2831853, vertexOffset2D.y, vertexOffset2D.x);

			// calc offset in world space
			float radius = 0.5 * _GroupMaxParticleDiameter;

			vertexOffsetWS =
				(radius * vertexOffset2D.x) * vertexTangentWS +
				(radius * vertexOffset2D.y) * vertexNormalWS;
		}
	}

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
		x.strandUV = GetSurfaceUV(decodedTubularUV) * float2(1.0, _RootScale[strandIndex]);//TODO scroll to handle twist / change in view direction
		x.strandIndex = strandIndex;
		x.strandNormalTS = GetSurfaceNormalTS(decodedTubularUV);
		x.strandDebugColor = GetStrandDebugColor(decodedStrandIndex);
	}
	return x;
}

HairVertex GetHairVertex_Live(in float4 packedID, in float2 packedUV)
{
	HairVertexWS x = GetHairVertexWS_Live(packedID, packedUV);
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
	}
	return v;
}

HairVertex GetHairVertex(
	in float4 in_packedID,
	in float2 in_packedUV,
	in float3 in_staticPositionOS,
	in float3 in_staticNormalOS,
	in float3 in_staticTangentOS)
{
	if (_DecodeVertexCount > 0)
		return GetHairVertex_Live(in_packedID, in_packedUV);
	else
		return GetHairVertex_Static(in_staticPositionOS, in_staticNormalOS, in_staticTangentOS);
}

void HairVertex_float(
	in float4 in_packedID,
	in float2 in_packedUV,
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
	out float out_strandIndex,
	out float3 out_strandNormalTS,
	out float3 out_strandDebugColor)
{
	HairVertex v = GetHairVertex(in_packedID, in_packedUV, in_staticPositionOS, in_staticNormalOS, in_staticTangentOS);
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
	}
}

#endif//__HAIRVERTEX_HLSL__
