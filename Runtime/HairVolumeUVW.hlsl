#ifndef __HAIRVOLUMEUVW_HLSL__
#define __HAIRVOLUMEUVW_HLSL__

#include "HairSimComputeVolumeUtility.hlsl"

void HairVolumeUVW_float(
	in float3 in_positionWS,
	out float3 out_volumeUVW)
{
	out_volumeUVW = VolumeWorldToUVW(in_positionWS);
}

#endif//__HAIRVOLUMEUVW_HLSL__
