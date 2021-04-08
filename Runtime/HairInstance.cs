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
			public MaterialPropertyBlock lineRendererMPB;
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
				UniformLowerBound,
				UniformUpperBound,
			}

			public enum StrandRenderer
			{
				PrimitiveLines,
				InstancedMesh,//TODO
#if HAS_PACKAGE_UNITY_VFXGRAPH
				VFXGraph,//TODO
#endif
			}

			public enum Simulation
			{
				Disabled,
				Enabled,
				EnabledInPlaymode,
			}

			public enum SimulationRate
			{
				Fixed30Hz,
				Fixed60Hz,
				Fixed120Hz,
				CustomTimeStep,
			}

			[LineHeader("Sizing")]

			[Range(0.070f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
			public float strandDiameter;
			[Tooltip("Strand scale")]
			public StrandScale strandScale;

			[LineHeader("Rendering")]

			[ToggleGroup]
			public bool strandMaterial;
			[ToggleGroupItem]
			public Material strandMaterialValue;
			public StrandRenderer strandRenderer;
			[VisibleIf(nameof(strandRenderer), StrandRenderer.InstancedMesh)]
			public Mesh strandMesh;
#if HAS_PACKAGE_UNITY_VFXGRAPH
			[VisibleIf(nameof(strandRenderer), StrandRenderer.VFXGraph)]
			public VisualEffect strandOutputGraph;
#endif
			public ShadowCastingMode strandShadows;
			[RenderingLayerMask]
			public int strandLayers;

			[LineHeader("Physics")]

			[Tooltip("Simulation state")]
			public Simulation simulation;
			[Tooltip("Simulation update rate")]
			public SimulationRate simulationRate;
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

				simulation = Simulation.Enabled,
				simulationRate = SimulationRate.Fixed60Hz,
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
			switch (settingsStrands.simulation)
			{
				case SettingsStrands.Simulation.Enabled: return true;
				case SettingsStrands.Simulation.EnabledInPlaymode: return Application.isPlaying;
			}

			return false;
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

				case SettingsStrands.StrandScale.UniformLowerBound:
					{
						var lossyScaleAbs = this.transform.lossyScale.Abs();
						var lossyScaleAbsMin = lossyScaleAbs.ComponentMin();
						return lossyScaleAbsMin;
					}

				case SettingsStrands.StrandScale.UniformUpperBound:
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

		public void DispatchTime(CommandBuffer cmd, float dt)
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

				var strandTransform = Matrix4x4.TRS(Vector3.zero, GetRootRotation(componentGroups[i]), Vector3.one * strandScale);

				HairSim.UpdateSolverData(cmd, ref solverData[i], solverSettings, strandTransform, strandScale, dt);
				HairSim.UpdateSolverRoots(cmd, rootMesh, rootTransform, solverData[i]);
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

		public void UpdateRenderer(CommandBuffer cmd, in ComponentGroup componentGroup, in HairSim.SolverData solverData)
		{
			var lineRenderer = componentGroup.lineRenderer;
			var lineRendererMPB = componentGroup.lineRendererMPB;

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
				HairSim.PushSolverData(cmd, lineRenderer.sharedMaterial, lineRendererMPB, solverData);

				lineRenderer.sharedMaterial.EnableKeyword("HAIRSIMVERTEX_ENABLE_POSITION");
				lineRenderer.SetPropertyBlock(componentGroup.lineRendererMPB);
			}

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

						Debug.Log("... rebuilding underlying prefab");

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

				var strandScale = GetStrandScale();
				var strandTransform = Matrix4x4.TRS(Vector3.zero, GetRootRotation(componentGroups[i]), Vector3.one * strandScale);

				HairSim.UpdateSolverData(cmd, ref solverData[i], solverSettings, strandTransform, strandScale, 1.0f);
				HairSim.UpdateSolverRoots(cmd, rootMesh, rootTransform, solverData[i]);
				{
					HairSim.InitSolverParticles(cmd, solverData[i], strandTransform);
				}

				// init renderer
				if (componentGroups[i].lineRendererMPB == null)
					componentGroups[i].lineRendererMPB = new MaterialPropertyBlock();

				UpdateRenderer(cmd, componentGroups[i], solverData[i]);
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
