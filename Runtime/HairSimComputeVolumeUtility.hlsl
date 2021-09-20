#ifndef __HAIRSIMCOMPUTEVOLUMEUTILITY_HLSL__
#define __HAIRSIMCOMPUTEVOLUMEUTILITY_HLSL__

#include "HairSimData.hlsl"
#include "HairSimComputeConfig.hlsl"
/*
	_VolumeCells = (3, 3, 3)

			  +---+---+---Q
		 +---+---+---+  o |
	+---+---+---+  o | ---+
	| o | o | o | ---+  o |
	+---+---+---+  o | ---+
	| o | o | o | ---+  o |
	+---+---+---+  o | ---+
	| o | o | o | ---+
	P---+---+---+

	_VolumeWorldMin = P
	_VolumeWorldMax = Q

	https://asawicki.info/news_1516_half-pixel_offset_in_directx_11.html

	worldToUVW(P) = (0, 0, 0)
	worldToUVW(Q) = (1, 1, 1)

	worldToLocal(P) = (0, 0, 0)
	worldToLocal(Q) = (3, 3, 3)
*/

float3 VolumeWorldSize()
{
	return _VolumeWorldMax - _VolumeWorldMin;
}

float3 VolumeWorldExtent()
{
	return VolumeWorldSize() * 0.5;
}

float3 VolumeWorldCellSize()
{
	return VolumeWorldSize() / _VolumeCells;
}

float VolumeWorldCellVolume()
{
	float3 worldCellSize = VolumeWorldCellSize();
	return worldCellSize.x * worldCellSize.y * worldCellSize.z;
}

float3 VolumeLocalToUVW(float3 localPos)
{
	float3 uvw = localPos / _VolumeCells;
	return uvw;
}

float3 VolumeUVWToLocal(float3 uvw)
{
	float3 localPos = uvw * _VolumeCells;
	return localPos;
}

float3 VolumeUVWToWorld(float3 uvw)
{
	return lerp(_VolumeWorldMin, _VolumeWorldMax, uvw);
}

//TODO sanitize out of bounds?
float3 VolumeWorldToUVW(float3 worldPos)
{
	float3 uvw = (worldPos - _VolumeWorldMin) / (_VolumeWorldMax - _VolumeWorldMin);
	return uvw;
}

float3 VolumeWorldToLocal(float3 worldPos)
{
	float3 localPos = _VolumeCells * VolumeWorldToUVW(worldPos);
	return localPos;
}

uint3 VolumeWorldToIndex(float3 worldPos)
{
	float3 localPos = VolumeWorldToLocal(worldPos);
	float3 localPosFloor = floor(localPos);
	return localPosFloor;
}

float3 VolumeIndexToUVW(uint3 index)
{
	float3 uvw = (index + 0.5) / _VolumeCells;
	return uvw;
}

float3 VolumeIndexToLocal(uint3 index)
{
	float3 localPos = index + 0.5;
	return localPos;
}

float3 VolumeIndexToWorld(uint3 index)
{
	float3 worldPos = _VolumeWorldMin + (index + 0.5) * VolumeWorldCellSize();
	return worldPos;
}

uint3 VolumeFlatIndexToIndex(uint flatIndex)
{
	uint x = (flatIndex % _VolumeCells.x);
	uint y = (flatIndex / _VolumeCells.x) % _VolumeCells.y;
	uint z = (flatIndex / (_VolumeCells.x * _VolumeCells.y));
	return uint3(x, y, z);
}

float3 VolumeStaggededOffsets()
{
	return 0.5 / _VolumeCells;
}

float3 VolumeStaggeredSample(Texture3D<float3> volume, float3 uvw, SamplerState state)
{
	return float3(
		volume.SampleLevel(state, uvw + float3(VolumeStaggededOffsets().x, 0.0, 0.0), 0).x,
		volume.SampleLevel(state, uvw + float3(0.0, VolumeStaggededOffsets().y, 0.0), 0).y,
		volume.SampleLevel(state, uvw + float3(0.0, 0.0, VolumeStaggededOffsets().z), 0).z);
}

float3 VolumeStaggeredSample(Texture3D<float4> volume, float3 uvw, SamplerState state)
{
	return float3(
		volume.SampleLevel(state, uvw + float3(VolumeStaggededOffsets().x, 0.0, 0.0), 0).x,
		volume.SampleLevel(state, uvw + float3(0.0, VolumeStaggededOffsets().y, 0.0), 0).y,
		volume.SampleLevel(state, uvw + float3(0.0, 0.0, VolumeStaggededOffsets().z), 0).z);
}

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
	const float2 h = float2(1.0 / _VolumeCells.x, 0.0);
	
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

struct TrilinearWeights
{
	uint3 idx0;
	float3 w0;
	float3 w1;
};

TrilinearWeights VolumeWorldToCellTrilinear(float3 worldPos, float3 offset = 0.5)
{
	float3 localPos = VolumeWorldToLocal(worldPos) - offset;// subtract offset to cell center
	float3 localPosFloor = floor(localPos);

	uint3 cellIdx0 = localPosFloor;
	float3 cellPos = localPos - localPosFloor;

	TrilinearWeights tri;
	tri.idx0 = cellIdx0;
	tri.w0 = 1.0 - cellPos;
	tri.w1 = cellPos;
	return tri;
}

float3 VolumeWorldCellStep(float3 worldDir)
{
#if VOLUME_SQUARE_CELLS
	float3 dirAbs = abs(worldDir);
	float3 dirMax = max(dirAbs.x, max(dirAbs.y, dirAbs.z));
	float3 step = worldDir * (VolumeWorldCellSize().x / dirMax);
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

VolumeTraceState VolumeTraceBegin(float3 worldPos, float3 worldDir, float cellOffset, int cellSubsteps)
{
	float3 uvwStep = VolumeWorldCellStep(worldDir) / (_VolumeWorldMax - _VolumeWorldMin);
	float3 uvw = VolumeWorldToUVW(worldPos) + cellOffset * uvwStep;

	uvwStep /= cellSubsteps;
	uvw -= uvwStep;

	VolumeTraceState trace;
	{
		trace.uvwStep = uvwStep;
		trace.uvw = uvw;
	}

	return trace;
}

bool VolumeTraceStep(inout VolumeTraceState trace)
{
	trace.uvw += trace.uvwStep;

	// terminate if for any axis the trace is outside the volume and not reentering
	if (any((trace.uvw < 0 && trace.uvwStep <= 0) || (trace.uvw > 1 && trace.uvwStep >= 0)))
	{
		return false;
	}
	else
	{
		return true;
	}
}

#endif//__HAIRSIMCOMPUTEVOLUMEUTILITY_HLSL__
