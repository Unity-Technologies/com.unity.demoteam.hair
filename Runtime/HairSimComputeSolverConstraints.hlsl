#ifndef __HAIRSIMCOMPUTECONSTRAINTS_HLSL__
#define __HAIRSIMCOMPUTECONSTRAINTS_HLSL__

// Constraints between particles with infinite mass may exhibit division by zero.
// Ideally, the application should not evaluate such constraints, as checking for
// division by zero incurs an additional cost. For generic applications where the
// mass of the particles is not known in advance, one can enable division by zero
// checks by defining CONSTRAINTS_GUARD_DIVISION_BY_ZERO before including.
//
// E.g.:
//   #define CONSTRAINTS_GUARD_DIVISION_BY_ZERO
//   #include "HairSimComputeSolverConstraints.hlsl"

#ifdef CONSTRAINTS_GUARD_DIVISION_BY_ZERO
#define GUARD(x) if (x)
#else
#define GUARD(x)
#endif

#include "HairSimComputeSolverBoundaries.hlsl"
#include "HairSimComputeSolverQuaternion.hlsl"

//----------------------
// constraint functions

void SolveCollisionConstraint(
	const float w,
	const float3 p,
	inout float3 d)
{
	//  - - -- -- ----+
	//                |
	//                |
	//             d  |
	//           .---.|
	//          p----->
	//                |
	//                :
	//                .

	//         .
	//           `.
	//          d  \
	//        .---. .
	//       p----->|
	//              `
	//             /
	//           .`
	//         ´

	float depth = BoundaryDistance(p);
	if (depth < 0.0)
	{
		float3 normal = BoundaryNormal(p, depth);

		GUARD(w > 0.0)
		{
			d += normal * depth;
		}
	}
}

void SolveCollisionFrictionConstraint(
	const float friction,
	const float3 x0,
	const float w,
	const float3 p,
	inout float3 d)
{
	// see: "Unified Particle Physics for Real-Time Applications"
	// https://mmacklin.com/uppfrta_preprint.pdf
	//
	//                          x0
	//                         '
	//                d_tan   '
	//               .-----. '
	// - -- ------- x* ---> x ----- -- -
	//            .`|      /
	//            | |     /
	//            | |    /
	//      d_nrm | |   / d
	//            | |  /
	//            | | /
	//            '.|/
	//              p

	float depth = BoundaryDistance(p);
	if (depth < 0.0)
	{
		uint index = BoundarySelect(p, depth);
		float3 normal = BoundaryNormal(p, depth);

		//const float4x4 M_prev = mul(_BoundaryMatrixPrev[contact.index], _BoundaryMatrixInv[contact.index]);
		const float4x4 M_prev = _BoundaryMatrixW2PrevW[index];

		const float3 x_star = p + normal * depth;
		const float3 x_delta = (x_star - x0) - (x_star - mul(M_prev, float4(x_star, 1.0)).xyz);
		const float3 x_delta_tan = x_delta - dot(x_delta, normal) * normal;

		const float norm2_delta_tan = dot(x_delta_tan, x_delta_tan);

		const float muS = friction;// for now just using the same constant here
		const float muK = friction;// ...

		GUARD(w > 0.0)
		{
			d += normal * depth;

			if (norm2_delta_tan < muS * muS * depth * depth)
				d -= x_delta_tan;
			else
				d -= x_delta_tan * min(-muK * depth * rsqrt(norm2_delta_tan), 1);
		}
	}
}

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

	float delta = 1.0 - (distance * rd_inv);
	float W_inv = (delta * stiffness) / (w0 + w1);

	GUARD(W_inv > 0.0)
	{
		d0 += (w0 * W_inv) * r;
		d1 -= (w1 * W_inv) * r;
	}
}

void SolveDistanceMinConstraint(
	const float distanceMin, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	// variation of SolveDistanceConstraint(...),
	// ensures distance p0-p1 >= distanceMin

	float3 r = p1 - p0;
	float rd_inv = rsqrt(dot(r, r));

	float delta = 1.0 - max(1.0, distanceMin * rd_inv);// === if (rd < distanceMin) { delta = 1.0 - distanceMin / rd; }
	float W_inv = (delta * stiffness) / (w0 + w1);

	GUARD(W_inv > 0.0)
	{
		d0 += (w0 * W_inv) * r;
		d1 -= (w1 * W_inv) * r;
	}
}

void SolveDistanceMaxConstraint(
	const float distanceMax, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	// variation of SolveDistanceConstraint(...),
	// ensures distance p0-p1 <= distanceMax

	float3 r = p1 - p0;
	float rd_inv = rsqrt(dot(r, r));

	float delta = 1.0 - min(1.0, distanceMax * rd_inv);// === if (rd > distanceMax) { delta = 1.0 - distanceMax / rd; }
	float W_inv = (delta * stiffness) / (w0 + w1);

	GUARD(W_inv > 0.0)
	{
		d0 += (w0 * W_inv) * r;
		d1 -= (w1 * W_inv) * r;
	}
}

void SolveDistanceLRAConstraint(
	const float distanceMax,
	const float3 p0, const float3 p1,
	inout float3 d1)
{
	// see: "Long Range Attachments - A Method to Simulate Inextensible Clothing in Computer Games"
	// https://matthias-research.github.io/pages/publications/sca2012cloth.pdf
	//
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

void SolveDistanceFTLConstraint(
	const float distance,
	const float3 p0, const float3 p1,
	inout float3 d1)
{
	// see: "Fast Simulation of Inextensible Hair and Fur"
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
	// see: "A Triangle Bending Constraint Model for Position-Based Dynamics"
	// http://image.diku.dk/kenny/download/kelager.niebe.ea10.pdf
	//
	//                 p1
	//                  o
	//                ,´|`.
	//              ,´  |  `.
	//            ,´    | r  `.
	//    .     ,´      |      `.     .
	//    |   ,´        o C      `.   | r/2
	//    | ,´                     `. |
	// p0 o´-------------------------`o p2

	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt(dot(r, r));

	float delta = 1.0 - radius * rd_inv;
	float W_inv = (2.0 * delta * stiffness) / (w0 + 2.0 * w1 + w2);

	GUARD(W_inv > 0.0)
	{
		d0 += (w0 * W_inv) * r;
		d1 -= (w1 * W_inv * 2.0) * r;
		d2 += (w2 * W_inv) * r;
	}
}

void SolveTriangleBendingMinConstraint(
	const float radiusMin, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	// variation of SolveTriangleBendingConstraint(...),
	// ensures triangle bending radius >= radiusMin

	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt(dot(r, r));

	float delta = 1.0 - max(1.0, radiusMin * rd_inv);// === if (rd < radiusMin) { delta = 1.0 - radiusMin / rd; }
	float W_inv = (2.0 * delta * stiffness) / (w0 + 2.0 * w1 + w2);

	GUARD(W_inv > 0.0)
	{
		d0 += (w0 * W_inv) * r;
		d1 -= (w1 * W_inv * 2.0) * r;
		d2 += (w2 * W_inv) * r;
	}
}

void SolveTriangleBendingMaxConstraint(
	const float radiusMax, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	// variation of SolveTriangleBendingConstraint(...),
	// ensures triangle bending radius <= radiusMax

	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt(dot(r, r));

	float delta = 1.0 - min(1.0, radiusMax * rd_inv);// === if (rd > radiusMax) { delta = 1.0 - radiusMax / rd; }
	float W_inv = (2.0 * delta * stiffness) / (w0 + 2.0 * w1 + w2);

	GUARD(W_inv > 0.0)
	{
		d0 += (w0 * W_inv) * r;
		d1 -= (w1 * W_inv * 2.0) * r;
		d2 += (w2 * W_inv) * r;
	}
}

void SolveMaterialFrameBendTwistConstraint(
	const float4 darboux0, const float stiffness,
	const float w0, const float w1,
	const float4 q0, const float4 q1,
	inout float4 d0, inout float4 d1)
{
	// see: "Position and Orientation Based Cosserat Rods" by T. Kugelstadt and E. Schömer
	// https://www.cg.informatik.uni-mainz.de/files/2016/06/Position-and-Orientation-Based-Cosserat-Rods.pdf
	
	// calc relative rotation from q0 to q1
	float4 darboux = QMul(QInverse(q0), q1);
	
	// apply eq. 32 + 33 to pick closest delta
	float4 delta_add = darboux + darboux0;
	float4 delta_sub = darboux - darboux0;
	float sqnorm_add = dot(delta_add, delta_add);
	float sqnorm_sub = dot(delta_sub, delta_sub);
	float4 delta = (sqnorm_add < sqnorm_sub) ? delta_add : delta_sub;

	// apply eq. 40 to compute the corrections
	float W_inv = stiffness / (w0 + w1);
	GUARD(W_inv > 0.0)
	{
		delta.w = 0.0;// scalar part
		d0 += (w0 * W_inv) * QMul(q1, delta);
		d1 -= (w1 * W_inv) * QMul(q0, delta);
	}
}

void SolveMaterialFrameTangentConstraint(
	const float4 frame, const float3 tangent, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
#if 0
	float3 delta = p1 - (p0 + QMul(frame, tangent));
#else
	float3 mid = 0.5 * (p1 + p0);
	float3 mid_plus = mid + 0.5 * QMul(frame, tangent);
	float3 delta = p1 - mid_plus;
#endif

	float W_inv = stiffness / (w0 + w1);
	GUARD(W_inv > 0.0)
	{
		d0 += (w0 * W_inv) * delta;
		d1 -= (w1 * W_inv) * delta;
	}
}

//--------------------------------------------------
// constraint shortcuts: weight in fourth component

void SolveCollisionConstraint(
	const float4 p,
	inout float3 d)
{
	SolveCollisionConstraint(p.w, p.xyz, d);
}

void SolveCollisionFrictionConstraint(
	const float friction,
	const float3 x0,
	const float4 p,
	inout float3 d)
{
	SolveCollisionFrictionConstraint(friction, x0, p.w, p.xyz, d);
}

void SolveDistanceConstraint(
	const float distance, const float stiffness,
	const float4 p0, const float4 p1,
	inout float3 d0, inout float3 d1)
{
	SolveDistanceConstraint(distance, stiffness, p0.w, p1.w, p0.xyz, p1.xyz, d0, d1);
}

void SolveDistanceMinConstraint(
	const float distanceMin, const float stiffness,
	const float4 p0, const float4 p1,
	inout float3 d0, inout float3 d1)
{
	SolveDistanceMinConstraint(distanceMin, stiffness, p0.w, p1.w, p0.xyz, p1.xyz, d0, d1);
}

void SolveDistanceMaxConstraint(
	const float distanceMax, const float stiffness,
	const float4 p0, const float4 p1,
	inout float3 d0, inout float3 d1)
{
	SolveDistanceMaxConstraint(distanceMax, stiffness, p0.w, p1.w, p0.xyz, p1.xyz, d0, d1);
}

void SolveTriangleBendingConstraint(
	const float radius, const float stiffness,
	const float4 p0, const float4 p1, const float4 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	SolveTriangleBendingConstraint(radius, stiffness, p0.w, p1.w, p2.w, p0.xyz, p1.xyz, p2.xyz, d0, d1, d2);
}

void SolveTriangleBendingMinConstraint(
	const float radiusMin, const float stiffness,
	const float4 p0, const float4 p1, const float4 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	SolveTriangleBendingMinConstraint(radiusMin, stiffness, p0.w, p1.w, p2.w, p0.xyz, p1.xyz, p2.xyz, d0, d1, d2);
}

void SolveTriangleBendingMaxConstraint(
	const float radiusMax, const float stiffness,
	const float4 p0, const float4 p1, const float4 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	SolveTriangleBendingMaxConstraint(radiusMax, stiffness, p0.w, p1.w, p2.w, p0.xyz, p1.xyz, p2.xyz, d0, d1, d2);
}

//------------------------------------------------------------
// constraint shortcuts: apply directly to position variables

void ApplyCollisionConstraint(inout float3 p)
{
	float3 d = 0.0;
	SolveCollisionConstraint(1.0, p, d);
	p += d;
}

void ApplyCollisionFrictionConstraint(const float friction, const float3 x0, inout float3 p)
{
	float3 d = 0.0;
	SolveCollisionFrictionConstraint(friction, x0, 1.0, p, d);
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

void ApplyDistanceFTLConstraint(const float distance, const float3 p0, inout float3 p1, inout float3 d1)
{
	float3 d1_tmp = 0.0;
	SolveDistanceFTLConstraint(distance, p0, p1, d1_tmp);
	p1 += d1_tmp;
	d1 += d1_tmp;
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

void ApplyMaterialFrameBendTwistConstraint(
	const float4 darboux0, const float stiffness,
	const float w0, const float w1,
	inout float4 q0, inout float4 q1)
{
	float4 d0 = 0.0;
	float4 d1 = 0.0;
	SolveMaterialFrameBendTwistConstraint(darboux0, stiffness, w0, w1, q0, q1, d0, d1);
	q0 = normalize(q0 + d0);
	q1 = normalize(q1 + d1);
}

void ApplyMaterialFrameTangentConstraint(
	const float4 frame, const float3 tangent, const float stiffness,
	const float w0, const float w1,
	inout float3 p0, inout float3 p1)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	SolveMaterialFrameTangentConstraint(frame, tangent, stiffness, w0, w1, p0, p1, d0, d1);
	p0 += d0;
	p1 += d1;
}

#endif//__HAIRSIMCOMPUTECONSTRAINTS_HLSL__
