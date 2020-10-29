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

		SerializedProperty _settingsSolver;
		SerializedProperty _settingsVolume;
		SerializedProperty _settingsDebug;
		SerializedProperty _boundaries;//replace with overlapbox

		SerializedProperty _settingsRoots;
		SerializedProperty _settingsRender;

		void OnEnable()
		{
			_groomAsset = serializedObject.FindProperty("groomAsset");
			_groomAssetQuickEdit = serializedObject.FindProperty("groomAssetQuickEdit");

			_settingsRoots = serializedObject.FindProperty("settingsRoots");
			_settingsRender = serializedObject.FindProperty("settingsRender");

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
				DrawStrandGroupsGUI();
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

		public void DrawStrandGroupsGUI()
		{
			var groom = target as Groom;
			if (groom == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				StructPropertyFieldsWithHeader(_settingsRoots);
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
			}

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}
	}
}
