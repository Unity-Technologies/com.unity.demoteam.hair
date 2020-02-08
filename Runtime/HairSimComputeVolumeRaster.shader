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
		float4 cellValue : TEXCOORD0;// xyz = velocity, w = density
		float2 cellOffset : TEXCOORD1;
	};

	float3 ParticleVolumePosition(uint i)
	{
		float3 worldPos = _ParticlePosition[i].xyz;
		float3 localUVW = (worldPos - _VolumeWorldMin) / (_VolumeWorldMax - _VolumeWorldMin);
		float3 localPos = (_VolumeCells - 1) * saturate(localUVW);
		return localPos;
	}

	void MakeVertex(inout TriangleStream<SliceVaryings> outStream, float2 uv, float4 value, float2 offset, uint slice)
	{
		SliceVaryings output;
		output.volumePos = float4(2.0 * float2(uv.x, 1.0 - uv.y) - 1.0, 0.0, 1.0);
		output.volumeSlice = slice;
		output.cellValue = value;
		output.cellOffset = offset;
		outStream.Append(output);
	}

	Varyings Vert(uint vertexID : SV_VertexID)
	{
		Varyings output;
		output.i = vertexID + 128;
		return output;
	}

	[maxvertexcount(8)]
	void Geom(point Varyings input[1], inout TriangleStream<SliceVaryings> outStream)
	{
		const float2 volumeCellSize = 1.0 / (_VolumeCells.xy - 1);
		const float3 volumePos = ParticleVolumePosition(input[0].i);
		const float3 volumePosFloor = floor(volumePos);

		/*

		+-------------------+
		|                .´
		|    +---------+
		|    |         |
		|    |    p    |
		|    |         |
		|    +---------+
		| .´
		+

		*/

		const float2 pos0 = (volumePosFloor.xy - 0.5);
		const float2 pos1 = (volumePosFloor.xy + 1.5);

		//const float2 volumePosDelta = volumePos - volumePosFloor;

		const float2 p0 = pos0 - 0.375;
		const float2 p1 = pos1 - 0.375;

		const uint slice0 = volumePosFloor.z;
		const uint slice1 = slice0 + 1;

		const float2 uv0 = pos0 * volumeCellSize.xy;
		const float2 uv1 = pos1 * volumeCellSize.xy;
		const float3 uvS = float3(uv1 - uv0, 0.0);

		const float4 value = float4(_ParticleVelocity[input[0].i].xyz, 1.0);

		MakeVertex(outStream, uv0 + uvS.zz, 1.0, float2(p0.x, p0.y) - volumePos.xy, slice0);
		MakeVertex(outStream, uv0 + uvS.xz, 1.0, float2(p1.x, p0.y) - volumePos.xy, slice0);
		MakeVertex(outStream, uv0 + uvS.zy, 1.0, float2(p0.x, p1.y) - volumePos.xy, slice0);
		MakeVertex(outStream, uv0 + uvS.xy, 1.0, float2(p1.x, p1.y) - volumePos.xy, slice0);
		outStream.RestartStrip();

		//MakeVertex(outStream, uv0 + uvS.zz, value, float3(w0.x, w0.y, w0.z), slice1);
		//MakeVertex(outStream, uv0 + uvS.xz, value, float3(w1.x, w0.y, w0.z), slice1);
		//MakeVertex(outStream, uv0 + uvS.zy, value, float3(w0.x, w1.y, w0.z), slice1);
		//MakeVertex(outStream, uv0 + uvS.xy, value, float3(w1.x, w1.y, w0.z), slice1);
		//outStream.RestartStrip();
	}

	float4 Frag(SliceVaryings input) : SV_Target
	{
		float2 d = 1.0 - saturate(abs(input.cellOffset));
		return d.x * d.y;
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
