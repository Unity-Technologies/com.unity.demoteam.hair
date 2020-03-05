Shader "Hidden/HairSimDebugDraw"
{
	CGINCLUDE

	#pragma target 5.0
	
	#include "HairSimComputeConfig.hlsl"

	uint _StrandCount;
	uint _StrandParticleCount;

	StructuredBuffer<float4> _ParticlePosition;
	StructuredBuffer<float4> _ParticlePositionPrev;

	uint _DebugSliceAxis;
	float _DebugSliceOffset;
	float _DebugSliceDivider;

	float3 _VolumeCells;
	float3 _VolumeWorldMin;
	float3 _VolumeWorldMax;

	Texture3D<int> _VolumeDensity;
	Texture3D<int> _VolumeVelocityX;
	Texture3D<int> _VolumeVelocityY;
	Texture3D<int> _VolumeVelocityZ;

	Texture3D<float4> _VolumeVelocity;
	Texture3D<float3> _VolumeGradient;
	Texture3D<float> _VolumeDivergence;
	Texture3D<float> _VolumePressure;
	Texture3D<float3> _VolumePressureGradient;
	Texture3D<float3> _VolumeVelocitySolenoidal;

	SamplerState sampler_trilinear_clamp;

	#include "HairSimComputeVolumeUtility.hlsl"

	//--- built-in begin
	float4x4 _ViewProjMatrix;
	float4x4 _PrevViewProjMatrix;
	float4x4 _NonJitteredViewProjMatrix;
	float2 _CameraMotionVectorsScale;
	//--- built-in end

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

	ENDCG

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest LEqual
		ZWrite On
		Offset 0, -1

		Pass// 0 == STRANDS
		{
			CGPROGRAM

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
				float3 worldPos = _ParticlePosition[i].xyz;

				float volumeDensity = _VolumeVelocity.SampleLevel(sampler_trilinear_clamp, VolumeWorldToUVW(worldPos), 0).w;
				float volumeOcclusion = pow(1.0 - saturate(volumeDensity / 200.0), 4.0);

				DebugVaryings output;
				output.positionCS = mul(_ViewProjMatrix, float4(worldPos - _WorldSpaceCameraPos, 1.0));
				output.color = volumeOcclusion * float4(ColorizeCycle(instanceID, _StrandCount), 1.0);
				return output;
			}

			ENDCG
		}

		Pass// 1 == DENSITY
		{
			CGPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID);
				int volumeDensity = _VolumeDensity[volumeIdx];

				float3 worldPos = VolumeIndexToWorld(volumeIdx);
				if (volumeDensity == 0)
				{
					worldPos += 1e7;// just push far away
				}

				DebugVaryings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(ColorizeRamp(volumeDensity, 32 * DENSITY_SCALE), 1.0);
				return output;
			}

			ENDCG
		}

		Pass// 2 == GRADIENT
		{
			CGPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID >> 1);
				float3 worldPos = VolumeIndexToWorld(volumeIdx);
				if (vertexID & 1)
				{
					worldPos += _VolumeGradient[volumeIdx] * 0.002;
				}

				DebugVaryings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(ColorizeRamp(1 - vertexID, 2), 1.0);
				return output;
			}

			ENDCG
		}

		Pass// 3 == SLICE
		{
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag_Slice

			float3 ColorizeDensity(float d)
			{
				return saturate(d.xxx);
			}

			float3 ColorizeGradient(float3 n)
			{
				float d = dot(n, n);
				if (d > 1e-4)
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
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(uvw, 1);
				return output;
			}

			float4 DebugFrag_Slice(DebugVaryings input) : SV_Target
			{
				float3 uvw = input.color.xyz;
				float3 localPos = VolumeUVWToLocal(uvw);
				float3 localPosFloor = floor(localPos);

				uint3 volumeIdx = localPosFloor;
				float volumeDensity = _VolumeDensity[volumeIdx] / DENSITY_SCALE;
#if DENSITY_TRILINEAR
				float4 volumeVelocity = _VolumeVelocity.SampleLevel(sampler_trilinear_clamp, uvw, 0);
#else
				float4 volumeVelocity = _VolumeVelocity[volumeIdx];
#endif
				float3 volumeGradient = _VolumeGradient.SampleLevel(sampler_trilinear_clamp, uvw, 0);
				float volumeDivergence = _VolumeDivergence.SampleLevel(sampler_trilinear_clamp, uvw, 0);
				float volumePressure = _VolumePressure.SampleLevel(sampler_trilinear_clamp, uvw, 0);
				float3 volumePressureGradient = _VolumePressureGradient.SampleLevel(sampler_trilinear_clamp, uvw, 0);
				float3 volumeVelocitySolenoidal = _VolumeVelocitySolenoidal.SampleLevel(sampler_trilinear_clamp, uvw, 0);

				const float opacity = 0.9;
				{
					float3 gridDist = abs(localPos - localPosFloor);
					float3 gridWidth = fwidth(localPos);
					if (any(gridDist < gridWidth))
					{
						uint i = volumeIdx[_DebugSliceAxis] % 3;
						return float4(0.2 * float3(i == 0, i == 1, i == 2), opacity);
						//return float4(0.2 * ColorizeRamp(volumeIdx[_DebugSliceAxis], _VolumeCells[_DebugSliceAxis]), opacity);
					}
				}

				float x = uvw.x + _DebugSliceDivider;
				if (x < 1.0)
					return float4(ColorizeDensity(volumeDensity), opacity);
				else if (x < 2.0)
					return float4(ColorizeGradient(volumeGradient), opacity);
				else
					return float4(ColorizeVelocity(volumeVelocity.xyz), opacity);
			}

			ENDCG
		}

		Pass// 4 == STRANDS MOTION
		{
			ZTest Equal
			ZWrite Off

			CGPROGRAM

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
				float4 worldPos1 = float4(_ParticlePosition[i].xyz - _WorldSpaceCameraPos, 1.0);
				float4 worldPos0 = float4(_ParticlePositionPrev[i].xyz - _WorldSpaceCameraPos, 1.0);

				float4 clipPos1 = mul(_NonJitteredViewProjMatrix, worldPos1);
				float4 clipPos0 = mul(_PrevViewProjMatrix, worldPos0);

				float2 ndc1 = clipPos1.xy / clipPos1.w;
				float2 ndc0 = clipPos0.xy / clipPos0.w;

				DebugVaryings output;
				output.positionCS = mul(_ViewProjMatrix, worldPos1);
				output.color = float4(_CameraMotionVectorsScale * 0.5 * (ndc1 - ndc0), 0, 0);
				return output;
			}

			ENDCG
		}
	}
}
