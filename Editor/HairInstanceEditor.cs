using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	using static HairGUILayout;
	using static HairGUI;

	[CustomEditor(typeof(HairInstance)), CanEditMultipleObjects]
	public class HairInstanceEditor : Editor
	{
		static bool s_indicator;

		Dictionary<HairAsset, Editor> hairAssetEditorMap = new Dictionary<HairAsset, Editor>(1);

		SerializedProperty _settingsExecutive;
		SerializedProperty _settingsDebugging;

		SerializedProperty _strandGroupProviders;
		SerializedProperty _strandGroupSettings;
		SerializedProperty _strandGroupDefaults;

		SerializedProperty _settingsEnvironment;
		SerializedProperty _settingsVolumetrics;

		void OnEnable()
		{
			_settingsExecutive = serializedObject.FindProperty(nameof(HairInstance.settingsExecutive));
			_settingsDebugging = serializedObject.FindProperty(nameof(HairInstance.settingsDebugging));

			_strandGroupProviders = serializedObject.FindProperty(nameof(HairInstance.strandGroupProviders));
			_strandGroupSettings = serializedObject.FindProperty(nameof(HairInstance.strandGroupSettings));
			_strandGroupDefaults = serializedObject.FindProperty(nameof(HairInstance.strandGroupDefaults));

			_settingsEnvironment = serializedObject.FindProperty(nameof(HairInstance.settingsEnvironment));
			_settingsVolumetrics = serializedObject.FindProperty(nameof(HairInstance.settingsVolumetrics));
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
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return new Bounds();
			else
				return HairSim.GetVolumeBounds(hairInstance.volumeData).WithScale(0.5f);
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
				//TODO maybe re-label and re-group like so?
				// Hair Assets
				// Hair Runtime
				// Hair Settings

				EditorGUILayout.LabelField("System Contents", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawSystemContentsGUI(hairInstance);
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("System Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawSystemControlsGUI(hairInstance);
					DrawSystemSettingsGUI(hairInstance);
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Strand Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawStrandSettingsGUI(hairInstance);
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Volume Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawVolumeSettingsGUI(hairInstance);
				}
				EditorGUILayout.EndVertical();

				//EditorGUILayout.Space();
				//EditorGUILayout.LabelField(hairInstance.strandGroupChecksums, EditorStyles.centeredGreyMiniLabel);
			}
			EditorGUILayout.EndVertical();
		}

		void DrawSystemContentsGUI(HairInstance hairInstance)
		{
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
						var property_hairAsset = property.FindPropertyRelative(nameof(HairInstance.GroupProvider.hairAsset));
						var property_hairAssetQuickEdit = property.FindPropertyRelative(nameof(HairInstance.GroupProvider.hairAssetQuickEdit));
						var property_delete = false;

						EditorGUILayout.BeginHorizontal();
						{
							using (new EditorGUI.DisabledScope(property_hairAsset.objectReferenceValue == null))
							{
								property_hairAssetQuickEdit.boolValue = GUILayout.Toggle(property_hairAssetQuickEdit.boolValue, HairGUIStyles.miniButtonIconEdit, EditorStyles.miniButton, GUILayout.Width(widthSymbol));
							}

							EditorGUILayout.ObjectField(property_hairAsset, GUIContent.none);

							if (multipleAssets)//TODO considering: using (new EditorGUI.DisabledScope(multipleAssets == false))
							{
								property_delete = GUILayout.Button(HairGUIStyles.miniButtonIconSub, EditorStyles.miniButton, GUILayout.Width(widthSymbol));
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
									(hairAssetEditor as HairAssetEditor).DrawStrandDataDeclGUI();
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
					if (GUILayout.Button(HairGUIStyles.miniButtonIconAdd, GUILayout.Width(widthSymbol)))
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

			// loaded status
			{
				var countGroups = (hairInstance.solverData != null) ? hairInstance.solverData.Length : 0;
				var countStrands = 0u;
				var countParticles = 0u;

				for (int i = 0; i != countGroups; i++)
				{
					ref readonly var constants = ref hairInstance.solverData[i].constants;
					{
						countStrands += constants._StrandCount;
						countParticles += constants._StrandCount * constants._StrandParticleCount;
					}
				}

				var countTxt = string.Format("Loaded: {0} group(s), {1} strands, {2} particles.", countGroups, countStrands, countParticles);

				EditorGUILayout.LabelField(countTxt, HairGUIStyles.statusBox, GUILayout.ExpandWidth(true));
			}
		}

		void DrawSystemControlsGUI(HairInstance hairInstance)
		{
			EditorGUI.BeginChangeCheck();
			{
				ref var simAllowed = ref hairInstance.settingsExecutive.updateSimulationInEditor;
				ref var simActive = ref hairInstance.settingsExecutive.updateSimulation;

				var simState = (simAllowed && simActive) ? "Running" : (simAllowed ? "Paused" : "Stopped");
				var simTime = hairInstance.execState.elapsedTime;
				var simTxt = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Simulation state: {0}\nElapsed time: {1:F3}s", simState, simTime);

				using (new ColorScope(simActive ? Color.green : Color.yellow, simAllowed ? ColorType.BackgroundColor : ColorType.None))
				{
					EditorGUILayout.LabelField(simTxt, HairGUIStyles.statusBox, GUILayout.ExpandWidth(true));
				}

				EditorGUILayout.BeginHorizontal();
				{
					using (new ColorScope(Color.cyan, Application.isPlaying ? ColorType.ContentColor : ColorType.None))
					{
						simAllowed = GUILayout.Toggle(Application.isPlaying || simAllowed, HairGUIStyles.miniButtonIconPlay, EditorStyles.miniButton);
					}

					simActive = (GUILayout.Toggle(simActive == false, HairGUIStyles.miniButtonIconPause, EditorStyles.miniButton) == false);

					if (GUILayout.Button(HairGUIStyles.miniButtonIconRewind, EditorStyles.miniButton))
					{
						hairInstance.ResetSimulationState();
					}
				}
				EditorGUILayout.EndHorizontal();
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		void DrawSystemSettingsGUI(HairInstance hairInstance)
		{
			EditorGUI.BeginChangeCheck();
			{
				if (StructPropertyFieldsWithHeader(_settingsExecutive, ValidationGUIExecutive, hairInstance))
				{
					using (new EditorGUI.IndentLevelScope())
					{
						var rect = GUILayoutUtility.GetRect(0.0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
						{
							rect = EditorGUI.PrefixLabel(rect, new GUIContent("Update Steps Status"));
						}

						if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
						{
							s_indicator = !s_indicator;
						}

						if (s_indicator)
						{
							var stepsMin = hairInstance.settingsExecutive.updateStepsMin ? hairInstance.settingsExecutive.updateStepsMinValue : 0;
							var stepsMax = hairInstance.settingsExecutive.updateStepsMax ? hairInstance.settingsExecutive.updateStepsMaxValue : (int)Mathf.Ceil(hairInstance.execState.lastStepCountSmooth);
							if (stepsMax == 0)
								stepsMax = 1;

							var stepDT = hairInstance.GetSimulationTimeStep();
							var stepCount = hairInstance.execState.lastStepCountSmooth;

							var rectWidth = rect.width;
							var rectWidthStep = rectWidth / stepsMax;
							var rectWidthCount = Mathf.Min(rectWidth, rectWidthStep * stepCount);

							rect.width = rectWidthCount;
							{
								using (new ColorScope(hairInstance.execState.lastStepCountRaw > hairInstance.execState.lastStepCount ? Color.red : Color.green, ColorType.Color))
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

				EditorGUILayout.Space();
				if (StructPropertyFieldsWithHeader(_settingsDebugging, ValidationGUIDebugging, hairInstance))
				{
					using (new EditorGUI.IndentLevelScope())
					{
						var divider = hairInstance.settingsDebugging.drawSliceDivider;
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
								case 6: return "probes(scattering)";
								case 7: return "impulse(wind+external)";
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

		void DrawStrandSettingsGUI(HairInstance hairInstance)
		{
			const float widthSymbol = 30.0f;

			EditorGUI.BeginChangeCheck();
			{
				// settings default
				{
					var property = _strandGroupDefaults;
					var property_settingsSkinning = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSkinning));
					var property_settingsGeometry = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsGeometry));
					var property_settingsRendering = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsRendering));
					var property_settingsPhysics = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsPhysics));

					EditorGUILayout.BeginHorizontal();
					{
						var expanded = GUILayout.Toggle(property.isExpanded, HairGUIStyles.miniButtonIconEdit, EditorStyles.miniButton, GUILayout.Width(widthSymbol));
						if (expanded != property.isExpanded)
							property.isExpanded = expanded;

						using (var scope = new HairGUILayout.PropertyRectScope(property, "Defaults (All Groups)"))
						{
							EditorGUI.LabelField(scope.position, scope.label, EditorStyles.textArea);
						}
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
							StructPropertyFieldsWithHeader(property_settingsGeometry, ValidationGUIGeometry, hairInstance);
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsRendering, ValidationGUIRendering, hairInstance);
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsPhysics, ValidationGUIPhysics, hairInstance);
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
					var property_settingsGeometry = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsGeometry));
					var property_settingsRendering = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsRendering));
					var property_settingsPhysics = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsPhysics));
					var property_settingsSkinningToggle = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsSkinningToggle));
					var property_settingsGeometryToggle = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsGeometryToggle));
					var property_settingsRenderingToggle = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsRenderingToggle));
					var property_settingsPhysicsToggle = property.FindPropertyRelative(nameof(HairInstance.GroupSettings.settingsPhysicsToggle));
					var property_delete = false;

					EditorGUILayout.BeginHorizontal();
					{
						var expanded = GUILayout.Toggle(property.isExpanded, HairGUIStyles.miniButtonIconEdit, EditorStyles.miniButton, GUILayout.Width(widthSymbol));
						if (expanded != property.isExpanded)
							property.isExpanded = expanded;

						var blockCount = property_groupAssetReferences.arraySize;
						var blockLabel = "Overrides (" + blockCount + (blockCount == 1 ? " Group Asset)" : " Group Assets)");
						{
							using (var scope = new HairGUILayout.PropertyRectScope(property, blockLabel))
							{
								EditorGUI.LabelField(scope.position, scope.label, EditorStyles.textArea);
							}
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
									using (new ColorScope(Color.green, ColorType.BackgroundColor))
									{
										if (GUILayout.Toggle(true, groupLabel, EditorStyles.miniButton, GUILayout.ExpandWidth(false)) == false)
										{
											hairInstance.AssignStrandGroupSettings(-1, j);
										}
									}
								}
								else
								{
									using (new ColorScope(Color.Lerp(Color.white, Color.grey, 0.5f), ColorType.ContentColor))
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
							property_delete = GUILayout.Button(HairGUIStyles.miniButtonIconSub, EditorStyles.miniButton, GUILayout.Width(widthSymbol));
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
							StructPropertyFieldsWithHeader(property_settingsGeometry, property_settingsGeometryToggle, ValidationGUIGeometry, hairInstance);
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsRendering, property_settingsRenderingToggle, ValidationGUIRendering, hairInstance);
							EditorGUILayout.Space();
							StructPropertyFieldsWithHeader(property_settingsPhysics, property_settingsPhysicsToggle, ValidationGUIPhysics, hairInstance);
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

		void DrawVolumeSettingsGUI(HairInstance hairInstance)
		{
			EditorGUI.BeginChangeCheck();
			{
				if (StructPropertyFieldsWithHeader(_settingsEnvironment, ValidationGUIEnvironment, hairInstance))
				{
					using (new EditorGUI.IndentLevelScope())
					{
						ref readonly var delimDiscrete = ref hairInstance.volumeData.constantsEnvironment._BoundaryDelimDiscrete;
						ref readonly var delimCapsule = ref hairInstance.volumeData.constantsEnvironment._BoundaryDelimCapsule;
						ref readonly var delimSphere = ref hairInstance.volumeData.constantsEnvironment._BoundaryDelimSphere;
						ref readonly var delimTorus = ref hairInstance.volumeData.constantsEnvironment._BoundaryDelimTorus;
						ref readonly var delimCube = ref hairInstance.volumeData.constantsEnvironment._BoundaryDelimCube;

						var countDiscrete = delimDiscrete;
						var countCapsule = delimCapsule - delimDiscrete;
						var countSphere = delimSphere - delimCapsule;
						var countTorus = delimTorus - delimSphere;
						var countCube = delimCube - delimTorus;

						var countAll = countDiscrete + countCapsule + countSphere + countTorus + countCube;
						var countTxt = string.Format("{0} boundaries ({1} discrete, {2} capsule, {3} sphere, {4} torus, {5} cube)", countAll, countDiscrete, countCapsule, countSphere, countTorus, countCube);

						EditorGUILayout.LabelField(countTxt, HairGUIStyles.statusBox, GUILayout.ExpandWidth(true));

						var countDiscarded = hairInstance.volumeData.boundaryCountDiscard;
						if (countDiscarded > 0)
						{
							using (new ColorScope(Color.Lerp(Color.red, Color.yellow, 0.5f)))
							{
								EditorGUILayout.LabelField(string.Format("{0} discarded (due to limit of {1})", countDiscarded, HairSim.Conf.MAX_BOUNDARIES), HairGUIStyles.statusBox, GUILayout.ExpandWidth(true));
							}
						}
					}
				}

				EditorGUILayout.Space();
				if (StructPropertyFieldsWithHeader(_settingsVolumetrics, ValidationGUIVolumetrics, hairInstance))
				{
					// ...
				}
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		//TODO update validation callbacks for updated settings & data

		static StructValidation ValidationGUIExecutive(object userData) => StructValidation.Pass;
		static StructValidation ValidationGUIDebugging(object userData) => StructValidation.Pass;

		static StructValidation ValidationGUISkinning(object userData)
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			return StructValidation.Pass;
#else
			EditorGUILayout.HelpBox("Root attachments require package: 'com.unity.demoteam.digital-human >= 0.1.1-preview'.", MessageType.None, wide: true);
			return StructValidation.Inaccessible;
#endif
		}

		static StructValidation ValidationGUIGeometry(object userData) => StructValidation.Pass;
		static StructValidation ValidationGUIRendering(object userData)
		{
			var hairInstance = userData as HairInstance;
			{
				//if (hairInstance.settingsSystem.strandRenderer == HairInstance.SettingsSystem.StrandRenderer.HDRPHighQualityLines)
				/*TODO
				if (false)
				{
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
					var strandGroupInstances = hairInstance.strandGroupInstances;
					if (strandGroupInstances != null)
					{
						//TODO warn when at least one assigned material is not a valid hair material
						//see HairRenderer.IsHairMaterial(...)
					}
#else
					EditorGUILayout.HelpBox(string.Format("Configuration warning: '{0}' requires package: 'com.unity.render-pipelines.high-definition >= 15.0.2'. Using '{1}' as fallback.", HairSim.RenderSettings.Renderer.HDRPHighQualityLines, HairSim.RenderSettings.Renderer.BuiltinLines), MessageType.Warning, wide: true);
#endif
				}
				*/
			}

			return StructValidation.Pass;
		}

		static StructValidation ValidationGUIPhysics(object userData)
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

		static StructValidation ValidationGUIEnvironment(object userData) => StructValidation.Pass;
		static StructValidation ValidationGUIVolumetrics(object userData) => StructValidation.Pass;
	}
}
