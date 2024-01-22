#ifndef __HAIRSIMDEBUGDRAWCOLORS_HLSL__
#define __HAIRSIMDEBUGDRAWCOLORS_HLSL__

//----------------
// colors generic

float3 ColorCycle(uint index, uint count)
{
	float t = frac(index / (float)count);

	// source: https://www.shadertoy.com/view/4ttfRn
	float3 c = 3.0 * float3(abs(t - 0.5), t.xx) - float3(1.5, 1.0, 2.0);
	return 1.0 - c * c;
}

float3 ColorRamp(uint index, uint count)
{
	float t = 1.0 - frac(index / (float)count);

	// source: https://www.shadertoy.com/view/4ttfRn
	float3 c = 2.0 * t - float3(0.0, 1.0, 2.0);
	return 1.0 - c * c;
}

//-------------------
// colors quantities

float3 ColorDensity(float rho)
{
	float above = saturate(rho - 1.0);
	return float3(saturate(rho), saturate(rho) - above, saturate(rho) - above);
}

float3 ColorDivergence(float div)
{
	if (div < 0.0)// inward flux increases pressure
		return saturate(abs(float3(div, div, 0.0)));
	else
		return saturate(abs(float3(0.0, div, div)));
}

float3 ColorPressure(float p)
{
	p *= 15.0;
	if (p > 0.0)
		return saturate(float3(frac(p), 0.0, 0.0));
	else
		return saturate(float3(0.0, 0.0, frac(-p)));
}

float3 ColorGradient(float3 n)
{
	//return abs(n.zzz);

	float d = dot(n, n);
	if (d > 1e-11)
		return 0.5 + 0.5 * (n * rsqrt(d));
	else
		return 0.0;

	//return (0.5 + 0.5 * normalize(n.xzy));
}

float3 ColorVelocity(float3 v)
{
	return saturate(abs(float3(v.x, 0.0, -v.z)));
}

float3 ColorProbe(float3 s)
{
	return s;
}

#endif//__HAIRSIMDEBUGDRAWCOLORS_HLSL__
