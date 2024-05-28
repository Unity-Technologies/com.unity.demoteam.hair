#ifndef __HAIRSIMDATA_HLSL__
#define __HAIRSIMDATA_HLSL__

#include "HairSimData.cs.hlsl"
#include "HairSim.LOD.cs.hlsl"
	
//-------------
// solver data

#if HAIRSIM_WRITEABLE_SOLVERINPUT
#define HAIRSIM_SOLVERINPUT RWStructuredBuffer
#else
#define HAIRSIM_SOLVERINPUT StructuredBuffer
#endif

#if HAIRSIM_WRITEABLE_SOLVERINIT
#define HAIRSIM_SOLVERINIT RWStructuredBuffer
#else
#define HAIRSIM_SOLVERINIT StructuredBuffer
#endif

#if HAIRSIM_WRITEABLE_SOLVERDATA
#define HAIRSIM_SOLVERDATA RWStructuredBuffer
#else
#define HAIRSIM_SOLVERDATA StructuredBuffer
#endif

#if HAIRSIM_WRITEABLE_SOLVERDATA
#define HAIRSIM_RENDERDATA RWByteAddressBuffer
#else
#define HAIRSIM_RENDERDATA ByteAddressBuffer
#endif

StructuredBuffer<float2> _RootUV;						// xy: root uv
StructuredBuffer<float4> _RootScale;					// xy: root scale (length, diameter normalized to maximum within group), z: tip scale offset, w: tip scale

HAIRSIM_SOLVERINPUT<float4> _RootPositionNext;			// xyz: strand root position, w: -
HAIRSIM_SOLVERINPUT<float4> _RootPositionPrev;			// xyz: ...
HAIRSIM_SOLVERINPUT<float4> _RootPosition;				// xyz: ...
HAIRSIM_SOLVERINPUT<float4> _RootFrameNext;				// quat(xyz,w): strand root material frame where (0,1,0) is tangent to curve
HAIRSIM_SOLVERINPUT<float4> _RootFramePrev;				// quat(xyz,w): ...
HAIRSIM_SOLVERINPUT<float4> _RootFrame;					// quat(xyz,w): ...

HAIRSIM_SOLVERDATA<LODIndices> _SolverLODStage;			// x: lod index lo, y: lod index hi, z: lod blend fraction, w: lod value/quantity
HAIRSIM_SOLVERDATA<uint2> _SolverLODRange;				// xy: dispatch strand range [begin, end)
HAIRSIM_SOLVERDATA<uint> _SolverLODDispatch;			// xyz: dispatch args compute, w: dispatch strand count || xyzw: dispatch args draw

HAIRSIM_SOLVERINIT<float4> _InitialParticleOffset;		// xyz: initial particle offset from strand root, w: initial local accumulated weight (gather)
HAIRSIM_SOLVERINIT<float4> _InitialParticleFrameDelta;	// quat(xyz,w): initial particle material frame delta
HAIRSIM_SOLVERINIT<uint2> _InitialParticleFrameDelta16;	// xy: compressed initial particle material frame delta

HAIRSIM_SOLVERDATA<float3> _ParticlePosition;			// xyz: position
HAIRSIM_SOLVERINIT<float3> _ParticlePositionPrev;		// xyz: ...
HAIRSIM_SOLVERINIT<float3> _ParticlePositionPrevPrev;	// xyz: ...
HAIRSIM_SOLVERDATA<float3> _ParticleVelocity;			// xyz: velocity
HAIRSIM_SOLVERINIT<float3> _ParticleVelocityPrev;		// xyz: ...
HAIRSIM_SOLVERDATA<float3> _ParticleCorrection;			// xyz: ftl distance correction

StructuredBuffer<float2> _ParticleOptTexCoord;			// xy: optional particle uv
StructuredBuffer<float> _ParticleOptDiameter;			// x: optional particle diameter

StructuredBuffer<uint> _LODGuideCount;					// x: lod index -> num. guides
StructuredBuffer<uint> _LODGuideIndex;					// x: lod index * strand count + strand index -> guide index
StructuredBuffer<float> _LODGuideCarry;					// x: lod index * strand count + strand index -> guide carry
StructuredBuffer<float> _LODGuideReach;					// x: lod index * strand count + strand index -> guide reach (approximate cluster extent)

HAIRSIM_RENDERDATA _StagingVertex;						// xyz: position (uncompressed) || xy: position (compressed)
HAIRSIM_RENDERDATA _StagingVertexPrev;					// xyz: ...

//-------------
// volume data

#if HAIRSIM_WRITEABLE_VOLUMESUBSTEP
#define HAIRSIM_VOLUMESUBSTEP RWStructuredBuffer
#else
#define HAIRSIM_VOLUMESUBSTEP StructuredBuffer
#endif

#if HAIRSIM_WRITEABLE_VOLUMEBOUNDS
#define HAIRSIM_VOLUMEBOUNDS RWStructuredBuffer
#else
#define HAIRSIM_VOLUMEBOUNDS StructuredBuffer
#endif

#if SHADER_API_METAL
#define PLATFORM_SUPPORTS_TEXTURE_ATOMICS 0
#else
#define PLATFORM_SUPPORTS_TEXTURE_ATOMICS 1
#endif

#if PLATFORM_SUPPORTS_TEXTURE_ATOMICS
#if HAIRSIM_WRITEABLE_VOLUMEACCU
#define HAIRSIM_VOLUMEACCU RWTexture3D
#else
#define HAIRSIM_VOLUMEACCU Texture3D
#endif
#else
#if HAIRSIM_WRITEABLE_VOLUMEACCU
#define HAIRSIM_VOLUMEACCU RWStructuredBuffer
#else
#define HAIRSIM_VOLUMEACCU StructuredBuffer
#endif
#endif

#if HAIRSIM_WRITEABLE_VOLUMEDATA
#define HAIRSIM_VOLUMEDATA RWTexture3D
#else
#define HAIRSIM_VOLUMEDATA Texture3D
#endif

#if HAIRSIM_WRITEABLE_VOLUMEOPTS
#define HAIRSIM_VOLUMEOPTS RWTexture3D
#else
#define HAIRSIM_VOLUMEOPTS Texture3D
#endif

StructuredBuffer<LODFrustum> _LODFrustum;

HAIRSIM_VOLUMEBOUNDS<uint3> _BoundsMinMaxU;			// xyz: bounds min/max (unsigned sortable)
HAIRSIM_VOLUMEBOUNDS<LODBounds> _Bounds;			// array(LODBounds): bounds (center, extent, radius, reach)
HAIRSIM_VOLUMEBOUNDS<LODBounds> _BoundsPrev;		// array(LODBounds): bounds (center, extent, radius, reach)
HAIRSIM_VOLUMEBOUNDS<LODGeometry> _BoundsGeometry;	// array(LODGeometry): bounds geometry description (dimensions for coverage)
HAIRSIM_VOLUMEBOUNDS<float2> _BoundsCoverage;		// xy: bounds coverage (unbiased ceiling)

HAIRSIM_VOLUMEBOUNDS<VolumeLODGrid> _VolumeLODStage;// array(VolumeLODGrid): grid properties
HAIRSIM_VOLUMEBOUNDS<uint> _VolumeLODDispatch;		// xyz: num groups, w: num grid cells in one dimension

HAIRSIM_VOLUMEACCU<int> _AccuWeight;				// x: fp accumulated weight
HAIRSIM_VOLUMEACCU<int> _AccuWeight0;				// x: fp accumulated target weight
HAIRSIM_VOLUMEACCU<int> _AccuVelocityX;				// x: fp accumulated x-velocity
HAIRSIM_VOLUMEACCU<int> _AccuVelocityY;				// x: ... ... ... .. y-velocity
HAIRSIM_VOLUMEACCU<int> _AccuVelocityZ;				// x: .. ... ... ... z-velocity

HAIRSIM_VOLUMEDATA<float> _VolumeDensity;			// x: density (cell fraction occupied)
HAIRSIM_VOLUMEDATA<float> _VolumeDensity0;			// x: density target
HAIRSIM_VOLUMEDATA<float4> _VolumeVelocity;			// xyz: velocity, w: accumulated weight

HAIRSIM_VOLUMEDATA<float> _VolumeDivergence;		// x: velocity divergence + source term
HAIRSIM_VOLUMEDATA<float> _VolumePressure;			// x: pressure
HAIRSIM_VOLUMEDATA<float> _VolumePressureNext;		// x: pressure (output of iteration)
HAIRSIM_VOLUMEDATA<float3> _VolumePressureGrad;		// xyz: pressure gradient, w: -

HAIRSIM_VOLUMEOPTS<float4> _VolumeScattering;		// xyzw: L1 spherical harmonic
HAIRSIM_VOLUMEOPTS<float3> _VolumeImpulse;			// xyz: accumulated external forces, w: -

SamplerState _Volume_point_clamp;
SamplerState _Volume_trilinear_clamp;

//-------------------
// volume boundaries

struct BoundaryShape
{
	// shape    |  float3     float      float3     float
	// -------------------------------------------------------
	// discrete |  __pad      __pad      __pad      __pad
	// capsule  |  centerA    radius     centerB    __pad
	// sphere   |  center     radius     __pad      __pad
	// torus    |  center     radiusA    axis       radiusB
	// cube     |  center     rotf16x    extent     rotf16y

	float3 pA; float tA;
	float3 pB; float tB;
};

StructuredBuffer<float4x4> _BoundaryMatrixNext;
StructuredBuffer<float4x4> _BoundaryMatrixPrevA;
StructuredBuffer<float4> _BoundaryMatrixPrevQ;
HAIRSIM_VOLUMESUBSTEP<float4x4> _BoundaryMatrix;
HAIRSIM_VOLUMESUBSTEP<float4x4> _BoundaryMatrixInv;
HAIRSIM_VOLUMESUBSTEP<float4x4> _BoundaryMatrixInvStep;

StructuredBuffer<BoundaryShape>	_BoundaryShapeNext;
StructuredBuffer<int> _BoundaryShapePrevLUT;
HAIRSIM_VOLUMESUBSTEP<BoundaryShape> _BoundaryShapePrev;
HAIRSIM_VOLUMESUBSTEP<BoundaryShape> _BoundaryShape;

Texture3D<float> _BoundarySDF;

//-------------
// volume wind

struct WindEmitter
{
	float3 p;	// emitter origin
	float3 n;	// emitter forward
	float t0;	// emitter base offset
	float h0;	// emitter base radius
	float m;	// emitter slope

	float v;	// flow speed
	float A;	// flow pulse amplitude
	float f;	// flow pulse frequency

	float jd;	// jitter displacement
	float jw;	// jitter resolution
	float jp;	// jitter planar
};

StructuredBuffer<WindEmitter> _WindEmitterNext;
StructuredBuffer<int> _WindEmitterPrevLUT;
HAIRSIM_VOLUMESUBSTEP<WindEmitter> _WindEmitterPrev;
HAIRSIM_VOLUMESUBSTEP<WindEmitter> _WindEmitter;

//---------
// utility

uint2 EncodePosition(float3 p, float4 pivot)
{
	uint3 p_f16 = f32tof16((p - pivot.xyz) / pivot.w);
	uint2 p_enc = p_f16.xy | (p_f16.z << 16);
	return p_enc;
}

float3 DecodePosition(uint2 p_enc, float4 pivot)
{
	uint3 p_f16 = uint3(p_enc, p_enc.x >> 16);
	float3 p = f16tof32(p_f16) * pivot.w + pivot.xyz;
	return p;
}

float3 LoadStagingPosition(const uint i, const LODBounds lodBounds)
{
	const float4 stagingPivot = float4(lodBounds.center, lodBounds.reach);

	switch (_StagingVertexFormat)
	{
		case STAGINGVERTEXFORMAT_COMPRESSED:
			return DecodePosition(asuint(_StagingVertex.Load2(i * _StagingVertexStride)), stagingPivot);

		case STAGINGVERTEXFORMAT_UNCOMPRESSED:
			return asfloat(_StagingVertex.Load3(i * _StagingVertexStride));

		default:
			return 0;
	}
}

float3 LoadStagingPositionPrev(const uint i, const LODBounds lodBoundsPrev)
{
	const float4 stagingPivotPrev = float4(lodBoundsPrev.center, lodBoundsPrev.reach);

	switch (_StagingVertexFormat)
	{
		case STAGINGVERTEXFORMAT_COMPRESSED:
			return DecodePosition(asuint(_StagingVertexPrev.Load2(i * _StagingVertexStride)), stagingPivotPrev);

		case STAGINGVERTEXFORMAT_UNCOMPRESSED:
			return asfloat(_StagingVertexPrev.Load3(i * _StagingVertexStride));

		default:
			return 0;
	}
}

void StoreStagingPosition(const uint i, float3 p, const LODBounds lodBounds)
{
#if HAIRSIM_WRITEABLE_SOLVERDATA
	const float4 stagingPivot = float4(lodBounds.center, lodBounds.reach);

	switch (_StagingVertexFormat)
	{
		case STAGINGVERTEXFORMAT_COMPRESSED:
			_StagingVertex.Store2(i * _StagingVertexStride, EncodePosition(p, stagingPivot));
			break;

		case STAGINGVERTEXFORMAT_UNCOMPRESSED:
			_StagingVertex.Store3(i * _StagingVertexStride, asuint(p));
			break;
	}
#endif
}

void ResetStagingPositionPrev(const uint i)
{
#if HAIRSIM_WRITEABLE_SOLVERDATA
	switch (_StagingVertexFormat)
	{
		case STAGINGVERTEXFORMAT_COMPRESSED:
			_StagingVertexPrev.Store2(i * _StagingVertexStride, _StagingVertex.Load2(i * _StagingVertexStride));
			break;

		case STAGINGVERTEXFORMAT_UNCOMPRESSED:
			_StagingVertexPrev.Store3(i * _StagingVertexStride, _StagingVertex.Load3(i * _StagingVertexStride));
			break;
	}
#endif
}

#endif//__HAIRSIMDATA_HLSL__
