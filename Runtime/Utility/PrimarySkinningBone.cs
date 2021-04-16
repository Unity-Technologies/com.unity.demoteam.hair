using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	[Serializable]
	public struct PrimarySkinningBone
	{
		public Transform skinningBone;
		public Matrix4x4 skinningBoneBindPose;
		public Matrix4x4 skinningBoneBindPoseInverse;

		public PrimarySkinningBone(Transform transform)
		{
			this.skinningBone = transform;
			this.skinningBoneBindPose = Matrix4x4.identity;
			this.skinningBoneBindPoseInverse = Matrix4x4.identity;

			// search for skinning bone
			var smr = transform.GetComponent<SkinnedMeshRenderer>();
			if (smr != null)
			{
				var skinningBoneIndex = -1;
				var skinningBoneWeight = 0.0f;

				unsafe
				{
					var boneWeights = smr.sharedMesh.GetAllBoneWeights();
					var boneWeightPtr = (BoneWeight1*)boneWeights.GetUnsafeReadOnlyPtr();

					for (int i = 0; i != boneWeights.Length; i++)
					{
						if (skinningBoneWeight < boneWeightPtr[i].weight)
						{
							skinningBoneWeight = boneWeightPtr[i].weight;
							skinningBoneIndex = boneWeightPtr[i].boneIndex;
						}
					}
				}

				if (skinningBoneIndex != -1)
				{
					this.skinningBone = smr.bones[skinningBoneIndex];
					this.skinningBoneBindPose = smr.sharedMesh.bindposes[skinningBoneIndex];
					this.skinningBoneBindPoseInverse = skinningBoneBindPose.inverse;
					//Debug.Log("discovered skinning bone for " + smr.name + " : " + skinningBone.name);
				}
				else if (smr.rootBone != null)
				{
					this.skinningBone = smr.rootBone;
				}
			}
		}

		public Matrix4x4 GetWorldToLocalSkinning()
		{
			return skinningBoneBindPoseInverse * skinningBone.worldToLocalMatrix;
		}

		public Matrix4x4 GetLocalSkinningToWorld()
		{
			return skinningBone.localToWorldMatrix * skinningBoneBindPose;
		}
	}
}
