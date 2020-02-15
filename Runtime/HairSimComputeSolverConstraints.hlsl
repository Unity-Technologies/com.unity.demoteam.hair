//-------------------
// solve constraints

void SolveCollisionConstraint(
	const float3 p,
	inout float3 d)
{
	//  - - -- -- ----+
	//                |
	//             d  |
	//           .---.|
	//          p----->
	//                |
	//                :
	//                .

	float4 P = BoundaryNormalDistance(p);
	if (P.w < 0.0)
	{
		d += P.xyz * P.w;
	}
}

/*
void SolveCollisionFrictionConstraint(
	const float friction,
	const float3 x,
	const float3 p,
	inout float3 d)
{
	//TODO

	float4 P = BoundaryNormalDistance(p);
	if (P.w < 0.0)
	{
		d += P.xyz * P.w;
	}
}
*/

void SolveDistanceConstraint(
	const float distance, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	//      d0                      d1
	//    .----.                  .----.
	// p0 ------><--------------><------ p1
	//           \______________/
	//               distance

	float3 r = p1 - p0;
	float rd_inv = rsqrt(dot(r, r));

	r *= 1.0 - (distance * rd_inv);

	d0 += (stiffness * w0) * r;
	d1 -= (stiffness * w1) * r;
}

void SolveDistanceConstraintTEST(
	const float distance, const float stiffness,
	const float4 p0, const float4 p1,
	inout float3 d0, inout float3 d1)
{
	//      d0                      d1
	//    .----.                  .----.
	// p0 ------><--------------><------ p1
	//           \______________/
	//               distance

	float W_inv = 1.0 / (p0.w + p1.w);
	//if (W_inv > 0.0)
	{
		float3 r = p1.xyz - p0.xyz;
		float rd_inv = rsqrt(dot(r, r));

		r *= 1.0 - (distance * rd_inv);

		d0 += (stiffness * p0.w * W_inv) * r;
		d1 -= (stiffness * p1.w * W_inv) * r;
	}
}

void SolveDistanceMinConstraint(
	const float distanceMin, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	float3 r = p1 - p0;
	float rd_inv = rsqrt(dot(r, r));

	// if (rd < distanceMin)
	// { ... 1.0 - distanceMin / rd }
	//
	// =>
	// { ... 1.0 - max(1.0, distanceMin / rd) }

	r *= 1.0 - max(1.0, distanceMin * rd_inv);

	d0 += (stiffness * w0) * r;
	d1 -= (stiffness * w1) * r;
}

void SolveDistanceMaxConstraint(
	const float distanceMax, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	float3 r = p1 - p0;
	float rd_inv = rsqrt(dot(r, r));

	// if (rd > distanceMax)
	// { ... 1.0 - distanceMax / rd }
	//
	// =>
	// { ... 1.0 - min(1.0, distanceMax / rd) }

	r *= 1.0 - min(1.0, distanceMax * rd_inv);

	d0 += (stiffness * w0) * r;
	d1 -= (stiffness * w1) * r;
}

void SolveDistanceLRAConstraint(const float distanceMax, const float3 p0, const float3 p1, inout float3 d1)
{
	//                        d1
	//                      .----.
	// p0 #----------------<------ p1
	//    \_______________/
	//       distanceMax

	float3 r = p1 - p0;
	float rd_inv = rsqrt(dot(r, r));

	r *= 1.0 - min(1.0, distanceMax * rd_inv);

	d1 -= r;
}

void SolveDistanceFTLConstraint(const float distance, const float3 p0, const float3 p1, inout float3 d1)
{
	// Fast Simulation of Inextensible Hair and Fur
	// https://matthias-research.github.io/pages/publications/FTLHairFur.pdf
	//
	//                       d1
	//                     .----.
	// p0 #--------------><------ p1
	//    \______________/
	//        distance

	float3 r = p1 - p0;
	float rd_inv = rsqrt(dot(r, r));

	r *= 1.0 - (distance * rd_inv);

	d1 -= r;
}

void SolveTriangleBendingConstraint(
	const float radius, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	// A Triangle Bending Constraint Model for Position-Based Dynamics
	// http://image.diku.dk/kenny/download/kelager.niebe.ea10.pdf
	//
	//                     p1
	//                 . ´ : ` .
	//            . ´      :      ` .
	// :     . ´           c           ` .    :
	// p0 ´ - - - - - - - - - - - - - - - - ` p2

	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt(dot(r, r));

	float delta = 1.0 - radius * rd_inv;
	float W_inv = 1.0 / (w0 + w1 * 2.0 + w2);

	d0 += (2.0 * w0 * W_inv * delta * stiffness) * r;
	d1 -= (4.0 * w1 * W_inv * delta * stiffness) * r;
	d2 += (2.0 * w2 * W_inv * delta * stiffness) * r;
}

void SolveTriangleBendingConstraintTEST(
	const float radius, const float stiffness,
	const float4 p0, const float4 p1, const float4 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	// A Triangle Bending Constraint Model for Position-Based Dynamics
	// http://image.diku.dk/kenny/download/kelager.niebe.ea10.pdf
	//
	//                     p1
	//                 . ´ : ` .
	//            . ´      :      ` .
	// :     . ´           c           ` .    :
	// p0 ´ - - - - - - - - - - - - - - - - ` p2

	float3 c = (p0.xyz + p1.xyz + p2.xyz) / 3.0;
	float3 r = p1.xyz - c;
	float rd_inv = rsqrt(dot(r, r));

	float delta = 1.0 - radius * rd_inv;
	float W_inv = 1.0 / (p0.w + p1.w*2.0 + p2.w);

	d0 += (2.0 * p0.w * W_inv * delta * stiffness) * r;
	d1 -= (4.0 * p1.w * W_inv * delta * stiffness) * r;
	d2 += (2.0 * p2.w * W_inv * delta * stiffness) * r;
}

void SolveTriangleBendingMinConstraint(
	const float radiusMin, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt(dot(r, r));

	// if (rd > radiusMin)
	// { ... 1.0 - radiusMin / rd }
	//
	// =>
	// { ... 1.0 - max(1.0, radiusMin / rd) }

	float delta = 1.0 - max(1.0, radiusMin * rd_inv);
	float W_inv = 1.0 / (w0 + w1 * 2.0 + w2);

	d0 += (2.0 * w0 * W_inv * delta * stiffness) * r;
	d1 -= (4.0 * w1 * W_inv * delta * stiffness) * r;
	d2 += (2.0 * w2 * W_inv * delta * stiffness) * r;
}

void SolveTriangleBendingMaxConstraint(
	const float radiusMax, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt(dot(r, r));

	// if (rd > radiusMax)
	// { ... 1.0 - radiusMax / rd }
	//
	// =>
	// { ... 1.0 - min(1.0, radiusMax / rd) }

	float delta = 1.0 - min(1.0, radiusMax * rd_inv);
	float W_inv = 1.0 / (w0 + w1 * 2.0 + w2);

	d0 += (2.0 * w0 * W_inv * delta * stiffness) * r;
	d1 -= (4.0 * w1 * W_inv * delta * stiffness) * r;
	d2 += (2.0 * w2 * W_inv * delta * stiffness) * r;
}

//-------------------
// apply constraints

void ApplyCollisionConstraint(inout float3 p)
{
	float3 d = 0.0;
	SolveCollisionConstraint(p, d);
	p += d;
}

void ApplyDistanceConstraint(const float distance, const float stiffness, const float w0, const float w1, inout float3 p0, inout float3 p1)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	SolveDistanceConstraint(distance, stiffness, w0, w1, p0, p1, d0, d1);
	p0 += d0;
	p1 += d1;
}

void ApplyDistanceMinConstraint(const float distanceMin, const float stiffness, const float w0, const float w1, inout float3 p0, inout float3 p1)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	SolveDistanceMinConstraint(distanceMin, stiffness, w0, w1, p0, p1, d0, d1);
	p0 += d0;
	p1 += d1;
}

void ApplyDistanceMaxConstraint(const float distanceMax, const float stiffness, const float w0, const float w1, inout float3 p0, inout float3 p1)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	SolveDistanceMaxConstraint(distanceMax, stiffness, w0, w1, p0, p1, d0, d1);
	p0 += d0;
	p1 += d1;
}

void ApplyDistanceLRAConstraint(const float distanceMax, const float3 p0, inout float3 p1)
{
	float3 d1 = 0.0;
	SolveDistanceLRAConstraint(distanceMax, p0, p1, d1);
	p1 += d1;
}

void ApplyDistanceFTLConstraint(const float distance, const float3 p0, inout float3 p1)
{
	float3 d1 = 0.0;
	SolveDistanceFTLConstraint(distance, p0, p1, d1);
	p1 += d1;
}

void ApplyDistanceFTLConstraint(const float distance, const float3 p0, inout float3 p1, inout float3 d1)
{
	SolveDistanceFTLConstraint(distance, p0, p1, d1);
	p1 += d1;
}

void ApplyTriangleBendingConstraint(
	const float radius, const float stiffness,
	const float w0, const float w1, const float w2,
	inout float3 p0, inout float3 p1, inout float3 p2)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	float3 d2 = 0.0;
	SolveTriangleBendingConstraint(radius, stiffness, w0, w1, w2, p0, p1, p2, d0, d1, d2);
	p0 += d0;
	p1 += d1;
	p2 += d2;
}

void ApplyTriangleBendingMinConstraint(
	const float radiusMin, const float stiffness,
	const float w0, const float w1, const float w2,
	inout float3 p0, inout float3 p1, inout float3 p2)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	float3 d2 = 0.0;
	SolveTriangleBendingMinConstraint(radiusMin, stiffness, w0, w1, w2, p0, p1, p2, d0, d1, d2);
	p0 += d0;
	p1 += d1;
	p2 += d2;
}

void ApplyTriangleBendingMaxConstraint(
	const float radiusMax, const float stiffness,
	const float w0, const float w1, const float w2,
	inout float3 p0, inout float3 p1, inout float3 p2)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	float3 d2 = 0.0;
	SolveTriangleBendingMaxConstraint(radiusMax, stiffness, w0, w1, w2, p0, p1, p2, d0, d1, d2);
	p0 += d0;
	p1 += d1;
	p2 += d2;
}
