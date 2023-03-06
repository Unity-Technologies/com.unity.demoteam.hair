Shader "Hidden/HairSimComputeRoots"
{
	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ POINT_RASTERIZATION_NEEDS_PSIZE
	// 0 == when platform does not require explicit psize
	// 1 == when platform requires explicit psize

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

	#include "HairSimData.hlsl"
	#include "HairSimComputeSolverQuaternion.hlsl"

	RWStructuredBuffer<float4> _UpdatedRootPosition : register(u1);
	RWStructuredBuffer<float4> _UpdatedRootFrame : register(u2);

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
		// update root position
		_UpdatedRootPosition[strandIndex].xyz = mul(_RootTransform, float4(attribs.positionOS, 1.0)).xyz;

		// update root frame
		{
			// add direction
			float3 localRootDir = attribs.normalOS;
			float4 localRootFrame = MakeQuaternionFromTo(float3(0.0, 1.0, 0.0), localRootDir);

		#if 1
			// add twist from skinning delta
			float4 skinningBoneLocalDelta = QMul(_RootRotationInv, _WorldRotation);
			float4 skinningBoneLocalTwist = QDecomposeTwist(skinningBoneLocalDelta, localRootDir);
			localRootFrame = QMul(skinningBoneLocalTwist, localRootFrame);
		#endif

			// output world frame
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

	Fallback Off
}
