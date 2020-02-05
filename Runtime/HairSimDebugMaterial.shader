Shader "Hidden/HairSimDebugDraw"
{
	CGINCLUDE

	#pragma target 5.0
	
	//#include "HairSimComputeConfig.hlsl"
	#define LAYOUT_INTERLEAVED 1
	#define DENSITY_SCALE 8192.0

	uint _StrandCount;
	uint _StrandParticleCount;

	StructuredBuffer<float4> _ParticlePosition;
	StructuredBuffer<float4> _ParticlePositionPrev;

	float4 _DebugColor;
	float _DebugSliceOffset;
	float _DebugSliceDivider;

	float3 _VolumeCells;
	float3 _VolumeWorldMin;
	float3 _VolumeWorldMax;

	Texture3D<int> _VolumeDensity;
	Texture3D<int> _VolumeVelocityX;
	Texture3D<int> _VolumeVelocityY;
	Texture3D<int> _VolumeVelocityZ;
	Texture3D<float3> _VolumeVelocity;
	Texture3D<float3> _VolumeGradient;
	SamplerState sampler_VolumeGradient;

	float4x4 _ViewProjMatrix;
	float4x4 _PrevViewProjMatrix;
	float4x4 _NonJitteredViewProjMatrix;
	float2 _CameraMotionVectorsScale;

	struct DebugVaryings
	{
		float4 positionCS : SV_POSITION;
		float4 color : TEXCOORD0;
		float4 extra : TEXCOORD1;
	};

	float4 DebugFrag(DebugVaryings input) : SV_Target
	{
		return input.color;
	}

	uint3 InstanceVolumeIndex(uint instanceID)
	{
		uint3 cellCount = _VolumeCells;
		uint volumeIdxX = instanceID % cellCount.x;
		uint volumeIdxY = (instanceID / cellCount.x) % cellCount.y;
		uint volumeIdxZ = (instanceID / (cellCount.x * cellCount.y));
		return uint3(volumeIdxX, volumeIdxY, volumeIdxZ);
	}

	float3 VolumeCellSize()
	{
		return (_VolumeWorldMax - _VolumeWorldMin) / (_VolumeCells - 1);
	}

	float3 VolumeIndexWorldPos(uint3 volumeIdx)
	{
		return _VolumeWorldMin + volumeIdx * VolumeCellSize();
	}

	float3 ColorizeCycle(uint index, const uint count)
	{
		const float k = 1.0 / (count - 1);
		float t = k * index;

		// source: https://www.shadertoy.com/view/4ttfRn
		float3 c = 3.0 * float3(abs(t - 0.5), t.xx) - float3(1.5, 1.0, 2.0);
		return 1.0 - c * c;
	}

	float3 ColorizeRepeat(uint index, const uint count)
	{
		index = min(index, count - 1);

		const float k = 1.0 / (count - 1);
		float t = 0.5 - 0.5 * k * index;// 0.5 * (1.0 - (k * index));

		// source: https://www.shadertoy.com/view/4ttfRn
		float3 c = 2.0 * t - float3(0.0, 1.0, 2.0);
		return 1.0 - c * c;
	}

	float3 ColorizeDensity(int d)
	{
		return d.xxxx / DENSITY_SCALE;
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

				float3 worldPos = _ParticlePosition[strandParticleBegin + strandParticleStride * vertexID].xyz;

				DebugVaryings output;
				output.positionCS = mul(_ViewProjMatrix, float4(worldPos - _WorldSpaceCameraPos, 1.0));
				output.color = float4(ColorizeCycle(instanceID, _StrandCount), 1.0);//_DebugColor;
				output.extra = 0;
				return output;
			}

			ENDCG
		}

		Pass// 1 == DENSITY
		{
			CGPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = InstanceVolumeIndex(instanceID);
				int volumeDensity = _VolumeDensity[volumeIdx];

				float3 worldPos = VolumeIndexWorldPos(volumeIdx);
				if (volumeDensity == 0)
				{
					worldPos += 1e7;// push it far away
				}

				DebugVaryings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(ColorizeRepeat(volumeDensity, 32 * DENSITY_SCALE), 1.0);
				output.extra = 0;
				return output;
			}

			ENDCG
		}

		Pass// 2 == GRADIENT
		{
			CGPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			DebugVaryings DebugVert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = InstanceVolumeIndex(instanceID);

				float3 worldPos = VolumeIndexWorldPos(volumeIdx);
				if (vertexID == 1)
				{
					worldPos += _VolumeGradient[volumeIdx] * 0.002;
				}

				DebugVaryings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(ColorizeRepeat(1 - vertexID, 2), 1.0);
				output.extra = 0;
				return output;
			}

			ENDCG
		}

		Pass// 3 == SLICE
		{
			CGPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag_Slice

			DebugVaryings DebugVert(uint vertexID : SV_VertexID)
			{
				float3 uvw = 0;
				switch (vertexID)
				{
				case 0: uvw = float3(0, 0, _DebugSliceOffset); break;
				case 1: uvw = float3(1, 0, _DebugSliceOffset); break;
				case 2: uvw = float3(1, 1, _DebugSliceOffset); break;
				case 3: uvw = float3(0, 1, _DebugSliceOffset); break;
				}

				float3 worldPos = lerp(_VolumeWorldMin, _VolumeWorldMax, uvw);

				DebugVaryings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(uvw, 1);
				output.extra = 0;
				return output;
			}

			float4 DebugFrag_Slice(DebugVaryings input) : SV_Target
			{
				float3 uvw = input.color.xyz;
				float3 localPos = uvw * (_VolumeCells - 1);
				float3 localPosQuantized = round(localPos);

				uint3 volumeIdx = localPosQuantized;
				int volumeDensity = _VolumeDensity[volumeIdx];
				float3 volumeGradient = _VolumeGradient.SampleLevel(sampler_VolumeGradient, uvw, 0);
				float3 volumeVelocity = _VolumeVelocity.SampleLevel(sampler_VolumeGradient, uvw, 0);
				//float3 volumeVelocity = float3(
				//	_VolumeVelocityX[volumeIdx],
				//	_VolumeVelocityY[volumeIdx],
				//	_VolumeVelocityZ[volumeIdx]) / (float)(1 + volumeDensity);

				float x = uvw.x + _DebugSliceDivider;
				if (x < 1.0)
					return float4(ColorizeDensity(volumeDensity), 1.0);
				else if (x < 2.0)
					return float4(ColorizeGradient(volumeGradient), 1.0);
				else
					return float4(ColorizeVelocity(volumeVelocity), 1.0);
			}

			ENDCG
		}

		Pass// 4 == STRANDS MOTION
		{
			Cull Off
			ZTest Equal
			ZWrite Off
			Offset 0, -1

			CGPROGRAM

			#pragma vertex DebugVert
			#pragma fragment DebugFrag_Motion

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

				DebugVaryings output;
				output.positionCS = mul(_ViewProjMatrix, worldPos1);
				output.color = clipPos1;
				output.extra = clipPos0;
				return output;
			}

			float4 DebugFrag_Motion(DebugVaryings input) : SV_Target
			{
				float2 ndc1 = input.color.xy / input.color.w;
				float2 ndc0 = input.extra.xy / input.extra.w;
				return float4(_CameraMotionVectorsScale * 0.5 * (ndc1 - ndc0), 0, 0);
			}

			ENDCG
		}
	}
}
