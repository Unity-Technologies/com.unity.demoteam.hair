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
		float4 volumePos : SV_POSITION;
		float4 volumeVal : TEXCOORD0;// xyz = velocity, w = density
		uint volumeSlice : TEXCOORD1;
	};

	struct Varyings2
	{
		nointerpolation uint i : TEXCOORD0;
	};

	struct SliceVaryings
	{
		float4 volumePos : SV_POSITION;
		nointerpolation float4 volumeVal : TEXCOORD0;// xyz = velocity, w = density
		uint volumeSlice : SV_RenderTargetArrayIndex;
	};

	float3 ParticleVolumePosition(uint i)
	{
		float3 worldPos = _ParticlePosition[i].xyz;
		float3 localUVW = (worldPos - _VolumeWorldMin) / (_VolumeWorldMax - _VolumeWorldMin);
		float3 localPos = (_VolumeCells - 1) * saturate(localUVW);
		return localPos;
	}

	Varyings Vert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
		float3 uvw = float3(((vertexID >> 1) ^ vertexID) & 1, vertexID >> 1, 0);

		float3 velocity = _ParticleVelocity[instanceID];

		float3 volumePos = ParticleVolumePosition(instanceID);
		float3 volumePosFloor = floor(volumePos);
		float3 volumeUVW = (volumePosFloor + uvw) / (_VolumeCells - 1);

		Varyings output;
		output.volumePos = float4(2.0 * float2(volumeUVW.x, 1.0 - volumeUVW.y) - 1.0, 0.5, 1.0);
		output.volumeVal = float4(velocity, 1);
		output.volumeSlice = volumePosFloor.z;
		return output;
	}

	[maxvertexcount(3)]
	void Geom(triangle Varyings input[3], inout TriangleStream<SliceVaryings> outStream)
	{
		SliceVaryings output;
		[unroll(3)]
		for (int i = 0; i < 3; ++i)
		{
			output.volumePos = input[i].volumePos;
			output.volumeVal = input[i].volumeVal;
			output.volumeSlice = input[i].volumeSlice;
			outStream.Append(output);
		}
	}

	void MakeVertex(inout TriangleStream<SliceVaryings> outStream, float2 uv, float4 value, uint slice)
	{
		SliceVaryings output;
		output.volumePos = float4(2.0 * float2(uv.x, 1.0 - uv.y) - 1.0, 0.0, 1.0);
		output.volumeVal = value;
		output.volumeSlice = slice;
		outStream.Append(output);
	}

	Varyings2 Vert2(uint vertexID : SV_VertexID)
	{
		Varyings2 output;
		output.i = vertexID;
		return output;
	}

	[maxvertexcount(6)]
	void Geom2(point Varyings2 input[1], inout TriangleStream<SliceVaryings> outStream)
	{
		const float2 volumeCellSize = 1.0 / (_VolumeCells.xy - 1);
		const float3 volumePos = ParticleVolumePosition(input[0].i);
		const float3 volumePosFloor = floor(volumePos);

		const float3 w1 = 1;// volumePos - volumePosFloor;
		const float3 w0 = 1;// 1.0 - w1;

		const uint slice0 = volumePosFloor.z;
		const uint slice1 = slice0 + 1;

		const float eps = 1e-4;
		float2 pos0 = (volumePosFloor.xy);
		float2 pos1 = 0.25 + (volumePosFloor.xy + 1.0);

		float2 uv0 = float2(pos0.x * volumeCellSize.x, pos0.y * volumeCellSize.y);
		float2 uv1 = float2(pos1.x * volumeCellSize.x, pos1.y * volumeCellSize.y);
		float3 uvS = float3(uv1 - uv0, 0.0);

		//const float3 uvS = float3(volumeCellSize.x, -volumeCellSize.y, 0.0);
		//const float2 uv0 = float2(volumePosFloor.x * volumeCellSize.x, 1.0 - volumePosFloor.y * volumeCellSize.y) - 0.5 * uvS;

		const float4 value = float4(_ParticleVelocity[input[0].i].xyz, 1.0);

		MakeVertex(outStream, uv0 + uvS.zz, value * w0.x * w0.y * w0.z, slice0);
		MakeVertex(outStream, uv0 + uvS.xz, value * w1.x * w0.y * w0.z, slice0);
		MakeVertex(outStream, uv0 + uvS.xy, value * w1.x * w1.y * w0.z, slice0);
		
		MakeVertex(outStream, uv0 + uvS.xy, value * w1.x * w1.y * w0.z, slice0);
		MakeVertex(outStream, uv0 + uvS.zy, value * w0.x * w1.y * w0.z, slice0);
		MakeVertex(outStream, uv0 + uvS.zz, value * w0.x * w0.y * w0.z, slice0);

		//MakeVertex(outStream, uv0 + uvS.zz, value * w0.x * w0.y * w1.z, slice1);
		//MakeVertex(outStream, uv0 + uvS.xz, value * w1.x * w0.y * w1.z, slice1);
		//MakeVertex(outStream, uv0 + uvS.xy, value * w1.x * w1.y * w1.z, slice1);

		//MakeVertex(outStream, uv0 + uvS.xy, value * w1.x * w1.y * w1.z, slice1);
		//MakeVertex(outStream, uv0 + uvS.zy, value * w0.x * w1.y * w1.z, slice1);
		//MakeVertex(outStream, uv0 + uvS.zz, value * w0.x * w0.y * w1.z, slice1);
	}

	float4 Frag(SliceVaryings input) : SV_Target
	{
		return input.volumeVal;
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

			#pragma vertex Vert2
			#pragma geometry Geom2
			#pragma fragment Frag

			ENDCG
		}
	}
}
