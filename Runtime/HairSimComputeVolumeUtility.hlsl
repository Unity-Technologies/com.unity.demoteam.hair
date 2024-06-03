#ifndef __HAIRSIMCOMPUTEVOLUMEUTILITY_HLSL__
#define __HAIRSIMCOMPUTEVOLUMEUTILITY_HLSL__

#include "HairSimData.hlsl"
#include "HairSimComputeConfig.hlsl"
/*
	_VolumeCells = (3, 3, 3)

			  +---+---+-- Q
		 +---+---+---+  o :
	+---+---+---+  o | ---+
	| o | o | o | ---+  o |
	+---+---+---+  o | ---+
	| o | o | o | ---+  o |
	+---+---+---+  o | ---+
	: o | o | o | ---+
	P --+---+---+

	_VolumeWorldMin = P
	_VolumeWorldMax = Q

	https://asawicki.info/news_1516_half-pixel_offset_in_directx_11.html

	worldToUVW(P) = (0, 0, 0)
	worldToUVW(Q) = (1, 1, 1)

	worldToLocal(P) = (0, 0, 0)
	worldToLocal(Q) = (3, 3, 3)
*/

float3 VolumeWorldSize(const VolumeLODGrid lodGrid)
{
	return lodGrid.volumeWorldMax.xyz - lodGrid.volumeWorldMin.xyz;
}

float3 VolumeWorldCellSize(const VolumeLODGrid lodGrid)
{
	return VolumeWorldSize(lodGrid) / _GridResolution.xxx;
}

float VolumeWorldCellVolume(const VolumeLODGrid lodGrid)
{
	float3 worldCellSize = VolumeWorldCellSize(lodGrid);
	return worldCellSize.x * worldCellSize.y * worldCellSize.z;
}

//-------------
// conversions

float3 VolumeWorldToUVW(const VolumeLODGrid lodGrid, float3 worldPos)
{
	//TODO sanitize out of bounds?
	float3 uvw = (worldPos - lodGrid.volumeWorldMin.xyz) / (lodGrid.volumeWorldMax.xyz - lodGrid.volumeWorldMin.xyz);
	return uvw;
}

float3 VolumeWorldToLocal(const VolumeLODGrid lodGrid, float3 worldPos)
{
	float3 localPos = _GridResolution.xxx * VolumeWorldToUVW(lodGrid, worldPos);
	return localPos;
}

uint3 VolumeWorldToIndex(const VolumeLODGrid lodGrid, float3 worldPos)
{
	float3 localPos = VolumeWorldToLocal(lodGrid, worldPos);
	float3 localPosFloor = floor(localPos);
	return localPosFloor;
}

float3 VolumeUVWToWorld(const VolumeLODGrid lodGrid, float3 uvw)
{
	return lerp(lodGrid.volumeWorldMin.xyz, lodGrid.volumeWorldMax.xyz, uvw);
}

float3 VolumeUVWToLocal(const VolumeLODGrid lodGrid, float3 uvw)
{
	float3 localPos = uvw * _GridResolution.xxx;
	return localPos;
}

float3 VolumeLocalToUVW(const VolumeLODGrid lodGrid, float3 localPos)
{
	float3 uvw = localPos / _GridResolution.xxx;
	return uvw;
}

float3 VolumeIndexToUVW(const VolumeLODGrid lodGrid, uint3 index)
{
	float3 uvw = (index + 0.5) / _GridResolution.xxx;
	return uvw;
}

float3 VolumeIndexToLocal(const VolumeLODGrid lodGrid, uint3 index)
{
	float3 localPos = index + 0.5;
	return localPos;
}

float3 VolumeIndexToWorld(const VolumeLODGrid lodGrid, uint3 index)
{
	float3 worldPos = lodGrid.volumeWorldMin.xyz + (index + 0.5) * VolumeWorldCellSize(lodGrid);
	return worldPos;
}

uint VolumeIndexToFlatIndex(uint3 index)
{
	uint3 gridResolution = _GridResolution.xxx;
	uint3 clampedIndex = min(index, gridResolution - 1);
	uint flatIndex = (
		clampedIndex.z * (gridResolution.x * gridResolution.y) +
		clampedIndex.y * gridResolution.x +
		clampedIndex.x
	);
	return flatIndex;
}

uint3 VolumeFlatIndexToIndex(uint flatIndex)
{
	uint3 gridResolution = _GridResolution.xxx;
	uint x = (flatIndex % gridResolution.x);
	uint y = (flatIndex / gridResolution.x) % gridResolution.y;
	uint z = (flatIndex / (gridResolution.x * gridResolution.y));
	return uint3(x, y, z);
}

//---------------
// sample scalar

float VolumeSampleScalar(Texture3D<float> volume, float3 uvw, SamplerState state)
{
	return volume.SampleLevel(state, uvw, 0);
}

float VolumeSampleScalar(Texture3D<float> volume, float3 uvw)
{
	return VolumeSampleScalar(volume, uvw, _Volume_trilinear_clamp);
}

float3 VolumeSampleScalarGradient(Texture3D<float> volume, float3 uvw)
{
	const float2 h = float2(1.0 / _GridResolution, 0.0);
	
	float s_xm = VolumeSampleScalar(volume, uvw - h.xyy, _Volume_trilinear_clamp);
	float s_ym = VolumeSampleScalar(volume, uvw - h.yxy, _Volume_trilinear_clamp);
	float s_zm = VolumeSampleScalar(volume, uvw - h.yyx, _Volume_trilinear_clamp);

	float s_xp = VolumeSampleScalar(volume, uvw + h.xyy, _Volume_trilinear_clamp);
	float s_yp = VolumeSampleScalar(volume, uvw + h.yxy, _Volume_trilinear_clamp);
	float s_zp = VolumeSampleScalar(volume, uvw + h.yyx, _Volume_trilinear_clamp);

	const float3 diff = float3(
		s_xp - s_xm,
		s_yp - s_ym,
		s_zp - s_zm);

	return diff / (2.0 * h.x);
}

//---------------
// sample vector

float3 VolumeStaggededOffsetUVW()
{
	return 0.5 / _GridResolution.xxx;
}

float3 VolumeStaggeredSample(Texture3D<float3> volume, float3 uvw, SamplerState state)
{
	return float3(
		volume.SampleLevel(state, uvw + float3(VolumeStaggededOffsetUVW().x, 0.0, 0.0), 0).x,
		volume.SampleLevel(state, uvw + float3(0.0, VolumeStaggededOffsetUVW().y, 0.0), 0).y,
		volume.SampleLevel(state, uvw + float3(0.0, 0.0, VolumeStaggededOffsetUVW().z), 0).z);
}

float3 VolumeStaggeredSample(Texture3D<float4> volume, float3 uvw, SamplerState state)
{
	return float3(
		volume.SampleLevel(state, uvw + float3(VolumeStaggededOffsetUVW().x, 0.0, 0.0), 0).x,
		volume.SampleLevel(state, uvw + float3(0.0, VolumeStaggededOffsetUVW().y, 0.0), 0).y,
		volume.SampleLevel(state, uvw + float3(0.0, 0.0, VolumeStaggededOffsetUVW().z), 0).z);
}

float3 VolumeSampleVector(Texture3D<float3> volume, float3 uvw, SamplerState state)
{
#if VOLUME_STAGGERED_GRID
	return VolumeStaggeredSample(volume, uvw, state);
#else
	return volume.SampleLevel(state, uvw, 0).xyz;
#endif
}

float3 VolumeSampleVector(Texture3D<float4> volume, float3 uvw, SamplerState state)
{
#if VOLUME_STAGGERED_GRID
	return VolumeStaggeredSample(volume, uvw, state);
#else
	return volume.SampleLevel(state, uvw, 0).xyz;
#endif
}

float3 VolumeSampleVector(Texture3D<float3> volume, float3 uvw)
{
	return VolumeSampleVector(volume, uvw, _Volume_trilinear_clamp);
}

float3 VolumeSampleVector(Texture3D<float4> volume, float3 uvw)
{
	return VolumeSampleVector(volume, uvw, _Volume_trilinear_clamp);
}

//---------
// weights

struct TrilinearWeights
{
	uint3 idx0;
	float3 w0;
	float3 w1;
};

TrilinearWeights VolumeWorldToCellTrilinear(const VolumeLODGrid lodGrid, float3 worldPos, float3 offset = 0.5)
{
	float3 localPos = VolumeWorldToLocal(lodGrid, worldPos) - offset;// subtracting offset to cell center
	float3 localPosFloor = floor(localPos);

	uint3 cellIdx0 = localPosFloor;
	float3 cellPos = localPos - localPosFloor;

	TrilinearWeights tri;
	tri.idx0 = cellIdx0;
	tri.w0 = 1.0 - cellPos;
	tri.w1 = cellPos;
	return tri;
}

float VolumeWorldToCellTrilinearInverseMultiplier(const VolumeLODGrid lodGrid, float3 worldPos, float3 offset = 0.5)
{
	//TODO comment/illustrate

	TrilinearWeights tri = VolumeWorldToCellTrilinear(lodGrid, worldPos);

	const float3 sqw0 = tri.w0 * tri.w0;
	const float3 sqw1 = tri.w1 * tri.w1;

	const float d = (
		sqw0.x * sqw0.y * sqw0.z +
		sqw1.x * sqw0.y * sqw0.z +
		sqw0.x * sqw1.y * sqw0.z +
		sqw1.x * sqw1.y * sqw0.z +
		sqw0.x * sqw0.y * sqw1.z +
		sqw1.x * sqw0.y * sqw1.z +
		sqw0.x * sqw1.y * sqw1.z +
		sqw1.x * sqw1.y * sqw1.z
		);

	return 1.0 / d;
}

//-------
// trace

float3 VolumeWorldCellStep(const VolumeLODGrid lodGrid, float3 worldDir)
{
#if VOLUME_SQUARE_CELLS
	float3 dirAbs = abs(worldDir);
	float3 dirMax = max(dirAbs.x, max(dirAbs.y, dirAbs.z));
	float3 step = worldDir * (VolumeWorldCellSize(lodGrid).x / dirMax);
	return step;
#else
	//TODO or otherwise always force VOLUME_SQUARE_CELLS ?
#endif
}

struct VolumeTraceState
{
	float3 uvw;
	float3 uvwStep;
};

VolumeTraceState VolumeTraceBegin(const VolumeLODGrid lodGrid, float3 worldPos, float3 worldDir, float cellOffset, int cellSubsteps)
{
	float3 uvwStep = VolumeWorldCellStep(lodGrid, worldDir) / (lodGrid.volumeWorldMax.xyz - lodGrid.volumeWorldMin.xyz);
	float3 uvw = VolumeWorldToUVW(lodGrid, worldPos) + cellOffset * uvwStep;

	uvwStep /= cellSubsteps;
	uvw -= uvwStep;

	VolumeTraceState trace;
	{
		trace.uvwStep = uvwStep;
		trace.uvw = uvw;
	}

	return trace;
}

bool VolumeTraceEnded(in VolumeTraceState trace)
{
	// trace has ended if for any axis the trace is outside the volume and not reentering
#if 0
	if (any(and(trace.uvw < 0, trace.uvwStep <= 0)) || any(and(trace.uvw > 1, trace.uvwStep >= 0)))
#else
	if (any(trace.uvw < 0 && trace.uvwStep <= 0) || any(trace.uvw > 1 && trace.uvwStep >= 0))
#endif
		return true;
	else
		return false;
}

bool VolumeTraceStep(inout VolumeTraceState trace)
{
	trace.uvw += trace.uvwStep;

	// signal no further steps required if the trace has exited the volume
	if (VolumeTraceEnded(trace))
		return false;
	else
		return true;
}

#endif//__HAIRSIMCOMPUTEVOLUMEUTILITY_HLSL__
