using UnityEditor;

public static class HairEditorGUI
{
	public static void StructPropertyFields(SerializedProperty settings)
	{
		if (settings.hasChildren)
		{
			var itSiblings = settings.Copy();
			var itChildren = settings.Copy();

			itSiblings.Next(enterChildren: false);

			if (itChildren.NextVisible(enterChildren: true))
			{
				EditorGUILayout.PropertyField(itChildren, includeChildren: true);

				while (itChildren.NextVisible(enterChildren: false))
				{
					if (SerializedProperty.EqualContents(itSiblings, itChildren))
						break;

					EditorGUILayout.PropertyField(itChildren, includeChildren: true);
				}
			}
		}
	}

	public static void StructPropertyFieldsWithHeader(SerializedProperty settings, string label)
	{
		EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
		using (new EditorGUI.IndentLevelScope())
		{
			StructPropertyFields(settings);
		}
	}

	public static void StructPropertyFieldsWithHeader(SerializedProperty settings)
	{
		StructPropertyFieldsWithHeader(settings, settings.displayName);
	}
}
