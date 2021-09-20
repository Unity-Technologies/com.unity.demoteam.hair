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

StructuredBuffer<float2> _RootUV;					// xy: strand root uv
StructuredBuffer<float> _RootScale;					// x: relative strand length [0..1] (to maximum within group)
StructuredBuffer<float4> _RootPosition;				// xyz: strand root position, w: -
StructuredBuffer<float4> _RootDirection;			// xyz: strand root direction, w: -
StructuredBuffer<float4> _RootFrame;				// quat(xyz,w): strand root material frame where (0,1,0) is tangent

HAIRSIM_SOLVERDATA<float4> _InitialRootFrame;			// quat(xyz,w): initial strand root material frame
HAIRSIM_SOLVERDATA<float4> _InitialParticleOffset;		// xyz: initial particle offset from strand root, w: -
HAIRSIM_SOLVERDATA<float4> _InitialParticleFrameDelta;	// quat(xyz,w): initial particle material frame delta

HAIRSIM_SOLVERDATA<float4> _ParticlePosition;		// xyz: position, w: initial local accumulated weight (gather)
HAIRSIM_SOLVERDATA<float4> _ParticlePositionPrev;		// ...
HAIRSIM_SOLVERDATA<float4> _ParticlePositionPrevPrev;	// ...
HAIRSIM_SOLVERDATA<float4> _ParticlePositionCorr;	// xyz: ftl correction, w: -
HAIRSIM_SOLVERDATA<float4> _ParticleVelocity;		// xyz: velocity, w: splatting weight
HAIRSIM_SOLVERDATA<float4> _ParticleVelocityPrev;	// xyz: velocity, w: splatting weight

HAIRSIM_SOLVERDATA<uint> _LODGuideCount;			// n: lod index -> num. guides
HAIRSIM_SOLVERDATA<uint> _LODGuideIndex;			// i: lod index * strand count + strand index -> guide index
HAIRSIM_SOLVERDATA<float> _LODGuideCarry;			// f: lod index * strand count + strand index -> guide carry

//----------------
// solver staging

#if STAGING_COMPRESSION
HAIRSIM_SOLVERDATA<uint2> _StagingPosition;			// xy: encoded position
HAIRSIM_SOLVERDATA<uint2> _StagingPositionPrev;		// ...
#else
HAIRSIM_SOLVERDATA<float3> _StagingPosition;		// xyz: position
HAIRSIM_SOLVERDATA<float3> _StagingPositionPrev;	// ...
#endif

//-------------
// volume data

#if HAIRSIM_WRITEABLE_VOLUMEDATA
#define HAIRSIM_VOLUMEDATA RWTexture3D
#else
#define HAIRSIM_VOLUMEDATA Texture3D
#endif

#if HAIRSIM_WRITEABLE_VOLUMEPROBE
#define HAIRSIM_VOLUMEPROBE RWTexture3D
#else
#define HAIRSIM_VOLUMEPROBE Texture3D
#endif

HAIRSIM_VOLUMEDATA<int> _AccuWeight;				// x: fp accumulated weight
HAIRSIM_VOLUMEDATA<int> _AccuWeight0;				// x: fp accumulated target weight
HAIRSIM_VOLUMEDATA<int> _AccuVelocityX;				// x: fp accumulated x-velocity
HAIRSIM_VOLUMEDATA<int> _AccuVelocityY;				// x: ... ... ... .. y-velocity
HAIRSIM_VOLUMEDATA<int> _AccuVelocityZ;				// x: .. ... ... ... z-velocity
//TODO this sure would be nice: https://developer.nvidia.com/unlocking-gpu-intrinsics-hlsl

HAIRSIM_VOLUMEDATA<float> _VolumeDensity;			// x: density (as fraction occupied)
HAIRSIM_VOLUMEDATA<float> _VolumeDensity0;			// x: density target
HAIRSIM_VOLUMEDATA<float4> _VolumeVelocity;			// xyz: velocity, w: accumulated weight

HAIRSIM_VOLUMEDATA<float> _VolumeDivergence;		// x: velocity divergence + source term
HAIRSIM_VOLUMEDATA<float> _VolumePressure;			// x: pressure
HAIRSIM_VOLUMEDATA<float> _VolumePressureNext;		// x: pressure (output of iteration)
HAIRSIM_VOLUMEDATA<float3> _VolumePressureGrad;		// xyz: pressure gradient, w: -

HAIRSIM_VOLUMEPROBE<float4> _VolumeStrandCountProbe;

SamplerState _Volume_point_clamp;
SamplerState _Volume_trilinear_clamp;

//-------------------
// volume boundaries

Texture3D<float> _BoundarySDF;

struct BoundaryShape
{
	//  shape   |  float3     float      float3     float
	//  -------------------------------------------------------
	//  capsule |  centerA    radius     centerB    __pad
	//  sphere  |  center     radius     __pad      __pad
	//  torus   |  center     radiusA    axis       radiusB
	//  cube    |  __pad      __pad      __pad      __pad

	float3 pA; float tA;
	float3 pB; float tB;
};

StructuredBuffer<BoundaryShape> _BoundaryShape;
StructuredBuffer<float4x4> _BoundaryMatrix;
StructuredBuffer<float4x4> _BoundaryMatrixInv;
StructuredBuffer<float4x4> _BoundaryMatrixW2PrevW;

//---------
// utility

uint2 EncodePosition(float3 p, float4 originExtent)
{
	uint3 p_f16 = f32tof16((p - originExtent.xyz) / originExtent.w);
	uint2 p_enc;
	{
		p_enc.x = p_f16.x << 16 | p_f16.y;
		p_enc.y = p_f16.z;
	}
	return p_enc;
}

float3 DecodePosition(uint2 p_enc, float4 originExtent)
{
	uint3 p_f16 = uint3(p_enc.x >> 16, p_enc.x & 0xffff, p_enc.y);
	float3 p = f16tof32(p_f16) * originExtent.w + originExtent.xyz;
	return p;
}

float3 LoadStagingPosition(uint i)
{
#if STAGING_COMPRESSION
	return DecodePosition(_StagingPosition[i], _StagingOriginExtent);
#else
	return _StagingPosition[i].xyz;
#endif
}

float3 LoadStagingPositionPrev(uint i)
{
#if STAGING_COMPRESSION
	return DecodePosition(_StagingPositionPrev[i], _StagingOriginExtentPrev);
#else
	return _StagingPositionPrev[i].xyz;
#endif
}

void StoreStagingPosition(uint i, float3 p)
{
#if HAIRSIM_WRITEABLE_SOLVERDATA
#if STAGING_COMPRESSION
	_StagingPosition[i] = EncodePosition(p, _StagingOriginExtent);
#else
	_StagingPosition[i].xyz = p;
#endif
#endif
}

#endif//__HAIRSIMDATA_HLSL__
