#ifndef __HAIRSIMCOMPUTEQUATERNION_HLSL__
#define __HAIRSIMCOMPUTEQUATERNION_HLSL__

//------------
// quaternion

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

float4 MakeQuaternionIdentity()
{
	return float4(0.0, 0.0, 0.0, 1.0);
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

float4 MakeQuaternionFromBend(float3 p0, float3 p1, float3 p2)
{
	float3 u = normalize(p1 - p0);
	float3 v = normalize(p2 - p1);
	return MakeQuaternionFromTo(u, v);
}

#endif//__HAIRSIMCOMPUTEQUATERNION_HLSL__
