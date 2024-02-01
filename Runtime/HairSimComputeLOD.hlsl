#ifndef __HAIRSIMCOMPUTELOD_HLSL__
#define __HAIRSIMCOMPUTELOD_HLSL__

#include "HairSimData.hlsl"
#include "HairSim.LOD.cs.hlsl"

#ifdef __HAIRVERTEX_HLSL__
LODFrustum MakeLODFrustumForCurrentCamera()
{
	//float unitDepthScreenSpan = abs(1.0 / UNITY_MATRIX_P._m11);
	//float unitDepthPixelCount = 0.5 * _ScreenParams.y;
	//float unitDepthPixelSpan = unitDepthScreenSpan / unitDepthPixelCount;
	//float unitSpanSubpixelDepth = 1.0 / unitDepthPixelSpan;
	//                            = 1.0 / (unitDepthScreenSpan / unitDepthPixelCount);
	//                            = unitDepthPixelCount / unitDepthScreenSpan;
	//                            = unitDepthPixelCount / (1.0 / UNITY_MATRIX_P._m11);
	//                            = unitDepthPixelCount * UNITY_MATRIX_P._m11;
	//                            = 0.5 * _ScreenParams.y * UNITY_MATRIX_P._m11;
	//float unitSpanClippingDepth = unitSpanSubpixelDepth / lodClipThreshold;

	float unitDepthInvScreenSpan = UNITY_MATRIX_P._m11;
	float unitSpanSubpixelDepth = abs(0.5 * _ScreenParams.y * unitDepthInvScreenSpan);// lerp(unitDepthInvScreenSpan, 0.5 / unity_OrthoParams.y, unity_OrthoParams.w));

	bool cameraOrtho = (UNITY_MATRIX_P._m33 != 0.0);

	LODFrustum lodFrustum;
	{
		lodFrustum.cameraPosition = HAIR_VERTEX_IMPL_WS_POS_TO_RWS(_WorldSpaceCameraPos);
		lodFrustum.cameraForward = cameraOrtho ? float3(0.0, 0.0, 0.0) : -UNITY_MATRIX_V[2].xyz;
		lodFrustum.cameraNear = cameraOrtho ? 1.0 : _ProjectionParams.y;// cameraOrtho ? 1.0 : 0.001;// lerp(_ProjectionParams.y, 1.0, unity_OrthoParams.w);
		lodFrustum.unitSpanSubpixelDepth = unitSpanSubpixelDepth;
	}

	return lodFrustum;
}
#endif

bool LODFrustumContains(const LODFrustum lodFrustum, const LODBounds lodBounds)
{
	// see: https://fgiesen.wordpress.com/2010/10/17/view-frustum-culling/
	return (
		dot(lodBounds.center + lodBounds.extent * sign(lodFrustum.plane0.xyz), lodFrustum.plane0.xyz) > -lodFrustum.plane0.w &&
		dot(lodBounds.center + lodBounds.extent * sign(lodFrustum.plane1.xyz), lodFrustum.plane1.xyz) > -lodFrustum.plane1.w &&
		dot(lodBounds.center + lodBounds.extent * sign(lodFrustum.plane2.xyz), lodFrustum.plane2.xyz) > -lodFrustum.plane2.w &&
		dot(lodBounds.center + lodBounds.extent * sign(lodFrustum.plane3.xyz), lodFrustum.plane3.xyz) > -lodFrustum.plane3.w &&
		dot(lodBounds.center + lodBounds.extent * sign(lodFrustum.plane4.xyz), lodFrustum.plane4.xyz) > -lodFrustum.plane4.w &&
		dot(lodBounds.center + lodBounds.extent * sign(lodFrustum.plane5.xyz), lodFrustum.plane5.xyz) > -lodFrustum.plane5.w
	);
}

float LODFrustumCoverage(const LODFrustum lodFrustum, const float sampleDepth, const float sampleSpan)
{
	float sampleSpanSubpixelDepth = sampleSpan * lodFrustum.unitSpanSubpixelDepth;
	float sampleCoverage = sampleSpanSubpixelDepth / max(sampleDepth, lodFrustum.cameraNear);
	{
		return sampleCoverage;
	}
}

float LODFrustumCoverage(const LODFrustum lodFrustum, const float3 samplePosition, const float sampleSpan)
{
	float sampleDepth = dot(lodFrustum.cameraForward, samplePosition - lodFrustum.cameraPosition);
	{
		return LODFrustumCoverage(lodFrustum, sampleDepth, sampleSpan);
	}
}

float2 LODFrustumCoverageCeiling(const LODFrustum lodFrustum, const LODBounds lodBounds, const LODGeometry lodGeometry)
{
	if (LODFrustumContains(lodFrustum, lodBounds))
	{
		float3 cameraVector = lodFrustum.cameraPosition - lodBounds.center;
		float cameraDistance = length(cameraVector);

		//TODO revisit this
		float3 nearestSamplePosition = lodBounds.center + cameraVector * (min(cameraDistance, lodBounds.radius) / cameraDistance);
		float2 nearestSampleCoverage = float2(
			LODFrustumCoverage(lodFrustum, nearestSamplePosition, lodGeometry.maxParticleDiameter),
			LODFrustumCoverage(lodFrustum, nearestSamplePosition, lodGeometry.maxParticleInterval)
		);

		return nearestSampleCoverage;
	}
	else
	{
		return 0.0;
	}
}

float2 LODFrustumCoverageCeilingSequential(const LODBounds lodBounds, const LODGeometry lodGeometry)
{
	float2 maxCoverage = 0.0;
	{
		for (uint i = 0; i != _LODFrustumCount; i++)
		{
			maxCoverage = max(maxCoverage, LODFrustumCoverageCeiling(_LODFrustum[i], lodBounds, lodGeometry));
		}
	}

	return maxCoverage;
}

float ResolveLODQuantity(const float sampleCoverage, const float lodCeiling, const float lodScale, const float lodBias)
{
	//TODO: more friendly hybrid?
	//
	// if (bias < 0.0)
	//		scale	= 1 + min(0.0, bias)
	//		bias	= 0
	// else
	//		scale	= 1
	//		bias	= max(0.0, bias)
	//
	// float curveLod = saturate(curveCoverage) * (1.0 + min(0.0, lodBias)) + max(0.0, lodBias));   // lod in [0..1]

	float lodValue = saturate(saturate(sampleCoverage * lodScale) + lodBias);
	{
		return min(lodValue, lodCeiling);
	}
}

LODIndices ResolveLODIndices(const float lodValue)
{
	//TODO optimize
	//TODO e.g. could replace with lut, floor(sample(_LODIndicesByLODValue, lodValue))

	LODIndices lodDesc;
	{
		lodDesc.lodIndexLo = 0;
		lodDesc.lodIndexHi = _LODCount - 1;
		{
			while (lodDesc.lodIndexHi > 0)
			{
				if (lodValue > _LODGuideCount[lodDesc.lodIndexHi - 1] / (float)_StrandCount)
				{
					break;
				}
				else
				{
					lodDesc.lodIndexHi--;
				}
			}
		}
		
		if (lodDesc.lodIndexLo != lodDesc.lodIndexHi)
		{
			lodDesc.lodIndexLo = lodDesc.lodIndexHi - 1;
			{
				float lodValueLo = _LODGuideCount[lodDesc.lodIndexLo] / (float)_StrandCount;
				float lodValueHi = _LODGuideCount[lodDesc.lodIndexHi] / (float)_StrandCount;
			
				lodDesc.lodBlendFrac = saturate((lodValue - lodValueLo) / (lodValueHi - lodValueLo));
			}
		}
		else
		{
			lodDesc.lodBlendFrac = 0.0f;
		}
		
		lodDesc.lodValue = lodValue;
	}

	return lodDesc;
}

#endif//__HAIRSIMCOMPUTELOD_HLSL__