#ifndef __HAIRSIMCOMPUTEVOLUMETRANSFER_HLSL__
#define __HAIRSIMCOMPUTEVOLUMETRANSFER_HLSL__

#include "HairSimData.hlsl"

uint GetParticleStrandIndex(uint particleIndex)
{
#if LAYOUT_INTERLEAVED
	return particleIndex % _StrandCount;
#else
	return particleIndex / _StrandParticleCount;
#endif
}

float GetParticleLODCarry(uint particleIndex)
{
#if VOLUME_SPLAT_CLUSTERS
	LODIndices lodDesc = _SolverLODStage[SOLVERLODSTAGE_PHYSICS];

	//TODO check if it's worth doing this in practice
	//uint strandIndex = GetParticleStrandIndex(particleIndex);
	//float strandCarryLo = _LODGuideCarry[(lodDesc.lodIndexLo * _StrandCount) + strandIndex];
	//float strandCarryHi = _LODGuideCarry[(lodDesc.lodIndexHi * _StrandCount) + strandIndex];
	//return lerp(strandCarryLo, strandCarryHi, lodDesc.lodBlendFrac);

	return _LODGuideCarry[(lodDesc.lodIndexHi * _StrandCount) + GetParticleStrandIndex(particleIndex)];
#else
	return 1.0;
#endif
}

float GetParticleVolumeWeight(uint particleIndex)
{
	float2 S = _RootScale[GetParticleStrandIndex(particleIndex)].xy;
	float V = (_GroupMaxParticleVolume) * (S.x * S.y * S.y);
	return (V * GetParticleLODCarry(particleIndex)) / _AllGroupsMaxParticleVolume;
}

#define WEIGHT_BITS 16
#define WEIGHT_MASK 0xFFFF

#if PLATFORM_SUPPORTS_TEXTURE_ATOMICS
#define ACCUIDX uint3
#define WORLDIDX_TO_ACCUIDX(idx) (idx)
#else
#define ACCUIDX uint
#define WORLDIDX_TO_ACCUIDX(idx) VolumeIndexToFlatIndex(idx)
#endif

void InterlockedAddTrilinear(HAIRSIM_VOLUMEACCU<int> volume, float value, uint3 idx0, float3 w0, float3 w1)
{
	const uint2 h = uint2(1, 0);
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yyy)], (int)round(value * w0.x * w0.y * w0.z));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xyy)], (int)round(value * w1.x * w0.y * w0.z));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yxy)], (int)round(value * w0.x * w1.y * w0.z));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xxy)], (int)round(value * w1.x * w1.y * w0.z));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yyx)], (int)round(value * w0.x * w0.y * w1.z));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xyx)], (int)round(value * w1.x * w0.y * w1.z));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yxx)], (int)round(value * w0.x * w1.y * w1.z));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xxx)], (int)round(value * w1.x * w1.y * w1.z));
}

void InterlockedAddTrilinearPackW(HAIRSIM_VOLUMEACCU<int> volume, float value, uint3 idx0, float3 w0, float3 w1)
{
	const uint2 h = uint2(1, 0);
	const float r = (1 << SPLAT_FRACTIONAL_BITS);
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yyy)], ((int)round(value * w0.x * w0.y * w0.z) << WEIGHT_BITS) | ((uint)round(r * w0.x * w0.y * w0.z) & WEIGHT_MASK));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xyy)], ((int)round(value * w1.x * w0.y * w0.z) << WEIGHT_BITS) | ((uint)round(r * w1.x * w0.y * w0.z) & WEIGHT_MASK));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yxy)], ((int)round(value * w0.x * w1.y * w0.z) << WEIGHT_BITS) | ((uint)round(r * w0.x * w1.y * w0.z) & WEIGHT_MASK));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xxy)], ((int)round(value * w1.x * w1.y * w0.z) << WEIGHT_BITS) | ((uint)round(r * w1.x * w1.y * w0.z) & WEIGHT_MASK));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yyx)], ((int)round(value * w0.x * w0.y * w1.z) << WEIGHT_BITS) | ((uint)round(r * w0.x * w0.y * w1.z) & WEIGHT_MASK));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xyx)], ((int)round(value * w1.x * w0.y * w1.z) << WEIGHT_BITS) | ((uint)round(r * w1.x * w0.y * w1.z) & WEIGHT_MASK));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yxx)], ((int)round(value * w0.x * w1.y * w1.z) << WEIGHT_BITS) | ((uint)round(r * w0.x * w1.y * w1.z) & WEIGHT_MASK));
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xxx)], ((int)round(value * w1.x * w1.y * w1.z) << WEIGHT_BITS) | ((uint)round(r * w1.x * w1.y * w1.z) & WEIGHT_MASK));
}

void InterlockedMaxTrilinear(HAIRSIM_VOLUMEACCU<int> volume, float value, uint3 idx0, float3 w0, float3 w1)
{
	const uint2 h = uint2(1, 0);
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yyy)], (int)round(value * w0.x * w0.y * w0.z));
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xyy)], (int)round(value * w1.x * w0.y * w0.z));
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yxy)], (int)round(value * w0.x * w1.y * w0.z));
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xxy)], (int)round(value * w1.x * w1.y * w0.z));
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yyx)], (int)round(value * w0.x * w0.y * w1.z));
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xyx)], (int)round(value * w1.x * w0.y * w1.z));
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.yxx)], (int)round(value * w0.x * w1.y * w1.z));
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(idx0 + h.xxx)], (int)round(value * w1.x * w1.y * w1.z));
}

void InterlockedAddParticleContribution(const VolumeLODGrid lodGrid, float3 worldPos, float value, HAIRSIM_VOLUMEACCU<int> volume, float3 offset = 0.5)
{
#if SPLAT_TRILINEAR
	TrilinearWeights tri = VolumeWorldToCellTrilinear(lodGrid, worldPos, offset);
	InterlockedAddTrilinear(volume, value * (1 << SPLAT_FRACTIONAL_BITS), tri.idx0, tri.w0, tri.w1);
#else
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(VolumeWorldToIndex(lodGrid, worldPos))], (int) round(value * (1 << SPLAT_FRACTIONAL_BITS)));
#endif
}

void InterlockedAddParticleContributionPackW(const VolumeLODGrid lodGrid, float3 worldPos, float value, HAIRSIM_VOLUMEACCU<int> volume, float3 offset = 0.5)
{
#if SPLAT_TRILINEAR
	TrilinearWeights tri = VolumeWorldToCellTrilinear(lodGrid, worldPos, offset);
	InterlockedAddTrilinearPackW(volume, value * (1 << SPLAT_FRACTIONAL_BITS), tri.idx0, tri.w0, tri.w1);
#else
	InterlockedAdd(volume[WORLDIDX_TO_ACCUIDX(VolumeWorldToIndex(lodGrid, worldPos))], (int) round(value * (1 << SPLAT_FRACTIONAL_BITS)));
#endif
}

void InterlockedMaxParticleContribution(const VolumeLODGrid lodGrid, float3 worldPos, float value, HAIRSIM_VOLUMEACCU<int> volume, float3 offset = 0.5)
{
#if SPLAT_TRILINEAR
	TrilinearWeights tri = VolumeWorldToCellTrilinear(lodGrid, worldPos, offset);
	InterlockedMaxTrilinear(volume, value * (1 << SPLAT_FRACTIONAL_BITS), tri.idx0, tri.w0, tri.w1);
#else
	InterlockedMax(volume[WORLDIDX_TO_ACCUIDX(VolumeWorldToIndex(lodGrid, worldPos))], (int) round(value * (1 << SPLAT_FRACTIONAL_BITS)));
#endif
}

#endif//__HAIRSIMCOMPUTEVOLUMETRANSFER_HLSL__
