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

	RootVaryings RootVert(RootAttribs attribs, uint strandIndex : SV_VertexID)
	{
		_UpdatedRootPosition[strandIndex].xyz = mul(_RootTransform, float4(attribs.positionOS, 1.0)).xyz;
		_UpdatedRootDirection[strandIndex].xyz = normalize(QMul(_RootRotation, attribs.normalOS));

		// update material frame
		{
		#if 1
			// perturb initial local frame
			float3 localRootReference = QMul(_InitialRootFrame[strandIndex], float3(0.0, 1.0, 0.0));
			float4 localRootPerturb = MakeQuaternionFromTo(localRootReference, attribs.normalOS);
			float4 localRootFrame = QMul(localRootPerturb, _InitialRootFrame[strandIndex]);
		#else
			// construct new local frame
			float4 localRootFrame = MakeQuaternionFromTo(float3(0.0, 1.0, 0.0), attribs.normalOS);
		#endif

		#if 1
			// add twist from skinning delta
			float4 skinningBoneLocalDelta = QMul(_RootRotationInv, _WorldRotation);
			float4 skinningBoneLocalTwist = QDecomposeTwist(skinningBoneLocalDelta, attribs.normalOS);
			localRootFrame = QMul(skinningBoneLocalTwist, localRootFrame);
		#endif

			// done
			_UpdatedRootFrame[strandIndex] = normalize(QMul(_RootRotation, localRootFrame));
		}

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
