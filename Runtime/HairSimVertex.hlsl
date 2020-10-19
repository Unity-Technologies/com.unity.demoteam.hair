#ifndef __HAIRSIMVERTEX_HLSL__
#define __HAIRSIMVERTEX_HLSL__

#include "HairSimData.hlsl"
#include "HairSimDebugDrawUtility.hlsl"

void HairSimVertex_float(in float3 osPosition, in float2 uv, out float3 wsPosition, out float3 debugColor)
{
	int strandParticleIndex = (int)uv.x;
	int strandIndex = (int)uv.y;

	debugColor = ColorCycle(strandIndex, _StrandCount);

#if HAIRSIMVERTEX_STATIC_PREVIEW
	wsPosition = osPosition;
#else
	wsPosition = _ParticlePosition[strandParticleIndex].xyz;
#endif
}

#endif//__HAIRSIMVERTEX_HLSL__
