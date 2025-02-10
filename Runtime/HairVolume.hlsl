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
#if UNITY_VERSION >= 202023
	out UnityTexture3D out_volumeDensity,
	out UnityTexture3D out_volumeVelocity,
	out UnityTexture3D out_volumeScattering,
#else
	out Texture3D out_volumeDensity,
	out Texture3D out_volumeVelocity,
	out Texture3D out_volumeScattering,
#endif
	out float3 out_volumeUVW)
{
	const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];
	{
		out_boundsMinWS = lodGrid.volumeWorldMin;
		out_boundsMaxWS = lodGrid.volumeWorldMax;
		out_cellCount = lodGrid.volumeCellCount;
		out_cellSizeWS = VolumeWorldCellSize(lodGrid);
		out_cellSizeUVW = 1.0f / lodGrid.volumeCellCount;
#if UNITY_VERSION >= 202023
		out_volumeDensity.tex = _UntypedVolumeDensity;
		out_volumeDensity.samplerstate = _Volume_trilinear_clamp;
		out_volumeVelocity.tex = _UntypedVolumeVelocity;
		out_volumeVelocity.samplerstate = _Volume_trilinear_clamp;
		out_volumeScattering.tex = _UntypedVolumeScattering;
		out_volumeScattering.samplerstate = _Volume_trilinear_clamp;
#else
		out_volumeDensity = _UntypedVolumeDensity;
		out_volumeVelocity = _UntypedVolumeVelocity;
		out_volumeScattering = _UntypedVolumeScattering;
#endif
		out_volumeUVW = VolumeWorldToUVW(lodGrid, in_positionWS);
	}
}

#endif//__HAIRVOLUME_HLSL__
