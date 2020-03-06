#ifndef __HAIRSIMCOMPUTE_VOLUMEUTILITY__
#define __HAIRSIMCOMPUTE_VOLUMEUTILITY__

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

float3 VolumeIndexToWorld(uint3 index)
{
	return _VolumeWorldMin + (index + 0.5) * VolumeWorldCellSize();
}

uint3 VolumeFlatIndexToIndex(uint flatIndex)
{
	uint x = (flatIndex % _VolumeCells.x);
	uint y = (flatIndex / _VolumeCells.x) % _VolumeCells.y;
	uint z = (flatIndex / (_VolumeCells.x * _VolumeCells.y));
	return uint3(x, y, z);
}

#endif//__HAIRSIMCOMPUTE_VOLUMEUTILITY__
