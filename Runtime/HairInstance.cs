using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.DemoTeam.Attributes;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
#endif
#if HAS_PACKAGE_UNITY_VFXGRAPH
using UnityEngine.VFX;
#endif

namespace Unity.DemoTeam.Hair
{
	[ExecuteAlways, SelectionBase]
	public class HairInstance : MonoBehaviour
	{
		public static HashSet<HairInstance> s_instances = new HashSet<HairInstance>();

		[Serializable]
		public struct ComponentGroup
		{
			public GameObject container;
			public MeshFilter lineFilter;
			public MeshRenderer lineRenderer;
			public MeshFilter rootFilter;
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			public SkinAttachment rootAttachment;
#endif
		}

		[Serializable]
		public struct SettingsRoots
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			[ToggleGroup]
			public bool rootsAttach;
			[ToggleGroupItem]
			public SkinAttachmentTarget rootsAttachTarget;
			[HideInInspector]
			public PrimarySkinningBone rootsAttachTargetBone;//TODO move to ComponentGroup?
#endif

			public static readonly SettingsRoots defaults = new SettingsRoots()
			{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
				rootsAttach = false,
				rootsAttachTarget = null,
#endif
			};
		}

		[Serializable]
		public struct SettingsStrands
		{
			public enum StrandScale
			{
				Fixed,
				UniformMin,
				UniformMax,
			}

			public enum StrandRenderer
			{
				PrimitiveLines,
				PrimitiveStrips,
#if HAS_PACKAGE_UNITY_VFXGRAPH
				VFXGraph,//TODO
#endif
			}

			//public enum Simulation
			//{
			//	Disabled,
			//	Enabled,
			//	EnabledInPlaymode,
			//}

			public enum SimulationRate
			{
				Fixed30Hz,
				Fixed60Hz,
				Fixed120Hz,
				CustomTimeStep,
			}

			[LineHeader("Rendering")]

			[ToggleGroup]
			public bool strandMaterial;
			[ToggleGroupItem]
			public Material strandMaterialValue;
			public StrandRenderer strandRenderer;
#if HAS_PACKAGE_UNITY_VFXGRAPH
			[VisibleIf(nameof(strandRenderer), StrandRenderer.VFXGraph)]
			public VisualEffect strandOutputGraph;
#endif
			public ShadowCastingMode strandShadows;
			[RenderingLayerMask]
			public int strandLayers;

			[LineHeader("Proportions")]

			[Tooltip("Strand scale")]
			public StrandScale strandScale;
			[Range(0.070f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
			public float strandDiameter;

			[LineHeader("Dynamics")]

			//[Tooltip("Simulation state")]
			//public Simulation simulation;
			[ToggleGroup, Tooltip("Enable simulation")]
			public bool simulation;
			[ToggleGroupItem, Tooltip("Simulation update rate")]
			public SimulationRate simulationRate;
			[ToggleGroupItem(withLabel = true), Tooltip("Enable simulation in Edit Mode")]
			public bool simulationInEditor;
			[VisibleIf(nameof(simulationRate), SimulationRate.CustomTimeStep), Tooltip("Simulation time step (in seconds)")]
			public float simulationTimeStep;
			[ToggleGroup, Tooltip("Enable minimum number of simulation steps per rendered frame")]
			public bool stepsMin;
			[ToggleGroupItem, Tooltip("Minimum number of simulation steps per rendered frame")]
			public int stepsMinValue;
			[ToggleGroup, Tooltip("Enable maximum number of simulation steps per rendered frame")]
			public bool stepsMax;
			[ToggleGroupItem, Tooltip("Maximum number of simulation steps per rendered frame")]
			public int stepsMaxValue;

			public static readonly SettingsStrands defaults = new SettingsStrands()
			{
				strandDiameter = 1.0f,
				strandScale = StrandScale.Fixed,

				strandMaterial = false,
				strandMaterialValue = null,
				strandRenderer = StrandRenderer.PrimitiveLines,
				strandShadows = ShadowCastingMode.On,
				strandLayers = 0x0101,//TODO this is the HDRP default -- should decide based on active pipeline asset

				simulation = true,
				simulationRate = SimulationRate.Fixed60Hz,
				simulationInEditor = true,

				simulationTimeStep = 1.0f / 100.0f,
				stepsMin = false,
				stepsMinValue = 1,
				stepsMax = true,
				stepsMaxValue = 2,
			};
		}

		public HairAsset hairAsset;
		public bool hairAssetQuickEdit;

		public ComponentGroup[] componentGroups;
		public string componentGroupsChecksum;

		public SettingsRoots settingsRoots = SettingsRoots.defaults;
		public SettingsStrands settingsStrands = SettingsStrands.defaults;

		public HairSim.SolverSettings solverSettings = HairSim.SolverSettings.defaults;
		public HairSim.VolumeSettings volumeSettings = HairSim.VolumeSettings.defaults;
		public HairSim.DebugSettings debugSettings = HairSim.DebugSettings.defaults;

		public HairSim.SolverData[] solverData;
		public HairSim.VolumeData volumeData;

		[NonSerialized]
		public float accumulatedTime;
		[NonSerialized]
		public int stepsLastFrame;
		[NonSerialized]
		public float stepsLastFrameSmooth;
		[NonSerialized]
		public int stepsLastFrameSkipped;

		void OnEnable()
		{
			InitializeComponents();
			InitializeComponentsHideFlags();

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
			Gizmos.DrawWireCube(HairSim.GetVolumeCenter(volumeData), 2.0f * HairSim.GetVolumeExtent(volumeData));

			if (componentGroups != null)
			{
				foreach (var componentGroup in componentGroups)
				{
					var rootFilter = componentGroup.rootFilter;
					if (rootFilter != null)
					{
						var rootMesh = rootFilter.sharedMesh;
						if (rootMesh != null)
						{
							var rootBounds = rootMesh.bounds;

							Gizmos.color = Color.Lerp(Color.blue, Color.clear, 0.5f);
							Gizmos.matrix = rootFilter.transform.localToWorldMatrix;
							Gizmos.DrawWireCube(rootBounds.center, rootBounds.size);
						}
					}
				}
			}
		}

		void Update()
		{
			InitializeComponents();

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
	#if UNITY_EDITOR
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
			if (isPrefabInstance)
				return;
	#endif

			if (componentGroups != null)
			{
				var subjectsChanged = false;

				foreach (var componentGroup in componentGroups)
				{
					var subject = componentGroup.rootAttachment;
					if (subject != null && (subject.target != settingsRoots.rootsAttachTarget || subject.attached != settingsRoots.rootsAttach))
					{
						subject.target = settingsRoots.rootsAttachTarget;

						if (subject.target != null && settingsRoots.rootsAttach)
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

				if (subjectsChanged && settingsRoots.rootsAttachTarget != null)
				{
					settingsRoots.rootsAttachTarget.CommitSubjectsIfRequired();
					settingsRoots.rootsAttachTargetBone = new PrimarySkinningBone(settingsRoots.rootsAttachTarget.transform);
	#if UNITY_EDITOR
					UnityEditor.EditorUtility.SetDirty(settingsRoots.rootsAttachTarget);
	#endif
				}
			}
#endif
		}

		void LateUpdate()
		{
			if (solverData == null)
				return;

			for (int i = 0; i != solverData.Length; i++)
			{
				var mat = componentGroups[i].lineRenderer.sharedMaterial;
				if (mat == null)
					continue;

				HairSim.PushSolverData(mat, solverData[i]);

				switch (settingsStrands.strandRenderer)
				{
					case SettingsStrands.StrandRenderer.PrimitiveLines:
						Graphics.DrawMeshInstancedProcedural(hairAsset.strandGroups[i].meshAssetLines, 0, mat, GetSimulationBounds(), 1, castShadows: settingsStrands.strandShadows);
						break;

					case SettingsStrands.StrandRenderer.PrimitiveStrips:
						Graphics.DrawMeshInstancedProcedural(hairAsset.strandGroups[i].meshAssetStrips, 0, mat, GetSimulationBounds(), 1, castShadows: settingsStrands.strandShadows);
						break;

					default:
						break;//TODO
				}
			}
		}

		public Quaternion GetRootRotation(in ComponentGroup group)
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			if (settingsRoots.rootsAttach && settingsRoots.rootsAttachTarget != null)
			{
				return settingsRoots.rootsAttachTargetBone.skinningBone.rotation;
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

			return new Bounds(worldCenter, 2.0f * worldExtent.Abs());
		}

		public Bounds GetSimulationBounds(bool square = true)
		{
			Debug.Assert(hairAsset != null);
			Debug.Assert(hairAsset.strandGroups != null);

			var strandScale = GetStrandScale();
			var worldBounds = GetRootBounds(componentGroups[0]);
			var worldMargin = hairAsset.strandGroups[0].maxStrandLength * strandScale;

			for (int i = 1; i != componentGroups.Length; i++)
			{
				worldBounds.Encapsulate(GetRootBounds(componentGroups[i]));
				worldMargin = Mathf.Max(hairAsset.strandGroups[i].maxStrandLength * strandScale, worldMargin);
			}

			worldMargin *= 1.5f;
			worldBounds.Expand(2.0f * worldMargin);

			if (square)
				return new Bounds(worldBounds.center, worldBounds.size.ComponentMax() * Vector3.one);
			else
				return new Bounds(worldBounds.center, worldBounds.size);
		}

		public bool GetSimulationActive()
		{
			if (settingsStrands.simulation)
			{
				return settingsStrands.simulationInEditor || Application.isPlaying;
			}
			else
			{
				return false;
			}
		}

		public float GetSimulationTimeStep()
		{
			switch (settingsStrands.simulationRate)
			{
				case SettingsStrands.SimulationRate.Fixed30Hz: return 1.0f / 30.0f;
				case SettingsStrands.SimulationRate.Fixed60Hz: return 1.0f / 60.0f;
				case SettingsStrands.SimulationRate.Fixed120Hz: return 1.0f / 120.0f;
				case SettingsStrands.SimulationRate.CustomTimeStep: return settingsStrands.simulationTimeStep;
			}

			return 0.0f;
		}

		public float GetStrandDiameter()
		{
			return settingsStrands.strandDiameter;
		}

		public float GetStrandScale()
		{
			switch (settingsStrands.strandScale)
			{
				default:
				case SettingsStrands.StrandScale.Fixed:
					{
						return 1.0f;
					}

				case SettingsStrands.StrandScale.UniformMin:
					{
						var lossyScaleAbs = this.transform.lossyScale.Abs();
						var lossyScaleAbsMin = lossyScaleAbs.ComponentMin();
						return lossyScaleAbsMin;
					}

				case SettingsStrands.StrandScale.UniformMax:
					{
						var lossyScaleAbs = this.transform.lossyScale.Abs();
						var lossyScaleAbsMax = lossyScaleAbs.ComponentMax();
						return lossyScaleAbsMax;
					}
			}
		}

		public Material GetStrandMaterial()
		{
			var mat = null as Material;

			if (mat == null && settingsStrands.strandMaterial)
				mat = settingsStrands.strandMaterialValue;

			if (mat == null && hairAsset != null)
				mat = hairAsset.settingsBasic.material;

			return mat;
		}

		public void DispatchStepAccumulated(CommandBuffer cmd, float dt)
		{
			var active = GetSimulationActive();
			var stepDT = GetSimulationTimeStep();

			// skip if inactive or time step zero
			if (stepDT == 0.0f || active == false)
			{
				stepsLastFrame = 0;
				stepsLastFrameSmooth = 0.0f;
				stepsLastFrameSkipped = 0;
				return;
			}

			// calc number of steps
			accumulatedTime += dt;

			var stepCountRT = (int)Mathf.Floor(accumulatedTime / stepDT);
			var stepCount = stepCountRT;
			{
				stepCount = Mathf.Max(stepCount, settingsStrands.stepsMin ? settingsStrands.stepsMinValue : stepCount);
				stepCount = Mathf.Min(stepCount, settingsStrands.stepsMax ? settingsStrands.stepsMaxValue : stepCount);
			}

			// always subtract the maximum (effectively clear accumulated if skipping frames)
			accumulatedTime -= Mathf.Max(stepCountRT, stepCount) * stepDT;

			if (accumulatedTime < 0.0f)
				accumulatedTime = 0.0f;

			// perform the steps
			for (int i = 0; i != stepCount; i++)
			{
				DispatchStep(cmd, stepDT);
			}

			// update counters
			stepsLastFrame = stepCount;
			stepsLastFrameSmooth = Mathf.Lerp(stepsLastFrameSmooth, stepsLastFrame, 1.0f - Mathf.Pow(0.01f, dt / 0.2f));
			stepsLastFrameSkipped = Mathf.Max(0, stepCountRT - stepCount);
		}

		public void DispatchStep(CommandBuffer cmd, float dt)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			// get bounds and scale
			var simulationBounds = GetSimulationBounds();
			var strandDiameter = GetStrandDiameter();
			var strandScale = GetStrandScale();

			// update solver roots
			for (int i = 0; i != solverData.Length; i++)
			{
				var rootMesh = componentGroups[i].rootFilter.sharedMesh;
				var rootTransform = componentGroups[i].rootFilter.transform.localToWorldMatrix;

				var strandRotation = GetRootRotation(componentGroups[i]);

				HairSim.UpdateSolverData(cmd, ref solverData[i], solverSettings, rootTransform, strandRotation, strandDiameter, strandScale, dt);
				HairSim.UpdateSolverRoots(cmd, solverData[i], rootMesh);
			}

			// update volume boundaries
			HairSim.UpdateVolumeBoundaries(cmd, ref volumeData, volumeSettings, simulationBounds);

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeGridResolution, halfPrecision: false))
			{
				HairSim.UpdateVolumeData(cmd, ref volumeData, volumeSettings, simulationBounds, strandDiameter, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
			}

			// step solver data
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.StepSolverData(cmd, ref solverData[i], solverSettings, volumeData);
			}

			// step volume data
			HairSim.UpdateVolumeData(cmd, ref volumeData, volumeSettings, simulationBounds, strandDiameter, strandScale);
			HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);

			// update renderers
			for (int i = 0; i != solverData.Length; i++)
			{
				UpdateRenderer(componentGroups[i], solverData[i]);
			}
		}

		public void DispatchDraw(CommandBuffer cmd)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			// draw solver data
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.DrawSolverData(cmd, solverData[i], debugSettings);
			}

			// draw volume data
			HairSim.DrawVolumeData(cmd, volumeData, debugSettings);
		}

		public void UpdateRenderer(in ComponentGroup componentGroup, in HairSim.SolverData solverData)
		{
			var lineRenderer = componentGroup.lineRenderer;

			var material = GetStrandMaterial();
			if (material != null)
			{
				if (lineRenderer.sharedMaterial == null)
				{
					lineRenderer.sharedMaterial = new Material(material);
					lineRenderer.sharedMaterial.name += "(Instance)";
					lineRenderer.sharedMaterial.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
				}
				else
				{
					if (lineRenderer.sharedMaterial.shader != material.shader)
						lineRenderer.sharedMaterial.shader = material.shader;

					lineRenderer.sharedMaterial.CopyPropertiesFromMaterial(material);
				}
			}

			if (lineRenderer.sharedMaterial != null)
			{
				HairSim.PushSolverData(lineRenderer.sharedMaterial, solverData);

				lineRenderer.sharedMaterial.EnableKeyword("HAIR_VERTEX_DYNAMIC");
			}

			lineRenderer.enabled = false;//TODO either get rid of the renderer or swap meshes on there
			lineRenderer.shadowCastingMode = settingsStrands.strandShadows;
			lineRenderer.renderingLayerMask = (uint)settingsStrands.strandLayers;
		}

		// when to build runtime data?
		//   1. on object enabled
		//   2. on checksum changed

		// when to clear runtime data?
		//   1. on destroy

		void InitializeComponents()
		{
#if UNITY_EDITOR
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
			if (isPrefabInstance)
			{
				if (hairAsset != null)
				{
					// did the underlying asset change since prefab was built?
					if (componentGroupsChecksum != hairAsset.checksum)
					{
						var prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(this);
						var prefabContents = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);

						Debug.LogWarningFormat(this, "{0} rebuilding underlying prefab", this.name);

						UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
						UnityEditor.PrefabUtility.UnloadPrefabContents(prefabContents);
					}
				}
				return;
			}
#endif

			if (hairAsset != null)
			{
				if (componentGroupsChecksum != hairAsset.checksum)
				{
					HairInstanceBuilder.BuildHairInstance(this, hairAsset);
					componentGroupsChecksum = hairAsset.checksum;

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
				HairInstanceBuilder.ClearHairInstance(this);
				componentGroupsChecksum = string.Empty;

				ReleaseRuntimeData();
			}
		}

		void InitializeComponentsHideFlags()
		{
#if UNITY_EDITOR
			if (componentGroups != null)
			{
				foreach (var componentGroup in componentGroups)
				{
					componentGroup.container.hideFlags = HideFlags.NotEditable;
					componentGroup.lineFilter.gameObject.hideFlags = HideFlags.NotEditable;
					componentGroup.rootFilter.gameObject.hideFlags = HideFlags.NotEditable;
				}
			}
#endif
		}

		bool InitializeRuntimeData(CommandBuffer cmd)
		{
			if (hairAsset == null)
				return false;

			if (hairAsset.checksum != componentGroupsChecksum)
				return false;

			var strandGroups = hairAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return false;

			if (solverData != null && solverData.Length == strandGroups.Length)
				return true;

			// prep volume data
			HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeGridResolution, halfPrecision: false);

			volumeData.allGroupsMaxParticleInterval = 0.0f;

			for (int i = 0; i != strandGroups.Length; i++)
			{
				volumeData.allGroupsMaxParticleInterval = Mathf.Max(volumeData.allGroupsMaxParticleInterval, strandGroups[i].maxParticleInterval);
			}

			// init solver data
			solverData = new HairSim.SolverData[strandGroups.Length];

			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var strandGroup = ref strandGroups[i];

				HairSim.PrepareSolverData(ref solverData[i], strandGroup.strandCount, strandGroup.strandParticleCount);

				solverData[i].memoryLayout = strandGroup.particleMemoryLayout;

				solverData[i].cbuffer._StrandCount = (uint)strandGroup.strandCount;
				solverData[i].cbuffer._StrandParticleCount = (uint)strandGroup.strandParticleCount;
				solverData[i].cbuffer._StrandMaxParticleInterval = strandGroup.maxParticleInterval;
				solverData[i].cbuffer._StrandMaxParticleWeight = strandGroup.maxParticleInterval / volumeData.allGroupsMaxParticleInterval;

				int strandGroupParticleCount = strandGroup.strandCount * strandGroup.strandParticleCount;

				using (var tmpRootPosition = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpRootDirection = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpParticlePosition = new NativeArray<Vector4>(strandGroupParticleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						fixed (void* srcRootPosition = strandGroup.rootPosition)
						fixed (void* srcRootDirection = strandGroup.rootDirection)
						fixed (void* srcParticlePosition = strandGroup.particlePosition)
						{
							UnsafeUtility.MemCpyStride(tmpRootPosition.GetUnsafePtr(), sizeof(Vector4), srcRootPosition, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
							UnsafeUtility.MemCpyStride(tmpRootDirection.GetUnsafePtr(), sizeof(Vector4), srcRootDirection, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
							UnsafeUtility.MemCpyStride(tmpParticlePosition.GetUnsafePtr(), sizeof(Vector4), srcParticlePosition, sizeof(Vector3), sizeof(Vector3), strandGroupParticleCount);
						}
					}

					solverData[i].rootScale.SetData(strandGroup.rootScale);
					solverData[i].rootPosition.SetData(tmpRootPosition);
					solverData[i].rootDirection.SetData(tmpRootDirection);

					solverData[i].particlePosition.SetData(tmpParticlePosition);

					// NOTE: the rest of the particle buffers are initialized by KInitParticles
					//solverData[i].particlePositionPrev.SetData(tmpParticlePosition);
					//solverData[i].particlePositionCorr.SetData(tmpZero);
					//solverData[i].particleVelocity.SetData(tmpZero);
					//solverData[i].particleVelocityPrev.SetData(tmpZero);
				}

				var rootMesh = componentGroups[i].rootFilter.sharedMesh;
				var rootTransform = componentGroups[i].rootFilter.transform.localToWorldMatrix;

				var strandDiameter = GetStrandDiameter();
				var strandScale = GetStrandScale();
				var strandRotation = GetRootRotation(componentGroups[i]);

				HairSim.UpdateSolverData(cmd, ref solverData[i], solverSettings, rootTransform, strandRotation, strandDiameter, strandScale, 1.0f);
				HairSim.UpdateSolverRoots(cmd, solverData[i], rootMesh);
				{
					HairSim.InitSolverParticles(cmd, solverData[i]);
				}
			}

			// init volume data
			{
				var simulationBounds = GetSimulationBounds();
				var strandDiameter = GetStrandDiameter();
				var strandScale = GetStrandScale();

				HairSim.UpdateVolumeData(cmd, ref volumeData, volumeSettings, simulationBounds, strandDiameter, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.InitSolverParticlesPostVolume(cmd, solverData[i], volumeData);
				}
			}

			// init renderers
			for (int i = 0; i != strandGroups.Length; i++)
			{
				UpdateRenderer(componentGroups[i], solverData[i]);
			}

			// ready
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

	//move to HairInstanceEditorUtility ?
	public static class HairInstanceBuilder
	{
		public static void ClearHairInstance(HairInstance hairInstance)
		{
			if (hairInstance.componentGroups == null)
				return;

			foreach (var componentGroup in hairInstance.componentGroups)
			{
				var material = componentGroup.lineRenderer.sharedMaterial;
				if (material != null)
				{
#if UNITY_EDITOR
					GameObject.DestroyImmediate(material);
#else
					GameObject.Destroy(material);
#endif
				}

				var container = componentGroup.container;
				if (container != null)
				{
#if UNITY_EDITOR
					GameObject.DestroyImmediate(container);
#else
					GameObject.Destroy(container);
#endif
				}
			}

			hairInstance.componentGroups = null;

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(hairInstance);
#endif
		}

		public static void BuildHairInstance(HairInstance hairInstance, HairAsset hairAsset)
		{
			ClearHairInstance(hairInstance);

			var strandGroups = hairAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return;

			// prep component groups
			hairInstance.componentGroups = new HairInstance.ComponentGroup[strandGroups.Length];

			// build component groups
			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var componentGroup = ref hairInstance.componentGroups[i];

				var container = new GameObject();
				{
					container.name = "Group:" + i;
					container.transform.SetParent(hairInstance.transform, worldPositionStays: false);
					container.hideFlags = HideFlags.NotEditable;

					var linesContainer = new GameObject();
					{
						linesContainer.name = "Lines:" + i;
						linesContainer.transform.SetParent(container.transform, worldPositionStays: false);
						linesContainer.hideFlags = HideFlags.NotEditable;

						componentGroup.lineFilter = linesContainer.AddComponent<MeshFilter>();
						componentGroup.lineFilter.sharedMesh = strandGroups[i].meshAssetLines;

						componentGroup.lineRenderer = linesContainer.AddComponent<MeshRenderer>();

						var material = hairInstance.GetStrandMaterial();
						if (material != null)
						{
							componentGroup.lineRenderer.sharedMaterial = new Material(material);
							componentGroup.lineRenderer.sharedMaterial.name += "(Instance)";
							componentGroup.lineRenderer.sharedMaterial.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
						}
					}

					var rootsContainer = new GameObject();
					{
						rootsContainer.name = "Roots:" + i;
						rootsContainer.transform.SetParent(container.transform, worldPositionStays: false);
						rootsContainer.hideFlags = HideFlags.NotEditable;

						componentGroup.rootFilter = rootsContainer.AddComponent<MeshFilter>();
						componentGroup.rootFilter.sharedMesh = strandGroups[i].meshAssetRoots;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
						componentGroup.rootAttachment = rootsContainer.AddComponent<SkinAttachment>();
						componentGroup.rootAttachment.attachmentType = SkinAttachment.AttachmentType.Mesh;
						componentGroup.rootAttachment.forceRecalculateBounds = true;
#endif
					}
				}

				hairInstance.componentGroups[i].container = container;
			}

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(hairInstance);
#endif
		}
	}
}
