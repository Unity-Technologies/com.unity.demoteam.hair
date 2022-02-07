using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	using static HairGUILayout;
	using static HairGUIUtility;

	[CustomEditor(typeof(HairInstance)), CanEditMultipleObjects]
	public class HairInstanceEditor : Editor
	{
		static bool s_indicator;

		Dictionary<HairAsset, Editor> hairAssetEditorMap = new Dictionary<HairAsset, Editor>(1);

		SerializedProperty _strandGroupProviders;
		SerializedProperty _strandGroupSettings;
		SerializedProperty _strandGroupDefaults;

		SerializedProperty _settingsSystem;
		SerializedProperty _settingsVolume;
		SerializedProperty _settingsDebug;

		void OnEnable()
		{
			_strandGroupProviders = serializedObject.FindProperty(nameof(HairInstance.strandGroupProviders));
			_strandGroupSettings = serializedObject.FindProperty(nameof(HairInstance.strandGroupSettings));
			_strandGroupDefaults = serializedObject.FindProperty(nameof(HairInstance.strandGroupDefaults));

			_settingsSystem = serializedObject.FindProperty(nameof(HairInstance.settingsSystem));
			_settingsVolume = serializedObject.FindProperty(nameof(HairInstance.settingsVolume));
			_settingsDebug = serializedObject.FindProperty(nameof(HairInstance.settingsDebug));
		}

		void OnDisable()
		{
			foreach (var hairAssetEditor in hairAssetEditorMap.Values)
			{
				if (hairAssetEditor != null)
				{
					DestroyImmediate(hairAssetEditor);
				}
			}

			hairAssetEditorMap.Clear();
		}

		public bool HasFrameBounds()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null || hairInstance.strandGroupInstances == null)
				return false;
			else
				return true;
		}

		public Bounds OnGetFrameBounds()
		{
			return (target as HairInstance).GetSimulationBounds().WithScale(0.5f);
		}

		public override bool UseDefaultMargins()
		{
			return false;
		}

		public override void OnInspectorGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			EditorGUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
			{
				EditorGUILayout.LabelField("Instance", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawInstanceGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("System Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawSystemSettingsGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Strand Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawStrandSettingsGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Volume Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawVolumeSettingsGUI();
				}
				EditorGUILayout.EndVertical();

				//EditorGUILayout.Space();
				//EditorGUILayout.LabelField(hairInstance.strandGroupInstancesChecksum, EditorStyles.centeredGreyMiniLabel);
			}
			EditorGUILayout.EndVertical();
		}

		static StructValidation ValidationGUISystem(object userData)
		{
			return StructValidation.Pass;
		}

		static StructValidation ValidationGUISkinning(object userData)
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			return StructValidation.Pass;
#else
			EditorGUILayout.HelpBox("Root attachments require package: 'com.unity.demoteam.digital-human >= 0.1.1-preview'.", MessageType.None, wide: true);
#endif
		}

		static StructValidation ValidationGUIStrands(object userData)
		{
			//TODO make aware of multiple settings groups
			//
			//var hairInstance = userData as HairInstance;
			//{
			//	var material = hairInstance.GetStrandMaterial();
			//	if (material == null)
			//	{
			//		EditorGUILayout.HelpBox("Configuration warning: No active material.", MessageType.Info, wide: true);
			//	}
			//}

			return StructValidation.Pass;
		}

		static StructValidation ValidationGUISolver(object userData)
		{
			//TODO make aware of multiple settings groups
			//
			//var hairInstance = userData as HairInstance;
			//if (hairInstance.solverData != null && hairInstance.solverData.Length > 0)
			//{
			//	var strandSolver = hairInstance.strandGroupDefaults.settingsSolver.method;
			//	var strandMemoryLayout = hairInstance.solverData[0].memoryLayout;
			//	var strandParticleCount = hairInstance.solverData[0].cbuffer._StrandParticleCount;

			//	switch (strandSolver)
			//	{
			//		case HairSim.SolverSettings.Method.GaussSeidelReference:
			//			EditorGUILayout.HelpBox("Performance warning: Using slow reference solver.", MessageType.Warning, wide: true);
			//			break;

			//		case HairSim.SolverSettings.Method.GaussSeidel:
			//			if (strandMemoryLayout != HairAsset.MemoryLayout.Interleaved)
			//			{
			//				EditorGUILayout.HelpBox("Performance warning: Gauss-Seidel solver performs better with memory layout 'Interleaved'. This can be solved by changing memory layout in the asset.", MessageType.Warning, wide: true);
			//			}
			//			break;

			//		case HairSim.SolverSettings.Method.Jacobi:
			//			if (strandParticleCount != 16 &&
			//				strandParticleCount != 32 &&
			//				strandParticleCount != 64 &&
			//				strandParticleCount != 128)
			//			{
			//				EditorGUILayout.HelpBox("Configuration error: Jacobi solver requires strand particle count of 16, 32, 64, 128. Using slow reference solver as fallback. This can be solved by resampling curves in the asset.", MessageType.Error, wide: true);
			//			}
			//			else if (strandMemoryLayout != HairAsset.MemoryLayout.Sequential)
			//			{
			//				EditorGUILayout.HelpBox("Performance warning: Jacobi solver performs better with memory layout 'Sequential'. This can be solved by changing memory layout in the asset.", MessageType.Warning, wide: true);
			//			}
			//			break;
			//	}
			//}

			return StructValidation.Pass;
		}

		public void DrawInstanceGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			const float widthSymbol = 30.0f;
			const float widthUnlock = 70.0f;

			using (new GovernedByPrefabScope(hairInstance))
			{
				EditorGUI.BeginChangeCheck();
				{
					var multipleAssets = _strandGroupProviders.arraySize > 1;
					for (int i = 0; i != _strandGroupProviders.arraySize; i++)
					{
						var property = _strandGroupProviders.GetArrayElementAtIndex(i);
						var property_hairAsset = property.FindPropertyRelative("hairAsset");
						var property_hairAssetQuickEdit = property.FindPropertyRelative("hairAssetQuickEdit");
						var property_delete = false;

						EditorGUILayout.BeginHorizontal();
						{
							property_hairAssetQuickEdit.boolValue = GUILayout.Toggle(property_hairAssetQuickEdit.boolValue, ". . .", EditorStyles.miniButton, GUILayout.Width(widthSymbol));
							EditorGUILayout.ObjectField(property_hairAsset, GUIContent.none);

							if (multipleAssets)//TODO considering: using (new EditorGUI.DisabledScope(multipleAssets == false))
							{
								property_delete = GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(widthSymbol));
							}
						}
						EditorGUILayout.EndHorizontal();

						if (property_delete)
						{
							_strandGroupProviders.DeleteArrayElementAtIndex(i--);
							break;
						}

						if (property_hairAssetQuickEdit.boolValue)
						{
							var hairAsset = property_hairAsset.objectReferenceValue as HairAsset;
							if (hairAsset != null)
							{
								if (hairAssetEditorMap.TryGetValue(hairAsset, out var hairAssetEditor) == false)
								{
									CreateCachedEditor(hairAsset, typeof(HairAssetEditor), ref hairAssetEditor);
									hairAssetEditorMap.Add(hairAsset, hairAssetEditor);
								}
								EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
								{
									(hairAssetEditor as HairAssetEditor).DrawImporterGUI();
								}
								EditorGUILayout.EndVertical();
							}
						}
					}
				}

				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();
				}

				EditorGUILayout.BeginHorizontal();
				{
					if (GUILayout.Button("+", GUILayout.Width(widthSymbol)))
					{
						var countPrev = _strandGroupProviders.arraySize;
						var countNext = countPrev + 1;

						_strandGroupProviders.arraySize = countNext;
						serializedObject.ApplyModifiedProperties();

						if (countNext == hairInstance.strandGroupProviders.Length)
						{
							// write zero element to get rid of default duplicate of previous element
							hairInstance.strandGroupProviders[countNext - 1] = new HairInstance.GroupProvider();
						}
					}

					if (GUILayout.Button("Reload"))
					{
						hairInstance.strandGroupChecksums = null;
					}

					if (GUILayout.Button("Unlock", GUILayout.Width(widthUnlock)))
					{
						var strandGroupInstances = hairInstance.strandGroupInstances;
						if (strandGroupInstances != null)
						{
							for (int i = 0; i != strandGroupInstances.Length; i++)
							{
								ref var strandGroupInstance = ref strandGroupInstances[i];

								strandGroupInstance.sceneObjects.groupContainer.hideFlags &= ~HideFlags.NotEditable;
								strandGroupInstance.sceneObjects.rootMeshFilter.gameObject.hideFlags &= ~HideFlags.NotEditable;
								strandGroupInstance.sceneObjects.strandMeshFilter.gameObject.hideFlags &= ~HideFlags.NotEditable;
							}
						}
					}
				}
				EditorGUILayout.EndHorizontal();
			}
		}

		public void DrawSystemSettingsGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				if (StructPropertyFieldsWithHeader(_settingsSystem, ValidationGUISystem, hairInstance))
				{
					using (new EditorGUI.IndentLevelScope())
					{
						var rect = GUILayoutUtility.GetRect(0.0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
						{
							rect = EditorGUI.PrefixLabel(rect, new GUIContent("Steps Status"));
						}

						if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
						{
							s_indicator = !s_indicator;
						}

						if (s_indicator)
						{
							var stepsMin = hairInstance.settingsSystem.stepsMin ? hairInstance.settingsSystem.stepsMinValue : 0;
							var stepsMax = hairInstance.settingsSystem.stepsMax ? hairInstance.settingsSystem.stepsMaxValue : (int)Mathf.Ceil(hairInstance.stepsLastFrameSmooth);
							if (stepsMax == 0)
								stepsMax = 1;

							var stepDT = hairInstance.GetSimulationTimeStep();
							var stepCount = hairInstance.stepsLastFrameSmooth;

							var rectWidth = rect.width;
							var rectWidthStep = rectWidth / stepsMax;
							var rectWidthCount = Mathf.Min(rectWidth, rectWidthStep * stepCount);

							rect.width = rectWidthCount;
							{
								using (new ColorScope(hairInstance.stepsLastFrameSkipped > 0 ? Color.red : Color.green, ColorScope.Type.Color))
								{
									EditorGUI.HelpBox(rect, string.Empty, MessageType.None);
								}
							}

							rect.width = rectWidthStep;
							{
								var text = stepDT.ToString();
								for (int i = 0; i != stepsMax; i++)
								{
									EditorGUI.HelpBox(rect, text, MessageType.None);
									rect.x += rectWidthStep;
								}
							}

							EditorUtility.SetDirty(hairInstance);
						}
						else
						{
							EditorGUI.HelpBox(rect, "Click to toggle indicator.", MessageType.None);
						}
					}
				}
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		public void DrawStrandSettingsGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			const float widthSymbol = 30.0f;

			EditorGUI.BeginChangeCheck();
			{
				// settings default
				{
					var property = _strandGroupDefaults;
					var property_settingsSkinning = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSkinning));
					var property_settingsStrands = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsStrands));
					var property_settingsSolver = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSolver));

					EditorGUILayout.BeginHorizontal();
					{
						var expanded = GUILayout.Toggle(property.isExpanded, ". . .", EditorStyles.miniButton, GUILayout.Width(widthSymbol));
						if (expanded != property.isExpanded)
							property.isExpanded = expanded;

						EditorGUILayout.LabelField("Defaults (All Groups)", EditorStyles.textArea);
					}
					EditorGUILayout.EndHorizontal();

					if (property.isExpanded)
					{
						EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
						{
							using (new GovernedByPrefabScope(hairInstance))
							{
								StructPropertyFieldsWithHeader(property_settingsSkinning, ValidationGUISkinning, hairInstance);
							}
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsStrands, ValidationGUIStrands, hairInstance);
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsSolver, ValidationGUISolver, hairInstance);
						}
						EditorGUILayout.EndVertical();
					}
				}

				// settings override
				for (int i = 0; i != _strandGroupSettings.arraySize; i++)
				{
					var property = _strandGroupSettings.GetArrayElementAtIndex(i);
					var property_groupAssetReferences = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.groupAssetReferences));
					var property_settingsSkinning = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSkinning));
					var property_settingsStrands = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsStrands));
					var property_settingsSolver = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSolver));
					var property_settingsSkinningToggle = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSkinningToggle));
					var property_settingsStrandsToggle = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsStrandsToggle));
					var property_settingsSolverToggle = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSolverToggle));
					var property_delete = false;

					EditorGUILayout.BeginHorizontal();
					{
						var expanded = GUILayout.Toggle(property.isExpanded, ". . .", EditorStyles.miniButton, GUILayout.Width(widthSymbol));
						if (expanded != property.isExpanded)
							property.isExpanded = expanded;

						var assetCount = property_groupAssetReferences.arraySize;
						{
							EditorGUILayout.LabelField("Overrides (" + assetCount + (assetCount == 1 ? " Group Asset)" : " Group Assets)"), EditorStyles.textArea);
						}

						var strandGroupInstances = hairInstance.strandGroupInstances;
						if (strandGroupInstances != null && strandGroupInstances.Length > 0)
						{
							for (int j = 0; j != strandGroupInstances.Length; j++)
							{
								var groupLabel = "Gr." + j;
								var groupAssigned = (strandGroupInstances[j].settingsIndex == i);
								if (groupAssigned)
								{
									using (new ColorScope(Color.green, ColorScope.Type.BackgroundColor))
									{
										if (GUILayout.Toggle(true, groupLabel, EditorStyles.miniButton, GUILayout.ExpandWidth(false)) == false)
										{
											hairInstance.AssignStrandGroupSettings(-1, j);
										}
									}
								}
								else
								{
									using (new ColorScope(Color.Lerp(Color.white, Color.grey, 0.5f), ColorScope.Type.ContentColor))
									{
										if (GUILayout.Toggle(false, groupLabel, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
										{
											hairInstance.AssignStrandGroupSettings(i, j);
										}
									}
								}
							}
						}

						if (true)
						{
							property_delete = GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(widthSymbol));
						}
					}
					EditorGUILayout.EndHorizontal();

					if (property_delete)
					{
						_strandGroupSettings.DeleteArrayElementAtIndex(i--);
						break;
					}

					if (property.isExpanded)
					{
						EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
						{
							using (new GovernedByPrefabScope(hairInstance))
							{
								StructPropertyFieldsWithHeader(property_settingsSkinning, property_settingsSkinningToggle, ValidationGUISkinning, hairInstance);
							}
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsStrands, property_settingsStrandsToggle, ValidationGUIStrands, hairInstance);
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsSolver, property_settingsSolverToggle, ValidationGUISolver, hairInstance);
						}
						EditorGUILayout.EndVertical();
					}
				}

				if (GUILayout.Button("Add override block ..."))
				{
					var countPrev = _strandGroupSettings.arraySize;
					var countNext = countPrev + 1;

					_strandGroupSettings.arraySize = countNext;
					serializedObject.ApplyModifiedProperties();

					if (countNext == hairInstance.strandGroupSettings.Length)
					{
						hairInstance.strandGroupSettings[countNext - 1] = HairInstance.GroupSettings.defaults;
					}
				}
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		public void DrawVolumeSettingsGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				if (StructPropertyFieldsWithHeader(_settingsVolume))
				{
					using (new EditorGUI.IndentLevelScope())
					{
						var countDiscrete = hairInstance.volumeData.cbuffer._BoundaryCountDiscrete;
						var countCapsule = hairInstance.volumeData.cbuffer._BoundaryCountCapsule;
						var countSphere = hairInstance.volumeData.cbuffer._BoundaryCountSphere;
						var countTorus = hairInstance.volumeData.cbuffer._BoundaryCountTorus;
						var countCube = hairInstance.volumeData.cbuffer._BoundaryCountCube;

						var countAll = countDiscrete + countCapsule + countSphere + countTorus + countCube;
						var countTxt = countAll + " boundaries (" + countDiscrete + " discrete, " + countCapsule + " capsule, " + countSphere + " sphere, " + countTorus + " torus, " + countCube + " cube)";

						EditorGUILayout.LabelField(countTxt, HairGUIStyles.statusBox, GUILayout.ExpandWidth(true));

						var countDiscarded = hairInstance.volumeData.boundaryPrevCountDiscard;
						if (countDiscarded > 0)
						{
							using (new ColorScope(Color.Lerp(Color.red, Color.yellow, 0.5f)))
							{
								EditorGUILayout.LabelField(countDiscarded + " discarded (due to limit of " + HairSim.MAX_BOUNDARIES + ")", HairGUIStyles.statusBox, GUILayout.ExpandWidth(true));
							}
						}
					}

				}

				EditorGUILayout.Space();
				if (StructPropertyFieldsWithHeader(_settingsDebug))
				{
					using (new EditorGUI.IndentLevelScope())
					{
						var divider = hairInstance.settingsDebug.drawSliceDivider;
						var dividerBase = Mathf.Floor(divider);
						var dividerFrac = divider - Mathf.Floor(divider);

						var rect = GUILayoutUtility.GetRect(0.0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));

						rect = EditorGUI.IndentedRect(rect);

						var rect0 = new Rect(rect);
						var rect1 = new Rect(rect);

						rect0.width = (rect.width) * (1.0f - dividerFrac);
						rect1.width = (rect.width) * dividerFrac;
						rect1.x += rect0.width;

						string DividerLabel(int index)
						{
							switch (index)
							{
								case 0: return "density";
								case 1: return "rest-density";
								case 2: return "velocity";
								case 3: return "divergence";
								case 4: return "pressure";
								case 5: return "grad(pressure)";
								case 6: return "scattering";
							}
							return "unknown";
						}

						EditorGUILayout.BeginHorizontal();
						{
							GUI.Box(rect0, DividerLabel((int)dividerBase + 0), EditorStyles.helpBox);
							GUI.Box(rect1, DividerLabel((int)dividerBase + 1), EditorStyles.helpBox);
						}
						EditorGUILayout.EndHorizontal();
					}
				}
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}
	}
}
