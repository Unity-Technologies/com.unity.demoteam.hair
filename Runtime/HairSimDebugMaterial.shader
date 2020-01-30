Shader "Hidden/HairSimDebugDraw"
{
	CGINCLUDE

	#define LAYOUT_INTERLEAVED 1

	#pragma vertex Vert
	#pragma fragment Frag

	uint _StrandCount;
	uint _StrandParticleCount;

	StructuredBuffer<float3> _ParticlePosition;
	StructuredBuffer<float3> _ParticleVelocity;

	float4 _DebugColor;

	float3 _VolumeSize;
	float3 _VolumeWorldMin;
	float3 _VolumeWorldMax;

	Texture3D<uint> _VolumeDensity;
	Texture3D<float3> _VolumeGradient;
		
	ENDCG

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Pass// 0 == debug draw strands
		{
			Cull Off
			ZTest LEqual
			ZWrite On
			Offset 0, -1

			CGPROGRAM

			float3 Colorize(uint instanceID)
			{
				const float k = 1.0 / (_StrandCount - 1);
				float t = k * instanceID;

				// source: https://www.shadertoy.com/view/4ttfRn
				float3 c = 3.0 * float3(abs(t - 0.5), t.xx) - float3(1.5, 1.0, 2.0);
				return 1.0 - c * c;
			}

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 color : TEXCOORD0;
			};

			Varyings Vert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
#if LAYOUT_INTERLEAVED
				const uint strandParticleBegin = instanceID;
				const uint strandParticleStride = _StrandCount;
#else
				const uint strandParticleBegin = instanceID * _StrandParticleCount;
				const uint strandParticleStride = 1;
#endif

				float3 vertex = _ParticlePosition[strandParticleBegin + strandParticleStride * vertexID];

				Varyings output;
				output.positionCS = UnityObjectToClipPos(float4(vertex, 1.0));
				output.color = float4(Colorize(instanceID), 1.0);//_DebugColor;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				return input.color;
			}

			ENDCG
		}

		Pass// 1 == debug draw volume
		{
			Cull Off
			ZTest LEqual
			ZWrite Off

			CGPROGRAM

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 color : TEXCOORD0;
			};

			Varyings Vert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
			{
				uint3 volumeSize = _VolumeSize;

				uint volumeIdxX = instanceID % volumeSize.x;
				uint volumeIdxY = (instanceID / volumeSize.x) % volumeSize.y;
				uint volumeIdxZ = (instanceID / (volumeSize.x * volumeSize.y));
				uint3 volumeIdx = uint3(volumeIdxX, volumeIdxY, volumeIdxZ);

				float3 worldPos = _VolumeWorldMin + volumeIdx * ((_VolumeWorldMax - _VolumeWorldMin) / _VolumeSize);
				float density = _VolumeDensity[volumeIdx] * 2.0;

				Varyings output;
				output.positionCS = UnityObjectToClipPos(float4(worldPos, 1.0));
				output.color = float4(density, saturate(1.0-density), 0.0, 1.0);
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				return input.color;
			}

			ENDCG
		}
	}
}
