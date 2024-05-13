using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	using static HairGUILayout;
	using static HairGUI;

	[CustomEditor(typeof(HairBoundary)), CanEditMultipleObjects]
	public class HairBoundaryEditor : Editor
	{
		SerializedProperty _settings;
		SerializedProperty _settings_mode;
		SerializedProperty _settings_type;

		SerializedProperty _settingsCapsule;
		SerializedProperty _settingsSphere;
		SerializedProperty _settingsTorus;
		SerializedProperty _settingsCube;
		SerializedProperty _settingsSDF;

		void OnEnable()
		{
			_settings = serializedObject.FindProperty(nameof(HairBoundary.settings));
			_settings_mode = _settings.FindPropertyRelative(nameof(HairBoundary.settings.mode));
			_settings_type = _settings.FindPropertyRelative(nameof(HairBoundary.settings.type));

			_settingsCapsule = serializedObject.FindProperty(nameof(HairBoundary.settingsCapsule));
			_settingsSphere = serializedObject.FindProperty(nameof(HairBoundary.settingsSphere));
			_settingsTorus = serializedObject.FindProperty(nameof(HairBoundary.settingsTorus));
			_settingsCube = serializedObject.FindProperty(nameof(HairBoundary.settingsCube));
			_settingsSDF = serializedObject.FindProperty(nameof(HairBoundary.settingsSDF));
		}

		public override bool UseDefaultMargins()
		{
			return false;
		}

		public override void OnInspectorGUI()
		{
			var hairBoundary = target as HairBoundary;
			if (hairBoundary == null)
				return;

			EditorGUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
			{
				EditorGUILayout.LabelField("Boundary Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawBoundaryGUI();
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndVertical();
		}

		public void DrawBoundaryGUI()
		{
			var hairBoundary = target as HairBoundary;
			if (hairBoundary == null)
				return;

			EditorGUI.BeginChangeCheck();

			StructPropertyFieldsWithHeader(_settings, "Settings Shape");

			var selectedMode = (HairBoundary.Settings.Mode)_settings_mode.intValue;
			var selectedType = (HairBoundary.Settings.Type)_settings_type.intValue;

			//TODO move to ValidationGUI
			if (selectedMode == HairBoundary.Settings.Mode.BindToComponent && selectedType == HairBoundary.Settings.Type.DiscreteSDF)
			{
#if !HAS_PACKAGE_DEMOTEAM_MESHTOSDF
				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUILayout.HelpBox("Binding to SDF component requires package: 'com.unity.demoteam.mesh-to-sdf >= 1.0.0'.", MessageType.None, wide: true);
				}
#endif
			}

			if (selectedMode == HairBoundary.Settings.Mode.BindToComponent)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					if (HairBoundary.TryGetMatchingComponent(hairBoundary, out var component))
					{
						using (new ColorScope(Color.white))
						using (new ColorScope(Color.green, ColorType.BackgroundColor))
						{
							GUILayout.Label("Bound to component: " + component.GetType().Name, HairGUIStyles.statusBox, GUILayout.ExpandWidth(true));
							//TODO change to EditorGUILayout.LabelField to preserve indent level?
						}
					}
					else
					{
						EditorGUILayout.HelpBox("No matching components for the selected type.", MessageType.Warning, wide: true);
					}
				}
			}
			else
			{
				if (selectedType == HairBoundary.Settings.Type.Any)
				{
					using (new EditorGUI.IndentLevelScope())
					{
						EditorGUILayout.HelpBox("Type '" + HairBoundary.Settings.Type.Any + "' requires mode '" + ObjectNames.NicifyVariableName(HairBoundary.Settings.Mode.BindToComponent.ToString()) + "'.", MessageType.Warning, wide: true);
					}
				}
				else
				{ 
					EditorGUILayout.Space();

					switch (selectedType)
					{
						case HairBoundary.Settings.Type.DiscreteSDF:
							StructPropertyFieldsWithHeader(_settingsSDF);
							break;
						case HairBoundary.Settings.Type.Capsule:
							StructPropertyFieldsWithHeader(_settingsCapsule);
							break;
						case HairBoundary.Settings.Type.Sphere:
							StructPropertyFieldsWithHeader(_settingsSphere);
							break;
						case HairBoundary.Settings.Type.Torus:
							StructPropertyFieldsWithHeader(_settingsTorus);
							break;
						case HairBoundary.Settings.Type.Cube:
							StructPropertyFieldsWithHeader(_settingsCube);
							break;
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
