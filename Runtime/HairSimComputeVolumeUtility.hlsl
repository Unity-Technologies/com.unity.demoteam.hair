#ifndef __HAIRSIMCOMPUTE_VOLUMEUTILITY__
#define __HAIRSIMCOMPUTE_VOLUMEUTILITY__

#include "HairSimComputeConfig.hlsl"
#include "HairSimComputeVolumeData.hlsl"
/*
	_VolumeCells = (3, 3, 3)

			  +---+---+---Q
		 +---+---+---+    |
	+---+---+---+    | ---+
	|   |   |   | ---+    |
	+---+---+---+    | ---+
	|   |   |   | ---+    |
	+---+---+---+    | ---+
	|   |   |   | ---+
	P---+---+---+

	_VolumeWorldMin = P
	_VolumeWorldMax = Q
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

float VolumeSampleScalar(Texture3D<float> volume, float3 uvw)
{
	return VolumeSampleScalar(volume, uvw, _Volume_trilinear_clamp);
}

float3 VolumeSampleVector(Texture3D<float3> volume, float3 uvw)
{
	return VolumeSampleVector(volume, uvw, _Volume_trilinear_clamp);
}

float3 VolumeSampleVector(Texture3D<float4> volume, float3 uvw)
{
	return VolumeSampleVector(volume, uvw, _Volume_trilinear_clamp);
}

#endif//__HAIRSIMCOMPUTE_VOLUMEUTILITY__
