#ifndef __HAIRSIMCOMPUTEBOUNDARIES_HLSL__
#define __HAIRSIMCOMPUTEBOUNDARIES_HLSL__

#include "HairSimData.hlsl"
#include "HairSimComputeSolverQuaternion.hlsl"

#define BOUNDARIES_OPT_HINT_LOOP 1
#define BOUNDARIES_OPT_PACK_CUBE 0
#define BOUNDARIES_OPT_CBUF_DATA 1
#if GROUPSHARED_BOUNDARY_DATA
#define BOUNDARIES_OPT_GROUP_MEM 0// requires call to PrepareBoundaryData(threadIndex, threadCount)
#endif

//-----------------
// boundary shapes

float SdDiscrete(const float3 p, const float3x4 invM, Texture3D<float> sdf)
{
	float3 uvw = mul(invM, float4(p, 1.0));
	return sdf.SampleLevel(_Volume_trilinear_clamp, uvw, 0);
}

float SdCapsule(const float3 p, const float3 centerA, const float3 centerB, const float radius)
{
	// see: "distance functions" by Inigo Quilez
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float3 pa = p - centerA;
	const float3 ba = centerB - centerA;

	const float h = saturate(dot(pa, ba) / dot(ba, ba));
	const float r = radius;

	return (length(pa - ba * h) - r);
}

float SdSphere(const float3 p, const float3 center, const float radius)
{
	// see: "distance functions" by Inigo Quilez
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	return (length(p - center) - radius);
}

float SdTorus(float3 p, const float3 center, const float3 axis, const float radiusA, const float radiusB)
{
	const float3 basisX = (abs(axis.y) > 1.0 - 1e-4) ? float3(1.0, 0.0, 0.0) : normalize(cross(axis, float3(0.0, 1.0, 0.0)));
	const float3 basisY = axis;
	const float3 basisZ = cross(basisX, axis);
	const float3x3 invM = float3x3(basisX, basisY, basisZ);

	p = mul(invM, p - center);

	// see: "distance functions" by Inigo Quilez
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float2 t = float2(radiusA, radiusB);
	const float2 q = float2(length(p.xz) - t.x, p.y);

	return length(q) - t.y;
}

#if BOUNDARIES_OPT_PACK_CUBE
float SdCube(float3 p, const float3 center, const float3 extent, const uint2 rotf16)
#else
float SdCube(float3 p, const float3 extent, const float3x4 invM)
#endif
{
#if BOUNDARIES_OPT_PACK_CUBE
	
	p = QMul(f16tof32(uint4(rotf16, rotf16 >> 16)), p - center);
#else
	p = mul(invM, float4(p, 1.0));
#endif

	// see: "distance functions" by Inigo Quilez
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float3 b = extent;
	const float3 q = abs(p) - b;

	return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float SdCapsule(const float3 p, const BoundaryShape capsule)
{
	return SdCapsule(p, capsule.pA, capsule.pB, capsule.tA);
}

float SdSphere(const float3 p, const BoundaryShape sphere)
{
	return SdSphere(p, sphere.pA, sphere.tA);
}

float SdTorus(const float3 p, const BoundaryShape torus)
{
	return SdTorus(p, torus.pA, torus.pB, torus.tA, torus.tB);
}

#if BOUNDARIES_OPT_PACK_CUBE
float SdCube(const float3 p, const BoundaryShape cube)
{
	return SdCube(p, cube.pA, cube.pB, asuint(float2(cube.tA, cube.tB)));
}
#else
float SdCube(const float3 p, const BoundaryShape cube, const float3x4 invM)
{
	return SdCube(p, cube.pB, invM);
}
#endif

//--------------------
// boundary accessors

#if BOUNDARIES_OPT_CBUF_DATA

BoundaryShape __GetBoundaryShape(const uint i)
{
	BoundaryShape shape = {
		_CB_BoundaryShape[i * 2 + 0],
		_CB_BoundaryShape[i * 2 + 1]
	};
	return shape;
}
float3x4 __GetBoundaryMatrix(const uint i)
{
	return float3x4(
		_CB_BoundaryMatrix[i * 3 + 0],
		_CB_BoundaryMatrix[i * 3 + 1],
		_CB_BoundaryMatrix[i * 3 + 2]);
}
float3x4 __GetBoundaryMatrixInv(const uint i)
{
	return float3x4(
		_CB_BoundaryMatrixInv[i * 3 + 0],
		_CB_BoundaryMatrixInv[i * 3 + 1],
		_CB_BoundaryMatrixInv[i * 3 + 2]);
}
float3x4 __GetBoundaryMatrixW2PrevW(const uint i)
{
	return float3x4(
		_CB_BoundaryMatrixW2PrevW[i * 3 + 0],
		_CB_BoundaryMatrixW2PrevW[i * 3 + 1],
		_CB_BoundaryMatrixW2PrevW[i * 3 + 2]);
}

#else

#define __GetBoundaryShape(i) _BoundaryShape[i]
#define __GetBoundaryMatrix(i) (float3x4)_BoundaryMatrix[i]
#define __GetBoundaryMatrixInv(i) (float3x4)_BoundaryMatrixInv[i]
#define __GetBoundaryMatrixW2PrevW(i) (float3x4)_BoundaryMatrixW2PrevW[i]

#endif

#if BOUNDARIES_OPT_GROUP_MEM

groupshared BoundaryShape gs_boundaryShape[MAX_BOUNDARIES];
groupshared float3x4 gs_boundaryMatrix[MAX_BOUNDARIES];
groupshared float3x4 gs_boundaryMatrixInv[MAX_BOUNDARIES];
groupshared float3x4 gs_boundaryMatrixW2PrevW[MAX_BOUNDARIES];

#define GetBoundaryShape(i) gs_boundaryShape[i]
#define GetBoundaryMatrix(i) gs_boundaryMatrix[i]
#define GetBoundaryMatrixInv(i) gs_boundaryMatrixInv[i]
#define GetBoundaryMatrixW2PrevW(i) gs_boundaryMatrixW2PrevW[i]

void PrepareBoundaryData(const uint threadIndex, const uint threadCount)
{
	if (threadIndex == 0)
	{
		for (int i = 0; i != MAX_BOUNDARIES; i++)
		{
			gs_boundaryShape[i] = __GetBoundaryShape(i);
			gs_boundaryMatrix[i] = __GetBoundaryMatrix(i);
			gs_boundaryMatrixInv[i] = __GetBoundaryMatrixInv(i);
			gs_boundaryMatrixW2PrevW[i] = __GetBoundaryMatrixW2PrevW(i);
		}
	}

	GroupMemoryBarrierWithGroupSync();
}

#else

#define GetBoundaryShape(i) __GetBoundaryShape(i)
#define GetBoundaryMatrix(i) __GetBoundaryMatrix(i)
#define GetBoundaryMatrixInv(i) __GetBoundaryMatrixInv(i)
#define GetBoundaryMatrixW2PrevW(i) __GetBoundaryMatrixW2PrevW(i)

void PrepareBoundaryData(const uint threadIndex, const uint threadCount) { }

#endif

//------------------
// boundary queries

#if BOUNDARIES_OPT_HINT_LOOP
#define BOUNDARIES_LOOP [loop]
#else
#define BOUNDARIES_LOOP
#endif

#if 1
#define BOUNDARIES_FOR_VARS(i, val) uint i = val;
#define BOUNDARIES_FOR(i, n) BOUNDARIES_LOOP for (; i != n; i++)
#else
#define BOUNDARIES_FOR_VARS(i, val) uint i = val; uint j = val;
#define BOUNDARIES_FOR(i, n) BOUNDARIES_LOOP for (j += n; i != j; i++)
#endif

float BoundaryDistance(const float3 p)
{
	float d = 1e+7;

	BOUNDARIES_FOR_VARS(i, 0)
	BOUNDARIES_FOR(i, _BoundaryDelimDiscrete)
	{
		d = min(d, SdDiscrete(p, GetBoundaryMatrixInv(i), _BoundarySDF));
	}
	BOUNDARIES_FOR(i, _BoundaryDelimCapsule)
	{
		d = min(d, SdCapsule(p, GetBoundaryShape(i)));
	}
	BOUNDARIES_FOR(i, _BoundaryDelimSphere)
	{
		d = min(d, SdSphere(p, GetBoundaryShape(i)));
	}
	BOUNDARIES_FOR(i, _BoundaryDelimTorus)
	{
		d = min(d, SdTorus(p, GetBoundaryShape(i)));
	}
	BOUNDARIES_FOR(i, _BoundaryDelimCube)
	{
#if BOUNDARIES_OPT_PACK_CUBE
		d = min(d, SdCube(p, GetBoundaryShape(i)));
#else
		d = min(d, SdCube(p, GetBoundaryShape(i), GetBoundaryMatrixInv(i)));
#endif
	}

	return d;
}

uint BoundarySelect(const float3 p, const float d)
{
	uint k = 0;

	BOUNDARIES_FOR_VARS(i, 0)
	BOUNDARIES_FOR(i, _BoundaryDelimDiscrete)
	{
		if (d == SdDiscrete(p, GetBoundaryMatrixInv(i), _BoundarySDF))
			k = i;
	}
	BOUNDARIES_FOR(i, _BoundaryDelimCapsule)
	{
		if (d == SdCapsule(p, GetBoundaryShape(i)))
			k = i;
	}
	BOUNDARIES_FOR(i, _BoundaryDelimSphere)
	{
		if (d == SdSphere(p, GetBoundaryShape(i)))
			k = i;
	}
	BOUNDARIES_FOR(i, _BoundaryDelimTorus)
	{
		if (d == SdTorus(p, GetBoundaryShape(i)))
			k = i;
	}
	BOUNDARIES_FOR(i, _BoundaryDelimCube)
	{
#if BOUNDARIES_OPT_PACK_CUBE
		if (d == SdCube(p, GetBoundaryShape(i)))
			k = i;
#else
		if (d == SdCube(p, GetBoundaryShape(i), GetBoundaryMatrixInv(i)))
			k = i;
#endif
	}

	return k;
}

float3 BoundaryNormal(const float3 p, const float d)
{
	const float2 h = float2(_BoundaryWorldEpsilon, 0.0);
#if 1
	float3 diff = float3(
		BoundaryDistance(p - h.xyy) - d,
		BoundaryDistance(p - h.yxy) - d,
		BoundaryDistance(p - h.yyx) - d);
	return diff * rsqrt(dot(diff, diff) + 1e-7);
#else
	return normalize(float3(
		BoundaryDistance(p - h.xyy) - d,
		BoundaryDistance(p - h.yxy) - d,
		BoundaryDistance(p - h.yyx) - d
		));
#endif
}

#endif//__HAIRSIMCOMPUTEBOUNDARIES_HLSL__
