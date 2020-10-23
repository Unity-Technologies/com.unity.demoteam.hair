using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	using static HairEditorGUI;

	[CustomEditor(typeof(GroomProperties))]
	public class GroomPropertiesEditor : Editor
	{
		SerializedProperty _settingsStrand;
		SerializedProperty _settingsSolver;
		SerializedProperty _settingsVolume;

		void OnEnable()
		{
			_settingsStrand = serializedObject.FindProperty("settingsStrand");
			_settingsSolver = serializedObject.FindProperty("settingsSolver");
			_settingsVolume = serializedObject.FindProperty("settingsVolume");
		}

		public override void OnInspectorGUI()
		{
			var groomProps = target as GroomProperties;
			if (groomProps == null)
				return;

			EditorGUILayout.LabelField("Strand solver", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				StructPropertyFieldsWithHeader(_settingsStrand);
				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(_settingsSolver);
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Strand volume", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				StructPropertyFieldsWithHeader(_settingsVolume);
			}
			EditorGUILayout.EndVertical();
		}
	}
}
