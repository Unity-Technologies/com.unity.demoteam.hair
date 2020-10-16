#ifndef __HAIRSIMVERTEX_HLSL__
#define __HAIRSIMVERTEX_HLSL__

#include "HairSimData.hlsl"

void HairSimVertex_float(in float3 osPosition, in float2 uv, out float3 wsPosition)
{
#if HAIRSIMVERTEX_ENABLE_POSITION
	wsPosition = _ParticlePosition[(int)uv.x].xyz;
#else
	wsPosition = osPosition;
#endif
}

#endif//__HAIRSIMVERTEX_HLSL__
