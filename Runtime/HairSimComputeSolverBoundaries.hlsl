#ifndef __HAIRSIMCOMPUTEBOUNDARIES_HLSL__
#define __HAIRSIMCOMPUTEBOUNDARIES_HLSL__

#include "HairSimData.hlsl"

//-----------------
// boundary shapes

float SdCapsule(const float3 p, const float3 centerA, const float3 centerB, const float radius)
{
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float3 pa = p - centerA;
	const float3 ba = centerB - centerA;

	const float h = saturate(dot(pa, ba) / dot(ba, ba));
	const float r = radius;

	return (length(pa - ba * h) - r);
}

float SdSphere(const float3 p, const float3 center, const float radius)
{
	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float3 a = center;
	const float r = radius;

	return (length(a - p) - r);
}

float SdTorus(float3 p, const float3 center, const float3 axis, const float radiusA, const float radiusB)
{
	const float3 basisX = (axis.y > 1.0 - 1e-4) ? float3(1.0, 0.0, 0.0) : normalize(cross(axis, float3(0.0, 1.0, 0.0)));
	const float3 basisY = axis;
	const float3 basisZ = cross(basisX, axis);
	const float3x3 invM = float3x3(basisX, basisY, basisZ);

	p = mul(invM, p - center);

	// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

	const float2 t = float2(radiusA, radiusB);
	const float2 q = float2(length(p.xz) - t.x, p.y);

	return length(q) - t.y;
}

float SdCapsule(const float3 p, const BoundaryPack capsule)
{
	return SdCapsule(p, capsule.pA, capsule.pB, capsule.tA);
}

float SdSphere(const float3 p, const BoundaryPack sphere)
{
	return SdSphere(p, sphere.pA, sphere.tA);
}

float SdTorus(const float3 p, const BoundaryPack torus)
{
	return SdTorus(p, torus.pA, torus.pB, torus.tA, torus.tB);
}

//------------------
// boundary queries

float BoundaryDistance(const float3 p)
{
	float d = 1e+7;

#if 0
	for (uint i = 0; i != _BoundaryCapsuleCount; i++)
	{
		BoundaryCapsule capsule = _BoundaryCapsule[i];
		d = min(d, SdCapsule(p, capsule.centerA, capsule.centerB, capsule.radius));
	}

	for (uint i = 0; i != _BoundarySphereCount; i++)
	{
		BoundarySphere sphere = _BoundarySphere[i];
		d = min(d, SdSphere(p, sphere.center, sphere.radius));
	}

	for (uint i = 0; i != _BoundaryTorusCount; i++)
	{
		BoundaryTorus torus = _BoundaryTorus[i];
		d = min(d, SdTorus(p, torus.center, torus.axis, torus.radiusA, torus.radiusB));
	}
#else
	uint i = 0;
	uint j = 0;

	for (j += _BoundaryCapsuleCount; i < j; i++)
	{
		d = min(d, SdCapsule(p, _BoundaryPack[i]));
	}

	for (j += _BoundarySphereCount; i < j; i++)
	{
		d = min(d, SdSphere(p, _BoundaryPack[i]));
	}

	for (j += _BoundaryTorusCount; i < j; i++)
	{
		d = min(d, SdTorus(p, _BoundaryPack[i]));
	}
#endif

	return d;
}

uint BoundarySelect(const float3 p, const float d)
{
	uint index = 0;

	uint i = 0;
	uint j = 0;

	for (j += _BoundaryCapsuleCount; i < j; i++)
	{
		if (d == SdCapsule(p, _BoundaryPack[i]))
			index = i;
	}

	for (j += _BoundarySphereCount; i < j; i++)
	{
		if (d == SdSphere(p, _BoundaryPack[i]))
			index = i;
	}

	for (j += _BoundaryTorusCount; i < j; i++)
	{
		if (d == SdTorus(p, _BoundaryPack[i]))
			index = i;
	}

	return index;
}

float3 BoundaryNormal(const float3 p, const float d)
{
	const float2 h = float2(1e-4, 0.0);
	return normalize(float3(
		BoundaryDistance(p - h.xyy) - d,
		BoundaryDistance(p - h.yxy) - d,
		BoundaryDistance(p - h.yyx) - d
		));
}

#endif//__HAIRSIMCOMPUTEBOUNDARIES_HLSL__
