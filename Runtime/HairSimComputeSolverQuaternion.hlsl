#ifndef __HAIRSIMCOMPUTEQUATERNION_HLSL__
#define __HAIRSIMCOMPUTEQUATERNION_HLSL__

//----------------------
// quaternion functions

float4 QConjugate(float4 q)
{
	return q * float4(-1.0, -1.0, -1.0, 1.0);
}

float4 QInverse(float4 q)
{
	return QConjugate(q) * rcp(dot(q, q));
}

float4 QMul(float4 a, float4 b)
{
	float4 q;
	q.xyz = a.w * b.xyz + b.w * a.xyz + cross(a.xyz, b.xyz);
	q.w = a.w * b.w - dot(a.xyz, b.xyz);
	return q;
}

float3 QMul(float4 q, float3 v)
{
	float3 t = 2.0 * cross(q.xyz, v);
	return v + q.w * t + cross(q.xyz, t);
}

float4 QNlerp(float4 a, float4 b, float t)
{
	float d = dot(a, b);
	if (d < 0.0)
	{
		b = -b;
	}

	return normalize(lerp(a, b, t));
}

float4 QSlerp(float4 a, float4 b, float t)
{
	float d = dot(a, b);
	if (d < 0.0)
	{
		d = -d;
		b = -b;
	}

	if (d < (1.0 - 1e-5))
	{
		float2 w = sin(acos(d) * float2(1.0 - t, t)) * rsqrt(1.0 - d * d);
		return a * w.x + b * w.y;
	}
	else
	{
		return normalize(lerp(a, b, t));
	}
}

float3x3 QMat3x3(float4 q)
{
	float3 c0 = QMul(q, float3(1.0, 0.0, 0.0));
	float3 c1 = QMul(q, float3(0.0, 1.0, 0.0));
	float3 c2 = QMul(q, float3(0.0, 0.0, 1.0));

	return float3x3(
		c0.x, c1.x, c2.x,
		c0.y, c1.y, c2.y,
		c0.z, c1.z, c2.z);
}

float4 QDecomposeTwist(float4 q, float3 axis)
{
	// see: "Component of a quaternion rotation around an axis"
	// https://stackoverflow.com/a/22401169

	float3 v = float3(q.x, q.y, q.z);
	float dn = dot(v, axis);
	float3 p = dn * axis;
	float4 r = float4(p.x, p.y, p.z, q.w);

	if (dot(r, r) < 1e-5)
	{
		r = float4(0.0, 0.0, 0.0, 1.0);
	}
	else
	{
		if (dn < 0.0)
			r = normalize(-r);
		else
			r = normalize(r);
	}

	return r;
}

//-------------------------
// quaternion constructors

float4 MakeQuaternionIdentity()
{
	return float4(0.0, 0.0, 0.0, 1.0);
}

float4 MakeQuaternionTwistIdentity()
{
	return float4(0.0, 1.0, 0.0, 0.0);
}

float4 MakeQuaternionFromAxisAngle(float3 axis, float angle)
{
	float sina = sin(0.5 * angle);
	float cosa = cos(0.5 * angle);
	return float4(axis * sina, cosa);
}

float4 MakeQuaternionFromTo(float3 u, float3 v)
{
	float4 q;
	float s = 1.0 + dot(u, v);
	if (s < 1e-6)// if 'u' and 'v' are directly opposing
	{
		q.xyz = abs(u.x) > abs(u.z) ? float3(-u.y, u.x, 0.0) : float3(0.0, -u.z, u.y);
		q.w = 0.0;
	}
	else
	{
		q.xyz = cross(u, v);
		q.w = s;
	}
	return normalize(q);
}

float4 MakeQuaternionFromToWithFallback(float3 u, float3 v, float3 w)
{
	float4 q;
	float s = 1.0 + dot(u, v);
	if (s < 1e-6)// if 'u' and 'v' are directly opposing
	{
		q.xyz = w;
		q.w = 0.0;
	}
	else
	{
		q.xyz = cross(u, v);
		q.w = s;
	}
	return normalize(q);
}

float4 MakeQuaternionLookAtBasis(float3 forwardRef, float3 forward, float3 upRef, float3 up)
{
	float4 rotForward = MakeQuaternionFromTo(forwardRef, forward);
	float4 rotForwardTwist = MakeQuaternionFromToWithFallback(QMul(rotForward, upRef), up, forward);
	return QMul(rotForwardTwist, rotForward);
}

float4 MakeQuaternionLookAt(float3 forward, float3 up)
{
	float3 unityForward = float3(0, 0, 1);
	float3 unityUp = float3(0, 1, 0);
	return MakeQuaternionLookAtBasis(unityForward, forward, unityUp, up);
}

float4 MakeQuaternionFromBend(float3 p0, float3 p1, float3 p2)
{
	float3 u = normalize(p1 - p0);
	float3 v = normalize(p2 - p1);
	return MakeQuaternionFromTo(u, v);
}

float4 NextQuaternionFromBend(float3 p0, float3 p1, float3 p2, float4 q1)
{
	float3 u = QMul(q1, float3(0, 1, 0));
	float3 v = normalize(p2 - p1);

	float4 rotTangent = MakeQuaternionFromToWithFallback(u, v, QMul(q1, float3(1, 0, 0)));
	float4 rotTwist = MakeQuaternionTwistIdentity();

	return QMul(rotTangent, QMul(q1, rotTwist));
}

float4 NextQuaternionFromBendRMF(float3 p0, float3 p1, float3 p2, float4 q1)
{
	// see: "Computation of Rotation Minimizing Frames" by W. Wang, B. Jüttler, D. Zheng and Y. Liu
	// https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/Computation-of-rotation-minimizing-frames.pdf

	float3 localNormal = float3(0, 0, 1);
	float3 localTangent = float3(0, 1, 0);
	float3 localBitangent = float3(1, 0, 0);

	float3 v1 = normalize(p2 - p1);
	float3 ri = QMul(q1, localBitangent);
	float3 ti = QMul(q1, localTangent);

	float3 rLi = reflect(ri, v1);
	float3 tLi = reflect(ti, v1);

	float3 t2 = v1;
	float3 v2 = normalize(t2 - tLi);
	float3 r2 = reflect(rLi, v2);
	float3 s2 = cross(r2, t2);

#if 1
	// build new frame
	float4 rotTangent = MakeQuaternionFromToWithFallback(localTangent, t2, ri);
	float4 rotTangentTwist = MakeQuaternionFromToWithFallback(QMul(rotTangent, localNormal), -s2, t2);
	return QMul(rotTangentTwist, rotTangent);
#else
	// rotate existing frame
	float4 rotTangent = MakeQuaternionFromToWithFallback(ti, t2, ri);
	q1 = QMul(rotTangent, q1);
	float4 rotTangentTwist = MakeQuaternionFromToWithFallback(QMul(q1, localBitangent), r2, t2);
	q1 = QMul(rotTangentTwist, q1);
	return q1;
#endif
}

//--------------------------------
// quaternion storage/compression

float4 QDecode16(uint2 q16)
{
	return float4(f16tof32(q16), f16tof32(q16 >> 16));
}

uint2 QEncode16(float4 q)
{
	return f32tof16(q.xy) | (f32tof16(q.zw) << 16);
}

#endif//__HAIRSIMCOMPUTEQUATERNION_HLSL__
