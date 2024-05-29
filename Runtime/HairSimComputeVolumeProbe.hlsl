#ifndef __HAIRSIMCOMPUTEVOLUMEPROBE_HLSL__
#define __HAIRSIMCOMPUTEVOLUMEPROBE_HLSL__

#define HALF_SQRT_INV_PI    0.5 * 0.56418958354775628694 
#define HALF_SQRT_3_DIV_PI  0.5 * 0.97720502380583984317

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

float EstimateStrandCount(const VolumeLODGrid lodGrid, float3 P, float3 L)
{
	float rho_sum = 0;

	VolumeTraceState trace = VolumeTraceBegin(lodGrid, P, L, 0, _ScatteringProbeSubsteps);

	if ((_VolumeFeatures & VOLUMEFEATURES_SCATTERING_FASTPATH) != 0)
	{
		while (VolumeTraceStep(trace))
		{
			rho_sum += VolumeSampleScalar(_VolumeDensity, trace.uvw);
		}
	}
	else
	{
		while (VolumeTraceStep(trace))
		{
			if (BoundaryDistance(VolumeUVWToWorld(lodGrid, trace.uvw)) < _ScatteringProbeOccluderMargin * lodGrid.volumeCellRadius)
			{
				rho_sum += VolumeSampleScalar(_VolumeDensity, trace.uvw) + _ScatteringProbeOccluderDensity;
			}
			else
			{
				rho_sum += VolumeSampleScalar(_VolumeDensity, trace.uvw);
			}
		}
	}
		
	const float stepLength = length(VolumeWorldSize(lodGrid) * trace.uvwStep);
	const float stepCapacity = max(0.0, stepLength / _ScatteringProbeUnitWidth);

	return (rho_sum * stepCapacity);
}

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

float EncodeSHCoefficient(const VolumeLODGrid lodGrid, float3 P, int l, int m)
{
	const int SAMPLES_PHI = _ScatteringProbeSamplesPhi;
	const int SAMPLES_THETA = _ScatteringProbeSamplesTheta;

	const float dPhi = TWO_PI / SAMPLES_PHI;// step yaw
	const float dTheta = PI / SAMPLES_THETA;// step pitch

	float C = 0;

	for (int i = 0; i < SAMPLES_PHI; i++)
	{
		float phi = i * dPhi;

		for (int j = 0; j < SAMPLES_THETA; j++)
		{
			float theta = (0.5 + j) * dTheta;

			float sinTheta, cosTheta;
			sincos(theta, sinTheta, cosTheta);

			float3 L = SphericalToCartesian(phi, cosTheta);

			float value   = EstimateStrandCount(lodGrid, P, L);
			float valueSH = EstimateSH(l, m, L); 

			C += value * valueSH * sinTheta * dPhi * dTheta;
		}
	}

	return C;
}

// Projects the neighboring density field of the cell into an L1 spherical harmonic.
void ProjectStrandCountSH(const VolumeLODGrid lodGrid, uint3 index, inout float coefficients[4])
{
	float3 P = VolumeIndexToWorld(lodGrid, index);

	// L0
	coefficients[0] = EncodeSHCoefficient(lodGrid, P, 0,  0);

	// L1
	coefficients[1] = EncodeSHCoefficient(lodGrid, P, 1, -1);
	coefficients[2] = EncodeSHCoefficient(lodGrid, P, 1,  0);
	coefficients[3] = EncodeSHCoefficient(lodGrid, P, 1, +1);
}

// Projects the neighboring density field of the cell into an L1 spherical harmonic.
float4 ProjectStrandCountSH_L0L1(const VolumeLODGrid lodGrid, float3 P)
{
	const int SAMPLES_PHI = _ScatteringProbeSamplesPhi;
	const int SAMPLES_THETA = _ScatteringProbeSamplesTheta;

	const float dPhi = TWO_PI / SAMPLES_PHI;// step yaw
	const float dTheta = PI / SAMPLES_THETA;// step pitch

	float4 probe = 0.0;

	for (int i = 0; i < SAMPLES_PHI; i++)
	{
		float sinPhi, cosPhi;
		sincos(i * dPhi, sinPhi, cosPhi);

		for (int j = 0; j < SAMPLES_THETA; j++)
		{
			float sinTheta, cosTheta;
			sincos((0.5 + j) * dTheta, sinTheta, cosTheta);

			float3 L = float3(
				sinTheta * cosPhi,
				sinTheta * sinPhi,
				cosTheta);

			float strandCountApprox = EstimateStrandCount(lodGrid, P, L);
			float strandCountTerm = strandCountApprox * sinTheta * dPhi * dTheta;

			// L0
			probe.x += strandCountTerm * EstimateSH(0,  0, L);

			// L1
			probe.y += strandCountTerm * EstimateSH(1, -1, L);
			probe.z += strandCountTerm * EstimateSH(1,  0, L);
			probe.w += strandCountTerm * EstimateSH(1, +1, L);
		}
	}

	return probe;
}

// Returns the approximate strand count in direction L from an L1 band spherical harmonic.
float DecodeStrandCount(float3 L, float4 probe)
{
	float4 Ylm = float4(
		HALF_SQRT_INV_PI,
		HALF_SQRT_3_DIV_PI * L.y,
		HALF_SQRT_3_DIV_PI * L.z,
		HALF_SQRT_3_DIV_PI * L.x
		);

	return abs(dot(probe, Ylm));
}

#endif//__HAIRSIMCOMPUTEVOLUMEPROBE_HLSL__
