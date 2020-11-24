using UnityEngine;
using UnityEditor;

public static class HairGUIStyles
{
	public static readonly GUIStyle settingsBox;

	public static readonly GUIStyle statusBox;
	public static readonly GUIStyle statusBoxVertical;

	static HairGUIStyles()
	{
		settingsBox = new GUIStyle(EditorStyles.helpBox);

		statusBox = new GUIStyle(GUI.skin.box);
		statusBox.normal.textColor = statusBox.hover.textColor;

		statusBoxVertical = new GUIStyle(EditorStyles.textField);
		statusBoxVertical.font = EditorStyles.miniFont;
		statusBoxVertical.fontSize = 8;
		statusBoxVertical.wordWrap = true;
	}
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
			StructPropertyFields(property);
		}
	}

	public static void StructPropertyFieldsWithHeader(SerializedProperty property)
	{
		StructPropertyFieldsWithHeader(property, property.displayName);
	}

	public struct ColorScope : System.IDisposable
	{
		public Color restoreColor;
		public Type restoreColorType;

		public enum Type
		{
			Color,
			ContentColor,
			BackgroundColor,
		}

		static Color GetColor(Type colorType)
		{
			switch (colorType)
			{
				default:
				case Type.Color: return GUI.color;
				case Type.ContentColor: return GUI.contentColor;
				case Type.BackgroundColor: return GUI.backgroundColor;
			}
		}

		static void SetColor(Type colorType, in Color color)
		{
			switch (colorType)
			{
				default:
				case Type.Color: GUI.color = color; break;
				case Type.ContentColor: GUI.contentColor = color; break;
				case Type.BackgroundColor: GUI.backgroundColor = color; break;
			}
		}

		public ColorScope(Color color, Type colorType = Type.Color)
		{
			this.restoreColor = GetColor(colorType);
			this.restoreColorType = colorType;

			SetColor(colorType, color);
		}

		public void Dispose()
		{
			SetColor(restoreColorType, restoreColor);
		}
	}

	public struct GovernedByPrefabScope : System.IDisposable
	{
		public bool isPrefabInstance;
		public EditorGUI.DisabledScope disableScope;

		public GovernedByPrefabScope(bool isPrefabInstance)
		{
			this.isPrefabInstance = isPrefabInstance;
			if (isPrefabInstance)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.BeginVertical();
			}

			disableScope = new EditorGUI.DisabledScope(isPrefabInstance);
		}

		public GovernedByPrefabScope(Object componentOrGameObject) : this(PrefabUtility.IsPartOfPrefabInstance(componentOrGameObject))
		{
			// foo
		}

		public void Dispose()
		{
			disableScope.Dispose();

			if (isPrefabInstance)
			{
				EditorGUILayout.EndVertical();
				using (new ColorScope(Color.Lerp(Color.red, Color.white, 0.75f), ColorScope.Type.BackgroundColor))
				{
					GUILayout.Label("prefab", HairGUIStyles.statusBoxVertical, GUILayout.Width(20.0f), GUILayout.ExpandHeight(true));
				}
				EditorGUILayout.EndHorizontal();
			}
		}
	}
}
