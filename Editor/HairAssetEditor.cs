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

		HairTopologyDesc[] previewMeshes;
		HairSim.VolumeData previewDataShared;
		HairSim.SolverData[] previewData;
		string previewDataChecksum;
		int[] previewLOD;

		void OnEnable()
		{
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
				EditorGUILayout.LabelField("Strand Declaration", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawStrandDataDeclGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Strand Data", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawStrandDataGUI();
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
				if (hairAsset.settingsProcedural.placement == HairAsset.SettingsProcedural.PlacementMode.Mesh)
				{
					WarnIfNotAssigned(hairAsset.settingsProcedural.placementMesh, LabelName(nameof(HairAsset.SettingsProcedural.placementMesh)));
					WarnIfNotReadable(hairAsset.settingsProcedural.mappedDensity, LabelName(nameof(HairAsset.SettingsProcedural.mappedDensity)));
					WarnIfNotReadable(hairAsset.settingsProcedural.mappedDirection, LabelName(nameof(HairAsset.SettingsProcedural.mappedDirection)));
					WarnIfNotReadable(hairAsset.settingsProcedural.mappedParameters, LabelName(nameof(HairAsset.SettingsProcedural.mappedParameters)));
				}

				if (hairAsset.settingsProcedural.placement == HairAsset.SettingsProcedural.PlacementMode.Custom)
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

		public void DrawStrandDataDeclGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			var currentEv = Event.current.type;
			if (currentEv == EventType.MouseDown)
			{
				// prevent accidental changes due to hotControl not being reset if mouse was released during long progress bar
				if (EditorGUIUtility.hotControl != 0)
				{
					EditorGUIUtility.hotControl = 0;
					GUIUtility.keyboardControl = 0;
				}
			}

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
							if (placementType == HairAsset.SettingsProcedural.PlacementMode.Custom)
							{
								var placementProvider = hairAsset.settingsProcedural.placementProvider;
								if (placementProvider != null)
								{
									EditorGUILayout.Space();
									if (StructHeader("Settings Procedural Placement"))
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

		public void DrawStrandDataGUI()
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
									var boundsCenter = hairAsset.strandGroups[i].bounds.center;
									var boundsRadius = hairAsset.strandGroups[i].bounds.extents.magnitude;
									var boundsRadiusMag = boundsRadius * Mathf.Lerp(1.0f, 0.5f, previewZoom);

									var cameraDistanceFitted = boundsRadiusMag / Mathf.Sin(0.5f * Mathf.Deg2Rad * previewRenderer.cameraFieldOfView);
									var cameraTransform = previewRenderer.camera.transform;
									{
										cameraTransform.Rotate(drag.y, drag.x, 0.0f, Space.Self);
										cameraTransform.position = boundsCenter - cameraDistanceFitted * cameraTransform.forward;

										previewRenderer.camera.nearClipPlane = Mathf.Max(1e-7f, cameraDistanceFitted - boundsRadius);
										previewRenderer.camera.farClipPlane = Mathf.Max(1e-7f, cameraDistanceFitted + boundsRadius);
									}

									var sourceMaterial = HairMaterialUtility.GetCurrentPipelineDefault();
									if (sourceMaterial != null)
									{
										if (previewMaterial.shader != sourceMaterial.shader)
											previewMaterial.shader = sourceMaterial.shader;

										previewMaterial.CopyPropertiesFromMaterial(sourceMaterial);
										previewMaterial.EnableKeyword("HAIR_VERTEX_LIVE");
									}

									ref var previewBuffersShared = ref previewDataShared.buffers;
									{
										ref readonly var bufferIDs = ref HairSim.VolumeData.s_bufferIDs;
										previewMaterial.SetBuffer(bufferIDs._BoundsPrev, previewBuffersShared._Bounds);
										previewMaterial.SetBuffer(bufferIDs._Bounds, previewBuffersShared._Bounds);
									}

									ref var previewBuffers = ref previewData[i].buffers;
									unsafe
									{
										ref readonly var bufferIDs = ref HairSim.SolverData.s_bufferIDs;
										previewMaterial.SetConstantBuffer(bufferIDs.SolverCBuffer, previewBuffers.SolverCBuffer, 0, sizeof(HairSim.SolverCBuffer));
										previewMaterial.SetBuffer(bufferIDs._RootUV, previewBuffers._RootUV);
										previewMaterial.SetBuffer(bufferIDs._RootScale, previewBuffers._RootScale);
										previewMaterial.SetBuffer(bufferIDs._LODGuideCount, previewBuffers._LODGuideCount);
										previewMaterial.SetBuffer(bufferIDs._LODGuideIndex, previewBuffers._LODGuideIndex);
										previewMaterial.SetBuffer(bufferIDs._LODGuideCarry, previewBuffers._LODGuideCarry);
										previewMaterial.SetBuffer(bufferIDs._LODGuideReach, previewBuffers._LODGuideReach);
										previewMaterial.SetBuffer(bufferIDs._StagingVertex, previewBuffers._StagingVertex);
										previewMaterial.SetBuffer(bufferIDs._StagingVertexPrev, previewBuffers._StagingVertex);
										previewMaterial.SetBuffer(bufferIDs._SolverLODStage, previewBuffers._SolverLODStage);
										previewMaterial.SetBuffer(bufferIDs._SolverLODRange, previewBuffers._SolverLODRange);
									}

									previewMaterial.SetInt("_DecodeVertexCount", 1);
									previewMaterial.SetInt("_DecodeVertexWidth", 0);

									var mesh = HairTopologyCache.GetSharedMesh(previewMeshes[i]);
									{
										switch (mesh.GetVertexAttributeFormat(VertexAttribute.TexCoord0))
										{
											case VertexAttributeFormat.UNorm16:
												previewMaterial.SetInt("_DecodeVertexComponentValue", ushort.MaxValue);
												previewMaterial.SetInt("_DecodeVertexComponentWidth", 16);
												break;

											case VertexAttributeFormat.UNorm8:
												previewMaterial.SetInt("_DecodeVertexComponentValue", byte.MaxValue);
												previewMaterial.SetInt("_DecodeVertexComponentWidth", 8);
												break;
										}
									}

									previewRenderer.BeginPreview(rect, GUIStyle.none);
									previewRenderer.DrawMesh(mesh, Matrix4x4.identity, previewMaterial, subMeshIndex: 0);
									previewRenderer.Render(true, true);
									previewRenderer.EndAndDrawPreview(rect);
								}
							}
							EditorGUILayout.EndVertical();

							using (new EditorGUI.DisabledScope(hairAsset.strandGroups[i].lodCount < 2))
							{
								var selectedLOD = EditorGUILayout.IntSlider("Preview LOD", previewLOD[i], 0, hairAsset.strandGroups[i].lodCount - 1);
								if (selectedLOD != previewLOD[i])
								{
									InitializePreviewLOD(i, selectedLOD);
								}
							}

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
			HairSim.ReleaseVolumeData(ref previewDataShared);

			if (previewData != null)
			{
				for (int i = 0; i != previewData.Length; i++)
				{
					HairSim.ReleaseSolverData(ref previewData[i]);
				}
			}

			previewData = null;
			previewDataChecksum = string.Empty;
			previewMeshes = null;
			previewLOD = null;

			EditorApplication.update -= PingPreviewMeshes;
		}

		void InitializePreviewData(HairAsset hairAsset)
		{
			ReleasePreviewData();

			if (hairAsset == null)
				return;

			var strandGroups = hairAsset.strandGroups;
			if (strandGroups == null)
				return;

			previewMeshes = new HairTopologyDesc[strandGroups.Length];
			previewData = new HairSim.SolverData[strandGroups.Length];
			previewLOD = new int[strandGroups.Length];

			using (var cmd = HairSimUtility.ScopedCommandBuffer.Get())
			{
				using (var dummyBounds = new NativeArray<HairSim.LODBounds>(1, Allocator.Temp))
				{
					CreateBufferWithNativeData(ref previewDataShared.buffers._Bounds, dummyBounds);
				}

				for (int i = 0; i != previewData.Length; i++)
				{
					ref var strandGroup = ref strandGroups[i];

					ref var previewMesh = ref previewMeshes[i];
					{
						previewMesh = new HairTopologyDesc
						{
							type = HairTopologyType.Lines,
							strandCount = hairAsset.strandGroups[i].strandCount,
							strandParticleCount = hairAsset.strandGroups[i].strandParticleCount,
							memoryLayout = hairAsset.strandGroups[i].particleMemoryLayout,
						};
					}

					ref var previewBuffers = ref previewData[i].buffers;
					ref var previewConstants = ref previewData[i].constants;
					{
						HairAssetUtility.DeclareParticleStride(strandGroup, out var strandParticleOffset, out var strandParticleStride);

						previewConstants._StrandCount = (uint)strandGroup.strandCount;
						previewConstants._StrandParticleCount = (uint)strandGroup.strandParticleCount;
						previewConstants._StrandParticleOffset = (uint)strandParticleOffset;
						previewConstants._StrandParticleStride = (uint)strandParticleStride;
						previewConstants._LODCount = (uint)strandGroup.lodCount;

						previewConstants._GroupScale = 1.0f;
						previewConstants._GroupMaxParticleInterval = strandGroup.maxStrandLength / (previewConstants._StrandParticleCount - 1);
						previewConstants._GroupMaxParticleDiameter = strandGroup.maxStrandDiameter;
						previewConstants._GroupBoundsIndex = 0;

						previewConstants._StagingSubdivision = 0;
						previewConstants._StagingVertexFormat = (uint)HairSim.StagingVertexFormat.Uncompressed;
						previewConstants._StagingVertexStride = sizeof(float) * 3;

						previewConstants._StagingStrandVertexCount = previewConstants._StrandParticleCount;
						previewConstants._StagingStrandVertexOffset = previewConstants._StrandParticleOffset;

						previewConstants._RenderLODMethod = (uint)HairSim.RenderLODSelection.Manual;
						previewConstants._RenderLODScale = 1.0f;
						previewConstants._RenderLODBias = 0.0f;
						previewConstants._RenderLODClipThreshold = 0.05f;//TODO remove this once HairVertex implements hard upper bound
					}

					unsafe
					{
						HairSimUtility.CreateBuffer(ref previewBuffers.SolverCBuffer, string.Empty, 1, sizeof(HairSim.SolverCBuffer), ComputeBufferType.Constant);
						HairSimUtility.PushConstantBufferData(cmd, previewBuffers.SolverCBuffer, previewConstants);
					}

					CreateBufferWithData(ref previewBuffers._RootUV, strandGroup.rootUV);
					CreateBufferWithData(ref previewBuffers._RootScale, strandGroup.rootScale);
					CreateBufferWithData(ref previewBuffers._LODGuideCount, strandGroup.lodGuideCount);
					CreateBufferWithData(ref previewBuffers._LODGuideIndex, strandGroup.lodGuideIndex);
					CreateBufferWithData(ref previewBuffers._LODGuideCarry, strandGroup.lodGuideCarry);
					CreateBufferWithData(ref previewBuffers._LODGuideReach, strandGroup.lodGuideReach);
					CreateBufferWithData(ref previewBuffers._StagingVertex, strandGroup.particlePosition, ComputeBufferType.Raw);

					InitializePreviewLOD(i, strandGroup.lodCount - 1);
				}

				Graphics.ExecuteCommandBuffer(cmd);
			}

			previewDataChecksum = hairAsset.checksum;

			EditorApplication.update += PingPreviewMeshes;
		}

		void InitializePreviewLOD(int i, int lodIndex)
		{
			using (var lodDescs = new NativeArray<HairSim.LODIndices>((int)HairSim.SolverLODStage.__COUNT, Allocator.Temp))
			using (var lodRange = new NativeArray<Vector2Int>((int)HairSim.SolverLODRange.__COUNT, Allocator.Temp))
			{
				unsafe
				{
					static HairSim.LODIndices MakeLODDesc(int lodIndex) => new HairSim.LODIndices
					{
						lodIndexLo = (uint)lodIndex,
						lodIndexHi = (uint)lodIndex,
						lodBlendFrac = 0.0f,
					};

					var lodDescsPtr = (HairSim.LODIndices*)lodDescs.GetUnsafePtr();
					var lodRangePtr = (Vector2Int*)lodRange.GetUnsafePtr();

					var lodIndexCeil = Mathf.Max(0, (int)previewData[i].constants._LODCount - 1);
					var lodIndexDesc = MakeLODDesc(Mathf.Clamp(lodIndex, 0, lodIndexCeil));

					lodDescsPtr[(int)HairSim.SolverLODStage.Physics] = lodIndexDesc;
					lodDescsPtr[(int)HairSim.SolverLODStage.Rendering] = lodIndexDesc;
					lodRangePtr[(int)HairSim.SolverLODRange.Render] = new Vector2Int(0, (int)previewData[i].constants._StrandCount);

					CreateBufferWithNativeData(ref previewData[i].buffers._SolverLODStage, lodDescs);
					CreateBufferWithNativeData(ref previewData[i].buffers._SolverLODRange, lodRange);
				}
			}

			previewLOD[i] = lodIndex;
		}

		void PingPreviewMeshes()
		{
			foreach (var meshDesc in previewMeshes)
			{
				HairTopologyCache.GetSharedMesh(meshDesc);
			}
		}

		static void CreateBufferWithData<T>(ref ComputeBuffer buffer, T[] data, ComputeBufferType type = ComputeBufferType.Default) where T : unmanaged
		{
			unsafe
			{
				var elementCount = data.Length;
				var elementStride = UnsafeUtility.SizeOf<T>();

				HairSimUtility.CreateBuffer(ref buffer, string.Empty, elementCount, elementStride, type);

				buffer.SetData(data);
			}
		}

		static void CreateBufferWithNativeData<T>(ref ComputeBuffer buffer, NativeArray<T> data, ComputeBufferType type = ComputeBufferType.Default) where T : unmanaged
		{
			unsafe
			{
				var elementCount = data.Length;
				var elementStride = sizeof(T);

				HairSimUtility.CreateBuffer(ref buffer, string.Empty, elementCount, elementStride, type);

				buffer.SetData(data);
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
