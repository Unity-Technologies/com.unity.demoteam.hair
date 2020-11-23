using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.DemoTeam.Attributes;

#if UNITY_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
#endif

#if UNITY_VISUALEFFECTGRAPH
using UnityEngine.VFX;
#endif

namespace Unity.DemoTeam.Hair
{
	[ExecuteAlways, SelectionBase]
	public class Groom : MonoBehaviour
	{
		public static HashSet<Groom> s_instances = new HashSet<Groom>();

		[Serializable]
		public struct ComponentGroup
		{
			public GameObject container;
			public MeshFilter lineFilter;
			public MeshRenderer lineRenderer;
			public MaterialPropertyBlock lineRendererMPB;
			public MeshFilter rootFilter;
#if UNITY_DEMOTEAM_DIGITALHUMAN
			public SkinAttachment rootAttachment;
#endif
		}

		[Serializable]
		public struct SettingsRoots
		{
#if UNITY_DEMOTEAM_DIGITALHUMAN
			[ToggleGroup]
			public bool rootsAttached;
			[ToggleGroupItem]
			public SkinAttachmentTarget rootsAttachedTarget;
			[HideInInspector]
			public PrimarySkinningBone rootsAttachedTargetBone;//TODO move to ComponentGroup?
#endif
		}

		[Serializable]
		public struct SettingsStrands
		{
			public enum Renderer
			{
				PrimitiveLines,
				InstancedMesh,//TODO
#if UNITY_VISUALEFFECTGRAPH
				VFXGraph,//TODO
#endif
			}

			public enum Scale
			{
				Fixed,
				UniformFromHierarchy,//TODO
			}

			public Scale strandScale;
			public Renderer strandRenderer;
			[VisibleIf(nameof(strandRenderer), Renderer.InstancedMesh)]
			public Mesh strandMesh;
			[VisibleIf(nameof(strandRenderer), Renderer.InstancedMesh)]
			public bool strandMeshRigid;
#if UNITY_VISUALEFFECTGRAPH
			[VisibleIf(nameof(strandRenderer), Renderer.VFXGraph)]
			public VisualEffect strandOutputGraph;
#endif

			[LineHeader("Overrides")]

			[ToggleGroup]
			public bool material;
			[ToggleGroupItem]
			public Material materialValue;
		}

		public GroomAsset groomAsset;
		public bool groomAssetQuickEdit;

		public ComponentGroup[] componentGroups;
		public string componentGroupsChecksum;

		public SettingsRoots settingsRoots;
		public SettingsStrands settingsStrands;

		public HairSim.SolverSettings solverSettings = HairSim.SolverSettings.defaults;
		public HairSim.VolumeSettings volumeSettings = HairSim.VolumeSettings.defaults;
		public HairSim.DebugSettings debugSettings = HairSim.DebugSettings.defaults;

		public HairSim.SolverData[] solverData;
		public HairSim.VolumeData volumeData;

		void OnEnable()
		{
			InitializeComponents();

			s_instances.Add(this);
		}

		void OnDisable()
		{
			ReleaseRuntimeData();

			s_instances.Remove(this);
		}

		void OnValidate()
		{
			volumeSettings.volumeGridResolution = (Mathf.Max(8, volumeSettings.volumeGridResolution) / 8) * 8;
		}

		void OnDrawGizmos()
		{
			Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
			Gizmos.DrawWireCube(HairSim.GetVolumeCenter(volumeData.cbuffer), 2.0f * HairSim.GetVolumeExtent(volumeData.cbuffer));
		}

		void Update()
		{
			InitializeComponents();

#if UNITY_DEMOTEAM_DIGITALHUMAN
			var subjectsChanged = false;
			{
				foreach (var componentGroup in componentGroups)
				{
					var subject = componentGroup.rootAttachment;
					if (subject != null && (subject.target != settingsRoots.rootsAttachedTarget || subject.attached != settingsRoots.rootsAttached))
					{
						subject.target = settingsRoots.rootsAttachedTarget;

						if (subject.target != null && settingsRoots.rootsAttached)
						{
							subject.Attach(storePositionRotation: false);
						}
						else
						{
							subject.Detach(revertPositionRotation: false);
							subject.checksum0 = 0;
							subject.checksum1 = 0;
						}

						subjectsChanged = true;
					}
				}
			}

			if (subjectsChanged && settingsRoots.rootsAttachedTarget != null)
			{
				settingsRoots.rootsAttachedTarget.CommitSubjectsIfRequired();
				settingsRoots.rootsAttachedTargetBone = new PrimarySkinningBone(settingsRoots.rootsAttachedTarget.transform);
	#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(settingsRoots.rootsAttachedTarget);
	#endif
			}
#endif
		}

		public Quaternion GetRootRotation(in ComponentGroup group)
		{
#if UNITY_DEMOTEAM_DIGITALHUMAN
			if (settingsRoots.rootsAttached && settingsRoots.rootsAttachedTarget != null)
			{
				return settingsRoots.rootsAttachedTargetBone.skinningBone.rotation;
			}
#endif
			return group.rootFilter.transform.rotation;
		}

		public static Bounds GetRootBounds(in ComponentGroup group)
		{
			var rootBounds = group.rootFilter.sharedMesh.bounds;
			var rootTransform = group.rootFilter.transform;

			var localCenter = rootBounds.center;
			var localExtent = rootBounds.extents;

			var worldCenter = rootTransform.TransformPoint(localCenter);
			var worldExtent = rootTransform.TransformVector(localExtent);

			worldExtent.x = Mathf.Abs(worldExtent.x);
			worldExtent.y = Mathf.Abs(worldExtent.y);
			worldExtent.z = Mathf.Abs(worldExtent.z);

			return new Bounds(worldCenter, 2.0f * worldExtent);
		}

		public Bounds GetSimulationBounds()
		{
			Debug.Assert(groomAsset != null);
			Debug.Assert(groomAsset.strandGroups != null);

			var scaleFactor = GetSimulationStrandScale();
			var worldBounds = GetRootBounds(componentGroups[0]);
			var worldMargin = groomAsset.strandGroups[0].strandLengthMax * scaleFactor;

			for (int i = 1; i != componentGroups.Length; i++)
			{
				worldBounds.Encapsulate(GetRootBounds(componentGroups[i]));
				worldMargin = Mathf.Max(groomAsset.strandGroups[i].strandLengthMax * scaleFactor, worldMargin);
			}

			worldMargin *= 1.5f;
			worldBounds.Expand(2.0f * worldMargin);

			return new Bounds(worldBounds.center, worldBounds.size);
		}

		public Bounds GetSimulationBoundsSquare()
		{
			var worldBounds = GetSimulationBounds();
			var worldExtent = worldBounds.extents;

			return new Bounds(worldBounds.center, Vector3.one * (2.0f * Mathf.Max(worldExtent.x, worldExtent.y, worldExtent.z)));
		}

		public float GetSimulationStrandScale()
		{
			switch (settingsStrands.strandScale)
			{
				default:
				case SettingsStrands.Scale.Fixed:
					return 1.0f;

				case SettingsStrands.Scale.UniformFromHierarchy:
					return math.cmin(this.transform.lossyScale);
			}
		}

		public Material GetStrandMaterial()
		{
			if (settingsStrands.material)
				return settingsStrands.materialValue;
			else
				return groomAsset.settingsBasic.material;
		}

		//public float GetStrandDiameter()
		//{
		//	if (settingsStrands.strandDiameter)
		//		return settingsStrands.strandDiameterValue;
		//	else
		//		return groomAsset.settingsBasic.strandDiameter;
		//}

		public void DispatchStep(CommandBuffer cmd, float dt)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			// get bounds
			var simulationBounds = GetSimulationBoundsSquare();
			var simulationScale = GetSimulationStrandScale();

			// update solver roots
			for (int i = 0; i != solverData.Length; i++)
			{
				var rootMesh = componentGroups[i].rootFilter.sharedMesh;
				var rootTransform = componentGroups[i].rootFilter.transform.localToWorldMatrix;

				HairSim.UpdateSolverRoots(cmd, rootMesh, rootTransform, solverData[i]);
			}

			// update volume boundaries
			HairSim.UpdateVolumeBoundaries(ref volumeData, volumeSettings, simulationBounds);

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeGridResolution, halfPrecision: false))
			{
				HairSim.UpdateVolumeData(ref volumeData, volumeSettings, simulationBounds);
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
			}

			// step solver data
			for (int i = 0; i != solverData.Length; i++)
			{
				var rootFilter = componentGroups[i].rootFilter;

				var strandScale = GetSimulationStrandScale();
				var strandTransform = Matrix4x4.TRS(Vector3.zero, GetRootRotation(componentGroups[i]), Vector3.one * strandScale);

				HairSim.UpdateSolverData(ref solverData[i], solverSettings, strandTransform, dt);
				HairSim.StepSolverData(cmd, ref solverData[i], solverSettings, volumeData);
			}

			// step volume data
			HairSim.UpdateVolumeData(ref volumeData, volumeSettings, simulationBounds);
			HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);

			// update renderers
			for (int i = 0; i != solverData.Length; i++)
			{
				UpdateRenderer(cmd, componentGroups[i], solverData[i]);
			}
		}

		public void DispatchDraw(CommandBuffer cmd, RTHandle color, RTHandle depth)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			// draw solver data
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.DrawSolverData(cmd, color, depth, solverData[i], debugSettings);
			}

			// draw volume data
			HairSim.DrawVolumeData(cmd, color, depth, volumeData, debugSettings);
		}

		public void UpdateRenderer(CommandBuffer cmd, ComponentGroup componentGroup, in HairSim.SolverData solverData)
		{
			var mat = GetStrandMaterial();
			if (mat != null)
			{
				if (componentGroup.lineRenderer.sharedMaterial.shader != mat.shader)
					componentGroup.lineRenderer.sharedMaterial.shader = mat.shader;

				componentGroup.lineRenderer.sharedMaterial.CopyPropertiesFromMaterial(mat);
			}

			HairSim.PushSolverData(cmd, componentGroup.lineRenderer.sharedMaterial, componentGroup.lineRendererMPB, solverData);

			componentGroup.lineRenderer.sharedMaterial.EnableKeyword("HAIRSIMVERTEX_ENABLE_POSITION");
			componentGroup.lineRenderer.SetPropertyBlock(componentGroup.lineRendererMPB);
		}

		// when to build runtime data?
		//   1. on object enabled
		//   2. on checksum changed

		// when to clear runtime data?
		//   1. on destroy

		void InitializeComponents()
		{
			if (groomAsset != null)
			{
				if (componentGroupsChecksum != groomAsset.checksum)
				{
					GroomBuilder.BuildGroomInstance(this, groomAsset);
					componentGroupsChecksum = groomAsset.checksum;

					ReleaseRuntimeData();

					var cmd = CommandBufferPool.Get();
					{
						InitializeRuntimeData(cmd);
						Graphics.ExecuteCommandBuffer(cmd);
						CommandBufferPool.Release(cmd);
					}
				}
			}
			else
			{
				GroomBuilder.ClearGroomInstance(this);
				componentGroupsChecksum = string.Empty;

				ReleaseRuntimeData();
			}
		}

		bool InitializeRuntimeData(CommandBuffer cmd)
		{
			if (groomAsset == null)
				return false;

			if (groomAsset.checksum != componentGroupsChecksum)
				return false;

			var strandGroups = groomAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return false;

			if (solverData != null && solverData.Length == strandGroups.Length)
				return true;

			solverData = new HairSim.SolverData[strandGroups.Length];

			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var strandGroup = ref strandGroups[i];

				var rootFilter = componentGroups[i].rootFilter;
				var rootTransform = rootFilter.transform.localToWorldMatrix;

				var strandScale = GetSimulationStrandScale();
				var strandTransform = Matrix4x4.TRS(Vector3.zero, GetRootRotation(componentGroups[i]), Vector3.one * strandScale);

				HairSim.PrepareSolverData(ref solverData[i], strandGroup.strandCount, strandGroup.strandParticleCount);

				//TODO couple this with a scaling factor
				float strandParticleInterval = strandGroup.strandLengthAvg / (strandGroup.strandParticleCount - 1);

				solverData[i].cbuffer._StrandCount = (uint)strandGroup.strandCount;
				solverData[i].cbuffer._StrandParticleCount = (uint)strandGroup.strandParticleCount;
				solverData[i].cbuffer._StrandParticleInterval = strandParticleInterval * strandScale;

				int particleCount = strandGroup.strandCount * strandGroup.strandParticleCount;

				using (var tmpZero = new NativeArray<Vector4>(particleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpPosition = new NativeArray<Vector4>(particleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpRootPosition = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpRootDirection = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						fixed (void* srcPosition = strandGroup.initialPosition)
						fixed (void* srcRootPosition = strandGroup.initialRootPosition)
						fixed (void* srcRootDirection = strandGroup.initialRootDirection)
						{
							UnsafeUtility.MemCpyStride(tmpPosition.GetUnsafePtr(), sizeof(Vector4), srcPosition, sizeof(Vector3), sizeof(Vector3), particleCount);
							UnsafeUtility.MemCpyStride(tmpRootPosition.GetUnsafePtr(), sizeof(Vector4), srcRootPosition, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
							UnsafeUtility.MemCpyStride(tmpRootDirection.GetUnsafePtr(), sizeof(Vector4), srcRootDirection, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
						}
					}

					solverData[i].length.SetData(strandGroup.initialLength);
					solverData[i].rootPosition.SetData(tmpRootPosition);
					solverData[i].rootDirection.SetData(tmpRootDirection);

					solverData[i].particlePosition.SetData(tmpPosition);
					solverData[i].particlePositionPrev.SetData(tmpPosition);
					solverData[i].particlePositionPose.SetData(tmpZero);
					solverData[i].particlePositionCorr.SetData(tmpZero);
					solverData[i].particleVelocity.SetData(tmpZero);
					solverData[i].particleVelocityPrev.SetData(tmpZero);
				}

				solverData[i].memoryLayout = strandGroup.memoryLayout;

				HairSim.UpdateSolverData(ref solverData[i], solverSettings, strandTransform, 0.0f);
				HairSim.UpdateSolverRoots(cmd, componentGroups[i].rootFilter.sharedMesh, rootTransform, solverData[i]);
				{
					HairSim.InitSolverParticles(cmd, solverData[i], strandTransform);
				}

				// initialize renderer
				if (componentGroups[i].lineRendererMPB == null)
					componentGroups[i].lineRendererMPB = new MaterialPropertyBlock();

				UpdateRenderer(cmd, componentGroups[i], solverData[i]);
			}

			HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeGridResolution, halfPrecision: false);

			return true;
		}

		void ReleaseRuntimeData()
		{
			if (solverData != null)
			{
				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.ReleaseSolverData(ref solverData[i]);
				}

				solverData = null;
			}

			HairSim.ReleaseVolumeData(ref volumeData);
		}
	}

	//move to GroomEditorUtility ?
	public static class GroomBuilder
	{
		public static void ClearGroomInstance(Groom groom)
		{
			if (groom.componentGroups == null)
				return;

			foreach (var componentGroup in groom.componentGroups)
			{
				if (componentGroup.container != null)
				{
#if UNITY_EDITOR
					GameObject.DestroyImmediate(componentGroup.container);
#else
					GameObject.Destroy(groomContainer.group);
#endif
				}
			}

			groom.componentGroups = null;
		}

		public static void BuildGroomInstance(Groom groom, GroomAsset groomAsset)
		{
			ClearGroomInstance(groom);

			var strandGroups = groomAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return;

			// prep component groups
			groom.componentGroups = new Groom.ComponentGroup[strandGroups.Length];

			// build component groups
			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var componentGroup = ref groom.componentGroups[i];

				var container = new GameObject();
				{
					container.name = "Group:" + i;
					container.transform.SetParent(groom.transform, worldPositionStays: false);
					container.hideFlags = HideFlags.NotEditable;

					var linesContainer = new GameObject();
					{
						linesContainer.name = "Lines:" + i;
						linesContainer.transform.SetParent(container.transform, worldPositionStays: false);
						linesContainer.hideFlags = HideFlags.NotEditable;

						componentGroup.lineFilter = linesContainer.AddComponent<MeshFilter>();
						componentGroup.lineFilter.sharedMesh = strandGroups[i].meshAssetLines;

						componentGroup.lineRenderer = linesContainer.AddComponent<MeshRenderer>();
						componentGroup.lineRenderer.sharedMaterial = new Material(groom.GetStrandMaterial());
						componentGroup.lineRenderer.sharedMaterial.hideFlags = HideFlags.NotEditable;
					}

					var rootsContainer = new GameObject();
					{
						rootsContainer.name = "Roots:" + i;
						rootsContainer.transform.SetParent(container.transform, worldPositionStays: false);
						rootsContainer.hideFlags = HideFlags.NotEditable;

						componentGroup.rootFilter = rootsContainer.AddComponent<MeshFilter>();
						componentGroup.rootFilter.sharedMesh = strandGroups[i].meshAssetRoots;

#if UNITY_DEMOTEAM_DIGITALHUMAN
						componentGroup.rootAttachment = rootsContainer.AddComponent<SkinAttachment>();
						componentGroup.rootAttachment.attachmentType = SkinAttachment.AttachmentType.Mesh;
						componentGroup.rootAttachment.forceRecalculateBounds = true;
#endif
					}
				}

				groom.componentGroups[i].container = container;
			}
		}
	}

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
