#ifndef __HAIRVOLUME_HLSL__
#define __HAIRVOLUME_HLSL__

#include "HairSimData.hlsl"
#include "HairSimComputeVolumeUtility.hlsl"

Texture3D _UntypedVolumeDensity;
Texture3D _UntypedVolumeVelocity;
Texture3D _UntypedVolumeScattering;

void HairVolume_float(
	in float3 in_positionWS,
	out float3 out_boundsMinWS,
	out float3 out_boundsMaxWS,
	out float3 out_cellCount,
	out float3 out_cellSizeWS,
	out float3 out_cellSizeUVW,
	out Texture3D out_volumeDensity,
	out Texture3D out_volumeVelocity,
	out Texture3D out_volumeScattering,
	out float3 out_volumeUVW)
{
	const VolumeLODGrid volumeDesc = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];
	{
		out_boundsMinWS = volumeDesc.volumeWorldMin;
		out_boundsMaxWS = volumeDesc.volumeWorldMax;
		out_cellCount = volumeDesc.volumeCellCount;
		out_cellSizeWS = VolumeWorldCellSize(volumeDesc);
		out_cellSizeUVW = 1.0f / volumeDesc.volumeCellCount;
		out_volumeDensity = _UntypedVolumeDensity;
		out_volumeVelocity = _UntypedVolumeVelocity;
		out_volumeScattering = _UntypedVolumeScattering;
		out_volumeUVW = VolumeWorldToUVW(volumeDesc, in_positionWS);
	}
}

#endif//__HAIRVOLUME_HLSL__
