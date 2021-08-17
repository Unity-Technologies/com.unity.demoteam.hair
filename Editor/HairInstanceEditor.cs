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

		Editor hairAssetEditor;

		SerializedProperty _hairAsset;
		SerializedProperty _hairAssetQuickEdit;

		SerializedProperty _settingsRoots;
		SerializedProperty _settingsStrands;

		SerializedProperty _settingsSolver;
		SerializedProperty _settingsVolume;
		SerializedProperty _settingsDebug;

		void OnEnable()
		{
			_hairAsset = serializedObject.FindProperty(nameof(HairInstance.hairAsset));
			_hairAssetQuickEdit = serializedObject.FindProperty(nameof(HairInstance.hairAssetQuickEdit));

			_settingsRoots = serializedObject.FindProperty(nameof(HairInstance.settingsRoots));
			_settingsStrands = serializedObject.FindProperty(nameof(HairInstance.settingsStrands));

			_settingsSolver = serializedObject.FindProperty(nameof(HairInstance.solverSettings));
			_settingsVolume = serializedObject.FindProperty(nameof(HairInstance.volumeSettings));
			_settingsDebug = serializedObject.FindProperty(nameof(HairInstance.debugSettings));
		}

		void OnDisable()
		{
			if (hairAssetEditor != null)
			{
				DestroyImmediate(hairAssetEditor);
			}
		}

		public bool HasFrameBounds()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
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
				EditorGUILayout.LabelField("Strand Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawStrandSettingsGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Simulation Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawSimulationSettingsGUI();
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField(hairInstance.strandGroupInstancesChecksum, EditorStyles.centeredGreyMiniLabel);
			}
			EditorGUILayout.EndVertical();
		}

		static StructValidation ValidationGUIStrands(object userData)
		{
			var hairInstance = userData as HairInstance;
			{
				var material = hairInstance.GetStrandMaterial();
				if (material != null && !material.HasProperty("HAIRMATERIALTAG"))
				{
					EditorGUILayout.HelpBox("Configuration warning: Active material does not define property 'HAIRMATERIALTAG'.", MessageType.Warning, wide: true);
				}
			}

			return StructValidation.Pass;
		}

		static StructValidation ValidationGUISolver(object userData)
		{
			var hairInstance = userData as HairInstance;
			if (hairInstance.solverData != null && hairInstance.solverData.Length > 0)
			{
				var strandSolver = hairInstance.solverSettings.method;
				var strandMemoryLayout = hairInstance.solverData[0].memoryLayout;
				var strandParticleCount = hairInstance.solverData[0].cbuffer._StrandParticleCount;

				switch (strandSolver)
				{
					case HairSim.SolverSettings.Method.GaussSeidelReference:
						EditorGUILayout.HelpBox("Performance warning: Using slow reference solver.", MessageType.Warning, wide: true);
						break;

					case HairSim.SolverSettings.Method.GaussSeidel:
						if (strandMemoryLayout != HairAsset.MemoryLayout.Interleaved)
						{
							EditorGUILayout.HelpBox("Performance warning: Gauss-Seidel solver performs better with memory layout 'Interleaved'. This can be solved by changing memory layout in the asset.", MessageType.Warning, wide: true);
						}
						break;

					case HairSim.SolverSettings.Method.Jacobi:
						if (strandParticleCount != 16 &&
							strandParticleCount != 32 &&
							strandParticleCount != 64 &&
							strandParticleCount != 128)
						{
							EditorGUILayout.HelpBox("Configuration error: Jacobi solver requires strand particle count of 16, 32, 64, 128. Using slow reference solver as fallback. This can be solved by resampling curves in the asset.", MessageType.Error, wide: true);
						}
						else if (strandMemoryLayout != HairAsset.MemoryLayout.Sequential)
						{
							EditorGUILayout.HelpBox("Performance warning: Jacobi solver performs better with memory layout 'Sequential'. This can be solved by changing memory layout in the asset.", MessageType.Warning, wide: true);
						}
						break;
				}
			}

			return StructValidation.Pass;
		}

		public void DrawInstanceGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			using (new GovernedByPrefabScope(hairInstance))
			{
				EditorGUI.BeginChangeCheck();
				{
					EditorGUILayout.PropertyField(_hairAsset);
					_hairAssetQuickEdit.boolValue = GUILayout.Toggle(_hairAssetQuickEdit.boolValue, "Quick Edit", EditorStyles.miniButton);
				}

				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();
				}

				var hairAsset = hairInstance.hairAsset;
				if (hairAsset != null && _hairAssetQuickEdit.boolValue)
				{
					Editor.CreateCachedEditor(hairAsset, typeof(HairAssetEditor), ref hairAssetEditor);
					EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
					{
						(hairAssetEditor as HairAssetEditor).DrawImporterGUI();
					}
					EditorGUILayout.EndVertical();
				}

				EditorGUILayout.BeginHorizontal();
				{
					if (GUILayout.Button("Reload"))
					{
						hairInstance.strandGroupInstancesChecksum = string.Empty;
					}

					if (GUILayout.Button("Unlock", GUILayout.Width(60.0f)))
					{
						var strandGroupInstances = hairInstance.strandGroupInstances;
						if (strandGroupInstances != null)
						{
							foreach (var strandGroupInstance in hairInstance.strandGroupInstances)
							{
								strandGroupInstance.container.hideFlags &= ~HideFlags.NotEditable;
								strandGroupInstance.rootFilter.gameObject.hideFlags &= ~HideFlags.NotEditable;
								strandGroupInstance.strandFilter.gameObject.hideFlags &= ~HideFlags.NotEditable;
							}
						}
					}
				}
				EditorGUILayout.EndHorizontal();
			}
		}

		public void DrawStrandSettingsGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				using (new GovernedByPrefabScope(hairInstance))
				{
					StructPropertyFieldsWithHeader(_settingsRoots);
				}
#if !HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUILayout.HelpBox("Root attachments require package: 'com.unity.demoteam.digital-human >= 0.1.1-preview'.", MessageType.None, wide: true);
				}
#endif

				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(_settingsStrands, ValidationGUIStrands, hairInstance);

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
						var stepsMin = hairInstance.settingsStrands.stepsMin ? hairInstance.settingsStrands.stepsMinValue : 0;
						var stepsMax = hairInstance.settingsStrands.stepsMax ? hairInstance.settingsStrands.stepsMaxValue : (int)Mathf.Ceil(hairInstance.stepsLastFrameSmooth);
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

				//if (GUILayout.Button("Reset simulation"))
				//{
				//	hairInstance.ResetSimulationState();
				//}
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		public void DrawSimulationSettingsGUI()
		{
			var hairInstance = target as HairInstance;
			if (hairInstance == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				StructPropertyFieldsWithHeader(_settingsSolver, "Settings Solver", ValidationGUISolver, hairInstance);

				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(_settingsVolume, "Settings Volume");
				using (new EditorGUI.IndentLevelScope())
				{
					var countDiscrete = hairInstance.volumeData.cbuffer._BoundaryCountDiscrete;
					var countCapsule = hairInstance.volumeData.cbuffer._BoundaryCountCapsule;
					var countSphere = hairInstance.volumeData.cbuffer._BoundaryCountSphere;
					var countTorus = hairInstance.volumeData.cbuffer._BoundaryCountTorus;
					var countCube = hairInstance.volumeData.cbuffer._BoundaryCountCube;

					var countAll = countDiscrete + countCapsule + countSphere + countTorus + countCube;
					var countTxt = countAll + " boundaries (" + countDiscrete + " discrete, " + countCapsule + " capsule, " + countSphere + " sphere, " + countTorus + " torus, " + countCube + " cube)";

					var rectHeight = HairGUIStyles.statusBox.CalcHeight(new GUIContent(string.Empty), 0.0f);
					var rect = GUILayoutUtility.GetRect(0.0f, rectHeight, GUILayout.ExpandWidth(true));

					using (new ColorScope(Color.white))
					{
						GUI.Box(EditorGUI.IndentedRect(rect), countTxt, HairGUIStyles.statusBox);
					}

					var countDiscarded = hairInstance.volumeData.boundaryPrevCountDiscard;
					if (countDiscarded > 0)
					{
						rect = GUILayoutUtility.GetRect(0.0f, rectHeight, GUILayout.ExpandWidth(true));

						using (new ColorScope(Color.Lerp(Color.red, Color.yellow, 0.5f)))
						{
							GUI.Box(EditorGUI.IndentedRect(rect), countDiscarded + " discarded (due to limit of " + HairSim.MAX_BOUNDARIES + ")", HairGUIStyles.statusBox);
						}
					}
				}

				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(_settingsDebug, "Settings Debug");
				using (new EditorGUI.IndentLevelScope())
				{
					var divider = hairInstance.debugSettings.drawSliceDivider;
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

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}
	}
}
