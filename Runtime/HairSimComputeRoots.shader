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

	uint _RootFrameFromTangentFrame;

	struct RootAttribs
	{
		float3 positionOS : POSITION;
		float3 normalOS : NORMAL;
		float4 tangentOS : TANGENT;
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
			if (_RootFrameFromTangentFrame != 0)
			{
				// reconstruct from tangent frame
				float3 localRootDir = attribs.normalOS;
				float3 localRootPerp = attribs.tangentOS.xyz * attribs.tangentOS.w;
				float4 localRootFrame = MakeQuaternionLookAtBasis(float3(0.0, 1.0, 0.0), localRootDir, float3(1.0, 0.0, 0.0), localRootPerp);

				// output world frame
				_UpdatedRootFrame[strandIndex] = normalize(QMul(_RootRotation, localRootFrame));
			}
			else
			{
				// reconstruct partially from direction
				float3 localRootDir = attribs.normalOS;
				float4 localRootFrame = MakeQuaternionFromTo(float3(0.0, 1.0, 0.0), localRootDir);

				// approximate twist from skinning bone delta
				float4 skinningBoneLocalDelta = QMul(_RootRotationInv, _WorldRotation);
				float4 skinningBoneLocalTwist = QDecomposeTwist(skinningBoneLocalDelta, localRootDir);
				{
					localRootFrame = QMul(skinningBoneLocalTwist, localRootFrame);
				}

				// output world frame
				_UpdatedRootFrame[strandIndex] = normalize(QMul(_RootRotation, localRootFrame));
			}
		}

		// clip
		RootVaryings v;
		v.positionCS = float4(0.0, 0.0, 1.0, 0.0);
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
