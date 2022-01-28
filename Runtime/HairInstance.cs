#define REMOVE_AFTER_CONTENT_UPGRADE

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
		public struct GroupProvider
		{
			public HairAsset hairAsset;
			public bool hairAssetQuickEdit;
		}

		[Serializable]
		public struct GroupAssetReference : IEquatable<GroupAssetReference>
		{
			public HairAsset hairAsset;
			public int hairAssetGroupIndex;

			public ulong GetSortKey()
			{
				var a = (ulong)hairAsset.GetInstanceID();
				var b = (ulong)hairAssetGroupIndex;
				return (a << 32) | b;
			}

			public ulong GetSortKey48()
			{
				var a = (ulong)hairAsset.GetInstanceID();
				var b = (ulong)hairAssetGroupIndex & 0xffffuL;
				return (a << 16) | b;
			}

			public ref HairAsset.StrandGroup Resolve()
			{
				return ref hairAsset.strandGroups[hairAssetGroupIndex];
			}

			bool IEquatable<GroupAssetReference>.Equals(GroupAssetReference other)
			{
				return (GetSortKey() == other.GetSortKey());
			}
		}

		[Serializable]
		public struct GroupInstance
		{
			[Serializable]
			public struct SceneObjects
			{
				public GameObject groupContainer;

				public GameObject rootMeshContainer;
				public MeshFilter rootMeshFilter;
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
				public SkinAttachment rootMeshAttachment;
#endif

				public GameObject strandMeshContainer;
				public MeshFilter strandMeshFilter;
				public MeshRenderer strandMeshRenderer;

				[NonSerialized] public Material materialInstance;
				[NonSerialized] public Mesh meshInstanceLines;
				[NonSerialized] public Mesh meshInstanceStrips;
				[NonSerialized] public uint meshInstanceSubdivisionCount;
			}

#if REMOVE_AFTER_CONTENT_UPGRADE
			public GameObject container;
#endif

			public GroupAssetReference groupAssetReference;
			public SceneObjects sceneObjects;
			public int settingsIndex;
		}

		[Serializable]
		public struct GroupSettings
		{
			public List<GroupAssetReference> groupAssetReferences;

			public bool settingsSkinningOverride;
			public SettingsSkinning settingsSkinning;

			public bool settingsStrandsOverride;
			public SettingsStrands settingsStrands;

			public bool settingsSolverOverride;
			public HairSim.SolverSettings settingsSolver;

			public static GroupSettings defaults => new GroupSettings()
			{
				groupAssetReferences = new List<GroupAssetReference>(1),

				settingsSkinningOverride = true,
				settingsSkinning = SettingsSkinning.defaults,

				settingsStrandsOverride = true,
				settingsStrands = SettingsStrands.defaults,

				settingsSolverOverride = true,
				settingsSolver = HairSim.SolverSettings.defaults,
			};
		}

		[Serializable]
		public struct SettingsSystem
		{
			public enum LODSelection
			{
				Automatic,//TODO
				Fixed,
			}

			public enum BoundsMode
			{
				Automatic,
				Fixed,//TODO
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

			[LineHeader("LOD")]

			public LODSelection kLODSearch;
			[Range(0.0f, 1.0f)]
			public float kLODSearchValue;
			public bool kLODBlending;

			[LineHeader("Bounds")]

			public BoundsMode bounds;
			[VisibleIf(nameof(bounds), BoundsMode.Fixed)]
			public Vector3 boundsCenter;
			[VisibleIf(nameof(bounds), BoundsMode.Fixed)]
			public Vector3 boundsExtent;
			[Range(0.0f, 1.0f)]
			public float boundsTrim;

			[LineHeader("Renderer")]

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

			public static readonly SettingsSystem defaults = new SettingsSystem()
			{
				kLODSearch = LODSelection.Fixed,
				kLODSearchValue = 1.0f,
				kLODBlending = false,

				bounds = BoundsMode.Automatic,
				boundsCenter = new Vector3(0.0f, 0.0f, 0.0f),
				boundsExtent = new Vector3(1.0f, 1.0f, 1.0f),
				boundsTrim = 0.0f,

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
		public struct SettingsSkinning
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			[LineHeader("Skinning")]

			[ToggleGroup]
			public bool rootsAttach;
			[ToggleGroupItem]
			public SkinAttachmentTarget rootsAttachTarget;
			[HideInInspector]
			public PrimarySkinningBone rootsAttachTargetBone;//TODO move to StrandGroupInstance?
#endif

			public static readonly SettingsSkinning defaults = new SettingsSkinning()
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

			public enum StagingPrecision
			{
				Full,
				Half,
			}

			[LineHeader("Material")]

			[ToggleGroup]
			public bool material;
			[ToggleGroupItem]
			public Material materialValue;

			[LineHeader("Proportions")]

			public StrandScale strandScale;
			[Range(0.070f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
			public float strandDiameter;
			[Range(0.0f, 100.0f), Tooltip("Strand margin (in millimeters)")]
			public float strandMargin;

			[LineHeader("Geometry")]

			[EditableIf(nameof(staging), true)]
			public StagingPrecision stagingPrecision;
			[ToggleGroup]
			public bool staging;
			[ToggleGroupItem(withLabel = true), Range(0, 10)]
			public uint stagingSubdivision;

			public static readonly SettingsStrands defaults = new SettingsStrands()
			{
				material = false,
				materialValue = null,

				strandScale = StrandScale.Fixed,
				strandDiameter = 1.0f,
				strandMargin = 0.0f,

				staging = true,
				stagingSubdivision = 0,
				stagingPrecision = StagingPrecision.Half,
			};

#if REMOVE_AFTER_CONTENT_UPGRADE
			[HideInInspector] public float kLODSearchValue;
			[HideInInspector] public bool kLODBlending;
			[HideInInspector] public SettingsSystem.StrandRenderer strandRenderer;
			[HideInInspector] public ShadowCastingMode strandShadows;
			[HideInInspector] public int strandLayers;
			[HideInInspector] public MotionVectorGenerationMode motionVectors;
			[HideInInspector] public bool simulation;
			[HideInInspector] public SettingsSystem.SimulationRate simulationRate;
			[HideInInspector] public bool simulationInEditor;
			[HideInInspector] public float simulationTimeStep;
			[HideInInspector] public bool stepsMin;
			[HideInInspector] public int stepsMinValue;
			[HideInInspector] public bool stepsMax;
			[HideInInspector] public int stepsMaxValue;
#endif
		}

#if REMOVE_AFTER_CONTENT_UPGRADE
		[SerializeField, HideInInspector] private HairAsset hairAsset;
		[SerializeField, HideInInspector] private bool hairAssetQuickEdit;
#endif

		public GroupProvider[] strandGroupProviders = new GroupProvider[1];
		public GroupInstance[] strandGroupInstances;
		public GroupSettings[] strandGroupSettings;
		public string[] strandGroupChecksums;// stores checksums of providers for instantiated groups

		public SettingsSystem settingsSystem = SettingsSystem.defaults;					// per instance
		public bool settingsExpanded = true;
		[FormerlySerializedAs("settingsRoots")]
		public SettingsSkinning settingsSkinning = SettingsSkinning.defaults;			// per group
		public SettingsStrands settingsStrands = SettingsStrands.defaults;				// per group
		public HairSim.SolverSettings settingsSolver = HairSim.SolverSettings.defaults;	// per group
		public HairSim.VolumeSettings settingsVolume = HairSim.VolumeSettings.defaults;	// per instance
		public HairSim.DebugSettings settingsDebug = HairSim.DebugSettings.defaults;	// per instance

		[NonSerialized] public HairSim.SolverData[] solverData; // per group
		[NonSerialized] public HairSim.VolumeData volumeData;	// per instance

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
			UpdateStrandGroupSettings();

			s_instances.Add(this);
		}

		void OnDisable()
		{
			ReleaseRuntimeData();

			s_instances.Remove(this);
		}

		void OnValidate()
		{
			settingsVolume.gridResolution = (Mathf.Max(8, settingsVolume.gridResolution) / 8) * 8;
		}

		void OnDrawGizmos()
		{
			if (strandGroupInstances == null)
				return;

			// volume bounds
			Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
			Gizmos.DrawWireCube(HairSim.GetVolumeCenter(volumeData), 2.0f * HairSim.GetVolumeExtent(volumeData));

			// volume gravity
			for (int i = 0; i != solverData.Length; i++)
			{
				Gizmos.color = Color.cyan;
				Gizmos.DrawRay(HairSim.GetVolumeCenter(volumeData), solverData[i].cbuffer._WorldGravity * 0.1f);
			}
		}

		void OnDrawGizmosSelected()
		{
			if (strandGroupInstances == null)
				return;

			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				ref readonly var strandGroupInstance = ref strandGroupInstances[i];

				// root bounds
				var rootMeshFilter = strandGroupInstance.sceneObjects.rootMeshFilter;
				if (rootMeshFilter != null)
				{
					var rootMesh = rootMeshFilter.sharedMesh;
					if (rootMesh != null)
					{
						var rootBounds = rootMesh.bounds;

						Gizmos.color = Color.Lerp(Color.blue, Color.clear, 0.5f);
						Gizmos.matrix = rootMeshFilter.transform.localToWorldMatrix;
						Gizmos.DrawWireCube(rootBounds.center, rootBounds.size);
					}
				}

#if false
				// strand bounds
				var strandMeshFilter = strandGroupInstance.sceneObjects.strandMeshFilter;
				if (strandMeshFilter != null)
				{
					var strandMesh = strandMeshFilter.sharedMesh;
					if (strandMesh != null)
					{
						var strandBounds = strandMesh.bounds;

						Gizmos.color = Color.Lerp(Color.green, Color.clear, 0.5f);
						Gizmos.matrix = rootMeshFilter.transform.localToWorldMatrix;
						Gizmos.DrawWireCube(strandBounds.center, strandBounds.size);
					}
				}
#endif
			}
		}

		void Update()
		{
			UpdateStrandGroupInstances();
			UpdateStrandGroupSettings();
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

		enum StrandGroupInstancesStatus
		{
			RequireRebuild,
			RequireRelease,
			Valid,
		}

		StrandGroupInstancesStatus CheckStrandGroupInstances()
		{
			var strandGroupChecksumCount = strandGroupChecksums?.Length ?? 0;
			var strandGroupProviderPass = 0;
			var strandGroupProviderFail = 0;

			for (int i = 0, readIndexChecksum = 0; i != strandGroupProviders.Length; i++)
			{
				var hairAsset = strandGroupProviders[i].hairAsset;
				if (hairAsset == null || hairAsset.checksum == string.Empty)
					continue;

				if (readIndexChecksum < strandGroupChecksumCount)
				{
					if (strandGroupChecksums[readIndexChecksum++] == hairAsset.checksum)
						strandGroupProviderPass++;
					else
						strandGroupProviderFail++;
				}
				else
				{
					strandGroupProviderFail++;
				}
			}

			var validChecksums = (strandGroupProviderPass == strandGroupChecksumCount);
			var validProviders = (strandGroupProviderFail == 0);
			if (validProviders && validChecksums)
			{
				return StrandGroupInstancesStatus.Valid;
			}
			else
			{
				var strandGroupProviderCount = strandGroupProviderPass + strandGroupProviderFail;
				if (strandGroupProviderCount > 0)
					return StrandGroupInstancesStatus.RequireRebuild;
				else
					return StrandGroupInstancesStatus.RequireRelease;
			}
		}

		void UpdateStrandGroupInstances()
		{
			var status = CheckStrandGroupInstances();

#if REMOVE_AFTER_CONTENT_UPGRADE
			if (hairAsset != null)
			{
				status = StrandGroupInstancesStatus.RequireRebuild;
			}
#endif

#if UNITY_EDITOR
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
			if (isPrefabInstance)
			{
				// did the asset change since the prefab was built?
				switch (status)
				{
					case StrandGroupInstancesStatus.RequireRebuild:
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
						}
						ReleaseRuntimeData();
						break;

					case StrandGroupInstancesStatus.RequireRelease:
						ReleaseRuntimeData();
						break;
				}

				return;
			}
#endif

#if REMOVE_AFTER_CONTENT_UPGRADE
			if (hairAsset != null)
			{
				settingsSystem.kLODSearchValue = settingsStrands.kLODSearchValue;
				settingsSystem.kLODBlending = settingsStrands.kLODBlending;
				settingsSystem.strandRenderer = settingsStrands.strandRenderer;
				settingsSystem.strandShadows = settingsStrands.strandShadows;
				settingsSystem.strandLayers = settingsStrands.strandLayers;
				settingsSystem.motionVectors = settingsStrands.motionVectors;
				settingsSystem.simulation = settingsStrands.simulation;
				settingsSystem.simulationRate = settingsStrands.simulationRate;
				settingsSystem.simulationInEditor = settingsStrands.simulationInEditor;
				settingsSystem.simulationTimeStep = settingsStrands.simulationTimeStep;
				settingsSystem.stepsMin = settingsStrands.stepsMin;
				settingsSystem.stepsMinValue = settingsStrands.stepsMinValue;
				settingsSystem.stepsMax = settingsStrands.stepsMax;
				settingsSystem.stepsMaxValue = settingsStrands.stepsMaxValue;

				CoreUtils.Destroy(strandGroupInstances?[0].container);

				strandGroupProviders = new GroupProvider[1];
				strandGroupProviders[0].hairAsset = hairAsset;
				strandGroupProviders[0].hairAssetQuickEdit = hairAssetQuickEdit;
				strandGroupInstances = null;
				strandGroupChecksums = null;
				strandGroupSettings = null;

				hairAsset = null;
				hairAssetQuickEdit = false;
			}
#endif

			switch (status)
			{
				case StrandGroupInstancesStatus.RequireRebuild:
					HairInstanceBuilder.BuildHairInstance(this, strandGroupProviders, HideFlags.NotEditable);
					ReleaseRuntimeData();
					break;

				case StrandGroupInstancesStatus.RequireRelease:
					HairInstanceBuilder.ClearHairInstance(this);
					ReleaseRuntimeData();
					break;
			}
		}

		void UpdateStrandGroupHideFlags(HideFlags hideFlags = HideFlags.NotEditable)
		{
			if (strandGroupInstances == null)
				return;

			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				ref readonly var strandGroupInstance = ref strandGroupInstances[i];

				strandGroupInstance.sceneObjects.groupContainer.hideFlags = hideFlags;
				strandGroupInstance.sceneObjects.rootMeshContainer.hideFlags = hideFlags;
				strandGroupInstance.sceneObjects.strandMeshContainer.hideFlags = hideFlags;
			}
		}

		void UpdateStrandGroupSettings()
		{
			if (strandGroupSettings == null)
				return;
			if (strandGroupInstances == null)
				return;

			// remove duplicate references (meaning only one settings block can affect instances of a particular group asset)
			{
				var groupAssetKeyCapacity = 0;
				{
					for (int i = 0; i != strandGroupSettings.Length; i++)
					{
						groupAssetKeyCapacity += strandGroupSettings[i].groupAssetReferences.Count;
					}
				}

				using (var groupAssetKeys = new UnsafeHashSet<ulong>(groupAssetKeyCapacity, Allocator.Temp))
				{
					for (int i = 0; i != strandGroupSettings.Length; i++)
					{
						var groupAssetReferences = strandGroupSettings[i].groupAssetReferences;

						for (int j = groupAssetReferences.Count - 1; j >= 0; j--)
						{
							var groupAssetKey = groupAssetReferences[j].GetSortKey();
							if (groupAssetKeys.Contains(groupAssetKey) == false)
							{
								groupAssetKeys.Add(groupAssetKey);
							}
							else
							{
								groupAssetReferences.RemoveAtSwapBack(j);
							}
						}
					}
				}
			}

			// map settings to group instances
#if true
			using (var groupAssetInstancesMap = new UnsafeMultiHashMap<ulong, int>(strandGroupInstances.Length, Allocator.Temp))
			{
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					groupAssetInstancesMap.Add(strandGroupInstances[i].groupAssetReference.GetSortKey(), i);
					strandGroupInstances[i].settingsIndex = -1;
				}

				for (int i = 0; i != strandGroupSettings.Length; i++)
				{
					foreach (var groupAssetReference in strandGroupSettings[i].groupAssetReferences)
					{
						var groupAssetKey = groupAssetReference.GetSortKey();
						if (groupAssetInstancesMap.TryGetFirstValue(groupAssetKey, out var j, out var iterator))
						{
							do { strandGroupInstances[j].settingsIndex = i; }
							while (groupAssetInstancesMap.TryGetNextValue(out j, ref iterator));
						}
					}
				}
			}
#else		// alt. path without multihashmap
			unsafe
			{
				using (var sortedGroupInstances = new NativeArray<ulong>(strandGroupInstances.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var sortedGroupInstancesPtr = (ulong*)sortedGroupInstances.GetUnsafePtr();

					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						var groupAssetKey = (strandGroupInstances[i].groupAssetReference.GetSortKey48() << 16);
						{
							sortedGroupInstancesPtr[i] = groupAssetKey | ((ulong)i & 0xffffuL);
						}
					}

					sortedGroupInstances.Sort();

					for (int i = 0; i != strandGroupSettings.Length; i++)
					{
						foreach (var groupAssetReference in strandGroupSettings[i].groupAssetReferences)
						{
							var groupAssetKey = groupAssetReference.GetSortKey48() << 16;
							var groupAssetMask = (~0uL) << 16;

							var j = sortedGroupInstances.BinarySearch(groupAssetKey);
							if (j < 0)
								j = ~j;

							while (j < sortedGroupInstances.Length)
							{
								if ((sortedGroupInstancesPtr[j] & groupAssetMask) != groupAssetKey)
									break;

								strandGroupInstances[sortedGroupInstancesPtr[j++] & 0xffffuL].settingsIndex = i;
							}
						}
					}
				}
			}
#endif
		}

		public void AssignStrandGroupSettings(int settingsIndex, int instanceIndex)
		{
			if (strandGroupSettings == null || strandGroupSettings.Length <= settingsIndex)
				return;
			if (strandGroupInstances == null || strandGroupInstances.Length <= instanceIndex)
				return;

			ref var groupInstance = ref strandGroupInstances[instanceIndex];
			ref var groupAssetReference = ref groupInstance.groupAssetReference;

			var prevSettingsIndex = groupInstance.settingsIndex;
			if (prevSettingsIndex >= 0 && prevSettingsIndex < strandGroupSettings.Length)
			{
				strandGroupSettings[prevSettingsIndex].groupAssetReferences.Remove(groupAssetReference);
			}

			groupInstance.settingsIndex = -1;

			if (settingsIndex >= 0)
			{
				strandGroupSettings[settingsIndex].groupAssetReferences.Add(groupAssetReference);
				groupInstance.settingsIndex = settingsIndex;
			}

			UpdateStrandGroupSettings();
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
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					ref readonly var strandGroupInstance = ref strandGroupInstances[i];

					var attachment = strandGroupInstance.sceneObjects.rootMeshAttachment;
					if (attachment != null && (attachment.target != settingsSkinning.rootsAttachTarget || attachment.attached != settingsSkinning.rootsAttach))
					{
						attachment.target = settingsSkinning.rootsAttachTarget;

						if (attachment.target != null && settingsSkinning.rootsAttach)
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

			if (attachmentsChanged && settingsSkinning.rootsAttachTarget != null)
			{
				settingsSkinning.rootsAttachTarget.CommitSubjectsIfRequired();
				settingsSkinning.rootsAttachTargetBone = new PrimarySkinningBone(settingsSkinning.rootsAttachTarget.transform);
#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(settingsSkinning.rootsAttachTarget);
#endif
			}
#endif
		}

		void UpdateSimulationLOD(CommandBuffer cmd)
		{
			var lodValue = settingsSystem.kLODSearchValue;
			var lodBlending = settingsSystem.kLODBlending;

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
			for (int i = 0; i != solverData.Length; i++)
			{
				if (settingsStrands.staging)
				{
					var stagingCompression = (settingsStrands.stagingPrecision == SettingsStrands.StagingPrecision.Half);
					var stagingSubdivision = settingsStrands.stagingSubdivision;

					if (HairSim.PrepareSolverStaging(ref solverData[i], stagingCompression, stagingSubdivision))
					{
						HairSim.PushSolverStaging(cmd, ref solverData[i], stagingCompression, stagingSubdivision, volumeData);
						HairSim.PushSolverStaging(cmd, ref solverData[i], stagingCompression, stagingSubdivision, volumeData);
					}
					else
					{
						HairSim.PushSolverStaging(cmd, ref solverData[i], stagingCompression, stagingSubdivision, volumeData);
					}
				}
				else
				{
					solverData[i].cbuffer._StagingVertexCount = 0;// forces re-init after enable (see HairSim.PrepareSolverStaging)
					solverData[i].cbuffer._StagingSubdivision = 0;// ...
				}
			}

			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				switch (settingsSystem.strandRenderer)
				{
					case SettingsSystem.StrandRenderer.BuiltinLines:
					case SettingsSystem.StrandRenderer.BuiltinStrips:
						{
							UpdateRendererStateBuiltin(ref strandGroupInstances[i], solverData[i]);
						}
						break;
				}
			}

			// fire event
			if (onRenderingStateChanged != null)
				onRenderingStateChanged(cmd);
		}

		void UpdateRendererStateBuiltin(ref GroupInstance strandGroupInstance, in HairSim.SolverData solverData)
		{
			ref var meshFilter = ref strandGroupInstance.sceneObjects.strandMeshFilter;
			ref var meshRenderer = ref strandGroupInstance.sceneObjects.strandMeshRenderer;

			ref var materialInstance = ref strandGroupInstance.sceneObjects.materialInstance;
			ref var meshInstanceLines = ref strandGroupInstance.sceneObjects.meshInstanceLines;
			ref var meshInstanceStrips = ref strandGroupInstance.sceneObjects.meshInstanceStrips;

			var subdivisionCount = solverData.cbuffer._StagingSubdivision;
			if (subdivisionCount != strandGroupInstance.sceneObjects.meshInstanceSubdivisionCount)
			{
				strandGroupInstance.sceneObjects.meshInstanceSubdivisionCount = subdivisionCount;

				CoreUtils.Destroy(meshInstanceLines);
				CoreUtils.Destroy(meshInstanceStrips);
			}

			switch (settingsSystem.strandRenderer)
			{
				case SettingsSystem.StrandRenderer.BuiltinLines:
					{
						if (subdivisionCount == 0)
						{
							HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceLines, strandGroupInstance.groupAssetReference.Resolve().meshAssetLines, HideFlags.HideAndDontSave);
						}
						else
						{
							HairInstanceBuilder.CreateMeshLinesIfNull(ref meshInstanceLines, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.cbuffer._StrandCount, (int)solverData.cbuffer._StagingVertexCount, new Bounds());
						}

						if (meshFilter.sharedMesh != meshInstanceLines)
							meshFilter.sharedMesh = meshInstanceLines;
					}
					break;

				case SettingsSystem.StrandRenderer.BuiltinStrips:
					{
						if (subdivisionCount == 0)
						{
							HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceStrips, strandGroupInstance.groupAssetReference.Resolve().meshAssetStrips, HideFlags.HideAndDontSave);
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
				meshRenderer.shadowCastingMode = settingsSystem.strandShadows;
				meshRenderer.renderingLayerMask = (uint)settingsSystem.strandLayers;
				meshRenderer.motionVectorGenerationMode = settingsSystem.motionVectors;

				HairSim.BindSolverData(materialInstance, solverData);
				HairSim.BindVolumeData(materialInstance, volumeData);

				materialInstance.SetTexture("_UntypedVolumeDensity", volumeData.volumeDensity);
				materialInstance.SetTexture("_UntypedVolumeVelocity", volumeData.volumeVelocity);
				materialInstance.SetTexture("_UntypedVolumeStrandCountProbe", volumeData.volumeStrandCountProbe);

				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_ID_LINES", settingsSystem.strandRenderer == SettingsSystem.StrandRenderer.BuiltinLines);
				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_ID_STRIPS", settingsSystem.strandRenderer == SettingsSystem.StrandRenderer.BuiltinStrips);
				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_SRC_SOLVER", !settingsStrands.staging);
				CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_SRC_STAGING", settingsStrands.staging);
			}
			else
			{
				meshRenderer.enabled = false;
			}
		}

		public static Bounds GetRootBounds(in GroupInstance strandGroupInstance, Matrix4x4? worldTransform = null)
		{
			var rootLocalBounds = strandGroupInstance.sceneObjects.rootMeshFilter.sharedMesh.bounds;
			var rootLocalToWorld = strandGroupInstance.sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
			{
				return rootLocalBounds.WithTransform((worldTransform != null) ? (worldTransform.Value * rootLocalToWorld) : rootLocalToWorld);
			}
		}

		public Quaternion GetRootRotation(in GroupInstance strandGroupInstance)
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			if (settingsSkinning.rootsAttach && settingsSkinning.rootsAttachTarget != null)
			{
				return settingsSkinning.rootsAttachTargetBone.skinningBone.rotation;
			}
#endif
			return strandGroupInstance.sceneObjects.rootMeshFilter.transform.rotation;
		}

		public bool GetSimulationActive()
		{
			return settingsSystem.simulation && (settingsSystem.simulationInEditor || Application.isPlaying);
		}

		public float GetSimulationTimeStep()
		{
			switch (settingsSystem.simulationRate)
			{
				case SettingsSystem.SimulationRate.Fixed30Hz: return 1.0f / 30.0f;
				case SettingsSystem.SimulationRate.Fixed60Hz: return 1.0f / 60.0f;
				case SettingsSystem.SimulationRate.Fixed120Hz: return 1.0f / 120.0f;
				case SettingsSystem.SimulationRate.CustomTimeStep: return settingsSystem.simulationTimeStep;
				default: return 0.0f;
			}
		}

		public Bounds GetSimulationBounds(bool worldSquare = true, Matrix4x4? worldToLocalTransform = null)
		{
			Debug.Assert(worldSquare == false || worldToLocalTransform == null);

			var strandScale = GetStrandScale();
			var rootBounds = GetRootBounds(strandGroupInstances[0], worldToLocalTransform);
			var rootMargin = strandGroupInstances[0].groupAssetReference.Resolve().maxStrandLength * strandScale;

			for (int i = 1; i != strandGroupInstances.Length; i++)
			{
				rootBounds.Encapsulate(GetRootBounds(strandGroupInstances[i], worldToLocalTransform));
				rootMargin = Mathf.Max(strandGroupInstances[i].groupAssetReference.Resolve().maxStrandLength * strandScale, rootMargin);
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

			if (mat == null && settingsStrands.material)
				mat = settingsStrands.materialValue;

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
				stepCount = Mathf.Max(stepCount, settingsSystem.stepsMin ? settingsSystem.stepsMinValue : stepCount);
				stepCount = Mathf.Min(stepCount, settingsSystem.stepsMax ? settingsSystem.stepsMaxValue : stepCount);
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
				var rootMesh = strandGroupInstances[i].sceneObjects.rootMeshFilter.sharedMesh;
				var rootTransform = strandGroupInstances[i].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
				var strandRotation = GetRootRotation(strandGroupInstances[i]);

				HairSim.PushSolverParams(cmd, ref solverData[i], settingsSolver, rootTransform, strandRotation, strandDiameter, strandScale, stepDT);
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
			}

			// update volume boundaries
			HairSim.PushVolumeBoundaries(cmd, ref volumeData, settingsVolume, simulationBounds);// TODO handle substeps within frame (interpolate colliders)

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, settingsVolume))
			{
				HairSim.PushVolumeParams(cmd, ref volumeData, settingsVolume, simulationBounds, strandDiameter + strandMargin, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, settingsVolume, solverData);
			}

			// perform the steps
			for (int i = 0; i != stepCount; i++)
			{
				float stepFracLo = (i + 0) / (float)stepCount;
				float stepFracHi = (i + 1) / (float)stepCount;

				// step solver data
				for (int j = 0; j != solverData.Length; j++)
				{
					HairSim.StepSolverData(cmd, ref solverData[j], settingsSolver, volumeData, stepFracLo, stepFracHi);
				}

				// step volume data
				HairSim.PushVolumeParams(cmd, ref volumeData, settingsVolume, simulationBounds, strandDiameter + strandMargin, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, settingsVolume, solverData);
			}
		}

		public void DispatchDraw(CommandBuffer cmd)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			// draw solver data
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.DrawSolverData(cmd, solverData[i], settingsDebug);
			}

			// draw volume data
			HairSim.DrawVolumeData(cmd, volumeData, settingsDebug);
		}

		bool InitializeRuntimeData(CommandBuffer cmd)
		{
			var status = CheckStrandGroupInstances();
			if (status != StrandGroupInstancesStatus.Valid)
				return false;

			if (strandGroupInstances == null || strandGroupInstances.Length == 0)
				return false;

			if (solverData != null && solverData.Length == strandGroupInstances.Length)
				return true;

			// prep volume data
			HairSim.PrepareVolumeData(ref volumeData, settingsVolume);

			volumeData.allGroupsMaxParticleInterval = 0.0f;
			{
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					volumeData.allGroupsMaxParticleInterval = Mathf.Max(volumeData.allGroupsMaxParticleInterval, strandGroupInstances[i].groupAssetReference.Resolve().maxParticleInterval);
				}
			}

			// init solver data
			solverData = new HairSim.SolverData[strandGroupInstances.Length];

			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				ref readonly var strandGroupAsset = ref strandGroupInstances[i].groupAssetReference.Resolve();

				HairSim.PrepareSolverData(ref solverData[i], strandGroupAsset.strandCount, strandGroupAsset.strandParticleCount, strandGroupAsset.lodCount);
				{
					solverData[i].memoryLayout = strandGroupAsset.particleMemoryLayout;
					solverData[i].cbuffer._StrandCount = (uint)strandGroupAsset.strandCount;
					solverData[i].cbuffer._StrandParticleCount = (uint)strandGroupAsset.strandParticleCount;
					solverData[i].cbuffer._StrandMaxParticleInterval = strandGroupAsset.maxParticleInterval;
					solverData[i].cbuffer._StrandMaxParticleWeight = strandGroupAsset.maxParticleInterval / volumeData.allGroupsMaxParticleInterval;
					solverData[i].lodGuideCountCPU = new NativeArray<int>(strandGroupAsset.lodGuideCount, Allocator.Persistent);
					solverData[i].lodThreshold = new NativeArray<float>(strandGroupAsset.lodThreshold, Allocator.Persistent);
				}

				int strandGroupParticleCount = strandGroupAsset.strandCount * strandGroupAsset.strandParticleCount;

				using (var alignedRootPosition = new NativeArray<Vector4>(strandGroupAsset.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var alignedRootDirection = new NativeArray<Vector4>(strandGroupAsset.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var alignedParticlePosition = new NativeArray<Vector4>(strandGroupParticleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						fixed (void* rootPositionPtr = strandGroupAsset.rootPosition)
						fixed (void* rootDirectionPtr = strandGroupAsset.rootDirection)
						fixed (void* particlePositionPtr = strandGroupAsset.particlePosition)
						{
							UnsafeUtility.MemCpyStride(alignedRootPosition.GetUnsafePtr(), sizeof(Vector4), rootPositionPtr, sizeof(Vector3), sizeof(Vector3), strandGroupAsset.strandCount);
							UnsafeUtility.MemCpyStride(alignedRootDirection.GetUnsafePtr(), sizeof(Vector4), rootDirectionPtr, sizeof(Vector3), sizeof(Vector3), strandGroupAsset.strandCount);
							UnsafeUtility.MemCpyStride(alignedParticlePosition.GetUnsafePtr(), sizeof(Vector4), particlePositionPtr, sizeof(Vector3), sizeof(Vector3), strandGroupParticleCount);
						}
					}

					solverData[i].rootUV.SetData(strandGroupAsset.rootUV);
					solverData[i].rootScale.SetData(strandGroupAsset.rootScale);
					solverData[i].rootPosition.SetData(alignedRootPosition);
					solverData[i].rootDirection.SetData(alignedRootDirection);

					solverData[i].particlePosition.SetData(alignedParticlePosition);

					solverData[i].lodGuideCount.SetData(strandGroupAsset.lodGuideCount);
					solverData[i].lodGuideIndex.SetData(strandGroupAsset.lodGuideIndex);
					solverData[i].lodGuideCarry.SetData(strandGroupAsset.lodGuideCarry);

					// NOTE: the remaining buffers are initialized in KInitialize and KInitializePostVolume
				}

				var rootMesh = strandGroupInstances[i].sceneObjects.rootMeshFilter.sharedMesh;
				var rootTransform = strandGroupInstances[i].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;

				var strandDiameter = GetStrandDiameter();
				var strandScale = GetStrandScale();
				var strandRotation = GetRootRotation(strandGroupInstances[i]);

				HairSim.PushSolverLOD(cmd, ref solverData[i], strandGroupAsset.lodCount - 1);//TODO will need to move this around to generate rest density per LOD, to support target density initial pose in particles
				HairSim.PushSolverParams(cmd, ref solverData[i], settingsSolver, rootTransform, strandRotation, strandDiameter, strandScale, 1.0f);
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

				HairSim.PushVolumeParams(cmd, ref volumeData, settingsVolume, simulationBounds, strandDiameter + strandMargin, strandScale);
				HairSim.StepVolumeData(cmd, ref volumeData, settingsVolume, solverData);

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
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					ref readonly var strandGroupInstance = ref strandGroupInstances[i];

					CoreUtils.Destroy(strandGroupInstance.sceneObjects.materialInstance);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceLines);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceStrips);
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
