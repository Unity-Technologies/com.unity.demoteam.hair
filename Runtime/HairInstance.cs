using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

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
		public struct SettingsRoots
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			[ToggleGroup]
			public bool rootsAttach;
			[ToggleGroupItem]
			public SkinAttachmentTarget rootsAttachTarget;
			[HideInInspector]
			public PrimarySkinningBone rootsAttachTargetBone;//TODO move to StrandGroupInstance?
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
				UniformWorldMin,
				UniformWorldMax,
			}

			public enum LODSelection
			{
				Fixed,
			}

			public enum StagingPrecision
			{
				Full,
				Half,
			}

			public enum StrandRenderer
			{
				BuiltinLines,
				BuiltinStrips,
#if HAS_PACKAGE_UNITY_VFXGRAPH
				VFXGraph,//TODO
#endif
			}

			public enum SimulationRate
			{
				Fixed30Hz,
				Fixed60Hz,
				Fixed120Hz,
				CustomTimeStep,
			}

			[LineHeader("Proportions")]

			[Tooltip("Behaviour when transform hierarchy is scaled")]
			public StrandScale strandScale;
			[Range(0.070f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
			public float strandDiameter;//TODO per-group
			[Range(0.0f, 100.0f), Tooltip("Strand margin (in millimeters)")]
			public float strandMargin;//TODO per-group

			[LineHeader("Geometry")]

			[ToggleGroup]
			public bool staging;
			[ToggleGroupItem(withLabel = true), Range(0, 10)]
			public uint stagingSubdivision;
			[EditableIf(nameof(staging), true)]
			public StagingPrecision stagingPrecision;

			[LineHeader("LOD")]

			public LODSelection kLODSearch;
			[Range(0.0f, 1.0f)]
			public float kLODSearchValue;
			public bool kLODBlending;

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
			public MotionVectorGenerationMode motionVectors;

			[LineHeader("Simulation")]

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
				strandScale = StrandScale.Fixed,
				strandDiameter = 1.0f,
				strandMargin = 0.0f,

				kLODSearch = LODSelection.Fixed,
				kLODSearchValue = 1.0f,
				kLODBlending = false,

				staging = true,
				stagingSubdivision = 0,
				stagingPrecision = StagingPrecision.Half,

				strandMaterial = false,
				strandMaterialValue = null,
				strandRenderer = StrandRenderer.BuiltinLines,
				strandShadows = ShadowCastingMode.On,
				strandLayers = 0x0101,//TODO this is the HDRP default -- should decide based on active pipeline asset
				motionVectors = MotionVectorGenerationMode.Camera,

				simulation = true,
				simulationRate = SimulationRate.Fixed30Hz,
				simulationInEditor = true,

				simulationTimeStep = 1.0f / 100.0f,
				stepsMin = false,
				stepsMinValue = 1,
				stepsMax = true,
				stepsMaxValue = 1,
			};
		}

		[Serializable]
		public struct StrandGroupInstance
		{
			public GameObject container;

			public GameObject rootContainer;
			public MeshFilter rootFilter;
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			public SkinAttachment rootAttachment;
#endif

			public GameObject strandContainer;
			public MeshFilter strandFilter;
			public MeshRenderer strandRenderer;

			[NonSerialized] public Material materialInstance;
			[NonSerialized] public Mesh meshInstanceLines;
			[NonSerialized] public Mesh meshInstanceStrips;
			[NonSerialized] public uint meshInstanceSubdivisionCount;
		}

		public HairAsset hairAsset;
		public bool hairAssetQuickEdit;

		public StrandGroupInstance[] strandGroupInstances;
		public string strandGroupInstancesChecksum;

		public SettingsRoots settingsRoots = SettingsRoots.defaults;
		public SettingsStrands settingsStrands = SettingsStrands.defaults;

		public HairSim.SolverSettings solverSettings = HairSim.SolverSettings.defaults;
		public HairSim.VolumeSettings volumeSettings = HairSim.VolumeSettings.defaults;
		public HairSim.DebugSettings debugSettings = HairSim.DebugSettings.defaults;

		public HairSim.SolverData[] solverData;
		public HairSim.VolumeData volumeData;

		[NonSerialized] public float accumulatedTime;
		[NonSerialized] public int stepsLastFrame;
		[NonSerialized] public float stepsLastFrameSmooth;
		[NonSerialized] public int stepsLastFrameSkipped;

		public event Action<CommandBuffer> onSimulationStateChanged;
		public event Action<CommandBuffer> onRenderingStateChanged;

		void OnEnable()
		{
			UpdateStrandGroupInstances();
			UpdateStrandGroupHideFlags();

			s_instances.Add(this);
		}

		void OnDisable()
		{
			ReleaseRuntimeData();

			s_instances.Remove(this);
		}

		void OnValidate()
		{
			volumeSettings.gridResolution = (Mathf.Max(8, volumeSettings.gridResolution) / 8) * 8;
		}

		void OnDrawGizmos()
		{
			if (strandGroupInstances != null)
			{
				// volume bounds
				Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
				Gizmos.DrawWireCube(HairSim.GetVolumeCenter(volumeData), 2.0f * HairSim.GetVolumeExtent(volumeData));

				// volume gravity
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					Gizmos.color = Color.cyan;
					Gizmos.DrawRay(HairSim.GetVolumeCenter(volumeData), solverData[i].cbuffer._WorldGravity * 0.1f);
				}
			}
		}

		void OnDrawGizmosSelected()
		{
			if (strandGroupInstances != null)
			{
				foreach (var strandGroupInstance in strandGroupInstances)
				{
					// root bounds
					var rootFilter = strandGroupInstance.rootFilter;
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

#if false
					// strand bounds
					var strandFilter = strandGroupInstance.strandFilter;
					if (strandFilter != null)
					{
						var strandMesh = strandFilter.sharedMesh;
						if (strandMesh != null)
						{
							var strandBounds = strandMesh.bounds;

							Gizmos.color = Color.Lerp(Color.green, Color.clear, 0.5f);
							Gizmos.matrix = rootFilter.transform.localToWorldMatrix;
							Gizmos.DrawWireCube(strandBounds.center, strandBounds.size);
						}
					}
#endif
				}
			}
		}

		void Update()
		{
			UpdateStrandGroupInstances();
			UpdateAttachedState();
		}

		void LateUpdate()
		{
			var cmd = CommandBufferPool.Get();
			{
				if (InitializeRuntimeData(cmd))
				{
					UpdateSimulationLOD(cmd);
					UpdateSimulationState(cmd);
					UpdateRenderingState(cmd);
					Graphics.ExecuteCommandBuffer(cmd);
				}
			}
			CommandBufferPool.Release(cmd);
		}

		void UpdateStrandGroupInstances()
		{
#if UNITY_EDITOR
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
			if (isPrefabInstance)
			{
				if (hairAsset != null)
				{
					// did the asset change since the prefab was built?
					if (hairAsset.checksum != strandGroupInstancesChecksum)
					{
						var prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(this);

#if UNITY_2021_2_OR_NEWER
						var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#else
						var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#endif
						if (prefabStage != null && prefabStage.assetPath == prefabPath)
							return;// do nothing if prefab is already open

						Debug.LogFormat(this, "{0}: rebuilding governing prefab '{1}'...", this.name, prefabPath);

						var prefabContainer = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
						if (prefabContainer != null)
						{
							foreach (var prefabHairInstance in prefabContainer.GetComponentsInChildren<HairInstance>(includeInactive: true))
							{
								prefabHairInstance.UpdateStrandGroupInstances();
							}

							UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefabContainer, prefabPath);
							UnityEditor.PrefabUtility.UnloadPrefabContents(prefabContainer);
						}

						ReleaseRuntimeData();
					}
				}
				else
				{
					ReleaseRuntimeData();
				}

				return;
			}
#endif

			if (hairAsset != null)
			{
				if (hairAsset.checksum != strandGroupInstancesChecksum)
				{
					HairInstanceBuilder.BuildHairInstance(this, hairAsset, HideFlags.NotEditable);
					ReleaseRuntimeData();
				}
			}
			else
			{
				HairInstanceBuilder.ClearHairInstance(this);
				ReleaseRuntimeData();
			}
		}

		void UpdateStrandGroupHideFlags(HideFlags hideFlags = HideFlags.NotEditable)
		{
			if (strandGroupInstances == null)
				return;

			foreach (var strandGroupInstance in strandGroupInstances)
			{
				strandGroupInstance.container.hideFlags = hideFlags;
				strandGroupInstance.rootContainer.hideFlags = hideFlags;
				strandGroupInstance.strandContainer.hideFlags = hideFlags;
			}
		}

		void UpdateAttachedState()
		{
			if (strandGroupInstances == null)
				return;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
	#if UNITY_EDITOR
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
			if (isPrefabInstance)
				return;
	#endif

			var attachmentsChanged = false;
			{
				foreach (var strandGroupInstance in strandGroupInstances)
				{
					var attachment = strandGroupInstance.rootAttachment;
					if (attachment != null && (attachment.target != settingsRoots.rootsAttachTarget || attachment.attached != settingsRoots.rootsAttach))
					{
						attachment.target = settingsRoots.rootsAttachTarget;

						if (attachment.target != null && settingsRoots.rootsAttach)
						{
							attachment.Attach(storePositionRotation: false);
						}
						else
						{
							attachment.Detach(revertPositionRotation: false);
							attachment.checksum0 = 0;
							attachment.checksum1 = 0;
						}

						attachmentsChanged = true;
					}
				}
			}

			if (attachmentsChanged && settingsRoots.rootsAttachTarget != null)
			{
				settingsRoots.rootsAttachTarget.CommitSubjectsIfRequired();
				settingsRoots.rootsAttachTargetBone = new PrimarySkinningBone(settingsRoots.rootsAttachTarget.transform);
	#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(settingsRoots.rootsAttachTarget);
	#endif
			}
#endif
		}

		void UpdateSimulationLOD(CommandBuffer cmd)
		{
			if (strandGroupInstances == null)
				return;

			var lodValue = settingsStrands.kLODSearchValue;
			var lodBlending = settingsStrands.kLODBlending;

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.PushSolverLOD(cmd, ref solverData[i], lodValue, lodBlending);
			}
		}

		void UpdateSimulationState(CommandBuffer cmd)
		{
			var stepCount = DispatchStepsAccumulated(cmd, Time.deltaTime);
			if (stepCount > 0)
			{
				// fire event
				if (onSimulationStateChanged != null)
					onSimulationStateChanged(cmd);
			}
		}

		void UpdateRenderingState(CommandBuffer cmd)
		{
			if (strandGroupInstances == null)
				return;

			for (int i = 0; i != solverData.Length; i++)
			{
				if (settingsStrands.staging)
				{
					var stagingCompression = (settingsStrands.stagingPrecision == SettingsStrands.StagingPrecision.Half);
					var stagingSubdivisions = settingsStrands.stagingSubdivision;

					if (HairSim.PrepareSolverStaging(ref solverData[i], stagingCompression, stagingSubdivisions))
					{
						HairSim.PushSolverStaging(cmd, ref solverData[i], stagingCompression, stagingSubdivisions, volumeData);
						HairSim.PushSolverStaging(cmd, ref solverData[i], stagingCompression, stagingSubdivisions, volumeData);
					}
					else
					{
						HairSim.PushSolverStaging(cmd, ref solverData[i], stagingCompression, stagingSubdivisions, volumeData);
					}
				}
				else
				{
					solverData[i].cbuffer._StagingVertexCount = 0;// forces re-init after enable (see PrepareSolverStaging)
					solverData[i].cbuffer._StagingSubdivision = 0;// ...
				}
			}

			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				switch (settingsStrands.strandRenderer)
				{
					case SettingsStrands.StrandRenderer.BuiltinLines:
					case SettingsStrands.StrandRenderer.BuiltinStrips:
						{
							UpdateRendererStateBuiltin(ref strandGroupInstances[i], solverData[i], hairAsset.strandGroups[i]);
						}
						break;

					case SettingsStrands.StrandRenderer.VFXGraph:
						{
							//TODO support output to vfx graph
						}
						break;
				}
			}

			// fire event
			if (onRenderingStateChanged != null)
				onRenderingStateChanged(cmd);
		}

		void UpdateRendererStateBuiltin(ref StrandGroupInstance strandGroupInstance, in HairSim.SolverData solverData, in HairAsset.StrandGroup strandGroup)
		{
			ref var meshFilter = ref strandGroupInstance.strandFilter;
			ref var meshRenderer = ref strandGroupInstance.strandRenderer;

			ref var materialInstance = ref strandGroupInstance.materialInstance;
			ref var meshInstanceLines = ref strandGroupInstance.meshInstanceLines;
			ref var meshInstanceStrips = ref strandGroupInstance.meshInstanceStrips;

			var subdivisionCount = solverData.cbuffer._StagingSubdivision;
			if (subdivisionCount != strandGroupInstance.meshInstanceSubdivisionCount)
			{
				strandGroupInstance.meshInstanceSubdivisionCount = subdivisionCount;

				CoreUtils.Destroy(meshInstanceLines);
				CoreUtils.Destroy(meshInstanceStrips);
			}

			switch (settingsStrands.strandRenderer)
			{
				case SettingsStrands.StrandRenderer.BuiltinLines:
					{
						if (subdivisionCount == 0)
						{
							HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceLines, strandGroup.meshAssetLines, HideFlags.HideAndDontSave);
						}
						else
						{
							HairInstanceBuilder.CreateMeshLinesIfNull(ref meshInstanceLines, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.cbuffer._StrandCount, (int)solverData.cbuffer._StagingVertexCount, new Bounds());
						}

						if (meshFilter.sharedMesh != meshInstanceLines)
							meshFilter.sharedMesh = meshInstanceLines;
					}
					break;

				case SettingsStrands.StrandRenderer.BuiltinStrips:
					{
						if (subdivisionCount == 0)
						{
							HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceStrips, strandGroup.meshAssetStrips, HideFlags.HideAndDontSave);
						}
						else
						{
							HairInstanceBuilder.CreateMeshStripsIfNull(ref meshInstanceStrips, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.cbuffer._StrandCount, (int)solverData.cbuffer._StagingVertexCount, new Bounds());
						}

						if (meshFilter.sharedMesh != meshInstanceStrips)
							meshFilter.sharedMesh = meshInstanceStrips;
					}
					break;
			}

			//TODO tighten renderer bounds
			//meshFilter.sharedMesh.bounds = GetSimulationBounds(worldSquare: false, worldToLocalTransform: meshFilter.transform.worldToLocalMatrix);
			if (meshFilter.sharedMesh != null)
				meshFilter.sharedMesh.bounds = GetSimulationBounds().WithTransform(meshFilter.transform.worldToLocalMatrix);

			var materialAsset = GetStrandMaterial();
			if (materialAsset != null)
			{
				if (materialInstance == null)
				{
					materialInstance = new Material(materialAsset);
					materialInstance.name += "(Instance)";
					materialInstance.hideFlags = HideFlags.HideAndDontSave;
				}
				else
				{
					if (materialInstance.shader != materialAsset.shader)
						materialInstance.shader = materialAsset.shader;

					materialInstance.CopyPropertiesFromMaterial(materialAsset);
				}
			}

			if (materialInstance != null)
			{
				meshRenderer.enabled = true;
				meshRenderer.sharedMaterial = materialInstance;
				meshRenderer.shadowCastingMode = settingsStrands.strandShadows;
				meshRenderer.renderingLayerMask = (uint)settingsStrands.strandLayers;
				meshRenderer.motionVectorGenerationMode = settingsStrands.motionVectors;

				HairSim.BindSolverData(materialInstance, solverData);
				HairSim.BindVolumeData(materialInstance, volumeData);

				materialInstance.SetTexture("_UntypedVolumeDensity", volumeData.volumeDensity);
				materialInstance.SetTexture("_UntypedVolumeVelocity", volumeData.volumeVelocity);
				materialInstance.SetTexture("_UntypedVolumeStrandCountProbe", volumeData.volumeStrandCountProbe);

				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_ID_LINES", settingsStrands.strandRenderer == SettingsStrands.StrandRenderer.BuiltinLines);
				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_ID_STRIPS", settingsStrands.strandRenderer == SettingsStrands.StrandRenderer.BuiltinStrips);
				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_SRC_SOLVER", !settingsStrands.staging);
				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_SRC_STAGING", settingsStrands.staging);
			}
			else
			{
				meshRenderer.enabled = false;
			}
		}

		public static Bounds GetRootBounds(in StrandGroupInstance strandGroupInstance, Matrix4x4? worldTransform = null)
		{
			var rootLocalBounds = strandGroupInstance.rootFilter.sharedMesh.bounds;
			var rootLocalToWorld = strandGroupInstance.rootFilter.transform.localToWorldMatrix;
			{
				return rootLocalBounds.WithTransform((worldTransform != null) ? (worldTransform.Value * rootLocalToWorld) : rootLocalToWorld);
			}
		}

		public Quaternion GetRootRotation(in StrandGroupInstance strandGroupInstance)
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			if (settingsRoots.rootsAttach && settingsRoots.rootsAttachTarget != null)
			{
				return settingsRoots.rootsAttachTargetBone.skinningBone.rotation;
			}
#endif
			return strandGroupInstance.rootFilter.transform.rotation;
		}

		public bool GetSimulationActive()
		{
			return settingsStrands.simulation && (settingsStrands.simulationInEditor || Application.isPlaying);
		}

		public float GetSimulationTimeStep()
		{
			switch (settingsStrands.simulationRate)
			{
				case SettingsStrands.SimulationRate.Fixed30Hz: return 1.0f / 30.0f;
				case SettingsStrands.SimulationRate.Fixed60Hz: return 1.0f / 60.0f;
				case SettingsStrands.SimulationRate.Fixed120Hz: return 1.0f / 120.0f;
				case SettingsStrands.SimulationRate.CustomTimeStep: return settingsStrands.simulationTimeStep;
				default: return 0.0f;
			}
		}

		public Bounds GetSimulationBounds(bool worldSquare = true, Matrix4x4? worldToLocalTransform = null)
		{
			Debug.Assert(worldSquare == false || worldToLocalTransform == null);

			var strandScale = GetStrandScale();
			var rootBounds = GetRootBounds(strandGroupInstances[0], worldToLocalTransform);
			var rootMargin = hairAsset.strandGroups[0].maxStrandLength * strandScale;

			for (int i = 1; i != strandGroupInstances.Length; i++)
			{
				rootBounds.Encapsulate(GetRootBounds(strandGroupInstances[i], worldToLocalTransform));
				rootMargin = Mathf.Max(hairAsset.strandGroups[i].maxStrandLength * strandScale, rootMargin);
			}

			rootMargin *= 1.25f;
			rootBounds.Expand(2.0f * rootMargin);

			if (worldSquare)
				return new Bounds(rootBounds.center, rootBounds.size.CMax() * Vector3.one);
			else
				return rootBounds;
		}

		public float GetStrandDiameter()
		{
			return settingsStrands.strandDiameter;
		}

		public float GetStrandMargin()
		{
			return settingsStrands.strandMargin;
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
				case SettingsStrands.StrandScale.UniformWorldMin:
					{
						var lossyScaleAbs = this.transform.lossyScale.Abs();
						var lossyScaleAbsMin = lossyScaleAbs.CMin();
						return lossyScaleAbsMin;
					}
				case SettingsStrands.StrandScale.UniformWorldMax:
					{
						var lossyScaleAbs = this.transform.lossyScale.Abs();
						var lossyScaleAbsMax = lossyScaleAbs.CMax();
						return lossyScaleAbsMax;
					}
			}
		}

		public Material GetStrandMaterial()
		{
			var mat = null as Material;

			if (mat == null && settingsStrands.strandMaterial)
				mat = settingsStrands.strandMaterialValue;

			//TODO handle this as part of ui cleanup
			if (mat == null && hairAsset != null)
				mat = hairAsset.settingsBasic.material;

			if (mat == null)
				mat = HairMaterialUtility.GetCurrentPipelineDefault();

			return mat;
		}

		public int DispatchStepsAccumulated(CommandBuffer cmd, float dt)
		{
			var active = GetSimulationActive();
			var stepDT = GetSimulationTimeStep();

			// skip if inactive or if time step zero
			if (stepDT == 0.0f || active == false)
			{
				stepsLastFrame = 0;
				stepsLastFrameSmooth = 0.0f;
				stepsLastFrameSkipped = 0;
				return 0;// no steps performed
			}

			// add time passed
			accumulatedTime += dt;

			// calc number of steps
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
			if (stepCount > 0)
			{
				DispatchSteps(cmd, stepDT, stepCount);
			}

			// update counters
			stepsLastFrame = stepCount;
			stepsLastFrameSmooth = Mathf.Lerp(stepsLastFrameSmooth, stepsLastFrame, 1.0f - Mathf.Pow(0.01f, dt / 0.2f));
			stepsLastFrameSkipped = Mathf.Max(0, stepCountRT - stepCount);

			// return steps performced
			return stepCount;
		}

		public void DispatchSteps(CommandBuffer cmd, float stepDT, int stepCount)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			// get bounds and scale
			var simulationBounds = GetSimulationBounds();
			var strandDiameter = GetStrandDiameter();
			var strandMargin = GetStrandMargin();
			var strandScale = GetStrandScale();

			// update solver roots
			for (int i = 0; i != solverData.Length; i++)
			{
				var rootMesh = strandGroupInstances[i].rootFilter.sharedMesh;
				var rootTransform = strandGroupInstances[i].rootFilter.transform.localToWorldMatrix;
				var strandRotation = GetRootRotation(strandGroupInstances[i]);

				HairSim.PushSolverParams(cmd, ref solverData[i], solverSettings, rootTransform, strandRotation, strandDiameter, strandScale, stepDT);
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
			}

			// update volume boundaries
			HairSim.PushVolumeBoundaries(cmd, ref volumeData, volumeSettings, simulationBounds);// TODO handle substeps within frame (interpolate colliders)

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, volumeSettings))
			{
				HairSim.PushVolumeParams(cmd, ref volumeData, volumeSettings, simulationBounds, strandDiameter + strandMargin, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
			}

			// perform the steps
			for (int i = 0; i != stepCount; i++)
			{
				float stepFracLo = (i + 0) / (float)stepCount;
				float stepFracHi = (i + 1) / (float)stepCount;

				// step solver data
				for (int j = 0; j != solverData.Length; j++)
				{
					HairSim.StepSolverData(cmd, ref solverData[j], solverSettings, volumeData, stepFracLo, stepFracHi);
				}

				// step volume data
				HairSim.PushVolumeParams(cmd, ref volumeData, volumeSettings, simulationBounds, strandDiameter + strandMargin, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
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

		bool InitializeRuntimeData(CommandBuffer cmd)
		{
			if (hairAsset == null)
				return false;

			if (hairAsset.checksum != strandGroupInstancesChecksum)
				return false;

			var strandGroups = hairAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return false;

			if (solverData != null && solverData.Length == strandGroups.Length)
				return true;

			// prep volume data
			HairSim.PrepareVolumeData(ref volumeData, volumeSettings);

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

				HairSim.PrepareSolverData(ref solverData[i], strandGroup.strandCount, strandGroup.strandParticleCount, strandGroup.lodCount);
				{
					solverData[i].memoryLayout = strandGroup.particleMemoryLayout;
					solverData[i].cbuffer._StrandCount = (uint)strandGroup.strandCount;
					solverData[i].cbuffer._StrandParticleCount = (uint)strandGroup.strandParticleCount;
					solverData[i].cbuffer._StrandMaxParticleInterval = strandGroup.maxParticleInterval;
					solverData[i].cbuffer._StrandMaxParticleWeight = strandGroup.maxParticleInterval / volumeData.allGroupsMaxParticleInterval;
					solverData[i].lodGuideCountCPU = new NativeArray<int>(strandGroup.lodGuideCount, Allocator.Persistent);
					solverData[i].lodThreshold = new NativeArray<float>(strandGroup.lodThreshold, Allocator.Persistent);
				}

				int strandGroupParticleCount = strandGroup.strandCount * strandGroup.strandParticleCount;

				using (var alignedRootPosition = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var alignedRootDirection = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var alignedParticlePosition = new NativeArray<Vector4>(strandGroupParticleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						fixed (void* rootPositionPtr = strandGroup.rootPosition)
						fixed (void* rootDirectionPtr = strandGroup.rootDirection)
						fixed (void* particlePositionPtr = strandGroup.particlePosition)
						{
							UnsafeUtility.MemCpyStride(alignedRootPosition.GetUnsafePtr(), sizeof(Vector4), rootPositionPtr, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
							UnsafeUtility.MemCpyStride(alignedRootDirection.GetUnsafePtr(), sizeof(Vector4), rootDirectionPtr, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
							UnsafeUtility.MemCpyStride(alignedParticlePosition.GetUnsafePtr(), sizeof(Vector4), particlePositionPtr, sizeof(Vector3), sizeof(Vector3), strandGroupParticleCount);
						}
					}

					solverData[i].rootUV.SetData(strandGroup.rootUV);
					solverData[i].rootScale.SetData(strandGroup.rootScale);
					solverData[i].rootPosition.SetData(alignedRootPosition);
					solverData[i].rootDirection.SetData(alignedRootDirection);

					solverData[i].particlePosition.SetData(alignedParticlePosition);

					solverData[i].lodGuideCount.SetData(strandGroup.lodGuideCount);
					solverData[i].lodGuideIndex.SetData(strandGroup.lodGuideIndex);
					solverData[i].lodGuideCarry.SetData(strandGroup.lodGuideCarry);

					// NOTE: the remaining buffers are initialized in KInitialize and KInitializePostVolume
				}

				var rootMesh = strandGroupInstances[i].rootFilter.sharedMesh;
				var rootTransform = strandGroupInstances[i].rootFilter.transform.localToWorldMatrix;

				var strandDiameter = GetStrandDiameter();
				var strandScale = GetStrandScale();
				var strandRotation = GetRootRotation(strandGroupInstances[i]);

				HairSim.PushSolverLOD(cmd, ref solverData[i], strandGroup.lodCount - 1);//TODO will need to move this around to generate rest density per LOD, to support target density initial pose in particles
				HairSim.PushSolverParams(cmd, ref solverData[i], solverSettings, rootTransform, strandRotation, strandDiameter, strandScale, 1.0f);
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
				{
					HairSim.InitSolverData(cmd, solverData[i]);
				}

				//TODO clean this up (currently necessary for full initialization of root buffers)
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
			}

			// init volume data
			{
				var simulationBounds = GetSimulationBounds();
				var strandDiameter = GetStrandDiameter();
				var strandMargin = GetStrandMargin();
				var strandScale = GetStrandScale();

				HairSim.PushVolumeParams(cmd, ref volumeData, volumeSettings, simulationBounds, strandDiameter + strandMargin, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.InitSolverDataPostVolume(cmd, solverData[i], volumeData);
				}
			}

			// ready
			return true;
		}

		void ReleaseRuntimeData()
		{
			if (strandGroupInstances != null)
			{
				foreach (var strandGroupInstance in strandGroupInstances)
				{
					CoreUtils.Destroy(strandGroupInstance.materialInstance);
					CoreUtils.Destroy(strandGroupInstance.meshInstanceLines);
					CoreUtils.Destroy(strandGroupInstance.meshInstanceStrips);
				}
			}

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

		public void ResetSimulationState()
		{
			ReleaseRuntimeData();
		}
	}
}
