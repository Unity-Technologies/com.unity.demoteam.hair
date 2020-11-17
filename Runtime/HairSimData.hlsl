#ifndef __HAIRSIMDATA_HLSL__
#define __HAIRSIMDATA_HLSL__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#include "HairSimData.cs.hlsl"

//-------------
// solver data

#if HAIRSIM_WRITEABLE_SOLVERDATA
#define HAIRSIM_SOLVERDATA RWStructuredBuffer
#else
#define HAIRSIM_SOLVERDATA StructuredBuffer
#endif

HAIRSIM_SOLVERDATA<float> _Length;
HAIRSIM_SOLVERDATA<float4> _RootPosition;
HAIRSIM_SOLVERDATA<float4> _RootDirection;

HAIRSIM_SOLVERDATA<float4> _ParticlePosition;
HAIRSIM_SOLVERDATA<float4> _ParticlePositionPrev;
HAIRSIM_SOLVERDATA<float4> _ParticlePositionCorr;
HAIRSIM_SOLVERDATA<float4> _ParticlePositionPose;
HAIRSIM_SOLVERDATA<float4> _ParticleVelocity;
HAIRSIM_SOLVERDATA<float4> _ParticleVelocityPrev;

//-------------
// volume data

#if HAIRSIM_WRITEABLE_VOLUMEDATA
#define HAIRSIM_VOLUMEDATA RWTexture3D
#else
#define HAIRSIM_VOLUMEDATA Texture3D
#endif

HAIRSIM_VOLUMEDATA<int> _AccuDensity;
HAIRSIM_VOLUMEDATA<int> _AccuVelocityX;// this sure would be nice: https://developer.nvidia.com/unlocking-gpu-intrinsics-hlsl
HAIRSIM_VOLUMEDATA<int> _AccuVelocityY;
HAIRSIM_VOLUMEDATA<int> _AccuVelocityZ;

HAIRSIM_VOLUMEDATA<float> _VolumeDensity;
HAIRSIM_VOLUMEDATA<float4> _VolumeVelocity;
HAIRSIM_VOLUMEDATA<float> _VolumeDivergence;

HAIRSIM_VOLUMEDATA<float> _VolumePressure;
HAIRSIM_VOLUMEDATA<float> _VolumePressureNext;
HAIRSIM_VOLUMEDATA<float3> _VolumePressureGrad;

SamplerState _Volume_point_clamp;
SamplerState _Volume_trilinear_clamp;

struct BoundaryCapsule { float3 centerA; float radius; float3 centerB; float __pad__; };
struct BoundarySphere { float3 center; float radius; };
struct BoundaryTorus { float3 center; float radiusA; float3 axis; float radiusB; };
struct BoundaryPack
{
	//	shape	|	capsule		sphere		torus
	//	----------------------------------------------
	//	float3	|	centerA		center		center
	//	float	|	radius		radius		radiusA
	//	float3	|	centerB		__pad__		axis
	//	float	|	__pad__		__pad__		radiusB

	float3 pA;
	float tA;
	float3 pB;
	float tB;
};

StructuredBuffer<BoundaryCapsule> _BoundaryCapsule;
StructuredBuffer<BoundarySphere> _BoundarySphere;
StructuredBuffer<BoundaryTorus> _BoundaryTorus;
StructuredBuffer<BoundaryPack> _BoundaryPack;

StructuredBuffer<float4x4> _BoundaryMatrix;
StructuredBuffer<float4x4> _BoundaryMatrixInv;
StructuredBuffer<float4x4> _BoundaryMatrixW2PrevW;

#endif//__HAIRSIMDATA_HLSL__
