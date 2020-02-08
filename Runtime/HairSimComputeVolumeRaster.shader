Shader "Hidden/HairSimComputeVolumeRaster"
{
	CGINCLUDE

	#pragma target 5.0
	
	//#include "HairSimComputeConfig.hlsl"
	#define LAYOUT_INTERLEAVED 1
	#define DENSITY_SCALE 8192.0

	uint _StrandCount;
	uint _StrandParticleCount;

	StructuredBuffer<float4> _ParticlePosition;
	StructuredBuffer<float4> _ParticleVelocity;

	float3 _VolumeCells;
	float3 _VolumeWorldMin;
	float3 _VolumeWorldMax;

	struct Varyings
	{
		uint i : TEXCOORD0;
	};

	struct SliceVaryings
	{
		float4 volumePos : SV_POSITION;
		uint volumeSlice : SV_RenderTargetArrayIndex;
		float2 valuePos : TEXCOORD0;
		float4 value : TEXCOORD1;// xyz = velocity, w = density
	};

	float3 ParticleVolumePosition(uint i)
	{
		float3 worldPos = _ParticlePosition[i].xyz;
		float3 localUVW = (worldPos - _VolumeWorldMin) / (_VolumeWorldMax - _VolumeWorldMin);
		float3 localPos = (_VolumeCells - 1) * saturate(localUVW);
		return localPos;
	}

	void AddVertex(inout TriangleStream<SliceVaryings> outStream, float2 pos, float4 value, float2 valuePos, uint slice)
	{
		SliceVaryings output;
		output.volumePos = float4(pos, 0.0, 1.0);
		output.volumeSlice = slice;
		output.valuePos = valuePos;
		output.value = value;
		outStream.Append(output);
	}

	uint Vert(uint vertexID : SV_VertexID) : TEXCOORD0
	{
		return vertexID;
	}

	[maxvertexcount(8)]
	void Geom(point uint input[1] : TEXCOORD0, inout TriangleStream<SliceVaryings> outStream)
	{
		const float3 volumePos = ParticleVolumePosition(input[0]);
		const float3 volumePosFloor = floor(volumePos);

		/*

		1-------------------2
		|                .´ |
		|    G---------G    |
		|    |  x      |    |
		|    |    C    |    |
		|    |         |    |
		|    G---------G    |
		| .´                |
		3-------------------4

		# = triangle vertices
		G = grid vertices
		C = cell center
		x = particle

		*/

		const float2 cellSize = 1.0 / (_VolumeCells.xy - 1);
		const float2 pos0 = float2(cellSize.xy * (volumePosFloor.xy - 0.5));
		const float3 posH = float3(cellSize.xy * 2.0, 0.0);

		const float2 ndc0 = 2.0 * float2(pos0.x, 1.0 - pos0.y) - 1.0;
		const float3 ndcH = 2.0 * float3(posH.x, -posH.y, 0.0);

		const uint slice0 = volumePosFloor.z;
		const uint slice1 = slice0 + 1;

		const float4 value = float4(_ParticleVelocity[input[0]].xyz, 1.0);

		const float w1 = volumePos.z - volumePosFloor.z;
		const float w0 = 1.0 - w1;

		AddVertex(outStream, ndc0 + ndcH.zz, value * w0, volumePos.xy, slice0);
		AddVertex(outStream, ndc0 + ndcH.xz, value * w0, volumePos.xy, slice0);
		AddVertex(outStream, ndc0 + ndcH.zy, value * w0, volumePos.xy, slice0);
		AddVertex(outStream, ndc0 + ndcH.xy, value * w0, volumePos.xy, slice0);
		outStream.RestartStrip();

		AddVertex(outStream, ndc0 + ndcH.zz, value * w1, volumePos.xy, slice1);
		AddVertex(outStream, ndc0 + ndcH.xz, value * w1, volumePos.xy, slice1);
		AddVertex(outStream, ndc0 + ndcH.zy, value * w1, volumePos.xy, slice1);
		AddVertex(outStream, ndc0 + ndcH.xy, value * w1, volumePos.xy, slice1);
		outStream.RestartStrip();
	}

	float4 Frag(SliceVaryings input) : SV_Target
	{
		float2 d = 1.0 - saturate(abs(input.valuePos - input.volumePos.xy + 0.5));
		return d.x * d.y * input.value;
	}

	ENDCG

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest Always
		ZWrite Off
		Blend One One

		Pass
		{
			CGPROGRAM

			#pragma vertex Vert
			#pragma geometry Geom
			#pragma fragment Frag

			ENDCG
		}
	}
}
