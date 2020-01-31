Shader "Hidden/HairSimDebugDraw"
{
	CGINCLUDE

	#define LAYOUT_INTERLEAVED 1

	#pragma vertex DebugVert
	#pragma fragment DebugFrag

	uint _StrandCount;
	uint _StrandParticleCount;

	StructuredBuffer<float3> _ParticlePosition;
	StructuredBuffer<float3> _ParticleVelocity;

	float4 _DebugColor;

	float3 _VolumeCells;
	float3 _VolumeWorldMin;
	float3 _VolumeWorldMax;

	Texture3D<int> _VolumeDensity;
	Texture3D<float3> _VolumeGradient;
		
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
		float t = k * index;

		// source: https://www.shadertoy.com/view/4ttfRn
		float3 c = 3.0 * float3(abs(t - 0.5), t.xx) - float3(1.5, 1.0, 2.0);
		return 1.0 - c * c;
	}

	float3 ColorizeRamp(uint index, const uint count)
	{
		index = min(index, count - 1);

		const float k = 1.0 / (count - 1);
		float t = 0.5 - 0.5 * k * index;// 0.5 * (1.0 - (k * index));

		// source: https://www.shadertoy.com/view/4ttfRn
		float3 c = 2.0 * t - float3(0.0, 1.0, 2.0);
		return 1.0 - c * c;
	}

	uint3 InstanceVolumeIndex(uint instanceID)
	{
		uint3 cellCount = _VolumeCells;
		uint volumeIdxX = instanceID % cellCount.x;
		uint volumeIdxY = (instanceID / cellCount.x) % cellCount.y;
		uint volumeIdxZ = (instanceID / (cellCount.x * cellCount.y));
		return uint3(volumeIdxX, volumeIdxY, volumeIdxZ);
	}

	float3 VolumeIndexWorldPos(uint3 volumeIdx)
	{
		return _VolumeWorldMin + volumeIdx * ((_VolumeWorldMax - _VolumeWorldMin) / _VolumeCells);
	}

	float3 VolumeCellSize()
	{
		return (_VolumeWorldMax - _VolumeWorldMin) / _VolumeCells;
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

			DebugVaryings DebugVert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
#if LAYOUT_INTERLEAVED
				const uint strandParticleBegin = instanceID;
				const uint strandParticleStride = _StrandCount;
#else
				const uint strandParticleBegin = instanceID * _StrandParticleCount;
				const uint strandParticleStride = 1;
#endif

				float3 worldPos = _ParticlePosition[strandParticleBegin + strandParticleStride * vertexID];

				DebugVaryings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(ColorizeCycle(instanceID, _StrandCount), 1.0);//_DebugColor;
				return output;
			}

			ENDCG
		}

		Pass// 1 == DENSITY
		{
			CGPROGRAM

			DebugVaryings DebugVert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = InstanceVolumeIndex(instanceID);
				int volumeDensity = max(0, _VolumeDensity[volumeIdx]);

				float3 worldPos = VolumeIndexWorldPos(volumeIdx);
				if (volumeDensity == 0)
				{
					worldPos += 1e7;// push it far away
				}

				DebugVaryings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(ColorizeRamp(volumeDensity, 255), 1);
				return output;
			}

			ENDCG
		}

		Pass// 2 == GRADIENT
		{
			CGPROGRAM

			DebugVaryings DebugVert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
				uint3 volumeIdx = InstanceVolumeIndex(instanceID);
				float volumeDensity = _VolumeDensity[volumeIdx];

				float3 worldPos = VolumeIndexWorldPos(volumeIdx);
				if (vertexID == 1)
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
	}
}
