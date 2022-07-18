#pragma warning disable 0414 // some fields are unused in case of disabled optional features

#define REMOVE_AFTER_CONTENT_UPGRADE

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if HAS_PACKAGE_UNITY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
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

			public ref readonly HairAsset.StrandGroup Resolve()
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
#if HAS_PACKAGE_UNITY_HDRP_15
				public HairRenderer strandMeshRendererHDRP;
#endif

				[NonSerialized] public Material materialInstance;
				[NonSerialized] public Mesh meshInstanceLines;
				[NonSerialized] public Mesh meshInstanceStrips;
				[NonSerialized] public uint meshInstanceSubdivisionCount;
			}

#if REMOVE_AFTER_CONTENT_UPGRADE
			[FormerlySerializedAs("container")] public GameObject OLD__container;
#endif

			public GroupAssetReference groupAssetReference;
			public SceneObjects sceneObjects;
			public int settingsIndex;
		}

		[Serializable]
		public struct GroupSettings
		{
			public List<GroupAssetReference> groupAssetReferences;

			public SettingsSkinning settingsSkinning;
			public bool settingsSkinningToggle;

			public SettingsStrands settingsStrands;
			public bool settingsStrandsToggle;

			public HairSim.SolverSettings settingsSolver;
			public bool settingsSolverToggle;

			public static GroupSettings defaults => new GroupSettings()
			{
				groupAssetReferences = new List<GroupAssetReference>(1),

				settingsSkinning = SettingsSkinning.defaults,
				settingsSkinningToggle = false,

				settingsStrands = SettingsStrands.defaults,
				settingsStrandsToggle = false,

				settingsSolver = HairSim.SolverSettings.defaults,
				settingsSolverToggle = false,
			};
		}

		[Serializable]
		public struct SettingsSystem
		{
			public enum BoundsMode
			{
				Automatic,
				Fixed,
			}

			public enum LODSelection
			{
				Automatic,//TODO
				Fixed,
			}

			public enum StrandRenderer
			{
				Disabled,
				BuiltinLines,
				BuiltinStrips,
				HDRPHairRenderer,
			}

			public enum SimulationRate
			{
				Fixed30Hz,
				Fixed60Hz,
				Fixed120Hz,
				CustomTimeStep,
			}

			[LineHeader("Bounds")]

			public BoundsMode boundsMode;
			[VisibleIf(nameof(boundsMode), BoundsMode.Fixed)]
			public Vector3 boundsCenter;
			[VisibleIf(nameof(boundsMode), BoundsMode.Fixed)]
			public Vector3 boundsExtent;
			[ToggleGroup]
			public bool boundsScale;
			[ToggleGroupItem, Range(0.0f, 2.0f)]
			public float boundsScaleValue;

			[LineHeader("LOD")]

			public LODSelection kLODSearch;
			[Range(0.0f, 1.0f)]
			public float kLODSearchValue;
			public bool kLODBlending;

			[LineHeader("Renderer")]

			public StrandRenderer strandRenderer;
#if HAS_PACKAGE_UNITY_HDRP_15
			//[VisibleIf(nameof(strandRenderer), StrandRenderer.HDRPHairRenderer)]
			//public HairRasterizationMode strandRendererMode;
			[ToggleGroup, VisibleIf(nameof(strandRenderer), StrandRenderer.HDRPHairRenderer)]
			public bool strandRendererGrouping;
			[ToggleGroupItem]
			public HairRendererGroup strandRendererGroupingValue;
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
				boundsMode = BoundsMode.Automatic,
				boundsCenter = new Vector3(0.0f, 0.0f, 0.0f),
				boundsExtent = new Vector3(1.0f, 1.0f, 1.0f),
				boundsScale = false,
				boundsScaleValue = 1.25f,

				kLODSearch = LODSelection.Fixed,
				kLODSearchValue = 1.0f,
				kLODBlending = false,

				strandRenderer = StrandRenderer.BuiltinLines,
#if HAS_PACKAGE_UNITY_HDRP_15
				//strandRendererMode = HairRasterizationMode.Performance,
				strandRendererGrouping = false,
				strandRendererGroupingValue = HairRendererGroup.Group0,
#endif
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
			public PrimarySkinningBone rootsAttachTargetBone;
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

			[ToggleGroup, FormerlySerializedAs("strandMaterial")]
			public bool material;
			[ToggleGroupItem, FormerlySerializedAs("strandMaterialValue")]
			public Material materialValue;

			[LineHeader("Proportions")]

			public StrandScale strandScale;
			[Range(0.01f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
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
		[SerializeField, HideInInspector, FormerlySerializedAs("hairAsset")] private HairAsset OLD__hairAsset;
		[SerializeField, HideInInspector, FormerlySerializedAs("hairAssetQuickEdit")] private bool OLD__hairAssetQuickEdit;
		[SerializeField, HideInInspector, FormerlySerializedAs("settingsRoots")] public SettingsSkinning OLD__settingsRoots = SettingsSkinning.defaults;
		[SerializeField, HideInInspector, FormerlySerializedAs("settingsStrands")] public SettingsStrands OLD__settingsStrands = SettingsStrands.defaults;
		[SerializeField, HideInInspector, FormerlySerializedAs("solverSettings")] public HairSim.SolverSettings OLD__settingsSolver = HairSim.SolverSettings.defaults;
#endif

		public string[] strandGroupChecksums;// checksums of active providers for instantiated groups

		public GroupProvider[] strandGroupProviders = new GroupProvider[1];
		public GroupInstance[] strandGroupInstances;
		public GroupSettings[] strandGroupSettings;
		public GroupSettings strandGroupDefaults = GroupSettings.defaults;

		public SettingsSystem settingsSystem = SettingsSystem.defaults;					// per instance
		[FormerlySerializedAs("volumeSettings")]
		public HairSim.VolumeSettings settingsVolume = HairSim.VolumeSettings.defaults;	// per instance
		[FormerlySerializedAs("debugSettings")]
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
			if (solverData == null)
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

		private HashSet<SkinAttachmentTarget> preqGPUAttachmentTargets = new HashSet<SkinAttachmentTarget>();
		private Hash128 preqGPUAttachmentTargetsHash = new Hash128();
		private int preqCountdown = 1;

		void UpdatePrerequisite()
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_2
			var hash = new Hash128();
			{
				if (strandGroupInstances != null)
				{
					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						ref readonly var settingsSkinning = ref GetSettingsSkinning(strandGroupInstances[i]);
						if (settingsSkinning.rootsAttach)
						{
							var attachmentTarget = settingsSkinning.rootsAttachTarget;
							if (attachmentTarget != null && attachmentTarget.isActiveAndEnabled && attachmentTarget.executeOnGPU)
							{
								hash.Append(attachmentTarget.GetInstanceID());
							}
						}
					}
				}
			}

			if (hash != preqGPUAttachmentTargetsHash)
			{
				foreach (var preq in preqGPUAttachmentTargets)
				{
					preq.afterGPUAttachmentWorkCommitted -= HandlePrerequisite;
				}

				preqGPUAttachmentTargets.Clear();
				preqGPUAttachmentTargetsHash = hash;

				if (strandGroupInstances != null)
				{
					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						ref readonly var settingsSkinning = ref GetSettingsSkinning(strandGroupInstances[i]);
						if (settingsSkinning.rootsAttach)
						{
							var attachmentTarget = settingsSkinning.rootsAttachTarget;
							if (attachmentTarget != null && attachmentTarget.isActiveAndEnabled && attachmentTarget.executeOnGPU)
							{
								preqGPUAttachmentTargets.Add(attachmentTarget);
							}
						}
					}
				}

				foreach (var preq in preqGPUAttachmentTargets)
				{
					preq.afterGPUAttachmentWorkCommitted += HandlePrerequisite;
				}
			}
#else
			preqGPUAttachmentTargets.Clear();
			preqGPUAttachmentTargetsHash = new Hash128();
#endif

			preqCountdown = preqGPUAttachmentTargets.Count + 1;
		}

		void HandlePrerequisite()
		{
			if (--preqCountdown == 0)
			{
				LateUpdateInternal();
			}
		}

		void Update()
		{
			UpdateStrandGroupInstances();
			UpdateStrandGroupSettings();
			UpdateAttachedState();
			UpdatePrerequisite();
		}

		void LateUpdate()
		{
			HandlePrerequisite();
		}

		void LateUpdateInternal()
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
				if (hairAsset == null || hairAsset.checksum == "")
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
			if (OLD__hairAsset != null)
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

							var serializedObject = new UnityEditor.SerializedObject(this);
							{
								var property_strandGroupProviders = serializedObject.FindProperty(nameof(strandGroupChecksums));
								var property_strandGroupChecksums = serializedObject.FindProperty(nameof(strandGroupProviders));
								var property_strandGroupDefaults = serializedObject.FindProperty(nameof(strandGroupDefaults));
								var property_strandGroupSettings = serializedObject.FindProperty(nameof(strandGroupSettings));

								UnityEditor.PrefabUtility.RevertPropertyOverride(property_strandGroupProviders, UnityEditor.InteractionMode.AutomatedAction);
								UnityEditor.PrefabUtility.RevertPropertyOverride(property_strandGroupChecksums, UnityEditor.InteractionMode.AutomatedAction);
								UnityEditor.PrefabUtility.RevertPropertyOverride(property_strandGroupDefaults.FindPropertyRelative(nameof(GroupSettings.settingsSkinning)), UnityEditor.InteractionMode.AutomatedAction);

								for (int i = 0; i != property_strandGroupSettings.arraySize; i++)
								{
									UnityEditor.PrefabUtility.RevertPropertyOverride(property_strandGroupSettings.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(GroupSettings.settingsSkinning)), UnityEditor.InteractionMode.AutomatedAction);
								}
							}

							serializedObject.ApplyModifiedProperties();
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
			if (OLD__hairAsset != null)
			{
				settingsSystem.kLODSearchValue = OLD__settingsStrands.kLODSearchValue;
				settingsSystem.kLODBlending = OLD__settingsStrands.kLODBlending;
				settingsSystem.strandRenderer = OLD__settingsStrands.strandRenderer;
				settingsSystem.strandShadows = OLD__settingsStrands.strandShadows;
				settingsSystem.strandLayers = OLD__settingsStrands.strandLayers;
				settingsSystem.motionVectors = OLD__settingsStrands.motionVectors;
				settingsSystem.simulation = OLD__settingsStrands.simulation;
				settingsSystem.simulationRate = OLD__settingsStrands.simulationRate;
				settingsSystem.simulationInEditor = OLD__settingsStrands.simulationInEditor;
				settingsSystem.simulationTimeStep = OLD__settingsStrands.simulationTimeStep;
				settingsSystem.stepsMin = OLD__settingsStrands.stepsMin;
				settingsSystem.stepsMinValue = OLD__settingsStrands.stepsMinValue;
				settingsSystem.stepsMax = OLD__settingsStrands.stepsMax;
				settingsSystem.stepsMaxValue = OLD__settingsStrands.stepsMaxValue;

				CoreUtils.Destroy(strandGroupInstances?[0].OLD__container);

				strandGroupProviders = new GroupProvider[1];
				strandGroupProviders[0].hairAsset = OLD__hairAsset;
				strandGroupProviders[0].hairAssetQuickEdit = OLD__hairAssetQuickEdit;
				strandGroupInstances = null;
				strandGroupChecksums = null;
				strandGroupSettings = null;
				strandGroupDefaults.settingsSkinning = OLD__settingsRoots;
				strandGroupDefaults.settingsStrands = OLD__settingsStrands;
				strandGroupDefaults.settingsSolver = OLD__settingsSolver;

				OLD__hairAsset = null;
				OLD__hairAssetQuickEdit = false;
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
			if (strandGroupInstances == null)
				return;

			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				strandGroupInstances[i].settingsIndex = -1;
			}

			if (strandGroupSettings == null)
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
#else// alt. path without multihashmap
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

			using (var attachmentsChangedMask = new UnsafeBitArray(1 + (strandGroupSettings?.Length ?? 0), Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					ref readonly var strandGroupInstance = ref strandGroupInstances[i];
					ref readonly var settingsSkinning = ref GetSettingsSkinning(strandGroupInstance);

					var attachment = strandGroupInstance.sceneObjects.rootMeshAttachment;
					if (attachment == null)
					{
						var container = strandGroupInstance.sceneObjects.rootMeshContainer;
						if (container != null && container.TryGetComponent(out attachment) == false)
						{
							attachment = strandGroupInstances[i].sceneObjects.rootMeshAttachment = HairInstanceBuilder.CreateComponent<SkinAttachment>(container, container.hideFlags);
							attachment.attachmentType = SkinAttachment.AttachmentType.Mesh;
							attachment.forceRecalculateBounds = true;
						}
					}

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

						// default settings (settingsIndex -1) sets bit 0, and so forth
						attachmentsChangedMask.Set(strandGroupInstance.settingsIndex + 1, true);
					}
				}

				void CommitAttachments(ref SettingsSkinning settingsSkinning)
				{
					if (settingsSkinning.rootsAttachTarget != null)
					{
						settingsSkinning.rootsAttachTarget.CommitSubjectsIfRequired();
						settingsSkinning.rootsAttachTargetBone = new PrimarySkinningBone(settingsSkinning.rootsAttachTarget.transform);
#if UNITY_EDITOR
						UnityEditor.EditorUtility.SetDirty(settingsSkinning.rootsAttachTarget);
#endif
					}
				}

				for (int i = 0; i != attachmentsChangedMask.Length; i++)
				{
					if (attachmentsChangedMask.IsSet(i))
					{
						if (i == 0)
							CommitAttachments(ref strandGroupDefaults.settingsSkinning);
						else
							CommitAttachments(ref strandGroupSettings[i - 1].settingsSkinning);
					}
				}
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
				ref readonly var settingsStrands = ref GetSettingsStrands(strandGroupInstances[i]);

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
					solverData[i].cbuffer._StagingSubdivision = 0;// forces re-init after enable (see HairSim.PrepareSolverStaging)
					solverData[i].cbuffer._StagingVertexCount = 0;// ...
				}
			}

			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				UpdateRendererState(ref strandGroupInstances[i], solverData[i]);
			}

			// fire event
			if (onRenderingStateChanged != null)
				onRenderingStateChanged(cmd);
		}

		void UpdateRendererState(ref GroupInstance strandGroupInstance, in HairSim.SolverData solverData)
		{
			ref readonly var settingsStrands = ref GetSettingsStrands(strandGroupInstance);

			// update material instance
			ref var materialInstance = ref strandGroupInstance.sceneObjects.materialInstance;
			{
				var materialAsset = GetStrandMaterial(strandGroupInstance);
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
					HairSim.BindSolverData(materialInstance, solverData);
					HairSim.BindVolumeData(materialInstance, volumeData);

					materialInstance.SetTexture("_UntypedVolumeDensity", volumeData.volumeDensity);
					materialInstance.SetTexture("_UntypedVolumeVelocity", volumeData.volumeVelocity);
					materialInstance.SetTexture("_UntypedVolumeStrandCountProbe", volumeData.volumeStrandCountProbe);

					CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_ID_LINES", settingsSystem.strandRenderer == SettingsSystem.StrandRenderer.BuiltinLines || settingsSystem.strandRenderer == SettingsSystem.StrandRenderer.HDRPHairRenderer);
					CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_ID_STRIPS", settingsSystem.strandRenderer == SettingsSystem.StrandRenderer.BuiltinStrips);

					CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_SRC_SOLVER", !settingsStrands.staging);
					CoreUtils.SetKeyword(materialInstance, "HAIR_VERTEX_SRC_STAGING", settingsStrands.staging);
				}
			}

			// update mesh instance
			var meshInstance = null as Mesh;
			{
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
					case SettingsSystem.StrandRenderer.Disabled:
						{
							meshInstance = null;
						}
						break;

					case SettingsSystem.StrandRenderer.HDRPHairRenderer:
					case SettingsSystem.StrandRenderer.BuiltinLines:
						{
							if (subdivisionCount == 0)
								HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceLines, strandGroupInstance.groupAssetReference.Resolve().meshAssetLines, HideFlags.HideAndDontSave);
							else
								HairInstanceBuilder.CreateMeshLinesIfNull(ref meshInstanceLines, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.cbuffer._StrandCount, (int)solverData.cbuffer._StagingVertexCount, new Bounds());

							meshInstance = meshInstanceLines;
						}
						break;

					case SettingsSystem.StrandRenderer.BuiltinStrips:
						{
							if (subdivisionCount == 0)
								HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceStrips, strandGroupInstance.groupAssetReference.Resolve().meshAssetStrips, HideFlags.HideAndDontSave);
							else
								HairInstanceBuilder.CreateMeshStripsIfNull(ref meshInstanceStrips, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.cbuffer._StrandCount, (int)solverData.cbuffer._StagingVertexCount, new Bounds());

							meshInstance = meshInstanceStrips;
						}
						break;
				}
			}

			// update mesh filter
			ref var meshFilter = ref strandGroupInstance.sceneObjects.strandMeshFilter;
			{
				if (meshFilter.sharedMesh != meshInstance)
					meshFilter.sharedMesh = meshInstance;

				//TODO better renderer bounds
				//meshFilter.sharedMesh.bounds = GetSimulationBounds(worldSquare: false, worldToLocalTransform: meshFilter.transform.worldToLocalMatrix);
				if (meshFilter.sharedMesh != null)
					meshFilter.sharedMesh.bounds = GetSimulationBounds().WithTransform(meshFilter.transform.worldToLocalMatrix);
			}

			// update mesh renderer
			ref var meshRenderer = ref strandGroupInstance.sceneObjects.strandMeshRenderer;
#if HAS_PACKAGE_UNITY_HDRP_15
			ref var meshRendererHDRP = ref strandGroupInstance.sceneObjects.strandMeshRendererHDRP;
			{
				if (meshRendererHDRP == null)
				{
					var container = strandGroupInstance.sceneObjects.strandMeshContainer;
					if (container != null && container.TryGetComponent(out meshRendererHDRP) == false)
					{
						meshRendererHDRP = strandGroupInstance.sceneObjects.strandMeshRendererHDRP = HairInstanceBuilder.CreateComponent<HairRenderer>(container, container.hideFlags);
					}
				}
			}
#endif
			{
				var meshRendererEnabled = false;
#if HAS_PACKAGE_UNITY_HDRP_15
				var meshRendererHDRPEnabled = false;
#endif

				switch (settingsSystem.strandRenderer)
				{
#if !HAS_PACKAGE_UNITY_HDRP_15
					case SettingsSystem.StrandRenderer.HDRPHairRenderer:
#endif
					case SettingsSystem.StrandRenderer.BuiltinLines:
					case SettingsSystem.StrandRenderer.BuiltinStrips:
						{
							meshRenderer.enabled = meshRendererEnabled = true;
							meshRenderer.sharedMaterial = materialInstance;
							meshRenderer.shadowCastingMode = settingsSystem.strandShadows;
							meshRenderer.renderingLayerMask = (uint)settingsSystem.strandLayers;
							meshRenderer.motionVectorGenerationMode = settingsSystem.motionVectors;
						}
						break;

#if HAS_PACKAGE_UNITY_HDRP_15
					case SettingsSystem.StrandRenderer.HDRPHairRenderer:
						{
							meshRendererHDRP.enabled = meshRendererHDRPEnabled = true;
							meshRendererHDRP.mesh = meshInstance;
							meshRendererHDRP.material = materialInstance;
							//meshRendererHDRP.rasterMode = settingsSystem.strandRendererMode;
							meshRendererHDRP.shadowCastingMode = settingsSystem.strandShadows;
							meshRendererHDRP.renderingLayerMask = (uint)settingsSystem.strandLayers;
							meshRendererHDRP.motionVectorMode = settingsSystem.motionVectors;
							meshRendererHDRP.groupMerging = settingsSystem.strandRendererGrouping;
							meshRendererHDRP.rendererGroup = settingsSystem.strandRendererGroupingValue;
						}
						break;
#endif
				}

				if (meshRendererEnabled == false)
					meshRenderer.enabled = false;

#if HAS_PACKAGE_UNITY_HDRP_15
				if (meshRendererHDRPEnabled == false)
				{
					meshRendererHDRP.enabled = false;
					meshRendererHDRP.shadowCastingMode = ShadowCastingMode.Off;
				}
#endif
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
			ref readonly var settingsSkinning = ref GetSettingsSkinning(strandGroupInstance);
			{
				if (settingsSkinning.rootsAttach && settingsSkinning.rootsAttachTarget != null)
				{
					return settingsSkinning.rootsAttachTargetBone.skinningBone.rotation;
				}
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

			var boundsScale = settingsSystem.boundsScale ? settingsSystem.boundsScaleValue : 1.25f;
			var bounds = new Bounds();
			{
				bounds.center = Vector3.zero;
				bounds.extents = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
			}

			switch (settingsSystem.boundsMode)
			{
				case SettingsSystem.BoundsMode.Automatic:
					{
						for (int i = 0; i != strandGroupInstances.Length; i++)
						{
							var rootMargin = GetStrandScale(strandGroupInstances[i]) * strandGroupInstances[i].groupAssetReference.Resolve().maxStrandLength;
							var rootBounds = GetRootBounds(strandGroupInstances[i], worldToLocalTransform);
							{
								rootBounds.Expand(2.0f * rootMargin * boundsScale);
							}

							bounds.Encapsulate(rootBounds);
						}
					}
					break;

				case SettingsSystem.BoundsMode.Fixed:
					{
						bounds.center = settingsSystem.boundsCenter + this.transform.position;
						bounds.extents = settingsSystem.boundsExtent * boundsScale;
					}
					break;
			}

			if (worldSquare)
				return new Bounds(bounds.center, bounds.size.CMax() * Vector3.one);
			else
				return bounds;
		}

		public ref readonly SettingsSkinning GetSettingsSkinning(in GroupInstance strandGroupInstance)
		{
			var i = strandGroupInstance.settingsIndex;
			if (i != -1 && strandGroupSettings[i].settingsSkinningToggle)
				return ref strandGroupSettings[i].settingsSkinning;
			else
				return ref strandGroupDefaults.settingsSkinning;
		}

		public ref readonly SettingsStrands GetSettingsStrands(in GroupInstance strandGroupInstance)
		{
			var i = strandGroupInstance.settingsIndex;
			if (i != -1 && strandGroupSettings[i].settingsStrandsToggle)
				return ref strandGroupSettings[i].settingsStrands;
			else
				return ref strandGroupDefaults.settingsStrands;
		}

		public ref readonly HairSim.SolverSettings GetSettingsSolver(in GroupInstance strandGroupInstance)
		{
			var i = strandGroupInstance.settingsIndex;
			if (i != -1 && strandGroupSettings[i].settingsSolverToggle)
				return ref strandGroupSettings[i].settingsSolver;
			else
				return ref strandGroupDefaults.settingsSolver;
		}

		public Material GetStrandMaterial(in GroupInstance strandGroupInstance)
		{
			ref readonly var settingsStrands = ref GetSettingsStrands(strandGroupInstance);

			var mat = null as Material;

			if (mat == null && settingsStrands.material)
				mat = settingsStrands.materialValue;

			if (mat == null)
				mat = HairMaterialUtility.GetCurrentPipelineDefault();

			return mat;
		}

		public float GetStrandDiameter(in GroupInstance strandGroupInstance)
		{
			return GetSettingsStrands(strandGroupInstance).strandDiameter;
		}

		public float GetStrandMargin(in GroupInstance strandGroupInstance)
		{
			return GetSettingsStrands(strandGroupInstance).strandMargin;
		}

		public float GetStrandScale(in GroupInstance strandGroupInstance)
		{
			switch (GetSettingsStrands(strandGroupInstance).strandScale)
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

			// update solver roots
			for (int i = 0; i != solverData.Length; i++)
			{
				var rootMesh = strandGroupInstances[i].sceneObjects.rootMeshFilter.sharedMesh;
				var rootTransform = strandGroupInstances[i].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;

				var strandRotation = GetRootRotation(strandGroupInstances[i]);
				var strandDiameter = GetStrandDiameter(strandGroupInstances[i]);
				var strandMargin = GetStrandMargin(strandGroupInstances[i]);
				var strandScale = GetStrandScale(strandGroupInstances[i]);

				HairSim.PushSolverParams(cmd, ref solverData[i], GetSettingsSolver(strandGroupInstances[i]), rootTransform, strandRotation, strandDiameter, strandMargin, strandScale, stepDT);
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
			}

			// update volume boundaries
			HairSim.PushVolumeBoundaries(cmd, ref volumeData, settingsVolume, simulationBounds);//TODO handle substeps within frame (interpolate colliders)

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, settingsVolume))
			{
				HairSim.PushVolumeParams(cmd, ref volumeData, settingsVolume, solverData, simulationBounds);
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
					HairSim.StepSolverData(cmd, ref solverData[j], GetSettingsSolver(strandGroupInstances[j]), volumeData, stepFracLo, stepFracHi);
				}

				// step volume data
				HairSim.PushVolumeParams(cmd, ref volumeData, settingsVolume, solverData, simulationBounds);
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

					switch (solverData[i].memoryLayout)
					{
						case HairAsset.MemoryLayout.Interleaved:
							solverData[i].cbuffer._StrandParticleOffset = 1;
							solverData[i].cbuffer._StrandParticleStride = solverData[i].cbuffer._StrandCount;
							break;

						case HairAsset.MemoryLayout.Sequential:
							solverData[i].cbuffer._StrandParticleOffset = solverData[i].cbuffer._StrandParticleCount;
							solverData[i].cbuffer._StrandParticleStride = 1;
							break;
					}

					solverData[i].initialMaxParticleInterval = strandGroupAsset.maxParticleInterval;
					solverData[i].initialMaxParticleDiameter = 0.0f;//TODO
					solverData[i].initialTotalLength = strandGroupAsset.totalLength;
					solverData[i].lodGuideCountCPU = new NativeArray<int>(strandGroupAsset.lodGuideCount, Allocator.Persistent);
					solverData[i].lodThreshold = new NativeArray<float>(strandGroupAsset.lodThreshold, Allocator.Persistent);
				}

				int strandGroupParticleCount = strandGroupAsset.strandCount * strandGroupAsset.strandParticleCount;

				//DEBUG BEGIN
				/*
				int debugIndex = 315;// squiggly strand
				debugIndex = 0;
				if (debugIndex < strandGroupAsset.strandCount)
				{
					HairAssetUtility.DeclareStrandIterator(strandGroupAsset.particleMemoryLayout, debugIndex, strandGroupAsset.strandCount, strandGroupAsset.strandParticleCount, out var debugBegin, out var debugStride, out var debugEnd);

					var fmt = "R";
					var cul = System.Globalization.CultureInfo.InvariantCulture;

					var tmp = "float[] x = new float[] { ";
					for (int particleIndex = debugBegin; particleIndex != debugEnd; particleIndex += debugStride)
					{
						tmp += (
							(particleIndex != debugBegin ? ", " : "") +
							strandGroupAsset.particlePosition[particleIndex].x.ToString(fmt, cul) + "f, " +
							strandGroupAsset.particlePosition[particleIndex].y.ToString(fmt, cul) + "f, " +
							strandGroupAsset.particlePosition[particleIndex].z.ToString(fmt, cul) + "f"
						);
					}
					tmp += " };\nfloat stepLength = " + (strandGroupAsset.maxParticleInterval * strandGroupAsset.rootScale[debugIndex]).ToString(fmt, cul) + "f;";
					Debug.Log(tmp);
				}
				*/
				//DEBUG END

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

				var strandRotation = GetRootRotation(strandGroupInstances[i]);
				var strandDiameter = GetStrandDiameter(strandGroupInstances[i]);
				var strandMargin = GetStrandMargin(strandGroupInstances[i]);
				var strandScale = GetStrandScale(strandGroupInstances[i]);

				HairSim.PushSolverLOD(cmd, ref solverData[i], strandGroupAsset.lodCount - 1);//TODO will need to move this around to generate rest density per LOD, to support target density initial pose in particles
				HairSim.PushSolverParams(cmd, ref solverData[i], GetSettingsSolver(strandGroupInstances[i]), rootTransform, strandRotation, strandDiameter, strandMargin, strandScale, 1.0f);
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
				{
					HairSim.InitSolverData(cmd, solverData[i]);
				}

				//TODO clean this up (currently necessary for full initialization of root buffers)
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
				HairSim.PushSolverRoots(cmd, ref solverData[i], rootMesh);
			}

			// init volume data
			HairSim.PrepareVolumeData(ref volumeData, settingsVolume);
			{
				var simulationBounds = GetSimulationBounds();

				HairSim.PushVolumeParams(cmd, ref volumeData, settingsVolume, solverData, simulationBounds);
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
