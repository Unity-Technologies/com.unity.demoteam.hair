#ifndef __HAIRSIMCOMPUTEBOUNDARIES_HLSL__
#define __HAIRSIMCOMPUTEBOUNDARIES_HLSL__

#include "HairSimData.hlsl"
#include "HairSimComputeVolumeUtility.hlsl"

//-----------------
// boundary shapes

float SdDiscrete(const float3 p, const float4x4 invM, Texture3D<float> sdf)
{
	float3 uvw = mul(invM, float4(p, 1.0)).xyz;
	return VolumeSampleScalar(sdf, uvw, _Volume_trilinear_clamp);
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

float SdCube(float3 p, const float4x4 invM)
{
	p = mul(invM, float4(p, 1.0)).xyz;

	// see: "distance functions" by Inigo Quilez
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float3 b = float3(0.5, 0.5, 0.5);
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

//------------------
// boundary queries

float BoundaryDistance(const float3 p)
{
	float d = 1e+7;

	uint i = 0;
	uint j = 0;

	for (j += _BoundaryCountDiscrete; i != j; i++)
	{
		d = min(d, SdDiscrete(p, _BoundaryMatrixInv[i], _BoundarySDF));
	}

	for (j += _BoundaryCountCapsule; i != j; i++)
	{
		d = min(d, SdCapsule(p, _BoundaryShape[i]));
	}

	for (j += _BoundaryCountSphere; i != j; i++)
	{
		d = min(d, SdSphere(p, _BoundaryShape[i]));
	}

	for (j += _BoundaryCountTorus; i != j; i++)
	{
		d = min(d, SdTorus(p, _BoundaryShape[i]));
	}

	for (j += _BoundaryCountCube; i != j; i++)
	{
		d = min(d, SdCube(p, _BoundaryMatrixInv[i]));
	}

	return d;
}

uint BoundarySelect(const float3 p, const float d)
{
	uint index = 0;

	uint i = 0;
	uint j = 0;

	for (j += _BoundaryCountDiscrete; i != j; i++)
	{
		if (d == SdDiscrete(p, _BoundaryMatrixInv[i], _BoundarySDF))
			index = i;
	}

	for (j += _BoundaryCountCapsule; i != j; i++)
	{
		if (d == SdCapsule(p, _BoundaryShape[i]))
			index = i;
	}

	for (j += _BoundaryCountSphere; i != j; i++)
	{
		if (d == SdSphere(p, _BoundaryShape[i]))
			index = i;
	}

	for (j += _BoundaryCountTorus; i != j; i++)
	{
		if (d == SdTorus(p, _BoundaryShape[i]))
			index = i;
	}

	for (j += _BoundaryCountCube; i != j; i++)
	{
		if (d == SdCube(p, _BoundaryMatrixInv[i]))
			index = i;
	}

	return index;
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
