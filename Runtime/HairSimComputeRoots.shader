Shader "Hidden/HairSimComputeRoots"
{
	HLSLINCLUDE

	#pragma target 5.0

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

	#include "HairSimData.cs.hlsl"

	RWStructuredBuffer<float4> _UpdateRootPosition : register(u1);
	RWStructuredBuffer<float4> _UpdateRootDirection : register(u2);

	struct RootAttribs
	{
		float3 positionOS : POSITION;
		float3 directionOS : NORMAL;
	};

	void RootVert(RootAttribs attribs, uint i : SV_VertexID)
	{
		_UpdateRootPosition[i].xyz = mul(_LocalToWorld, float4(attribs.positionOS, 1.0)).xyz;
		_UpdateRootDirection[i].xyz = mul(_LocalToWorldInvT, float4(attribs.directionOS, 0.0)).xyz;
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
