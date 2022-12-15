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
		Editor editorCustomData;
		Editor editorCustomPlacement;

		SerializedProperty _settingsBasic;
		SerializedProperty _settingsCustom;
		SerializedProperty _settingsCustom_settingsResolve;
		SerializedProperty _settingsAlembic;
		SerializedProperty _settingsAlembic_settingsResolve;
		SerializedProperty _settingsProcedural;
		SerializedProperty _settingsLODClusters;
		SerializedProperty _settingsLODClusters_baseLOD;
		SerializedProperty _settingsLODClusters_baseLODParamsGenerated;
		SerializedProperty _settingsLODClusters_baseLODParamsUVMapped;
		SerializedProperty _settingsLODClusters_highLOD;
		SerializedProperty _settingsLODClusters_highLODParamsAutomatic;
		SerializedProperty _settingsLODClusters_highLODParamsManual;

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
			previewRenderer.camera.clearFlags = CameraClearFlags.SolidColor;
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
			_settingsCustom = serializedObject.FindProperty(nameof(HairAsset.settingsCustom));
			_settingsCustom_settingsResolve = _settingsCustom.FindPropertyRelative(nameof(HairAsset.SettingsCustom.settingsResolve));
			_settingsAlembic = serializedObject.FindProperty(nameof(HairAsset.settingsAlembic));
			_settingsAlembic_settingsResolve = _settingsAlembic.FindPropertyRelative(nameof(HairAsset.SettingsAlembic.settingsResolve));
			_settingsProcedural = serializedObject.FindProperty(nameof(HairAsset.settingsProcedural));
			_settingsLODClusters = serializedObject.FindProperty(nameof(HairAsset.settingsLODClusters));
			_settingsLODClusters_baseLOD = _settingsLODClusters.FindPropertyRelative(nameof(HairAsset.SettingsLODClusters.baseLOD));
			_settingsLODClusters_baseLODParamsGenerated = _settingsLODClusters.FindPropertyRelative(nameof(HairAsset.SettingsLODClusters.baseLODParamsGenerated));
			_settingsLODClusters_baseLODParamsUVMapped = _settingsLODClusters.FindPropertyRelative(nameof(HairAsset.SettingsLODClusters.baseLODParamsUVMapped));
			_settingsLODClusters_highLOD = _settingsLODClusters.FindPropertyRelative(nameof(HairAsset.SettingsLODClusters.highLOD));
			_settingsLODClusters_highLODParamsAutomatic = _settingsLODClusters.FindPropertyRelative(nameof(HairAsset.SettingsLODClusters.highLODParamsAutomatic));
			_settingsLODClusters_highLODParamsManual = _settingsLODClusters.FindPropertyRelative(nameof(HairAsset.SettingsLODClusters.highLODParamsManual));

			_strandGroups = serializedObject.FindProperty(nameof(HairAsset.strandGroups));
			_strandGroupsAutoBuild = serializedObject.FindProperty(nameof(HairAsset.strandGroupsAutoBuild));
		}

		void OnDisable()
		{
			if (editorCustomData)
			{
				DestroyImmediate(editorCustomData);
			}

			if (editorCustomPlacement)
			{
				DestroyImmediate(editorCustomPlacement);
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

		static StructValidation ValidationGUIProcedural(object userData)
		{
			var hairAsset = userData as HairAsset;
			if (hairAsset != null)
			{
				if (hairAsset.settingsProcedural.placement == HairAsset.SettingsProcedural.PlacementType.Mesh)
				{
					WarnIfNotAssigned(hairAsset.settingsProcedural.placementMesh, LabelName(nameof(HairAsset.SettingsProcedural.placementMesh)));
					WarnIfNotReadable(hairAsset.settingsProcedural.mappedDensity, LabelName(nameof(HairAsset.SettingsProcedural.mappedDensity)));
					WarnIfNotReadable(hairAsset.settingsProcedural.mappedDirection, LabelName(nameof(HairAsset.SettingsProcedural.mappedDirection)));
					WarnIfNotReadable(hairAsset.settingsProcedural.mappedParameters, LabelName(nameof(HairAsset.SettingsProcedural.mappedParameters)));
				}

				if (hairAsset.settingsProcedural.placement == HairAsset.SettingsProcedural.PlacementType.Custom)
				{
					WarnIfNotAssigned(hairAsset.settingsProcedural.placementProvider, LabelName(nameof(HairAsset.SettingsProcedural.placementProvider)));
				}
			}
			return StructValidation.Pass;
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
				else
				{
					WarnIfNotAssigned(alembicAsset, LabelName(nameof(HairAsset.SettingsAlembic.alembicAsset)));
				}

				if (hairAsset.settingsAlembic.settingsResolve.rootUV == HairAsset.SettingsResolve.RootUV.ResolveFromMesh)
				{
					WarnIfNotAssigned(hairAsset.settingsAlembic.settingsResolve.rootUVMesh, LabelName(nameof(HairAsset.SettingsResolve.rootUVMesh)));
				}
			}
			return StructValidation.Pass;
#else
			EditorGUILayout.HelpBox("Alembic settings require package 'com.unity.formats.alembic' >= 2.2.2", MessageType.Warning, wide: true);
			return StructValidation.Inaccessible;
#endif
		}

		static StructValidation ValidationGUICustom(object userData)
		{
			var hairAsset = userData as HairAsset;
			if (hairAsset != null)
			{
				WarnIfNotAssigned(hairAsset.settingsCustom.dataProvider, LabelName(nameof(HairAsset.SettingsCustom.dataProvider)));

				if (hairAsset.settingsCustom.settingsResolve.rootUV == HairAsset.SettingsResolve.RootUV.ResolveFromMesh)
				{
					WarnIfNotAssigned(hairAsset.settingsCustom.settingsResolve.rootUVMesh, LabelName(nameof(HairAsset.SettingsResolve.rootUVMesh)));
				}
			}
			return StructValidation.Pass;
		}

		static StructValidation ValidationGUILODClusters(object userData)
		{
			var hairAsset = userData as HairAsset;
			if (hairAsset.settingsBasic.kLODClusters)
			{
				switch (hairAsset.settingsLODClusters.baseLOD.baseLOD)
				{
					case HairAsset.SettingsLODClusters.BaseLODMode.Generated:
						break;

					case HairAsset.SettingsLODClusters.BaseLODMode.UVMapped:
						{
							var clusterMaps = hairAsset.settingsLODClusters.baseLODParamsUVMapped.baseLODClusterMaps;
							if (clusterMaps != null)
							{
								for (int i = 0; i != clusterMaps.Length; i++)
								{
									WarnIfNotReadable(clusterMaps[i], string.Format("{0}[{1}]", LabelName(nameof(HairAsset.SettingsLODClusters.BaseLODParamsUVMapped.baseLODClusterMaps)), i));
								}
							}
						}
						break;
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

				switch (hairAsset.settingsBasic.type)
				{
					case HairAsset.Type.Procedural:
						{
							StructPropertyFieldsWithHeader(_settingsProcedural, ValidationGUIProcedural, hairAsset);

							var placementType = hairAsset.settingsProcedural.placement;
							if (placementType == HairAsset.SettingsProcedural.PlacementType.Custom)
							{
								var placementProvider = hairAsset.settingsProcedural.placementProvider;
								if (placementProvider != null)
								{
									EditorGUILayout.Space();
									if (StructHeader("Settings Custom Placement"))
									{
										using (new EditorGUI.IndentLevelScope())
										{
											CreateCachedEditor(placementProvider, editorType: null, ref editorCustomPlacement);
											editorCustomPlacement.OnInspectorGUI();
										}
									}
								}
							}
						}
						break;

					case HairAsset.Type.Alembic:
						{
							var alembicExpanded = StructPropertyFieldsWithHeader(_settingsAlembic, ValidationGUIAlembic, hairAsset);
							if (alembicExpanded)
							{
								using (new EditorGUI.IndentLevelScope())
								{
									StructPropertyFields(_settingsAlembic_settingsResolve);
								}
							}
						}
						break;

					case HairAsset.Type.Custom:
						{
							var customExpanded = StructPropertyFieldsWithHeader(_settingsCustom, ValidationGUICustom, hairAsset);
							if (customExpanded)
							{
								using (new EditorGUI.IndentLevelScope())
								{
									StructPropertyFields(_settingsCustom_settingsResolve);
								}
							}

							var dataProvider = hairAsset.settingsCustom.dataProvider;
							if (dataProvider != null)
							{
								EditorGUILayout.Space();
								if (StructHeader("Settings Custom Data"))
								{
									using (new EditorGUI.IndentLevelScope())
									{
										CreateCachedEditor(dataProvider, editorType: null, ref editorCustomData);
										editorCustomData.OnInspectorGUI();
									}
								}
							}
						}
						break;
				}

				if (hairAsset.settingsBasic.kLODClusters)
				{
					EditorGUILayout.Space();

					var lodClustersExpanded = StructPropertyFieldsWithHeader(_settingsLODClusters, ValidationGUILODClusters, hairAsset);
					if (lodClustersExpanded)
					{
						using (new EditorGUI.IndentLevelScope())
						{
							StructPropertyFields(_settingsLODClusters_baseLOD);

							switch (hairAsset.settingsLODClusters.baseLOD.baseLOD)
							{
								case HairAsset.SettingsLODClusters.BaseLODMode.Generated:
									StructPropertyFields(_settingsLODClusters_baseLODParamsGenerated);
									break;

								case HairAsset.SettingsLODClusters.BaseLODMode.UVMapped:
									StructPropertyFields(_settingsLODClusters_baseLODParamsUVMapped);
									break;
							}

							StructPropertyFields(_settingsLODClusters_highLOD);

							using (new EditorGUI.DisabledScope(hairAsset.settingsLODClusters.highLOD.highLOD == false))
							{
								switch (hairAsset.settingsLODClusters.highLOD.highLODMode)
								{
									case HairAsset.SettingsLODClusters.HighLODMode.Automatic:
										StructPropertyFields(_settingsLODClusters_highLODParamsAutomatic);
										break;

									case HairAsset.SettingsLODClusters.HighLODMode.Manual:
										StructPropertyFields(_settingsLODClusters_highLODParamsManual);
										break;
								}
							}
						}
					}
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			{
				EditorGUIUtility.labelWidth = GUI.skin.label.CalcSize(new GUIContent("Auto")).x;

				var buildNow = GUILayout.Button("Build strand groups"); GUILayout.Space(2.0f);
				var buildAuto = EditorGUILayout.ToggleLeft("Auto", _strandGroupsAutoBuild.boolValue, GUILayout.ExpandHeight(true), GUILayout.Width(EditorGUIUtility.labelWidth + 16.0f));

				EditorGUIUtility.labelWidth = 0;

				if (_strandGroupsAutoBuild.boolValue != buildAuto)
					_strandGroupsAutoBuild.boolValue = buildAuto;

				var settingsChanged = EditorGUI.EndChangeCheck();
				if (settingsChanged)
				{
					serializedObject.ApplyModifiedPropertiesWithoutUndo();
				}

				var buildNowAuto = buildAuto && settingsChanged;
				if (buildNowAuto || buildNow)
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

				if (StructHeader("Summary"))
				{
					using (new EditorGUI.IndentLevelScope())
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.IntField("Number of groups", hairAsset.strandGroups.Length);
						EditorGUILayout.IntField("Number of strands (total)", numStrands);
						EditorGUILayout.IntField("Number of particles (total)", numParticles);
					}
				}

				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					EditorGUILayout.Space();

					if (StructHeader("Group:" + i))
					{
						using (new EditorGUI.IndentLevelScope())
						{
							EditorGUILayout.Space(1.0f);
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
				}

				EditorGUILayout.Space();
				using (new EditorGUI.DisabledScope(true))
				{
					if (GUILayout.Button("Save changes"))
					{
						//TODO remove this?
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

						switch (previewData[i].memoryLayout)
						{
							case HairAsset.MemoryLayout.Interleaved:
								previewData[i].cbuffer._StrandParticleOffset = 1;
								previewData[i].cbuffer._StrandParticleStride = previewData[i].cbuffer._StrandCount;
								break;

							case HairAsset.MemoryLayout.Sequential:
								previewData[i].cbuffer._StrandParticleOffset = previewData[i].cbuffer._StrandParticleCount;
								previewData[i].cbuffer._StrandParticleStride = 1;
								break;
						}
					}

					using (var stagingData = new NativeArray<Vector4>(strandGroup.particlePosition.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
					{
						fixed (void* sourcePtr = strandGroup.particlePosition)
						{
							UnsafeUtility.MemCpyStride(stagingData.GetUnsafePtr(), sizeof(Vector4), sourcePtr, sizeof(Vector3), sizeof(Vector3), stagingData.Length);
						}

						previewData[i].particlePosition.SetData(stagingData);
					}

					previewData[i].lodGuideCount.SetData(strandGroup.lodGuideCount);
					previewData[i].lodGuideIndex.SetData(strandGroup.lodGuideIndex);
					previewData[i].lodGuideCarry.SetData(strandGroup.lodGuideCarry);
					previewData[i].cbuffer._LODCount = (uint)strandGroup.lodCount;
					previewData[i].cbuffer._LODIndexLo = previewData[i].cbuffer._LODCount - 1;
					previewData[i].cbuffer._LODIndexHi = previewData[i].cbuffer._LODCount - 1;
				}

				var cmd = CommandBufferPool.Get();
				{
					for (int i = 0; i != previewData.Length; i++)
					{
						HairSim.PushSolverParams(cmd, ref previewData[i], HairSim.SolverSettings.defaults, Matrix4x4.identity, Quaternion.identity, 1.0f, 1.0f, 0.0f, 1.0f);
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
