#ifndef __HAIRSIMCOMPUTE_BOUNDARIES__
#define __HAIRSIMCOMPUTE_BOUNDARIES__

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

uint _BoundaryCapsuleCount;
uint _BoundarySphereCount;
uint _BoundaryTorusCount;

float BoundaryDistance(const float3 p)
{
	float d = 1e+7;

#if 1
	// capsules
	{
		for (uint i = 0; i != _BoundaryCapsuleCount; i++)
		{
			BoundaryCapsule capsule = _BoundaryCapsule[i];

			float3 pa = p - capsule.centerA;
			float3 ba = capsule.centerB - capsule.centerA;

			float h = saturate(dot(pa, ba) / dot(ba, ba));
			float r = capsule.radius;

			d = min(d, length(pa - ba * h) - r);
		}
	}

	// spheres
	{
		for (uint i = 0; i != _BoundarySphereCount; i++)
		{
			BoundarySphere sphere = _BoundarySphere[i];

			float3 a = sphere.center;
			float r = sphere.radius;

			d = min(d, length(a - p) - r);
		}
	}

	// tori
	{
		for (uint i = 0; i != _BoundaryTorusCount; i++)
		{
			BoundaryTorus torus = _BoundaryTorus[i];
			//TODO
		}
	}
#else
	uint i = 0;
	uint j = 0;

	// capsules
	{
		for (j += _BoundaryCapsuleCount; i < j; i++)
		{
			BoundaryPack capsulePack = _BoundaryPack[i];

			float3 pa = p - capsulePack.pA;
			float3 ba = capsulePack.pB - capsulePack.pA;

			float h = saturate(dot(pa, ba) / dot(ba, ba));
			float r = capsulePack.tA;

			d = min(d, length(pa - ba * h) - r);
		}
	}

	// spheres
	{
		for (j += _BoundarySphereCount; i < j; i++)
		{
			BoundaryPack spherePack = _BoundaryPack[i];

			float3 a = spherePack.pA;
			float r = spherePack.tA;

			d = min(d, length(a - p) - r);
		}
	}

	// tori
	{
		for (j += _BoundaryTorusCount; i < j; i++)
		{
			BoundaryPack torusPack = _BoundaryPack[i];
			//TODO
		}
	}
#endif

	return d;
}

/*
float4 BoundaryDistance4(const float3 p0, const float3 p1, const float3 p2, const float3 p3)
{
	float4 d = 1e+7;

	// capsules
	{
		for (uint i = 0; i != _BoundaryCapsuleCount; i++)
		{
			BoundaryCapsule capsule = _BoundaryCapsule[i];

			float3 pa0 = p0 - capsule.centerA;
			float3 pa1 = p1 - capsule.centerA;
			float3 pa2 = p2 - capsule.centerA;
			float3 pa3 = p3 - capsule.centerA;
			float3 ba = capsule.centerB - capsule.centerA;

			float h0 = saturate(dot(pa0, ba) / dot(ba, ba));
			float h1 = saturate(dot(pa1, ba) / dot(ba, ba));
			float h2 = saturate(dot(pa2, ba) / dot(ba, ba));
			float h3 = saturate(dot(pa3, ba) / dot(ba, ba));
			float r = capsule.radius;

			d.x = min(d.x, length(pa0 - ba * h0) - r);
			d.y = min(d.y, length(pa1 - ba * h1) - r);
			d.z = min(d.z, length(pa2 - ba * h2) - r);
			d.w = min(d.w, length(pa3 - ba * h3) - r);
		}
	}

	// spheres
	{
		for (uint i = 0; i != _BoundarySphereCount; i++)
		{
			BoundarySphere sphere = _BoundarySphere[i];

			float3 a = sphere.center;
			float r = sphere.radius;

			d.x = min(d.x, length(a - p0) - r);
			d.y = min(d.y, length(a - p1) - r);
			d.z = min(d.z, length(a - p2) - r);
			d.w = min(d.w, length(a - p3) - r);
		}
	}

	// tori
	{
		for (uint i = 0; i != _BoundaryTorusCount; i++)
		{
			BoundaryTorus torus = _BoundaryTorus[i];
			//TODO
		}
	}

	return d;
}
//*/

float3 BoundaryNormal(const float3 p, const float d)
{
	const float2 h = float2(1e-4, 0.0);
	return normalize(float3(
		BoundaryDistance(p - h.xyy) - d,
		BoundaryDistance(p - h.yxy) - d,
		BoundaryDistance(p - h.yyx) - d
		));
}

float3 BoundaryNormal(const float3 p)
{
	return BoundaryNormal(p, BoundaryDistance(p));
}

float4 BoundaryNormalDistance(const float3 p)
{
	float d = BoundaryDistance(p);
	return float4(BoundaryNormal(p, d), d);
}

float4 BoundaryContact(const float3 p)
{
	float d = BoundaryDistance(p);
	if (d < 0.0)
		return float4(BoundaryNormal(p, d), d);
	else
		return 0.0;
}

#endif//__HAIRSIMCOMPUTE_BOUNDARIES__
