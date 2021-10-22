Shader "Hidden/HairSimComputeRoots"
{
	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ POINT_RASTERIZATION_NEEDS_PSIZE
	// 0 == when platform does not require explicit psize
	// 1 == when platform requires explicit psize

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
		float3 normalOS : NORMAL;
	};

	struct RootVaryings
	{
		float4 positionCS : SV_POSITION;
#if POINT_RASTERIZATION_NEEDS_PSIZE
		float pointSize : PSIZE;
#endif
	};

	RootVaryings RootVert(RootAttribs attribs, uint i : SV_VertexID)
	{
		_UpdatedRootPosition[i].xyz = mul(_LocalToWorld, float4(attribs.positionOS, 1.0)).xyz;
		_UpdatedRootDirection[i].xyz = normalize(mul(_LocalToWorldInvT, float4(attribs.normalOS, 0.0)).xyz);
		_UpdatedRootFrame[i] = normalize(QMul(_WorldRotation, _InitialRootFrame[i]));

		RootVaryings v;
		v.positionCS = float4(0, 0, 1, 0);// clip
#if POINT_RASTERIZATION_NEEDS_PSIZE
		v.pointSize = 1.0;
#endif
		return v;
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
