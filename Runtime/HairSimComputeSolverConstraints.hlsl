#ifndef __HAIRSIMCOMPUTECONSTRAINTS_HLSL__
#define __HAIRSIMCOMPUTECONSTRAINTS_HLSL__

#include "HairSimComputeSolverBoundaries.hlsl"
#include "HairSimComputeSolverQuaternion.hlsl"

//--------
// macros

#ifndef w_EPSILON
#define w_EPSILON 1e-7
#endif

#ifndef rsqrt_safe// used where NaN would otherwise damage the data
#define rsqrt_safe(x) max(0.0, rsqrt(x))
#endif

#ifndef rsqrt_unsafe// used where NaN is already handled by later operation
#define rsqrt_unsafe(x) rsqrt(x)
#endif

//----------------------
// constraint functions

void SolveCollisionConstraint(
	const float margin,
	const float w,
	const float3 p,
	inout float3 d)
{
	//         .
	//           `.
	//          d  \
	//        .---. .
	//       p----->|
	//              `
	//             /
	//           .`
	//         ´

	if (w > 0.0)
	{
		float depth = BoundaryDistance(p);
		if (depth < (_BoundaryWorldMargin + margin))
		{
			float3 normal = BoundaryNormal(p, depth);

			d += normal * (depth - (_BoundaryWorldMargin + margin));
		}
	}
}

void SolveCollisionFrictionConstraint(
	const float margin,
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

	if (w > 0.0)
	{
		float depth = BoundaryDistance(p);
		if (depth < (_BoundaryWorldMargin + margin))
		{
			uint index = BoundarySelect(p, depth);
			float3 normal = BoundaryNormal(p, depth);

			depth -= (_BoundaryWorldMargin + margin);

			//... float4x4 M_inv_step = mul(_BoundaryMatrixPrev[index], _BoundaryMatrixInv[index]);
			const float3x4 M_inv_step = GetBoundaryMatrixInvStep(index);

			const float3 x_star = p + normal * depth;
			//... float3 x_delta = (x_star - x0) - (x_star - mul(M_inv_step, float4(x_star, 1.0)));
			const float3 x_delta = mul(M_inv_step, float4(x_star, 1.0)) - x0;
			const float3 x_delta_tan = x_delta - dot(x_delta, normal) * normal;

			d += normal * depth;

#define BOUNDARY_FRICTION_AS_CONSTANT_FRACTION 0
#if BOUNDARY_FRICTION_AS_CONSTANT_FRACTION
			d -= x_delta_tan * friction;// always subtract specified fraction of tangential delta, regardless of ratio between depth and tangential delta
#else
			const float norm2_delta_tan = dot(x_delta_tan, x_delta_tan);

			const float muS = friction * 1.7;// for now just using a constant multiplier to ensure that static friction is higher than kinetic friction
			const float muK = friction * 1.0;// ...

			if (norm2_delta_tan < muS * muS * depth * depth)
				d -= x_delta_tan;
			else
				d -= x_delta_tan * min(-muK * depth * rsqrt_unsafe(norm2_delta_tan), 1.0);
#endif
		}
	}
}

void SolveDistanceConstraint(
	const float distance0, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	//      d0                      d1
	//    .----.                  .----.
	// p0 ------><--------------><------ p1
	//           \______________/
	//               distance0

	float3 r = p1 - p0;
	float rd_inv = rsqrt_safe(dot(r, r));

	float delta = 1.0 - (distance0 * rd_inv);
	float W_inv = (delta * stiffness) / (w0 + w1 + w_EPSILON);

	d0 += (w0 * W_inv) * r;
	d1 -= (w1 * W_inv) * r;
}

void SolveDistanceMinConstraint(
	const float distanceMin, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	// variation of SolveDistanceConstraint(...),
	// ensures distance p0 - p1 >= distanceMin

	float3 r = p1 - p0;
	float rd_inv = rsqrt_unsafe(dot(r, r));

	float delta = 1.0 - max(1.0, distanceMin * rd_inv);// === if (rd < distanceMin) { delta = 1.0 - distanceMin / rd; }
	float W_inv = (delta * stiffness) / (w0 + w1 + w_EPSILON);

	d0 += (w0 * W_inv) * r;
	d1 -= (w1 * W_inv) * r;
}

void SolveDistanceMaxConstraint(
	const float distanceMax, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	// variation of SolveDistanceConstraint(...),
	// ensures distance p0 - p1 <= distanceMax

	float3 r = p1 - p0;
	float rd_inv = rsqrt_unsafe(dot(r, r));

	float delta = 1.0 - min(1.0, distanceMax * rd_inv);// === if (rd > distanceMax) { delta = 1.0 - distanceMax / rd; }
	float W_inv = (delta * stiffness) / (w0 + w1 + w_EPSILON);

	d0 += (w0 * W_inv) * r;
	d1 -= (w1 * W_inv) * r;
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
	float rd_inv = rsqrt_unsafe(dot(r, r));

	r *= 1.0 - min(1.0, distanceMax * rd_inv);

	d1 -= r;
}

void SolveDistanceFTLConstraint(
	const float distance0,
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
	//        distance0

	float3 r = p1 - p0;
	float rd_inv = rsqrt_safe(dot(r, r));

	r *= 1.0 - (distance0 * rd_inv);

	d1 -= r;
}

void SolveTriangleBendingConstraint(
	const float radius0, const float stiffness,
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
	float rd_inv = rsqrt_safe(dot(r, r));

	float delta = 1.0 - radius0 * rd_inv;
	float W_inv = (2.0 * delta * stiffness) / (w0 + 2.0 * w1 + w2 + w_EPSILON);

	d0 += (w0 * W_inv) * r;
	d1 -= (w1 * W_inv * 2.0) * r;
	d2 += (w2 * W_inv) * r;
}

void SolveTriangleBendingMinConstraint(
	const float radiusMin, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	// variation of SolveTriangleBendingConstraint(...),
	// ensures triangle bend radius >= radiusMin

	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt_unsafe(dot(r, r));

	float delta = 1.0 - max(1.0, radiusMin * rd_inv);// === if (rd < radiusMin) { delta = 1.0 - radiusMin / rd; }
	float W_inv = (2.0 * delta * stiffness) / (w0 + 2.0 * w1 + w2 + w_EPSILON);

	d0 += (w0 * W_inv) * r;
	d1 -= (w1 * W_inv * 2.0) * r;
	d2 += (w2 * W_inv) * r;
}

void SolveTriangleBendingMaxConstraint(
	const float radiusMax, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	// variation of SolveTriangleBendingConstraint(...),
	// ensures triangle bend radius <= radiusMax

	float3 c = (p0 + p1 + p2) / 3.0;
	float3 r = p1 - c;
	float rd_inv = rsqrt_unsafe(dot(r, r));

	float delta = 1.0 - min(1.0, radiusMax * rd_inv);// === if (rd > radiusMax) { delta = 1.0 - radiusMax / rd; }
	float W_inv = (2.0 * delta * stiffness) / (w0 + 2.0 * w1 + w2 + w_EPSILON);

	d0 += (w0 * W_inv) * r;
	d1 -= (w1 * W_inv * 2.0) * r;
	d2 += (w2 * W_inv) * r;
}

void SolveEdgeVectorConstraint(
	const float3 v0, const float stiffness,
	const float w0, const float w1,
	const float3 p0, const float3 p1,
	inout float3 d0, inout float3 d1)
{
	float3 r = (p0 + v0) - p1;

	float W_inv = stiffness / (w0 + w1 + w_EPSILON);

	d0 -= (w0 * W_inv) * r;
	d1 += (w1 * W_inv) * r;
}

void SolveDualEdgeVectorConstraint(
	const float3 v0, const float3 v1, const float stiffness,
	const float w0, const float w1, const float w2,
	const float3 p0, const float3 p1, const float3 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	float3 r = (p0 - 2.0 * p1 + p2 + v0 - v1);

	float W_inv = (1.0 * stiffness) / (w0 + 4.0 * w1 + w2 + w_EPSILON);

	d0 -= (w0 * W_inv) * r;
	d1 += (w1 * W_inv * 2.0) * r;
	d2 -= (w2 * W_inv) * r;
}

void SolveMaterialFrameBendTwistConstraint(
	const float4 darboux0, const float stiffness,
	const float w0, const float w1,
	const float4 q0, const float4 q1,
	inout float4 d0, inout float4 d1)
{
	// see: "Position and Orientation Based Cosserat Rods" by T. Kugelstadt and E. Schömer
	// https://www.cg.informatik.uni-mainz.de/files/2016/06/Position-and-Orientation-Based-Cosserat-Rods.pdf

	float4 darboux = QMul(QInverse(q0), q1);

	// apply eq. 32 + 33 to pick closest delta
#if 1
	float sqnorm_add = dot(darboux + darboux0, darboux + darboux0);
	float sqnorm_sub = dot(darboux - darboux0, darboux - darboux0);
	float4 delta = darboux + (sqnorm_add < sqnorm_sub ? 1.0 : -1.0) * darboux0;
#else
	float4 delta_add = (darboux + darboux0);
	float4 delta_sub = (darboux - darboux0);
	float4 delta = (dot(delta_add, delta_add) < dot(delta_sub, delta_sub)) ? delta_add : delta_sub;
#endif

	//TODO consider this?
	//if (dot(delta.xyz, delta.xyz) < 1e-7)
	//	return;

	// apply eq. 40 to calc corrections
	float W_inv = stiffness / (w0 + w1 + w_EPSILON);

	delta.w = 0.0;// zero scalar part
	d0 += (w0 * W_inv) * QMul(q1, delta);
	d1 -= (w1 * W_inv) * QMul(q0, delta);
}

void SolveMaterialFrameStretchShearConstraint(
	const float distance0, const float stiffness,
	const float w0, const float w1, const float wq,
	const float3 p0, const float3 p1, const float4 q,
	inout float3 d0, inout float3 d1, inout float4 dq)
{
	// see: "Position and Orientation Based Cosserat Rods" by T. Kugelstadt and E. Schömer
	// https://www.cg.informatik.uni-mainz.de/files/2016/06/Position-and-Orientation-Based-Cosserat-Rods.pdf

	const float3 e3 = float3(0, 1, 0);

	// apply eq. 31 to obtain change vector
	float3 r = (p1 - p0) / distance0 - QMul(q, e3);

	// apply eq. 37 to calc corrections
	float W_inv = stiffness / (w0 + w1 + 4.0 * wq * distance0 * distance0 + w_EPSILON);

	d0 += (w0 * W_inv * distance0) * r;
	d1 -= (w1 * W_inv * distance0) * r;
	dq += (wq * W_inv * distance0 * distance0) * QMul(float4(r, 0), QMul(q, QConjugate(float4(e3, 0))));
}

//--------------------------------------------------
// constraint shortcuts: weight in fourth component

void SolveCollisionConstraint(
	const float margin,
	const float4 p,
	inout float3 d)
{
	SolveCollisionConstraint(margin, p.w, p.xyz, d);
}

void SolveCollisionFrictionConstraint(
	const float margin,
	const float friction,
	const float3 x0,
	const float4 p,
	inout float3 d)
{
	SolveCollisionFrictionConstraint(margin, friction, x0, p.w, p.xyz, d);
}

void SolveDistanceConstraint(
	const float distance0, const float stiffness,
	const float4 p0, const float4 p1,
	inout float3 d0, inout float3 d1)
{
	SolveDistanceConstraint(distance0, stiffness, p0.w, p1.w, p0.xyz, p1.xyz, d0, d1);
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
	const float radius0, const float stiffness,
	const float4 p0, const float4 p1, const float4 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	SolveTriangleBendingConstraint(radius0, stiffness, p0.w, p1.w, p2.w, p0.xyz, p1.xyz, p2.xyz, d0, d1, d2);
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

void SolveEdgeVectorConstraint(
	const float3 v0, const float stiffness,
	const float4 p0, const float4 p1,
	inout float3 d0, inout float3 d1)
{
	SolveEdgeVectorConstraint(v0, stiffness, p0.w, p1.w, p0.xyz, p1.xyz, d0, d1);
}

void SolveDualEdgeVectorConstraint(
	const float3 v0, const float3 v1, const float stiffness,
	const float4 p0, const float4 p1, const float4 p2,
	inout float3 d0, inout float3 d1, inout float3 d2)
{
	SolveDualEdgeVectorConstraint(v0, v1, stiffness, p0.w, p1.w, p2.w, p0.xyz, p1.xyz, p2.xyz, d0, d1, d2);
}

//------------------------------------------------------------
// constraint shortcuts: apply directly to position variables

void ApplyCollisionConstraint(const float margin, inout float3 p)
{
	float3 d = 0.0;
	SolveCollisionConstraint(margin, 1.0, p, d);
	p += d;
}

void ApplyCollisionFrictionConstraint(const float margin, const float friction, const float3 x0, inout float3 p)
{
	float3 d = 0.0;
	SolveCollisionFrictionConstraint(margin, friction, x0, 1.0, p, d);
	p += d;
}

void ApplyDistanceConstraint(const float distance0, const float stiffness, const float w0, const float w1, inout float3 p0, inout float3 p1)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	SolveDistanceConstraint(distance0, stiffness, w0, w1, p0, p1, d0, d1);
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

void ApplyDistanceFTLConstraint(const float distance0, const float3 p0, inout float3 p1, inout float3 d1)
{
	float3 d1_tmp = 0.0;
	SolveDistanceFTLConstraint(distance0, p0, p1, d1_tmp);
	p1 += d1_tmp;
	d1 += d1_tmp;
}

void ApplyTriangleBendingConstraint(
	const float radius0, const float stiffness,
	const float w0, const float w1, const float w2,
	inout float3 p0, inout float3 p1, inout float3 p2)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	float3 d2 = 0.0;
	SolveTriangleBendingConstraint(radius0, stiffness, w0, w1, w2, p0, p1, p2, d0, d1, d2);
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

void ApplyEdgeVectorConstraint(
	const float3 v0, const float stiffness,
	const float w0, const float w1,
	inout float3 p0, inout float3 p1)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	SolveEdgeVectorConstraint(v0, stiffness, w0, w1, p0, p1, d0, d1);
	p0 += d0;
	p1 += d1;
}

void ApplyDualEdgeVectorConstraint(
	const float3 v0, const float3 v1, const float stiffness,
	const float w0, const float w1, const float w2,
	inout float3 p0, inout float3 p1, inout float3 p2)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	float3 d2 = 0.0;
	SolveDualEdgeVectorConstraint(v0, v1, stiffness, w0, w1, w2, p0, p1, p2, d0, d1, d2);
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

void ApplyMaterialFrameStretchShearConstraint(
	const float distance0, const float stiffness,
	const float w0, const float w1, const float wq,
	inout float3 p0, inout float3 p1, inout float4 q)
{
	float3 d0 = 0.0;
	float3 d1 = 0.0;
	float4 dq = 0.0;
	SolveMaterialFrameStretchShearConstraint(distance0, stiffness, w0, w1, wq, p0, p1, q, d0, d1, dq);
	p0 += d0;
	p1 += d1;
	q = normalize(q + dq);
}

#endif//__HAIRSIMCOMPUTECONSTRAINTS_HLSL__
