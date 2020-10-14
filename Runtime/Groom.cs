using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	[ExecuteAlways]
	public class Groom : MonoBehaviour
	{
		public static HashSet<Groom> s_instances = new HashSet<Groom>();

		public GroomAsset groomAsset;
		public GameObject[] groomContainers;
		[HideInInspector] public Hash128 groomChecksum;

		private HairSim.SolverData[] solverData;
		private HairSim.VolumeData volumeData;

		//public bool solverSettingsOverride;
		//public bool volumeSettingsOverride;

		public HairSim.SolverSettings[] solverSettings;
		public HairSim.VolumeSettings volumeSettings = HairSim.VolumeSettings.basic;
		public HairSim.DebugSettings debugSettings = HairSim.DebugSettings.basic;

		public List<HairSimBoundary> boundaries = new List<HairSimBoundary>(HairSim.MAX_BOUNDARIES);

		void OnEnable()
		{
			InitializeContainers();
			InitializeRuntimeData();

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
			if (!debugSettings.drawDensity &&
				!debugSettings.drawSliceX &&
				!debugSettings.drawSliceY &&
				!debugSettings.drawSliceZ)
				return;

			Gizmos.color = Color.Lerp(Color.black, Color.clear, 0.5f);
			Gizmos.DrawWireCube(HairSim.GetVolumeCenter(volumeData.cbuffer), 2.0f * HairSim.GetVolumeExtent(volumeData.cbuffer));
		}

		void Update()
		{
			InitializeContainers();
		}

		Bounds GetBounds()
		{
			var strandBounds = groomAsset.strandGroups[0].meshAssetRoots.bounds;
			var strandLength = groomAsset.strandGroups[0].strandLengthMax;

			for (int i = 1; i != groomAsset.strandGroups.Length; i++)
			{
				strandBounds.Encapsulate(groomAsset.strandGroups[i].meshAssetRoots.bounds);
				strandLength = Mathf.Max(groomAsset.strandGroups[i].strandLengthMax, strandLength);
			}

			strandBounds.Expand(1.5f * (2.0f * strandLength));

			var extent = strandBounds.extents;
			var extentMax = Mathf.Max(extent.x, extent.y, extent.z);

			return new Bounds(strandBounds.center, Vector3.one * (2.0f * extentMax));
		}

		Bounds GetBoundsForSquareCells()
		{
			var bounds = GetBounds();
			{
				var nonSquareExtent = bounds.extents;
				var nonSquareExtentMax = Mathf.Max(nonSquareExtent.x, nonSquareExtent.y, nonSquareExtent.z);

				return new Bounds(bounds.center, Vector3.one * (2.0f * nonSquareExtentMax));
			}
		}

		public void DispatchStep(CommandBuffer cmd, float dt)
		{
			if (!InitializeRuntimeData())
				return;

			// apply settings
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.UpdateSolverData(ref solverData[i], solverSettings[i], dt);
			}

			var volumeBounds = GetBoundsForSquareCells();
			{
				volumeSettings.volumeWorldCenter = volumeBounds.center;
				volumeSettings.volumeWorldExtent = volumeBounds.extents;
			}

			HairSim.UpdateVolumeData(ref volumeData, volumeSettings, boundaries);

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeResolution))
			{
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
			}

			// step
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.StepSolverData(cmd, ref solverData[i], solverSettings[i], volumeData);
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
					GroomUtility.BuildGroomInstance(this, groomAsset);
					groomChecksum = groomAsset.checksum;

					ReleaseRuntimeData();
				}
			}
			else
			{
				GroomUtility.ClearGroomInstance(this);
				groomChecksum = new Hash128();

				ReleaseRuntimeData();
			}
		}

		bool InitializeRuntimeData()
		{
			if (solverData != null)
				return true;

			if (groomAsset == null || groomAsset.strandGroups == null)
				return false;

			solverData = new HairSim.SolverData[groomAsset.strandGroups.Length];
			solverSettings = new HairSim.SolverSettings[groomAsset.strandGroups.Length];

			for (int i = 0; i != solverData.Length; i++)
			{
				ref var strandGroup = ref groomAsset.strandGroups[i];

				solverSettings[i] = HairSim.SolverSettings.basic;
				solverSettings[i].strandDiameter = groomAsset.settingsBasic.defaultStrandDiameter;
				solverSettings[i].strandLength = strandGroup.strandLengthAvg;

				HairSim.PrepareSolverData(ref solverData[i],
					strandGroup.strandCount,
					strandGroup.strandParticleCount);

				int particleCount = strandGroup.strandCount * strandGroup.strandParticleCount;

				solverData[i].cbuffer._StrandCount = (uint)strandGroup.strandCount;
				solverData[i].cbuffer._StrandParticleCount = (uint)strandGroup.strandParticleCount;

				using (var tmpZero = new NativeArray<Vector4>(particleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpPosition = new NativeArray<Vector4>(particleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpRootPosition = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpRootDirection = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						fixed (void* srcPosition = strandGroup.initialPositions)
						fixed (void* srcRootPosition = strandGroup.initialRootPositions)
						fixed (void* srcRootDirection = strandGroup.initialRootDirections)
						{
							UnsafeUtility.MemCpyStride(tmpPosition.GetUnsafePtr(), sizeof(Vector4), srcPosition, sizeof(Vector3), sizeof(Vector3), particleCount);
							UnsafeUtility.MemCpyStride(tmpRootPosition.GetUnsafePtr(), sizeof(Vector4), srcRootPosition, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
							UnsafeUtility.MemCpyStride(tmpRootDirection.GetUnsafePtr(), sizeof(Vector4), srcRootDirection, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
						}
					}

					solverData[i].rootPosition.SetData(tmpRootPosition);
					solverData[i].rootDirection.SetData(tmpRootDirection);

					solverData[i].particlePosition.SetData(tmpPosition);
					solverData[i].particlePositionPrev.SetData(tmpPosition);
					solverData[i].particlePositionCorr.SetData(tmpZero);
					solverData[i].particleVelocity.SetData(tmpZero);
					solverData[i].particleVelocityPrev.SetData(tmpZero);
				}
			}

			HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeResolution);

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
	public static class GroomUtility
	{
		public static void ClearGroomInstance(Groom groom)
		{
			if (groom.groomContainers == null)
				return;

			foreach (var groomObject in groom.groomContainers)
			{
				if (groomObject != null)
				{
					GameObject.DestroyImmediate(groomObject);
				}
			}

			groom.groomContainers = null;
		}

		public static void BuildGroomInstance(Groom groom, GroomAsset groomAsset)
		{
			ClearGroomInstance(groom);

			var strandGroups = groomAsset.strandGroups;
			if (strandGroups == null)
				return;

			// prep groom containers
			groom.groomContainers = new GameObject[strandGroups.Length];

			// build groom containers
			for (int i = 0; i != strandGroups.Length; i++)
			{
				var groupObject = new GameObject();
				{
					groupObject.name = "Group:" + i;
					groupObject.transform.SetParent(groom.transform, worldPositionStays: false);

					var linesContainer = new GameObject();
					{
						linesContainer.name = "Lines:" + i;
						linesContainer.transform.SetParent(groupObject.transform, worldPositionStays: false);

						var lineFilter = linesContainer.AddComponent<MeshFilter>();
						{
							lineFilter.sharedMesh = strandGroups[i].meshAssetLines;
						}

						var lineRenderer = linesContainer.AddComponent<MeshRenderer>();
						{
							lineRenderer.sharedMaterial = groomAsset.settingsBasic.defaultMaterial;
						}
					}

					var rootsContainer = new GameObject();
					{
						rootsContainer.name = "Roots:" + i;
						rootsContainer.transform.SetParent(groupObject.transform, worldPositionStays: false);

						var rootFilter = rootsContainer.AddComponent<MeshFilter>();
						{
							rootFilter.sharedMesh = strandGroups[i].meshAssetRoots;
						}

						//var rootAttachment = rootObject.AddComponent<SkinAttachment>();
					}
				}

				groom.groomContainers[i] = groupObject;
			}
		}
	}
}
