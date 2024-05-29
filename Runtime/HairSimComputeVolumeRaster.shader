Shader "Hidden/HairSimComputeVolumeRaster"
{
	HLSLINCLUDE

	#pragma target 5.0

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

	#include "HairSimData.hlsl"
	#include "HairSimComputeConfig.hlsl"
	#include "HairSimComputeVolumeUtility.hlsl"
	#include "HairSimComputeVolumeTransfer.hlsl"

	struct SliceVaryings
	{
		float4 volumePos : SV_POSITION;
		uint volumeSlice : SV_RenderTargetArrayIndex;
		nointerpolation float2 valuePos : TEXCOORD0;
		nointerpolation float4 value : TEXCOORD1;// xyz = velocity, w = density
		float pointSize : PSIZE;
	};

	SliceVaryings MakeVertex(float2 pos, float4 value, float2 valuePos, uint slice)
	{
		SliceVaryings output;
		output.volumePos = float4(pos, 0.0, 1.0);
		output.volumeSlice = slice;
		output.valuePos = valuePos;
		output.value = value;
		output.pointSize = 1.0;
		return output;
	}

#if !SHADER_API_METAL
	uint SliceVert(uint vertexID : SV_VertexID) : TEXCOORD0
	{
		return vertexID;
	}

	[maxvertexcount(8)]
	void SliceGeom(point uint vertexID[1] : TEXCOORD0, inout TriangleStream<SliceVaryings> outStream)
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

		const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];

		const float3 localPos = VolumeWorldToLocal(lodGrid, _ParticlePosition[vertexID[0]].xyz) - 0.5;// subtract offset to cell center
		const float3 localPosFloor = floor(localPos);

		const float2 uvCellSize = 1.0 / lodGrid.volumeCellCount.xy;
		const float2 uv0 = uvCellSize * localPosFloor.xy;
		const float2 uvH = uvCellSize * 2.0;

		const float2 ndc0 = 2.0 * float2(uv0.x, 1.0 - uv0.y) - 1.0;
		const float3 ndcH = 2.0 * float3(uvH.x, -uvH.y, 0.0);

		const uint slice0 = localPosFloor.z;
		const uint slice1 = slice0 + 1;

		const float w1 = localPos.z - localPosFloor.z;
		const float w0 = 1.0 - w1;

		const float4 v = float4(_ParticleVelocity[vertexID[0]], GetParticleVolumeWeight(vertexID[0]));
		const float4 value = float4((v.xyz * v.w), v.w);

		outStream.Append(MakeVertex(ndc0 + ndcH.zz, value * w0, localPos.xy, slice0));
		outStream.Append(MakeVertex(ndc0 + ndcH.xz, value * w0, localPos.xy, slice0));
		outStream.Append(MakeVertex(ndc0 + ndcH.zy, value * w0, localPos.xy, slice0));
		outStream.Append(MakeVertex(ndc0 + ndcH.xy, value * w0, localPos.xy, slice0));
		outStream.RestartStrip();

		outStream.Append(MakeVertex(ndc0 + ndcH.zz, value * w1, localPos.xy, slice1));
		outStream.Append(MakeVertex(ndc0 + ndcH.xz, value * w1, localPos.xy, slice1));
		outStream.Append(MakeVertex(ndc0 + ndcH.zy, value * w1, localPos.xy, slice1));
		outStream.Append(MakeVertex(ndc0 + ndcH.xy, value * w1, localPos.xy, slice1));
		outStream.RestartStrip();
	}
#endif

	SliceVaryings SliceVertNoGS(uint vertexID : SV_VertexID)
	{
		// generates 2 quads per particle
		// 
		//            <----- particle 0 ----->    <----- particle 1 ----->
		// vertexID   0  1  2  3    4  5  6  7    8  9 10 11   12 13 14 15
		// i          0  0  0  0    0  0  0  0    1  1  1  1    1  1  1  1    (vertexID >> 3)
		// j          0  0  0  0    1  1  1  1    0  0  0  0    1  1  1  1    (vertexID >> 2) & 1
		// uvID       0  1  2  3    0  1  2  3    0  1  2  3    0  1  2  3    (vertexID & 3)

		const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];

		const uint i = (vertexID >> 3);
		const uint j = (vertexID >> 2) & 1;

		const uint uvID = (vertexID & 3);
		const float2 uv = float2(((uvID >> 1) ^ uvID) & 1, uvID >> 1);

		const float3 localPos = VolumeWorldToLocal(lodGrid, _ParticlePosition[i].xyz) - 0.5;// subtract offset to cell center
		const float3 localPosFloor = floor(localPos);

		const float2 uvCellSize = 1.0 / lodGrid.volumeCellCount.xy;
		const float2 uv0 = uvCellSize * localPosFloor.xy;
		const float2 uvH = uvCellSize * 2.0;

		const float2 ndc0 = 2.0 * float2(uv0.x, 1.0 - uv0.y) - 1.0;
		const float2 ndcH = 2.0 * float2(uvH.x, -uvH.y);

		const float w1 = localPos.z - localPosFloor.z;
		const float w0 = 1.0 - w1;

		const float4 v = float4(_ParticleVelocity[i], GetParticleVolumeWeight(i));

		SliceVaryings output;
		output.volumePos = float4(ndc0 + ndcH * uv, 0.0, 1.0);
		output.volumeSlice = localPosFloor.z + j;
		output.valuePos = localPos.xy;
		output.value = float4((v.xyz * v.w), v.w) * lerp(w0, w1, j);
		output.pointSize = 1.0;
		return output;
	}

	float4 SliceFrag(SliceVaryings input) : SV_Target
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

#if !SHADER_API_METAL
			#pragma vertex SliceVert
			#pragma geometry SliceGeom
			#pragma fragment SliceFrag
#endif

			ENDHLSL
		}

		Pass
		{
			HLSLPROGRAM

			#pragma vertex SliceVertNoGS
			#pragma fragment SliceFrag

			ENDHLSL
		}
	}

	Fallback Off
}
