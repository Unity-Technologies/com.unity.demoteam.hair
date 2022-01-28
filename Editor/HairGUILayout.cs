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

			propertyEnd.Next(enterChildren: false);

			for (int i = 0; propertyIt.NextVisible(enterChildren: i == 0); i++)
			{
				if (SerializedProperty.EqualContents(propertyIt, propertyEnd))
					break;

				EditorGUILayout.PropertyField(propertyIt, includeChildren: true);
			}
		}

		public static void StructPropertyFields(SerializedProperty property, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			var validationResult = (validationGUI != null) ? validationGUI(validationUserData) : StructValidation.Pass;
			using (new EditorGUI.DisabledScope(validationResult == StructValidation.Inaccessible))
			{
				StructPropertyFields(property);
			}
		}

		public static void StructPropertyFieldsWithHeader(SerializedProperty property, string label, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
#if false
			var expanded = EditorGUILayout.Foldout(property.isExpanded, label, toggleOnLabelClick: true, HairGUIStyles.settingsFoldout);
			if (expanded)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					StructPropertyFields(property, validationGUI, validationUserData);
				}
			}
			property.isExpanded = expanded;//TODO changecheck
#else
			EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
			using (new EditorGUI.IndentLevelScope())
			{
				StructPropertyFields(property, validationGUI, validationUserData);
			}
#endif
		}

		public static void StructPropertyFieldsWithHeader(SerializedProperty property, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			StructPropertyFieldsWithHeader(property, property.displayName, validationGUI, validationUserData);
		}
	}
}
