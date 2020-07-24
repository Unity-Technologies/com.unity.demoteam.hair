Shader "Hidden/HairSimDebugDraw"
{
	HLSLINCLUDE

	#pragma target 5.0
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

	#include "HairSimComputeConfig.hlsl"
	#include "HairSimComputeSolverData.hlsl"
	#include "HairSimComputeVolumeData.hlsl"
	#include "HairSimComputeVolumeUtility.hlsl"
	
	uint _DebugSliceAxis;
	float _DebugSliceOffset;
	float _DebugSliceDivider;

	struct DebugVaryings
	{
		float4 positionCS : SV_POSITION;
		float4 color : TEXCOORD0;
	};

	float4 DebugFrag(DebugVaryings input) : SV_Target
	{
		return input.color;
	}

	float3 ColorizeCycle(uint index, const uint count)
	{
		const float k = 1.0 / (count - 1);
		float t = k * (index % count);

		// source: https://www.shadertoy.com/view/4ttfRn
		float3 c = 3.0 * float3(abs(t - 0.5), t.xx) - float3(1.5, 1.0, 2.0);
		return 1.0 - c * c;
	}

	float3 ColorizeRamp(uint index, const uint count)
	{
		index = min(index % count, count - 1);

		const float k = 1.0 / (count - 1);
		float t = 0.5 - 0.5 * k * index;// 0.5 * (1.0 - (k * index));

		// source: https://www.shadertoy.com/view/4ttfRn
		float3 c = 2.0 * t - float3(0.0, 1.0, 2.0);
		return 1.0 - c * c;
	}

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest LEqual
		ZWrite On
		Offset 0, -1

		Pass// 0 == STRANDS
		{
			HLSLPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
#if LAYOUT_INTERLEAVED
				const uint strandParticleBegin = instanceID;
				const uint strandParticleStride = _StrandCount;
#else
				const uint strandParticleBegin = instanceID * _StrandParticleCount;
				const uint strandParticleStride = 1;
#endif

#if STRAND_31_32_DEBUG == 2
				if (vertexID > 1)
					vertexID = 1;
#endif

				uint i = strandParticleBegin + strandParticleStride * vertexID;
				float3 worldPos = _ParticlePosition[i].xyz;

				float volumeDensity = _VolumeDensity.SampleLevel(_Volume_trilinear_clamp, VolumeWorldToUVW(worldPos), 0);
				float volumeDensityShadow = 8.0;
				float volumeOcclusion = saturate((volumeDensityShadow - volumeDensity) / volumeDensityShadow);// pow(1.0 - saturate(volumeDensity / 200.0), 4.0);
				//float volumeOcclusion = saturate(1.0 - pow(1.0 - exp(-volumeDensity), 2.0));
				//float volumeOcclusion = pow(1.0 - saturate(volumeDensity / 400.0), 4.0);

				DebugVaryings output;
				output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
				output.color = volumeOcclusion * float4(ColorizeCycle(instanceID, _StrandCount), 1.0);
				return output;
			}

			ENDHLSL
		}

		Pass// 1 == DENSITY
		{
			HLSLPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID);
				float volumeDensity = _VolumeDensity[volumeIdx];
				float3 worldPos = (volumeDensity == 0.0) ? 1e+7 : VolumeIndexToWorld(volumeIdx);

				DebugVaryings output;
				output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
				output.color = float4(ColorizeRamp(volumeDensity, 32), 1.0);
				return output;
			}

			ENDHLSL
		}

		Pass// 2 == GRADIENT
		{
			HLSLPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID >> 1);
				float3 worldPos = VolumeIndexToWorld(volumeIdx);

				if (vertexID & 1)
				{
					worldPos -= _VolumeDensityGrad[volumeIdx] * 0.002;
				}

				DebugVaryings output;
				output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
				output.color = float4(ColorizeRamp(1 - vertexID, 2), 1.0);
				return output;
			}

			ENDHLSL
		}

		Pass// 3 == SLICE
		{
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag_Slice

			float3 ColorizeDensity(float rho)
			{
				return saturate(rho).xxx;
			}

			float3 ColorizeDivergence(float div)
			{
				if (div > 0.0)
					return saturate(float3(div, div, 0.0));
				else
					return saturate(float3(0.0, -div, -div));
			}

			float3 ColorizePressure(float p)
			{
				p *= 1000.0;
				if (p > 0.0)
					return saturate(float3(p, 0.0, 0.0));
				else
					return saturate(float3(0.0, 0.0, -p));
			}

			float3 ColorizeGradient(float3 n)
			{
				//return (0.5 + 0.5 * normalize(n.xzy));
				float d = dot(n, n);
				if (d > 1e-11)
					return 0.5 + 0.5 * (n * rsqrt(d));
				else
					return 0.0;
			}

			float3 ColorizeVelocity(float3 v)
			{
				return abs(v);
			}

			DebugVaryings DebugVert(uint vertexID : SV_VertexID)
			{
				float3 uvw = float3(((vertexID >> 1) ^ vertexID) & 1, vertexID >> 1, _DebugSliceOffset);
				float3 uvwWorld = (_DebugSliceAxis == 0) ? uvw.zxy : (_DebugSliceAxis == 1 ? uvw.xzy : uvw.xyz);
				float3 worldPos = lerp(_VolumeWorldMin, _VolumeWorldMax, uvwWorld);

				uvw = uvwWorld;

				DebugVaryings output;
				output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
				output.color = float4(uvw, 1);
				return output;
			}

			float4 DebugFrag_Slice(DebugVaryings input) : SV_Target
			{
				float3 uvw = input.color.xyz;

#if SPLAT_TRILINEAR
#define SLICE_SAMPLER _Volume_trilinear_clamp
#else
#define SLICE_SAMPLER _Volume_point_clamp
#endif

				float volumeDensity = _VolumeDensity.SampleLevel(SLICE_SAMPLER, uvw, 0);
				float3 volumeVelocity = _VolumeVelocity.SampleLevel(SLICE_SAMPLER, uvw, 0).xyz;
				float3 volumeDensityGrad = _VolumeDensityGrad.SampleLevel(SLICE_SAMPLER, uvw, 0);
				float volumeDivergence = _VolumeDivergence.SampleLevel(SLICE_SAMPLER, uvw, 0);
				float volumePressure = _VolumePressure.SampleLevel(SLICE_SAMPLER, uvw, 0);
				float3 volumePressureGrad = _VolumePressureGrad.SampleLevel(SLICE_SAMPLER, uvw, 0);

				const float opacity = 0.9;
				{
					float3 localPos = VolumeUVWToLocal(uvw);
					float3 localPosFloor = floor(localPos);

					float3 gridDist = abs(localPos - localPosFloor);
					float3 gridWidth = fwidth(localPos);
					if (any(gridDist < gridWidth))
					{
						uint i = (uint)localPosFloor[_DebugSliceAxis] % 3;
						return float4(0.2 * float3(i == 0, i == 1, i == 2), opacity);
					}
				}

				float x = uvw.x + _DebugSliceDivider;
				if (x < 1.0)
					return float4(ColorizeDensity(volumeDensity), opacity);
				else if (x < 2.0)
					return float4(ColorizeGradient(volumeDensityGrad), opacity);
				else if (x < 3.0)
					return float4(ColorizeVelocity(volumeVelocity), opacity);
				else if (x < 4.0)
					return float4(ColorizeDivergence(volumeDivergence), opacity);
				else if (x < 5.0)
					return float4(ColorizePressure(volumePressure), opacity);
				else
					return float4(ColorizeGradient(volumePressureGrad), opacity);
			}

			ENDHLSL
		}

		Pass// 4 == STRANDS MOTION
		{
			ZTest Equal
			ZWrite Off

			HLSLPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
#if LAYOUT_INTERLEAVED
				const uint strandParticleBegin = instanceID;
				const uint strandParticleStride = _StrandCount;
#else
				const uint strandParticleBegin = instanceID * _StrandParticleCount;
				const uint strandParticleStride = 1;
#endif

				uint i = strandParticleBegin + strandParticleStride * vertexID;
				float3 worldPos0 = _ParticlePositionPrev[i].xyz;
				float3 worldPos1 = _ParticlePosition[i].xyz;
				
				float4 clipPos0 = mul(UNITY_MATRIX_PREV_VP, float4(GetCameraRelativePositionWS(worldPos0), 1.0));
				float4 clipPos1 = mul(UNITY_MATRIX_UNJITTERED_VP, float4(GetCameraRelativePositionWS(worldPos1), 1.0));

				float2 ndc0 = clipPos0.xy / clipPos0.w;
				float2 ndc1 = clipPos1.xy / clipPos1.w;

				DebugVaryings output;
				output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos1));
				output.color = float4(0.5 * (ndc1 - ndc0), 0, 0);
				return output;
			}

			ENDHLSL
		}
	}
}
