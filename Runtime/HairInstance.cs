#pragma warning disable 0414 // some fields are unused in case of disabled optional features

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if UNITY_EDITOR 
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

#if HAS_PACKAGE_UNITY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
#endif

namespace Unity.DemoTeam.Hair
{
	[ExecuteAlways, SelectionBase]
	public partial class HairInstance : MonoBehaviour
	{
		public static HashSet<HairInstance> s_instances = new HashSet<HairInstance>();

		// Hook into RenderPipelineManager.beginContextRendering so we can have the option of handling prereqs late in the frame. 
#if UNITY_EDITOR
		[InitializeOnLoadMethod]
		static void InitializeOnLoad() => HookCallback();
#else
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void InitializeOnLoad() => HookCallback();
#endif
		static void HookCallback()
		{
			RenderPipelineManager.beginContextRendering -= RenderPipelineManagerOnBeginContextRendering;
			RenderPipelineManager.beginContextRendering += RenderPipelineManagerOnBeginContextRendering;
		}

		static void RenderPipelineManagerOnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
		{
			foreach (var instance in s_instances)
			{
				if (instance != null && instance.settingsExecutive.updateMode == SettingsExecutive.UpdateMode.BuiltinLateEvent)
				{
					instance.HandlePrerequisite();
				}
			}
		}

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
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_2_PREVIEW
				public SkinAttachmentMesh rootMeshAttachment;
#else
				public SkinAttachment rootMeshAttachment;
#endif
#endif

				public GameObject strandMeshContainer;
				public MeshFilter strandMeshFilter;
				public MeshRenderer strandMeshRenderer;
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
				public HDAdditionalMeshRendererSettings strandMeshRendererHDRP;
#endif

				[NonSerialized] public Material materialInstance;
				[NonSerialized] public Material materialInstanceShadows;

#if !UNITY_2021_2_OR_NEWER
				[NonSerialized] public Mesh meshInstance;
				[NonSerialized] public Mesh meshInstanceShadows;

				[NonSerialized] public ulong meshInstanceKey;
				[NonSerialized] public ulong meshInstanceKeyShadows;
#endif
			}

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

			public HairSim.SettingsGeometry settingsGeometry;
			public bool settingsGeometryToggle;

			public HairSim.SettingsRendering settingsRendering;
			public bool settingsRenderingToggle;

			public HairSim.SettingsPhysics settingsPhysics;
			public bool settingsPhysicsToggle;

			public static GroupSettings defaults => new GroupSettings()
			{
				groupAssetReferences = new List<GroupAssetReference>(1),

				settingsSkinning = SettingsSkinning.defaults,
				settingsSkinningToggle = false,

				settingsGeometry = HairSim.SettingsGeometry.defaults,
				settingsGeometryToggle = false,

				settingsRendering = HairSim.SettingsRendering.defaults,
				settingsRenderingToggle = false,

				settingsPhysics = HairSim.SettingsPhysics.defaults,
				settingsPhysicsToggle = false,
			};
		}

		public struct ExecutiveStep
		{
			public int countRaw;
			public int count;
			public float dt;
			public float hi;

			public static readonly ExecutiveStep defaults = new ExecutiveStep()
			{
				countRaw = 0,
				count = 0,
				dt = 0.0f,
				hi = 1.0f,
			};
		}

		public struct ExecutiveState
		{
			public float accumulatedTime;

			public double elapsedTimeRaw;
			public double elapsedTime;

			//public NativeQueue<TimeStepDesc> stepHistory;

			public int lastStepCount;
			public int lastStepCountRaw;
			public float lastStepCountSmooth;

			public bool pausePostStep;

			public static readonly ExecutiveState defaults = new ExecutiveState()
			{
				accumulatedTime = 0.0f,

				elapsedTime = 0.0f,
				elapsedTimeRaw = 0.0f,

				lastStepCount = 0,
				lastStepCountRaw = 0,
				lastStepCountSmooth = 0,

				pausePostStep = false,
			};
		}

		public string[] strandGroupChecksums;// checksums of active providers for instantiated groups

		public GroupProvider[] strandGroupProviders = new GroupProvider[1];
		public GroupInstance[] strandGroupInstances;
		public GroupSettings[] strandGroupSettings;
		public GroupSettings strandGroupDefaults = GroupSettings.defaults;

		[NonSerialized] public HairSim.SolverData[] solverData;
		[NonSerialized] public HairSim.VolumeData volumeData;
		[NonSerialized] public ExecutiveState execState = ExecutiveState.defaults;

		public SettingsExecutive settingsExecutive = SettingsExecutive.defaults;
		public HairSim.SettingsDebugging settingsDebugging = HairSim.SettingsDebugging.defaults;
		public HairSim.SettingsEnvironment settingsEnvironment = HairSim.SettingsEnvironment.defaults;
		public HairSim.SettingsVolume settingsVolumetrics = HairSim.SettingsVolume.defaults;

		public event Action<CommandBuffer> onSimulationStateChanged;
		public event Action<CommandBuffer> onRenderingStateChanged;

		void Reset()
		{
			version = VERSION;
		}

		void OnValidate()
		{
			if (version < 0)
				version = 0;

			VersionedDataUtility.HandleVersionChangeOnValidate(this);

			settingsVolumetrics.gridResolution = (uint)(Mathf.Max(8, (int)settingsVolumetrics.gridResolution) / 8) * 8;
		}

		void OnEnable()
		{
			VersionedDataUtility.HandleVersionChange(this);

			UpdateStrandGroupInstances();
			UpdateStrandGroupHideFlags();
			UpdateStrandGroupSettings();

			s_instances.Add(this);
		}

		void OnDisable()
		{
			ReleaseRuntimeData();
			ReleasePrerequisite();

			s_instances.Remove(this);
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
			if (settingsExecutive.updateMode == SettingsExecutive.UpdateMode.BuiltinEvent)
			{
				HandlePrerequisite();
			}
		}

		#region Update Group Instances
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
			if (status == StrandGroupInstancesStatus.Valid)
				return;

#if UNITY_EDITOR
			// if this is part of a prefab instance, then we need to handle content changes in underlying prefab
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
			if (isPrefabInstance)
			{
				// ensure that all content-related properties are inherited from underlying prefab (these are also marked "prefab" and not-editable in UI)
				{
					var serializedObject = new UnityEditor.SerializedObject(this);

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

				// handle content changes in underlying prefab
				Debug.Log(string.Format("{0} ({1}): rebuilding governing prefab...", this.GetType().Name, this.name), this);

				PrefabContentsUtility.UpdateUnderlyingPrefabContents(this, verbose: false, (GameObject prefabContentsRoot) =>
				{
					foreach (var prefabHairInstance in prefabContentsRoot.GetComponentsInChildren<HairInstance>(includeInactive: true))
					{
						prefabHairInstance.UpdateStrandGroupInstances();
					}
				});
			}
			else
#endif
			{
				// handle content changes
				switch (status)
				{
					case StrandGroupInstancesStatus.RequireRebuild:
						HairInstanceBuilder.BuildHairInstance(this, strandGroupProviders, HideFlags.NotEditable);
						break;

					case StrandGroupInstancesStatus.RequireRelease:
						HairInstanceBuilder.ClearHairInstance(this);
						break;
				}
			}

			// release runtime data
			ReleaseRuntimeData();
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
		#endregion

		#region Update Group Settings
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

#if HAS_PACKAGE_UNITY_COLLECTIONS_1_3_0
				using (var groupAssetKeys = new UnsafeParallelHashSet<ulong>(groupAssetKeyCapacity, Allocator.Temp))
#else
				using (var groupAssetKeys = new UnsafeHashSet<ulong>(groupAssetKeyCapacity, Allocator.Temp))
#endif
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
#if HAS_PACKAGE_UNITY_COLLECTIONS_1_3_0 && !HAS_PACKAGE_UNITY_COLLECTIONS_2_0_0_UNTIL_2_1_0_EXP_4
			using (var groupAssetInstancesMap = new UnsafeParallelMultiHashMap<ulong, int>(strandGroupInstances.Length, Allocator.Temp))
#else
			using (var groupAssetInstancesMap = new UnsafeMultiHashMap<ulong, int>(strandGroupInstances.Length, Allocator.Temp))
#endif
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
		#endregion

		#region Update Attached State
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

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_2_PREVIEW
			{
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					ref var strandGroupInstance = ref strandGroupInstances[i];
					ref readonly var settingsSkinning = ref GetSettingsSkinning(strandGroupInstance);
					var container = strandGroupInstance.sceneObjects.rootMeshContainer;

					//remove old SkinAttachment
					{
						if (container != null && container.TryGetComponent(out LegacySkinAttachment oldAttachment))
						{
							CoreUtils.Destroy(oldAttachment);
						}
					}
					var attachment = strandGroupInstance.sceneObjects.rootMeshAttachment;
					if (attachment == null)
					{
						if (container != null && container.TryGetComponent(out attachment) == false)
						{
							attachment = strandGroupInstances[i].sceneObjects.rootMeshAttachment = HairInstanceBuilder.CreateComponent<SkinAttachmentMesh>(container, container.hideFlags);
							attachment.attachmentType = SkinAttachmentMesh.MeshAttachmentType.Mesh;
							attachment.common.bakedDataEntryName = this.name + "/" + attachment.name;
						}
					}

					if (attachment != null)
					{

						bool buildTargetChanged = attachment.ExplicitTargetBakeMesh !=
						                          settingsSkinning.explicitRootsAttachMesh;



						attachment.ExplicitTargetBakeMesh = settingsSkinning.explicitRootsAttachMesh;

						if (attachment != null && (attachment.Target != settingsSkinning.rootsAttachTarget || (attachment.IsAttached != settingsSkinning.rootsAttach && settingsSkinning.rootsAttachTarget != null)))
						{
							SkinAttachmentDataRegistry dataStorage = GetAttachmentDataStorage(settingsSkinning, ref strandGroupInstance);
							attachment.DataStorage = dataStorage;
							bool dataStorageNeedsRefresh = attachment.common.HasDataStorageChanged() ||
							                               attachment.DataStorage == null ||
							                               !attachment.ValidateBakedData();
							bool attachmentStateChanged = attachment.IsAttached != settingsSkinning.rootsAttach;
							
							if (dataStorageNeedsRefresh || buildTargetChanged || attachmentStateChanged)
							{
								attachment.Target = settingsSkinning.rootsAttachTarget;

								if (attachment.Target != null && settingsSkinning.rootsAttach)
								{
									attachment.Detach(revertPositionRotation: false);
									attachment.Attach(storePositionRotation: false);

									if (strandGroupInstance.settingsIndex == -1)
										strandGroupDefaults.settingsSkinning.rootsAttachTargetBone = new PrimarySkinningBone(settingsSkinning.rootsAttachTarget.transform);
									else
										strandGroupSettings[strandGroupInstance.settingsIndex].settingsSkinning.rootsAttachTargetBone = new PrimarySkinningBone(settingsSkinning.rootsAttachTarget.transform);
								}
								else
								{
									attachment.Detach(revertPositionRotation: false);
								}
							}
#if UNITY_EDITOR
							UnityEditor.EditorUtility.SetDirty(attachment);
#endif
						}
					}
				}
			}
#else
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

				static void CommitAttachments(ref SettingsSkinning settingsSkinning)
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
#endif
		}

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_2_PREVIEW
		SkinAttachmentDataRegistry GetAttachmentDataStorage(in SettingsSkinning settings, ref GroupInstance groupInstance)
		{
			if (settings.explicitDataRegistry)
			{
				return settings.explicitDataRegistry;
			}


			if (groupInstance.sceneObjects.rootMeshAttachment != null)
			{	
				var currentStorage = groupInstance.sceneObjects.rootMeshAttachment.DataStorage;
				return currentStorage;
			}
			return null;
		}
#endif
		#endregion

		#region Update Prerequisite
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_2_PREVIEW
		private HashSet<SkinAttachmentMesh> preqGPUAttachments = new HashSet<SkinAttachmentMesh>();
		private Hash128 preqGPUAttachmentHash = default;
#elif HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_0_PREVIEW
		private HashSet<SkinAttachmentTarget> preqGPUAttachmentTargets = new HashSet<SkinAttachmentTarget>();
		private Hash128 preqGPUAttachmentTargetsHash = new Hash128();
#endif
		private int preqCountdown = 1;

		void UpdatePrerequisite()
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_2_PREVIEW
			var hash = new Hash128();
			{
				if (strandGroupInstances != null)
				{
					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						SkinAttachmentMesh rootAttachment = strandGroupInstances[i].sceneObjects.rootMeshAttachment;
						ref readonly var settingsSkinning = ref GetSettingsSkinning(strandGroupInstances[i]);
						if (settingsSkinning.rootsAttach)
						{
							if (rootAttachment != null && rootAttachment.isActiveAndEnabled && rootAttachment.SchedulingMode == SkinAttachmentComponentCommon.SchedulingMode.GPU)
							{
								hash.Append(rootAttachment.GetInstanceID());
							}
						}
					}
				}
			}

			if (hash != preqGPUAttachmentHash)
			{
				ReleasePrerequisite();

				if (strandGroupInstances != null)
				{
					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						SkinAttachmentMesh rootAttachment = strandGroupInstances[i].sceneObjects.rootMeshAttachment;
						ref readonly var settingsSkinning = ref GetSettingsSkinning(strandGroupInstances[i]);
						if (settingsSkinning.rootsAttach)
						{
							if (rootAttachment != null && rootAttachment.isActiveAndEnabled  && rootAttachment.SchedulingMode == SkinAttachmentComponentCommon.SchedulingMode.GPU && rootAttachment.IsAttachmentMeshValid() && rootAttachment.ValidateBakedData())
							{
								preqGPUAttachments.Add(rootAttachment);
							}
						}
					}
				}

				foreach (var preq in preqGPUAttachments)
				{
					preq.onSkinAttachmentMeshResolved += HandlePrerequisite;
				}

				preqGPUAttachmentHash = hash;
			}

			preqCountdown = 1 + preqGPUAttachments.Count;
#elif HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_0_PREVIEW
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
				ReleasePrerequisite();

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

				preqGPUAttachmentTargetsHash = hash;
			}

			preqCountdown = 1 + preqGPUAttachmentTargets.Count;
#else
			preqCountdown = 1;
#endif
		}

		void ReleasePrerequisite()
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_2_PREVIEW
			foreach (var preq in preqGPUAttachments)
			{
				preq.onSkinAttachmentMeshResolved -= HandlePrerequisite;
			}

			preqGPUAttachments.Clear();
			preqGPUAttachmentHash = default;
#elif HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_0_PREVIEW
			foreach (var preq in preqGPUAttachmentTargets)
			{
				preq.afterGPUAttachmentWorkCommitted -= HandlePrerequisite;
			}

			preqGPUAttachmentTargets.Clear();
			preqGPUAttachmentTargetsHash = new Hash128();
#endif
		}

		void HandlePrerequisite()
		{
			if (--preqCountdown == 0)
			{
				DispatchUpdate();
			}
		}
		#endregion

		public ExecutiveStep UpdateExecutiveState(float dt)
		{
			var stepDesc = new ExecutiveStep();
			{
				stepDesc.countRaw = 0;
				stepDesc.count = 0;
				stepDesc.dt = 0.0f;
				stepDesc.hi = 1.0f;
			}

			if (GetSimulationActive())
			{
				var simulationTimeStep = GetSimulationTimeStep();
				if (simulationTimeStep > 0.0f)
				{
					execState.accumulatedTime += dt;

					var accumulatedStepCount = Mathf.FloorToInt(execState.accumulatedTime / simulationTimeStep);
					{
						var minStepCount = settingsExecutive.updateStepsMin ? settingsExecutive.updateStepsMinValue : accumulatedStepCount;
						var maxStepCount = settingsExecutive.updateStepsMax ? settingsExecutive.updateStepsMaxValue : accumulatedStepCount;

						stepDesc.countRaw = accumulatedStepCount;
						stepDesc.count = Mathf.Clamp(accumulatedStepCount, minStepCount, maxStepCount);
						stepDesc.dt = simulationTimeStep;

						// method A: align per-frame data with simulation time steps
						//
						//           accu + accu = a
						//      .---------.--------------.
						//      :         :              :
						// F----X---------F--------------F - - -
						//      :
						//      : dt   dt   dt
						//      O----S----S----X---------|
						//      :              :         :
						//      '--------------'---------'
						//          (dt * n)   :    b
						//                     :
						//                     '- blend fraction
						{
							//TODO put this to use pending changes to root interpolation and staging resolve
							stepDesc.hi = (simulationTimeStep * accumulatedStepCount) / execState.accumulatedTime;
						}
					}

					execState.accumulatedTime -= simulationTimeStep * accumulatedStepCount;
				}

				execState.elapsedTimeRaw += dt;
				execState.elapsedTime += stepDesc.dt * stepDesc.count;

				if (execState.pausePostStep && stepDesc.count > 0)
				{
					execState.pausePostStep = false;

					//TODO separate editor player state from settings?
					settingsExecutive.updateSimulation = false;
				}
			}
			else
			{
				if (execState.pausePostStep)
				{
					execState.pausePostStep = false;
				}
			}

			//timeState.stepHistory.Enqueue(stepDesc);

			execState.lastStepCount = stepDesc.count;
			execState.lastStepCountRaw = stepDesc.countRaw;
			execState.lastStepCountSmooth = Mathf.Lerp(execState.lastStepCountSmooth, stepDesc.count, 1.0f - Mathf.Pow(0.01f, dt / 0.2f));

			return stepDesc;
		}

		void UpdateSystemState(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, in ExecutiveStep stepDesc)
		{
			for (int i = 0; i != solverData.Length; i++)
			{
				var rootMesh = strandGroupInstances[i].sceneObjects.rootMeshFilter.sharedMesh;
				var rootMeshMatrix = strandGroupInstances[i].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
				var rootMeshSkinningRotation = GetRootMeshSkinningRotation(strandGroupInstances[i], GetSettingsSkinning(strandGroupInstances[i]));

				HairSim.PushSolverRoots(cmd, cmdFlags, ref solverData[i], rootMesh, rootMeshMatrix, rootMeshSkinningRotation, stepDesc.count);
			}

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.PushSolverGeometry(cmd, ref solverData[i], GetSettingsGeometry(strandGroupInstances[i]), this.transform.localToWorldMatrix);
			}

			HairSim.PushVolumeObservers(cmd, ref volumeData, CameraType.Game | CameraType.SceneView);
			HairSim.PushVolumeGeometry(cmd, ref volumeData, solverData);
			HairSim.PushVolumeBounds(cmd, ref volumeData, solverData);

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.PushSolverLOD(cmd, ref solverData[i], GetSettingsPhysics(strandGroupInstances[i]), GetSettingsRendering(strandGroupInstances[i]), volumeData, stepDesc.count);
			}

			HairSim.PushVolumeLOD(cmd, ref volumeData, settingsVolumetrics);
			HairSim.PushVolumeEnvironment(cmd, ref volumeData, settingsEnvironment, stepDesc.count, 1.0f);//stepDesc.hi);
			HairSim.PushVolumeStepBegin(cmd, ref volumeData, settingsVolumetrics, stepDesc.dt);
			{
				//var frameTimeLo = execState.elapsedTime - stepDesc.count * stepDesc.dt;
				//var frameTimeHi = execState.elapsedTime;

				if (stepDesc.count >= 1)
				{
					var stepVolumePre = HairSim.PrepareVolumeData(ref volumeData, settingsVolumetrics, solverData.Length + 1);
					if (stepVolumePre)
					{
						var stepFracLo = 0.0f;
						var stepFracHi = 1.0f / (float)stepDesc.count;
						var stepTimeHi = execState.elapsedTime - stepDesc.dt * (stepDesc.count - 1);

						HairSim.PushVolumeStep(cmd, cmdFlags, ref volumeData, settingsVolumetrics, solverData, stepFracLo, stepFracHi, stepTimeHi);
					}

					for (int i = 0; i != solverData.Length; i++)
					{
						HairSim.PushSolverStepBegin(cmd, ref solverData[i], GetSettingsPhysics(strandGroupInstances[i]), stepDesc.dt);
					}

					for (int k = 0; k != stepDesc.count; k++)
					{
						var stepFracLo = (k + 0) / (float)stepDesc.count;
						var stepFracHi = (k + 1) / (float)stepDesc.count;
						var stepTimeHi = execState.elapsedTime - stepDesc.dt * (stepDesc.count - 1 - k);

						for (int i = 0; i != solverData.Length; i++)
						{
							HairSim.PushSolverStep(cmd, ref solverData[i], GetSettingsPhysics(strandGroupInstances[i]), volumeData, stepFracLo, stepFracHi, stepFinal: k == stepDesc.count - 1);
						}

						HairSim.PushVolumeStep(cmd, cmdFlags, ref volumeData, settingsVolumetrics, solverData, stepFracLo, stepFracHi, stepTimeHi);

						if (onSimulationStateChanged != null)
							onSimulationStateChanged(cmd);
					}

					for (int i = 0; i != solverData.Length; i++)
					{
						HairSim.PushSolverStepEnd(cmd, solverData[i], volumeData);
					}
				}
			}
			HairSim.PushVolumeStepEnd(cmd, volumeData, settingsVolumetrics);

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.PushSolverStaging(cmd, ref solverData[i], GetSettingsGeometry(strandGroupInstances[i]), GetSettingsRendering(strandGroupInstances[i]), volumeData);
			}

			if (onRenderingStateChanged != null)
				onRenderingStateChanged(cmd);
		}

		void UpdateSceneState(CommandBuffer cmd)
		{
			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				UpdateRendererState(cmd, ref strandGroupInstances[i], solverData[i]);
			}
		}

		void UpdateRendererState(CommandBuffer cmd, ref GroupInstance strandGroupInstance, in HairSim.SolverData solverData)
		{
			ref readonly var settingsRendering = ref GetSettingsRendering(strandGroupInstance);

			#region GetRenderTopologyType(..)
			static HairTopologyType GetRenderTopologyType(HairSim.SettingsRendering.Renderer renderer)
			{
				switch (renderer)
				{
					default:
						return HairTopologyType.Lines;
					case HairSim.SettingsRendering.Renderer.BuiltinStrips:
						return HairTopologyType.Strips;
					case HairSim.SettingsRendering.Renderer.BuiltinTubes:
						return HairTopologyType.Tubes;
				}
			}
			#endregion

			#region GetShadowTopologyType(..)
			static HairTopologyType GetShadowTopologyType(HairSim.SettingsRendering.ShadowSubstitute shadowSubstitute)
			{
				switch (shadowSubstitute)
				{
					default:
						return HairTopologyType.Lines;
					case HairSim.SettingsRendering.ShadowSubstitute.BuiltinStrips:
						return HairTopologyType.Strips;
					case HairSim.SettingsRendering.ShadowSubstitute.BuiltinTubes:
						return HairTopologyType.Tubes;
				}
			}
			#endregion

			#region GetTopologyMesh(..)
#if !UNITY_2021_2_OR_NEWER
			static Mesh GetTopologyMesh(in HairSim.SolverData solverData, HairTopologyType meshType, bool gpuInstancing, ref Mesh meshInstance, ref ulong meshInstanceKey)
#else
			static Mesh GetTopologyMesh(in HairSim.SolverData solverData, HairTopologyType meshType, bool gpuInstancing)
#endif
			{
				var meshDesc = new HairTopologyDesc
				{
					type = meshType,
					strandCount = Mathf.Min((int)solverData.constants._StrandCount, gpuInstancing ? HairSim.Conf.INSTANCING_BATCH_SIZE : (int)solverData.constants._StrandCount),
					strandParticleCount = (int)solverData.constants._StagingStrandVertexCount,
					memoryLayout = solverData.memoryLayout,
				};

#if !UNITY_2021_2_OR_NEWER
				// if possible, keep current instance and do not touch topology cache (allow shared mesh to deallocate)
				var meshKey = HairTopologyCache.GetSortKey(meshDesc);
				if (meshKey != meshInstanceKey || meshInstance == null)
				{
					CoreUtils.Destroy(meshInstance);
						
					meshInstance = HairInstanceBuilder.CreateMeshInstance(HairTopologyCache.GetSharedMesh(meshDesc), HideFlags.HideAndDontSave);
					meshInstanceKey = meshKey;
				}

				return meshInstance;
#else
				return HairTopologyCache.GetSharedMesh(meshDesc);
#endif
			}
			#endregion

			#region GetTopologyMaterial(..)
			static Material GetTopologyMaterial(in HairSim.SolverData solverData, in HairSim.VolumeData volumeData, Mesh mesh, HairTopologyType meshType, bool gpuInstancing, Material materialAsset, ref Material materialInstance)
			{
				if (materialAsset != null && materialAsset.shader != null)
				{
					if (materialInstance == null)
					{
						materialInstance = new Material(materialAsset.shader);
						materialInstance.hideFlags = HideFlags.HideAndDontSave;
						materialInstance.name = materialAsset.name + "(Instance)";
					}
					else
					{
						if (materialInstance.shader != materialAsset.shader)
						{
							materialInstance.shader = materialAsset.shader;
							materialInstance.name = materialAsset.name + "(Instance)";
						}
					}

					materialInstance.CopyPropertiesFromMaterial(materialAsset);
					materialInstance.EnableKeyword("HAIR_VERTEX_LIVE");
				}

				if (materialInstance != null)
				{
					UpdateMaterialState(materialInstance, solverData, volumeData, mesh, meshType);

#if UNITY_EDITOR
					var materialInstancePendingPasses = HairMaterialUtility.TryCompileCountPassesPending(materialInstance);
					if (materialInstancePendingPasses > 0)
					{
						materialInstance.shader = HairMaterialUtility.GetReplacementShader(HairMaterialUtility.ReplacementType.Async);
						UpdateMaterialState(materialInstance, solverData, volumeData, mesh, meshType);
					}
#endif
				}

				return materialInstance;
			}
			#endregion

			// prepare settings
			var layerMask = settingsRendering.rendererLayers;
			var layerMaskShadows = settingsRendering.shadowLayers ? settingsRendering.shadowLayersValue : layerMask;

			var meshType = GetRenderTopologyType(settingsRendering.renderer);
			var meshTypeShadows = settingsRendering.shadowSubstitute ? GetShadowTopologyType(settingsRendering.shadowSubstituteValue) : meshType;

			// prepare conditionals
			var needRenderer = false;
			var needRendererShadows = false;

			var needResources = false;
			var needResourcesShadows = false;
			{
				if (settingsRendering.renderer != HairSim.SettingsRendering.Renderer.Disabled)
				{
					var sameShadowLayer = (layerMask == layerMaskShadows);
					var sameShadowMesh = (meshType == meshTypeShadows);
					var sameShadow = sameShadowLayer && sameShadowMesh;

					if (sameShadow == true || settingsRendering.rendererShadows != ShadowCastingMode.ShadowsOnly)
					{
						needRenderer = true;
						needResources = true;
					}

					if (sameShadow == false && settingsRendering.rendererShadows != ShadowCastingMode.Off)
					{
						needRendererShadows = true;
						needResourcesShadows = (sameShadowMesh == false) || (needResources == false);
					}
				}
			}

			// prepare instancing
			var supportIndirect = false;
			{
#pragma warning disable CS0219
#if UNITY_EDITOR
				const bool false_if_UNITY_EDITOR = false;
#else
				const bool false_if_UNITY_EDITOR = true;
#endif
#pragma warning restore CS0219

				// indirect feature table
				//
				//	N = NO
				//	y = yes, partially (no picking)
				//	Y = YES
				//
				//			BRP/20	BRP/22	BRP/23	SRP/20	SRP/22	SRP/23
				//	-1		y		y		Y		y		y		Y
				//	mask	y		y		Y		N		y		Y

#if UNITY_2023_2_OR_NEWER
				// supports indirect w/ layer mask + picking
				supportIndirect = true;
#elif UNITY_2021_2_OR_NEWER
				// supports indirect w/ layer mask
				supportIndirect = false_if_UNITY_EDITOR;
#else
				// supports indirect
				supportIndirect = false_if_UNITY_EDITOR && ((RenderPipelineManager.currentPipeline == null) || (layerMask == GraphicsSettings.defaultRenderingLayerMask));
#endif
			}

			var enableIndirect = settingsRendering.allowIndirect && supportIndirect && (settingsRendering.renderer != HairSim.SettingsRendering.Renderer.HDRPHighQualityLines);
			var enableIndirectShadows = settingsRendering.allowIndirect && supportIndirect;
			var enableInstancing = settingsRendering.allowInstancing && enableIndirect;
			var enableInstancingShadows = settingsRendering.allowInstancing && enableIndirectShadows;
			var enableStereoInstancing = (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced);

			var topologyIndex = (int)meshType + (enableInstancing ? 3 : 0) + (enableStereoInstancing ? 6 : 0);
			var topologyIndexShadows = (int)meshTypeShadows + (enableInstancingShadows ? 3 : 0);

			// prepare resources
			var mesh = null as Mesh;
			var meshShadows = null as Mesh;

			var materialAsset = GetMaterial(strandGroupInstance);
			var materialInstance = null as Material;
			var materialInstanceShadows = null as Material;
			{
				if (needResources)
				{
#if !UNITY_2021_2_OR_NEWER
					mesh = GetTopologyMesh(solverData, meshType, enableInstancing,
						ref strandGroupInstance.sceneObjects.meshInstance,
						ref strandGroupInstance.sceneObjects.meshInstanceKey);
#else
					mesh = GetTopologyMesh(solverData, meshType, enableInstancing);
#endif
					materialInstance = GetTopologyMaterial(solverData, volumeData, mesh, meshType, enableInstancing, materialAsset, ref strandGroupInstance.sceneObjects.materialInstance);
				}

				if (needResourcesShadows)
				{
#if !UNITY_2021_2_OR_NEWER
					meshShadows = GetTopologyMesh(solverData, meshTypeShadows, enableInstancingShadows,
						ref strandGroupInstance.sceneObjects.meshInstanceShadows,
						ref strandGroupInstance.sceneObjects.meshInstanceKeyShadows);
#else
					meshShadows = GetTopologyMesh(solverData, meshTypeShadows, enableInstancingShadows);
#endif
					materialInstanceShadows = GetTopologyMaterial(solverData, volumeData, meshShadows, meshTypeShadows, enableInstancingShadows, materialAsset, ref strandGroupInstance.sceneObjects.materialInstanceShadows);
				}
				else
				{
					meshShadows = mesh;
					materialInstanceShadows = materialInstance;
				}
			}

			// update mesh filter
			ref var meshFilter = ref strandGroupInstance.sceneObjects.strandMeshFilter;
			{
				if (meshFilter.sharedMesh != mesh)
					meshFilter.sharedMesh = mesh;
			}

			// update mesh renderer
			ref var meshRenderer = ref strandGroupInstance.sceneObjects.strandMeshRenderer;
			{
				meshRenderer.enabled = needRenderer;
				meshRenderer.sharedMaterial = materialInstance;
				meshRenderer.shadowCastingMode = needRendererShadows ? ShadowCastingMode.Off : settingsRendering.rendererShadows;
				meshRenderer.renderingLayerMask = (uint)layerMask;
				meshRenderer.motionVectorGenerationMode = settingsRendering.motionVectors;

				if (meshRenderer.rayTracingMode != UnityEngine.Experimental.Rendering.RayTracingMode.Off && SystemInfo.supportsRayTracing)
					meshRenderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
			}

#if HAS_PACKAGE_UNITY_HDRP_15_0_2
			ref var meshRendererHDRP = ref strandGroupInstance.sceneObjects.strandMeshRendererHDRP;
			{
				if (meshRendererHDRP == null)
				{
					var container = strandGroupInstance.sceneObjects.strandMeshContainer;
					if (container != null && container.TryGetComponent(out meshRendererHDRP) == false)
					{
						meshRendererHDRP = strandGroupInstance.sceneObjects.strandMeshRendererHDRP = HairInstanceBuilder.CreateComponent<HDAdditionalMeshRendererSettings>(container, container.hideFlags);
					}
				}

				meshRendererHDRP.enabled = needRenderer && (settingsRendering.rendererShadows != ShadowCastingMode.ShadowsOnly);
				meshRendererHDRP.rendererGroup = settingsRendering.rendererGroup;
				meshRendererHDRP.enableHighQualityLineRendering = (settingsRendering.renderer == HairSim.SettingsRendering.Renderer.HDRPHighQualityLines);
			}
#endif

			// update mesh bounds
			{
#if !UNITY_2021_2_OR_NEWER
				// prior to 2021.2 it was only possible to set renderer bounds indirectly via mesh bounds
				if (mesh != null)
					mesh.bounds = HairSim.GetSolverBounds(solverData, volumeData).WithTransform(meshFilter.transform.worldToLocalMatrix);
#else
				// starting with 2021.2 we can override renderer bounds directly
				meshRenderer.localBounds = HairSim.GetSolverBounds(solverData, volumeData).WithTransform(meshFilter.transform.worldToLocalMatrix);

				//TODO the world space bounds override is failing in some cases -- figure out why?
				//meshRenderer.bounds = HairSim.GetSolverBounds(solverData, volumeData);
#endif
			}

			// render (optional depending on configuration)
			if (needRenderer)
			{
				if (enableIndirect)
				{
					meshRenderer.enabled = false;

#if HAS_PACKAGE_UNITY_HDRP_15_0_2
					if (meshRendererHDRP != null)
						meshRendererHDRP.enabled = false;
#endif

#if !UNITY_2021_2_OR_NEWER
					Graphics.DrawMeshInstancedIndirect(
						mesh,
						submeshIndex: 0,
						materialInstance,
						HairSim.GetSolverBounds(solverData, volumeData),
						solverData.buffers._SolverLODTopology,
						argsOffset: (int)HairSim.GetSolverLODTopologyOffset((HairSim.SolverLODTopology)topologyIndex),
						properties: null,
						meshRenderer.shadowCastingMode);
#else
					var rparams = new RenderParams(materialInstance);
					{
						/* defaults since 2023.2:

						rparams.layer = 0;
						rparams.renderingLayerMask = GraphicsSettings.defaultRenderingLayerMask;
						rparams.rendererPriority = 0;
						rparams.worldBounds = new Bounds(Vector3.zero, Vector3.zero);
						rparams.camera = null;
						rparams.motionVectorMode = MotionVectorGenerationMode.Camera;
						rparams.reflectionProbeUsage = ReflectionProbeUsage.Off;
						rparams.material = mat;
						rparams.matProps = null;
						rparams.shadowCastingMode = ShadowCastingMode.Off;
						rparams.receiveShadows = false;
						rparams.lightProbeUsage = LightProbeUsage.Off;
						rparams.lightProbeProxyVolume = null;
						rparams.overrideSceneCullingMask = false;
						rparams.sceneCullingMask = 0uL;
						rparams.instanceID = 0;

						*/

						rparams.renderingLayerMask = (uint)meshRenderer.renderingLayerMask;
						rparams.worldBounds = HairSim.GetSolverBounds(solverData, volumeData);
						rparams.motionVectorMode = meshRenderer.motionVectorGenerationMode;
						rparams.shadowCastingMode = meshRenderer.shadowCastingMode;
						rparams.receiveShadows = true;
#if UNITY_2023_2_OR_NEWER
						rparams.instanceID = this.GetInstanceID();
#endif
					}

					Graphics.RenderMeshIndirect(rparams, mesh, solverData.buffers._SolverLODTopology, 1, topologyIndex);
#endif
				}
			}

			// render separate shadows (optional depending on configuration)
			if (needRendererShadows)
			{
#if !UNITY_2021_2_OR_NEWER
				if (enableIndirectShadows)
				{
					Graphics.DrawMeshInstancedIndirect(
						meshShadows,
						submeshIndex: 0,
						materialInstanceShadows,
						HairSim.GetSolverBounds(solverData, volumeData),
						solverData.buffers._SolverLODTopology,
						argsOffset: (int)HairSim.GetSolverLODTopologyOffset((HairSim.SolverLODTopology)topologyIndexShadows),
						properties: null,
						ShadowCastingMode.ShadowsOnly);
				}
				else
				{
					if (meshShadows != null)
						meshShadows.bounds = HairSim.GetSolverBounds(solverData, volumeData).WithTransform(meshFilter.transform.worldToLocalMatrix);

					Graphics.DrawMesh(meshShadows, Matrix4x4.identity, materialInstanceShadows, 0, null, 0, null, ShadowCastingMode.ShadowsOnly);
				}
#else
				var rparams = new RenderParams(materialInstanceShadows);
				{
					/* defaults since 2023.2:

					rparams.layer = 0;
					rparams.renderingLayerMask = GraphicsSettings.defaultRenderingLayerMask;
					rparams.rendererPriority = 0;
					rparams.worldBounds = new Bounds(Vector3.zero, Vector3.zero);
					rparams.camera = null;
					rparams.motionVectorMode = MotionVectorGenerationMode.Camera;
					rparams.reflectionProbeUsage = ReflectionProbeUsage.Off;
					rparams.material = mat;
					rparams.matProps = null;
					rparams.shadowCastingMode = ShadowCastingMode.Off;
					rparams.receiveShadows = false;
					rparams.lightProbeUsage = LightProbeUsage.Off;
					rparams.lightProbeProxyVolume = null;
					rparams.overrideSceneCullingMask = false;
					rparams.sceneCullingMask = 0uL;
					rparams.instanceID = 0;

					*/

					rparams.renderingLayerMask = (uint)layerMaskShadows;
					rparams.worldBounds = HairSim.GetSolverBounds(solverData, volumeData);
					rparams.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
				}

				if (enableIndirectShadows)
				{
					Graphics.RenderMeshIndirect(rparams, meshShadows, solverData.buffers._SolverLODTopology, 1, topologyIndexShadows);
				}
				else
				{
					Graphics.RenderMesh(rparams, meshShadows, 0, Matrix4x4.identity);
				}
#endif
			}
		}

		static void UpdateMaterialState(Material materialInstance, in HairSim.SolverData solverData, in HairSim.VolumeData volumeData, Mesh mesh, HairTopologyType meshType)
		{
			HairSim.BindSolverData(materialInstance, solverData);
			HairSim.BindVolumeData(materialInstance, volumeData);

			switch (meshType)
			{
				case HairTopologyType.Lines:
					materialInstance.SetInt("_DecodeVertexCount", 1);
					materialInstance.SetInt("_DecodeVertexWidth", 0);
					break;

				case HairTopologyType.Strips:
					materialInstance.SetInt("_DecodeVertexCount", 2);
					materialInstance.SetInt("_DecodeVertexWidth", 1);
					break;

				case HairTopologyType.Tubes:
					materialInstance.SetInt("_DecodeVertexCount", 4);
					materialInstance.SetInt("_DecodeVertexWidth", 2);
					break;
			}

			if (mesh != null)
			{
				switch (mesh.GetVertexAttributeFormat(VertexAttribute.TexCoord0))
				{
					case VertexAttributeFormat.UNorm16:
						materialInstance.SetInt("_DecodeVertexComponentValue", ushort.MaxValue);
						materialInstance.SetInt("_DecodeVertexComponentWidth", 16);
						break;

					case VertexAttributeFormat.UNorm8:
						materialInstance.SetInt("_DecodeVertexComponentValue", byte.MaxValue);
						materialInstance.SetInt("_DecodeVertexComponentWidth", 8);
						break;
				}
			}

			materialInstance.SetTexture("_UntypedVolumeDensity", volumeData.textures._VolumeDensity);
			materialInstance.SetTexture("_UntypedVolumeVelocity", volumeData.textures._VolumeVelocity);
			materialInstance.SetTexture("_UntypedVolumeScattering", volumeData.textures._VolumeScattering);
		}

		public bool GetSimulationActive()
		{
			return settingsExecutive.updateSimulation && (settingsExecutive.updateSimulationInEditor || Application.isPlaying);
		}

		public float GetSimulationTimeStep()
		{
			switch (settingsExecutive.updateSimulationRate)
			{
				case SettingsExecutive.UpdateRate.Fixed30Hz: return 1.0f / 30.0f;
				case SettingsExecutive.UpdateRate.Fixed60Hz: return 1.0f / 60.0f;
				case SettingsExecutive.UpdateRate.Fixed90Hz: return 1.0f / 90.0f;
				case SettingsExecutive.UpdateRate.Fixed120Hz: return 1.0f / 120.0f;
				case SettingsExecutive.UpdateRate.CustomTimeStep: return Mathf.Max(0.0f, settingsExecutive.updateTimeStep);
				default: return 0.0f;
			}
		}

		public static Bounds GetRootMeshBounds(in GroupInstance strandGroupInstance)
		{
			var rootLocalBounds = strandGroupInstance.sceneObjects.rootMeshFilter.sharedMesh.bounds;
			var rootLocalToWorld = strandGroupInstance.sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
			{
				return rootLocalBounds.WithTransform(rootLocalToWorld);
			}
		}

		public static Quaternion GetRootMeshSkinningRotation(in GroupInstance strandGroupInstance, in SettingsSkinning settingsSkinning)
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			if (settingsSkinning.rootsAttach && settingsSkinning.rootsAttachTarget != null)
			{
				return settingsSkinning.rootsAttachTargetBone.skinningBone.rotation;
			}
#endif
			return strandGroupInstance.sceneObjects.rootMeshFilter.transform.rotation;
		}

		public ref readonly SettingsSkinning GetSettingsSkinning(in GroupInstance strandGroupInstance)
		{
			var i = strandGroupInstance.settingsIndex;
			if (i != -1 && strandGroupSettings[i].settingsSkinningToggle)
				return ref strandGroupSettings[i].settingsSkinning;
			else
				return ref strandGroupDefaults.settingsSkinning;
		}

		public ref readonly HairSim.SettingsGeometry GetSettingsGeometry(in GroupInstance strandGroupInstance)
		{
			var i = strandGroupInstance.settingsIndex;
			if (i != -1 && strandGroupSettings[i].settingsGeometryToggle)
				return ref strandGroupSettings[i].settingsGeometry;
			else
				return ref strandGroupDefaults.settingsGeometry;
		}

		public ref readonly HairSim.SettingsRendering GetSettingsRendering(in GroupInstance strandGroupInstance)
		{
			var i = strandGroupInstance.settingsIndex;
			if (i != -1 && strandGroupSettings[i].settingsRenderingToggle)
				return ref strandGroupSettings[i].settingsRendering;
			else
				return ref strandGroupDefaults.settingsRendering;
		}

		public ref readonly HairSim.SettingsPhysics GetSettingsPhysics(in GroupInstance strandGroupInstance)
		{
			var i = strandGroupInstance.settingsIndex;
			if (i != -1 && strandGroupSettings[i].settingsPhysicsToggle)
				return ref strandGroupSettings[i].settingsPhysics;
			else
				return ref strandGroupDefaults.settingsPhysics;
		}

		public Material GetMaterial(in GroupInstance strandGroupInstance)
		{
			ref readonly var settingsRendering = ref GetSettingsRendering(strandGroupInstance);

			var mat = null as Material;

			if (mat == null && settingsRendering.material)
				mat = settingsRendering.materialAsset;

			if (mat == null)
				mat = HairMaterialUtility.GetCurrentPipelineDefault();

			return mat;
		}

		public void DispatchUpdate()
		{
			using (var cmd = HairSimUtility.ScopedCommandBuffer.Get())
			{
				if (DispatchUpdate(cmd, CommandBufferExecutionFlags.None, Time.deltaTime))
				{
					Graphics.ExecuteCommandBuffer(cmd);
				}
			}
		}

		public bool DispatchUpdate(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, float dt)
		{
			if (InitializeRuntimeData())
			{
				var stepDesc = UpdateExecutiveState(dt);
				{
					UpdateSystemState(cmd, cmdFlags, stepDesc);
					UpdateSceneState(cmd);
				}

				return true;
			}
			else
			{
				return false;
			}
		}

		public void DispatchDraw(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags)
		{
			if (InitializeRuntimeData())
			{
				// draw solver data
				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.DrawSolverData(cmd, solverData[i], settingsDebugging);
				}

				// draw volume data
				HairSim.DrawVolumeData(cmd, volumeData, settingsDebugging);
			}
		}

		bool InitializeRuntimeData()
		{
			var groupStatus = CheckStrandGroupInstances();
			if (groupStatus != StrandGroupInstancesStatus.Valid)
				return false;

			var groupCount = strandGroupInstances?.Length ?? 0;
			if (groupCount == 0)
				return false;

			if (solverData?.Length == groupCount)
				return true;

			// record and dispatch initialization
			using (var cmd = HairSimUtility.ScopedCommandBuffer.Get())
			{
				InitializeRuntimeDataUnchecked(cmd, CommandBufferExecutionFlags.None);
				Graphics.ExecuteCommandBuffer(cmd);
			}

			// ensure that readback buffers are ready first frame
			AsyncGPUReadback.WaitAllRequests();

			// ready
			return true;
		}

		void InitializeRuntimeDataUnchecked(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags)
		{
			var groupCount = strandGroupInstances.Length;

			// prepare solver data
			solverData = new HairSim.SolverData[groupCount];
			{
				for (int i = 0; i != groupCount; i++)
				{
					ref readonly var groupAsset = ref strandGroupInstances[i].groupAssetReference.Resolve();

					HairSim.PrepareSolverData(ref solverData[i], groupAsset.strandCount, groupAsset.strandParticleCount, groupAsset.lodCount);
					{
						HairAssetUtility.DeclareParticleStride(groupAsset, out var strandParticleOffset, out var strandParticleStride);

						ref var solverConstants = ref solverData[i].constants;
						{
							solverConstants._StrandCount = (uint)groupAsset.strandCount;
							solverConstants._StrandParticleCount = (uint)groupAsset.strandParticleCount;
							solverConstants._StrandParticleOffset = (uint)strandParticleOffset;
							solverConstants._StrandParticleStride = (uint)strandParticleStride;
							solverConstants._LODCount = (uint)groupAsset.lodCount;
							//solverConstants._GroupScale
							//solverConstants._GroupMaxParticleVolume
							//solverConstants._GroupMaxParticleInterval
							//solverConstants._GroupMaxParticleDiameter
							//solverConstants._GroupAvgParticleDiameter
							//solverConstants._GroupAvgParticleMargin
							solverConstants._GroupBoundsIndex = (uint)i;
							//solverConstants._GroupBoundsPadding

							HairSimUtility.PushConstantBufferData(cmd, solverData[i].buffers.SolverCBuffer, solverConstants);
						}

						solverData[i].memoryLayout = groupAsset.particleMemoryLayout;

						solverData[i].initialStrandLengthTotal = groupAsset.strandLengthTotal;
						solverData[i].initialStrandParamsMax = groupAsset.strandParamsMax;
						solverData[i].initialStrandParamsAvg = groupAsset.strandParamsAvg;

						solverData[i].lodThreshold = new NativeArray<float>(groupAsset.lodThreshold, Allocator.Persistent);
					}

					using (var uploadCtx = new HairSimUtility.BufferUploadContext(cmd, cmdFlags))
					{
						ref readonly var solverBuffers = ref solverData[i].buffers;

						uploadCtx.SetData(solverBuffers._RootUV, groupAsset.rootUV);
						uploadCtx.SetData(solverBuffers._RootScale, groupAsset.rootScale);
						uploadCtx.SetData(solverBuffers._ParticlePosition, groupAsset.particlePosition);

						unsafe
						{
							// optional particle features
							//TODO defer allocation and upload to staging (let render settings decide if material should be able to access)
							{
								var particleCount = (groupAsset.strandCount * groupAsset.strandParticleCount);
								var particleTexCoordAvailable = groupAsset.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.TexCoord);
								var particleDiameterAvailable = groupAsset.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.Diameter);

								HairSimUtility.CreateBuffer(ref solverData[i].buffers._ParticleOptTexCoord, "ParticleOptTexCoord", particleTexCoordAvailable ? particleCount : 1, sizeof(Vector2));
								HairSimUtility.CreateBuffer(ref solverData[i].buffers._ParticleOptDiameter, "ParticleOptDiameter", particleDiameterAvailable ? particleCount : 1, sizeof(Vector2));

								if (particleTexCoordAvailable) uploadCtx.SetData(solverBuffers._ParticleOptTexCoord, groupAsset.particleTexCoord);
								if (particleDiameterAvailable) uploadCtx.SetData(solverBuffers._ParticleOptDiameter, groupAsset.particleDiameter);
							}
						}

						uploadCtx.SetData(solverBuffers._LODGuideCount, groupAsset.lodGuideCount);
						uploadCtx.SetData(solverBuffers._LODGuideIndex, groupAsset.lodGuideIndex);
						uploadCtx.SetData(solverBuffers._LODGuideCarry, groupAsset.lodGuideCarry);
						uploadCtx.SetData(solverBuffers._LODGuideReach, groupAsset.lodGuideReach);
					}
				}
			}

			// prepare volume data
			volumeData = new HairSim.VolumeData();
			{
				HairSim.PrepareVolumeData(ref volumeData, settingsVolumetrics, solverData.Length + 1);
				{
					volumeData.constants._CombinedBoundsIndex = (uint)solverData.Length;
				}
			}

			// perform initialization
			{
				for (int i = 0; i != solverData.Length; i++)
				{
					var rootMesh = strandGroupInstances[i].sceneObjects.rootMeshFilter.sharedMesh;
					var rootMeshMatrix = strandGroupInstances[i].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
					var rootMeshSkinningRotation = GetRootMeshSkinningRotation(strandGroupInstances[i], GetSettingsSkinning(strandGroupInstances[i]));

					HairSim.PushSolverRoots(cmd, cmdFlags, ref solverData[i], rootMesh, rootMeshMatrix, rootMeshSkinningRotation, stepCount: 1);
					HairSim.PushSolverRootsHistory(cmd, solverData[i]);
				}

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.PushSolverGeometry(cmd, ref solverData[i], GetSettingsGeometry(strandGroupInstances[i]), this.transform.localToWorldMatrix);
				}

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.InitSolverData(cmd, solverData[i]);
				}

				HairSim.PushVolumeObservers(cmd, ref volumeData, CameraType.Game | CameraType.SceneView);
				HairSim.PushVolumeGeometry(cmd, ref volumeData, solverData);
				HairSim.PushVolumeBounds(cmd, ref volumeData, solverData);
				HairSim.PushVolumeBoundsHistory(cmd, volumeData);

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.PushSolverLODInit(cmd, solverData[i]);
					HairSim.PushSolverLOD(cmd, ref solverData[i], GetSettingsPhysics(strandGroupInstances[i]), GetSettingsRendering(strandGroupInstances[i]), volumeData, stepCount: 1);
				}

				HairSim.PushVolumeLOD(cmd, ref volumeData, settingsVolumetrics);
				HairSim.PushVolumeStepBegin(cmd, ref volumeData, settingsVolumetrics, 0.0f);
				HairSim.PushVolumeStep(cmd, cmdFlags, ref volumeData, settingsVolumetrics, solverData, stepFracLo: 0.0f, stepFracHi: 0.0f, stepTimeHi: 0.0);
				HairSim.PushVolumeStepEnd(cmd, volumeData, settingsVolumetrics);

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.InitSolverDataPostVolume(cmd, solverData[i], volumeData);
				}
			}
		}

		void ReleaseRuntimeData()
		{
			if (strandGroupInstances != null)
			{
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{	
					ref var strandGroupInstance = ref strandGroupInstances[i];

					CoreUtils.Destroy(strandGroupInstance.sceneObjects.materialInstance);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.materialInstanceShadows);

#if !UNITY_2021_2_OR_NEWER
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstance);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceShadows);

					strandGroupInstance.sceneObjects.meshInstanceKey = 0uL;
					strandGroupInstance.sceneObjects.meshInstanceKeyShadows = 0uL;
#endif
				}
			}

			AsyncGPUReadback.WaitAllRequests();

			if (solverData != null)
			{
				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.ReleaseSolverData(ref solverData[i]);
				}

				solverData = null;
			}

			HairSim.ReleaseVolumeData(ref volumeData);

			execState = ExecutiveState.defaults;
		}

		public void PauseSimulationPostStep()
		{
			execState.pausePostStep = true;
		}

		public void ResetSimulationState()
		{
			ReleaseRuntimeData();
		}
	}
}
