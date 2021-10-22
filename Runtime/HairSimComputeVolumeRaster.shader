Shader "Hidden/HairSimComputeVolumeRaster"
{
	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ POINT_RASTERIZATION_NEEDS_PSIZE
	// 0 == when platform does not require explicit psize
	// 1 == when platform requires explicit psize

	#include "HairSimData.hlsl"
	#include "HairSimComputeConfig.hlsl"
	#include "HairSimComputeVolumeUtility.hlsl"

	struct Varyings
	{
		float4 volumePos : SV_POSITION;
		uint volumeSlice : SV_RenderTargetArrayIndex;
		nointerpolation float2 valuePos : TEXCOORD0;
		nointerpolation float4 value : TEXCOORD1;// xyz = velocity, w = density
#if POINT_RASTERIZATION_NEEDS_PSIZE
		float pointSize : PSIZE;
#endif
	};

	void MakeVertex(inout TriangleStream<Varyings> outStream, float2 pos, float4 value, float2 valuePos, uint slice)
	{
		Varyings output;
		output.volumePos = float4(pos, 0.0, 1.0);
		output.volumeSlice = slice;
		output.valuePos = valuePos;
		output.value = value;
#if POINT_RASTERIZATION_NEEDS_PSIZE
		output.pointSize = 1.0;
#endif
		outStream.Append(output);
	}

	uint Vert(uint vertexID : SV_VertexID) : TEXCOORD0
	{
		return vertexID;
	}

	[maxvertexcount(8)]
	void Geom(point uint vertexID[1] : TEXCOORD0, inout TriangleStream<Varyings> outStream)
	{
		// generates 2 quads per particle
		//
		//   3---------+---------4
		//   | `.      :         |
		//   |    C    :    C    |
		//   |      `. :         |
		//   + - - - - + - - - - +
		//   |       x : `.      |
		//   |    C    :    C    |
		//   |         :      `. |
		//   1---------+---------2
		//
		// where
		//
		//   # = triangle vertices
		//   C = cell centers
		//   x = particle

		const float3 localPos = VolumeWorldToLocal(_ParticlePosition[vertexID[0]].xyz) - 0.5;// subtract offset to cell center
		const float3 localPosFloor = floor(localPos);

		const float2 uvCellSize = 1.0 / _VolumeCells.xy;
		const float2 uv0 = uvCellSize * localPosFloor.xy;
		const float2 uvH = uvCellSize * 2.0;

		const float2 ndc0 = 2.0 * float2(uv0.x, 1.0 - uv0.y) - 1.0;
		const float3 ndcH = 2.0 * float3(uvH.x, -uvH.y, 0.0);

		const uint slice0 = localPosFloor.z;
		const uint slice1 = slice0 + 1;

		const float w1 = localPos.z - localPosFloor.z;
		const float w0 = 1.0 - w1;

		const float4 v = _ParticleVelocity[vertexID[0]];
		const float4 value = float4((v.xyz * v.w), v.w);

		MakeVertex(outStream, ndc0 + ndcH.zz, value * w0, localPos.xy, slice0);
		MakeVertex(outStream, ndc0 + ndcH.xz, value * w0, localPos.xy, slice0);
		MakeVertex(outStream, ndc0 + ndcH.zy, value * w0, localPos.xy, slice0);
		MakeVertex(outStream, ndc0 + ndcH.xy, value * w0, localPos.xy, slice0);
		outStream.RestartStrip();

		MakeVertex(outStream, ndc0 + ndcH.zz, value * w1, localPos.xy, slice1);
		MakeVertex(outStream, ndc0 + ndcH.xz, value * w1, localPos.xy, slice1);
		MakeVertex(outStream, ndc0 + ndcH.zy, value * w1, localPos.xy, slice1);
		MakeVertex(outStream, ndc0 + ndcH.xy, value * w1, localPos.xy, slice1);
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

		const float3 localPos = VolumeWorldToLocal(_ParticlePosition[i].xyz) - 0.5;// subtract offset to cell center
		const float3 localPosFloor = floor(localPos);

		const float2 uvCellSize = 1.0 / _VolumeCells.xy;
		const float2 uv0 = uvCellSize * localPosFloor.xy;
		const float2 uvH = uvCellSize * 2.0;

		const float2 ndc0 = 2.0 * float2(uv0.x, 1.0 - uv0.y) - 1.0;
		const float2 ndcH = 2.0 * float2(uvH.x, -uvH.y);

		const float w1 = localPos.z - localPosFloor.z;
		const float w0 = 1.0 - w1;

		const float4 v = _ParticleVelocity[i];

		Varyings output;
		output.volumePos = float4(ndc0 + ndcH * uv, 0.0, 1.0);
		output.volumeSlice = localPosFloor.z + j;
		output.valuePos = localPos.xy;
		output.value = float4((v.xyz * v.w), v.w) * lerp(w0, w1, j);
#if POINT_RASTERIZATION_NEEDS_PSIZE
		output.pointSize = 1.0;
#endif
		return output;
	}

	float4 Frag(Varyings input) : SV_Target
	{
		float2 d = 1.0 - saturate(abs(input.valuePos - input.volumePos.xy + 0.5));
		return d.x * d.y * input.value;
	}

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest Always
		ZWrite Off
		Blend One One

		Pass
		{
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma geometry Geom
			#pragma fragment Frag

			ENDHLSL
		}

		Pass
		{
			HLSLPROGRAM

			#pragma vertex VertDirect
			#pragma fragment Frag

			ENDHLSL
		}
	}
}
