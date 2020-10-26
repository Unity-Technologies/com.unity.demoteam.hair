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
		SerializedProperty _settingsSolver;
		SerializedProperty _settingsVolume;

		void OnEnable()
		{
			_groomAsset = serializedObject.FindProperty("groomAsset");
			_settingsSolver = serializedObject.FindProperty("solverSettings");
			_settingsVolume = serializedObject.FindProperty("volumeSettings");
		}

		public override void OnInspectorGUI()
		{
			var groom = target as Groom;
			if (groom == null)
				return;

			EditorGUILayout.LabelField("Asset", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				EditorGUI.BeginChangeCheck();
				{
					EditorGUILayout.PropertyField(_groomAsset);
				}
				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();
				}

				if (GUILayout.Button("Reload"))
				{
					groom.groomChecksum = string.Empty;
				}
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.LabelField("Strands", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Simulation", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					StructPropertyFieldsWithHeader(_settingsSolver);
					EditorGUILayout.Space();
					StructPropertyFieldsWithHeader(_settingsVolume);
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
