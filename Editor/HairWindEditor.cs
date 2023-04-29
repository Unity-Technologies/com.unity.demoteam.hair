using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	using static HairGUILayout;
	using static HairGUI;

	[CustomEditor(typeof(HairWind)), CanEditMultipleObjects]
	public class HairWindEditor : Editor
	{
		SerializedProperty _settingsEmitter;
		SerializedProperty _settingsDirectional;
		SerializedProperty _settingsSpherical;
		SerializedProperty _settingsTurbine;
		SerializedProperty _settingsFlow;

		void OnEnable()
		{
			_settingsEmitter = serializedObject.FindProperty(nameof(HairWind.settingsEmitter));
			_settingsDirectional = serializedObject.FindProperty(nameof(HairWind.settingsDirectional));
			_settingsSpherical = serializedObject.FindProperty(nameof(HairWind.settingsSpherical));
			_settingsTurbine = serializedObject.FindProperty(nameof(HairWind.settingsTurbine));
			_settingsFlow = serializedObject.FindProperty(nameof(HairWind.settingsFlow));
		}

		public override bool UseDefaultMargins()
		{
			return false;
		}

		public override void OnInspectorGUI()
		{
			var hairWind = target as HairWind;
			if (hairWind == null)
				return;

			EditorGUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
			{
				EditorGUILayout.LabelField("Wind Settings", EditorStyles.centeredGreyMiniLabel);
				EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
				{
					DrawWindGUI();
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndVertical();
		}

		public void DrawWindGUI()
		{
			var hairWind = target as HairWind;
			if (hairWind == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				StructPropertyFieldsWithHeader(_settingsEmitter);

				if (hairWind.settingsEmitter.mode == HairWind.SettingsEmitter.Mode.BindToComponent)
				{
					using (new EditorGUI.IndentLevelScope())
					{
						if (HairWind.TryGetMatchingComponent(hairWind, out var component))
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
					if (hairWind.settingsEmitter.type == HairWind.SettingsEmitter.Type.Any)
					{
						using (new EditorGUI.IndentLevelScope())
						{
							EditorGUILayout.HelpBox("Type '" + HairWind.SettingsEmitter.Type.Any + "' requires mode '" + ObjectNames.NicifyVariableName(HairWind.SettingsEmitter.Mode.BindToComponent.ToString()) + "'.", MessageType.Warning, wide: true);
						}
					}
					else
					{
						EditorGUILayout.Space();

						var expanded = false;

						switch (hairWind.settingsEmitter.type)
						{
							case HairWind.SettingsEmitter.Type.Directional:
								expanded = StructPropertyFieldsWithHeader(_settingsDirectional);
								break;
							case HairWind.SettingsEmitter.Type.Spherical:
								expanded = StructPropertyFieldsWithHeader(_settingsSpherical);
								break;
							case HairWind.SettingsEmitter.Type.Turbine:
								expanded = StructPropertyFieldsWithHeader(_settingsTurbine);
								break;
						}

						if (expanded)
						{
							using (new EditorGUI.IndentLevelScope())
							{
								StructPropertyFields(_settingsFlow);
							}
						}
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
