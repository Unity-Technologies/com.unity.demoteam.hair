using System;
using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairGUILayout
	{
		public enum StructValidation
		{
			Pass,
			Inaccessible,
		}

		public delegate StructValidation StructValidationGUI(object userData);

		public static void StructPropertyFields(SerializedProperty property)
		{
			var propertyIt = property.Copy();
			var propertyEnd = property.Copy();

			propertyEnd.NextVisible(enterChildren: false);

			for (int i = 0; propertyIt.NextVisible(enterChildren: i == 0); i++)
			{
				if (SerializedProperty.EqualContents(propertyIt, propertyEnd))
					break;

				EditorGUILayout.PropertyField(propertyIt, includeChildren: true);
			}
		}

		public static void StructPropertyFields(SerializedProperty property, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			var validationResult = validationGUI?.Invoke(validationUserData) ?? StructValidation.Pass;
			{
				using (new EditorGUI.DisabledScope(validationResult == StructValidation.Inaccessible))
				{
					StructPropertyFields(property);
				}
			}
		}

		[Flags]
		public enum StructHeaderFlags
		{
			DefaultParts = Expand,
			DefaultState = Expand | Toggle,
			Expand = 1 << 0,
			Toggle = 1 << 1,
		}

		public static StructHeaderFlags StructHeader(StructHeaderFlags parts, StructHeaderFlags state, string label, string labelToggle = null)
		{
			EditorGUILayout.BeginHorizontal(HairGUIStyles.settingsHeader);

			if (parts.HasFlag(StructHeaderFlags.Expand))
			{
				if (EditorGUILayout.Foldout(state.HasFlag(StructHeaderFlags.Expand), label, toggleOnLabelClick: true, HairGUIStyles.settingsFoldout))
					state |= StructHeaderFlags.Expand;
				else
					state &= ~StructHeaderFlags.Expand;
			}
			else
			{
				EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
				state |= StructHeaderFlags.Expand;
			}

			if (parts.HasFlag(StructHeaderFlags.Toggle))
			{
				GUILayout.Label(labelToggle ?? "Enabled", EditorStyles.miniLabel, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(false));

				if (GUILayout.Toggle(state.HasFlag(StructHeaderFlags.Toggle), GUIContent.none, GUILayout.Width(16.0f), GUILayout.ExpandHeight(true)))
					state |= StructHeaderFlags.Toggle;
				else
					state &= ~StructHeaderFlags.Toggle;
			}
			else
			{
				state |= StructHeaderFlags.Toggle;
			}

			EditorGUILayout.EndHorizontal();

			return state;
		}

		public static bool StructHeader(string label)
		{
			var state = StructHeader(
				parts: StructHeaderFlags.DefaultParts,
				state: StructHeaderFlags.DefaultState,
				label: label);

			return state.HasFlag(StructHeaderFlags.Expand);
		}

		public static StructHeaderFlags StructPropertyFieldsWithHeader(SerializedProperty property, StructHeaderFlags parts, StructHeaderFlags state, string label = null, string labelToggle = null, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			if (property.isExpanded)
				state |= StructHeaderFlags.Expand;
			else
				state &= ~StructHeaderFlags.Expand;

			state = StructHeader(parts, state, label ?? property.displayName, labelToggle);

			var expand = state.HasFlag(StructHeaderFlags.Expand);
			if (expand != property.isExpanded)
			{
				property.isExpanded = expand;
			}

			EditorGUI.BeginProperty(GUILayoutUtility.GetLastRect(), null, property);
			EditorGUI.EndProperty();// add context menu for copy/paste/revert/etc.

			if (expand)
			{
				using (new EditorGUI.DisabledScope(state.HasFlag(StructHeaderFlags.Toggle) == false))
				using (new EditorGUI.IndentLevelScope())
				{
					StructPropertyFields(property, validationGUI, validationUserData);
				}
			}

			return state;
		}

		public static bool StructPropertyFieldsWithHeader(SerializedProperty property, StructValidationGUI validationGUI = null, object validationUserData = null) => StructPropertyFieldsWithHeader(property, null as string, validationGUI, validationUserData);
		public static bool StructPropertyFieldsWithHeader(SerializedProperty property, string label, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			var state = StructPropertyFieldsWithHeader(property,
				parts: StructHeaderFlags.DefaultParts,
				state: StructHeaderFlags.DefaultState,
				label: label,
				validationGUI: validationGUI,
				validationUserData: validationUserData);

			return state.HasFlag(StructHeaderFlags.Expand);
		}


		public static bool StructPropertyFieldsWithHeader(SerializedProperty property, ref bool toggle, StructValidationGUI validationGUI = null, object validationUserData = null) => StructPropertyFieldsWithHeader(property, null, ref toggle, validationGUI, validationUserData);
		public static bool StructPropertyFieldsWithHeader(SerializedProperty property, string label, ref bool toggle, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			var state = StructPropertyFieldsWithHeader(property,
				parts: StructHeaderFlags.DefaultParts | StructHeaderFlags.Toggle,
				state: (StructHeaderFlags.DefaultParts & ~StructHeaderFlags.Toggle) | (toggle ? StructHeaderFlags.Toggle : 0),
				label: label,
				validationGUI: validationGUI,
				validationUserData: validationUserData);

			toggle = state.HasFlag(StructHeaderFlags.Toggle);
			return state.HasFlag(StructHeaderFlags.Expand);
		}

		public static bool StructPropertyFieldsWithHeader(SerializedProperty property, SerializedProperty toggleProperty, StructValidationGUI validationGUI = null, object validationUserData = null) => StructPropertyFieldsWithHeader(property, null, toggleProperty, validationGUI, validationUserData);
		public static bool StructPropertyFieldsWithHeader(SerializedProperty property, string label, SerializedProperty toggleProperty, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			var toggle = toggleProperty.boolValue;
			var expand = StructPropertyFieldsWithHeader(property, label, ref toggle, validationGUI, validationUserData);

			if (toggleProperty.boolValue != toggle)
				toggleProperty.boolValue = toggle;

			return expand;
		}
	}
}
