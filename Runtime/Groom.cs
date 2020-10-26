using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	[ExecuteAlways]
	[SelectionBase]
	public class Groom : MonoBehaviour
	{
		public static HashSet<Groom> s_instances = new HashSet<Groom>();

		[Serializable]
		public struct GroomContainer
		{
			public GameObject group;

			public MeshFilter lineFilter;
			public MeshRenderer lineRenderer;
			public MaterialPropertyBlock lineRendererMPB;

			public MeshFilter rootFilter;
			//public SkinAttachment rootAttachment;
		}

		public GroomAsset groomAsset;
		public GroomContainer[] groomContainers;
		[HideInInspector] public string groomChecksum;

		private HairSim.SolverData[] solverData;
		private HairSim.VolumeData volumeData;

		public HairSim.SolverSettings solverSettings = HairSim.SolverSettings.defaults;
		public HairSim.VolumeSettings volumeSettings = HairSim.VolumeSettings.defaults;
		public HairSim.DebugSettings debugSettings = HairSim.DebugSettings.defaults;
		public List<HairSimBoundary> boundaries = new List<HairSimBoundary>(HairSim.MAX_BOUNDARIES);

		void OnEnable()
		{
			InitializeContainers();

			s_instances.Add(this);
		}

		void OnDisable()
		{
			ReleaseRuntimeData();

			s_instances.Remove(this);
		}

		void OnValidate()
		{
			if (boundaries.Count > HairSim.MAX_BOUNDARIES)
			{
				boundaries.RemoveRange(HairSim.MAX_BOUNDARIES, boundaries.Count - HairSim.MAX_BOUNDARIES);
				boundaries.TrimExcess();
			}

			volumeSettings.volumeResolution = (Mathf.Max(8, volumeSettings.volumeResolution) / 8) * 8;
		}

		void OnDrawGizmos()
		{
			Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
			Gizmos.DrawWireCube(HairSim.GetVolumeCenter(volumeData.cbuffer), 2.0f * HairSim.GetVolumeExtent(volumeData.cbuffer));
		}

		void Update()
		{
			InitializeContainers();
		}

		public static Bounds GetRootBounds(MeshFilter rootFilter)
		{
			var rootBounds = rootFilter.sharedMesh.bounds;
			var rootTransform = rootFilter.transform;

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

			var worldBounds = GetRootBounds(groomContainers[0].rootFilter);
			var worldMargin = groomAsset.strandGroups[0].strandLengthMax;

			for (int i = 1; i != groomContainers.Length; i++)
			{
				worldBounds.Encapsulate(GetRootBounds(groomContainers[i].rootFilter));
				worldMargin = Mathf.Max(groomAsset.strandGroups[i].strandLengthMax, worldMargin);
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

		public Bounds GetSimulationBoundsForSquareCells()
		{
			var bounds = GetSimulationBounds();
			{
				var size = bounds.size;
				var sizeMax = Mathf.Max(size.x, size.y, size.z);

				return new Bounds(bounds.center, new Vector3(sizeMax, sizeMax, sizeMax));
			}
		}

		public void DispatchStep(CommandBuffer cmd, float dt)
		{
			if (!InitializeRuntimeData())
				return;

			// apply settings
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.UpdateSolverData(ref solverData[i], solverSettings, dt);
				HairSim.UpdateSolverRoots(cmd, groomContainers[i].rootFilter.sharedMesh, groomContainers[i].rootFilter.transform.localToWorldMatrix, solverData[i]);
			}

			var volumeBounds = GetSimulationBoundsForSquareCells();
			{
				volumeSettings.volumeWorldCenter = volumeBounds.center;
				volumeSettings.volumeWorldExtent = volumeBounds.extents;
			}

			HairSim.UpdateVolumeData(ref volumeData, volumeSettings, boundaries);
			//TODO this needs to happen after stepping solver
			//TODO split boundary data update from volume data update

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeResolution, halfPrecision: false))
			{
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
			}

			// perform time step
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.StepSolverData(cmd, ref solverData[i], solverSettings, volumeData);
				HairSim.PushSolverData(cmd, groomContainers[i].lineRenderer.sharedMaterial, groomContainers[i].lineRendererMPB, solverData[i]);
			}

			HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
		}

		public void DispatchDraw(CommandBuffer cmd, RTHandle color, RTHandle depth, RTHandle movec)
		{
			if (!InitializeRuntimeData())
				return;

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.DrawSolverData(cmd, color, depth, movec, solverData[i], debugSettings);
			}

			HairSim.DrawVolumeData(cmd, color, depth, volumeData, debugSettings);
		}

		// when to build runtime data?
		//   1. on object enabled
		//   2. on checksum changed

		// when to clear runtime data?
		//   1. on destroy

		void InitializeContainers()
		{
			if (groomAsset != null)
			{
				if (groomChecksum != groomAsset.checksum)
				{
					GroomBuilder.BuildGroomInstance(this, groomAsset);
					groomChecksum = groomAsset.checksum;

					ReleaseRuntimeData();
					InitializeRuntimeData();
				}
			}
			else
			{
				GroomBuilder.ClearGroomInstance(this);
				groomChecksum = string.Empty;

				ReleaseRuntimeData();
			}
		}

		bool InitializeRuntimeData()
		{
			if (groomAsset == null)
				return false;

			if (groomAsset.checksum != groomChecksum)
				return false;

			var strandGroups = groomAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return false;

			if (solverData != null && solverData.Length != 0)
				return true;

			solverData = new HairSim.SolverData[strandGroups.Length];

			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var strandGroup = ref strandGroups[i];

				HairSim.PrepareSolverData(ref solverData[i], strandGroup.strandCount, strandGroup.strandParticleCount);

				float strandCrossSectionArea = 0.25f * Mathf.PI * groomAsset.settingsBasic.strandDiameter * groomAsset.settingsBasic.strandDiameter;
				float strandParticleInterval = strandGroup.strandLengthAvg / (strandGroup.strandParticleCount - 1);
				float strandParticleVolume = (1000.0f * strandParticleInterval) * strandCrossSectionArea;

				solverData[i].cbuffer._StrandCount = (uint)strandGroup.strandCount;
				solverData[i].cbuffer._StrandParticleCount = (uint)strandGroup.strandParticleCount;
				solverData[i].cbuffer._StrandParticleInterval = strandParticleInterval;
				solverData[i].cbuffer._StrandParticleVolume = strandParticleVolume;
				solverData[i].cbuffer._StrandParticleContrib = groomAsset.settingsBasic.strandParticleContrib;

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
					solverData[i].particlePositionCorr.SetData(tmpZero);
					solverData[i].particleVelocity.SetData(tmpZero);
					solverData[i].particleVelocityPrev.SetData(tmpZero);
				}

				solverData[i].memoryLayout = strandGroup.memoryLayout;

				// initialize the renderer
				if (groomContainers[i].lineRendererMPB == null)
					groomContainers[i].lineRendererMPB = new MaterialPropertyBlock();

				groomContainers[i].lineRendererMPB.SetBuffer(HairSim.UniformIDs._ParticlePosition, solverData[i].particlePosition);
				groomContainers[i].lineRendererMPB.SetInt("_StrandCount", (int)solverData[i].cbuffer._StrandCount);
				groomContainers[i].lineRenderer.SetPropertyBlock(groomContainers[i].lineRendererMPB);
			}

			HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeResolution, halfPrecision: false);

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
			if (groom.groomContainers == null)
				return;

			foreach (var groomContainer in groom.groomContainers)
			{
				if (groomContainer.group != null)
				{
#if UNITY_EDITOR
					GameObject.DestroyImmediate(groomContainer.group);
#else
					GameObject.Destroy(groomContainer.group);
#endif
				}
			}

			groom.groomContainers = null;
		}

		public static void BuildGroomInstance(Groom groom, GroomAsset groomAsset)
		{
			ClearGroomInstance(groom);

			var strandGroups = groomAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return;

			// prep groom containers
			groom.groomContainers = new Groom.GroomContainer[strandGroups.Length];

			// build groom containers
			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var groomContainer = ref groom.groomContainers[i];

				var group = new GameObject();
				{
					group.name = "Group:" + i;
					group.transform.SetParent(groom.transform, worldPositionStays: false);
					group.hideFlags = HideFlags.NotEditable;

					var linesContainer = new GameObject();
					{
						linesContainer.name = "Lines:" + i;
						linesContainer.transform.SetParent(group.transform, worldPositionStays: false);
						linesContainer.hideFlags = HideFlags.NotEditable;

						groomContainer.lineFilter = linesContainer.AddComponent<MeshFilter>();
						groomContainer.lineFilter.sharedMesh = strandGroups[i].meshAssetLines;

						groomContainer.lineRenderer = linesContainer.AddComponent<MeshRenderer>();
						groomContainer.lineRenderer.sharedMaterial = groomAsset.settingsBasic.material;
					}

					var rootsContainer = new GameObject();
					{
						rootsContainer.name = "Roots:" + i;
						rootsContainer.transform.SetParent(group.transform, worldPositionStays: false);
						rootsContainer.hideFlags = HideFlags.NotEditable;

						groomContainer.rootFilter = rootsContainer.AddComponent<MeshFilter>();
						groomContainer.rootFilter.sharedMesh = strandGroups[i].meshAssetRoots;

						//TODO
						//groomContainer.rootAttachment = rootObject.AddComponent<SkinAttachment>();
						//groomContainer.rootAttachment = rootAttachment;
					}
				}

				groom.groomContainers[i].group = group;
			}
		}
	}
}
