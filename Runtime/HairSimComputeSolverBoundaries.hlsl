#ifndef __HAIRSIMCOMPUTEBOUNDARIES_HLSL__
#define __HAIRSIMCOMPUTEBOUNDARIES_HLSL__

#include "HairSimData.hlsl"
#include "HairSimComputeSolverQuaternion.hlsl"

#define BOUNDARIES_OPT_HINT_LOOP 1
#define BOUNDARIES_OPT_PACK_CUBE 0
#if ALLOW_GROUPSHARED_BOUNDARY_DATA
#define BOUNDARIES_OPT_GROUP_MEM 0// requires call to PrepareBoundaryData(threadIndex, threadCount)
#endif

//-----------------
// boundary shapes

float SdDiscrete(const float3 p, const float scale, const float3x4 invM, Texture3D<float> sdf)
{
	float3 uvw = mul(invM, float4(p, 1.0));
	return scale * sdf.SampleLevel(_Volume_trilinear_clamp, uvw, 0);
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

float4 SdgCapsule(const float3 p, const float3 centerA, const float3 centerB, const float radius)
{
	// SdCapsule extended to include gradient in .yzw

	const float3 pa = p - centerA;
	const float3 ba = centerB - centerA;

	const float h = saturate(dot(pa, ba) / dot(ba, ba));
	const float r = radius;

	const float3 v = pa - ba * h;
	const float mv = length(v);

	return float4(mv - r, v / mv);
}

float SdSphere(const float3 p, const float3 center, const float radius)
{
	// see: "distance functions" by Inigo Quilez
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	return (length(p - center) - radius);
}

float4 SdgSphere(const float3 p, const float3 center, const float radius)
{
	// SdSphere extended to include gradient in .yzw

	const float3 v = p - center;
	const float mv = length(v);

	return float4(mv - radius, v / mv);
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

float4 SdgTorus(float3 p, const float3 center, const float3 axis, const float radiusA, const float radiusB)
{
	// SdTorus extended to include gradient in .yzw

	const float3 basisX = (abs(axis.y) > 1.0 - 1e-4) ? float3(1.0, 0.0, 0.0) : normalize(cross(axis, float3(0.0, 1.0, 0.0)));
	const float3 basisY = axis;
	const float3 basisZ = cross(basisX, axis);
	const float3x3 invM = float3x3(basisX, basisY, basisZ);

	p = mul(invM, p - center);

	const float2 t = float2(radiusA, radiusB);
	const float da = length(p.xz);
	const float2 q = float2(da - t.x, p.y);
	const float mq = length(q);

	float2 na = p.xz / da;
	float3 nq = float3(q.x * na.x, q.y, q.x * na.y) / mq;

	return float4(mq - t.y, nq);
}

#if BOUNDARIES_OPT_PACK_CUBE
float SdCube(float3 p, const float3 center, const float3 extent, const uint2 rotf16)
#else
float SdCube(float3 p, const float3 extent, const float3x4 invM)
#endif
{
#if BOUNDARIES_OPT_PACK_CUBE
	p = QMul(QDecode16(rotf16), p - center);
#else
	p = mul(invM, float4(p, 1.0));
	// assuming TRS, can apply scale post-transform to preserve primitive scale
	// T R S x_local = x_world
	//       x_local = S^-1 R^-1 T^-1 x_world
	//     S x_local = S S^-1 R^-1 T^-1 world
	p *= 2.0 * extent;
#endif

	// see: "distance functions" by Inigo Quilez
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float3 b = extent;
	const float3 q = abs(p) - b;

	return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

#if BOUNDARIES_OPT_PACK_CUBE
float4 SdgCube(float3 p, const float3 center, const float3 extent, const uint2 rotf16)
#else
float4 SdgCube(float3 p, const float3 extent, const float3x4 invM)
#endif
{
	// SdCube extended to include gradient in .yzw

#if BOUNDARIES_OPT_PACK_CUBE
	p = QMul(QDecode16(rotf16), p - center);
#else
	p = mul(invM, float4(p, 1.0));
	// assuming TRS, can apply scale post-transform to preserve primitive scale
	// T R S x_local = x_world
	//       x_local = S^-1 R^-1 T^-1 x_world
	//     S x_local = S S^-1 R^-1 T^-1 world
	p *= 2.0 * extent;
#endif

	const float3 b = extent;
	const float3 q = abs(p) - b;
	const float3 s = sign(p);

	float3 v = max(q, 0.0);
	float kv = dot(v, v);
	if (kv > 0.0)
	{
		float mv = sqrt(kv);
		return float4(mv, s * v / mv);
	}
	else
	{
		if (q.x > q.y && q.x > q.z)
			return float4(q.x, s.x, 0.0, 0.0);
		else if (q.y > q.z)
			return float4(q.y, 0.0, s.y, 0.0);
		else
			return float4(q.z, 0.0, 0.0, s.z);
	}
}

float SdDiscrete(const float3 p, const BoundaryShape discrete, const float3x4 invM, Texture3D<float> sdf)
{
	return SdDiscrete(p, discrete.tA, invM, sdf);
}

float SdCapsule(const float3 p, const BoundaryShape capsule)
{
	return SdCapsule(p, capsule.pA, capsule.pB, capsule.tA);
}

float4 SdgCapsule(const float3 p, const BoundaryShape capsule)
{
	return SdgCapsule(p, capsule.pA, capsule.pB, capsule.tA);
}

float SdSphere(const float3 p, const BoundaryShape sphere)
{
	return SdSphere(p, sphere.pA, sphere.tA);
}

float4 SdgSphere(const float3 p, const BoundaryShape sphere)
{
	return SdgSphere(p, sphere.pA, sphere.tA);
}

float SdTorus(const float3 p, const BoundaryShape torus)
{
	return SdTorus(p, torus.pA, torus.pB, torus.tA, torus.tB);
}

float4 SdgTorus(const float3 p, const BoundaryShape torus)
{
	return SdgTorus(p, torus.pA, torus.pB, torus.tA, torus.tB);
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

#if BOUNDARIES_OPT_PACK_CUBE
float4 SdgCube(const float3 p, const BoundaryShape cube)
{
	return SdgCube(p, cube.pA, cube.pB, asuint(float2(cube.tA, cube.tB)));
}
#else
float4 SdgCube(const float3 p, const BoundaryShape cube, const float3x4 invM)
{
	return SdgCube(p, cube.pB, invM);
}
#endif

//--------------------
// boundary accessors

#define __GetBoundaryShape(i) _BoundaryShape[i]
#define __GetBoundaryMatrix(i) (float3x4)_BoundaryMatrix[i]
#define __GetBoundaryMatrixInv(i) (float3x4)_BoundaryMatrixInv[i]
#define __GetBoundaryMatrixInvStep(i) (float3x4)_BoundaryMatrixInvStep[i]

#if BOUNDARIES_OPT_GROUP_MEM

groupshared BoundaryShape gs_boundaryShape[MAX_BOUNDARIES];
groupshared float3x4 gs_boundaryMatrix[MAX_BOUNDARIES];
groupshared float3x4 gs_boundaryMatrixInv[MAX_BOUNDARIES];
groupshared float3x4 gs_boundaryMatrixW2PrevW[MAX_BOUNDARIES];

#define GetBoundaryShape(i) gs_boundaryShape[i]
#define GetBoundaryMatrix(i) gs_boundaryMatrix[i]
#define GetBoundaryMatrixInv(i) gs_boundaryMatrixInv[i]
#define GetBoundaryMatrixInvStep(i) gs_boundaryMatrixInvStep[i]

void PrepareBoundaryData(const uint threadIndex, const uint threadCount)
{
	if (threadIndex == 0)
	{
		for (int i = 0; i != MAX_BOUNDARIES; i++)
		{
			gs_boundaryShape[i] = __GetBoundaryShape(i);
			gs_boundaryMatrix[i] = __GetBoundaryMatrix(i);
			gs_boundaryMatrixInv[i] = __GetBoundaryMatrixInv(i);
			gs_boundaryMatrixInvStep[i] = __GetBoundaryMatrixInvStep(i);
		}
	}

	GroupMemoryBarrierWithGroupSync();
}

#else

#define GetBoundaryShape(i) __GetBoundaryShape(i)
#define GetBoundaryMatrix(i) __GetBoundaryMatrix(i)
#define GetBoundaryMatrixInv(i) __GetBoundaryMatrixInv(i)
#define GetBoundaryMatrixInvStep(i) __GetBoundaryMatrixInvStep(i)

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
		d = min(d, SdDiscrete(p, GetBoundaryShape(i), GetBoundaryMatrixInv(i), _BoundarySDF));
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

float3 BoundaryDistanceDiscrete(const float3 p)
{
	float d = 1e+7;

	BOUNDARIES_FOR_VARS(i, 0)
	BOUNDARIES_FOR(i, _BoundaryDelimDiscrete)
	{
		d = min(d, SdDiscrete(p, GetBoundaryShape(i), GetBoundaryMatrixInv(i), _BoundarySDF));
	}

	return d;
}

uint BoundarySelect(const float3 p, const float d)
{
	uint k = 0;

	BOUNDARIES_FOR_VARS(i, 0)
	BOUNDARIES_FOR(i, _BoundaryDelimDiscrete)
	{
		if (d == SdDiscrete(p, GetBoundaryShape(i), GetBoundaryMatrixInv(i), _BoundarySDF))
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

float4 BoundaryNormalAnalyticMin(const float4 sdgA, const float4 sdgB)
{
	if (sdgA.x < sdgB.x)
		return sdgA;
	else
		return sdgB;
}

float3 BoundaryNormalAnalytic(const float3 p)
{
	float4 sdg = float4(1e+7, 0.0, 0.0, 0.0);

	BOUNDARIES_FOR_VARS(i, _BoundaryDelimDiscrete)
	BOUNDARIES_FOR(i, _BoundaryDelimCapsule)
	{
		sdg = BoundaryNormalAnalyticMin(sdg, SdgCapsule(p, GetBoundaryShape(i)));
	}
	BOUNDARIES_FOR(i, _BoundaryDelimSphere)
	{
		sdg = BoundaryNormalAnalyticMin(sdg, SdSphere(p, GetBoundaryShape(i)));
	}
	BOUNDARIES_FOR(i, _BoundaryDelimTorus)
	{
		sdg = BoundaryNormalAnalyticMin(sdg, SdTorus(p, GetBoundaryShape(i)));
	}
	BOUNDARIES_FOR(i, _BoundaryDelimCube)
	{
#if BOUNDARIES_OPT_PACK_CUBE
		sdg = BoundaryNormalAnalyticMin(sdg, SdCube(p, GetBoundaryShape(i)));
#else
		sdg = BoundaryNormalAnalyticMin(sdg, SdCube(p, GetBoundaryShape(i), GetBoundaryMatrixInv(i)));
#endif
	}

	return sdg.yzw;
}

float3 BoundaryNormal(const float3 p, const float d)
{
#if 0

	return BoundaryNormalAnalytic(p);

#else

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

#endif
}

#endif//__HAIRSIMCOMPUTEBOUNDARIES_HLSL__
