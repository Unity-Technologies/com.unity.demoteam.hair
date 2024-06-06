//#pragma use_dxc
//#pragma enable_d3d11_debug_symbols

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
#include "HairSimComputeLOD.hlsl"
#include "HairSimDebugDrawColors.hlsl"

#ifndef UNITY_PREV_MATRIX_I_M// not defined by e.g. URP graphs prior to 2021.2.x
#define UNITY_PREV_MATRIX_I_M UNITY_MATRIX_I_M
#endif

#define STRAND_PARTICLE_COUNT	_StagingStrandVertexCount
#define STRAND_PARTICLE_OFFSET	_StagingStrandVertexOffset

#define DECLARE_STRAND(x)													\
	const uint strandIndex = x;												\
	const uint strandParticleBegin = strandIndex * STRAND_PARTICLE_OFFSET;	\
	const uint strandParticleStride = _StrandParticleStride;				\
	const uint strandParticleEnd = strandParticleBegin + strandParticleStride * STRAND_PARTICLE_COUNT;

//---------
// utility

float3 LoadPosition(const uint i, const LODBounds lodBounds)
{
	return LoadStagingPosition(i, lodBounds);
}

float3 LoadPositionPrev(const uint i, const LODBounds lodBoundsPrev)
{
	return LoadStagingPositionPrev(i, lodBoundsPrev);
}

float GetStrandParticleOffset(const uint strandParticleNumber)
{
	return strandParticleNumber / (float)(STRAND_PARTICLE_COUNT - 1);
}

float GetStrandParticleTaperScale(const uint strandParticleNumber, const float2 strandTaperOffsetScale)
{
	float strandParticleOffset = GetStrandParticleOffset(strandParticleNumber);
	float strandParticleTaperT = saturate((strandParticleOffset - strandTaperOffsetScale.x) / (1.0 - strandTaperOffsetScale.x));
	return lerp(1.0, strandTaperOffsetScale.y, strandParticleTaperT);
}

float3 GetStrandDebugColor(const uint strandIndex)
{
	LODIndices lodDesc = _SolverLODStage[SOLVERLODSTAGE_PHYSICS];
	uint guideIndexLo = _LODGuideIndex[(lodDesc.lodIndexLo * _StrandCount) + strandIndex];
	uint guideIndexHi = _LODGuideIndex[(lodDesc.lodIndexHi * _StrandCount) + strandIndex];
	float3 guideColorLo = ColorCycle(guideIndexLo, _LODGuideCount[_LODCount - 1]);
	float3 guideColorHi = ColorCycle(guideIndexHi, _LODGuideCount[_LODCount - 1]);
	return lerp(guideColorLo, guideColorHi, lodDesc.lodBlendFrac);
}

//-----------
// accessors

int _DecodeVertexCount;
int _DecodeVertexWidth;
int _DecodeVertexComponentValue;
int _DecodeVertexComponentWidth;

struct HairVertexID
{
	uint strandIndex;
	uint vertexIndex;
	uint vertexFacet;
	float2 tubularUV;
};

static const HairVertexID defaultHairVertexID =
{
	/* uint strandIndex; */ 0,
	/* uint vertexIndex; */ 0,
	/* uint vertexFacet; */ 0,
	/* float2 tubularUV; */ float2(0.5, 0.0),
};

struct HairVertexModifiers
{
	float lodScale;
	float lodBias;
	float widthMod;
	bool widthSet;
};

static const HairVertexModifiers defaultHairVertexModifiers =
{
	/* float lodScale; */ 1.0,
	/* float lodBias;  */ 0.0,
	/* float widthMod; */ 1.0,
	/* bool widthSet;  */ false,
};

struct HairVertexData
{
	float3 surfacePosition;
	float3 surfaceNormal;
	float3 surfaceTangent;
	float3 surfaceVelocity;
	float3 surfaceNormalTS;
	float2 surfaceUV;
	float2 surfaceUVClip;
	float lodOutputOpacity;
	float lodOutputWidth;
	float2 rootUV;
	float4 rootScale;
	uint strandIndex;
	float3 strandIndexColor;
};

static const HairVertexData defaultHairVertexData =
{
	/* float3 surfacePosition;  */ float3(0.0, 0.0, 0.0),
	/* float3 surfaceNormal;    */ float3(0.0, 0.0, 0.0),
	/* float3 surfaceTangent;   */ float3(0.0, 0.0, 0.0),
	/* float3 surfaceVelocity;  */ float3(0.0, 0.0, 0.0),
	/* float3 surfaceNormalTS;  */ float3(0.0, 0.0, 1.0),
	/* float2 surfaceUV;        */ float2(0.5, 0.0),
	/* float2 surfaceUVClip;    */ float2(0.5, 0.0),
	/* float lodOutputOpacity;  */ 1.0,
	/* float lodOutputWidth;    */ 1.0 * 0.1,
	/* float2 rootUV;           */ float2(0.0, 0.0),
	/* float4 rootScale;        */ float4(1.0, 1.0, 1.0, 1.0),
	/* uint strandIndex;        */ 0,
	/* float3 strandIndexColor; */ float3(0.5, 0.5, 0.5),
};

HairVertexID DecodeHairVertexID(float4 packedID)
{
	HairVertexID id;
	{
		uint4 unpack = round(packedID * _DecodeVertexComponentValue);

		id.strandIndex = (
			(unpack.w << ((_DecodeVertexComponentWidth << 1) - _DecodeVertexWidth)) |
			(unpack.z << ((_DecodeVertexComponentWidth << 0) - _DecodeVertexWidth)) |
			(unpack.y >> _DecodeVertexWidth)
		);
		
		id.vertexIndex = unpack.x;
		id.vertexFacet = unpack.y & ((1 << _DecodeVertexWidth) - 1);
		
		//TODO evaluate this in comparison
		//	id.strandIndex = floor(dot(packedID.yzw, _DecodeStrandIndex.xyz))
		//	id.vertexFacet = frac(packedID.y * _DecodeStrandFacet.x) * _DecodeStrandFacet.y
		//	id.vertexIndex = packedID.x * _DecodeStrandIndex.w

		if (_DecodeVertexCount == 1)
		{
			id.tubularUV.x = 0.5;
			id.tubularUV.y = (packedID.x * _DecodeVertexComponentValue) / (float) (STRAND_PARTICLE_COUNT - 1);
		}
		else
		{
			id.tubularUV.x = frac(packedID.y * (_DecodeVertexComponentValue / (float)(1 << _DecodeVertexWidth))) * ((1 << _DecodeVertexWidth) / (float)_DecodeVertexCount);
			id.tubularUV.y = (packedID.x * _DecodeVertexComponentValue) / (float) (STRAND_PARTICLE_COUNT - 1);
		}
	}
	return id;
}

float3 GetSurfaceNormalTS(const float2 tubularUV)
{
	float3 surfaceNormalTS;
	{
		if (_DecodeVertexCount == 2)// strips
		{
			surfaceNormalTS.x = 4.0 * saturate(tubularUV.x) - 1.0;
			surfaceNormalTS.y = 0.0;
			surfaceNormalTS.z = sqrt(max(1e-5, 1.0 - surfaceNormalTS.x * surfaceNormalTS.x));
		}
		else// everything else
		{
			surfaceNormalTS = float3(0.0, 0.0, 1.0);
		}
	}
	return surfaceNormalTS;
}

float2 GetSurfaceUV(const float2 tubularUV)
{
	float2 surfaceUV = tubularUV;
	{
		if (_DecodeVertexCount >= 2)// strips, tubes
		{
			surfaceUV.x *= 2.0;
		}
	}
	return surfaceUV;
}

HairVertexData GetHairVertexWS(const HairVertexID id, const HairVertexModifiers m)
{
	DECLARE_STRAND(id.strandIndex);
	const uint i = strandParticleBegin + id.vertexIndex * strandParticleStride;
	const uint i_next = i + strandParticleStride;
	const uint i_prev = i - strandParticleStride;
	const uint i_head = strandParticleBegin;
	const uint i_tail = strandParticleEnd - strandParticleStride;

	const LODBounds lodBounds = _Bounds[_GroupBoundsIndex];
	const LODBounds lodBoundsPrev = _BoundsPrev[_GroupBoundsIndex];

	float3 p = LoadPosition(i, lodBounds);
	float3 r0 = (i == i_head) ? LoadPosition(i_next, lodBounds) - p : p - LoadPosition(i_prev, lodBounds);
	float3 r1 = (i == i_tail) ? r0 /* .......................... */ : LoadPosition(i_next, lodBounds) - p;

	float3 curvePositionRWS = HAIR_VERTEX_IMPL_WS_POS_TO_RWS(p);
	float3 curvePositionRWSPrev = HAIR_VERTEX_IMPL_WS_POS_TO_RWS(LoadPositionPrev(i, lodBoundsPrev));
	float3 curveTangentWS = (r0 + r1); // approx tangent to curve

	// calc world radius
	float radius = (0.5 * _GroupMaxParticleDiameter) * _RootScale[strandIndex].y;
	{
		// apply clustering lod
		{
			LODFrustum lodFrustum = MakeLODFrustumForCurrentCamera();

			float curveSpan = 2.0 * radius;
			float curveDepth = dot(lodFrustum.cameraForward, curvePositionRWS - lodFrustum.cameraPosition);
			float curveCoverage = LODFrustumCoverage(lodFrustum, curveDepth, curveSpan);

			// lod selection
			LODIndices lodDesc;
			{
				switch (_RenderLODMethod)
				{
					case RENDERLODSELECTION_AUTOMATIC_PER_SEGMENT:
						lodDesc = ResolveLODIndices(ResolveLODQuantity(curveCoverage, _RenderLODCeiling, _RenderLODScale * m.lodScale, _RenderLODBias + m.lodBias));
						break;

					default:
						if (m.lodScale == 1.0 && m.lodBias == 0.0)
						{
							lodDesc = _SolverLODStage[SOLVERLODSTAGE_RENDERING];
						}
						else
						{
							lodDesc = ResolveLODIndices(ResolveLODQuantity(_SolverLODStage[SOLVERLODSTAGE_RENDERING].lodValue, _RenderLODCeiling, m.lodScale, m.lodBias));
						}
						break;
				}
			}

			// lod subpixel accumulation -> cluster centroid
			{
				float guideCarryLo = _LODGuideCarry[(lodDesc.lodIndexLo * _StrandCount) + strandIndex];
				float guideCarryHi = _LODGuideCarry[(lodDesc.lodIndexHi * _StrandCount) + strandIndex];
				float guideCarry = lerp(guideCarryLo, guideCarryHi, lodDesc.lodBlendFrac);

				float guideReachLo = _LODGuideReach[(lodDesc.lodIndexLo * _StrandCount) + strandIndex] * _GroupScale;
				float guideReachHi = _LODGuideReach[(lodDesc.lodIndexHi * _StrandCount) + strandIndex] * _GroupScale;
				float guideReach = lerp(guideReachLo, guideReachHi, lodDesc.lodBlendFrac);

				float guideProjectedCoverageLo = 1.0 - exp(-radius * guideCarryLo / guideReachLo);
				float guideProjectedCoverageHi = 1.0 - exp(-radius * guideCarryHi / guideReachHi);
				float guideProjectedCoverage = 1.0 - exp(-radius * guideCarry / guideReach);

#define USE_PASSING_FRACTION 1
#if USE_PASSING_FRACTION
				if (_RenderLODMethod == RENDERLODSELECTION_AUTOMATIC_PER_SEGMENT)
				{
					//float lodThreshClip = lodClipThreshold;
					//                    = unitSpanSubpixelDepth / unitSpanClippingDepth;
					//float lodThreshClipCluster = unitSpanSubpixelDepth / (unitSpanClippingDepth + guideReach * 2);
					//                           = unitSpanSubpixelDepth / ((unitSpanSubpixelDepth / lodClipThreshold) + guideReach * 2);
					//                           = (unitSpanSubpixelDepth * lodClipThreshold) / (unitSpanSubpixelDepth + 2 * guideReach * lodClipThreshold);

					float lodThresh = 1.0;
					float lodThreshClip = max(_RenderLODClipThreshold, 1e-5);
					float lodThreshClipCluster = (lodFrustum.unitSpanSubpixelDepth * lodThreshClip) / (lodFrustum.unitSpanSubpixelDepth + 2.0 * guideReach * lodThreshClip);

					float farDepth = curveDepth + guideReach;
					float farLod = saturate(lodDesc.lodValue * (curveDepth / farDepth));
					float farLodT = saturate((farLod - lodThreshClipCluster) / (lodThresh - lodThreshClipCluster));

#define USE_CIRCULAR_SECTION 1
#if USE_CIRCULAR_SECTION
					// replaces t with normalized area of circular section at height 2rt:
					// A = (acos(1-2*x)-(1-2*x)*2*sqrt(x*(1-x)))/PI
					farLodT = (acos(1.0 - 2.0 * farLodT) - (1.0 - 2.0 * farLodT) * 2 * sqrt(farLodT * (1.0 - farLodT))) / 3.14159;
#endif

					radius = lerp((radius + guideReach) * guideProjectedCoverage, radius, farLodT);
				}
				else
#endif
				{
					radius = lerp((radius + guideReachLo) * guideProjectedCoverageLo, (radius + guideReachHi) * guideProjectedCoverageHi, lodDesc.lodBlendFrac);
				}

				curveSpan = 2.0 * radius;
				curveCoverage = LODFrustumCoverage(lodFrustum, curveDepth, curveSpan);
			}

			// lod subpixel discard
			{
				if (curveCoverage < _RenderLODClipThreshold)
				{
					curvePositionRWS = asfloat(0x7FC00000u); // => NaN
				}
			}
		}

		// apply tapering
		{
			radius *= GetStrandParticleTaperScale(id.vertexIndex, _RootScale[strandIndex].zw);
		}
		
		// apply scaling
		{
			radius = m.widthMod * (m.widthSet ? 0.5 : radius);
		}
	}

	// calc surface vectors
	float3 surfaceTangentWS = normalize_safe(cross(HAIR_VERTEX_IMPL_WS_POS_VIEW_DIR(curvePositionRWS), curveTangentWS));
	float3 surfaceNormalWS = normalize_safe(cross(curveTangentWS, surfaceTangentWS));
	
	// calc surface offset
	float3 surfaceOffsetWS = 0;
	{
		if (_DecodeVertexCount > 1)
		{
			// calc offset in plane
			float2 surfaceOffset2D;
			{
				sincos((0.5 - id.tubularUV.x) * 6.2831853, surfaceOffset2D.y, surfaceOffset2D.x);
			}

			// calc offset in world space
			if (_DecodeVertexCount == 2)
			{
				surfaceOffsetWS =
					(radius * surfaceOffset2D.x) * surfaceTangentWS +
					(radius * surfaceOffset2D.y) * surfaceNormalWS;
			}
			else
			{
				surfaceNormalWS =
					(surfaceOffset2D.x) * surfaceTangentWS +
					(surfaceOffset2D.y) * surfaceNormalWS;
				
				surfaceOffsetWS =
					(radius) * surfaceNormalWS;
			}
		}
	}

	// assemble output
	HairVertexData v;
	{
		v.surfacePosition = surfaceOffsetWS + curvePositionRWS;
		v.surfaceNormal = surfaceNormalWS;
		v.surfaceTangent = surfaceTangentWS;
		v.surfaceVelocity = curvePositionRWS - curvePositionRWSPrev;
		v.surfaceNormalTS = GetSurfaceNormalTS(id.tubularUV);
		v.surfaceUV = GetSurfaceUV(id.tubularUV);//TODO scroll to handle twist / change in view direction
		v.surfaceUVClip = GetSurfaceUV(id.tubularUV) * float2(1.0, _RootScale[strandIndex].x);//TODO scroll to handle twist / change in view direction
		v.lodOutputOpacity = 1.0;//TODO reserved for later use
		v.lodOutputWidth = (2.0 * radius) * 100.0;// output in cm
		v.rootUV = _RootUV[strandIndex];
		v.rootScale = _RootScale[strandIndex];
		v.strandIndex = strandIndex;
		v.strandIndexColor = GetStrandDebugColor(strandIndex);
	}
	return v;
}

HairVertexData GetHairVertexOS(const HairVertexID id, const HairVertexModifiers m)
{
	HairVertexData v = GetHairVertexWS(id, m);
	{
#define USE_OBJECT_SPACE_DELTA 1
#if USE_OBJECT_SPACE_DELTA
		float3 surfacePositionOS = mul(UNITY_MATRIX_I_M, float4(v.surfacePosition, 1.0)).xyz;
		float3 surfacePositionOSPrev = mul(UNITY_PREV_MATRIX_I_M, float4(v.surfacePosition - v.surfaceVelocity, 1.0)).xyz;
		
		v.surfacePosition = surfacePositionOS;
		v.surfaceNormal = HAIR_VERTEX_IMPL_WS_VEC_TO_OS(v.surfaceNormal);
		v.surfaceTangent = HAIR_VERTEX_IMPL_WS_VEC_TO_OS(v.surfaceTangent);
		v.surfaceVelocity = surfacePositionOS - surfacePositionOSPrev;
#else
		v.surfacePosition = mul(UNITY_MATRIX_I_M, float4(v.surfacePosition, 1.0)).xyz;
		v.surfaceNormal = HAIR_VERTEX_IMPL_WS_VEC_TO_OS(v.surfaceNormal);
		v.surfaceTangent = HAIR_VERTEX_IMPL_WS_VEC_TO_OS(v.surfaceTangent);
		v.surfaceVelocity = mul(UNITY_PREV_MATRIX_I_M, float4(v.surfaceVelocity, 0.0)).xyz;
#endif
	}
	return v;
}

HairVertexData GetHairVertex(
	const float4 packedID,
	const float3 staticPositionOS,
	const float3 staticNormalOS,
	const float3 staticTangentOS,
	const HairVertexModifiers m = defaultHairVertexModifiers)
{
#if HAIR_VERTEX_LIVE || !SHADER_STAGE_VERTEX
	if (_DecodeVertexCount > 0)
	{
		return GetHairVertexOS(DecodeHairVertexID(packedID), m);
	}
	else
#endif
	{
		HairVertexData v = defaultHairVertexData;
		{
			v.surfacePosition = staticPositionOS;
			v.surfaceNormal = staticNormalOS;
			v.surfaceTangent = staticTangentOS;
		}
		return v;
	}
}

//-------------
// shadergraph

void HairVertex_float(
	const float4 in_packedID,
	const float3 in_staticPositionOS,
	const float3 in_staticNormalOS,
	const float3 in_staticTangentOS,

	const float in_lodScale,
	const float in_lodBias,
	const float in_widthMod,
	const bool in_widthSet,

	out float3 out_surfacePositionOS,
	out float3 out_surfaceNormalOS,
	out float3 out_surfaceTangentOS,
	out float3 out_surfaceVelocityOS,
	out float3 out_surfaceNormalTS,
	out float2 out_surfaceUV,
	out float2 out_surfaceUVClip,
	out float out_lodOutputOpacity,
	out float out_lodOutputWidth,
	out float2 out_rootUV,
	out float4 out_rootScale,
	out float out_strandIndex,
	out float3 out_strandIndexColor)
{
	HairVertexModifiers m = { in_lodScale, in_lodBias, in_widthMod, in_widthSet };
	HairVertexData v = GetHairVertex(in_packedID, in_staticPositionOS, in_staticNormalOS, in_staticTangentOS, m);
	{
		out_surfacePositionOS = v.surfacePosition;
		out_surfaceNormalOS = v.surfaceNormal;
		out_surfaceTangentOS = v.surfaceTangent;
		out_surfaceVelocityOS = v.surfaceVelocity;
		out_surfaceNormalTS = v.surfaceNormalTS;
		out_surfaceUV = v.surfaceUV;
		out_surfaceUVClip = v.surfaceUVClip;
		out_lodOutputOpacity = v.lodOutputOpacity;
		out_lodOutputWidth = v.lodOutputWidth;
		out_rootUV = v.rootUV;
		out_rootScale = v.rootScale;
		out_strandIndex = v.strandIndex;
		out_strandIndexColor = v.strandIndexColor;
	}
}

#endif//__HAIRVERTEX_HLSL__
