using UnityEngine;
using UnityEditor;

public static class HairGUIStyles
{
	public static readonly GUIStyle settingsBox = new GUIStyle(EditorStyles.helpBox);
}

public static class HairGUILayout
{
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

	public static void StructPropertyFieldsWithHeader(SerializedProperty property, string label)
	{
		EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
		using (new EditorGUI.IndentLevelScope())
		{
			// settings.isExpanded
			StructPropertyFields(property);
		}
	}

	public static void StructPropertyFieldsWithHeader(SerializedProperty property)
	{
		StructPropertyFieldsWithHeader(property, property.displayName);
	}
}
