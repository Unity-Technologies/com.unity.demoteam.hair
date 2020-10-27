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

		void OnEnable()
		{
			_groomAsset = serializedObject.FindProperty("groomAsset");
			_groomAssetQuickEdit = serializedObject.FindProperty("groomAssetQuickEdit");

			_settingsSolver = serializedObject.FindProperty("solverSettings");
			_settingsVolume = serializedObject.FindProperty("volumeSettings");
			_settingsDebug = serializedObject.FindProperty("debugSettings");
		}

		public override void OnInspectorGUI()
		{
			var groom = target as Groom;
			if (groom == null)
				return;

			EditorGUILayout.LabelField("Instance", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
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
					(groomAssetEditor as GroomAssetEditor).DrawImporterGUI(groomAsset);
					EditorGUILayout.EndVertical();
				}

				if (GUILayout.Button("Reload"))
				{
					groom.groomChecksum = string.Empty;
				}
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Strand groups", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				GUILayout.Label("[ + summary ]");
				GUILayout.Label("[ + root attachments ]");
				GUILayout.Label("[ + per group overrides? ]");
			}
			EditorGUILayout.EndVertical();

			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Simulation settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					StructPropertyFieldsWithHeader(_settingsSolver, "Settings Solver");
					EditorGUILayout.Space();
					StructPropertyFieldsWithHeader(_settingsVolume, "Settings Volume");
					EditorGUILayout.Space();
					StructPropertyFieldsWithHeader(_settingsDebug, "Settings Debug");
				}
				EditorGUILayout.EndVertical();
			}
			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}
	}
}
