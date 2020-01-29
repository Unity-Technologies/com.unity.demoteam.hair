Shader "Hidden/HairSimDebugDraw"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest LEqual
		ZWrite Off
		Offset 0, -1

		Pass
		{
			CGPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag

			#define LAYOUT_INTERLEAVED 1

			uint _StrandCount;
			uint _StrandParticleCount;

			StructuredBuffer<float3> _ParticlePosition;
			StructuredBuffer<float3> _ParticleVelocity;

			float4 _DebugColor;

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
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
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				return _DebugColor;
			}

			ENDCG
		}
	}
}
