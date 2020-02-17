Shader "Hidden/HairSimComputeVolumeRaster"
{
	CGINCLUDE

	#pragma target 5.0
	
	#include "HairSimComputeConfig.hlsl"

	uint _StrandCount;
	uint _StrandParticleCount;

	StructuredBuffer<float4> _ParticlePosition;
	StructuredBuffer<float4> _ParticleVelocity;

	float3 _VolumeCells;
	float3 _VolumeWorldMin;
	float3 _VolumeWorldMax;

	struct Varyings
	{
		float4 volumePos : SV_POSITION;
		uint volumeSlice : SV_RenderTargetArrayIndex;
		nointerpolation float2 valuePos : TEXCOORD0;
		nointerpolation float4 value : TEXCOORD1;// xyz = velocity, w = density
	};

	float3 ParticleVolumePosition(uint i)
	{
		float3 worldPos = _ParticlePosition[i].xyz;
		float3 localUVW = (worldPos - _VolumeWorldMin) / (_VolumeWorldMax - _VolumeWorldMin);
		float3 localPos = (_VolumeCells - 1) * saturate(localUVW);
		return localPos;
	}

	uint Vert(uint vertexID : SV_VertexID) : TEXCOORD0
	{
		return vertexID;
	}

	void MakeVertex(inout TriangleStream<Varyings> outStream, float2 pos, float4 value, float2 valuePos, uint slice)
	{
		Varyings output;
		output.volumePos = float4(pos, 0.0, 1.0);
		output.volumeSlice = slice;
		output.valuePos = valuePos;
		output.value = value;
		outStream.Append(output);
	}

	[maxvertexcount(8)]
	void Geom(point uint vertexID[1] : TEXCOORD0, inout TriangleStream<Varyings> outStream)
	{
		// generates triangle strip
		//
		//   3-------------------4
		//   | `.                |
		//   |    G---------G    |
		//   |    |  x      |    |
		//   |    |    C    |    |
		//   |    |         |    |
		//   |    G---------G    |
		//   |                `. |
		//   1-------------------2
		//
		// where
		//
		//   # = triangle vertices
		//   G = grid vertices
		//   C = cell center
		//   x = particle

		const float3 volumePos = ParticleVolumePosition(vertexID[0]);
		const float3 volumePosFloor = floor(volumePos);

		const float2 cellSize = 1.0 / (_VolumeCells.xy - 1);
		const float2 pos0 = float2(cellSize.xy * (volumePosFloor.xy - 0.5));
		const float2 posH = float2(cellSize.xy * 2.0);

		const float2 ndc0 = 2.0 * float2(pos0.x, 1.0 - pos0.y) - 1.0;
		const float3 ndcH = 2.0 * float3(posH.x, -posH.y, 0.0);

		const uint slice0 = volumePosFloor.z;
		const uint slice1 = slice0 + 1;

		const float w1 = volumePos.z - volumePosFloor.z;
		const float w0 = 1.0 - w1;

		const float4 value = float4(_ParticleVelocity[vertexID[0]].xyz, 1.0);

		MakeVertex(outStream, ndc0 + ndcH.zz, value * w0, volumePos.xy, slice0);
		MakeVertex(outStream, ndc0 + ndcH.xz, value * w0, volumePos.xy, slice0);
		MakeVertex(outStream, ndc0 + ndcH.zy, value * w0, volumePos.xy, slice0);
		MakeVertex(outStream, ndc0 + ndcH.xy, value * w0, volumePos.xy, slice0);
		outStream.RestartStrip();

		MakeVertex(outStream, ndc0 + ndcH.zz, value * w1, volumePos.xy, slice1);
		MakeVertex(outStream, ndc0 + ndcH.xz, value * w1, volumePos.xy, slice1);
		MakeVertex(outStream, ndc0 + ndcH.zy, value * w1, volumePos.xy, slice1);
		MakeVertex(outStream, ndc0 + ndcH.xy, value * w1, volumePos.xy, slice1);
		outStream.RestartStrip();
	}

	Varyings VertDirect(uint vertexID : SV_VertexID)
	{
		// generates 2 quads per particle
		// 
		//            <----- particle 0 ----->    <----- particle 1 ----->
		// vertexID   0  1  2  3    4  5  6  7    8  9 10 11   12 13 14 15
		// i          0  0  0  0    0  0  0  0    1  1  1  1    1  1  1  1    (vertexID >> 3)
		// j          0  0  0  0    1  1  1  1    0  0  0  0    1  1  1  1    (vertexID >> 2) & 1
		// uvID       0  1  2  3    0  1  2  3    0  1  2  3    0  1  2  3    (vertexID & 3)

		const uint i = (vertexID >> 3);
		const uint j = (vertexID >> 2) & 1;

		const uint uvID = (vertexID & 3);
		const float2 uv = float2(((uvID >> 1) ^ uvID) & 1, uvID >> 1);

		const float3 volumePos = ParticleVolumePosition(i);
		const float3 volumePosFloor = floor(volumePos);

		const float2 cellSize = 1.0 / (_VolumeCells.xy - 1);
		const float2 pos0 = float2(cellSize.xy * (volumePosFloor.xy - 0.5));
		const float2 posH = float2(cellSize.xy * 2.0);

		const float2 ndc0 = 2.0 * float2(pos0.x, 1.0 - pos0.y) - 1.0;
		const float2 ndcH = 2.0 * float2(posH.x, -posH.y);

		const float w1 = volumePos.z - volumePosFloor.z;
		const float w0 = 1.0 - w1;

		Varyings output;
		output.volumePos = float4(ndc0 + ndcH * uv, 0.0, 1.0);
		output.volumeSlice = volumePosFloor.z + j;
		output.valuePos = volumePos.xy;
		output.value = float4(_ParticleVelocity[i].xyz, 1.0) * lerp(w0, w1, j);
		return output;
	}

	float4 Frag(Varyings input) : SV_Target
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

		Pass
		{
			CGPROGRAM

			#pragma vertex VertDirect
			#pragma fragment Frag

			ENDCG
		}
	}
}
