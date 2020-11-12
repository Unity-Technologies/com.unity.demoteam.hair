using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	using static HairEditorGUI;

	[CustomEditor(typeof(Groom))]
	public class GroomEditor : Editor
	{
		Editor groomAssetEditor;

		SerializedProperty _groomAsset;
		SerializedProperty _groomAssetQuickEdit;

		SerializedProperty _settingsRoots;
		SerializedProperty _settingsRoots_rootsAttached;
		SerializedProperty _settingsStrands;

		SerializedProperty _settingsSolver;
		SerializedProperty _settingsVolume;
		SerializedProperty _settingsDebug;
		SerializedProperty _boundaries;//replace with overlapbox

		void OnEnable()
		{
			_groomAsset = serializedObject.FindProperty("groomAsset");
			_groomAssetQuickEdit = serializedObject.FindProperty("groomAssetQuickEdit");

			_settingsRoots = serializedObject.FindProperty("settingsRoots");
			_settingsRoots_rootsAttached = _settingsRoots.FindPropertyRelative("rootsAttached");
			_settingsStrands = serializedObject.FindProperty("settingsStrands");

			_settingsSolver = serializedObject.FindProperty("solverSettings");
			_settingsVolume = serializedObject.FindProperty("volumeSettings");
			_settingsDebug = serializedObject.FindProperty("debugSettings");
			_boundaries = serializedObject.FindProperty("boundaries");
		}

		void OnDisable()
		{
			if (groomAssetEditor != null)
			{
				DestroyImmediate(groomAssetEditor);
			}
		}

		public override void OnInspectorGUI()
		{
			var groom = target as Groom;
			if (groom == null)
				return;

			EditorGUILayout.LabelField("Instance", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				DrawAssetGUI();
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Strand settings", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				DrawStrandSettingsGUI();
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Simulation settings", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				DrawSimulationSettingsGUI();
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField(groom.groomContainersChecksum, EditorStyles.centeredGreyMiniLabel);
		}

		public void DrawAssetGUI()
		{
			var groom = target as Groom;
			if (groom == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.PropertyField(_groomAsset);
				_groomAssetQuickEdit.boolValue = GUILayout.Toggle(_groomAssetQuickEdit.boolValue, "Quick Edit", EditorStyles.miniButton);
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}

			var groomAsset = groom.groomAsset;
			if (groomAsset != null && _groomAssetQuickEdit.boolValue)
			{
				Editor.CreateCachedEditor(groomAsset, null, ref groomAssetEditor);
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					(groomAssetEditor as GroomAssetEditor).DrawImporterGUI();
				}
				EditorGUILayout.EndVertical();
			}

			if (GUILayout.Button("Reload"))
			{
				groom.groomContainersChecksum = string.Empty;
			}
		}

		public void DrawStrandSettingsGUI()
		{
			var groom = target as Groom;
			if (groom == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				StructPropertyFieldsWithHeader(_settingsRoots);
				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(_settingsStrands);
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		public void DrawSimulationSettingsGUI()
		{
			var groom = target as Groom;
			if (groom == null)
				return;

			if (groom.solverData != null && groom.solverData.Length > 0)
			{
				var strandSolver = groom.solverSettings.method;
				var strandMemoryLayout = groom.solverData[0].memoryLayout;
				var strandParticleCount = groom.solverData[0].cbuffer._StrandParticleCount;

				switch (strandSolver)
				{
					case HairSim.SolverSettings.Method.GaussSeidelReference:
						EditorGUILayout.HelpBox("Performance warning: Using slow reference solver.", MessageType.Warning, wide: true);
						break;

					case HairSim.SolverSettings.Method.GaussSeidel:
						if (strandMemoryLayout != GroomAsset.MemoryLayout.Interleaved)
						{
							EditorGUILayout.HelpBox("Performance warning: Gauss-Seidel solver performs better with memory layout 'Interleaved'.\nConsider changing memory layout in the asset.", MessageType.Warning, wide: true);
						}
						break;

					case HairSim.SolverSettings.Method.Jacobi:
						if (strandParticleCount != 16 &&
							strandParticleCount != 32 &&
							strandParticleCount != 64 &&
							strandParticleCount != 128)
						{
							EditorGUILayout.HelpBox("Configuration error: Jacobi solver requires strand particle count of 16, 32, 64 or 128.\nUsing slow reference solver as fallback. Consider resampling curves in asset.", MessageType.Error, wide: true);
						}
						else if (strandMemoryLayout != GroomAsset.MemoryLayout.Sequential)
						{
							EditorGUILayout.HelpBox("Performance warning: Jacobi solver performs better with memory layout 'Sequential'.\nConsider changing memory layout in the asset.", MessageType.Warning, wide: true);
						}
						break;
				}
			}

			EditorGUI.BeginChangeCheck();
			{
				StructPropertyFieldsWithHeader(_settingsSolver, "Settings Solver");

				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(_settingsVolume, "Settings Volume");
				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUILayout.PropertyField(_boundaries);
				}

				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(_settingsDebug, "Settings Debug");
				using (new EditorGUI.IndentLevelScope())
				{
					var rect = GUILayoutUtility.GetRect(100.0f, 20.0f);

					rect = EditorGUI.IndentedRect(rect);

					var divider = _settingsDebug.FindPropertyRelative("drawSliceDivider").floatValue;
					var dividerBase = Mathf.Floor(divider);
					var dividerFrac = divider - Mathf.Floor(divider);

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
							case 1: return "velocity";
							case 2: return "divergence";
							case 3: return "pressure";
							case 4: return "grad(pressure)";
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
