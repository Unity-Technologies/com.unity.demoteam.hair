#ifndef __HAIRSIMCOMPUTESTRANDCOUNTPROBE_HLSL__
#define __HAIRSIMCOMPUTESTRANDCOUNTPROBE_HLSL__

// Transforms the unit vector from the spherical to the Cartesian (right-handed, Z up) coordinate.
real3 SphericalToCartesian(real cosPhi, real sinPhi, real cosTheta)
{
	real sinTheta = SinFromCos(cosTheta);

	return real3(real2(cosPhi, sinPhi) * sinTheta, cosTheta);
}

real3 SphericalToCartesian(real phi, real cosTheta)
{
	real sinPhi, cosPhi;
	sincos(phi, sinPhi, cosPhi);

	return SphericalToCartesian(cosPhi, sinPhi, cosTheta);
}

//TODO: Optimize Me
float EstimateStrandCount(float3 P, float3 L)
{
	const int numStepsWithinCell = _StrandCountSubstep;
	const int numSteps = _VolumeCells.x * numStepsWithinCell;

	VolumeTraceState trace = VolumeTraceBegin(P, L, 0, numStepsWithinCell);

	const float strandCountTerm = length(trace.uvwStep) * rcp(0.001);

	float n = 0;

	for (int i = 0; i != numSteps; i++)
	{
		if (VolumeTraceStep(trace))
		{
			//TODO: Strand Count
			uint3 idx = (trace.uvw * _VolumeCells) - 0.5;
			float cellDensitySampled = _VolumeDensity.Load(idx);
			n += cellDensitySampled * strandCountTerm;
		}
	}

	return n;
}

#define HALF_SQRT_INV_PI    0.5 * 0.56418958354775628694 
#define HALF_SQRT_3_DIV_PI  0.5 * 0.97720502380583984317

float EstimateSH(int l, int m, float3 L)
{
	const float x = L.x;
	const float y = L.y;
	const float z = L.z;

	// Evaluate the SH basis function given l and m
	if (l == 0 && m ==  0) return HALF_SQRT_INV_PI;
	if (l == 1 && m == -1) return HALF_SQRT_3_DIV_PI * y;
	if (l == 1 && m ==  0) return HALF_SQRT_3_DIV_PI * z;
	if (l == 1 && m == +1) return HALF_SQRT_3_DIV_PI * x;

	return -1;
}

float EncodeSHCoefficient(float3 P, int l, int m)
{
	const int STEPS_PHI   = _StrandCountPhi;
	const int STEPS_THETA = _StrandCountTheta;

	const float dPhi = TWO_PI / STEPS_PHI;
	const float dTheta = PI / STEPS_THETA;

	float C = 0;

	for (int i = 0; i < STEPS_PHI; i++)
	{
		float phi = i * dPhi;

		for (int j = 0; j < STEPS_THETA; j++)
		{
			float theta = (0.5 + j) * dTheta;

			float sinTheta, cosTheta;
			sincos(theta, sinTheta, cosTheta);

			float3 L = SphericalToCartesian(phi, cosTheta);

			float value   = EstimateStrandCount(P, L);
			float valueSH = EstimateSH(l, m, L); 

			C += value * valueSH * sinTheta * dPhi * dTheta;
		}
	}

	return C;
}

// Projects the neighboring density field of the cell into an L1 spherical harmonic.
void ProjectStrandCountSH(uint3 index, inout float coefficients[4])
{
	float3 P = VolumeIndexToWorld(index);

	// L0
	coefficients[0] = EncodeSHCoefficient(P, 0,  0);

	// L1
	coefficients[1] = EncodeSHCoefficient(P, 1, -1);
	coefficients[2] = EncodeSHCoefficient(P, 1,  0);
	coefficients[3] = EncodeSHCoefficient(P, 1, +1);
}

#endif//__HAIRSIMCOMPUTESTRANDCOUNTPROBE_HLSL__
