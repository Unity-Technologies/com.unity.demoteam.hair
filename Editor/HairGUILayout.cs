using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static class HairGUILayout
	{
		public enum StructValidation
		{
			Pass,
			Inaccessible,
		}

		public delegate StructValidation StructValidationGUI(object userData);

		public static void StructPropertyFields(SerializedProperty property)
		{
			if (property.hasChildren)
			{
				var nextSibling = property.Copy();
				var nextChild = property.Copy();

				nextSibling.Next(enterChildren: false);

				if (nextChild.NextVisible(enterChildren: true))
				{
					EditorGUILayout.PropertyField(nextChild, includeChildren: true);

					while (nextChild.NextVisible(enterChildren: false))
					{
						if (SerializedProperty.EqualContents(nextSibling, nextChild))
							break;

						EditorGUILayout.PropertyField(nextChild, includeChildren: true);
					}
				}
			}
		}

		public static void StructPropertyFieldsWithHeader(SerializedProperty property, string label, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
			using (new EditorGUI.IndentLevelScope())
			{
				var validationResult = (validationGUI != null) ? validationGUI(validationUserData) : StructValidation.Pass;
				using (new EditorGUI.DisabledScope(validationResult == StructValidation.Inaccessible))
				{
					StructPropertyFields(property);
				}
			}
		}

		public static void StructPropertyFieldsWithHeader(SerializedProperty property, StructValidationGUI validationGUI = null, object validationUserData = null)
		{
			StructPropertyFieldsWithHeader(property, property.displayName, validationGUI, validationUserData);
		}
	}
}
