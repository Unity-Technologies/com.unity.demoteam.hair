#ifndef __HAIRVOLUME_HLSL__
#define __HAIRVOLUME_HLSL__

#include "HairSimData.hlsl"
#include "HairSimComputeVolumeUtility.hlsl"

Texture3D _UntypedVolumeDensity;
Texture3D _UntypedVolumeVelocity;
Texture3D _UntypedVolumeStrandCountProbe;

void HairVolume_float(
	in float3 in_positionWS,
	out float3 out_boundsMinWS,
	out float3 out_boundsMaxWS,
	out float3 out_cellCount,
	out float3 out_cellSizeWS,
	out float3 out_cellSizeUVW,
	out Texture3D out_volumeDensity,
	out Texture3D out_volumeVelocity,
	out Texture3D out_volumeStrandCountProbe,
	out float3 out_volumeUVW)
{
	out_boundsMinWS = _VolumeWorldMin;
	out_boundsMaxWS = _VolumeWorldMax;
	out_cellCount = _VolumeCells;
	out_cellSizeWS = VolumeWorldCellSize();
	out_cellSizeUVW = 1.0f / _VolumeCells;
	out_volumeDensity = _UntypedVolumeDensity;
	out_volumeVelocity = _UntypedVolumeVelocity;
	out_volumeStrandCountProbe = _UntypedVolumeStrandCountProbe;
	out_volumeUVW = VolumeWorldToUVW(in_positionWS);
}

#endif//__HAIRVOLUME_HLSL__
