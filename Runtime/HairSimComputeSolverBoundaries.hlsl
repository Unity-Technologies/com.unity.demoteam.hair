#ifndef __HAIRSIMCOMPUTE_BOUNDARIES__
#define __HAIRSIMCOMPUTE_BOUNDARIES__

//-----------------
// boundary shapes

float SdCapsule(const float3 p, const float3 centerA, const float3 centerB, const float radius)
{
	const float3 pa = p - centerA;
	const float3 ba = centerB - centerA;

	const float h = saturate(dot(pa, ba) / dot(ba, ba));
	const float r = radius;

	return (length(pa - ba * h) - r);
}

float SdSphere(const float3 p, const float3 center, const float radius)
{
	const float3 a = center;
	const float r = radius;

	return (length(a - p) - r);
}

float SdTorus(const float3 p, const float3 center, const float3 axis, const float radiusA, const float radiusB)
{
	return 1e+7;//TODO
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
	// capsules
	{
		for (uint i = 0; i != _BoundaryCapsuleCount; i++)
		{
			BoundaryCapsule capsule = _BoundaryCapsule[i];
			d = min(d, SdCapsule(p, capsule.centerA, capsule.centerB, capsule.radius));
		}
	}

	// spheres
	{
		for (uint i = 0; i != _BoundarySphereCount; i++)
		{
			BoundarySphere sphere = _BoundarySphere[i];
			d = min(d, SdSphere(p, sphere.center, sphere.radius));
		}
	}

	// tori
	{
		for (uint i = 0; i != _BoundaryTorusCount; i++)
		{
			BoundaryTorus torus = _BoundaryTorus[i];
			d = min(d, SdTorus(p, torus.center, torus.axis, torus.radiusA, torus.radiusB));
		}
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

#endif//__HAIRSIMCOMPUTE_BOUNDARIES__


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
