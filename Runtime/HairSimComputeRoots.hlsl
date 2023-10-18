#ifndef __HAIRSIMCOMPUTEROOTS_HLSL__
#define __HAIRSIMCOMPUTEROOTS_HLSL__

#include "HairSimData.cs.hlsl"
#include "HairSimComputeSolverQuaternion.hlsl"

void RootResolve(out float3 rootPosition, out float4 rootFrame, const float3 rootMeshPositionOS, const float4 rootMeshTangentOS, const float3 rootMeshNormalOS)
{
	// resolve root position
	rootPosition = mul(_RootMeshMatrix, float4(rootMeshPositionOS, 1.0)).xyz;

	// resolve root frame
	if (_RootMeshTangentStride != 0)
	{
		// reconstruct from tangent frame
		float3 localRootDir = rootMeshNormalOS;
		float3 localRootPerp = rootMeshTangentOS.xyz * rootMeshTangentOS.w;
		float4 localRootFrame = MakeQuaternionLookAtBasis(float3(0.0, 1.0, 0.0), localRootDir, float3(1.0, 0.0, 0.0), localRootPerp);

		// output world frame
		rootFrame = normalize(QMul(_RootMeshRotation, localRootFrame));
	}
	else
	{
		// reconstruct partially from direction
		float3 localRootDir = rootMeshNormalOS;
		float4 localRootFrame = MakeQuaternionFromTo(float3(0.0, 1.0, 0.0), localRootDir);

		// approximate twist from skinning bone delta
		float4 skinningBoneLocalDelta = QMul(_RootMeshRotationInv, _RootMeshSkinningRotation);
		float4 skinningBoneLocalTwist = QDecomposeTwist(skinningBoneLocalDelta, localRootDir);
		{
			localRootFrame = QMul(skinningBoneLocalTwist, localRootFrame);
		}

		// output world frame
		rootFrame = normalize(QMul(_RootMeshRotation, localRootFrame));
	}
}

#endif//__HAIRSIMCOMPUTEROOTS_HLSL__