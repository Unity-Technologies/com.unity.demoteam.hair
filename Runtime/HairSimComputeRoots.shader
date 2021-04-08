Shader "Hidden/HairSimComputeRoots"
{
	HLSLINCLUDE

	#pragma target 5.0

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

	#include "HairSimData.hlsl"
	#include "HairSimData.cs.hlsl"
	#include "HairSimComputeSolverQuaternion.hlsl"

	RWStructuredBuffer<float4> _UpdatedRootPosition : register(u1);
	RWStructuredBuffer<float4> _UpdatedRootDirection : register(u2);
	RWStructuredBuffer<float4> _UpdatedRootFrame : register(u3);

	struct RootAttribs
	{
		float3 positionOS : POSITION;
		float3 directionOS : NORMAL;
	};

	float4 RootVert(RootAttribs attribs, uint i : SV_VertexID) : SV_Position
	{
		_UpdatedRootPosition[i].xyz = mul(_LocalToWorld, float4(attribs.positionOS, 1.0)).xyz;
		_UpdatedRootDirection[i].xyz = normalize(mul(_LocalToWorldInvT, float4(attribs.directionOS, 0.0)).xyz);
		_UpdatedRootFrame[i] = QMul(_WorldRotation, _InitialRootFrame[i]);
		return float4(0, 0, 1, 0);// clip
	}

	void RootFragDiscard()
	{
		discard;
	}

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Cull Off
		ZTest Always
		ZWrite Off

		Pass
		{
			HLSLPROGRAM

			#pragma vertex RootVert
			#pragma fragment RootFragDiscard

			ENDHLSL
		}
	}
}
