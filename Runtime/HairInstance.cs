#pragma warning disable 0414 // some fields are unused in case of disabled optional features

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
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

				[NonSerialized] public Mesh meshInstanceLines;
				[NonSerialized] public Mesh meshInstanceStrips;
				[NonSerialized] public Mesh meshInstanceTubes;
				[NonSerialized] public uint meshInstanceSubdivision;
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

		public struct ExecutiveState
		{
			public struct TimeStepDesc
			{
				public float dt;
				public int count;
				public int countRaw;
				public float countSmooth;

				public static readonly TimeStepDesc defaults = new TimeStepDesc()
				{
					dt = 1.0f / 60.0f,
					count = 1,
					countRaw = 1,
					countSmooth = 1.0f,
				};
			}

			public double accumulatedTimeSession;
			public float accumulatedTimeChunked;

			public TimeStepDesc stepDesc;

			public static readonly ExecutiveState defaults = new ExecutiveState()
			{
				accumulatedTimeChunked = 0.0f,
				accumulatedTimeSession = 0.0f,

				stepDesc = TimeStepDesc.defaults,
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
						if (container != null && container.TryGetComponent(out SkinAttachment oldAttachment))
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

							if (dataStorageNeedsRefresh || buildTargetChanged)
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

			SkinAttachmentDataRegistry storage = null;
			if (groupInstance.sceneObjects.rootMeshAttachment != null)
			{
				storage = groupInstance.sceneObjects.rootMeshAttachment.DataStorage;
			}

#if UNITY_EDITOR
			if(storage == null)
			{
				string assetPath;

#if UNITY_2021_2_OR_NEWER
				var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#else
				var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#endif

				var isPrefabInstance =  PrefabUtility.IsPartOfPrefabInstance(this);
				var isPartOfPrefabScene = prefabStage.IsPartOfPrefabContents(gameObject);
				if (isPrefabInstance)
				{
					assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(this);
				}
				else if (isPartOfPrefabScene)
				{
					assetPath = prefabStage.assetPath;
				}
				else
				{
					HairAsset hairAsset = groupInstance.groupAssetReference.hairAsset;
					assetPath = AssetDatabase.GetAssetPath(hairAsset);
				}


				string storagePath = Path.GetDirectoryName(assetPath) + "/" + Path.GetFileNameWithoutExtension(assetPath) + "_AttachmentDataRegistry" + ".asset";
				if (AssetDatabase.AssetPathExists(storagePath))
				{
					storage = (SkinAttachmentDataRegistry)AssetDatabase.LoadAssetAtPath(storagePath, typeof(SkinAttachmentDataRegistry));
				}

				if(storage == null)
				{
					storage = ScriptableObject.CreateInstance<SkinAttachmentDataRegistry>();
					AssetDatabase.CreateAsset(storage, storagePath);
				}
			}

#endif
			return storage;
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

		ExecutiveState.TimeStepDesc UpdateExecutiveState(float dt)
		{
			// accumulate time
			var simulationActive = GetSimulationActive();
			if (simulationActive)
			{
				execState.accumulatedTimeSession += dt;
				execState.accumulatedTimeChunked += dt;
			}
			else
			{
				execState.accumulatedTimeChunked = 0.0f;
			}

			// prepare time step
			ref var stepDesc = ref execState.stepDesc;
			{
				stepDesc.dt = GetSimulationTimeStep();
				stepDesc.count = (stepDesc.dt > 0.0f && simulationActive) ? Mathf.FloorToInt(execState.accumulatedTimeChunked / stepDesc.dt) : 0;
				stepDesc.countRaw = stepDesc.count;
			}

			// deduct time step from accumulated time
			execState.accumulatedTimeChunked -= (stepDesc.dt * stepDesc.count);

			// apply limits
			if (simulationActive)
			{
				stepDesc.count = Mathf.Max(stepDesc.count, settingsExecutive.updateStepsMin ? settingsExecutive.updateStepsMinValue : stepDesc.count);
				stepDesc.count = Mathf.Min(stepDesc.count, settingsExecutive.updateStepsMax ? settingsExecutive.updateStepsMaxValue : stepDesc.count);
			}

			// apply smoothing (note: only used for ui)
			stepDesc.countSmooth = Mathf.Lerp(stepDesc.countSmooth, stepDesc.count, 1.0f - Mathf.Pow(0.01f, dt / 0.2f));

			// update history
			if (simulationActive)
			{
				//TODO record history
				//execState.stepHistoryHead = (execState.stepHistoryHead + 1) % execState.stepHistoryLength;
				//execState.stepHistory[execState.stepHistoryHead] = stepDesc;
			}

			// done
			return stepDesc;
		}

		void UpdateSystemState(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, in ExecutiveState.TimeStepDesc stepDesc)
		{
			for (int i = 0; i != solverData.Length; i++)
			{
				var rootMesh = strandGroupInstances[i].sceneObjects.rootMeshFilter.sharedMesh;
				var rootMeshMatrix = strandGroupInstances[i].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
				var rootMeshSkinningRotation = GetRootMeshSkinningRotation(strandGroupInstances[i], GetSettingsSkinning(strandGroupInstances[i]));

				HairSim.PushSolverRoots(cmd, cmdFlags, ref solverData[i], rootMesh, rootMeshMatrix, rootMeshSkinningRotation, stepDesc.dt * stepDesc.count);
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
				HairSim.PushSolverLOD(cmd, ref solverData[i], GetSettingsPhysics(strandGroupInstances[i]), GetSettingsRendering(strandGroupInstances[i]), volumeData);
			}

			HairSim.PushVolumeLOD(cmd, ref volumeData, settingsVolumetrics);
			HairSim.PushVolumeEnvironment(cmd, ref volumeData, settingsEnvironment, (float)execState.accumulatedTimeSession);

			if (stepDesc.count == 0 || HairSim.PrepareVolumeData(ref volumeData, settingsVolumetrics, solverData.Length + 1))
			{
				HairSim.PushVolumeSettings(cmd, ref volumeData, settingsVolumetrics, stepDesc.dt);
				HairSim.PushVolumeStep(cmd, cmdFlags, ref volumeData, settingsVolumetrics, solverData, 1.0f);
			}

			if (stepDesc.count >= 1)
			{
				for (int k = 0; k != stepDesc.count; k++)
				{
					float stepFracLo = (k + 0) / (float)stepDesc.count;
					float stepFracHi = (k + 1) / (float)stepDesc.count;

					for (int i = 0; i != solverData.Length; i++)
					{
						ref readonly var settingsPhysics = ref GetSettingsPhysics(strandGroupInstances[i]);

						HairSim.PushSolverSettings(cmd, ref solverData[i], settingsPhysics, stepDesc.dt);
						HairSim.PushSolverStep(cmd, ref solverData[i], settingsPhysics, volumeData, stepFracLo, stepFracHi);
					}

					//TODO substep volume environment (interpolate colliders and emitters)
					//HairSim.SubstepVolumeEnvironment(cmd, ref volumeData, stepFracHi);

					HairSim.PushVolumeSettings(cmd, ref volumeData, settingsVolumetrics, stepDesc.dt);
					HairSim.PushVolumeStep(cmd, cmdFlags, ref volumeData, settingsVolumetrics, solverData, stepFracHi);

					if (onSimulationStateChanged != null)
						onSimulationStateChanged(cmd);
				}
			}

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.PushSolverStaging(cmd, ref solverData[i], GetSettingsGeometry(strandGroupInstances[i]), GetSettingsRendering(strandGroupInstances[i]), volumeData);
			}

			if (onRenderingStateChanged != null)
				onRenderingStateChanged(cmd);
		}

		void UpdateSceneState()
		{
			for (int i = 0; i != strandGroupInstances.Length; i++)
			{
				UpdateRendererState(ref strandGroupInstances[i], solverData[i]);
			}
		}

		void UpdateRendererState(ref GroupInstance strandGroupInstance, in HairSim.SolverData solverData)
		{
			ref readonly var settingsRendering = ref GetSettingsRendering(strandGroupInstance);

			// select mesh
			var mesh = null as Mesh;
			{
				ref var meshInstanceLines = ref strandGroupInstance.sceneObjects.meshInstanceLines;
				ref var meshInstanceStrips = ref strandGroupInstance.sceneObjects.meshInstanceStrips;
				ref var meshInstanceTubes = ref strandGroupInstance.sceneObjects.meshInstanceTubes;
				ref var meshInstanceSubdivision = ref strandGroupInstance.sceneObjects.meshInstanceSubdivision;

				var subdivision = solverData.constants._StagingSubdivision;
				if (subdivision != meshInstanceSubdivision)
				{
					CoreUtils.Destroy(meshInstanceLines);
					CoreUtils.Destroy(meshInstanceStrips);
					CoreUtils.Destroy(meshInstanceTubes);
					meshInstanceSubdivision = subdivision;
				}

				switch (settingsRendering.renderer)
				{
					case HairSim.SettingsRendering.Renderer.Disabled:
						{
							mesh = null;
						}
						break;

					case HairSim.SettingsRendering.Renderer.BuiltinLines:
					case HairSim.SettingsRendering.Renderer.HDRPHighQualityLines:
						{
							if (subdivision > 0)
							{
								mesh = HairInstanceBuilder.CreateRenderMeshLinesIfNull(ref meshInstanceLines, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.constants._StrandCount, (int)solverData.constants._StagingStrandVertexCount, new Bounds());
							}
							else
							{
								mesh = strandGroupInstance.groupAssetReference.Resolve().meshAssetLines;
#if !UNITY_2021_2_OR_NEWER
								mesh = HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceLines, strandGroupInstance.groupAssetReference.Resolve().meshAssetLines, HideFlags.HideAndDontSave);
#endif
							}
						}
						break;

					case HairSim.SettingsRendering.Renderer.BuiltinStrips:
						{
							if (subdivision > 0)
							{
								mesh = HairInstanceBuilder.CreateRenderMeshStripsIfNull(ref meshInstanceStrips, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.constants._StrandCount, (int)solverData.constants._StagingStrandVertexCount, new Bounds());
							}
							else
							{
								mesh = strandGroupInstance.groupAssetReference.Resolve().meshAssetStrips;
#if !UNITY_2021_2_OR_NEWER
								mesh = HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceStrips, mesh, HideFlags.HideAndDontSave);
#endif
							}
						}
						break;

					case HairSim.SettingsRendering.Renderer.BuiltinTubes:
						{
							if (subdivision > 0)
							{
								mesh = HairInstanceBuilder.CreateRenderMeshTubesIfNull(ref meshInstanceTubes, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.constants._StrandCount, (int)solverData.constants._StagingStrandVertexCount, new Bounds());
							}
							else
							{
								mesh = strandGroupInstance.groupAssetReference.Resolve().meshAssetTubes;
#if !UNITY_2021_2_OR_NEWER
								mesh = HairInstanceBuilder.CreateMeshInstanceIfNull(ref meshInstanceTubes, mesh, HideFlags.HideAndDontSave);
#endif
							}
						}
						break;
				}
			}

			// update mesh filter
			ref var meshFilter = ref strandGroupInstance.sceneObjects.strandMeshFilter;
			{
				if (meshFilter.sharedMesh != mesh)
					meshFilter.sharedMesh = mesh;
			}

			// update material instance
			ref var materialInstance = ref strandGroupInstance.sceneObjects.materialInstance;
			{
				var materialAsset = GetMaterial(strandGroupInstance);
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
				}

				if (materialInstance != null)
				{
					UpdateMaterialState(materialInstance, settingsRendering, solverData, volumeData, mesh);

#if UNITY_EDITOR
					var materialInstancePendingPasses = HairMaterialUtility.TryCompileCountPassesPending(materialInstance);
					if (materialInstancePendingPasses > 0)
					{
						materialInstance.shader = HairMaterialUtility.GetReplacementShader(HairMaterialUtility.ReplacementType.Async);
						UpdateMaterialState(materialInstance, settingsRendering, solverData, volumeData, mesh);
					}
#endif
				}
			}

			// update mesh renderer
			ref var meshRenderer = ref strandGroupInstance.sceneObjects.strandMeshRenderer;
			{
				meshRenderer.enabled = (settingsRendering.renderer != HairSim.SettingsRendering.Renderer.Disabled);
				meshRenderer.sharedMaterial = materialInstance;
				meshRenderer.shadowCastingMode = settingsRendering.rendererShadows;
				meshRenderer.renderingLayerMask = (uint)settingsRendering.rendererLayers;
				meshRenderer.motionVectorGenerationMode = settingsRendering.motionVectors;

				if (meshRenderer.rayTracingMode != UnityEngine.Experimental.Rendering.RayTracingMode.Off && SystemInfo.supportsRayTracing)
					meshRenderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;

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

					meshRendererHDRP.enabled = true;
					meshRendererHDRP.rendererGroup = settingsSystem.strandRendererGroup;
					meshRendererHDRP.enableHighQualityLineRendering = (settingsSystem.strandRenderer == SettingsSystem.StrandRenderer.HDRPHighQualityLines);
				}
#endif
			}

			// update renderer bounds
			{
#if UNITY_2021_2_OR_NEWER
				// starting with 2021.2 we can override renderer bounds directly
				meshRenderer.localBounds = HairSim.GetSolverBounds(solverData, volumeData).WithTransform(meshFilter.transform.worldToLocalMatrix);

				//TODO the world space bounds override is failing in some cases -- figure out why?
				//meshRenderer.bounds = HairSim.GetSolverBounds(solverData, volumeData);
#else
				// prior to 2021.2 it was only possible to set renderer bounds indirectly via mesh bounds
				if (mesh != null)
					mesh.bounds = HairSim.GetSolverBounds(solverData, volumeData).WithTransform(meshFilter.transform.worldToLocalMatrix);
#endif
			}
		}

		static void UpdateMaterialState(Material materialInstance, in HairSim.SettingsRendering settingsRendering, in HairSim.SolverData solverData, in HairSim.VolumeData volumeData, Mesh mesh)
		{
			HairSim.BindSolverData(materialInstance, solverData);
			HairSim.BindVolumeData(materialInstance, volumeData);

			switch (settingsRendering.renderer)
			{
				case HairSim.SettingsRendering.Renderer.BuiltinTubes:
					materialInstance.SetInt("_DecodeVertexCount", 4);
					materialInstance.SetInt("_DecodeVertexWidth", 2);
					break;

				case HairSim.SettingsRendering.Renderer.BuiltinStrips:
					materialInstance.SetInt("_DecodeVertexCount", 2);
					materialInstance.SetInt("_DecodeVertexWidth", 1);
					break;

				default:
					materialInstance.SetInt("_DecodeVertexCount", 1);
					materialInstance.SetInt("_DecodeVertexWidth", 0);
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
				case SettingsExecutive.UpdateRate.CustomTimeStep: return settingsExecutive.updateTimeStep;
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

				UpdateSystemState(cmd, cmdFlags, stepDesc);
				UpdateSceneState();

				//if (Time.renderedFrameCount % 60 == 0)
				//{
				//	Debug.Log("t " + (Time.renderedFrameCount / 60) + " bounds-coverage: " + volumeData.buffersReadback._BoundsCoverage.GetData<Vector2>()[0]);
				//}

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

			//Debug.Log("(in init) bounds: " + HairSim.GetVolumeBounds(volumeData) + " --------------------------------------------------------------------");

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
							solverConstants._GroupBoundsIndex = (uint)i;

							HairSimUtility.PushConstantBufferData(cmd, solverData[i].buffers.SolverCBuffer, solverConstants);
						}

						solverData[i].memoryLayout = groupAsset.particleMemoryLayout;

						solverData[i].initialSumStrandLength = groupAsset.sumStrandLength;
						solverData[i].initialMaxStrandLength = groupAsset.maxStrandLength;
						solverData[i].initialMaxStrandDiameter = groupAsset.maxStrandDiameter;
						solverData[i].initialAvgStrandDiameter = groupAsset.avgStrandDiameter;

						solverData[i].lodThreshold = new NativeArray<float>(groupAsset.lodThreshold, Allocator.Persistent);
					}

					using (var uploadCtx = new HairSimUtility.BufferUploadContext(cmd, cmdFlags))
					{
						ref readonly var solverBuffers = ref solverData[i].buffers;

						uploadCtx.SetData(solverBuffers._RootUV, groupAsset.rootUV);
						uploadCtx.SetData(solverBuffers._RootScale, groupAsset.rootScale);

						unsafe
						{
							using (var particlePositionAligned = new NativeArray<Vector4>(groupAsset.strandCount * groupAsset.strandParticleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory))
							{
								fixed (void* groupAssetParticlePositionPtr = groupAsset.particlePosition)
								{
									UnsafeUtility.MemCpyStride(particlePositionAligned.GetUnsafePtr(), sizeof(Vector4), groupAssetParticlePositionPtr, sizeof(Vector3), sizeof(Vector3), groupAsset.strandCount * groupAsset.strandParticleCount);
								}

								uploadCtx.SetData(solverBuffers._ParticlePosition, particlePositionAligned);
							}

							// optional particle features
							//TODO defer allocation and upload to staging (let render settings decide if material should be able to access)
							{
								var particleCount = (groupAsset.strandCount * groupAsset.strandParticleCount);
								var particleTexCoordAvailable = groupAsset.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.TexCoord);
								var particleDiameterAvailable = groupAsset.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.Diameter);

								HairSimUtility.CreateBuffer(ref solverData[i].buffers._ParticleExtTexCoord, "ParticleExtTexCoord", particleTexCoordAvailable ? particleCount : 1, sizeof(Vector2));
								HairSimUtility.CreateBuffer(ref solverData[i].buffers._ParticleExtDiameter, "ParticleExtDiameter", particleDiameterAvailable ? particleCount : 1, sizeof(Vector2));

								if (particleTexCoordAvailable)
									uploadCtx.SetData(solverBuffers._ParticleExtTexCoord, groupAsset.particleTexCoord);
								if (particleDiameterAvailable)
									uploadCtx.SetData(solverBuffers._ParticleExtDiameter, groupAsset.particleDiameter);
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

					HairSim.PushSolverRoots(cmd, cmdFlags, ref solverData[i], rootMesh, rootMeshMatrix, rootMeshSkinningRotation, 1.0f);
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

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.PushSolverLOD(cmd, ref solverData[i], GetSettingsPhysics(strandGroupInstances[i]), GetSettingsRendering(strandGroupInstances[i]), volumeData);
				}

				HairSim.PushVolumeLOD(cmd, ref volumeData, settingsVolumetrics);
				HairSim.PushVolumeSettings(cmd, ref volumeData, settingsVolumetrics, 1.0f);
				HairSim.PushVolumeStep(cmd, cmdFlags, ref volumeData, settingsVolumetrics, solverData, 1.0f);

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
					ref readonly var strandGroupInstance = ref strandGroupInstances[i];

					CoreUtils.Destroy(strandGroupInstance.sceneObjects.materialInstance);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceLines);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceStrips);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceTubes);
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

		public void ResetSimulationState()
		{
			ReleaseRuntimeData();
		}
	}
}
