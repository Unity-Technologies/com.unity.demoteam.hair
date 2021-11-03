using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if HAS_PACKAGE_UNITY_ALEMBIC
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	using static HairGUILayout;

	[CustomEditor(typeof(HairAsset))]
	public class HairAssetEditor : Editor
	{
		Editor rootGeneratorEditor;

		SerializedProperty _settingsBasic;
		SerializedProperty _settingsBasic_type;
		SerializedProperty _settingsBasic_kLODClusters;
		SerializedProperty _settingsBasic_kLODClustersProvider;
		SerializedProperty _settingsBasic_kLODClustersHighLOD;
		SerializedProperty _settingsAlembic;
		SerializedProperty _settingsProcedural;
		SerializedProperty _settingsProcedural_placement;
		SerializedProperty _settingsLODGenerated;
		SerializedProperty _settingsLODUVMapped;
		SerializedProperty _settingsLODPyramid;

		SerializedProperty _strandGroups;
		SerializedProperty _strandGroupsAutoBuild;

		PreviewRenderUtility previewRenderer;

		Material previewMaterial;
		Vector2 previewAngle;
		float previewZoom;

		HairSim.SolverData[] previewData;
		string previewDataChecksum;

		void OnEnable()
		{
			previewMaterial = new Material(HairMaterialUtility.GetCurrentPipelineDefault());
			previewAngle = Vector2.zero;
			previewZoom = 0.0f;

			previewRenderer = new PreviewRenderUtility();
			previewRenderer.camera.backgroundColor = Color.black;// Color.Lerp(Color.black, Color.grey, 0.5f);
			previewRenderer.camera.nearClipPlane = 0.001f;
			previewRenderer.camera.farClipPlane = 50.0f;
			previewRenderer.camera.fieldOfView = 50.0f;
			previewRenderer.camera.transform.position = Vector3.zero;
			previewRenderer.camera.transform.LookAt(Vector3.forward, Vector3.up);

			previewRenderer.lights[0].transform.SetParent(previewRenderer.camera.transform, worldPositionStays: false);
			previewRenderer.lights[0].transform.localPosition = Vector3.up;
			previewRenderer.lights[0].intensity = 1.0f;

			for (int i = 1; i != previewRenderer.lights.Length; i++)
			{
				previewRenderer.lights[i].enabled = false;
			}

			_settingsBasic = serializedObject.FindProperty(nameof(HairAsset.settingsBasic));
			_settingsBasic_type = _settingsBasic.FindPropertyRelative(nameof(HairAsset.settingsBasic.type));
			_settingsBasic_kLODClusters = _settingsBasic.FindPropertyRelative(nameof(HairAsset.settingsBasic.kLODClusters));
			_settingsBasic_kLODClustersProvider = _settingsBasic.FindPropertyRelative(nameof(HairAsset.settingsBasic.kLODClustersProvider));
			_settingsBasic_kLODClustersHighLOD = _settingsBasic.FindPropertyRelative(nameof(HairAsset.settingsBasic.kLODClustersHighLOD));
			_settingsAlembic = serializedObject.FindProperty(nameof(HairAsset.settingsAlembic));
			_settingsProcedural = serializedObject.FindProperty(nameof(HairAsset.settingsProcedural));
			_settingsProcedural_placement = _settingsProcedural.FindPropertyRelative(nameof(HairAsset.settingsProcedural.placement));
			_settingsLODGenerated = serializedObject.FindProperty(nameof(HairAsset.settingsLODGenerated));
			_settingsLODUVMapped = serializedObject.FindProperty(nameof(HairAsset.settingsLODUVMapped));
			_settingsLODPyramid = serializedObject.FindProperty(nameof(HairAsset.settingsLODPyramid));

			_strandGroups = serializedObject.FindProperty(nameof(HairAsset.strandGroups));
			_strandGroupsAutoBuild = serializedObject.FindProperty(nameof(HairAsset.strandGroupsAutoBuild));
		}

		void OnDisable()
		{
			if (rootGeneratorEditor)
			{
				DestroyImmediate(rootGeneratorEditor);
			}

			ReleasePreviewData();

			if (previewRenderer != null)
			{
				previewRenderer.Cleanup();
				previewRenderer = null;
			}

			if (previewMaterial != null)
			{
				Material.DestroyImmediate(previewMaterial);
				previewMaterial = null;
			}
		}

		public override bool UseDefaultMargins()
		{
			return false;
		}

		public override void OnInspectorGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			EditorGUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
			{
				EditorGUILayout.LabelField("Importer", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawImporterGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Strand Groups", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawStrandGroupsGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField(hairAsset.checksum, EditorStyles.centeredGreyMiniLabel);
			}
			EditorGUILayout.EndVertical();
		}

		static string LabelName(string variableName)
		{
			return ObjectNames.NicifyVariableName(variableName);
		}

		static void WarnIfNotAssigned(Object asset, string label)
		{
			if (asset == null)
			{
				EditorGUILayout.HelpBox(string.Format("Configuration error: '{0}' is required and not assigned.", label), MessageType.Error, wide: true);
			}
		}

		static void WarnIfNotReadable(Texture2D texture, string label)
		{
			if (texture != null && texture.isReadable == false)
			{
				EditorGUILayout.HelpBox(string.Format("Configuration warning: '{0}' will be ignored since the assigned texture asset is not marked 'Read/Write'.", label), MessageType.Warning, wide: true);
			}
		}

		static StructValidation ValidationGUIAlembic(object userData)
		{
#if HAS_PACKAGE_UNITY_ALEMBIC
			var hairAsset = userData as HairAsset;
			if (hairAsset != null)
			{
				var alembicAsset = hairAsset.settingsAlembic.alembicAsset;
				if (alembicAsset != null && alembicAsset.GetComponentInChildren<AlembicCurves>(includeInactive: true) == null)
				{
					EditorGUILayout.HelpBox("Configuration warning: Unable to locate curves in the assigned alembic asset.", MessageType.Warning, wide: true);
				}

				if (hairAsset.settingsAlembic.rootUV == HairAsset.SettingsAlembic.RootUV.ResolveFromMesh)
				{
					WarnIfNotAssigned(hairAsset.settingsAlembic.rootUVMesh, LabelName(nameof(hairAsset.settingsAlembic.rootUVMesh)));
				}
			}
			return StructValidation.Pass;
#else
			EditorGUILayout.HelpBox("Alembic settings require package 'com.unity.formats.alembic' >= 2.2.2", MessageType.Warning, wide: true);
			return StructValidation.Inaccessible;
#endif
		}

		static StructValidation ValidationGUIProcedural(object userData)
		{
			var hairAsset = userData as HairAsset;
			if (hairAsset.settingsProcedural.placement == HairAsset.SettingsProcedural.PlacementType.Mesh)
			{
				WarnIfNotAssigned(hairAsset.settingsProcedural.placementMesh, LabelName(nameof(hairAsset.settingsProcedural.placementMesh)));
				WarnIfNotReadable(hairAsset.settingsProcedural.mappedDensity, LabelName(nameof(hairAsset.settingsProcedural.mappedDensity)));
				WarnIfNotReadable(hairAsset.settingsProcedural.mappedDirection, LabelName(nameof(hairAsset.settingsProcedural.mappedDirection)));
				WarnIfNotReadable(hairAsset.settingsProcedural.mappedParameters, LabelName(nameof(hairAsset.settingsProcedural.mappedParameters)));
			}

			if (hairAsset.settingsProcedural.placement == HairAsset.SettingsProcedural.PlacementType.Custom)
			{
				WarnIfNotAssigned(hairAsset.settingsProcedural.placementProvider, LabelName(nameof(hairAsset.settingsProcedural.placementProvider)));
			}

			return StructValidation.Pass;
		}

		static StructValidation ValidationGUILODClusters(object userData)
		{
			var hairAsset = userData as HairAsset;
			if (hairAsset.settingsBasic.kLODClusters)
			{
				switch (hairAsset.settingsBasic.kLODClustersProvider)
				{
					case HairAsset.LODClusters.Generated:
						{
							EditorGUILayout.HelpBox("Configuration warning: Not yet implemented.", MessageType.Warning, wide: true);
							return StructValidation.Inaccessible;
						}

					case HairAsset.LODClusters.UVMapped:
						{
							var clusterMaps = hairAsset.settingsLODUVMapped.baseLODClusterMapChain;
							if (clusterMaps != null)
							{
								for (int i = 0; i != clusterMaps.Length; i++)
								{
									WarnIfNotReadable(clusterMaps[i], string.Format("{0}[{1}]", LabelName(nameof(hairAsset.settingsLODUVMapped.baseLODClusterMapChain)), i));
								}
							}
						}
						break;
				}

				if (hairAsset.settingsBasic.kLODClustersHighLOD)
				{
					if (hairAsset.settingsLODPyramid.highLODIntermediateLevels > 0)
					{
						EditorGUILayout.HelpBox("Configuration warning: Not yet implemented.", MessageType.Warning, wide: true);
					}
				}
			}

			return StructValidation.Pass;
		}

		public void DrawImporterGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				StructPropertyFieldsWithHeader(_settingsBasic);

				EditorGUILayout.Space();

				switch ((HairAsset.Type)_settingsBasic_type.enumValueIndex)
				{
					case HairAsset.Type.Alembic:
						StructPropertyFieldsWithHeader(_settingsAlembic, ValidationGUIAlembic, hairAsset);
						break;

					case HairAsset.Type.Procedural:
						StructPropertyFieldsWithHeader(_settingsProcedural, ValidationGUIProcedural, hairAsset);
						{
							var placementType = (HairAsset.SettingsProcedural.PlacementType)_settingsProcedural_placement.enumValueIndex;
							if (placementType == HairAsset.SettingsProcedural.PlacementType.Custom && hairAsset.settingsProcedural.placementProvider is HairAssetProvider)
							{
								Editor.CreateCachedEditor(hairAsset.settingsProcedural.placementProvider, editorType: null, ref rootGeneratorEditor);
								EditorGUILayout.Space();
								EditorGUILayout.LabelField("Settings Procedural Placement", EditorStyles.miniBoldLabel);
								using (new EditorGUI.IndentLevelScope())
								{
									rootGeneratorEditor.DrawDefaultInspector();
								}
							}
						}
						break;
				}

				if (_settingsBasic_kLODClusters.boolValue)
				{
					EditorGUILayout.Space();

					var clustersProvider = (HairAsset.LODClusters)_settingsBasic_kLODClustersProvider.enumValueIndex;
					var clustersSubdivided = _settingsBasic_kLODClustersHighLOD.boolValue;

					switch (clustersProvider)
					{
						case HairAsset.LODClusters.Generated:
							StructPropertyFieldsWithHeader(_settingsLODGenerated, "Settings Clusters Generated", ValidationGUILODClusters, hairAsset);
							break;

						case HairAsset.LODClusters.UVMapped:
							StructPropertyFieldsWithHeader(_settingsLODUVMapped, "Settings Clusters UV Mapped", ValidationGUILODClusters, hairAsset);
							break;
					}

					if (clustersSubdivided)
					{
						using (new EditorGUI.IndentLevelScope())
						{
							StructPropertyFields(_settingsLODPyramid);
						}
					}
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			{
				var buildNow = GUILayout.Button("Build strand groups");
				var buildAuto = EditorGUILayout.ToggleLeft("Auto", _strandGroupsAutoBuild.boolValue, GUILayout.Width(50.0f));

				if (_strandGroupsAutoBuild.boolValue != buildAuto)
					_strandGroupsAutoBuild.boolValue = buildAuto;

				var settingsChanged = EditorGUI.EndChangeCheck();
				if (settingsChanged)
				{
					serializedObject.ApplyModifiedPropertiesWithoutUndo();
				}

				if (buildNow || (buildAuto && settingsChanged))
				{
					HairAssetBuilder.BuildHairAsset(hairAsset);
					serializedObject.Update();
				}
			}
			EditorGUILayout.EndHorizontal();
		}

		public void DrawStrandGroupsGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			if (hairAsset.strandGroups == null || hairAsset.strandGroups.Length == 0)
			{
				EditorGUILayout.LabelField("None");
			}
			else
			{
				int numStrands = 0;
				int numParticles = 0;

				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					numStrands += hairAsset.strandGroups[i].strandCount;
					numParticles += hairAsset.strandGroups[i].strandCount * hairAsset.strandGroups[i].strandParticleCount;
				}

				if (previewDataChecksum != hairAsset.checksum)
				{
					InitializePreviewData(hairAsset);
				}

				EditorGUILayout.LabelField("Summary", EditorStyles.miniBoldLabel);
				using (new EditorGUI.IndentLevelScope())
				using (new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.IntField("Number of groups", hairAsset.strandGroups.Length);
					EditorGUILayout.IntField("Number of strands (total)", numStrands);
					EditorGUILayout.IntField("Number of particles (total)", numParticles);
				}

				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Group:" + i, EditorStyles.miniBoldLabel);
					using (new EditorGUI.IndentLevelScope())
					{
						EditorGUILayout.BeginVertical();
						{
							var rect = GUILayoutUtility.GetAspectRect(16.0f / 9.0f, EditorStyles.helpBox, GUILayout.MaxHeight(400.0f));
							if (rect.width >= 20.0f)
							{
								rect = EditorGUI.IndentedRect(rect);

								GUI.Box(rect, Texture2D.blackTexture, EditorStyles.textField);
								{
									rect.xMin += 1;
									rect.yMin += 1;
									rect.xMax -= 1;
									rect.yMax -= 1;
								}

								// apply zoom
								var e = Event.current;
								if (e.alt && e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
								{
									previewZoom -= 0.05f * e.delta.y;
									previewZoom = Mathf.Clamp01(previewZoom);
									e.Use();
								}

								// apply rotation
								var drag = Vector2.zero;
								if (Drag2D(ref drag, rect))
								{
									drag *= Vector2.one;
								}

								// draw preview
								var meshLines = hairAsset.strandGroups[i].meshAssetLines;
								var meshCenter = hairAsset.strandGroups[i].bounds.center;
								var meshRadius = hairAsset.strandGroups[i].bounds.extents.magnitude * Mathf.Lerp(1.0f, 0.5f, previewZoom);

								var cameraDistance = meshRadius / Mathf.Sin(0.5f * Mathf.Deg2Rad * previewRenderer.cameraFieldOfView);
								var cameraTransform = previewRenderer.camera.transform;
								{
									cameraTransform.Rotate(drag.y, drag.x, 0.0f, Space.Self);
									cameraTransform.position = meshCenter - cameraDistance * cameraTransform.forward;
								}

								var sourceMaterial = HairMaterialUtility.GetCurrentPipelineDefault();
								if (sourceMaterial != null)
								{
									if (previewMaterial.shader != sourceMaterial.shader)
										previewMaterial.shader = sourceMaterial.shader;

									previewMaterial.CopyPropertiesFromMaterial(sourceMaterial);
								}

								HairSim.BindSolverData(previewMaterial, previewData[i]);

								CoreUtils.SetKeyword(previewMaterial, "HAIR_VERTEX_ID_LINES", true);
								CoreUtils.SetKeyword(previewMaterial, "HAIR_VERTEX_ID_STRIPS", false);
								CoreUtils.SetKeyword(previewMaterial, "HAIR_VERTEX_SRC_SOLVER", true);
								CoreUtils.SetKeyword(previewMaterial, "HAIR_VERTEX_SRC_STAGING", false);

								previewRenderer.BeginPreview(rect, GUIStyle.none);
								previewRenderer.DrawMesh(meshLines, Matrix4x4.identity, previewMaterial, subMeshIndex: 0);
								previewRenderer.Render(true, true);
								previewRenderer.EndAndDrawPreview(rect);
							}
						}
						EditorGUILayout.EndVertical();

						using (new EditorGUI.DisabledScope(true))
						{
							StructPropertyFields(_strandGroups.GetArrayElementAtIndex(i));
						}
					}
				}

				EditorGUILayout.Space();
				using (new EditorGUI.DisabledScope(true))
				{
					if (GUILayout.Button("Save changes"))
					{
						//TODO
					}
				}
			}
		}

		void ReleasePreviewData()
		{
			if (previewData != null)
			{
				for (int i = 0; i != previewData.Length; i++)
				{
					HairSim.ReleaseSolverData(ref previewData[i]);
				}
			}

			previewData = null;
			previewDataChecksum = string.Empty;
		}

		void InitializePreviewData(HairAsset hairAsset)
		{
			ReleasePreviewData();

			if (hairAsset == null)
				return;

			var strandGroups = hairAsset.strandGroups;
			if (strandGroups == null)
				return;

			unsafe
			{
				previewData = new HairSim.SolverData[strandGroups.Length];

				for (int i = 0; i != previewData.Length; i++)
				{
					ref var strandGroup = ref strandGroups[i];

					HairSim.PrepareSolverData(ref previewData[i], strandGroup.strandCount, strandGroup.strandParticleCount, strandGroup.lodCount);
					{
						previewData[i].memoryLayout = strandGroup.particleMemoryLayout;
						previewData[i].cbuffer._StrandCount = (uint)strandGroup.strandCount;
						previewData[i].cbuffer._StrandParticleCount = (uint)strandGroup.strandParticleCount;
					}

					using (var stagingData = new NativeArray<Vector4>(strandGroup.particlePosition.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
					{
						fixed (void* sourcePtr = strandGroup.particlePosition)
						{
							UnsafeUtility.MemCpyStride(stagingData.GetUnsafePtr(), sizeof(Vector4), sourcePtr, sizeof(Vector3), sizeof(Vector3), stagingData.Length);
						}
						previewData[i].particlePosition.SetData(stagingData);
					}
				}

				var cmd = CommandBufferPool.Get();
				{
					for (int i = 0; i != previewData.Length; i++)
					{
						HairSim.PushSolverParams(cmd, ref previewData[i], HairSim.SolverSettings.defaults, Matrix4x4.identity, Quaternion.identity, 1.0f, 1.0f, 1.0f);
					}

					Graphics.ExecuteCommandBuffer(cmd);
				}
				CommandBufferPool.Release(cmd);

				previewDataChecksum = hairAsset.checksum;
			}
		}

		static bool Drag2D(ref Vector2 delta, Rect rect)
		{
			int i = GUIUtility.GetControlID("HairAssetEditor.Drag2D".GetHashCode(), FocusType.Passive);
			var e = Event.current;

			switch (e.GetTypeForControl(i))
			{
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == i)
					{
						delta += e.delta;
						e.Use();
						return true;// dragging
					}
					break;

				case EventType.MouseDown:
					if (rect.Contains(e.mousePosition))
					{
						if (GUIUtility.hotControl == 0)
							GUIUtility.hotControl = i;

						if (GUIUtility.hotControl == i)
						{
							e.Use();
							EditorGUIUtility.SetWantsMouseJumping(1);
							return true;// dragging
						}
					}
					break;

				case EventType.MouseUp:
					if (GUIUtility.hotControl == i)
						GUIUtility.hotControl = 0;

					EditorGUIUtility.SetWantsMouseJumping(0);
					break;
			}

			return false;// not dragging
		}
	}
}
